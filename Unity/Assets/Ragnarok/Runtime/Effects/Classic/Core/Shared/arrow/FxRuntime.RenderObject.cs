// Synced from reference/port/src/RoClassicFx/Shared/arrow/FxRuntime.RenderObject.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>Screen bounds RenderBody (0xbd6080) gathers for vf05 when the object's +0xa5 is set (left/top = screen size, right/bottom = 0 otherwise).</summary>
    public struct FxScreenBounds
    {
        public float Left, Top, Right, Bottom, Rhw;
    }

    /// <summary>
    /// Package arrow: the generic CRenderObject client code CArrowEffect runs — the target lookup FUN_00bd1c50, the 3D
    /// distance FUN_00bce680, the screen angle FUN_00bd0c40, CRenderObject::Render's shadow gate (FUN_00bd7580) and body
    /// (RenderBody 0xbd6080) and the CRenderObject destructor (0xbcdfb0). Ported 1:1 (oracle: tests/Packages/arrow).
    /// </summary>
    public sealed partial class FxRuntime
    {
        static readonly float K134 = BitConverter.Int32BitsToSingle(0x3fab851f);   // [0x1181958] 1.34
        static readonly float K3e7 = BitConverter.Int32BitsToSingle(0x34a10fb0);   // [0xffe824] 3e-7

        /// <summary>
        /// FUN_00a0c100 / FUN_00a0c0b0 on the object at [0x014ef980]: a std::map (+8, size +0xc) keyed by CRenderObject
        /// +0x10c whose node +0x18 float RenderBody adds to its scale argument (a non-zero sum multiplies the screen
        /// scale). Client state outside the engine, filled at runtime (0xa0bbf0 from the job setter 0xbe49f3); null = no
        /// entry (the default; the objects of the effect engine keep +0x10c = 0).
        /// </summary>
        public Func<int, float?> RenderScaleOverrideImpl;

        public float? RenderScaleOverride_a0c100(int key) => RenderScaleOverrideImpl?.Invoke(key);

        // ---- lookups -------------------------------------------------------------------------------------------

        /// <summary>
        /// FUN_00bd1c50 (thiscall on a CRenderObject (aid, float* out), RET 8): the position of the local player when
        /// <paramref name="aid"/> is the player's account id (DAT_0154f2b4), else of the world's actor with that id
        /// (CWorld::GetGameActorByAID) → true; otherwise false and the point mirrored through the object,
        /// <c>obj − (player − obj)</c> per axis.
        /// </summary>
        public bool TargetPosition_bd1c50(uint obj, uint aid, out float x, out float y, out float z)
        {
            uint world = M.U32(Mode + 0xcc);             // GetWorld()->+0xcc
            if (aid == PlayerAid)
            {
                uint p = M.U32(world + 0x2c);
                x = M.F32(p + 0x10); y = M.F32(p + 0x14); z = M.F32(p + 0x18);
                return true;
            }
            uint actor = FindActorByAid(aid);
            if (actor != 0)
            {
                x = M.F32(actor + 0x10); y = M.F32(actor + 0x14); z = M.F32(actor + 0x18);
                return true;
            }
            uint player = M.U32(world + 0x2c);
            float ox = M.F32(obj + 0x10), oy = M.F32(obj + 0x14), oz = M.F32(obj + 0x18);
            x = ox - (M.F32(player + 0x10) - ox);
            y = oy - (M.F32(player + 0x14) - oy);
            z = oz - (M.F32(player + 0x18) - oz);
            return false;
        }

        /// <summary><see cref="TargetPosition_bd1c50(uint, uint, out float, out float, out float)"/> writing the point to <paramref name="outVec"/>.</summary>
        public bool TargetPosition_bd1c50(uint obj, uint aid, uint outVec)
        {
            bool found = TargetPosition_bd1c50(obj, aid, out float x, out float y, out float z);
            M.F32(outVec, x); M.F32(outVec + 4, y); M.F32(outVec + 8, z);
            return found;
        }

        /// <summary>
        /// FUN_00bce680 (thiscall (x, y, z), RET 0xc, result in ST0) — distance from the object to (x, y, z): the
        /// horizontal distance first (FUN_00534e70, rounded to float), then sqrt(h² + dy²).
        /// </summary>
        public float Distance3D_bce680(uint obj, float x, float y, float z)
        {
            float dz = z - M.F32(obj + 0x18);
            float dx = x - M.F32(obj + 0x10);
            float dy = y - M.F32(obj + 0x14);
            float h = SqrtF(dz * dz + dx * dx);
            return SqrtF(h * h + dy * dy);
        }

        /// <summary>
        /// FUN_00bd0c40 (thiscall (heading), RET 4) — a world heading as a screen roll in whole degrees:
        /// <c>(int)(−cameraYaw − heading)</c> (view +0x30, cvttss2si) brought into 0..359 (negative values by
        /// +360·(1 + (−1 − a) / 360), unsigned); 0 without a game mode.
        /// </summary>
        public int ScreenAngle_bd0c40(float heading)
        {
            uint mode = Mode;
            if (mode == 0) return 0;
            uint view = M.U32(mode + 0xd0);
            int a = FxOps.Cvtt(-M.F32(view + 0x30) - heading);
            unchecked
            {
                if (a < 0) a = a + 360 + (int)((uint)(-1 - a) / 360u) * 360;
                if (a >= 360) a = a - (int)((uint)a / 360u) * 360;
            }
            return a;
        }

        // ---- CRenderObject::Render (0xbd6040) -----------------------------------------------------------------

        uint renderFrame;
        const uint RenderFrameMagic = 0x464e5252;   // "RRNF"
        const uint RenderFrameSize = 0x60;

        /// <summary>
        /// FxMemory home of RenderBody's stack temporaries (view point, rhw / colour outputs, blend, int rect, sprite
        /// rect — the helpers it calls take addresses). Cleared after every use, so the heap matches the client, whose
        /// temporaries live on the stack. Allocated on first use (validated like <see cref="SpriteStatics"/>, so a test
        /// harness that rewinds the heap gets a fresh one); touch it before an oracle comparison.
        /// </summary>
        public uint RenderFrame
        {
            get
            {
                uint f = renderFrame;
                if (f != 0 && M.Contains(f, RenderFrameSize + 8) && f < M.BumpPointer && M.U32(f + RenderFrameSize) == RenderFrameMagic
                    && M.U32(f + RenderFrameSize + 4) == f)
                    return f;
                f = M.Alloc(RenderFrameSize + 8);
                M.U32(f + RenderFrameSize, RenderFrameMagic);
                M.U32(f + RenderFrameSize + 4, f);
                renderFrame = f;
                return f;
            }
        }

        /// <summary>
        /// FUN_00bd7580 (thiscall (matrix, scale), RET 8) — the ground shadow of a render object. Ported up to its gates:
        /// nothing for +0x10c == 0x235, when the shadow flag +0xa4 is 0, or when +0x54 or <paramref name="scale"/> is an
        /// (ordered) zero. The drawing behind them (shadow.spr / shadow.act on the ground height) is not ported: no
        /// object of the effect engine sets +0xa4 (CRenderObject::Init clears it).
        /// </summary>
        public void RenderShadow_bd7580(uint obj, uint matrix, float scale)
        {
            if (M.I32(obj + 0x10c) == 0x235) return;
            if (M.U8(obj + 0xa4) == 0) return;
            if (M.F32(obj + 0x54) == 0f) return;
            if (scale == 0f) return;
            // shadow body (0xbd75cd..0xbd791c): not ported, unreachable for the objects this port creates
        }

        /// <summary>
        /// CRenderObject::RenderBody (0xbd6080; thiscall (matrix, byte p2, float p3), RET 0xc) as CRenderObject::Render
        /// (0xbd6040) calls it (p2 = 0, p3 = 0) — the generic one-sprite renderer: ACT motion (+0x38 action, +0x3c frame)
        /// of the object's ACT (+0x108); the position projected (FUN_0053b1a0) and its view-space point; scale =
        /// ScreenScale(rhw), times p3' when p3' = p3 (+ effect 0x946's +0x48 on the owner id +0x110, new system)
        /// (+ <see cref="RenderScaleOverride_a0c100"/>) is not zero; +0x5c = scale / 1.34; x offset = +0x78 · +0x50 ·
        /// renderer +0x18 · +0x5c; FUN_00be5810 (shrink / grow flags); then per clip with a sprite: vf09 (0xbce6f0, culled
        /// → next), the bounds (+0xa5), FUN_00bcff20 extras with blend 0x101, the 0x235 depth nudge, AddSprite (flip mode:
        /// offsets and angle 180) and the inline flash / glows (FUN_00be5740). Ends with +0xec = 1.
        /// Returns whether a clip was drawn; the caller runs the class's vf05 (bounds, sx, sy) when it was, else vf06
        /// (sx, sy) — they write other fields than +0xec, so the order of the stores does not matter.
        /// Not ported: the p2 == 1 seasonal sprite swap ([0x1552d78]) and the +0x48 custom-palette lookup (never set on
        /// the objects of the effect engine).
        /// </summary>
        public bool RenderBody_bd6080(uint obj, uint matrix, out float sx, out float sy, out FxScreenBounds bounds)
        {
            uint frame = RenderFrame;
            uint point = frame, rhwB = frame + 0xc, rhwT = frame + 0x10, colour = frame + 0x14, blend = frame + 0x18;
            uint intRect = frame + 0x20, rect = frame + 0x30;

            uint spr = M.U32(obj + 0x104);
            uint act = M.U32(obj + 0x108);
            uint motion = ActGetMotion(act, M.U32(obj + 0x38), M.U32(obj + 0x3c));
            float x = M.F32(obj + 0x10), y = M.F32(obj + 0x14), z = M.F32(obj + 0x18);
            float vx = y * M.F32(matrix + 0xc) + x * M.F32(matrix);
            vx = vx + z * M.F32(matrix + 0x18);
            vx = vx + M.F32(matrix + 0x24);
            float vy = x * M.F32(matrix + 4) + y * M.F32(matrix + 0x10);
            vy = vy + z * M.F32(matrix + 0x1c);
            vy = vy + M.F32(matrix + 0x28);
            float vz = x * M.F32(matrix + 8) + y * M.F32(matrix + 0x14);
            vz = vz + z * M.F32(matrix + 0x20);
            vz = vz + M.F32(matrix + 0x2c);
            Renderer.ProjectVertex2(M, matrix, x, y, z, out sx, out sy, out float rhw);
            float scale = Renderer.ScreenScale(rhw);

            float p3 = 0f;
            uint boost = EffectMgr != 0 ? FindEffectOfOwner_a4dd90(M.U32(obj + 0x110), 0x946) : 0u;
            if (boost != 0) p3 = p3 + M.F32(boost + 0x48);
            float k = p3;
            float? over = RenderScaleOverride_a0c100(M.I32(obj + 0x10c));
            if (over.HasValue) k = (float)((double)over.Value + (double)p3);   // fadd dword; fstp
            if (!(k == 0f)) scale = k * scale;                                  // UCOMISS/LAHF: only an ordered zero skips
            M.F32(obj + 0x5c, scale / K134);

            float xo = (float)M.I32(obj + 0x78) * M.F32(obj + 0x50);
            xo = (float)((double)Renderer.Field18 * (double)xo);                // FUN_0053ae90; fmul; fstp
            float xoff = xo * M.F32(obj + 0x5c);
            bounds = new FxScreenBounds
            {
                Left = (float)Renderer.ScreenWidth, Top = (float)Renderer.ScreenHeight, Right = 0f, Bottom = 0f, Rhw = rhw,
            };
            ActorRenderScale_be5810(obj, ref scale);

            M.F32(point, vx); M.F32(point + 4, vy); M.F32(point + 8, vz);
            bool drawn = false;
            for (int i = 0; i < M.I32(motion + 0x2c); i++)
            {
                uint clip = MotionGetClip(motion, (uint)i);
                int index = M.I32(clip + 8);
                if (index == -1) continue;
                uint images = M.U32(spr + (uint)(M.I32(clip + 0x20) + 0x6c) * 0xc);
                uint image = M.U32(images + (uint)index * 4);
                if (!SpriteRect_bce6f0(obj, i, i, scale, clip, sx, sy, point, xoff, rhwB, rhwT, image, rect, intRect, colour, 0, 0, false))
                    continue;
                drawn = true;
                M.U32(rect + 0x2c, image);
                if (M.U8(obj + 0xa5) != 0)
                {
                    float l = M.F32(rect + 4), r = M.F32(rect + 0xc);
                    float lo = l < r ? l : r;                                   // minss
                    float hi = l > r ? l : r;                                   // maxss
                    if (bounds.Left > lo) bounds.Left = lo;
                    if (bounds.Top > M.F32(rect)) bounds.Top = M.F32(rect);
                    if (hi > bounds.Right) bounds.Right = hi;
                    if (M.F32(rect + 8) > bounds.Bottom) bounds.Bottom = M.F32(rect + 8);
                }
                uint palette = spr + 0x110;
                M.U32(blend, 0x101);
                GenericLayerExtras_bcff20(obj, rect, clip, intRect, xoff, M.F32(rhwB), M.F32(rhwT), palette, image, blend, M.U32(colour));
                if (M.I32(obj + 0x10c) == 0x235)
                {
                    M.F32(rect + 0x10, M.F32(rhwB) - K3e7);
                    M.F32(rect + 0x14, M.F32(rhwT) - K3e7);
                }
                float offX = 0f, offY = 0f;
                if (FlipScreenY)
                {
                    offY = (sy - M.F32(rect + 8)) + (sy - M.F32(rect));
                    M.F32(rect + 0x1c, 180.0f);
                    offX = (sx - M.F32(rect + 0xc)) + (sx - M.F32(rect + 4));
                }
                AddSpriteRect(obj, rect, palette, M.U32(blend), offX, offY, 0);
                FlashAndGlows_be5740(obj, rect, palette);
            }
            M.U8(obj + 0xec, 1);
            M.Span(frame, (int)RenderFrameSize).Clear();
            return drawn;
        }

        /// <summary>
        /// CRenderObject::~CRenderObject (0x00bcdfb0): vtable CRenderObject, releases the sprite (+0x104) and action
        /// (+0x108) resources (CRes::Release), destroys the instant-var map at +0x11c (FUN_00bcda40; frees its head node —
        /// the objects of the effect engine never put entries in it) and the std::string at +0x1c (heap buffer from
        /// capacity 0x10 on), vtable CGameObject. Does not free the object.
        /// </summary>
        public void DestructRenderObject_bcdfb0(uint obj)
        {
            M.U32(obj, VtCRenderObject);
            uint spr = M.U32(obj + 0x104);
            if (spr != 0) ResRelease(spr);
            uint act = M.U32(obj + 0x108);
            if (act != 0) ResRelease(act);
            M.U32(obj + 0x118, VtCInstantVar);
            M.Free(M.U32(obj + 0x11c));
            uint cap = M.U32(obj + 0x30);
            if (cap >= 0x10)
            {
                uint data = M.U32(obj + 0x1c);
                M.Free(cap + 1 >= 0x1000 ? M.U32(data - 4) : data);   // FUN_004d8220: aligned blocks keep the raw pointer at [-4]
            }
            M.U32(obj + 0x2c, 0);
            M.U32(obj + 0x30, 0xf);
            M.U8(obj + 0x1c, 0);
            M.U32(obj, VtCGameObject);
        }
    }
}
