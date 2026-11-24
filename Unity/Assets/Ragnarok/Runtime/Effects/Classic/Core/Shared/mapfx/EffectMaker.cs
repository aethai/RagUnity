// Synced from reference/port/src/RoClassicFx/Shared/mapfx/EffectMaker.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CEffectMaker (vtable 0x0100a7a0, 0x178 bytes; CAbleToMakeEffect &gt; CRenderObject &gt; CGameObject) — the
    /// emitter the world builder FUN_009f9770 creates for every RSW effect record (object type 4) whose id is not
    /// in the new-system registry. It never moves, draws nothing (vf03 0xd3d250 is <c>ret</c>), never dies by itself
    /// (vf01 always returns 1) and launches its CRagEffect every <see cref="Interval"/> world passes while the local
    /// player is within 130 units (horizontal) of it. See packages/mapfx/NOTES.md.
    ///
    /// Fields: +0x10/+0x14/+0x18 position (the record's, raw), +0x158 pass counter, +0x15c interval (passes),
    /// +0x160 effect id, +0x164..+0x170 the record's four params (raw dwords, handed to every launched effect).
    /// </summary>
    public readonly partial struct EffectMaker
    {
        public const uint SizeOf = 0x178;
        public const uint VtCEffectMaker = 0x0100a7a0;
        public const uint VtCAbleToMakeEffect = 0x00ffdf40;

        /// <summary>RSW effect record size (C3dWorldRes::Load 0x6db4d0 reads object type 4 as 0x74 raw bytes).</summary>
        public const uint RecordSize = 0x74;

        /// <summary>The id with its own timing rule in SendMsg 0xe (0x2d = 45, EF_FIREFLY).</summary>
        public const int FireflyId = 0x2d;

        /// <summary>vf01's range: launches only while the player's horizontal distance is below this.</summary>
        public const float LaunchRange = 130f;

        public readonly FxMemory M;
        public readonly uint A;

        public EffectMaker(FxMemory m, uint address) { M = m; A = address; }

        public float X => M.F32(A + 0x10);
        public float Y => M.F32(A + 0x14);
        public float Z => M.F32(A + 0x18);
        /// <summary>+0x158: world passes since the last launch (unsigned; starts at a random value).</summary>
        public uint Counter { get => M.U32(A + 0x158); set => M.U32(A + 0x158, value); }
        /// <summary>+0x15c: launch when <see cref="Counter"/> &gt;= this (unsigned compare).</summary>
        public uint Interval { get => M.U32(A + 0x15c); set => M.U32(A + 0x15c, value); }
        public int EffectId => M.I32(A + 0x160);
        public uint Param(int i) => M.U32(A + 0x164 + (uint)i * 4);

        /// <summary>
        /// CEffectMaker::CEffectMaker (FUN_00d378a0): CAbleToMakeEffect ctor (FUN_00bcd310: CRenderObject ctor shim,
        /// empty effect std::list at +0x144 with a 0xc sentinel, +0x138 = −1, +0x13c/+0x140/+0x14c/+0x150 = 0,
        /// byte +0x130 = 0), then vtable 0x0100a7a0, +0x160 = −1, +0x15c = +0x158 = 360, +0x164..+0x170 = 0.
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
            // CEffectMaker part
            M.U32(A, VtCEffectMaker);
            M.U32(A + 0x160, 0xffffffff);
            M.U32(A + 0x15c, 0x168);
            M.U32(A + 0x158, 0x168);
            M.U32(A + 0x164, 0);
            M.U32(A + 0x168, 0);
            M.U32(A + 0x16c, 0);
            M.U32(A + 0x170, 0);
        }

        /// <summary>
        /// CEffectMaker::SendMsg (vf02, 0x00d3e080) message 0xe (high dword 0), p1 = the RSW effect record
        /// <paramref name="record"/> (0x74 bytes in memory: +0x50 position, +0x5c id, +0x60 emit speed, +0x64 params):
        /// <code>
        /// id = rec.id
        /// id != 45: interval = ftoui(emit);                       counter = ftoui(emit − rand() % 24)
        /// id == 45: n = (int)(emit / 10); r = n ? max(rand() % n, 0) : 0
        ///           interval = ftoui((r + emit) − emit / 20);     m = (int)(emit + 1); counter = m ? max(rand() % m, 0) : 0
        /// position = rec.pos; params = rec.params (raw)
        /// </code>
        /// ftoui = FUN_00d48d60 (negative values ≤ −1 give 0xffffffff, i.e. a launch on the first pass in range).
        /// One rand() for an ordinary id, up to two for 45 (none when the truncated value is 0).
        /// Every other message goes to CAbleToMakeEffect::SendMsg (0x00bd7fc0) in the client — not ported here,
        /// returns false and changes nothing.
        /// </summary>
        public bool SendMsg(FxRuntime rt, int msg, uint record)
        {
            if (msg != 0xe) return false;
            int id = M.I32(record + 0x5c);
            M.I32(A + 0x160, id);
            float emit = M.F32(record + 0x60);
            uint counter;
            if (id != FireflyId)
            {
                M.U32(A + 0x15c, FxOps.FloatToUInt_d48d60(emit));
                int r = rt.Rand.Next() % 24;             // CDQ; IDIV 0x18; CMOVNS
                if (r < 0) r = 0;
                counter = FxOps.FloatToUInt_d48d60(M.F32(record + 0x60) - (float)r);
            }
            else
            {
                int n = FxOps.Cvtt(emit / 10f);          // DIVSS [0xf4b624] = 10.0; CVTTSS2SI
                int r = 0;
                float e = emit;
                if (n != 0)
                {
                    r = rt.Rand.Next() % n;
                    if (r < 0) r = 0;
                    e = M.F32(record + 0x60);            // reloaded after the call
                }
                M.U32(A + 0x15c, FxOps.FloatToUInt_d48d60(((float)r + e) - e / 20f));   // [0xf7d134] = 20.0
                int m = FxOps.Cvtt(M.F32(record + 0x60) + 1f);                         // [0xf4a468] = 1.0
                if (m == 0) counter = 0;
                else
                {
                    int q = rt.Rand.Next() % m;
                    counter = q < 0 ? 0u : (uint)q;
                }
            }
            M.U32(A + 0x158, counter);
            M.U32(A + 0x10, M.U32(record + 0x50));
            M.U32(A + 0x14, M.U32(record + 0x54));
            M.U32(A + 0x18, M.U32(record + 0x58));
            M.U32(A + 0x164, M.U32(record + 0x64));
            M.U32(A + 0x168, M.U32(record + 0x68));
            M.U32(A + 0x16c, M.U32(record + 0x6c));
            M.U32(A + 0x170, M.U32(record + 0x70));
            return true;
        }

        /// <summary>
        /// CEffectMaker::OnProcess (vf01, 0x00d397a0), once per world pass (every logic tick and once on a 0-tick
        /// frame): d = FUN_00bce640(player.x, player.z) (horizontal distance, x87 fsqrt stored as float); if
        /// <c>130 &gt; d</c> (COMISS/JBE: NaN never launches) and <c>counter &gt;= interval</c> (unsigned): counter = 0,
        /// <c>effect = LaunchEffect(id, (0, 0, 0), 0)</c> on this maker and, when one was made, effect message 0x2e
        /// with a pointer to +0x164 — CRagEffect::SendMsg (vf16 0xb5e650) byte table 0xb5ea2e → case 4: the effect's
        /// +0x13c/+0x140 (floats) and +0x144/+0x148 (ints, the raw float bits) = the four params. Then always
        /// counter + 1; returns 1. <paramref name="launch"/> stands for CAbleToMakeEffect::LaunchEffect (0xbd1f40):
        /// (maker, id) → the new CRagEffect's address or 0.
        /// </summary>
        public bool OnProcess(FxRuntime rt, Func<uint, int, uint> launch)
        {
            uint player = M.U32(M.U32(rt.Mode + 0xcc) + 0x2c);   // FUN_00a03ba0()->+0xcc->+0x2c
            float d = rt.DistanceXZ(A, M.F32(player + 0x10), M.F32(player + 0x18));
            if (LaunchRange > d && M.U32(A + 0x158) >= M.U32(A + 0x15c))
            {
                M.U32(A + 0x158, 0);
                uint effect = launch(A, M.I32(A + 0x160));
                if (effect != 0)
                {
                    // vf16(0, 0x2e, 0, this + 0x164, ...): case 4
                    M.U32(effect + 0x13c, M.U32(A + 0x164));
                    M.U32(effect + 0x140, M.U32(A + 0x168));
                    M.U32(effect + 0x144, M.U32(A + 0x16c));
                    M.U32(effect + 0x148, M.U32(A + 0x170));
                }
            }
            M.U32(A + 0x158, M.U32(A + 0x158) + 1);
            return true;
        }
    }
}
