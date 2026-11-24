// Synced from reference/port/src/RoClassicFx/FxRuntime.Client.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Client functions outside the effect engine that the ported effects call: CRenderObject
    /// action / colour setters, CGameMode messages, the after-image actor and a few math helpers
    /// (sprite/act resources: FxRuntime.Sprites.cs).
    /// Everything that touches resources, the game mode or the OS is a shim (the client would reach the
    /// resource manager, UI, camera or winmm); the oracle tests install the same shims at the original
    /// addresses (tests/Oracle/ClientShimHooks.cs) so both sides write identical bytes. The pure helpers
    /// are ports and run natively in the oracle. Strings holding CP949 bytes are one char per byte
    /// (Latin-1), the same convention as <see cref="ReadCString"/>.
    /// </summary>
    public sealed partial class FxRuntime
    {
        // ---- CRenderObject actions (sprite/act resources: FxRuntime.Sprites.cs) --------------

        /// <summary>
        /// CActRes::GetDelay (FUN_006c16b0; thiscall (action), ret 4): the per-action frame delay from the ACT
        /// resource's delay vector (+0x12c/+0x130), 4.0 (DAT_00f4a764) for an action outside it (unsigned compare).
        /// Port; runs natively in the oracle on the same heap objects.
        /// </summary>
        public float ActGetDelay(uint actRes, int action)
        {
            uint begin = M.U32(actRes + 0x12c);
            uint count = (M.U32(actRes + 0x130) - begin) >> 2;
            return (uint)action < count ? M.F32(begin + (uint)action * 4) : 4.0f;
        }

        /// <summary>Observer of <see cref="SetAction"/> calls (render object, action, speed, type).</summary>
        public Action<uint, int, int, int> SetActionTrace;

        /// <summary>
        /// CRenderObject::SetAction (vtable slot 7, 0xbe3c20; thiscall (action, speed, type), ret 0xc) — port.
        /// Delay = <see cref="ActGetDelay"/>(act at +0x108, action); +0x58 (motion speed) = delay if &gt; 0, at
        /// least 1, times +0x64; then action → +0x34/+0x38, 0 → +0x3c, type → +0x6c, +0x9d = 0, +0x60 = +0x64 = 1,
        /// +0xc8/+0xd8/+0xdc = 0. The speed argument is unused by the client. Assumes DAT_015127dc (a global
        /// speed-scale switch that multiplies by a value from 0xaaee40/0xaabd20) is off, as in normal play.
        /// </summary>
        public void SetAction(uint obj, int action, int speed, int type)
        {
            SetActionTrace?.Invoke(obj, action, speed, type);
            float delay = ActGetDelay(M.U32(obj + 0x108), action);
            if (delay > 0f) M.F32(obj + 0x58, delay);                 // COMISS; JBE skips unordered too
            float x = M.F32(obj + 0x58);
            if (1.0f > x)                                             // COMISS [0xf4a468],x; JBE
            {
                M.F32(obj + 0x58, 1.0f);
                x = 1.0f;
            }
            x *= M.F32(obj + 0x64);
            if (x > 0f) M.F32(obj + 0x58, x);
            M.I32(obj + 0x34, action);
            M.I32(obj + 0x38, action);
            M.I32(obj + 0x3c, 0);
            M.I32(obj + 0x6c, type);
            M.U8(obj + 0x9d, 0);
            M.F32(obj + 0x60, 1.0f);
            M.F32(obj + 0x64, 1.0f);
            M.I32(obj + 0xc8, 0);
            M.I32(obj + 0xd8, 0);
            M.I32(obj + 0xdc, 0);
        }

        /// <summary>
        /// CRenderObject::SetARGB (0xbe3d40, thiscall, ret 0x10): each channel that is not -1 is stored as a
        /// byte — a at +0x9b, r at +0x9a, g at +0x99, b at +0x98. Port; runs natively in the oracle.
        /// </summary>
        public void SetArgb(uint obj, int a, int r, int g, int b)
        {
            if (a != -1) M.U8(obj + 0x9b, (byte)a);
            if (r != -1) M.U8(obj + 0x9a, (byte)r);
            if (g != -1) M.U8(obj + 0x99, (byte)g);
            if (b != -1) M.U8(obj + 0x98, (byte)b);
        }

        /// <summary>timeGetTime (winmm import) as seen by the engine; tests install the same source in the oracle.</summary>
        public Func<uint> TimeGetTimeImpl = () => 0;

        /// <summary>
        /// FUN_00be5f40 (thiscall on a render object, RET 0x20): starts a timed colour/scale pulse
        /// (+0xb5.. bytes, +0xcc/+0xd0 ints, +0xe4 end time = timeGetTime() + duration when enabled).
        /// </summary>
        public void StartRenderPulse(uint obj, byte a1, byte a2, byte a3, byte a4, byte a5, int enable, int duration, int a8)
        {
            M.U8(obj + 0xb5, a1);
            M.U8(obj + 0xb6, a2);
            M.U8(obj + 0xc5, a2);
            M.U8(obj + 0xc1, a3);
            M.U8(obj + 0xc3, a4);
            M.U8(obj + 0xc4, a5);
            M.I32(obj + 0xcc, a8);
            M.I32(obj + 0xd0, enable);
            M.I32(obj + 0xd4, 0);
            M.I32(obj + 0xc8, 1);
            M.I32(obj + 0xdc, 0);
            M.I32(obj + 0xd8, 0);
            if (enable != 0)
            {
                M.U32(obj + 0xe4, TimeGetTimeImpl() + (uint)duration);
                M.I32(obj + 0xe0, 1);
            }
            else
            {
                M.I32(obj + 0xe4, 0);
                M.I32(obj + 0xe0, 1);
            }
        }

        // ---- game mode / owner messages ----------------------------------------------------

        /// <summary>
        /// Receives CGameMode::SendMsg (0xc14090, vtable slot 6 of the mode returned by GetWorld; thiscall, 5 ints,
        /// ret 0x14) as issued by effects — e.g. 0x7a starts the screen quake (Meteor Storm, Stave Crasher,
        /// Falcon Assault), 0x87 sets the ambient colour. Client-side (UI / camera / sound); the engine only
        /// issues it. For messages whose arguments are pointers to floats in the original (0x87), the port
        /// passes the float bit patterns instead.
        /// </summary>
        public Action<int, int, int, int, int> GameModeSendMsgImpl;

        /// <summary>CGameMode::SendMsg shim.</summary>
        public void GameModeSendMsg(int msg, int a1, int a2, int a3, int a4) => GameModeSendMsgImpl?.Invoke(msg, a1, a2, a3, a4);

        /// <summary>
        /// Virtual CGameObject::SendMsg (vtable slot 2, 13 stdcall args) of an effect owner. For a plain
        /// CRenderObject (vf02 0xbe3a60, "ret 0x34") it does nothing; actors react (e.g. Sightrasher sends
        /// (0, 0x6d, 0, 0x16, 0…) to its caster). Engine-side no-op by default; hosts hook it.
        /// </summary>
        public Action<uint, uint[]> OwnerSendMsgImpl;

        public void OwnerSendMsg(uint owner, params uint[] args) => OwnerSendMsgImpl?.Invoke(owner, args);

        /// <summary>
        /// The same virtual SendMsg with its arguments as the engine spells them: (sender, msg, p1..p5), all
        /// 64-bit except the sender (13 dwords). Actors react to e.g. 0x7f / 0x80 (colour / state changes used by
        /// the Aid Berserk Potion effect). Forwarded to <see cref="OwnerSendMsg"/>.
        /// </summary>
        public void GameObjectSendMsg(uint obj, uint sender, long msg, long p1, long p2, long p3, long p4, long p5)
        {
            var args = new uint[13];
            args[0] = sender;
            long[] q = { msg, p1, p2, p3, p4, p5 };
            for (int i = 0; i < 6; i++)
            {
                args[1 + i * 2] = (uint)q[i];
                args[2 + i * 2] = (uint)((ulong)q[i] >> 32);
            }
            OwnerSendMsg(obj, args);
        }

        // ---- actors ------------------------------------------------------------------------

        /// <summary>
        /// DAT_0154f2b4 — the local player's account id. Prims that home in on an actor store the
        /// target id as a float (f_38c) and compare it with <c>(float)(uint)PlayerAid</c>.
        /// </summary>
        public uint PlayerAid;

        /// <summary>Override for <see cref="FindActorByAid"/> (client actor lookup).</summary>
        public Func<uint, uint> FindActorByAidImpl;

        /// <summary>
        /// CWorld::GetGameActorByAID (0x9f8aa0, thiscall on the world, RET 4) shim: walks the world's
        /// actor list (std::list at world+0x10, node+8 = actor, actor+0x110 = AID) and returns the actor
        /// or 0. A world without that list (0 at world+0x10) has no actors.
        /// </summary>
        public uint FindActorByAid(uint aid)
        {
            if (FindActorByAidImpl != null) return FindActorByAidImpl(aid);
            uint head = M.U32(World + 0x10);
            if (head == 0) return 0;
            for (uint node = M.U32(head); node != head; node = M.U32(node))
            {
                uint actor = M.U32(node + 8);
                if (M.U32(actor + 0x110) == aid) return actor;
            }
            return 0;
        }

        /// <summary>Override for <see cref="LaunchEffectOn"/>: (actor, effect id) → new CRagEffect address or 0.</summary>
        public Func<uint, int, uint> LaunchEffectOnImpl;

        /// <summary>
        /// CAbleToMakeEffect::LaunchEffect (0xbd1f40, thiscall on the actor, RET 0x14) shim: creates effect
        /// <paramref name="efId"/> on <paramref name="actor"/>. Returns the effect or 0 (the default: no host).
        /// </summary>
        public uint LaunchEffectOn(uint actor, int efId) => LaunchEffectOnImpl != null ? LaunchEffectOnImpl(actor, efId) : 0u;

        /// <summary>
        /// Shim source for FUN_00bd19d0 — a per-actor height constant of the owner game object (job / mount
        /// dependent: 0 or one of a few constants; prim type 0x23 adds it to its slot height). Client state
        /// outside the engine: the host supplies it. Default: 0.
        /// </summary>
        public Func<uint, float> ActorHeightOffsetImpl = owner => 0f;

        /// <summary>FUN_00bd19d0 (thiscall on the owner, no args, result in ST0) — see <see cref="ActorHeightOffsetImpl"/>.</summary>
        public float ActorHeightOffset_bd19d0(uint owner) => ActorHeightOffsetImpl(owner);

        // ---- skill cooldown clock (prim type 0, 'b') ---------------------------------------

        /// <summary>
        /// Skill-cooldown clock shim for prim type 0 with f_3e8 == 'b' (fn_adba70 / fn_aedbf0): the client scans
        /// its cooldown vector DAT_012c1fb8..DAT_012c1fbc for the prim's skill id (f_3f8) and computes the
        /// remaining seconds with timeGetTime / the server clock. Returns null when the skill has no entry
        /// (the vector is empty in the oracle), otherwise the value stored into f_3f4.
        /// </summary>
        public Func<int, int?> SkillCooldownSecondsImpl_adba70 = skillId => null;

        /// <summary>
        /// UI tail of fn_aedbf0 for the cooldown clock ('b'): draws the remaining-time text for matching
        /// cooldown entries (FUN_00bcb6e0 on DAT_015393b4) and flushes the text renderer
        /// (FUN_00bcb4e0 / FUN_00bcb660 depending on DAT_014eea94). Client UI — shim, default no-op.
        /// </summary>
        public Action<uint> SkillCooldownTextImpl_aedbf0 = prim => { };

        // ---- CBlurPC (Mirror Image after-image) --------------------------------------------

        /// <summary>CBlurPC vtable (a CPc subclass: the after-image copy of a player actor).</summary>
        public const uint VtCBlurPc = 0x0100211c;

        /// <summary>
        /// CBlurPC::CBlurPC(CPc* source) (FUN_00cbda20; thiscall, ret 4) — shim. The client builds a full player
        /// actor clone (CPc ctor 0xcc4080, job/sex/palette/hair/name copied from the source through its vtable).
        /// The port lays down a CRenderObject base at the source's position (so SetARGB / the world list work on
        /// it), the CBlurPC vtable at +0 and the source actor at +0x504 (the one field the ctor stores verbatim);
        /// hosts create the visible after-image from <see cref="BlurPcSpawnedImpl"/>.
        /// </summary>
        public void ConstructBlurPc_cbda20(uint obj, uint source)
        {
            ConstructRenderObject(obj);
            M.U32(obj + 0x10, M.U32(source + 0x10));
            M.U32(obj + 0x14, M.U32(source + 0x14));
            M.U32(obj + 0x18, M.U32(source + 0x18));
            M.U32(obj, VtCBlurPc);
            M.U32(obj + 0x504, source);
        }

        /// <summary>Host notification for CBlurPC::SendMsg: (blur object, source actor, msg, x, y, z).</summary>
        public Action<uint, uint, int, long, long, long> BlurPcSpawnedImpl;

        /// <summary>
        /// CBlurPC::SendMsg (vtable slot 2, 0xcc13a0; thiscall, 13 dwords, ret 0x34) — shim: forwarded to the host.
        /// Called by Mirror Image with msg 0 and the colour (155, 155, 255) as three 64-bit parameters.
        /// </summary>
        public void BlurPcSendMsg_cc13a0(uint obj, uint sender, int msg, int par1, long x, long y, long z)
            => BlurPcSpawnedImpl?.Invoke(obj, M.U32(obj + 0x504), msg, x, y, z);

        // ---- sounds ------------------------------------------------------------------------

        /// <summary>The client's absolute PlaySound3D (no player-relative subtraction) with the usual 0xfa/0x28/1.0/0 tail.</summary>
        public void PlaySound3DAbsolute(string wav, float x, float y, float z) => Host?.PlaySound3D(wav, x, y, z, 0xfa, 0x28, 1.0f, 0);

        /// <summary>
        /// FUN_00ce8810 — random "enemy hit" wav for an element (3 fire, 4 wind, anything else normal).
        /// Pure logic over CRT rand() (one call); runs natively in the oracle. rand() is never negative,
        /// so the MSVC signed-modulo fix-ups reduce to plain masks.
        /// </summary>
        public string EnemyHitWav_ce8810(int element)
        {
            if (element == 3)
                return (Rand.Next() & 1) == 0 ? "_enemy_hit_fire1.wav" : "_enemy_hit_fire2.wav";
            if (element == 4)
                return (Rand.Next() & 1) == 0 ? "_enemy_hit_wind1.wav" : "_enemy_hit_wind2.wav";
            switch (Rand.Next() & 3)
            {
                case 0: return "_enemy_hit_normal1.wav";
                case 1: return "_enemy_hit_normal2.wav";
                case 2: return "_enemy_hit_normal3.wav";
                default: return "_enemy_hit_normal4.wav";
            }
        }

        // ---- math helpers of the client ----------------------------------------------------

        /// <summary>FUN_00534e70 — x87 fsqrt of a float argument (53-bit precision), rounded back to float.</summary>
        public static float SqrtF(float v) => (float)Math.Sqrt(v);

        /// <summary>FUN_00bce640 (0xbce640; thiscall (x, z)) — horizontal distance from the object to (x, z) (x87 fsqrt, float).</summary>
        public float DistanceXZ(uint obj, float x, float z)
        {
            float dz = z - M.F32(obj + 0x18);
            float dx = x - M.F32(obj + 0x10);
            return SqrtF(dz * dz + dx * dx);
        }

        /// <summary>
        /// FUN_00bce590 (0xbce590; CRenderObject, thiscall (x, z), ret 8) — turn the object towards (x, z):
        /// unless both offsets are exactly zero (UCOMISS/LAHF: NaN counts as different), +0x4c = atan2(dx, -dz)
        /// (CRT _CIatan2 in double, rounded to float) / 3.141592 * 180, minus 360 when ≥ 360 (COMISS; JC:
        /// unordered skips), plus 360 when &lt; 0. .NET's Math.Atan2 stands in for the CRT atan2; the oracle
        /// runs the original with its _CIatan2 import serviced by an FPATAN code cave.
        /// </summary>
        public void FaceTowards(uint obj, float x, float z)
        {
            float dx = x - M.F32(obj + 0x10);
            float dz = z - M.F32(obj + 0x18);
            if (dx == 0f && dz == 0f) return;
            float a = (float)Math.Atan2((double)dx, (double)(-dz));
            a = a / 3.141592f * 180.0f;
            M.F32(obj + 0x4c, a);
            if (a >= 360.0f)
            {
                a -= 360.0f;
                M.F32(obj + 0x4c, a);
            }
            if (0f > a) M.F32(obj + 0x4c, a + 360.0f);
        }

        /// <summary>
        /// FUN_00bd04b0 — screen-relative direction index of a world heading (camera longitude at
        /// view +0x30): 8 sectors of 45° (sector 0 centred on 0° when <paramref name="centered"/>). Port.
        /// </summary>
        public int DirectionIndex(float heading, int centered)
        {
            uint mode = Mode;
            if (mode == 0) return 0;
            uint view = M.U32(mode + 0xd0);
            float a = -M.F32(view + 0x30) - heading;
            if (0f > a)
                do a += 360.0f; while (0f > a);
            if (!(a < 360.0f) && !float.IsNaN(a))
                do a -= 360.0f; while (a >= 360.0f);
            if (centered != 0)
            {
                if (22.5f > a) return 0;
                if (a > 337.5f) return 0;
                return FxOps.Cvtt(((double)a + 22.5) / 45.0);
            }
            return FxOps.Cvtt(a / 45.0f);
        }

        // ---- std::vector<float> as inlined by the engine -----------------------------------

        /// <summary>
        /// std::vector&lt;float&gt;::push_back as inlined by the engine on the prim keyframe vectors
        /// ({begin, end, capacity} at <paramref name="vec"/>): stores in place while end != capacity, otherwise
        /// CEffectPrim::fn_b1e8f0 (0xb1e8f0, _Emplace_reallocate at end): capacity grows by half (at least to
        /// size + 1), the new buffer comes from FUN_006c8fa0 (operator new(n * 4); 32-byte aligned with the raw
        /// pointer at [-4] from 0x1000 bytes up), the old one is released by _Change_array (0x6c8c70 → operator
        /// delete → free) after the copy. Allocation order matches the client (new buffer, then free old).
        /// </summary>
        public void FloatVectorPushBack(uint vec, float value)
        {
            uint end = M.U32(vec + 4);
            if (end != M.U32(vec + 8))
            {
                M.F32(end, value);
                M.U32(vec + 4, end + 4);
                return;
            }
            uint begin = M.U32(vec);
            int size = (int)(end - begin) >> 2;
            if (size == 0x3fffffff) throw new InvalidOperationException("vector too long");
            uint newSize = (uint)size + 1;
            uint cap = (uint)((int)(M.U32(vec + 8) - begin) >> 2);
            uint newCap;
            if (0x3fffffffu - (cap >> 1) < cap) newCap = 0x3fffffff;
            else
            {
                newCap = (cap >> 1) + cap;
                if (newCap < newSize) newCap = newSize;
            }
            uint buf = VectorAllocate_6c8fa0(newCap);
            M.F32(buf + (uint)size * 4, value);
            if (size > 0) M.Copy(buf, begin, size * 4);
            if (begin != 0) VectorDeallocate_6c8c70(begin, cap);
            M.U32(vec, buf);
            M.U32(vec + 4, buf + newSize * 4);
            M.U32(vec + 8, buf + newCap * 4);
        }

        /// <summary>FUN_006c8fa0 — allocate n dwords (operator new; big blocks 32-byte aligned, raw pointer at [-4]).</summary>
        uint VectorAllocate_6c8fa0(uint count)
        {
            uint bytes = count << 2;
            if (bytes < 0x1000) return bytes == 0 ? 0 : M.Alloc(bytes);
            uint raw = M.Alloc(bytes + 0x23);
            uint p = (raw + 0x23) & 0xffffffe0u;
            M.U32(p - 4, raw);
            return p;
        }

        /// <summary>The deallocation half of _Change_array (0x6c8c70): operator delete of the (raw) block.</summary>
        void VectorDeallocate_6c8c70(uint begin, uint capacity)
        {
            uint bytes = (capacity * 4) & 0xfffffffcu;
            M.Free(bytes < 0x1000 ? begin : M.U32(begin - 4));
        }
    }

    public static partial class FxMath
    {
        /// <summary>
        /// CEffectPrim::FUN_00ad5710 (0xad5710) — direction of (dx, dz) in whole degrees: normalise with
        /// FUN_00534e70 (x87 fsqrt, stored as float), find the first a in 0..180 with dx/len ≥ cos-table[a]
        /// (COMISS; JAE: unordered does not match; 180 if none, also for NaN), mirrored to 360 - a when
        /// dz/len &lt; 0 or unordered (CMOVB: CF=1 when below or unordered).
        /// </summary>
        public static short DirectionToAngle(float dx, float dz)
        {
            float len = FxRuntime.SqrtF(dx * dx + dz * dz);
            float c = dz / len;
            float s = dx / len;
            for (short a = 0; a <= 0xb4; a++)
            {
                if (s >= CosTableAt(a))
                    return (short)(c < 0f || float.IsNaN(c) ? 0x168 - a : a);
            }
            return 0xb4;
        }
    }

    public static partial class FxOps
    {
        /// <summary>
        /// FUN_00d48d60 (__ftoui3, the non-AVX512 path taken when __isa_available &lt; 6): float → uint32.
        /// Truncates; values in [2^31, 2^32) convert exactly; x &lt;= -1, x &gt;= 2^32, ±inf and NaN give 0xffffffff.
        /// </summary>
        public static uint FloatToUInt_d48d60(float v)
        {
            uint bits = (uint)BitConverter.SingleToInt32Bits(v);
            uint shl = bits << 1;
            if ((bits & 0x80000000u) != 0)
                return shl < 0x7f000000u ? (uint)Cvtt(v) : 0xffffffffu;
            if (shl < 0x9e000000u) return (uint)Cvtt(v);
            if (shl < 0x9f000000u) return (bits << 8) | 0x80000000u;
            return 0xffffffffu;
        }
    }
}
