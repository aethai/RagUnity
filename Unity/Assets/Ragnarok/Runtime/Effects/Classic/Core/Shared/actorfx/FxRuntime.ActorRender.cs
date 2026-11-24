// Synced from reference/port/src/RoClassicFx/Shared/actorfx/FxRuntime.ActorRender.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Package actorfx: the client's actor sprite renderer, as far as it turns the actor's effect-driven render state
    /// into draws. Effects never draw these looks themselves — they set bits in the owner's per-frame flag word
    /// (+0xe8) and counters (+0xf0 .. +0xfa, tint +0x98 .. +0x9b); the renderer below reads them for every sprite layer.
    ///
    /// Ported 1:1 from game.exe (oracle-verified, tests/Packages/actorfx):
    /// <list type="bullet">
    /// <item>CRenderObject::vf09 0xbce6f0 — layer rect (flags 0x80 jump, 0x20 squash, 0x40 spin, tint/colour);</item>
    /// <item>0xbe5810 — sprite scale (0x10 shrink, 0x400 grow);</item>
    /// <item>0xbcff20 + 0xbceff0 — generic (RenderBody 0xbd6080) layer extras: blend (1), aura (8), rings (0x800),
    ///   blue/green copies (2 / 0x80000), vf10 0xbceda0 (0x1000), vf11 0xbcf230 (0x200), vf12 0xbcf140 (0x2000 shake),
    ///   0x100000 (+60°), 2 (blue translucent body);</item>
    /// <item>0xcc5fa0 + 0xbe55f0 + 0xbe58f0 — player (CPc) layer extras and spin offsets;</item>
    /// <item>0xbe5740 / 0xbe5c40 (and RenderBody's inline copy) — white flash (0x100), blue (0x4000) / red (0x40000) glow;</item>
    /// <item>the actor OnProcess prologue 0xbd4210 (flag word reset).</item>
    /// </list>
    /// Everything works on FxMemory addresses with the client's layouts: the actor (CGameActor fields), a CSprClip
    /// (+0 x, +4 y, +0xc flags, +0x10 r,g,b,a, +0x14/+0x18 zoom, +0x1c angle), a SprImg (+0 w, +2 h, +4/+6 half
    /// flags), the 0x30-byte sprite rect of AddSprite (see FxSpriteDraw) and the int rect {left, top, right, bottom}.
    /// Draws go to <see cref="FxRenderer.AddSprite"/> exactly like the client's AddSprite (0xbd7930) calls.
    /// </summary>
    public sealed partial class FxRuntime
    {
        static float Bits(uint b) => BitConverter.Int32BitsToSingle((int)b);

        static readonly float K3_6e5 = Bits(0x3816feb5);   // [0x1181954] 3.6e-5
        static readonly float K9e6 = Bits(0x3716feb5);     // [0x1181950] 9e-6
        static readonly float K016 = Bits(0x3e23d70a);     // [0x113747c] / [0x1137478] 0.16
        static readonly float K006 = Bits(0x3d75c28f);     // [0xffd4f0] 0.06
        static readonly float K03 = Bits(0x3e99999a);      // [0xf81604] 0.3
        static readonly float K01 = Bits(0x3dcccccd);      // [0xf7cff8] 0.1
        static readonly float K001 = Bits(0x3c23d70a);     // [0xf4a690] 0.01
        static readonly float K04 = Bits(0x3ecccccd);      // [0xf958f0] 0.4
        static readonly float K5e6 = Bits(0x36a7c5ac);     // [0xffe828] 5e-6
        static readonly float K1e5 = Bits(0x3727c5ac);     // [0xf4b444] 1e-5
        static readonly float K5e5 = Bits(0x3851b717);     // [0xf91a4c] 5e-5
        static readonly float K005 = Bits(0x3d4ccccd);     // [0xf7d128] 0.05
        static readonly float K08 = Bits(0x3f4ccccd);      // [0xf82590] 0.8
        static readonly float K082 = Bits(0x3f51eb85);     // [0x100a574] 0.82

        // ---- client state the actor renderer reads -----------------------------------------------------

        /// <summary>
        /// DAT_01184300 — ground lighting of actor sprites (on in the client). Together with actor +0xb4 == 1 it makes
        /// vf09 multiply the tint's r/g/b by the light at the actor's cell (<see cref="ActorLightImpl"/>).
        /// </summary>
        public bool ActorLighting = true;

        /// <summary>
        /// The light colour multipliers (r, g, b) at the actor's GAT cell, or null when the ground has no light map
        /// (world +0x28 object vf10 returns false). In the client: CGameMode 0xbf84b0 (world → cell) + world +0x28 vf12.
        /// Host-side; default: no light map.
        /// </summary>
        public Func<uint, (float R, float G, float B)?> ActorLightImpl;

        /// <summary>
        /// FUN_00bd0cd0(-1) — the job id the actor is drawn as (mounts, costumes, transformations on top of +0x25c).
        /// Only the PC 0x800/0x80000 aura uses it (through <see cref="JobScale_d0f5c0"/>). Host-side; default +0x25c.
        /// </summary>
        public Func<uint, int> RenderJobImpl;

        public int RenderJob_bd0cd0(uint actor) => RenderJobImpl != null ? RenderJobImpl(actor) : M.I32(actor + 0x25c);

        /// <summary>
        /// The client list at DAT_012c1fb8 (0x14-byte entries) holds an entry 0xb8: hidden (+0xa0 / +0xa1 == 0) players
        /// are then still drawn (the "see hidden" case; CPc::Render also tints them black). Host-side; default false.
        /// </summary>
        public Func<bool> HiddenListHasB8Impl;

        bool HiddenListHasB8() => HiddenListHasB8Impl != null && HiddenListHasB8Impl();

        /// <summary>CGameMode view (mode +0xd0) +0x4c — the camera distance ("zoom"); 400 / zoom scales every pixel offset below.</summary>
        public float CameraZoom
        {
            get => M.F32(M.U32(Mode + 0xd0) + 0x4c);
            set => M.F32(M.U32(Mode + 0xd0) + 0x4c, value);
        }

        /// <summary><c>400.0f / view+0x4c</c> (GetWorld()-&gt;+0xd0-&gt;+0x4c), as every render helper computes it.</summary>
        float ZoomUnit() => 400.0f / M.F32(M.U32(Mode + 0xd0) + 0x4c);

        /// <summary>FUN_00d2b820 (thiscall on CSession, RET 4): jobs 0x1079..0x107d, 0x10d4, 0x10db (the Doram line).</summary>
        public static bool JobFlag_d2b820(int job)
        {
            uint i = (uint)(job - 0x1079);
            return i <= 4 || i == 0x5b || i == 0x62;
        }

        /// <summary>FUN_00d0f5c0 (thiscall on CSession, RET 4, result in ST0) — sprite size of a job: 1, 0.8, 0.75 or 0.82.</summary>
        public static float JobScale_d0f5c0(int job)
        {
            int j = job;
            bool special = (j >= 0xfb7 && j <= 0xfbd) || j == 0xfcd || j == 0x105f || (uint)(j - 0xfbe) <= 0xe
                || (uint)(j - 0x1000) <= 0x10 || (uint)(j - 0x103e) <= 0x18 || (j >= 0x1062 && j <= 0x1064) || j == 0x1061
                || (uint)(j - 0x106d) <= 5 || j == 0x107c || (uint)(j - 0x107e) <= 0x10 || j == 0x1091 || j == 0x1092
                || j == 0x1094 || j == 0x1097 || j == 0x1098;
            if (!special) return 1.0f;
            if (JobFlag_d2b820(j)) return K08;
            if ((j >= 0xfb7 && j <= 0xfbd) || j == 0xfcd || j == 0x105f) return 0.75f;
            return K082;
        }

        // ---- per-frame flag word -----------------------------------------------------------------------

        /// <summary>
        /// The flag-word reset at the top of every actor OnProcess (FUN_00bd4210, called first by CPc / CNpc / CPlayer
        /// vf01) and of every CRagEffect::OnProcess: on a frame with logic ticks, a word the renderer has consumed
        /// (+0xec == 1, set after the actor was drawn) is cleared; effects then set their bits again during the tick.
        /// </summary>
        public void ActorFlagReset_bd4210(uint actor)
        {
            if (TicksThisFrame > 0 && M.U8(actor + 0xec) == 1)
            {
                M.U32(actor + 0xe8, 0);
                M.U8(actor + 0xec, 0);
            }
        }

        // ---- scale -------------------------------------------------------------------------------------

        /// <summary>
        /// FUN_00be5810 (thiscall (float* scale), RET 4) — the renderers' sprite scale: flag 0x10 (EF_BABYBODY)
        /// shrinks to ((sin[4·f0 + 90] + 1)·0.25 + 0.5)× (1 → 0.5 as +0xf0 goes 0 → 45; 0.5 beyond), flag 0x400
        /// (EF_GIANTBODY) grows to (1.5 − (sin[4·f1 + 90] + 1)·0.25)× (1 → 1.5 with +0xf1; 1.5 beyond).
        /// </summary>
        public void ActorRenderScale_be5810(uint actor, ref float scale)
        {
            if ((M.U8(actor + 0xe8) & 0x10) != 0)
            {
                uint e = (uint)M.U8(actor + 0xf0) * 4 + 0x5a;
                uint idx = e >= 0x5a ? e : 0x5a;
                if (idx >= 0x10e) scale = scale * 0.5f;
                else scale = ((FxMath.SinTableAt((int)idx) + 1.0f) * 0.25f + 0.5f) * scale;
            }
            if ((M.U32(actor + 0xe8) & 0x400) != 0)
            {
                uint e = (uint)M.U8(actor + 0xf1) * 4 + 0x5a;
                uint idx = e >= 0x5a ? e : 0x5a;
                if (idx >= 0x10e) scale = scale * 1.5f;
                else scale = (1.5f - (FxMath.SinTableAt((int)idx) + 1.0f) * 0.25f) * scale;
            }
        }

        // ---- the layer rect (vf09) ---------------------------------------------------------------------

        /// <summary>
        /// CRenderObject::vf09 (0xbce6f0; thiscall, 17 args, RET 0x44) — the screen rect of one ACT clip of the actor:
        /// clip/image geometry × scale × clip zoom × actor +0x50 around the projected point (sx, sy), culled against the
        /// viewport (false = culled, nothing written). Then the effect modifiers: flag 0x80 lifts the rect by k·height
        /// (k = 0.3·sin(5·f4) up to f4 = 18, 0.3 until 20, (23 − f4)·0.1 until 23), flag 0x20 squashes it towards its
        /// bottom (height × 1 − 0.2·f4 for f4 ≤ 3, then 0.35..0.40 with one rand() per call until f4 = 33); depth
        /// (+0xa2 / +0x9c / p17 variants); colour = clip colour × actor tint (+0x98..+0x9b, r/g/b × ground light when
        /// +0xb4 == 1) unless the fixed colour +0x84 is on (+0x9f); flag 0x40 overrides the angle with a full turn
        /// (180 − 180·cos(5·f4) for f4 ≤ 36, else 0).
        /// Outputs: rect (0x30), intRect {left, top, right, bottom} (truncated, before xoff), *outColour, *outRhwB/T.
        /// </summary>
        public bool SpriteRect_bce6f0(uint actor, int p1, int p2, float scale, uint clip, float sx, float sy, uint point, float xoff,
            uint outRhwB, uint outRhwT, uint image, uint rect, uint intRect, uint outColour, int dx, int dy, bool p17)
        {
            float depthK = M.F32(actor + 0x5c) * K3_6e5;
            float ky0 = scale * M.F32(clip + 0x18) * M.F32(actor + 0x50);
            double ky = ky0;
            int cy = M.I32(clip + 4);
            double syd = sy;
            float top = (float)((double)(cy + dy) * ky + syd);
            int cy2 = (M.I16(image + 6) + 1) * M.I16(image + 2) + cy + dy;
            float bottom = (float)((double)cy2 * ky + syd + 1.0);
            float kx0 = scale * M.F32(clip + 0x14) * M.F32(actor + 0x50);
            double kx = kx0;
            double sxd = sx;
            int cx = M.I32(clip);
            float left, right;
            float screenH = (float)Renderer.ScreenHeight, screenW = (float)Renderer.ScreenWidth;
            if ((M.U8(clip + 0xc) & 1) != 0)
            {
                int cx2 = (M.I16(image + 4) + 1) * M.I16(image) + cx + dx;
                left = (float)((double)cx2 * kx + sxd);
                right = (float)((double)(dx + cx) * kx + sxd + 1.0);
                if (top >= screenH) return false;
                if (0f >= bottom) return false;
                if (right >= screenW) return false;
                if (0f >= left) return false;
            }
            else
            {
                left = (float)((double)(cx + dx) * kx + sxd);
                int cx2 = (M.I16(image + 4) + 1) * M.I16(image) + cx + dx;
                right = (float)((double)cx2 * kx + sxd + 1.0);
                if (top >= screenH) return false;
                if (0f >= bottom) return false;
                if (left >= screenW) return false;
                if (0f >= right) return false;
            }

            uint flags = M.U32(actor + 0xe8);
            if ((flags & 0x80) != 0)
            {
                float d = (bottom - top) * JumpFraction(M.I16(actor + 0xf4));
                top = top - d;
                bottom = bottom - d;
            }
            if ((flags & 0x20) != 0)
            {
                float f = SquashFactor(M.I16(actor + 0xf4), Rand);
                top = top + (bottom - top) * (1.0f - f);
            }

            float rhwB, rhwT;
            if (M.U8(actor + 0xa2) != 0)
            {
                float p1f = (float)p1;
                rhwB = p1f * K9e6 + K01 - K9e6 * 20.0f + (float)M.I32(actor + 0x80) * K9e6;
                M.F32(outRhwB, rhwB);
                rhwT = p1f * K9e6 + K01 - K9e6 * 20.0f + (float)M.I32(actor + 0x80) * K9e6;
            }
            else
            {
                int e = M.I32(actor + 0x80) + 3 + p2;
                float db = bottom - sy;
                float dt = top - sy;
                float z = M.F32(point + 8);
                if (M.U8(actor + 0x9c) != 0)
                {
                    db = db * K016;
                    rhwB = 1.0f / (z - db);
                    rhwB = rhwB + (float)e * K9e6 + depthK;
                    M.F32(outRhwB, rhwB);
                    dt = dt * K016;
                    rhwT = 1.0f / (z - dt);
                }
                else
                {
                    float ef = (float)e * K9e6;
                    if (p17)
                    {
                        db = db * K006;
                        dt = dt * K006;
                        rhwB = 1.0f / (db + z);
                    }
                    else
                    {
                        db = db * K016;
                        rhwB = 1.0f / (db + z);
                    }
                    rhwB = rhwB + ef + depthK;
                    M.F32(outRhwB, rhwB);
                    if (!p17) dt = dt * K016;
                    rhwT = 1.0f / (dt + z);
                }
                rhwT = rhwT + (float)(M.I32(actor + 0x80) + 3 + p2) * K9e6 + depthK;
            }

            M.F32(outRhwT, rhwT);
            M.F32(rect + 4, left + xoff);
            M.F32(rect, top);
            M.F32(rect + 8, bottom);
            M.F32(rect + 0xc, right + xoff);
            M.U32(rect + 0x10, M.U32(outRhwB));
            M.U32(rect + 0x14, M.U32(outRhwT));
            M.F32(rect + 0x1c, (float)(M.I32(clip + 0x1c) + M.I32(actor + 0x7c)));
            M.I32(intRect, FxOps.Cvtt(left));
            M.I32(intRect + 4, FxOps.Cvtt(top));
            M.I32(intRect + 8, FxOps.Cvtt(right));
            M.I32(intRect + 0xc, FxOps.Cvtt(bottom));

            float lr = 1.0f, lg = 1.0f, lb = 1.0f;
            if (M.U8(actor + 0xb4) == 1 && ActorLighting && ActorLightImpl != null)
            {
                var light = ActorLightImpl(actor);
                if (light.HasValue) { lr = light.Value.R; lg = light.Value.G; lb = light.Value.B; }
            }
            float fa = (float)M.U8(actor + 0x9b);
            float fr = (float)M.U8(actor + 0x9a) * lr;
            float fg = (float)M.U8(actor + 0x99) * lg;
            float fb = (float)M.U8(actor + 0x98) * lb;
            uint colour;
            if (M.U8(actor + 0x9f) != 0 && M.U32(actor + 0x84) != 0xffff0000u)
            {
                colour = M.U32(actor + 0x84);
                M.U32(outColour, colour);
            }
            else
            {
                M.U8(outColour + 3, (byte)FxOps.Cvtt((float)M.U8(clip + 0x13) * fa / 255.0f));
                M.U8(outColour + 2, (byte)FxOps.Cvtt((float)M.U8(clip + 0x10) * fr / 255.0f));
                M.U8(outColour + 1, (byte)FxOps.Cvtt((float)M.U8(clip + 0x11) * fg / 255.0f));
                M.U8(outColour, (byte)FxOps.Cvtt((float)M.U8(clip + 0x12) * fb / 255.0f));
                colour = M.U32(outColour);
            }
            M.U32(rect + 0x18, colour);

            if ((M.U8(actor + 0xe8) & 0x40) != 0) M.F32(rect + 0x1c, SpinAngle(M.I16(actor + 0xf4)));
            return true;
        }

        // ---- the effect formulas (shared by the ported renderer and FxActorLook) ------------------------

        /// <summary>vf09 flag 0x80 (EF_KICKEDBODY, 0xbce8ac): the rect is lifted by this fraction of its height.</summary>
        public static float JumpFraction(short counter)
        {
            if (counter <= 0x12) return FxMath.SinTableAt(counter * 5) * K03;
            if (counter <= 0x14) return K03;
            if (counter <= 0x17) return (float)(0x17 - counter) * K01;
            return 0f;
        }

        /// <summary>
        /// vf09 flag 0x20 (EF_PRESSEDBODY, 0xbce926): the height factor the rect is squashed to (towards its bottom edge).
        /// Counters 4..33 draw one CRT rand() per call (0.35 .. 0.40); pass the engine's <see cref="MsvcRand"/> to stay 1:1.
        /// </summary>
        public static float SquashFactor(short counter, MsvcRand rand)
        {
            if (counter <= 3) return (100.0f - (float)counter * 20.0f) * K001;
            if (counter <= 0x21)
            {
                int r = rand.Next() % 6;
                if (r < 0) r = 0;
                return K04 - (float)r * K001;
            }
            return 1.0f;
        }

        /// <summary>vf09 flag 0x40 (EF_SPINEDBODY, 0xbced31): the angle that replaces the clip's (degrees, one turn over 36 counts).</summary>
        public static float SpinAngle(short counter)
        {
            if (counter > 0x24) return 0f;
            float a = 180.0f - FxMath.SinTableAt(counter * 5 + 90) * 180.0f;
            return a >= 360.0f ? a - 360.0f : a;
        }

        /// <summary>FUN_00be5c40's vertex colour for glow phase <paramref name="f6"/> (+0xf6); false when nothing is drawn (f6 &gt; 50).</summary>
        public static bool GlowArgb(byte f6, int r, int g, int b, out uint argb)
        {
            int sr = (short)r, sg = (short)g, sb = (short)b;
            int x;
            if (f6 <= 10) x = f6 * 0xf00 + sr;
            else if (f6 <= 0x14) x = sr + 0xa000;
            else if (f6 <= 0x32) x = ((short)(ushort)(5 * (ushort)(0x33 - f6)) << 8) + sr;
            else { argb = 0; return false; }
            x = (x << 8) + sg;
            x = (x << 8) + sb;
            argb = (uint)x;
            return true;
        }

        /// <summary>FUN_00be5740's white flash colour for counter <paramref name="f4"/> (+0xf4, flag 0x100); false = not drawn (f4 &gt; 11).</summary>
        public static bool FlashArgb(ushort f4, out uint argb)
        {
            if ((short)f4 <= 5) argb = ((uint)(short)(ushort)(f4 * 0x32) << 24) + 0xffffffu;
            else if ((short)f4 <= 8) argb = 0xffffffffu;
            else if ((short)f4 <= 0xb) argb = ((uint)(short)(ushort)(0x37f - f4 * 0x50) << 24) + 0xffffffu;
            else { argb = 0; return false; }
            return true;
        }

        // ---- AddSprite helpers -------------------------------------------------------------------------

        /// <summary>AddSprite (0xbd7930) of a stack-built rect: offsets 0, depth bias 0 — the form every helper here uses.</summary>
        void EmitSprite(uint actor, float top, float left, float bottom, float right, float rhwB, float rhwT, uint argb, float angle,
            uint image, uint palette, uint flags)
            => Renderer.AddSprite(new FxSpriteDraw
            {
                Object = actor,
                Top = top, Left = left, Bottom = bottom, Right = right,
                RhwBottom = rhwB, RhwTop = rhwT,
                Argb = argb, Angle = angle,
                Image = image, Palette = palette, Flags = flags,
                OffsetX = 0f, OffsetY = 0f, DepthBias = 0,
            });

        /// <summary>AddSprite of a rect in FxMemory (0x30 bytes, image at +0x2c).</summary>
        public void AddSpriteRect(uint actor, uint rect, uint palette, uint flags, float offsetX = 0f, float offsetY = 0f, int depthBias = 0)
            => Renderer.AddSprite(ReadSpriteRect(actor, rect, palette, flags, offsetX, offsetY, depthBias));

        /// <summary>The <see cref="FxSpriteDraw"/> AddSprite would make of the rect at <paramref name="rect"/>.</summary>
        public FxSpriteDraw ReadSpriteRect(uint actor, uint rect, uint palette, uint flags, float offsetX = 0f, float offsetY = 0f, int depthBias = 0)
            => new FxSpriteDraw
            {
                Object = actor,
                Top = M.F32(rect), Left = M.F32(rect + 4), Bottom = M.F32(rect + 8), Right = M.F32(rect + 0xc),
                RhwBottom = M.F32(rect + 0x10), RhwTop = M.F32(rect + 0x14),
                Argb = M.U32(rect + 0x18), Angle = M.F32(rect + 0x1c),
                Image = M.U32(rect + 0x2c), Palette = palette, Flags = flags,
                OffsetX = offsetX, OffsetY = offsetY, DepthBias = depthBias,
            };

        // ---- generic renderer (RenderBody 0xbd6080: monsters, NPCs, single-sprite PCs) -------------------

        /// <summary>
        /// FUN_00bcff20 (thiscall, RET 0x28) — RenderBody's per-layer extras after vf09, before the layer's own AddSprite:
        /// blend (*blend = 1 when the colour is translucent, 5 = additive with flag 1), <see cref="BodyAura8_bceff0"/>,
        /// flag 0x800: ten white additive copies enlarged by 3, 6 .. 30 × (400 / zoom) px with alpha from f4
        /// (f4 − 5·i − 50 up to f4 = 130, then 245 − 10·i − f4; 0..200), flags 2 / 0x80000: two copies enlarged by 1× and
        /// 2× (400 / zoom) px, colour (f2, 55, 55, 255) for 2 else (f2, 5, 155, 5); then vf10 (0x1000), vf11 (0x200),
        /// vf12 (0x2000, ±10 units), flag 0x100000: angle = clip angle + actor +0x7c + 60 (mod 360),
        /// flag 2: the layer itself becomes 0x643737ff (translucent blue).
        /// </summary>
        public void GenericLayerExtras_bcff20(uint actor, uint rect, uint clip, uint intRect, float xoff, float rhwB, float rhwT,
            uint palette, uint image, uint blendPtr, uint colour)
        {
            if ((byte)(colour >> 24) < 0xff) M.U32(blendPtr, 1);
            if ((M.U8(actor + 0xe8) & 1) != 0) M.U32(blendPtr, 5);
            BodyAura8_bceff0(actor, clip, intRect, xoff, rhwB, rhwT, palette, image, colour);
            uint flags = M.U32(actor + 0xe8);
            if ((flags & 0x800) != 0)
            {
                int k3 = 3;
                for (int i = 1; ; i++)
                {
                    ushort f4 = M.U16(actor + 0xf4);
                    int v = (short)f4 <= 0x82 ? (ushort)(f4 - (i * 5 + 0x32)) : (ushort)(0xf5 - i * 10 - f4);
                    if ((short)v > 200) v = 200;
                    int alpha = (short)v >= 0 ? (ushort)v : 0;
                    float s = ZoomUnit() * (float)k3;
                    float right = (float)M.I32(intRect + 8);
                    float top = (float)M.I32(intRect + 4) - s;
                    right = right + xoff;
                    float bottom = (float)M.I32(intRect + 0xc) + s;
                    float left = (float)M.I32(intRect) + xoff;
                    if (left > right) { left = left + s; right = right - s; }
                    else { left = left - s; right = right + s; }
                    float d = s * K5e6;
                    float rb = d + rhwB + K1e5;
                    float rt = d + rhwT + K1e5;
                    uint argb = ((uint)(short)alpha << 24) + 0xffffffu;
                    float angle = (float)(M.I32(actor + 0x7c) + M.I32(clip + 0x1c));
                    EmitSprite(actor, top, left, bottom, right, rb, rt, argb, angle, image, palette, 5);
                    k3 += 3;
                    if (k3 > 0x1e) break;
                }
                flags = M.U32(actor + 0xe8);
            }
            if ((flags & 0x80002) != 0)
            {
                for (int i = 1; i <= 2; i++)
                {
                    byte a = M.U8(actor + 0xf2);
                    float s = ZoomUnit() * (float)i;
                    float right = (float)M.I32(intRect + 8) + xoff;
                    float top = (float)M.I32(intRect + 4) - s;
                    float bottom = (float)M.I32(intRect + 0xc) + s;
                    float left = (float)M.I32(intRect) + xoff;
                    if (left > right) { left = left + s; right = right - s; }
                    else { left = left - s; right = right + s; }
                    float d = s * K5e6;
                    uint argb = ((uint)a << 24) + ((M.U8(actor + 0xe8) & 2) != 0 ? 0x3737ffu : 0x59b05u);
                    float rb = d + rhwB + K1e5;
                    float rt = d + rhwT + K1e5;
                    float angle = (float)(M.I32(actor + 0x7c) + M.I32(clip + 0x1c));
                    EmitSprite(actor, top, left, bottom, right, rb, rt, argb, angle, image, palette, 5);
                }
            }
            float factor = (float)(M.I32(intRect + 0xc) - M.I32(intRect + 4)) * K005;
            SplitImages_bceda0(actor, clip, intRect, xoff, rhwB, rhwT, palette, image, factor, 0);
            GhostRings_bcf230(actor, rect, clip, intRect, xoff, rhwB, rhwT, palette, image, M.U32(blendPtr), 0);
            Shake_bcf140(actor, rect, intRect, xoff, 0x15, 0xa);
            flags = M.U32(actor + 0xe8);
            if ((flags & 0x100000) != 0)
            {
                ushort ax = (ushort)(M.U16(clip + 0x1c) + M.U16(actor + 0x7c));
                ushort dxv = (ushort)(ax + 0x3c);
                int cxv = (short)dxv < 0x168 ? dxv : (ushort)(dxv - 0x168);
                M.F32(rect + 0x1c, (float)(short)cxv);
                flags = M.U32(actor + 0xe8);
            }
            if ((flags & 2) != 0) M.U32(rect + 0x18, 0x643737ffu);
        }

        /// <summary>
        /// FUN_00bceff0 (thiscall, RET 0x20) — flag 8 (Assumptio, Full Protection, Spiral Pierce …) on the generic
        /// renderer: one additive copy of the layer in its own colour, enlarged on every side by (sin[+0xee]·1.5 + 5) ×
        /// (400 / zoom) px, slightly behind (rhw − 5e-5). +0xee advances once per call (per layer, per frame!), 0..180.
        /// </summary>
        public void BodyAura8_bceff0(uint actor, uint clip, uint intRect, float xoff, float rhwB, float rhwT, uint palette, uint image, uint colour)
        {
            if ((M.U8(actor + 0xe8) & 8) == 0) return;
            byte e = (byte)(M.U8(actor + 0xee) + 1);
            M.U8(actor + 0xee, e);
            if (e > 0xb4) { M.U8(actor + 0xee, 0); e = 0; }
            float s0 = FxMath.SinTableAt(e) * 1.5f + 5.0f;
            float S = ZoomUnit() * s0;
            float top = (float)M.I32(intRect + 4);
            float left = (float)M.I32(intRect) + xoff;
            float right = (float)M.I32(intRect + 8) + xoff;
            top = top - S;
            float bottom = (float)M.I32(intRect + 0xc) + S;
            if (left > right) { left = left + S; right = right - S; }
            else { left = left - S; right = right + S; }
            float rb = rhwB - K5e5;
            float rt = rhwT - K5e5;
            float angle = (float)(M.I32(clip + 0x1c) + M.I32(actor + 0x7c));
            EmitSprite(actor, top, left, bottom, right, rb, rt, colour, angle, image, palette, 5);
        }

        /// <summary>
        /// CRenderObject::vf10 (0xbceda0; thiscall, RET 0x24) — flag 0x1000 (EF_4WAYBODY): four copies of the layer
        /// pushed right / left / down / up by f4 · factor × (400 / zoom) px (factor: 0.05 × layer height generic, 4 for
        /// players), white with alpha 5·(30 − f4) (≥ 0), normal alpha blend (flags 0x201), in front (rhw + 0.1), raised by v.
        /// </summary>
        public void SplitImages_bceda0(uint actor, uint clip, uint intRect, float xoff, float rhwB, float rhwT, uint palette, uint image,
            float factor, int v)
        {
            if ((M.U32(actor + 0xe8) & 0x1000) == 0) return;
            float rb = rhwB + K01;
            float rt = rhwT + K01;
            int vs = (short)v;
            for (int n = 1; n <= 4; n++)
            {
                float k = (float)M.I16(actor + 0xf4) * factor;
                ushort cxv = (ushort)(0x1e - M.U16(actor + 0xf4));
                ushort axv = (ushort)(cxv + (ushort)(cxv << 2));
                int alpha = (short)axv >= 0 ? axv : 0;
                float S = ZoomUnit() * k;
                float top, bottom, left, right;
                switch (n)
                {
                    case 1:
                        left = (float)M.I32(intRect) + xoff + S;
                        top = (float)M.I32(intRect + 4);
                        bottom = (float)M.I32(intRect + 0xc);
                        right = (float)M.I32(intRect + 8) + xoff + S;
                        break;
                    case 2:
                        left = (float)M.I32(intRect) + xoff - S;
                        top = (float)M.I32(intRect + 4);
                        bottom = (float)M.I32(intRect + 0xc);
                        right = (float)M.I32(intRect + 8) + xoff - S;
                        break;
                    case 3:
                        top = (float)M.I32(intRect + 4) + S;
                        bottom = (float)M.I32(intRect + 0xc) + S;
                        left = (float)M.I32(intRect) + xoff;
                        right = (float)M.I32(intRect + 8) + xoff;
                        break;
                    default:
                        top = (float)M.I32(intRect + 4) - S;
                        bottom = (float)M.I32(intRect + 0xc) - S;
                        left = (float)M.I32(intRect) + xoff;
                        right = (float)M.I32(intRect + 8) + xoff;
                        break;
                }
                uint argb = ((uint)(short)alpha << 24) + 0xffffffu;
                float vf = (float)vs;
                top = top - vf;
                bottom = bottom - vf;
                float angle = (float)(M.I32(clip + 0x1c) + M.I32(actor + 0x7c));
                EmitSprite(actor, top, left, bottom, right, rb, rt, argb, angle, image, palette, 0x201);
            }
        }

        /// <summary>
        /// CRenderObject::vf11 (0xbcf230; thiscall, RET 0x28) — flag 0x200 (EF_REFLECTBODY): +0xef advances once per call
        /// (wraps at 200); four copies enlarged by k = (ef·0.1 + 5·n) mod 20 × (400 / zoom) px (n = 1..4), white with
        /// alpha 100 − 5k, behind the layer, drawn with the layer's blend unless it is additive (5); the layer itself gets
        /// colour 0x96ffffff (alpha 150).
        /// </summary>
        public void GhostRings_bcf230(uint actor, uint rect, uint clip, uint intRect, float xoff, float rhwB, float rhwT, uint palette,
            uint image, uint blend, int v)
        {
            if ((M.U32(actor + 0xe8) & 0x200) == 0) return;
            byte ef = (byte)(M.U8(actor + 0xef) + 1);
            M.U8(actor + 0xef, ef);
            if (ef >= 200) { ef = (byte)(ef + 0x38); M.U8(actor + 0xef, ef); }
            int vs = (short)v;
            for (int n = 5; n < 0x19; n += 5)
            {
                float k = (float)M.U8(actor + 0xef) * K01 + (float)n;
                if (k >= 20.0f) k = k - 20.0f;
                int alpha = (ushort)FxOps.Cvtt(100.0f - k * 5.0f);
                float S = ZoomUnit() * k;
                float vf = (float)vs;
                float top = (float)M.I32(intRect + 4) - S - vf;
                float right = (float)M.I32(intRect + 8) + xoff;
                float bottom = (float)M.I32(intRect + 0xc) + S - vf;
                float left = (float)M.I32(intRect) + xoff;
                if (left > right) { left = left + S; right = right - S; }
                else { left = left - S; right = right + S; }
                float d = S * K5e6;
                float rb = rhwB - d - K1e5;
                uint argb = ((uint)(short)alpha << 24) + 0xffffffu;
                float rt = rhwT - d - K1e5;
                float angle = (float)(M.I32(actor + 0x7c) + M.I32(clip + 0x1c));
                if (blend != 5) EmitSprite(actor, top, left, bottom, right, rb, rt, argb, angle, image, palette, blend);
            }
            M.U32(rect + 0x18, 0x96ffffffu);
        }

        /// <summary>
        /// CRenderObject::vf12 (0xbcf140; thiscall, RET 0x14) — flag 0x2000 (Stin, Close Confine, EF_QUAKEBODY2): the four
        /// edges of the layer rect are rebuilt from the int rect, each moved by (rand() % range − centre) × (400 / zoom) px
        /// (four rand() calls per layer per frame): generic actors range 21 / centre 10 (±10), players 11 / 5 (±5).
        /// </summary>
        public void Shake_bcf140(uint actor, uint rect, uint intRect, float xoff, int range, int centre)
        {
            if ((M.U32(actor + 0xe8) & 0x2000) == 0) return;
            Span<float> d = stackalloc float[4];
            for (int i = 0; i < 4; i++)
            {
                int r = 0;
                if (range != 0)
                {
                    int q = Rand.Next() % range;
                    r = q >= 0 ? q : 0;
                }
                d[i] = ZoomUnit() * (float)(r - centre);
            }
            M.F32(rect, (float)M.I32(intRect + 4) + d[0]);
            M.F32(rect + 8, (float)M.I32(intRect + 0xc) + d[1]);
            M.F32(rect + 4, (float)M.I32(intRect) + xoff + d[2]);
            M.F32(rect + 0xc, (float)M.I32(intRect + 8) + xoff + d[3]);
        }

        // ---- flash and glows (both renderers) ----------------------------------------------------------

        /// <summary>
        /// FUN_00be5c40 (thiscall (rect*, short r, short g, short b, palette), RET 0x14) — one glow: the layer drawn again
        /// twice, additive (flags 5), vertex colour (α, r, g, b) with α from +0xf6: f6·15 up to 10, 160 up to 20,
        /// 5·(51 − f6) up to 50; nothing beyond 50. Writes the colour into the (copied) rect.
        /// </summary>
        public void Glow_be5c40(uint actor, ref FxSpriteDraw rect, int r, int g, int b, uint palette)
        {
            if (GlowArgb(M.U8(actor + 0xf6), r, g, b, out uint argb)) rect.Argb = argb;
            if (M.U8(actor + 0xf6) <= 0x32)
            {
                var d = rect; d.Palette = palette; d.Flags = 5; d.OffsetX = 0f; d.OffsetY = 0f; d.DepthBias = 0; d.Object = actor;
                Renderer.AddSprite(d);
                Renderer.AddSprite(d);
            }
        }

        /// <summary>
        /// FUN_00be5740 (thiscall (rect by value, palette), RET 0x34; inline in RenderBody after the layer's AddSprite) —
        /// flag 0x100 (EF_HITBODY): one additive white copy, alpha 50·f4 up to 5, 255 up to 8, 0x37f − 80·f4 up to 11;
        /// flag 0x4000: blue glow (α, 5, 5, 255); flag 0x40000: red glow (α, 255, 5, 5) — see <see cref="Glow_be5c40"/>.
        /// <paramref name="rect"/> is the layer's final rect (the client passes a copy; nothing is written back).
        /// </summary>
        public void FlashAndGlows_be5740(uint actor, uint rect, uint palette)
        {
            var r = ReadSpriteRect(actor, rect, palette, 5);
            uint flags = M.U32(actor + 0xe8);
            if ((flags & 0x100) != 0)
            {
                if (FlashArgb(M.U16(actor + 0xf4), out uint argb))
                {
                    r.Argb = argb;
                    Renderer.AddSprite(r);
                    flags = M.U32(actor + 0xe8);
                }
            }
            if ((flags & 0x4000) != 0)
            {
                Glow_be5c40(actor, ref r, 5, 5, 0xff, palette);
                flags = M.U32(actor + 0xe8);
            }
            if ((flags & 0x40000) != 0) Glow_be5c40(actor, ref r, 0xff, 5, 5, palette);
        }

        // ---- player renderer (CPc: 0xcc9d00 → 0x5e5bd0 → 0x5e7900 per layer) ----------------------------

        /// <summary>
        /// FUN_00be55f0 (thiscall, no args) — the hover height of a flying (option 0x8000) player in pixels: +0xf8 advances
        /// 2°/frame, +0xfa eases towards 20 (idle / walk / flag 0x40000), 100 (action 6) or 0 (anything else); result
        /// max(0, cvtt(cvtt(sin(f8)·4 + fa) × 400 / zoom)).
        /// </summary>
        public int PcHover_be55f0(uint actor)
        {
            ushort f8 = (ushort)(M.U16(actor + 0xf8) + 2);
            M.U16(actor + 0xf8, f8);
            if ((short)f8 >= 0x168) { f8 = (ushort)(f8 - 0x168); M.U16(actor + 0xf8, f8); }
            int ang = f8;
            uint heavy = M.U32(actor + 0xe8) & 0x40000;
            int action = M.I32(actor + 0x70);
            byte bl = M.U8(actor + 0xfa);
            byte cl;
            if (action == 0 || action == 1 || heavy != 0)
            {
                cl = bl;
                if (bl < 0x14) { if ((short)ang % 30 == 0) { cl = (byte)(bl + 1); M.U8(actor + 0xfa, cl); } }
                else if (bl > 0x14) { if ((short)ang % 30 == 0) { cl = (byte)(bl - 1); M.U8(actor + 0xfa, cl); } }
            }
            else if (action == 6)
            {
                if (bl < 0x64)
                {
                    if ((short)ang % 30 == 0) { bl = (byte)(bl + 1); M.U8(actor + 0xfa, bl); }
                    cl = bl;
                }
                else
                {
                    cl = bl;
                    if (bl > 0x64 && (short)ang % 30 == 0) { cl = (byte)(bl - 1); M.U8(actor + 0xfa, cl); }
                }
            }
            else
            {
                cl = bl;
                if (bl != 0) { cl = (byte)(bl - 1); M.U8(actor + 0xfa, cl); }
            }
            float h = FxMath.SinTableAt((short)ang) * 4.0f + (float)cl;
            int hi = (ushort)FxOps.Cvtt(h);
            float S = ZoomUnit() * (float)(short)hi;
            int dxv = FxOps.Cvtt(S);
            return (short)dxv >= 0 ? (ushort)dxv : 0;
        }

        /// <summary>
        /// FUN_00be58f0 (thiscall (layer, int* dx, int* dy, int* outX, int* outY), RET 0x14) — flag 0x40 (spin) on the
        /// player renderer: the head (layer 1) orbits the body centre on a circle of 40 px, other layers ≥ 2 on 10 px,
        /// following the angle a = 180 − 180·cos(5·f4) (the rect itself is rotated by vf09): dx += sin a · r,
        /// dy −= cos a · r − r. The head offset is also stored to outX / outY (the layer offsets of later layers).
        /// </summary>
        public void PcSpinOffset_be58f0(uint actor, int layer, uint dxPtr, uint dyPtr, uint outX, uint outY)
        {
            if ((M.U8(actor + 0xe8) & 0x40) == 0) return;
            int c = M.I16(actor + 0xf4);
            int a = FxOps.Cvtt(180.0f - FxMath.SinTableAt(c * 5 + 90) * 180.0f);
            int i = a < 0x168 ? a : a - 0x168;
            if (layer == 1)
            {
                M.I32(dxPtr, FxOps.Cvtt(FxMath.SinTableAt(i) * 40.0f + (float)M.I32(dxPtr)));
                M.I32(dyPtr, FxOps.Cvtt((float)M.I32(dyPtr) - (FxMath.CosTableAt(i) * 40.0f - 40.0f)));
                M.I32(outX, M.I32(dxPtr));
                M.I32(outY, M.I32(dyPtr));
            }
            else if (layer >= 2)
            {
                M.I32(dxPtr, FxOps.Cvtt(FxMath.SinTableAt(i) * 10.0f + (float)M.I32(dxPtr)));
                M.I32(dyPtr, FxOps.Cvtt((float)M.I32(dyPtr) - (FxMath.CosTableAt(i) * 10.0f - 10.0f)));
            }
        }

        /// <summary>
        /// FUN_00cc5fa0 (thiscall, 15 args, RET 0x3c) — the player renderer's per-layer extras after vf09, before the
        /// layer's own (deferred) AddSprite. Layers: 0 body, 1 head, 2..4 accessories, 5 weapon, ≥ 6 others.
        /// Flag 1: layers 0..4 additive; flag 2: the weapon layer additive; flag 4: the weapon drawn once more additively
        /// every other frame (+0xed toggles); option 0x8000 (flying): hover v = <see cref="PcHover_be55f0"/> px, stored in
        /// +0x128 (int64) and subtracted from the layer; flag 8 (layers 0..4, not with 0x30000): two white additive copies
        /// enlarged by (sin[ee] + 3) and (2·sin[ee] + 5) × (400 / zoom) px, alpha 100 / 120, behind; flags 0x800 / 0x80000
        /// (not with 0x20000): two copies re-projected at scale × job scale × 1.01 / 1.02, colour (f2, 55, 55, 255) /
        /// (f2, 5, 100, 5), additive; vf10 (factor 4) and vf11 (not with 0x30000); vf12 shake ±5; flag 0x10000: rises by
        /// 20·f4 × (400 / zoom) px and fades (alpha 255 − 20·f4); flag 0x20000: comes down from (25 − f6)·20 × (400 / zoom) px
        /// fading in (alpha 17·(f6 − 10)); flag 0x100000: every layer sinks by 24000 / zoom px, body and layers &gt; 4 become
        /// additive 0x1e373737.
        /// </summary>
        public void PcLayerExtras_cc5fa0(uint actor, uint rect, uint clip, int layer, uint intRect, float xoff, float rhw, float rhwB, float rhwT,
            float sx, float sy, uint palette, uint image, uint blendPtr, int dx, int dy)
        {
            if (HiddenListHasB8())
            {
                if (M.U8(actor + 0xa0) == 0 || M.U8(actor + 0xa1) == 0)
                    if (((M.U32(actor + 0x2c0) >> 6) & 1) == 0) return;
            }
            uint flags = M.U32(actor + 0xe8);
            if ((flags & 1) != 0 && layer <= 4) { M.U32(blendPtr, 5); flags = M.U32(actor + 0xe8); }
            if ((flags & 2) != 0 && layer == 5) { M.U32(blendPtr, (uint)layer); flags = M.U32(actor + 0xe8); }
            if ((flags & 4) != 0 && layer == 5)
            {
                if (M.U8(actor + 0xed) == 0)
                {
                    AddSpriteRect(actor, rect, palette, (uint)layer);
                    M.U8(actor + 0xed, 1);
                }
                else M.U8(actor + 0xed, 0);
            }

            int v = 0;
            if (((M.U32(actor + 0x2c0) >> 15) & 1) == 1) v = (ushort)PcHover_be55f0(actor);
            int vShort = (short)v;
            M.I32(actor + 0x128, vShort);
            M.I32(actor + 0x12c, vShort >> 31);
            flags = M.U32(actor + 0xe8);

            if ((flags & 8) != 0 && layer <= 4 && (flags & 0x30000) == 0)
            {
                byte e = (byte)(M.U8(actor + 0xee) + 1);
                M.U8(actor + 0xee, e);
                if (e > 0xb4) { M.U8(actor + 0xee, 0); e = 0; }
                float s1 = FxMath.SinTableAt(e) + 3.0f;
                float S = ZoomUnit() * s1;
                float vf = (float)vShort;
                float top = (float)M.I32(intRect + 4) - S - vf;
                float bottom = (float)M.I32(intRect + 0xc) + S - vf;
                float right = (float)M.I32(intRect + 8) + xoff;
                float left = (float)M.I32(intRect) + xoff;
                if (left > right) { left = left + S; right = right - S; }
                else { left = left - S; right = right + S; }
                float rb = rhwB - K5e5;
                float rt = rhwT - K5e5;
                float angle = (float)(M.I32(clip + 0x1c) + M.I32(actor + 0x7c));
                EmitSprite(actor, top, left, bottom, right, rb, rt, 0x64ffffffu, angle, image, palette, 5);

                float sn = FxMath.SinTableAt(M.U8(actor + 0xee));
                float S2 = ZoomUnit() * (sn + sn + 5.0f);
                top = (float)M.I32(intRect + 4) - S2 - vf;
                bottom = (float)M.I32(intRect + 0xc) + S2 - vf;
                right = (float)M.I32(intRect + 8) + xoff;
                left = (float)M.I32(intRect) + xoff;
                if (left > right) { left = left + S2; right = right - S2; }
                else { left = left - S2; right = right + S2; }
                angle = (float)(M.I32(clip + 0x1c) + M.I32(actor + 0x7c));
                EmitSprite(actor, top, left, bottom, right, rb, rt, 0x78ffffffu, angle, image, palette, 5);
                flags = M.U32(actor + 0xe8);
            }

            if ((flags & 0x80800) != 0 && (flags & 0x20000) == 0)
            {
                double scale50 = M.F32(actor + 0x50);
                int k3 = 3;
                for (int i = 1; ; i++)
                {
                    byte a = M.U8(actor + 0xf2);
                    float s = Renderer.ScreenScale(rhw);
                    int job = RenderJob_bd0cd0(actor);
                    double t = (double)JobScale_d0f5c0(job) * (double)s;
                    float m = (float)i * K001 + 1.0f;
                    t = t * (double)m;
                    float k = (float)t;
                    float xoffBase = (float)M.I32(actor + 0x78) * M.F32(actor + 0x50);
                    double xk = (double)Renderer.Field18 * (double)xoffBase;
                    xk = xk * (double)k;
                    float xoffK = (float)xk;
                    double kd = k, zy = M.F32(clip + 0x18), syd = sy;
                    int ey = dy + M.I32(clip + 4);
                    int intTop = M.I32(intRect + 4);
                    float top = (float)((double)ey * kd * zy * scale50 + syd);
                    int ey2 = (M.I16(image + 6) + 1) * M.I16(image + 2) + M.I32(clip + 4) + dy;
                    float bottom = (float)((double)ey2 * kd * zy * scale50 + syd + 1.0);
                    double zx = M.F32(clip + 0x14);
                    double x1 = 0, x2 = 0;
                    bool skip;
                    int screenW = Renderer.ScreenWidth, screenH = Renderer.ScreenHeight;
                    if ((M.U8(clip + 0xc) & 1) != 0)
                    {
                        int c1 = (M.I16(image + 4) + 1) * M.I16(image) + M.I32(clip) + dx;
                        int c2 = dx + M.I32(clip);
                        x1 = (double)c1 * kd * zx;
                        x2 = (double)c2 * kd * zx;
                        skip = intTop >= screenH || intTop <= 0 || M.I32(intRect + 8) >= screenW || M.I32(intRect) <= 0;
                    }
                    else
                    {
                        skip = intTop >= screenH || M.I32(intRect + 0xc) <= 0 || M.I32(intRect) >= screenW || M.I32(intRect + 8) <= 0;
                        if (!skip)
                        {
                            x1 = (double)(M.I32(clip) + dx) * kd * zx;
                            int c2 = (M.I16(image + 4) + 1) * M.I16(image) + M.I32(clip) + dx;
                            x2 = (double)c2 * kd * zx;
                        }
                    }
                    if (!skip)
                    {
                        double sxd = sx;
                        float left = (float)(scale50 * x1 + sxd) + xoffK;
                        float right = (float)(scale50 * x2 + sxd + 1.0) + xoffK;
                        float vf = (float)vShort;
                        float topR = top - vf;
                        float bottomR = ((float)intTop - top) * 0.5f + bottom - vf;
                        float d = (float)k3 * K5e6;
                        float rb = d + rhwB + K1e5;
                        float rt = d + rhwT + K1e5;
                        uint f = M.U32(actor + 0xe8);
                        uint argb = (f & 0x800) != 0 ? ((uint)a << 24) + 0x3737ffu : ((uint)a << 24) + 0x56405u;
                        float angle = (float)(M.I32(clip + 0x1c) + M.I32(actor + 0x7c));
                        EmitSprite(actor, topR, left, bottomR, right, rb, rt, argb, angle, image, palette, 5);
                    }
                    k3 += 3;
                    if (k3 > 6) break;
                }
                flags = M.U32(actor + 0xe8);
            }

            if ((flags & 0x30000) == 0)
            {
                SplitImages_bceda0(actor, clip, intRect, xoff, rhwB, rhwT, palette, image, 4.0f, v);
                GhostRings_bcf230(actor, rect, clip, intRect, xoff, rhwB, rhwT, palette, image, M.U32(blendPtr), v);
            }
            Shake_bcf140(actor, rect, intRect, xoff, 0xb, 5);

            flags = M.U32(actor + 0xe8);
            if ((flags & 0x10000) != 0)
            {
                ushort f4 = M.U16(actor + 0xf4);
                ushort di = (ushort)(0xff - f4 * 20);
                float S = ZoomUnit() * (float)((short)f4 * 20);
                M.F32(rect, (float)M.I32(intRect + 4) - S);
                M.F32(rect + 8, (float)M.I32(intRect + 0xc) - S);
                int alpha = (short)di >= 0 ? di : 0;
                M.U32(rect + 0x18, ((uint)(short)alpha << 24) + 0xffffffu);
                flags = M.U32(actor + 0xe8);
            }
            if ((flags & 0x20000) != 0)
            {
                byte e = M.U8(actor + 0xf6);
                int dist = (0x19 - e) * 20;
                ushort cxv = (ushort)(e - 10);
                ushort axv = (ushort)((ushort)(cxv << 4) + cxv);
                int dxv = axv;
                if ((short)axv > 0xff) dxv = 0xff;
                int alpha = (short)dxv >= 0 ? (ushort)dxv : 0;
                uint argb = ((uint)(short)alpha << 24) + 0xffffffu;
                float S = ZoomUnit() * (float)dist;
                float S0 = 0.0f > S ? 0.0f : S;
                M.F32(rect, (float)M.I32(intRect + 4) - S0);
                M.U32(rect + 0x18, argb);
                M.F32(rect + 8, (float)M.I32(intRect + 0xc) - S0);
            }
            if ((short)v != 0)
            {
                float vf = (float)vShort;
                M.F32(rect, M.F32(rect) - vf);
                M.F32(rect + 8, M.F32(rect + 8) - vf);
            }
            if ((M.U32(actor + 0xe8) & 0x100000) != 0)
            {
                if (layer == 0 || layer > 4)
                {
                    M.U32(rect + 0x18, 0x1e373737u);
                    M.U32(blendPtr, 5);
                }
                float S = 24000.0f / M.F32(M.U32(Mode + 0xd0) + 0x4c);
                M.F32(rect, (float)M.I32(intRect + 4) + S);
                M.F32(rect + 8, (float)M.I32(intRect + 0xc) + S);
            }
        }
    }

    public sealed partial class FxRuntime
    {
        // ---- footprints (CPc::Render 0xcc89b0 prelude) --------------------------------------------------

        /// <summary>
        /// FUN_00c028a0 (thiscall on the game mode, RET 0x10): a CDummyEffectLauncher at <c>pos</c> that launches effect
        /// <c>ef</c> on itself and lives <c>lifetime</c> × 24 ms, heading <c>angle</c> degrees. Host-side:
        /// (x, y, z, ef, lifetime, angle).
        /// </summary>
        public Action<float, float, float, int, int, int> GroundEffectImpl;

        /// <summary>
        /// FUN_00b88070 (thiscall (mode), RET 4) — footprints, called by CPc::Render on frames with logic ticks:
        /// mode 1 for option 0x4000 (Chase Walk: EF 326 / 327 every 12 frames, alternating), mode 2 for flag 0x8000
        /// (TK_RUN, EF_RUN 442: EF 447 / 448 every 7 frames once +0xf7 &gt; 7). The frame counter +0x45c advances once per
        /// call; at the two phases the step from the last footprint (+0x460..+0x468) gives the heading
        /// (FxMath.DirectionToAngle + 135°), and the footprint is left there for 333 launcher ticks.
        /// </summary>
        public void Footprints_b88070(uint actor, int mode)
        {
            int period = mode == 1 ? 0xc : 7;
            if (TicksThisFrame <= 0) return;
            int n = M.I32(actor + 0x45c) + 1;
            M.I32(actor + 0x45c, n);
            int twice = period + period;
            if (n > twice) { M.I32(actor + 0x45c, 1); n = 1; }
            else if (n <= 0)
            {
                if (n < 0) M.I32(actor + 0x45c, 0);
                M.U32(actor + 0x460, M.U32(actor + 0x10));
                M.U32(actor + 0x464, M.U32(actor + 0x14));
                M.U32(actor + 0x468, M.U32(actor + 0x18));
                n = M.I32(actor + 0x45c);
            }
            if (n != period && n != twice) return;
            float px = M.F32(actor + 0x10), pz = M.F32(actor + 0x18);
            float dx = px - M.F32(actor + 0x460);
            float dz = pz - M.F32(actor + 0x468);
            M.F32(actor + 0x460, px);
            M.U32(actor + 0x464, M.U32(actor + 0x14));
            M.F32(actor + 0x468, pz);
            if (dx == 0f && dz == 0f) return;
            int a = (ushort)(FxMath.DirectionToAngle(dx, dz) + 0x87);
            int ang = (short)a < 0x168 ? a : (ushort)(a - 0x168);
            int angle = (short)ang;
            if (mode == 1)
            {
                int ef = 0x146 + (M.I32(actor + 0x45c) != period ? 1 : 0);
                GroundEffectImpl?.Invoke(M.F32(actor + 0x460), M.F32(actor + 0x464), M.F32(actor + 0x468), ef, 0x14d, angle);
            }
            else if (mode == 2 && M.U8(actor + 0xf7) > (uint)period)
            {
                int ef = M.I32(actor + 0x45c) == period ? 0x1bf : 0x1c0;
                GroundEffectImpl?.Invoke(M.F32(actor + 0x460), M.F32(actor + 0x464), M.F32(actor + 0x468), ef, 0x14d, angle);
            }
        }

        /// <summary>
        /// The effect part of CPc::Render (vf03 0xcc89b0) before the layers: Chase Walk footprints for option 0x4000,
        /// running footprints for flag 0x8000 (both advance +0x45c).
        /// </summary>
        public void PcRenderFootprints_cc89b0(uint actor)
        {
            if (((M.U32(actor + 0x2c0) >> 14) & 1) != 0) Footprints_b88070(actor, 1);
            if ((M.U32(actor + 0xe8) & 0x8000) != 0) Footprints_b88070(actor, 2);
        }
    }

    public sealed partial class FxRenderer
    {
        /// <summary>Renderer +0x18 (FUN_0053ae90 loads it into ST0): the x-offset factor of the PC 0x800 aura copies.</summary>
        public float Field18 { get => M.F32(Address + 0x18); set => M.F32(Address + 0x18, value); }
    }
}

