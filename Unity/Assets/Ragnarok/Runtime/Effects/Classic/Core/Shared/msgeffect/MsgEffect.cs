// Synced from reference/port/src/RoClassicFx/Shared/msgeffect/MsgEffect.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CMsgEffect (vtable 0x0100aa4c, 0x1b8 bytes; CAbleToMakeEffect &gt; CRenderObject &gt; CGameObject) — one glyph of a
    /// damage number (one object per digit), a MISS / LUCKY / critical or Taekwon burst over an actor. Not a CRagEffect,
    /// not a prim: a render object in the world list (world +0x8), processed by vf01 and drawn by vf03 through the
    /// ordinary actor sprite renderer (CRenderObject::Render 0xbd6040 → RenderBody 0xbd6080 → vf09 0xbce6f0).
    /// Created by FUN_00beda30 (numbers, msg 0x58), FUN_00bfd3b0 (MISS, msg 0x59) and FUN_00bfd630 (bursts, LUCKY:
    /// msgs 0x5c / 0x5f / 0x83) — see <see cref="FxRuntime.ShowNumber_beda30"/> and packages/msgeffect/NOTES.md.
    ///
    /// Fields: +0x98 tint (ARGB), +0x15c attached actor (msg 0x32 sender; its x/z are followed every tick), +0x158
    /// visible (byte), +0x160/+0x164/+0x168 drift / rise velocity, +0x178/+0x17c/+0x180 anchor position, +0x184 owner
    /// AID, +0x188 ended (msg 0x31 / 0x35), +0x190 start y, +0x194 start x, +0x198 start zoom, +0x1a8 type,
    /// +0x1ac x offset (pixels, also +0x78), +0x8c creation time (timeGetTime()).
    /// Ported types: 0xd MISS, 0xe number, 0xf critical burst, 0x10 critical number, 0x11 LUCKY, 0x15 multi-hit part,
    /// 0x16 combo total, 0x19 Taekwon burst, 0x72 heal.
    /// </summary>
    public readonly partial struct MsgEffect
    {
        public const uint SizeOf = 0x1b8;
        public const uint VtCMsgEffect = 0x0100aa4c;
        public const uint VtCAbleToMakeEffect = 0x00ffdf40;

        public const int MsgInit = 0xe, MsgOwner = 0x16, MsgOwnerGone = 0x18, MsgDetach = 0x31, MsgAttach = 0x32, MsgEnd = 0x35, MsgDigit = 0x40;

        public const int TypeMiss = 0xd, TypeNumber = 0xe, TypeCriticalBurst = 0xf, TypeCritical = 0x10, TypeLucky = 0x11,
            TypeMultiHit = 0x15, TypeComboTotal = 0x16, TypeComboBurst = 0x19, TypeHeal = 0x72;

        public readonly FxMemory M;
        public readonly uint A;

        public MsgEffect(FxMemory m, uint address) { M = m; A = address; }

        public int Type { get => M.I32(A + 0x1a8); set => M.I32(A + 0x1a8, value); }
        public uint OwnerAid => M.U32(A + 0x184);
        /// <summary>The actor the glyph follows (+0x15c, set by msg 0x32, cleared by msg 0x31); 0 = detached.</summary>
        public uint Anchor => M.U32(A + 0x15c);
        public bool Visible { get => M.U8(A + 0x158) != 0; set => M.U8(A + 0x158, (byte)(value ? 1 : 0)); }
        /// <summary>ACT frame (+0x3c): the digit for numbers.</summary>
        public int Frame => M.I32(A + 0x3c);

        /// <summary>The types whose OnProcess / messages / render path are ported.</summary>
        public static bool IsTypePorted(int type)
            => type == TypeEmotion || type == TypeMiss || type == TypeNumber || type == TypeCriticalBurst || type == TypeCritical || type == TypeLucky
               || type == TypeMultiHit || type == TypeComboTotal || type == TypeComboBurst || type == TypeHeal;

        /// <summary>Number types msg 0x58 (FUN_00beda30) may be asked to show with the ported digit path.</summary>
        public static bool IsNumberType(int type)
            => type == TypeNumber || type == TypeCritical || type == TypeMultiHit || type == TypeComboTotal || type == TypeHeal;

        static float Bits(uint b) => BitConverter.Int32BitsToSingle((int)b);

        static readonly float K08 = Bits(0x3f4ccccd), KNeg08 = Bits(0xbf4ccccd), K001 = Bits(0x3c23d70a);
        static readonly float KPi = Bits(0x40490fd8);        // [0xf4a46c] 3.141592
        static readonly float K13 = Bits(0x3fa66666), K12 = Bits(0x3f99999a);
        static readonly float K024 = Bits(0x3e75c28f), K06 = Bits(0x3f19999a), K34 = Bits(0x4059999a), K345 = Bits(0x405ccccd);
        static readonly float K054 = Bits(0x3f0a3d71), K01 = Bits(0x3dcccccd), K018 = Bits(0x3e3851ec), K015 = Bits(0x3e19999a);

        // ---- construction / destruction ---------------------------------------------------------------

        /// <summary>
        /// CMsgEffect::CMsgEffect (0x00d37b40) with the inlined CAbleToMakeEffect ctor (0x00bcd310): CRenderObject ctor
        /// (shim), an empty effect std::list at +0x144 (0xc sentinel), then the CMsgEffect defaults (+0xa2 = 1 flat
        /// depth, +0x80 = -1 depth bias, zooms 1, +0x1b0 = 1, +0x158 = 1 visible).
        /// </summary>
        public void Construct(FxRuntime rt)
        {
            rt.ConstructRenderObject(A);                 // 0x00bcd750
            M.U32(A, VtCAbleToMakeEffect);
            M.U32(A + 0x144, 0);
            M.U32(A + 0x148, 0);
            uint head = M.Alloc(0xc);                    // operator new(0xc): list sentinel
            M.U32(head, head);
            M.U32(head + 4, head);
            M.U32(A + 0x144, head);
            M.U32(A + 0x138, 0xffffffff);
            M.U32(A + 0x13c, 0);
            M.U32(A + 0x140, 0);
            M.U32(A + 0x14c, 0);
            M.U32(A + 0x150, 0);
            M.U8(A + 0x130, 0);
            // CMsgEffect part (0xd37b75..)
            M.U32(A, VtCMsgEffect);
            M.U32(A + 0x160, 0); M.U32(A + 0x164, 0); M.U32(A + 0x168, 0); M.U32(A + 0x16c, 0);
            M.U32(A + 0x170, 0); M.U32(A + 0x174, 0); M.U32(A + 0x178, 0); M.U32(A + 0x15c, 0);
            M.U32(A + 0x34, 0); M.U32(A + 0x3c, 0);
            M.U8(A + 0xa2, 1);
            M.U32(A + 0x184, 0); M.U32(A + 0x1a8, 0); M.U32(A + 0x188, 0); M.U32(A + 0x18c, 0);
            M.F32(A + 0x190, 1f); M.F32(A + 0x194, 1f); M.F32(A + 0x198, 1f);
            M.U32(A + 0x1a0, 0); M.U32(A + 0x1a4, 0);
            M.U32(A + 0x80, 0xffffffff);
            M.U32(A + 0x1ac, 0); M.U32(A + 0x1b4, 0);
            M.U32(A + 0x1b0, 1);
            M.U32(A + 0x160, 0); M.U32(A + 0x164, 0); M.U32(A + 0x168, 0);
            M.U32(A + 0x178, 0); M.U32(A + 0x17c, 0); M.U32(A + 0x180, 0);
            M.U8(A + 0x158, 1);
        }

        /// <summary>
        /// vf00(1) = 0x00d38330: if attached, the actor gets msg 0x30 (it unlinks the glyph from its list +0x3b0,
        /// <see cref="FxRuntime.ActorMsgEffectGone_bd884f"/>); CAbleToMakeEffect dtor (0x00bcdc60: the effect list is
        /// empty — its sentinel's next is zeroed, then freed), CRenderObject dtor (0x00bcdfb0: SPR / ACT released,
        /// CInstantVar tree head freed, std::string +0x1c reset), operator delete(0x1b8). The caller unlinks the
        /// world-list node.
        /// </summary>
        public void Destroy(FxRuntime rt)
        {
            M.U32(A, VtCMsgEffect);
            uint actor = M.U32(A + 0x15c);
            if (actor != 0) rt.ActorMsgEffectGone_bd884f(actor, A);
            // ~CAbleToMakeEffect (0x00bcdc60)
            M.U32(A, VtCAbleToMakeEffect);
            if (M.U32(A + 0x148) != 0) throw new NotSupportedException("CMsgEffect with launched effects (CAbleToMakeEffect list) is not ported");
            M.U32(A + 0x14c, 0);
            M.U32(A + 0x150, 0);
            uint head = M.U32(A + 0x144);
            M.U32(M.U32(head + 4), 0);                   // last->next = 0 (the empty list: the sentinel itself)
            for (uint n = M.U32(head); n != 0;)
            {
                uint next = M.U32(n);
                M.Free(n);
                n = next;
            }
            M.Free(M.U32(A + 0x144));
            rt.DestructRenderObject_bcdfb0(A);           // ~CRenderObject (package arrow)
            M.Free(A);                                   // operator delete(this, 0x1b8)
        }

        // ---- messages (vf02 0x00d3e620) ------------------------------------------------------------------

        /// <summary>
        /// CMsgEffect::SendMsg (vf02, 0x00d3e620; thiscall (sender, msg, p1..p5 as 64-bit), ret 0x34) for every message
        /// it handles itself except msg 0xe (names: <see cref="Init_e"/>). <paramref name="p1"/>.. are the low dwords:
        /// <list type="bullet">
        /// <item>0x16 (p1 owner AID, p2 type) → +0x184 / +0x1a8 (+ per-type depth / shadow / tint for types 3..7, 0x12, 0x1b, 0x1f);</item>
        /// <item>0x31 → detach (+0x15c = 0; types 7..0xc, 0x12..0x14, 0x17, 0x18 and ≥ 0x1a also end: +0x188 = 1);</item>
        /// <item>0x32 → attach to <paramref name="sender"/> (+0x15c; types 3, 6, 0x12, 0x1b, 0x1f also snap to it);</item>
        /// <item>0x35 → end (+0x188 = 1);</item>
        /// <item>0x40 (p1 anchor position pointer, p2 digit, p3 x offset px) → digit sprite, start position and motion
        /// (types 0xe, 0x10, 0x15, 0x16, 0x72; 0xd keeps x in +0x194).</item>
        /// </list>
        /// Message 0x18 (owner gone) reaches CAbleToMakeEffect::SendMsg (0x00bd7fc0), where it does nothing. Any other
        /// message, and the per-type branches of types that are not ported (3, 6, 0x12, 0x1b, 0x1f velocity / snap),
        /// throw <see cref="NotSupportedException"/>.
        /// </summary>
        public bool SendMsg(FxRuntime rt, uint sender, int msg, int p1 = 0, int p2 = 0, int p3 = 0)
        {
            switch (msg)
            {
                case MsgOwner:
                {
                    M.I32(A + 0x1a8, p2);
                    M.I32(A + 0x184, p1);
                    switch (p2)
                    {
                        case 0x1b:
                            M.U8(A + 0xa2, 0); M.I32(A + 0x80, 0); M.U8(A + 0xa4, 1); M.F32(A + 0x54, 2.0f);
                            break;
                        case 3: case 0x1f:
                            M.U8(A + 0xa2, 0); M.I32(A + 0x80, 0); M.U8(A + 0xa4, 1); M.F32(A + 0x54, 1.0f);
                            break;
                        case 0x12:
                            M.U8(A + 0xa2, 0); M.I32(A + 0x80, 0);
                            break;
                        case 4:
                            rt.SetArgb(A, 0x7f, 0xff, 0, 0);
                            break;
                        case 7:
                            rt.SetArgb(A, 0x7f, -1, -1, -1);   // 0x00d3e916, then the 5 / 6 tail
                            M.U8(A + 0xa2, 0); M.I32(A + 0x80, 5);
                            break;
                        case 5: case 6:
                            M.U8(A + 0xa2, 0); M.I32(A + 0x80, 5);
                            break;
                    }
                    return true;
                }
                case MsgOwnerGone:
                    return true;                         // CAbleToMakeEffect::SendMsg: no case for 0x18
                case MsgDetach:
                {
                    M.U32(A + 0x15c, 0);
                    int t = M.I32(A + 0x1a8);
                    uint idx = (uint)(t - 6);
                    bool keeps = idx <= 0x13 && (t == 6 || (t >= 0xd && t <= 0x11) || t == 0x15 || t == 0x16 || t == 0x19);
                    if (!keeps) M.I32(A + 0x188, 1);
                    return true;
                }
                case MsgAttach:
                {
                    int t = M.I32(A + 0x1a8);
                    M.U32(A + 0x15c, sender);
                    if (t == 3 || t == 6 || t == 0x12 || t == 0x1b || t == 0x1f)
                        throw new NotSupportedException($"CMsgEffect msg 0x32 for type 0x{t:x} is not ported");
                    return true;
                }
                case MsgEnd:
                    M.I32(A + 0x188, 1);
                    return true;
                case MsgDigit:
                    Digit_40(rt, (uint)p1, p2, p3);
                    return true;
                default:
                    throw new NotSupportedException($"CMsgEffect msg 0x{msg:x} is not ported");
            }
        }

        /// <summary>
        /// Msg 0x40 (0x00d3e93b): types 0xe, 0x10, 0x15 and 0x16 (and 0x72) load the damage-number sprite
        /// (<see cref="FxRuntime.DamageNumberSprite"/> + ".spr" / ".act", each formatted into a temporary std::string by
        /// FUN_00a21e90), start at the anchor position lifted by 12 (0x16: 27), frame = digit (0..11, else 0), x offset
        /// +0x78 = +0x1ac = <paramref name="xOffset"/>, zoom +0x50 = +0x198 = 5, depth bias −2, start time = timeGetTime().
        /// 0x72 also keeps x in +0x194. 0xd only copies x to +0x194; 0xf and 0x11..0x14 ignore the message.
        /// </summary>
        void Digit_40(FxRuntime rt, uint pos, int digit, int xOffset)
        {
            int t = M.I32(A + 0x1a8);
            int lift = 0;
            switch (t)
            {
                case 0x16:
                    lift = 0xf;
                    goto case 0xe;
                case 0xe: case 0x10: case 0x15:
                {
                    rt.SetSprNameViaStdString(A, rt.DamageNumberSprName);
                    rt.SetActNameViaStdString(A, rt.DamageNumberActName);
                    M.I32(A + 0x34, 0);
                    M.I32(A + 0x38, 0);
                    M.U32(A + 0x10, M.U32(pos));
                    M.U32(A + 0x14, M.U32(pos + 4));
                    M.U32(A + 0x18, M.U32(pos + 8));
                    float y = M.F32(A + 0x14) - (float)(lift + 0xc);   // movd/cvtdq2ps
                    M.F32(A + 0x14, y);
                    M.F32(A + 0x190, y);
                    break;
                }
                case 0xd:
                    M.U32(A + 0x194, M.U32(A + 0x10));
                    return;
                case 0x72:
                {
                    rt.SetSprNameViaStdString(A, rt.DamageNumberSprName);
                    rt.SetActNameViaStdString(A, rt.DamageNumberActName);
                    M.I32(A + 0x34, 0);
                    M.I32(A + 0x38, 0);
                    float x = M.F32(pos);
                    M.F32(A + 0x10, x);
                    M.U32(A + 0x14, M.U32(pos + 4));
                    M.U32(A + 0x18, M.U32(pos + 8));
                    float y = M.F32(A + 0x14) - 12.0f;
                    M.F32(A + 0x194, x);
                    M.F32(A + 0x14, y);
                    M.F32(A + 0x190, y);
                    break;
                }
                default:
                    return;                               // 0xf, 0x11..0x14 and every other type: nothing
            }
            // 0x00d3eb38
            M.I32(A + 0x3c, digit);
            M.U8(A + 0xa0, 1);
            if (digit < 0 || digit > 0xb) M.I32(A + 0x3c, 0);
            M.I32(A + 0x78, xOffset);
            M.I32(A + 0x1ac, xOffset);
            M.F32(A + 0x50, 5.0f);
            M.F32(A + 0x198, 5.0f);
            M.I32(A + 0x80, -2);
            M.U32(A + 0x8c, rt.TimeGetTimeImpl());
        }

        /// <summary>
        /// Msg 0xe (0x00d3eb8a; p1 position pointer, p2 a pair of std::strings {spr, act}, p3 action): visible, start
        /// time = timeGetTime(), SPR / ACT by name (skipped when both are null — the client tests the pointer), then
        /// SetAction(action, 4, 1) — action 1000 instead plays action 10 with +0x70 = 10000 and a red tint — and the
        /// per-type start: 0xd MISS (y − 12, frame 0, zoom 5, depth −5), 0xf / 0x19 bursts (y − 12, frame 1, zoom 3,
        /// depth −5, tint 0xffaaaaaa), 0x11 LUCKY and the rest (y − 20, 0xb / 0xc / 0x13 / 0x18 differ, not ported).
        /// </summary>
        public void Init_e(FxRuntime rt, uint pos, string spr, string act, int action)
        {
            M.U8(A + 0xa0, 1);
            M.U32(A + 0x8c, rt.TimeGetTimeImpl());
            if (spr != null || act != null)
            {
                rt.SetSprName(A, spr);
                rt.SetActName(A, act);
            }
            if (action == 0x3e8)
            {
                rt.SetAction(A, 10, 4, 0);
                M.I32(A + 0x70, 0x2710);
                M.U32(A + 0x98, 0xffff0000);
            }
            else rt.SetAction(A, action, 4, 1);
            int t = M.I32(A + 0x1a8);
            switch (t)
            {
                case TypeMiss:
                {
                    float x = M.F32(pos);
                    M.F32(A + 0x10, x);
                    M.U32(A + 0x14, M.U32(pos + 4));
                    M.U32(A + 0x18, M.U32(pos + 8));
                    float y = M.F32(A + 0x14) - 12.0f;
                    M.F32(A + 0x194, x);
                    M.I32(A + 0x3c, 0);
                    M.F32(A + 0x50, 5.0f);
                    M.F32(A + 0x198, 5.0f);
                    M.F32(A + 0x14, y);
                    M.F32(A + 0x190, y);
                    M.I32(A + 0x80, -5);
                    break;
                }
                case TypeCriticalBurst: case TypeComboBurst:
                {
                    M.U32(A + 0x10, M.U32(pos));
                    M.U32(A + 0x14, M.U32(pos + 4));
                    M.U32(A + 0x18, M.U32(pos + 8));
                    float y = M.F32(A + 0x14) - 12.0f;
                    M.I32(A + 0x3c, 1);
                    M.F32(A + 0x50, 3.0f);
                    M.F32(A + 0x198, 3.0f);
                    M.I32(A + 0x80, -5);
                    M.F32(A + 0x14, y);
                    M.F32(A + 0x190, y);
                    M.U32(A + 0x98, 0xffaaaaaa);
                    break;
                }
                case 3: case 0x1b: case 0x1f:
                    break;                               // 0x00d3ede2: nothing
                case 6: case 0xb: case 0xc: case 0x13: case 0x18:
                    throw new NotSupportedException($"CMsgEffect msg 0xe for type 0x{t:x} is not ported");
                default:
                {
                    // 0x00d3ed98 (0x11 LUCKY, 4, 5, 7..0xa, 0xe, 0x10, 0x12, 0x14..0x17, 0x1a, 0x1c..0x1e, and any type ≥ 0x20)
                    M.U32(A + 0x10, M.U32(pos));
                    M.U32(A + 0x14, M.U32(pos + 4));
                    float y = M.F32(A + 0x14) - 20.0f;
                    M.U32(A + 0x18, M.U32(pos + 8));
                    M.F32(A + 0x14, y);
                    break;
                }
            }
        }

        // ---- per tick (vf01 0x00d39970) ------------------------------------------------------------------

        /// <summary>FUN_00bd1c10 — time since +0x8c in 24 ms ticks: (float)((double)(uint)(timeGetTime() − start) / 24).</summary>
        public float Ticks_bd1c10(FxRuntime rt) => (float)((double)(rt.TimeGetTimeImpl() - M.U32(A + 0x8c)) / 24.0);

        /// <summary>
        /// FUN_00d3c860 — the anchor follow at the top of OnProcess, for the ported types: 0xd..0x11, 0x15, 0x16, 0x18,
        /// 0x19 copy the attached actor's x / z into the position and the anchor (+0x178 / +0x180; y is not followed)
        /// and set the render flags +0x124 = 0x200 (attached or not); 0x72 copies x / z into the position only — with
        /// no null check (a detached heal number would fault; msg 0x31 ends it first).
        /// </summary>
        public void Follow_d3c860(FxRuntime rt)
        {
            int t = M.I32(A + 0x1a8);
            if ((t >= 0xd && t <= 0x11) || t == 0x15 || t == 0x16 || t == 0x18 || t == 0x19)
            {
                uint a = M.U32(A + 0x15c);
                if (a != 0)
                {
                    float x = M.F32(a + 0x10);
                    M.F32(A + 0x10, x);
                    M.F32(A + 0x178, x);
                    float z = M.F32(a + 0x18);
                    M.F32(A + 0x18, z);
                    M.F32(A + 0x180, z);
                }
                M.U32(A + 0x124, 0x200);
                return;
            }
            if (t == TypeEmotion)
            {
                FollowEmotion_d3ca47();                  // package emotion
                return;
            }
            if (t == 0x72)
            {
                uint a = M.U32(A + 0x15c);
                M.U32(A + 0x10, M.U32(a + 0x10));
                M.U32(A + 0x18, M.U32(a + 0x18));
                return;
            }
            throw new NotSupportedException($"CMsgEffect follow for type 0x{t:x} is not ported");
        }

        /// <summary>
        /// CMsgEffect::OnProcess (vf01, 0x00d39970). Frames without a logic tick do nothing (alive). An ended glyph
        /// (+0x188) dies. Then the anchor follow (<see cref="Follow_d3c860"/>) and the type's motion, with t =
        /// <see cref="Ticks_bd1c10"/> (24 ms ticks since msg 0x40 / 0xe):
        /// <list type="bullet">
        /// <item>0xe / 0x15 / 0x10 digits and 0xf burst: on the first tick a drift (±0.8, ±0.8) diagonally behind the
        /// anchor's heading (option modes 1..3: fixed vectors turned by the camera yaw); x, z = anchor + v·(3 + t/3),
        /// y = start + 8 − (2 − t/30)·t + vy (vy becomes 4/3 after the first tick with y ≠ 0); zoom +0x198 − 0.24t down to
        /// 1.2 (0x10: 1.3; 0xf: −0.144t down to 1); alpha 250 − 3.4t (0xf: 255 − 3.45t, ACT frame t/2); dies when t &gt; 70;</item>
        /// <item>0xd MISS: y = start − 0.54t, alpha 250 − 3t, zoom 1, dies when t &gt; 80;</item>
        /// <item>0x16 combo total: y = start − 0.1t, alpha 250 − 8·(t − 90) after t = 90, zoom += 0.18t per tick while
        /// &lt; 3 (frame-rate dependent), dies when t &gt; 120;</item>
        /// <item>0x19 Taekwon burst: y = start − 0.1t − 15, frame 0, colour (210 − 8·(t − 90), 150, 150, 255) (all −1 =
        /// untouched once t − 90 &gt; 26.25), zoom towards 2 (+0.15t per tick while &lt; 2), dies when t &gt; 120;</item>
        /// <item>0x72 heal: y = start − 0.18t, alpha 250 − 2t, zoom +0x198 − 0.24t down to 1, dies when t &gt; 120;</item>
        /// <item>0x11 LUCKY: ACT frame t / delay, dies after one pass of the action.</item>
        /// </list>
        /// Returns false when the glyph should be deleted.
        /// </summary>
        public bool OnProcess(FxRuntime rt)
        {
            if (rt.TicksThisFrame == 0) return true;
            int type = M.I32(A + 0x1a8);
            if (M.I32(A + 0x188) != 0)
            {
                if (type != 0x12 || M.U8(A + 0x158) == 0) return false;
                throw new NotSupportedException("CMsgEffect type 0x12 is not ported");
            }
            if (!IsTypePorted(type)) throw new NotSupportedException($"CMsgEffect type 0x{type:x} is not ported");
            Follow_d3c860(rt);
            // FUN_00bd4a60 runs first for every type outside 0xd..0x12, 0x15, 0x16, 0x19, 0x72 — of the ported ones only 1.
            if (type == TypeEmotion)
            {
                rt.ActFrameSounds_bd4a60(A);             // package emotion
                return Emotion_d3a74e(rt);
            }
            switch (type)
            {
                case TypeCritical: return Arc(rt, 0x10);
                case TypeNumber: case TypeMultiHit: return Arc(rt, 0xe);
                case TypeCriticalBurst: return Arc(rt, 0xf);
                case TypeMiss: return Miss(rt);
                case TypeComboTotal: return ComboTotal(rt);
                case TypeComboBurst: return ComboBurst(rt);
                case TypeHeal: return Heal(rt);
                default: return Lucky(rt);
            }
        }

        /// <summary>The digit / burst arc (cases 0x00d39a47 type 0x10, 0x00d39dfb types 0xe / 0x15, 0x00d3a3ca type 0xf).</summary>
        bool Arc(FxRuntime rt, int kind)
        {
            float t = Ticks_bd1c10(rt);
            float vx = M.F32(A + 0x160);
            M.U32(A + 0x38, M.U32(A + 0x34));
            int mode = rt.DamageDriftMode;
            // UCOMISS vx, 0; LAHF; TEST AH, 0x44; JP — the drift is chosen only while vx == 0 (ordered)
            if (vx == 0f && M.U32(A + 0x15c) != 0)
            {
                if (mode == 0)
                {
                    int h = FxOps.Cvtt(M.F32(M.U32(A + 0x15c) + 0x4c));
                    float x, z;
                    if (h < 0) { x = K08; z = K08; }
                    else if (h < 0x5a) { x = KNeg08; z = K08; }
                    else if (h < 0xb4) { x = KNeg08; z = KNeg08; }
                    else if (h < 0x10e) { x = K08; z = KNeg08; }
                    else { x = K08; z = K08; }
                    M.F32(A + 0x160, x);
                    M.F32(A + 0x168, z);
                    vx = x;
                }
                else
                {
                    if (mode == 1) { M.F32(A + 0x160, K08); M.F32(A + 0x168, KNeg08); }
                    else if (mode == 2) { M.F32(A + 0x160, K001); M.F32(A + 0x168, K001); }
                    else if (mode == 3) { M.F32(A + 0x160, KNeg08); M.F32(A + 0x168, KNeg08); }
                    vx = RotateDrift(rt);
                }
            }

            float vz = M.F32(A + 0x168);
            float t3 = t / 3.0f;
            M.F32(A + 0x10, vx * t3 + M.F32(A + 0x178) + vx * 3.0f);
            float y0 = M.F32(A + 0x190) + 8.0f;
            M.F32(A + 0x18, vz * t3 + M.F32(A + 0x180) + vz * 3.0f);
            float k = (2.0f - t / 30.0f) * t;
            float y = y0 - k + M.F32(A + 0x164);
            M.F32(A + 0x14, y);
            if (y > 0f || 0f > y)
            {
                if (!(y > 0f)) y = -y;                   // XORPS with the sign mask
                M.F32(A + 0x164, y / (y * 3.0f * 0.25f));
            }

            float z50 = M.F32(A + 0x50);
            float alpha;
            if (kind == 0xf)
            {
                if (z50 > 1.0f) M.F32(A + 0x50, M.F32(A + 0x198) - t * K024 * K06);
                else if (1.0f > z50) M.F32(A + 0x50, 1.0f);
                uint act = rt.GetActRes(A);
                if (M.I32(A + 0x3c) < rt.ActMotionCount(act, M.U32(A + 0x38)) - 1)
                {
                    int n = rt.ActMotionCount(act, M.U32(A + 0x38));
                    M.I32(A + 0x3c, FxOps.Cvtt(t * 0.5f) % n);
                }
                alpha = 255.0f - t * K345;
            }
            else
            {
                float limit = kind == 0x10 ? K13 : K12;
                if (z50 > limit) M.F32(A + 0x50, M.F32(A + 0x198) - t * K024);
                else if (limit > z50) M.F32(A + 0x50, limit);
                alpha = 250.0f - t * K34;
            }
            rt.SetArgb(A, FxOps.Cvtt(alpha), -1, -1, -1);
            return !(t > 70.0f);                         // COMISS t, 70; JBE alive (unordered too)
        }

        [ThreadStatic] static FxMemory rotationScratch;

        /// <summary>
        /// Drift modes 1..3: (+0x160, +0x164, +0x168) turned by MakeRotationY(camera yaw (view +0x48) / 180 · π)
        /// (0x005333e0, built on the client's stack — here in a private scratch memory). Returns the new x.
        /// </summary>
        float RotateDrift(FxRuntime rt)
        {
            float yaw = 0f;
            if (rt.Mode != 0) yaw = M.F32(M.U32(rt.Mode + 0xd0) + 0x48);
            float rad = yaw / 180.0f * KPi;
            var S = rotationScratch ??= new FxMemory(0x100);
            uint m = S.Base + 0x10;
            FxMatrix.MakeRotationY(S, m, rad);
            float vy = M.F32(A + 0x164), vx = M.F32(A + 0x160), vz = M.F32(A + 0x168);
            float nx = S.F32(m) * vx + S.F32(m + 0xc) * vy + S.F32(m + 0x18) * vz + S.F32(m + 0x24);
            float nz = S.F32(m + 8) * vx + S.F32(m + 0x14) * vy + S.F32(m + 0x20) * vz + S.F32(m + 0x2c);
            M.F32(A + 0x160, nx);
            M.F32(A + 0x168, nz);
            return nx;
        }

        /// <summary>Case 0x00d3a1b6 (0xd MISS).</summary>
        bool Miss(FxRuntime rt)
        {
            float t = Ticks_bd1c10(rt);
            M.U32(A + 0x38, M.U32(A + 0x34));
            M.F32(A + 0x14, M.F32(A + 0x190) - t * K054);
            rt.SetArgb(A, FxOps.Cvtt(250.0f - t * 3.0f), -1, -1, -1);
            M.F32(A + 0x50, 1.0f);
            rt.GetActRes(A);
            return !(t > 80.0f);
        }

        /// <summary>Case 0x00d3a22e (0x16 combo total).</summary>
        bool ComboTotal(FxRuntime rt)
        {
            float t = Ticks_bd1c10(rt);
            int fade = 0;
            M.U32(A + 0x38, M.U32(A + 0x34));
            M.F32(A + 0x14, M.F32(A + 0x190) - t * K01);
            if (t > 90.0f) fade = FxOps.Cvtt((t - 90.0f) * 8.0f);
            rt.SetArgb(A, 0xfa - fade, -1, -1, -1);
            float z = M.F32(A + 0x50);
            if (3.0f > z) M.F32(A + 0x50, t * K018 + z);
            else if (z > 3.0f) M.F32(A + 0x50, 3.0f);
            return !(t > 120.0f);
        }

        /// <summary>Case 0x00d3a2e5 (0x19 Taekwon kick burst, bluemsg.spr).</summary>
        bool ComboBurst(FxRuntime rt)
        {
            float t = Ticks_bd1c10(rt);
            int fade = 0;
            M.U32(A + 0x38, M.U32(A + 0x34));
            M.I32(A + 0x3c, 0);
            M.F32(A + 0x14, M.F32(A + 0x190) - t * K01 - 15.0f);
            if (t > 90.0f) fade = FxOps.Cvtt((t - 90.0f) * 8.0f);
            int rg = fade <= 0xd2 ? 0x96 : -1;
            int b = fade <= 0xd2 ? 0xff : -1;
            int a = fade <= 0xd2 ? 0xd2 - fade : 0;
            rt.SetArgb(A, a, rg, rg, b);
            float z = M.F32(A + 0x50);
            if (2.0f > z) M.F32(A + 0x50, t * K015 + z);
            else if (z > 2.0f) M.F32(A + 0x50, 2.0f);
            return !(t > 120.0f);
        }

        /// <summary>Case 0x00d3aae6 (0x72 heal).</summary>
        bool Heal(FxRuntime rt)
        {
            float t = Ticks_bd1c10(rt);
            M.U32(A + 0x38, M.U32(A + 0x34));
            M.F32(A + 0x14, M.F32(A + 0x190) - t * K018);
            rt.SetArgb(A, FxOps.Cvtt(250.0f - t * 2.0f), -1, -1, -1);
            float z = M.F32(A + 0x50);
            if (z > 1.0f) M.F32(A + 0x50, M.F32(A + 0x198) - t * K024);
            else if (1.0f > z) M.F32(A + 0x50, 1.0f);
            return !(t > 120.0f);
        }

        /// <summary>Case 0x00d3a74e (types 0, 1, 2, 0x11 — only 0x11 LUCKY is ported): frame = (int)(t / delay) mod frames.</summary>
        bool Lucky(FxRuntime rt)
        {
            uint act = rt.GetActRes(A);
            float t = Ticks_bd1c10(rt);
            int n = rt.ActMotionCount(act, M.U32(A + 0x38));
            if (n == 0) return true;
            float f = t / M.F32(A + 0x58);
            M.I32(A + 0x3c, FxOps.Cvtt(f) % n);
            f = f / (float)n;
            return f < 1.0f || float.IsNaN(f);         // COMISS f, 1.0; JB alive (unordered too)
        }

        // ---- render (vf03 0x00d3d830) --------------------------------------------------------------------

        /// <summary>
        /// CMsgEffect::Render (vf03, 0x00d3d830): number glyphs (types 0xd..0x10, 0x15, 0x16, 0x72) are drawn only
        /// while <see cref="FxRuntime.ShowDamageNumbers"/> ([0x118232c]); every glyph only while visible (+0x158);
        /// then (tail call) CRenderObject::Render (0x00bd6040): nothing while +0xa0 == 0; the shadow gate
        /// (<see cref="FxRuntime.RenderShadow_bd7580"/>, never drawn: +0xa4 stays 0 for these types); RenderBody (0x00bd6080,
        /// <see cref="FxRuntime.RenderBody_bd6080"/>, package arrow); then vf05 (0x00be6bc0, a no-op) when a clip was drawn,
        /// else vf06 (0x00be7a70: +0xac / +0xb0 = (int) screen x / y).
        /// </summary>
        public void Render(FxRuntime rt, uint viewMatrix)
        {
            int t = M.I32(A + 0x1a8);
            bool number = (t >= 0xd && t <= 0x10) || t == 0x15 || t == 0x16 || t == 0x72;
            if (number && !rt.ShowDamageNumbers) return;
            if (M.U8(A + 0x158) == 0) return;
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
