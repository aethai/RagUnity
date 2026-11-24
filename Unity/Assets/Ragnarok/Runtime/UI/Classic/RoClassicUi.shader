// RO classic UI. Atlas = Texture2DArray of raw (gamma-space) bytes. Vertex: pixel position + anchor slot,
// colour, (u, v, page). Pass 0 blends straight into the sRGB target (exact for opaque pixels); pass 1 renders
// premultiplied gamma-space colour into a raw 8-bit target; pass 2 composites that over the scene in gamma space;
// passes 3 / 4 blend a window drawn into its own surface (pass 1) once with its group alpha, into the UI layer
// (premultiplied) or straight into the camera target.
Shader "Hidden/RO/ClassicUi" {
    Properties {
        _RoUiSharpScale ("Screen pixels per texel", Float) = 1
    }
    SubShader {
        Tags { "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        ZWrite Off
        ZTest Always
        Cull Off

        CGINCLUDE
        #pragma target 4.5
        #include "UnityCG.cginc"

        UNITY_DECLARE_TEX2DARRAY(_RoUiAtlas);
        StructuredBuffer<float4> _RoUiAnchors;
        struct RoUiVertex { float x; float y; float anchor; uint color; float u; float v; float page; };
        StructuredBuffer<RoUiVertex> _RoUiVertices;
        float _RoUiSharpScale;

        struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; };

        float3 RoToLinear(float3 c) { return lerp(pow((c + 0.055) / 1.055, 2.4), c / 12.92, step(c, 0.04045)); }
        float3 RoToGamma(float3 c) { return lerp(1.055 * pow(max(c, 0), 1.0 / 2.4) - 0.055, c * 12.92, step(c, 0.0031308)); }

        v2f vertUi(uint id : SV_VertexID) {
            v2f o;
            RoUiVertex v = _RoUiVertices[id];
            float4 a = _RoUiAnchors[(uint) v.anchor];
            float2 p = a.w > 0 ? float2(v.x, v.y) + a.xy : float2(-100000, -100000);
            o.pos = mul(UNITY_MATRIX_VP, float4(p, 0, 1));
            o.color = float4(v.color & 0xFF, (v.color >> 8) & 0xFF, (v.color >> 16) & 0xFF, v.color >> 24) / 255.0;
            o.uv = float3(v.u, v.v, v.page);
            return o;
        }

        // classic window bitmaps reach the screen as A1R5G5B5 tiles: 8-bit value >> 3, sampled back as x / 31
        float3 RoQuantize555(float3 c) {
            float3 bytes = floor(saturate(c) * 255.0 + 0.5);
            // 5 -> 8 bits by bit replication (x << 3 | x >> 2), measured on the original client's screen:
            // gauge border (20,25,155) shows as (16,24,156), not round(x * 255 / 31) = (16,25,156)
            float3 x5 = floor(bytes / 8.0);
            return (x5 * 8.0 + floor(x5 / 4.0)) / 255.0;
        }

        float4 SampleUi(v2f i) {
            float2 uv = i.uv.xy;
            #if RO_SHARP_BILINEAR
            float2 size = float2(2048, 2048);
            float2 texel = uv * size;
            float2 fl = floor(texel);
            float2 s = texel - fl - 0.5;
            float2 r = 0.5 - 0.5 / _RoUiSharpScale;
            float2 f = (s - clamp(s, -r, r)) * _RoUiSharpScale + 0.5;
            uv = (fl + f) / size;
            #endif
            bool quantise = i.uv.z >= 0;
            float page = quantise ? i.uv.z : -i.uv.z - 1;
            float4 c = UNITY_SAMPLE_TEX2DARRAY(_RoUiAtlas, float3(uv, page)) * i.color;
            if (quantise) c.rgb = RoQuantize555(c.rgb);
            return c;
        }
        ENDCG

        Pass { // 0: direct into the sRGB camera target
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertUi
            #pragma fragment frag
            #pragma multi_compile_local _ RO_SHARP_BILINEAR
            float4 frag(v2f i) : SV_Target {
                float4 c = SampleUi(i);
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb = RoToLinear(c.rgb);
                #endif
                return c;
            }
            ENDCG
        }

        Pass { // 1: premultiplied gamma-space colour into a raw 8-bit layer (blends like the original)
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertUi
            #pragma fragment frag
            #pragma multi_compile_local _ RO_SHARP_BILINEAR
            float4 frag(v2f i) : SV_Target {
                float4 c = SampleUi(i);
                return float4(c.rgb * c.a, c.a);
            }
            ENDCG
        }

        Pass { // 2: composite the layer over the scene in gamma space
            Blend Off
            CGPROGRAM
            #pragma vertex vertFull
            #pragma fragment frag
            Texture2D<float4> _RoUiLayerTex;
            Texture2D<float4> _RoUiSceneTex;
            float4 _RoUiTargetInfo; // w, h, scene rows mirrored, layer rows mirrored
            struct v2fFull { float4 pos : SV_POSITION; };
            v2fFull vertFull(uint id : SV_VertexID) {
                v2fFull o;
                float2 p = float2((id << 1) & 2, id & 2);
                o.pos = float4(p * 2 - 1, 0, 1);
                return o;
            }
            float4 frag(v2fFull i) : SV_Target {
                // same memory layout as the target: read the texel under this pixel
                int2 p = (int2) i.pos.xy;
                int2 mirrored = int2(p.x, (int) _RoUiTargetInfo.y - 1 - p.y);
                float4 ui = _RoUiLayerTex.Load(int3(_RoUiTargetInfo.w > 0 ? mirrored : p, 0));
                float3 scene = _RoUiSceneTex.Load(int3(_RoUiTargetInfo.z > 0 ? mirrored : p, 0)).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                float3 g = ui.rgb + RoToGamma(scene) * (1 - ui.a);
                return float4(RoToLinear(g), 1);
                #else
                return float4(ui.rgb + scene * (1 - ui.a), 1);
                #endif
            }
            ENDCG
        }

        Pass { // 3: a window surface (premultiplied gamma) over the UI layer, times its group alpha
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertGroup
            #pragma fragment frag
            Texture2D<float4> _RoUiGroupTex;
            float4 _RoUiGroupInfo; // w, h, rows mirrored, alpha
            struct v2fGroup { float4 pos : SV_POSITION; };
            v2fGroup vertGroup(uint id : SV_VertexID) {
                v2fGroup o;
                float2 p = float2((id << 1) & 2, id & 2);
                o.pos = float4(p * 2 - 1, 0, 1);
                return o;
            }
            float4 frag(v2fGroup i) : SV_Target {
                int2 p = (int2) i.pos.xy;
                int2 mirrored = int2(p.x, (int) _RoUiGroupInfo.y - 1 - p.y);
                return _RoUiGroupTex.Load(int3(_RoUiGroupInfo.z > 0 ? mirrored : p, 0)) * _RoUiGroupInfo.w;
            }
            ENDCG
        }

        Pass { // 4: a window surface (premultiplied gamma) straight into the sRGB camera target, times its group alpha
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertGroup
            #pragma fragment frag
            Texture2D<float4> _RoUiGroupTex;
            float4 _RoUiGroupInfo; // w, h, rows mirrored, alpha
            struct v2fGroup { float4 pos : SV_POSITION; };
            v2fGroup vertGroup(uint id : SV_VertexID) {
                v2fGroup o;
                float2 p = float2((id << 1) & 2, id & 2);
                o.pos = float4(p * 2 - 1, 0, 1);
                return o;
            }
            float4 frag(v2fGroup i) : SV_Target {
                int2 p = (int2) i.pos.xy;
                int2 mirrored = int2(p.x, (int) _RoUiGroupInfo.y - 1 - p.y);
                float4 c = _RoUiGroupTex.Load(int3(_RoUiGroupInfo.z > 0 ? mirrored : p, 0));
                float3 rgb = c.a > 0 ? c.rgb / c.a : 0;
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = RoToLinear(rgb);
                #endif
                return float4(rgb, c.a * _RoUiGroupInfo.w);
            }
            ENDCG
        }
    }
}
