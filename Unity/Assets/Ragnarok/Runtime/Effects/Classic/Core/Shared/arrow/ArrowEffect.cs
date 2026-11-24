// Synced from reference/port/src/RoClassicFx/Shared/arrow/ArrowEffect.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CArrowEffect (vtable 0x0100a9d4, 0x170 bytes; a plain CRenderObject, not a CRagEffect / CEffectPrim / CEffect) —
    /// the projectile of a basic attack: player arrows (bows, Doram Soul Attack), the arrows and bullets of archer
    /// monsters, and the nine arrows of AC_SHOWER. Created by FUN_00bfb110 (to an actor) or FUN_00bfb1c0 (point to
    /// point), pushed into the world's render-object list (world +0x8), processed (vf01) every world pass and drawn
    /// (vf03) every frame. See packages/arrow/NOTES.md and packages/combat-timing/NOTES.md §5.
    ///
    /// Fields: +0x8c spawn time (timeGetTime), +0xa0 visible (0 until the release tick), +0x138/+0x13c/+0x140 base
    /// position (source, y − 10), +0x144/+0x148/+0x14c velocity per 24 ms tick, +0x150/+0x154/+0x158 stored
    /// destination, +0x15c speed-up k (1, or 40 / distance under 40 units), +0x160 release (24 ms ticks after spawn),
    /// +0x164 target account id (0 = point to point), +0x168 OnProcess calls after the release (frame counter).
    /// The drawing is the generic CRenderObject renderer (Render 0xbd6040 → RenderBody 0xbd6080) on ACT action
    /// +0x34 = 0, frame +0x3c, rotated on screen by +0x7c.
    /// </summary>
    public readonly partial struct ArrowEffect
    {
        public const uint SizeOf = 0x170;
        public const uint VtCArrowEffect = 0x0100a9d4;
        public const int MsgToActor = 0xe, MsgToPoint = 0x40;

        public readonly FxMemory M;
        public readonly uint A;

        public ArrowEffect(FxMemory m, uint address) { M = m; A = address; }

        public float X => M.F32(A + 0x10);
        public float Y => M.F32(A + 0x14);
        public float Z => M.F32(A + 0x18);
        /// <summary>+0xa0: 0 until the release tick (invisible), then 1.</summary>
        public bool Launched => M.U8(A + 0xa0) != 0;
        public uint SpawnTime => M.U32(A + 0x8c);
        public float ReleaseTicks => M.F32(A + 0x160);
        public uint TargetAid => M.U32(A + 0x164);
        /// <summary>+0x15c: 1 for a flight of 8 ticks (≥ 40 units), 40 / distance below.</summary>
        public float SpeedUp => M.F32(A + 0x15c);
        /// <summary>+0x7c: screen roll of the sprite in whole degrees (FUN_00bd0c40 of the heading).</summary>
        public int ScreenAngle => M.I32(A + 0x7c);

        // ---- ctor / dtor ---------------------------------------------------------------------------------------

        /// <summary>CArrowEffect::CArrowEffect (0x00d37790): CRenderObject ctor (shim), then the arrow defaults.</summary>
        public void Construct(FxRuntime rt)
        {
            rt.ConstructRenderObject(A);                 // 0x00bcd750
            M.U32(A, VtCArrowEffect);
            for (uint o = 0x138; o <= 0x158; o += 4) M.U32(A + o, 0);
            M.U32(A + 0x34, 0);
            M.U32(A + 0x138, M.U32(A + 0x10));
            M.U32(A + 0x13c, M.U32(A + 0x14));
            M.U32(A + 0x140, M.U32(A + 0x18));
            M.U32(A + 0x160, 0);
            M.U32(A + 0x144, 0);
            M.U32(A + 0x148, 0);
            M.U32(A + 0x14c, 0);
            M.U32(A + 0x3c, 0);
            M.U32(A + 0x168, 0);
        }

        /// <summary>
        /// vf00(1) = 0x00d38100: CRenderObject::~CRenderObject (0x00bcdfb0) — releases the SPR / ACT resources,
        /// destroys the (empty) instant-var map at +0x11c and the std::string at +0x1c — then operator delete. The caller
        /// unlinks the object from the world list.
        /// </summary>
        public void Destroy(FxRuntime rt)
        {
            M.U32(A, VtCArrowEffect);
            rt.DestructRenderObject_bcdfb0(A);
            M.Free(A);
        }

        // ---- messages ------------------------------------------------------------------------------------------

        /// <summary>
        /// CArrowEffect::SendMsg (vf02, 0x00d3d9f0) msg 0xe — FUN_00bfb110's message: sprite / action by
        /// <paramref name="spriteClass"/> (the 64-bit p4: 0 for player arrows, the attacker's render job for monsters
        /// and the Doram Soul Attack), position = base = <paramref name="src"/> − (0, 10, 0), target account id,
        /// stored destination = FUN_00bd1c50(target) (the target's position, or 2·base − player when it is unknown),
        /// invisible, spawn time = timeGetTime(), release ticks = *p3 when p3 is not null (<paramref name="release"/>).
        /// </summary>
        public void MsgLaunchToActor(FxRuntime rt, float srcX, float srcY, float srcZ, uint targetAid, float? release, long spriteClass)
        {
            SpriteNames(spriteClass, out string spr, out string act);
            rt.SetSprName(A, spr);                      // 0x00be6c80
            rt.SetActName(A, act);                      // 0x00be3a70
            M.F32(A + 0x15c, 1.0f);
            M.F32(A + 0x10, srcX);
            M.F32(A + 0x14, srcY);
            M.F32(A + 0x18, srcZ);
            M.F32(A + 0x14, M.F32(A + 0x14) - 10.0f);   // [0xf4b624]
            M.F32(A + 0x138, srcX);
            M.U32(A + 0x13c, M.U32(A + 0x14));
            M.U32(A + 0x140, M.U32(A + 0x18));
            M.U32(A + 0x164, targetAid);
            M.U8(A + 0xa0, 0);
            rt.TargetPosition_bd1c50(A, targetAid, A + 0x150);
            M.U32(A + 0x8c, rt.TimeGetTimeImpl());
            if (release.HasValue) M.F32(A + 0x160, release.Value);
        }

        /// <summary>
        /// CArrowEffect::SendMsg msg 0x40 — FUN_00bfb1c0's message (AC_SHOWER): always the standard arrow sprite,
        /// base = <paramref name="src"/> − (0, 10, 0), stored destination = <paramref name="dst"/> − (0, 10, 0), no target,
        /// invisible, spawn time = timeGetTime(), release ticks = *p3 when p3 is not null.
        /// </summary>
        public void MsgLaunchToPoint(FxRuntime rt, float srcX, float srcY, float srcZ, float dstX, float dstY, float dstZ, float? release)
        {
            rt.SetSprName(A, ArrowSpr);
            rt.SetActName(A, ArrowAct);
            M.F32(A + 0x15c, 1.0f);
            M.F32(A + 0x10, srcX);
            M.F32(A + 0x14, srcY);
            float y = M.F32(A + 0x14) - 10.0f;
            M.F32(A + 0x18, srcZ);
            M.F32(A + 0x138, srcX);
            M.F32(A + 0x140, srcZ);
            M.F32(A + 0x14, y);
            M.F32(A + 0x13c, y);
            M.F32(A + 0x150, dstX);
            M.F32(A + 0x154, dstY);
            float dy = M.F32(A + 0x154) - 10.0f;
            M.F32(A + 0x158, dstZ);
            M.U8(A + 0xa0, 0);
            M.U32(A + 0x164, 0);
            M.F32(A + 0x154, dy);
            M.U32(A + 0x8c, rt.TimeGetTimeImpl());
            if (release.HasValue) M.F32(A + 0x160, release.Value);
        }

        /// <summary>
        /// Any other message (or 0xe / 0x40 with a non-zero high dword) goes to CRenderObject::SendMsg (0x00be3a60, a
        /// no-op) with p5 replaced by 0; forwarded to <see cref="FxRuntime.GameObjectSendMsg"/> (the hosts' hook).
        /// </summary>
        public void MsgOther(FxRuntime rt, uint sender, long msg, long p1, long p2, long p3, long p4)
            => rt.GameObjectSendMsg(A, sender, msg, p1, p2, p3, p4, 0);

        // ---- sprites -------------------------------------------------------------------------------------------

        const string Mon = "¸ó½ºÅÍ\\";      // 몬스터\ (CP949, one char per byte)

        /// <summary>몬스터\skel_archer_arrow.spr (0x0100b3f8): the arrow of players, most archer monsters and AC_SHOWER.</summary>
        public const string ArrowSpr = Mon + "skel_archer_arrow.spr";
        /// <summary>몬스터\skel_archer_arrow.act (0x0100b418).</summary>
        public const string ArrowAct = Mon + "skel_archer_arrow.act";

        /// <summary>
        /// The sprite / action names of msg 0xe by class (0xd3da0d..0xd3dd33): classes outside the unsigned 64-bit
        /// range [1410, 22176] and every class without an entry get <see cref="ArrowSpr"/>. Names keep the case the
        /// client spells them in (the resource manager is case-insensitive).
        /// </summary>
        public static void SpriteNames(long spriteClass, out string spr, out string act)
        {
            string name = (ulong)spriteClass < 0x582 || (ulong)spriteClass > 0x56a0 ? null : BulletName((int)spriteClass);
            if (name == null)
            {
                spr = ArrowSpr;
                act = ArrowAct;
                return;
            }
            spr = Mon + name + ".spr";
            act = Mon + name + ".act";
        }

        static string BulletName(int c)
        {
            switch (c)
            {
                case 1410: case 1550: return "PEACH_TREE_BULLET";
                case 1412: case 1531: return "GINSENG_BULLET";
                case 1495: case 20274: return "STONE_SHOOTER_BULLET";
                case 1498: case 20275: case 20279: return "WOOTAN_SHOOTER_BULLET";
                case 1500: case 1555: return "PARASITE_BULLET";
                case 1664: return "CANON_BULLET";
                case 1665: return "CANON_BULLET_1";
                case 1666: return "CANON_BULLET_2";
                case 1667: return "CANON_BULLET_3";
                case 1688: case 2021: return "LADY_TANEE_BULLET";
                case 1865: return "RAGGED_ZOMBIE_BULLET";
                case 2364: return "PETAL_BULLET";
                case 2475: return "MG_CORRUPTION_ROOT_BULLET";
                case 3882: return "e_nyailo_ammo";
                case 4217: case 4218: case 4219: case 4220: case 4221: case 4308: case 4315: return "su_soulattack";
                case 20575: return "mg_corruption_root_bullet";
                case 20851: return "SUMMON_HELLTREE_FRUIT";
                case 21520: case 21589: return "EP19_LIMACINA_BULLET";
                case 21970: return "phylla_bullet";
                case 21985: case 22001: return "canon_bullet_3";
                case 22175: case 22176: return "RIGEL_BULLET";
                default: return null;
            }
        }

        // ---- process -------------------------------------------------------------------------------------------

        /// <summary>
        /// CArrowEffect::OnProcess (vf01, 0x00d393f0). t = (timeGetTime() − spawn) / 24 (double, then float). While
        /// release &gt; t: nothing (invisible). First call at or after the release: visible; destination = the target's
        /// live position (FUN_00bd1c50) when the target id is set and still found, else the stored destination;
        /// y −= 10; velocity = (destination − position) · 0.125; distance d = FUN_00bce680; when 40 &gt; d: k = 40 / d and
        /// velocity ·= k; face the destination (FUN_00bce590); position += unit direction · 2.5 (overwritten below).
        /// Then every call: tl = t − release; +0x168++ and, past 8 calls, the ACT frame +0x3c advances once per call
        /// (held on the last frame); +0x7c = FUN_00bd0c40(heading); position = base + velocity · tl; dead when
        /// tl &gt; 8 / k.
        /// </summary>
        public bool OnProcess(FxRuntime rt)
        {
            uint elapsed = rt.TimeGetTimeImpl() - M.U32(A + 0x8c);
            float t = (float)((double)elapsed / 24.0);   // cvtdq2pd + unsigned fix-up, divsd [0xf7ed50], cvtpd2ps
            if (M.F32(A + 0x160) > t) return true;      // COMISS; JA (unordered falls through)
            if (M.U8(A + 0xa0) == 0)
            {
                M.U8(A + 0xa0, 1);
                float x, y, z;
                uint aid = M.U32(A + 0x164);
                if (aid != 0 && rt.TargetPosition_bd1c50(A, aid, out float tx, out float ty, out float tz))
                {
                    x = tx; y = ty; z = tz;
                }
                else
                {
                    z = M.F32(A + 0x158);
                    x = M.F32(A + 0x150);
                    y = M.F32(A + 0x154);
                }
                y = y - 10.0f;
                float vz = z - M.F32(A + 0x18);
                float vx = x - M.F32(A + 0x10);
                float vy = y - M.F32(A + 0x14);
                M.F32(A + 0x14c, vz);
                M.F32(A + 0x148, vy);
                M.F32(A + 0x148, M.F32(A + 0x148) * 0.125f);   // [0xf7ed48]
                vx = vx * 0.125f;
                M.F32(A + 0x144, vx);
                M.F32(A + 0x14c, M.F32(A + 0x14c) * 0.125f);
                float dist = rt.Distance3D_bce680(A, x, y, z);
                if (40.0f > dist)                               // [0xf7d138]; COMISS; JBE (unordered skips)
                {
                    float k = 40.0f / dist;
                    M.F32(A + 0x15c, k);
                    M.F32(A + 0x144, k * M.F32(A + 0x144));
                    M.F32(A + 0x148, k * M.F32(A + 0x148));
                    M.F32(A + 0x14c, M.F32(A + 0x15c) * M.F32(A + 0x14c));
                }
                rt.FaceTowards(A, x, z);                        // 0x00bce590
                float dy = y - M.F32(A + 0x14);
                float dx = x - M.F32(A + 0x10);
                float dz = z - M.F32(A + 0x18);
                float len2 = dy * dy + dx * dx;
                len2 = len2 + dz * dz;
                float inv = (float)(1.0 / (double)FxRuntime.SqrtF(len2));   // FUN_00534e70; fld1; fdivrp; fstp dword
                M.F32(A + 0x10, inv * dx * 2.5f + M.F32(A + 0x10));         // [0xf4a8c8]
                float ny = inv * dy;
                float nz = inv * dz;
                ny = ny * 2.5f;
                nz = nz * 2.5f;
                M.F32(A + 0x14, ny + M.F32(A + 0x14));
                M.F32(A + 0x18, nz + M.F32(A + 0x18));
            }
            float tl = t - M.F32(A + 0x160);
            M.I32(A + 0x168, M.I32(A + 0x168) + 1);
            M.U32(A + 0x38, M.U32(A + 0x34));
            if (M.I32(A + 0x168) > 8)
            {
                M.I32(A + 0x3c, M.I32(A + 0x3c) + 1);
                if (M.I32(A + 0x3c) >= rt.ActMotionCount(rt.GetActRes(A), M.U32(A + 0x38)))
                    M.I32(A + 0x3c, rt.ActMotionCount(rt.GetActRes(A), M.U32(A + 0x38)) - 1);
            }
            M.I32(A + 0x7c, rt.ScreenAngle_bd0c40(M.F32(A + 0x4c)));
            M.F32(A + 0x10, tl * M.F32(A + 0x144) + M.F32(A + 0x138));
            M.F32(A + 0x14, tl * M.F32(A + 0x148) + M.F32(A + 0x13c));
            M.F32(A + 0x18, tl * M.F32(A + 0x14c) + M.F32(A + 0x140));
            float limit = 8.0f / M.F32(A + 0x15c);             // [0xf4eca8]
            return !(tl > limit);                               // COMISS; JBE alive (unordered: alive)
        }

        // ---- render --------------------------------------------------------------------------------------------

        /// <summary>
        /// vf03 = CRenderObject::Render (0x00bd6040) for this class: nothing while invisible (+0xa0 == 0); the shadow
        /// (FUN_00bd7580, never drawn: +0xa4 stays 0); RenderBody (0x00bd6080) with p2 = 0, p3 = 0; then the class's vf05
        /// (0x00be6bc0, a no-op) when a clip was drawn, else vf06 (0x00be7a70: +0xac/+0xb0 = (int) screen x / y).
        /// </summary>
        public void Render(FxRuntime rt, uint viewMatrix)
        {
            if (M.U8(A + 0xa0) == 0) return;
            rt.RenderShadow_bd7580(A, viewMatrix, M.F32(A + 0x50));
            bool drawn = rt.RenderBody_bd6080(A, viewMatrix, out float sx, out float sy, out _);
            if (!drawn)
            {
                M.I32(A + 0xac, FxOps.Cvtt(sx));
                M.I32(A + 0xb0, FxOps.Cvtt(sy));
            }
            M.U8(A + 0xec, 1);
        }
    }
}
