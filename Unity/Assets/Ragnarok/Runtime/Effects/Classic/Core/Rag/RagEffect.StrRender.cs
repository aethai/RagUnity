// Synced from reference/port/src/RoClassicFx/Rag/RagEffect.StrRender.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::vf03 (0xb5d5e0) — Render: every visible STR layer except layer 0.
        /// <paramref name="viewMatrix"/> is the renderer's 4x3 view matrix.
        /// </summary>
        public void Render(FxRuntime rt, uint viewMatrix)
        {
            if (!rt.IsEffectVisible(this)) return;
            uint str = M.U32(A + 0x7f0);
            if (str == 0 || M.U32(A + 0x138) == 0) return;
            int order = 0;
            uint state = A + 0x868;
            uint layerOff = 0x380;
            for (int i = 1; i < M.I32(str + 8); i++, state += 0x74, layerOff += 0x380)
            {
                if (M.U8(A + 0x119fc + (uint)i) == 0) continue;
                RenderStrLayer(rt, str + 0x10 + layerOff, state, order, viewMatrix);
                order += 2;
                str = M.U32(A + 0x7f0);
            }
        }

        /// <summary>
        /// CRagEffect::fn_b5d690 (0xb5d690) — one STR layer as a screen-space quad anchored at the
        /// projected master position, scaled by xfactor·rhw/7 (1 STR pixel = 1/7 world unit),
        /// rotated by angle/2.8444 degrees, drawn at constant depth on top of the scene.
        /// </summary>
        void RenderStrLayer(FxRuntime rt, uint layer, uint p, int order, uint viewMatrix)
        {
            var r = rt.Renderer;
            uint master = M.U32(A + 0x138);
            r.ProjectVertex2(M, viewMatrix, M.F32(master + 0x10), M.F32(master + 0x14), M.F32(master + 0x18),
                out _, out float sy, out float rhw);
            float scale = r.ScreenScale(rhw);
            uint quad = r.AcquireQuad();
            uint v = M.U32(quad);
            var mem = M;
            float P(int i) => mem.F32(p + (uint)i * 4);
            void V(int i, float value) => mem.F32(v + (uint)i * 4, value);

            float u;
            if (P(0xb) <= P(0xa))
            {
                V(8, P(0xb)); V(0xe, P(4) + P(2)); V(0x18, P(0xa)); V(0x1e, P(2));
                V(0, P(0xc)); V(6, P(4) + P(2)); V(0x10, P(0xd)); u = P(2);
            }
            else
            {
                V(8, P(0xa)); V(0xe, P(2)); V(0x18, P(0xb)); V(0x1e, P(4) + P(2));
                V(0, P(0xd)); V(6, P(2)); V(0x10, P(0xc)); u = P(4) + P(2);
            }
            V(0x16, u);
            float t;
            if (P(0x10) <= P(0xf))
            {
                V(0x19, P(0x10)); V(0x1f, P(5) + P(3)); V(0x11, P(0xf)); V(0x17, P(3));
                V(9, P(0x11)); V(0xf, P(5) + P(3)); V(1, P(0xe)); t = P(3);
            }
            else
            {
                V(0x19, P(0xf)); V(0x1f, P(3)); V(0x11, P(0x10)); V(0x17, P(5) + P(3));
                V(9, P(0xe)); V(0xf, P(3)); V(1, P(0x11)); t = P(5) + P(3);
            }
            V(7, t);

            int frame = FxOps.Cvtt(P(0x12));
            if (frame < 0) frame = 0;
            uint tex = rt.GetLayerTexture(layer, frame);
            float ru = (float)(double)M.U32(tex + 0x14) / (float)(double)M.U32(tex + 0xc);
            float rv = (float)(double)M.U32(tex + 0x18) / (float)(double)M.U32(tex + 0x10);
            for (int k = 0; k < 4; k++)
            {
                V(6 + k * 8, ru * M.F32(v + (uint)(6 + k * 8) * 4));
                V(7 + k * 8, rv * M.F32(v + (uint)(7 + k * 8) * 4));
            }

            float rad = (float)((double)P(0x15) / 2.8444444444444) / 180.0f * 3.141592f;
            float sin = (float)Math.Sin(rad);
            float cos = (float)Math.Cos(rad);
            for (int k = 0; k < 4; k++)
            {
                uint vx = v + (uint)k * 0x20;
                float x = M.F32(vx), y = M.F32(vx + 4);
                float rot = y * cos + x * sin;
                M.F32(vx, ((x * cos - y * sin) + (P(0) - 320.0f)) * scale + f_154);
                if (!rt.FlipScreenY)
                    M.F32(vx + 4, ((P(1) - 240.0f) + rot) * scale + f_158);
                else
                {
                    float d = sy - f_158;
                    M.F32(vx + 4, (f_158 - ((P(1) - 240.0f) + rot) * scale) + (d + d));
                }
            }

            int argb = ((FxOps.Cvtt(P(0x19)) * 0x100 + (FxOps.Cvtt(P(0x16)) & 0xff)) * 0x100
                        + (FxOps.Cvtt(P(0x17)) & 0xff)) * 0x100 + (FxOps.Cvtt(P(0x18)) & 0xff);
            for (int k = 0; k < 4; k++)
            {
                uint vx = v + (uint)k * 0x20;
                M.I32(vx + 0x10, argb);
                M.U32(vx + 0x14, 0xffffffff);
                M.U32(vx + 0x8, 0x38021cc8);    // constant depth ≈ 3.1e-5: always in front
                M.F32(vx + 0xc, rhw);
            }
            M.U32(quad + 8, tex);
            M.U32(quad + 0x18, M.U32(p + 0x1a * 4));   // src blend
            M.U32(quad + 0x1c, M.U32(p + 0x1b * 4));   // dst blend
            M.U32(quad + 0x20, M.U32(p + 0x1c * 4));   // mtpreset
            r.AddQuad(quad, 5);
        }
    }
}
