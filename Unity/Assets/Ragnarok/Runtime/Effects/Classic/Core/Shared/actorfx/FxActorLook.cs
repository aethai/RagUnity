// Synced from reference/port/src/RoClassicFx/Shared/actorfx/FxActorLook.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// What the classic effects ask the actor renderer to do this frame, decoded from the owner shim (flag word +0xe8,
    /// counters +0xf0 .. +0xfa, tint +0x98 .. +0x9b) with the client's own formulas. For hosts with their own sprite
    /// renderer; the exact per-layer draws come from <see cref="FxActorLayerPipeline"/>. Semantics: ACTOR_LOOK.md.
    /// </summary>
    public struct FxActorLook
    {
        /// <summary>+0xe8 as the renderer reads it (bits set by every effect of the actor since the last render).</summary>
        public uint Flags;
        /// <summary>+0xf4 (short): the phase of flags 0x20 / 0x40 / 0x80 / 0x100 / 0x1000 / 0x2000 / 0x10000.</summary>
        public short Counter;
        /// <summary>+0xf0 / +0xf1: shrink (0x10) and grow (0x400) phases, 0..45.</summary>
        public byte ShrinkPhase, GrowPhase;
        /// <summary>+0xf2: alpha of the 0x800 / 0x80000 / 2 aura copies.</summary>
        public byte AuraAlpha;
        /// <summary>+0xf6: glow phase (0x4000 / 0x40000) or landing phase (0x20000).</summary>
        public byte GlowPhase;
        /// <summary>Tint (SetARGB, +0x9b a, +0x9a r, +0x99 g, +0x98 b) — multiplies the ACT clip colour of every layer.</summary>
        public uint Tint;
        /// <summary>Sprite scale factor of FUN_00be5810 (flags 0x10 / 0x400): 1 = unchanged.</summary>
        public float Scale;
        /// <summary>Flag 1: body layers blend additively (D3D SRCALPHA / ONE) with the tint as vertex colour.</summary>
        public bool Additive;
        /// <summary>Flag 0x80: every layer is lifted by this fraction of its own height (0..0.3).</summary>
        public float Lift;
        /// <summary>Flag 0x20: layers are squashed towards their bottom edge; see <see cref="FxRuntime.SquashFactor"/> (random 0.35..0.4 per layer while the counter is 4..33).</summary>
        public bool Squash;
        /// <summary>Flag 0x40: the angle (degrees) that replaces every layer's clip angle; null = no spin.</summary>
        public float? Spin;
        /// <summary>Flag 0x100: white additive copy of each layer, this vertex colour (alpha in the top byte); 0 = not drawn.</summary>
        public uint Flash;
        /// <summary>Flag 0x4000 / 0x40000: blue / red glow — each layer drawn twice more, additive, this vertex colour; 0 = not drawn.</summary>
        public uint BlueGlow, RedGlow;
        /// <summary>Flag 0x2000: layer edges jitter (±10 generic, ±5 players) × 400 / zoom px every frame.</summary>
        public bool Shake;

        public bool Has(uint bit) => (Flags & bit) != 0;

        /// <summary>Decodes the actor shim at <paramref name="actor"/> (pure: no rand(), no counters advanced).</summary>
        public static FxActorLook Decode(FxRuntime rt, uint actor)
        {
            var M = rt.M;
            var look = new FxActorLook
            {
                Flags = M.U32(actor + 0xe8),
                Counter = M.I16(actor + 0xf4),
                ShrinkPhase = M.U8(actor + 0xf0),
                GrowPhase = M.U8(actor + 0xf1),
                AuraAlpha = M.U8(actor + 0xf2),
                GlowPhase = M.U8(actor + 0xf6),
                Tint = M.U32(actor + 0x98),
            };
            float scale = 1.0f;
            rt.ActorRenderScale_be5810(actor, ref scale);
            look.Scale = scale;
            look.Additive = look.Has(1);
            if (look.Has(0x80)) look.Lift = FxRuntime.JumpFraction(look.Counter);
            look.Squash = look.Has(0x20);
            if (look.Has(0x40)) look.Spin = FxRuntime.SpinAngle(look.Counter);
            if (look.Has(0x100) && FxRuntime.FlashArgb((ushort)look.Counter, out uint flash)) look.Flash = flash;
            if (look.Has(0x4000) && FxRuntime.GlowArgb(look.GlowPhase, 5, 5, 0xff, out uint blue)) look.BlueGlow = blue;
            if (look.Has(0x40000) && FxRuntime.GlowArgb(look.GlowPhase, 0xff, 5, 5, out uint red)) look.RedGlow = red;
            look.Shake = look.Has(0x2000);
            return look;
        }
    }

    /// <summary>
    /// The client's per-layer actor drawing on host-supplied layer geometry, 1:1 (the ported functions of
    /// <c>FxRuntime.ActorRender.cs</c>, oracle-verified): the host describes one ACT clip of the actor (the numbers it
    /// already has from the ACT/SPR) and the actor's projected point; the pipeline returns the layer's own draw and every
    /// extra draw the effects add, as <see cref="FxSpriteDraw"/>s (the AddSprite contract of prims-sprites).
    /// Scratch CSprClip / SprImg / rect structures live in FxMemory; <see cref="FxSpriteDraw.Image"/> of the results is the
    /// scratch SprImg address (use <see cref="Palette"/> to tell layers apart — it is passed through untouched).
    /// </summary>
    public sealed class FxActorLayerPipeline
    {
        public struct Clip
        {
            /// <summary>Top-left offset of the image in ACT pixels (the loader's fixed-up x, y).</summary>
            public int X, Y;
            public bool Mirror;
            /// <summary>ACT clip colour.</summary>
            public byte R, G, B, A;
            public float ZoomX, ZoomY;
            /// <summary>ACT clip angle (degrees).</summary>
            public int Angle;
        }

        public struct Image
        {
            public short Width, Height;
            /// <summary>SprImg +4 / +6: the image is stored at half width / height (drawn doubled).</summary>
            public bool HalfWidth, HalfHeight;
        }

        /// <summary>Per-frame numbers of the actor (as RenderBody / the CPc renderer computes them).</summary>
        public struct Frame
        {
            /// <summary>Screen scale of the actor: ScreenScale(rhw) × FUN_00be5810 factor (× job scale for players).</summary>
            public float Scale;
            /// <summary>Projected actor position (pixels) and its view-space depth (point z, for the sprite depth).</summary>
            public float Sx, Sy, ViewZ;
            /// <summary>rhw of the actor point (only the player 0x800 / 0x80000 copies re-project with ScreenScale(Rhw)).</summary>
            public float Rhw;
            /// <summary>Horizontal offset in pixels (actor +0x78 × +0x50 × renderer +0x18 × +0x5c in the client).</summary>
            public float XOffset;
            /// <summary>vf09 p1 / p2: the clip / layer index (depth ordering terms).</summary>
            public int P1, P2;
            /// <summary>vf09 p17 (flat depth variant).</summary>
            public bool Flat;
        }

        readonly FxRuntime rt;
        readonly uint clip, image, rect, intRect, outs, point, blend, ints;

        /// <summary>The draws of the last call in the client's submission order (the base layer at <see cref="BaseIndex"/>).</summary>
        public readonly List<FxSpriteDraw> Draws = new List<FxSpriteDraw>();

        readonly Action<FxSpriteDraw> capture;
        readonly List<FxSpriteDraw> extrasScratch = new List<FxSpriteDraw>(), glowsScratch = new List<FxSpriteDraw>();

        /// <summary>Index of the layer's own draw in <see cref="Draws"/> (−1 when culled).</summary>
        public int BaseIndex { get; private set; } = -1;

        public FxActorLayerPipeline(FxRuntime runtime)
        {
            rt = runtime;
            var M = rt.M;
            clip = M.Alloc(0x24);
            image = M.Alloc(0x10);
            rect = M.Alloc(0x30);
            intRect = M.Alloc(0x10);
            outs = M.Alloc(0x10);
            point = M.Alloc(0xc);
            blend = M.Alloc(4);
            ints = M.Alloc(0x10);
            var list = Draws;
            capture = d => list.Add(d);   // one delegate for the pipeline's life, not one per call
        }

        void Load(in Clip c, in Image i, in Frame f)
        {
            var M = rt.M;
            M.I32(clip, c.X); M.I32(clip + 4, c.Y); M.I32(clip + 8, 0); M.U32(clip + 0xc, c.Mirror ? 1u : 0u);
            M.U8(clip + 0x10, c.R); M.U8(clip + 0x11, c.G); M.U8(clip + 0x12, c.B); M.U8(clip + 0x13, c.A);
            M.F32(clip + 0x14, c.ZoomX); M.F32(clip + 0x18, c.ZoomY); M.I32(clip + 0x1c, c.Angle); M.I32(clip + 0x20, 0);
            M.I16(image, i.Width); M.I16(image + 2, i.Height);
            M.I16(image + 4, (short)(i.HalfWidth ? 1 : 0)); M.I16(image + 6, (short)(i.HalfHeight ? 1 : 0));
            M.F32(point, 0f); M.F32(point + 4, 0f); M.F32(point + 8, f.ViewZ);
            M.U32(rect + 0x2c, image);
        }

        List<FxSpriteDraw> Capture(out Action<FxSpriteDraw> previous)
        {
            Draws.Clear();
            BaseIndex = -1;
            previous = rt.Renderer.SpriteSubmitted;
            rt.Renderer.SpriteSubmitted = capture;
            return Draws;
        }

        /// <summary>
        /// One clip of a monster / NPC / single-sprite actor, as RenderBody (0xbd6080) draws it: vf09, FUN_00bcff20
        /// (extras), the layer's AddSprite (blend 0x101 / 1 / 5), then flash / glows on a copy. False = culled.
        /// </summary>
        public bool Generic(uint actor, in Clip c, in Image i, in Frame f, uint palette)
        {
            Load(c, i, f);
            var M = rt.M;
            Capture(out var previous);
            try
            {
                if (!rt.SpriteRect_bce6f0(actor, f.P1, f.P2, f.Scale, clip, f.Sx, f.Sy, point, f.XOffset, outs, outs + 4, image, rect, intRect, outs + 8, 0, 0, f.Flat))
                    return false;
                M.U32(blend, 0x101);
                rt.GenericLayerExtras_bcff20(actor, rect, clip, intRect, f.XOffset, M.F32(outs), M.F32(outs + 4), palette, image, blend, M.U32(outs + 8));
                if (M.I32(actor + 0x10c) == 0x235)
                {
                    float k = BitConverter.Int32BitsToSingle(0x34a10fb0);   // [0xffe824] 3e-7
                    M.F32(rect + 0x10, M.F32(outs) - k);
                    M.F32(rect + 0x14, M.F32(outs + 4) - k);
                }
                BaseIndex = Draws.Count;
                rt.AddSpriteRect(actor, rect, palette, M.U32(blend));
                rt.FlashAndGlows_be5740(actor, rect, palette);
                return true;
            }
            finally { rt.Renderer.SpriteSubmitted = previous; }
        }

        /// <summary>
        /// One layer of a player (CPc renderer 0x5e7900): the spin offsets (FUN_00be58f0) move (<paramref name="dx"/>,
        /// <paramref name="dy"/>), vf09, FUN_00cc5fa0 (extras), the layer's own draw (deferred and depth-sorted by the
        /// client — it is appended here; <see cref="BaseIndex"/>), then flash / glows (FUN_00be5740).
        /// Layers: 0 body, 1 head, 2..4 accessories, 5 weapon, 6+ others. <paramref name="headX"/> / <paramref name="headY"/>
        /// receive the head's spun offset (layer 1), which the client adds to the accessories.
        /// </summary>
        public bool Player(uint actor, int layer, in Clip c, in Image i, in Frame f, uint palette, uint baseBlend,
            ref int dx, ref int dy, ref int headX, ref int headY)
        {
            Load(c, i, f);
            var M = rt.M;
            Capture(out var previous);
            try
            {
                M.I32(ints, dx); M.I32(ints + 4, dy); M.I32(ints + 8, headX); M.I32(ints + 0xc, headY);
                rt.PcSpinOffset_be58f0(actor, layer, ints, ints + 4, ints + 8, ints + 0xc);
                dx = M.I32(ints); dy = M.I32(ints + 4); headX = M.I32(ints + 8); headY = M.I32(ints + 0xc);
                if (!rt.SpriteRect_bce6f0(actor, f.P1, f.P2, f.Scale, clip, f.Sx, f.Sy, point, f.XOffset, outs, outs + 4, image, rect, intRect, outs + 8, dx, dy, f.Flat))
                    return false;
                M.U32(blend, baseBlend);
                rt.PcLayerExtras_cc5fa0(actor, rect, clip, layer, intRect, f.XOffset, f.Rhw, M.F32(outs), M.F32(outs + 4), f.Sx, f.Sy, palette, image, blend, dx, dy);
                var extras = extrasScratch;
                extras.Clear();
                extras.AddRange(Draws);
                Draws.Clear();
                rt.FlashAndGlows_be5740(actor, rect, palette);
                var glows = glowsScratch;
                glows.Clear();
                glows.AddRange(Draws);
                Draws.Clear();
                Draws.AddRange(extras);
                Draws.AddRange(glows);
                extras.Clear();
                glows.Clear();
                BaseIndex = Draws.Count;
                Draws.Add(rt.ReadSpriteRect(actor, rect, palette, M.U32(blend)));
                return true;
            }
            finally { rt.Renderer.SpriteSubmitted = previous; }
        }
    }
}
