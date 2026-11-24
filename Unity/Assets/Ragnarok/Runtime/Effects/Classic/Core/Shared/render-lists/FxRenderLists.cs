// Synced from reference/port/src/RoClassicFx/Shared/render-lists/FxRenderLists.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The client's render lists: one std::vector per list in the renderer object, named here by their offset.
    /// AddRP (0x537b00) files an RP into one of them by its flags; CRenderer::Flush (0x539f90, DX7; the DX9
    /// Flush 0x545430 has the same list order and explicit states) draws them in a fixed order with fixed states.
    /// See packages/render-lists/NOTES.md for the table.
    /// </summary>
    public static class FxRenderList
    {
        public const int Opaque = 0x15c;            // flags without bit 0 (default): alpha test 0xcf, no blend, z-write; sorted by texture
        public const int Ground = 0x168;            // !1, 0x800
        public const int GroundLightmap = 0x174;    // !1, 0x1000 (ONE / SRCALPHA lightmap pass)
        public const int AlphaTwoPass = 0x180;      // 1|0x100: alpha ≥ 0x9f with z-write, then < 0x9f without
        public const int Alpha = 0x18c;             // 1, RP +0x14 == 1 (every pooled RP): over, z-write = +0x14 && +0x15
        public const int AlphaNoDepth = 0x198;      // 1|0x200: over, no depth test, submission order (UI quads too)
        public const int AlphaB14 = 0x1a4;          // 1, RP +0x14 != 1: only drawn when renderer +0x48 == 1 (never)
        public const int OpaqueWrap = 0x1b0;        // !1, 0x20: as Opaque, WRAP addressing
        public const int AlphaWrap = 0x1bc;         // 1|0x20: as Alpha, WRAP addressing
        public const int AdditiveZWrite = 0x1c8;    // 1|2|0x100: ONE/ONE with depth writes
        public const int Additive = 0x1d4;          // 1|2: ONE/ONE, no z-write, no fog
        public const int AdditiveNoDepth = 0x1e0;   // 1|2|0x200: ONE/ONE, no depth test
        public const int OwnBlend = 0x1ec;          // 1|4: RP blend + texture-op preset, WRAP, submission order
        public const int GroundMultiTex = 0x1f8;    // ground + lightmap in one pass (filled by 0x537a40, not AddRP)
        public const int Unused204 = 0x204;         // filled by 0x537a40(rp, 1); never drawn
        public const int OwnBlendNoFog = 0x210;     // 1|4|8: RP blend + preset, no fog, no z-write, submission order
        public const int AlphaNoDepthSorted = 0x21c;// 1|0x200|8: over, no depth test, sorted
        public const int AlphaRefCf = 0x228;        // 1|0x10: over, alpha ref 0xcf
        public const int OwnBlendNoDepth = 0x234;   // 1|4|0x200: RP blend (key ≥ 0.99 → over) + preset, no depth test, submission order
        public const int MultiTex = 0x240;          // 2-texture effect quads (CEvilLandEffect via 0x537ab0, not AddRP)

        /// <summary>AddRP (0x537b00): the list an RP with <paramref name="flags"/> and byte +0x14 goes to.</summary>
        public static int Route(int flags, byte b14)
        {
            if ((flags & 1) != 0)
            {
                if ((flags & 4) != 0)
                {
                    if ((flags & 8) != 0) return OwnBlendNoFog;
                    return (flags & 0x200) != 0 ? OwnBlendNoDepth : OwnBlend;
                }
                if ((flags & 2) != 0)
                {
                    if ((flags & 0x100) != 0) return AdditiveZWrite;
                    return (flags & 0x200) != 0 ? AdditiveNoDepth : Additive;
                }
                if ((flags & 0x10) != 0) return AlphaRefCf;
                if ((flags & 0x100) != 0) return AlphaTwoPass;
                if ((flags & 0x200) != 0) return (flags & 8) != 0 ? AlphaNoDepthSorted : AlphaNoDepth;
                if ((flags & 0x20) != 0) return AlphaWrap;
                return b14 == 1 ? Alpha : AlphaB14;
            }
            if ((flags & 0x800) != 0) return Ground;
            if ((flags & 0x1000) != 0) return GroundLightmap;
            return (flags & 0x20) != 0 ? OpaqueWrap : Opaque;
        }

        /// <summary>True for the lists holding {float key, RP*} pairs (AddRP computes a depth key for them).</summary>
        public static bool IsKeyed(int list) => list switch
        {
            AlphaTwoPass or Alpha or AlphaNoDepth or AlphaB14 or AlphaWrap or AdditiveZWrite or Additive or AdditiveNoDepth
                or AlphaNoDepthSorted or AlphaRefCf or OwnBlendNoDepth => true,
            _ => false,
        };

        /// <summary>
        /// AddRP's key (0x537b00): rhw of vertex 0 for the default alpha lists (0x18c / 0x1a4 / 0x1bc), else the
        /// rhw midpoint of vertices 0 and 1 computed as (rhw0 − rhw1)·0.5 + rhw1 (SSE single, this order).
        /// </summary>
        public static float Key(int list, float rhw0, float rhw1)
            => list == Alpha || list == AlphaB14 || list == AlphaWrap ? rhw0 : (rhw0 - rhw1) * 0.5f + rhw1;
    }

    /// <summary>One RP (0x28-byte render primitive) as the lists and Flush see it.</summary>
    public struct FxRenderPrimitive
    {
        /// <summary>The RP object's address: tie-break of the depth sorts. Hosts without heap addresses pass a submission index.</summary>
        public uint Address;
        /// <summary>+0x00 TL vertex array (0x20 bytes per vertex: x, y, z, rhw, diffuse, specular, u, v).</summary>
        public uint Vertices;
        /// <summary>+0x04 vertex count (≤ 0: not drawn).</summary>
        public int VertexCount;
        /// <summary>+0x08 texture object (the per-stage SetTexture cache compares this).</summary>
        public uint Texture;
        /// <summary>Texture object +0x128: the surface SetTexture receives (0 when <see cref="Texture"/> is 0).</summary>
        public uint Surface;
        /// <summary>+0x0c / +0x10 index array and count (count ≠ 0 → DrawIndexedPrimitive).</summary>
        public uint Indices;
        public int IndexCount;
        /// <summary>+0x14 / +0x15 bytes: routing (+0x14 == 1 → list 0x18c) and the z-write of lists 0x18c / 0x1bc.</summary>
        public byte B14, B15;
        /// <summary>+0x18 / +0x1c D3DBLEND source / destination (used by the own-blend lists).</summary>
        public int SrcBlend, DstBlend;
        /// <summary>+0x20 texture-stage preset (0x53ce20; 0..15, others ignored).</summary>
        public int MtPreset;
        /// <summary>+0x24 D3DPRIMITIVETYPE (4 for AddRP faces, 5 for pooled quads).</summary>
        public int PrimitiveType;
        /// <summary>rhw of vertices 0 and 1 (vertex +0x0c, +0x2c): the depth-sort keys.</summary>
        public float Rhw0, Rhw1;
    }

    /// <summary>The device calls CRenderer::Flush makes (IDirect3DDevice7 slots 0x50 / 0x54 / 0x94 / 0x8c / 0x64 / 0x68).</summary>
    public interface IFxD3D7Device
    {
        void SetRenderState(int state, uint value);
        uint GetRenderState(int state);
        void SetTextureStageState(int stage, int type, uint value);
        void SetTexture(int stage, uint surface);
        /// <summary>One RP draw (CRendererDX7::vf14 0x538b10): DrawIndexedPrimitive when IndexCount ≠ 0, else DrawPrimitive.</summary>
        void Draw(int list, int rp, in FxRenderPrimitive p);
    }

    /// <summary>
    /// Render-list model: <see cref="Add"/> = AddRP (0x537b00), <see cref="Flush"/> = CRenderer::Flush (0x539f90)
    /// replayed call by call on an <see cref="IFxD3D7Device"/>, <see cref="Clear"/> = the list reset of 0x5381c0.
    /// Exact against game.exe for everything AddRP feeds (oracle: tests/Packages/render-lists). Not modelled:
    /// the fog parameter upload 0x538360 (runs only when renderer +0x154 is set), lists 0x1f8 / 0x240 (fed by
    /// 0x537a40 / 0x537ab0; treated as empty) and AddRP's vertex-fog pass (renderer +0x158, rewrites specular).
    /// </summary>
    public sealed class FxRenderLists
    {
        /// <summary>Renderer +0x150: FOGENABLE for the fogged lists.</summary>
        public bool Fog;
        /// <summary>DAT_01184300 (lightmaps; 1 in the image) and DAT_01136ec4 (device can multitexture).</summary>
        public bool Lightmaps = true, MultiTexture = true;
        /// <summary>Renderer +0x48 == 1: the two-pass path of 0x538e80 (0 in Custom: only the ctors write it).</summary>
        public bool SpriteTwoPass;

        FxRenderPrimitive[] rps = new FxRenderPrimitive[256];
        int rpCount;
        readonly Dictionary<int, List> lists = new Dictionary<int, List>();
        uint cachedTexture;   // renderer +0x268

        sealed class List
        {
            public Keyed[] Keyed = new Keyed[64];
            public ByTexture[] Plain = new ByTexture[64];
            public int Count;
        }

        struct Keyed { public float Key; public uint Address; public int Rp; }
        struct ByTexture { public uint Texture; public int Rp; }

        struct KeyLess : IFxLess<Keyed>
        {
            // std::pair<float, RP*>::operator< (0x5355f0): partial ordering of the keys, then the pointers
            public bool Less(in Keyed a, in Keyed b) => a.Key < b.Key || (a.Key == b.Key && a.Address < b.Address);
        }

        struct TextureLess : IFxLess<ByTexture>
        {
            // 0x536a10: rp->texture < rp->texture (unsigned)
            public bool Less(in ByTexture a, in ByTexture b) => a.Texture < b.Texture;
        }

        public int PrimitiveCount => rpCount;
        public ref readonly FxRenderPrimitive Primitive(int i) => ref rps[i];

        /// <summary>Entries of a list in their current order (sorted after <see cref="Flush"/>): RP index and key.</summary>
        public IEnumerable<(int Rp, float Key)> Entries(int list)
        {
            if (!lists.TryGetValue(list, out var l)) yield break;
            bool keyed = FxRenderList.IsKeyed(list);
            for (int i = 0; i < l.Count; i++)
                yield return keyed ? (l.Keyed[i].Rp, l.Keyed[i].Key) : (l.Plain[i].Rp, 0f);
        }

        /// <summary>AddRP (0x537b00): files <paramref name="p"/> into its list; returns the list.</summary>
        public int Add(in FxRenderPrimitive p, int flags)
        {
            if (rpCount == rps.Length) Array.Resize(ref rps, rps.Length * 2);
            int rp = rpCount++;
            rps[rp] = p;
            int id = FxRenderList.Route(flags, p.B14);
            var l = Get(id);
            if (FxRenderList.IsKeyed(id))
            {
                if (l.Count == l.Keyed.Length) Array.Resize(ref l.Keyed, l.Count * 2);
                l.Keyed[l.Count++] = new Keyed { Key = FxRenderList.Key(id, p.Rhw0, p.Rhw1), Address = p.Address, Rp = rp };
            }
            else
            {
                if (l.Count == l.Plain.Length) Array.Resize(ref l.Plain, l.Count * 2);
                l.Plain[l.Count++] = new ByTexture { Texture = p.Texture, Rp = rp };
            }
            return id;
        }

        /// <summary>0x5381c0 (after Flush, before EndScene): every list emptied; the texture cache is kept.</summary>
        public void Clear()
        {
            foreach (var l in lists.Values) l.Count = 0;
            rpCount = 0;
        }

        List Get(int id)
        {
            if (!lists.TryGetValue(id, out var l)) lists[id] = l = new List();
            return l;
        }

        int CountOf(int id) => lists.TryGetValue(id, out var l) ? l.Count : 0;

        void SortKeyed(int id) { var l = Get(id); FxMsvcSort.Sort(l.Keyed, 0, l.Count, default(KeyLess)); }
        void SortByTexture(int id) { var l = Get(id); FxMsvcSort.Sort(l.Plain, 0, l.Count, default(TextureLess)); }

        int RpAt(int id, int i)
        {
            var l = lists[id];
            return FxRenderList.IsKeyed(id) ? l.Keyed[i].Rp : l.Plain[i].Rp;
        }

        void Texture(IFxD3D7Device dev, in FxRenderPrimitive p)
        {
            if (cachedTexture == p.Texture) return;
            dev.SetTexture(0, p.Texture != 0 ? p.Surface : 0);
            cachedTexture = p.Texture;
        }

        static void Draw(IFxD3D7Device dev, int list, int rp, in FxRenderPrimitive p)
        {
            if (p.VertexCount > 0) dev.Draw(list, rp, p);   // CRendererDX7::vf14 0x538b10
        }

        /// <summary>Texture + draw for every entry in order (the plain loops of 0x539700 / 0x539960 / 0x538de0 / Flush).</summary>
        void DrawAll(IFxD3D7Device dev, int id)
        {
            int n = CountOf(id);
            for (int i = 0; i < n; i++)
            {
                int rp = RpAt(id, i);
                Texture(dev, rps[rp]);
                Draw(dev, id, rp, rps[rp]);
            }
        }

        /// <summary>The own-blend loops (0x210 / 0x1ec / 0x234): texture, preset, blend, draw.</summary>
        void DrawOwnBlend(IFxD3D7Device dev, int id)
        {
            int n = CountOf(id);
            var l = n > 0 ? lists[id] : null;
            for (int i = 0; i < n; i++)
            {
                int rp = RpAt(id, i);
                ref readonly var p = ref rps[rp];
                Texture(dev, p);
                FxTexturePresets.Apply(dev, p.MtPreset);
                if (id == FxRenderList.OwnBlendNoDepth && l.Keyed[i].Key >= 0.99f)
                {
                    dev.SetRenderState(D3Drs.SrcBlend, 5);
                    dev.SetRenderState(D3Drs.DestBlend, 6);
                }
                else
                {
                    dev.SetRenderState(D3Drs.SrcBlend, (uint)p.SrcBlend);
                    dev.SetRenderState(D3Drs.DestBlend, (uint)p.DstBlend);
                }
                Draw(dev, id, rp, p);
            }
            FxTexturePresets.Apply(dev, 0);
        }

        /// <summary>CRenderer::Flush (0x539f90), DX7, as the sequence of device calls it makes.</summary>
        public void Flush(IFxD3D7Device dev)
        {
            if (CountOf(FxRenderList.GroundMultiTex) != 0 || CountOf(FxRenderList.MultiTex) != 0)
                throw new NotSupportedException("lists 0x1f8 / 0x240 are not modelled");
            dev.SetTextureStageState(0, D3Dtss.MipFilter, 3);
            // 0x538360: fog parameters, skipped while renderer +0x154 is clear (assumed here)
            dev.SetTextureStageState(0, D3Dtss.ColorOp, 4);
            dev.SetTextureStageState(0, D3Dtss.AlphaOp, 4);
            dev.SetTextureStageState(1, D3Dtss.ColorOp, 1);
            dev.SetTextureStageState(1, D3Dtss.AlphaOp, 1);
            dev.SetRenderState(D3Drs.FogEnable, Fog ? 1u : 0u);
            dev.SetRenderState(D3Drs.AlphaRef, 0xcf);
            dev.SetRenderState(D3Drs.AlphaTestEnable, 1);
            dev.SetRenderState(D3Drs.AlphaBlendEnable, 0);
            dev.SetRenderState(D3Drs.ZEnable, 1);
            dev.SetRenderState(D3Drs.ZWriteEnable, 1);
            // 0x539700: opaque lists, sorted by texture
            dev.SetTextureStageState(0, D3Dtss.Address, 3);
            SortByTexture(FxRenderList.Opaque);
            DrawAll(dev, FxRenderList.Opaque);
            dev.SetTextureStageState(0, D3Dtss.Address, 1);
            SortByTexture(FxRenderList.OpaqueWrap);
            DrawAll(dev, FxRenderList.OpaqueWrap);
            dev.SetTextureStageState(0, D3Dtss.Address, 3);
            dev.SetRenderState(D3Drs.Ambient, 0xffffffff);
            dev.SetRenderState(D3Drs.CullMode, 2);
            if (Lightmaps)
            {
                if (MultiTexture)
                {
                    dev.SetRenderState(D3Drs.ColorKeyEnable, 0);
                    dev.SetTextureStageState(1, D3Dtss.ColorOp, 0x12);
                    // list 0x1f8 (empty here)
                    dev.SetRenderState(D3Drs.ColorKeyEnable, 1);
                    dev.SetTextureStageState(1, D3Dtss.ColorOp, 1);
                }
                else
                {
                    SortByTexture(FxRenderList.Ground);          // 0x539960
                    DrawAll(dev, FxRenderList.Ground);
                    dev.SetRenderState(D3Drs.ColorKeyEnable, 0);
                    dev.SetTextureStageState(0, D3Dtss.ColorOp, 2);
                    dev.SetTextureStageState(0, D3Dtss.AlphaOp, 2);
                    dev.SetRenderState(D3Drs.AlphaBlendEnable, 1);
                    dev.SetRenderState(D3Drs.AlphaRef, 1);
                    dev.SetRenderState(D3Drs.SrcBlend, 2);
                    dev.SetRenderState(D3Drs.DestBlend, 5);
                    SortByTexture(FxRenderList.GroundLightmap);
                    DrawAll(dev, FxRenderList.GroundLightmap);
                    dev.SetRenderState(D3Drs.ColorKeyEnable, 1);
                    dev.SetTextureStageState(0, D3Dtss.ColorOp, 4);
                    dev.SetTextureStageState(0, D3Dtss.AlphaOp, 4);
                    dev.SetRenderState(D3Drs.AlphaBlendEnable, 0);
                    dev.SetRenderState(D3Drs.AlphaRef, 0xcf);
                }
            }
            else
            {
                SortByTexture(FxRenderList.Ground);
                DrawAll(dev, FxRenderList.Ground);
            }
            dev.SetRenderState(D3Drs.ColorKeyEnable, 0);
            dev.SetRenderState(D3Drs.Ambient, 0);
            dev.SetRenderState(D3Drs.CullMode, 1);
            dev.SetRenderState(D3Drs.AlphaRef, 1);
            dev.SetRenderState(D3Drs.AlphaBlendEnable, 1);
            // 0x539b60: list 0x240 (empty here: no calls)

            uint fog = dev.GetRenderState(D3Drs.FogEnable), zw = dev.GetRenderState(D3Drs.ZWriteEnable);
            dev.SetRenderState(D3Drs.FogEnable, 0);
            dev.SetRenderState(D3Drs.ZWriteEnable, 0);
            dev.SetRenderState(D3Drs.SrcBlend, 2);
            dev.SetRenderState(D3Drs.DestBlend, 2);
            DrawOwnBlend(dev, FxRenderList.OwnBlendNoFog);
            dev.SetRenderState(D3Drs.FogEnable, fog);
            dev.SetRenderState(D3Drs.ZWriteEnable, zw);

            dev.SetRenderState(D3Drs.SrcBlend, 5);
            dev.SetRenderState(D3Drs.DestBlend, 6);
            dev.SetRenderState(D3Drs.ZEnable, 0);
            SortKeyed(FxRenderList.AlphaNoDepthSorted);
            DrawAll(dev, FxRenderList.AlphaNoDepthSorted);
            dev.SetRenderState(D3Drs.ZEnable, 1);

            uint zw2 = dev.GetRenderState(D3Drs.ZWriteEnable), at = dev.GetRenderState(D3Drs.AlphaTestEnable);
            uint aref = dev.GetRenderState(D3Drs.AlphaRef), afunc = dev.GetRenderState(D3Drs.AlphaFunc);
            dev.SetRenderState(D3Drs.AlphaTestEnable, 1);
            dev.SetRenderState(D3Drs.ZWriteEnable, 1);
            dev.SetRenderState(D3Drs.AlphaRef, 0x9f);
            dev.SetRenderState(D3Drs.AlphaFunc, 7);
            SortKeyed(FxRenderList.AlphaTwoPass);                // 0x538de0
            DrawAll(dev, FxRenderList.AlphaTwoPass);
            dev.SetRenderState(D3Drs.ZWriteEnable, 0);
            dev.SetRenderState(D3Drs.AlphaRef, 0x9f);
            dev.SetRenderState(D3Drs.AlphaFunc, 2);
            SortKeyed(FxRenderList.AlphaTwoPass);
            DrawAll(dev, FxRenderList.AlphaTwoPass);
            dev.SetRenderState(D3Drs.ZWriteEnable, zw2);
            dev.SetRenderState(D3Drs.AlphaTestEnable, at);
            dev.SetRenderState(D3Drs.AlphaRef, aref);
            dev.SetRenderState(D3Drs.AlphaFunc, afunc);

            dev.SetRenderState(D3Drs.SrcBlend, 2);
            dev.SetRenderState(D3Drs.DestBlend, 2);
            SortKeyed(FxRenderList.AdditiveZWrite);
            DrawAll(dev, FxRenderList.AdditiveZWrite);
            dev.SetRenderState(D3Drs.ZWriteEnable, 0);
            dev.SetRenderState(D3Drs.SrcBlend, 5);
            dev.SetRenderState(D3Drs.DestBlend, 6);
            DrawDefaultAlpha(dev);                               // 0x538e80

            uint aref2 = dev.GetRenderState(D3Drs.AlphaRef);
            dev.SetRenderState(D3Drs.AlphaRef, 0xcf);
            SortKeyed(FxRenderList.AlphaRefCf);
            DrawAll(dev, FxRenderList.AlphaRefCf);
            dev.SetRenderState(D3Drs.AlphaRef, aref2);
            dev.SetRenderState(D3Drs.FogEnable, 0);
            dev.SetRenderState(D3Drs.SrcBlend, 2);
            dev.SetRenderState(D3Drs.DestBlend, 2);
            SortKeyed(FxRenderList.Additive);
            DrawAll(dev, FxRenderList.Additive);
            dev.SetTextureStageState(0, D3Dtss.Address, 1);
            DrawOwnBlend(dev, FxRenderList.OwnBlend);
            dev.SetTextureStageState(0, D3Dtss.Address, 3);
            dev.SetRenderState(D3Drs.ZEnable, 0);
            dev.SetRenderState(D3Drs.SrcBlend, 2);
            dev.SetRenderState(D3Drs.DestBlend, 2);
            SortKeyed(FxRenderList.AdditiveNoDepth);
            DrawAll(dev, FxRenderList.AdditiveNoDepth);
            dev.SetRenderState(D3Drs.SrcBlend, 5);
            dev.SetRenderState(D3Drs.DestBlend, 6);
            DrawAll(dev, FxRenderList.AlphaNoDepth);              // not sorted
            DrawOwnBlend(dev, FxRenderList.OwnBlendNoDepth);      // not sorted; key only selects the blend
            dev.SetRenderState(D3Drs.SrcBlend, 5);
            dev.SetRenderState(D3Drs.DestBlend, 6);
            dev.SetTextureStageState(0, D3Dtss.MipFilter, 1);
        }

        /// <summary>0x538e80: lists 0x18c (CLAMP) and 0x1bc (WRAP), and 0x1a4 on the (dead) two-pass path.</summary>
        void DrawDefaultAlpha(IFxD3D7Device dev)
        {
            SortKeyed(FxRenderList.Alpha);
            SortKeyed(FxRenderList.AlphaWrap);
            uint zw = dev.GetRenderState(D3Drs.ZWriteEnable), at = dev.GetRenderState(D3Drs.AlphaTestEnable);
            uint aref = dev.GetRenderState(D3Drs.AlphaRef), afunc = dev.GetRenderState(D3Drs.AlphaFunc);
            uint ab = dev.GetRenderState(D3Drs.AlphaBlendEnable), src = dev.GetRenderState(D3Drs.SrcBlend);
            uint dst = dev.GetRenderState(D3Drs.DestBlend);
            dev.SetRenderState(D3Drs.AlphaBlendEnable, 1);
            dev.SetRenderState(D3Drs.SrcBlend, 5);
            dev.SetRenderState(D3Drs.DestBlend, 6);
            dev.SetRenderState(D3Drs.AlphaTestEnable, 1);
            dev.SetRenderState(D3Drs.AlphaRef, 1);
            dev.SetRenderState(D3Drs.AlphaFunc, 7);
            if (SpriteTwoPass)
            {
                dev.SetRenderState(D3Drs.ZWriteEnable, 1);
                dev.SetRenderState(D3Drs.AlphaFunc, 7);
                dev.SetTextureStageState(0, D3Dtss.Address, 3);
                DrawPerRpState(dev, FxRenderList.Alpha, true);       // 0x537270
                dev.SetTextureStageState(0, D3Dtss.Address, 1);
                DrawPerRpState(dev, FxRenderList.AlphaWrap, true);
                dev.SetRenderState(D3Drs.ZWriteEnable, 0);
                dev.SetRenderState(D3Drs.AlphaFunc, 2);
                dev.SetTextureStageState(0, D3Dtss.Address, 3);
                DrawPerRpState(dev, FxRenderList.Alpha, true);
                dev.SetTextureStageState(0, D3Dtss.Address, 1);
                DrawPerRpState(dev, FxRenderList.AlphaWrap, true);
                dev.SetRenderState(D3Drs.AlphaRef, 1);
                dev.SetRenderState(D3Drs.AlphaFunc, 7);
                SortKeyed(FxRenderList.AlphaB14);
                DrawAll(dev, FxRenderList.AlphaB14);
            }
            else
            {
                dev.SetTextureStageState(0, D3Dtss.Address, 3);
                DrawPerRpState(dev, FxRenderList.Alpha, false);      // 0x537330
                dev.SetTextureStageState(0, D3Dtss.Address, 1);
                DrawPerRpState(dev, FxRenderList.AlphaWrap, false);
            }
            dev.SetRenderState(D3Drs.ZWriteEnable, zw);
            dev.SetRenderState(D3Drs.AlphaTestEnable, at);
            dev.SetRenderState(D3Drs.AlphaRef, aref);
            dev.SetRenderState(D3Drs.AlphaFunc, afunc);
            dev.SetRenderState(D3Drs.AlphaBlendEnable, ab);
            dev.SetRenderState(D3Drs.SrcBlend, src);
            dev.SetRenderState(D3Drs.DestBlend, dst);
            dev.SetTextureStageState(0, D3Dtss.Address, 3);
        }

        /// <summary>
        /// 0x537270 (<paramref name="alphaRef"/>: ALPHAREF = 1 when +0x14 and +0x15 are set, else 0xf0) /
        /// 0x537330 (ZWRITEENABLE = +0x14 &amp;&amp; +0x15), then texture and draw, per RP.
        /// </summary>
        void DrawPerRpState(IFxD3D7Device dev, int id, bool alphaRef)
        {
            int n = CountOf(id);
            for (int i = 0; i < n; i++)
            {
                int rp = RpAt(id, i);
                ref readonly var p = ref rps[rp];
                bool both = p.B14 != 0 && p.B15 != 0;
                if (alphaRef) dev.SetRenderState(D3Drs.AlphaRef, both ? 1u : 0xf0u);
                else dev.SetRenderState(D3Drs.ZWriteEnable, both ? 1u : 0u);
                Texture(dev, p);
                Draw(dev, id, rp, p);
            }
        }
    }

    /// <summary>D3DRENDERSTATETYPE values Flush uses.</summary>
    public static class D3Drs
    {
        public const int ZEnable = 7, ZWriteEnable = 14, AlphaTestEnable = 15, SrcBlend = 19, DestBlend = 20, CullMode = 22,
            ZFunc = 23, AlphaRef = 24, AlphaFunc = 25, AlphaBlendEnable = 27, FogEnable = 28, ColorKeyEnable = 41, Ambient = 139;
    }

    /// <summary>D3DTEXTURESTAGESTATETYPE values Flush and the presets use.</summary>
    public static class D3Dtss
    {
        public const int ColorOp = 1, ColorArg1 = 2, ColorArg2 = 3, AlphaOp = 4, AlphaArg1 = 5, AlphaArg2 = 6, TexCoordIndex = 11,
            Address = 12, MagFilter = 16, MinFilter = 17, MipFilter = 18;
    }

    /// <summary>
    /// The texture-stage presets of 0x53ce20 (RP +0x20, STR "mtpreset"): the exact SetTextureStageState writes per
    /// preset (stage, type, value), in call order. Presets write only some arguments, so the stage state they leave
    /// depends on the previous preset (the client's behaviour). Presets 5..15 enable stage 1 (texture coordinates 1),
    /// whose texture the RP path never binds.
    /// </summary>
    public static class FxTexturePresets
    {
        static (int Stage, int Type, uint Value) W(int s, int t, uint v) => (s, t, v);

        public static readonly (int Stage, int Type, uint Value)[][] Writes =
        {
            /* 0 */ new[] { W(0,11,0), W(0,1,4), W(0,2,2), W(0,3,0), W(0,4,4), W(0,5,2), W(0,6,0), W(1,1,1), W(1,4,1) },
            /* 1 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,5,2), W(0,4,2), W(1,1,1), W(1,4,1) },
            /* 2 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,5,2), W(0,4,2), W(1,1,1), W(1,4,1) },
            /* 3 */ new[] { W(0,11,0), W(0,2,2), W(0,1,7), W(0,3,0), W(0,4,3), W(0,6,0), W(1,1,1), W(1,4,1) },
            /* 4 */ new[] { W(0,11,0), W(0,2,2), W(0,1,13), W(0,3,0), W(0,4,3), W(0,6,0), W(1,1,1), W(1,4,1) },
            /* 5 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,2), W(1,1,4), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 6 */ new[] { W(0,11,0), W(0,2,18), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,18), W(1,1,4), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 7 */ new[] { W(0,11,0), W(0,2,2), W(0,1,2), W(0,5,2), W(0,4,2), W(1,11,1), W(1,2,34), W(1,1,4), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 8 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,5,2), W(0,4,2), W(1,11,1), W(1,2,2), W(1,1,7), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 9 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,2), W(1,1,13), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 10 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,2), W(1,1,12), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 11 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,2), W(1,1,7), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 12 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,18), W(1,1,7), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 13 */ new[] { W(0,11,0), W(0,2,2), W(0,1,7), W(0,3,0), W(0,5,2), W(0,4,2), W(1,11,1), W(1,2,2), W(1,1,4), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 14 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,2), W(1,1,5), W(1,3,1), W(1,5,2), W(1,4,2) },
            /* 15 */ new[] { W(0,11,0), W(0,2,2), W(0,1,4), W(0,3,0), W(0,4,3), W(0,6,0), W(1,11,1), W(1,2,2), W(1,1,8), W(1,3,1), W(1,5,2), W(1,4,2) },
        };

        /// <summary>0x53ce20: applies preset <paramref name="preset"/> (unsigned &gt; 15: nothing).</summary>
        public static void Apply(IFxD3D7Device dev, int preset)
        {
            if ((uint)preset > 15) return;
            foreach (var (s, t, v) in Writes[preset]) dev.SetTextureStageState(s, t, v);
        }
    }
}
