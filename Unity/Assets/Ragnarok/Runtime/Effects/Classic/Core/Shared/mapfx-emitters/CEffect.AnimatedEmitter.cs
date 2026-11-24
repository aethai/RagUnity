// Synced from reference/port/src/RoClassicFx/Shared/mapfx-emitters/CEffect.AnimatedEmitter.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CAnimatedEmitterEffect (EF 1073, vtable 0xf7d5f4, 0x1a4 bytes; allocator VCAnimatedEmitterEffect 0xfe1828 → ctor
    /// FUN_00676eb0) — the map emitter of EF 974 (<see cref="VtCEmitterEffect"/>, every field and virtual method of it but
    /// vf01 / vf25 / vf29 / vf30) whose particles play a texture animation: two std::vector&lt;std::string&gt; frame sets
    /// at +0x18c (EFFECT\shockwave_a..e.bmp) and +0x198 (EFFECT\plazma_a, b, c, a, b.bmp), filled by vf01; every
    /// particle picks a set when it starts (vf29: +0x54 = rand() % 2) and shows frame (age / 8) % 5 of it (vf30).
    /// Package mapfx-emitters; configured by the map's EffectTool (<c>SetAnimatedEmitterInfo</c>, FUN_00aa7040,
    /// <see cref="FxRuntime.SetAnimatedEmitterInfo_aa7040"/>). 162 RSW records on 16 maps.
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCAnimatedEmitterEffect = 0x00f7d5f4;
        public const uint VtAllocAnimatedEmitter = 0x00fe1828;
        public const uint AnimatedEmitterSize = 0x1a4;
        static readonly int[] AnimatedEmitterIds = { 1073 };

        static readonly string[] AnimShockwave =
        {
            "EFFECT\\shockwave_a.bmp", "EFFECT\\shockwave_b.bmp", "EFFECT\\shockwave_c.bmp", "EFFECT\\shockwave_d.bmp",
            "EFFECT\\shockwave_e.bmp",
        };

        static readonly string[] AnimPlazma =
        {
            "EFFECT\\plazma_a.bmp", "EFFECT\\plazma_b.bmp", "EFFECT\\plazma_c.bmp", "EFFECT\\plazma_a.bmp", "EFFECT\\plazma_b.bmp",
        };

        /// <summary>
        /// FUN_00676eb0: CEmitterEffect ctor (FUN_00a77740), vtable, the two frame-set vectors empty (LAB_0058b300 × 2),
        /// then other defaults: gravity 0, spawn directions (−3, −1, −3) .. (3, 1, 3), emit radius 0, size 10..20, life
        /// 3..4 s, rate 1..3 / s, 4 particles.
        /// </summary>
        static uint ConstructAnimatedEmitter_676eb0(FxRuntime rt, uint a)
        {
            ConstructEmitter_a77740(rt, a);
            var M = rt.M;
            M.U32(a, VtCAnimatedEmitterEffect);
            for (uint o = 0x18c; o < 0x1a4; o += 4) M.U32(a + o, 0);
            M.U32(a + 0xe4, 0); M.U32(a + 0xe8, 0); M.U32(a + 0xec, 0);
            M.U32(a + 0xcc, 0xc0400000); M.U32(a + 0xd0, 0xbf800000); M.U32(a + 0xd4, 0xc0400000);
            M.U32(a + 0xd8, 0x40400000); M.U32(a + 0xdc, 0x3f800000); M.U32(a + 0xe0, 0x40400000);
            M.U32(a + 0xf0, 0); M.U32(a + 0xf4, 0); M.U32(a + 0xf8, 0);
            M.U32(a + 0x104, 0x41200000); M.U32(a + 0x108, 0x41a00000);
            M.U32(a + 0x10c, 0x40400000); M.U32(a + 0x110, 0x40800000);
            M.U32(a + 0x114, 0x3f800000); M.U32(a + 0x118, 0x40400000);
            M.I32(a + 0x154, 4);
            return a;
        }

        /// <summary>
        /// CAnimatedEmitterEffect::vf25_677050 body: vtable, the frame sets destroyed in reverse order (`eh vector destructor
        /// iterator' with FUN_00568f30), then CEmitterEffect's destructor body FUN_00a779e0.
        /// </summary>
        void AnimatedEmitterDestruct_677050(FxRuntime rt)
        {
            M.U32(A, VtCAnimatedEmitterEffect);
            rt.StrVectorDestroy_568f30(A + 0x198);
            rt.StrVectorDestroy_568f30(A + 0x18c);
            EmitterDestruct_a77a60(rt);
        }

        /// <summary>
        /// CAnimatedEmitterEffect::vf01_6770e0 — appends the ten frame names (each a local std::string moved into the vector:
        /// the first push inlined, the others FUN_00677890; FUN_0058b0c0 when full) — on every call, so a MAXPARTICLE
        /// change (vf05 + vf01) grows the sets by five names each (only the first five are ever shown) — then
        /// CEmitterEffect::vf01 (the particles; vf29 is this class's).
        /// </summary>
        void AnimatedEmitterInit_6770e0(FxRuntime rt)
        {
            foreach (string s in AnimShockwave) rt.StrVectorPushBackLocal(A + 0x18c, s);
            foreach (string s in AnimPlazma) rt.StrVectorPushBackLocal(A + 0x198, s);
            EmitterInit_a77ca0(rt);
        }

        /// <summary>CAnimatedEmitterEffect::vf29_6777b0 — frame set +0x54 = (float)(rand() % 2), then CEmitterEffect::vf29_a77d30.</summary>
        void AnimatedEmitterInitParticle_6777b0(FxRuntime rt, uint p)
        {
            int r = rt.Rand.Next() % 2;                                     // AND 0x80000001 with the sign fix-up = C's %
            M.F32(p + 0x54, (float)r);                                      // CVTDQ2PS
            EmitterInitParticle_a77d30(rt, p);
        }

        /// <summary>
        /// CAnimatedEmitterEffect::vf30_6777f0 — texture = frame (age / 8) % 5 of set (int)+0x54: the name is copied into a
        /// by-value std::string (FUN_004ccbd0: a heap buffer for names of 16+ bytes) that FUN_00a67fe0 looks up
        /// (GetTexture → +0x18) and destroys; then CEmitterEffect::vf30_a78490.
        /// </summary>
        void AnimatedEmitterUpdateParticle_6777f0(FxRuntime rt, uint p)
        {
            int set = FxOps.Cvtt(M.F32(p + 0x54));
            int frame = M.I32(p + 0xe8) / 8 % 5;
            uint s = unchecked(M.U32(A + 0x18c + (uint)(set * 0xc)) + (uint)(frame * 0x18));
            new CParticle(M, p).SetTextureByValue_a67fe0(rt, s);
            EmitterUpdateParticle_a78490(rt, p);
        }
    }

    public readonly partial struct CParticle
    {
        /// <summary>
        /// FUN_00a67fe0 with its by-value std::string argument copied from the string at <paramref name="src"/>
        /// (FUN_004ccbd0 in the caller): texture +0x18 = GetTexture(name), then the copy is destroyed (FUN_004d8220).
        /// </summary>
        public void SetTextureByValue_a67fe0(FxRuntime rt, uint src)
        {
            uint size = M.U32(src + 0x10);
            uint data = rt.StrData(src);
            uint tmp = rt.StrLocalAssign(data, size);
            M.U32(A + 0x18, rt.GetTexture(rt.ReadCString(tmp != 0 ? tmp : data)));
            rt.StrLocalDestroy(size, tmp);
        }
    }

    public sealed partial class FxRuntime
    {
        /// <summary>
        /// push_back of a local std::string holding <paramref name="bytes"/> (built with FUN_004d9270 / FUN_004d89d0: a heap
        /// buffer of (n | 15) + 1 for 16+ bytes) moved into the std::vector&lt;std::string&gt; at <paramref name="vec"/>:
        /// in place while end != capacity (FUN_00677890 / inlined), otherwise _Emplace_reallocate (FUN_0058b0c0). The moved-from
        /// local is left empty (capacity 15): nothing is freed. The local's 16-byte buffer is copied whole; its bytes past the
        /// terminator (or past the pointer) are uninitialised stack in the client — zeros here (see the package NOTES).
        /// </summary>
        public void StrVectorPushBackLocal(uint vec, string bytes)
        {
            uint buf = StrLocalAssign(bytes);
            uint end = M.U32(vec + 4);
            if (end != M.U32(vec + 8))
            {
                WriteMovedLocal(end, bytes, buf);
                M.U32(vec + 4, end + 0x18);
                return;
            }
            StrVectorEmplaceReallocate_58b0c0(vec, end, bytes, buf);
        }

        /// <summary>A local std::string (<paramref name="bytes"/>, heap buffer <paramref name="buf"/> or 0) move-constructed at <paramref name="dst"/>.</summary>
        void WriteMovedLocal(uint dst, string bytes, uint buf)
        {
            uint n = (uint)bytes.Length;
            if (buf != 0)
            {
                M.U32(dst, buf);
                for (uint i = 4; i < 0x10; i++) M.U8(dst + i, 0);
                M.U32(dst + 0x14, n | 0xf);
            }
            else
            {
                for (uint i = 0; i < 0x10; i++) M.U8(dst + i, i < n ? (byte)bytes[(int)i] : (byte)0);
                M.U32(dst + 0x14, 0xf);
            }
            M.U32(dst + 0x10, n);
        }

        /// <summary>
        /// FUN_0058b0c0 — std::vector&lt;std::string&gt;::_Emplace_reallocate (element 0x18 bytes): capacity grows by half (at
        /// least size + 1; clamped to 0xaaaaaaa), new buffer (FUN_00586820 = std::_Allocate), the new element constructed at
        /// <paramref name="where"/> (FUN_0058b2c0), the old elements moved around it (FUN_0055b760: 16 bytes + size / capacity
        /// taken, the source left empty), then FUN_00586700: the old elements destroyed and the old buffer released.
        /// </summary>
        void StrVectorEmplaceReallocate_58b0c0(uint vec, uint where, string bytes, uint buf)
        {
            uint begin = M.U32(vec);
            int size = (int)(M.U32(vec + 4) - begin) / 0x18;
            if (size == 0xaaaaaaa) throw new InvalidOperationException("vector too long");
            uint cap = (uint)((int)(M.U32(vec + 8) - begin) / 0x18);
            uint newCap;
            if (0xaaaaaaau - (cap >> 1) < cap) newCap = 0xaaaaaaa;
            else
            {
                newCap = (cap >> 1) + cap;
                if (newCap < (uint)size + 1) newCap = (uint)size + 1;
            }
            if (newCap > 0xaaaaaaa) throw new InvalidOperationException("vector too long");
            uint nb = StdAllocate(newCap * 0x18);
            uint at = nb + (uint)((int)(where - begin) / 0x18) * 0x18;
            M.U32(at, 0);
            M.U32(at + 0x10, 0);
            M.U32(at + 0x14, 0);
            WriteMovedLocal(at, bytes, buf);
            uint oldEnd = M.U32(vec + 4);
            uint first = M.U32(vec);
            if (where != oldEnd)
            {
                StrMoveRange_55b760(first, where, nb);
                StrMoveRange_55b760(where, oldEnd, at + 0x18);
            }
            else StrMoveRange_55b760(first, oldEnd, nb);
            StrVectorChangeArray_586700(vec, nb, (uint)size + 1, newCap);
        }

        /// <summary>FUN_0055b760 — uninitialised move of the strings [first, last) to <paramref name="dst"/>.</summary>
        void StrMoveRange_55b760(uint first, uint last, uint dst)
        {
            for (uint s = first; s != last; s += 0x18, dst += 0x18) StrMoveConstruct(dst, s);
        }

        /// <summary>FUN_00586700 — destroys the old elements, releases the old buffer, installs the new one.</summary>
        void StrVectorChangeArray_586700(uint vec, uint nb, uint count, uint cap)
        {
            StrVectorTidy(vec);
            M.U32(vec, nb);
            M.U32(vec + 4, nb + count * 0x18);
            M.U32(vec + 8, nb + cap * 0x18);
        }

        /// <summary>Every element destroyed (FUN_004d8220), the buffer released ((cap − begin) / 0x18 · 0x18 bytes).</summary>
        void StrVectorTidy(uint vec)
        {
            uint begin = M.U32(vec);
            if (begin == 0) return;
            uint end = M.U32(vec + 4);
            for (uint s = begin; s != end; s += 0x18) StrDestroy(s);
            begin = M.U32(vec);
            StdDeallocate(begin, (uint)((int)(M.U32(vec + 8) - begin) / 0x18) * 0x18);
        }

        /// <summary>FUN_00568f30 — std::vector&lt;std::string&gt; destructor: elements, buffer, then the three pointers 0.</summary>
        public void StrVectorDestroy_568f30(uint vec)
        {
            if (M.U32(vec) == 0) return;
            StrVectorTidy(vec);
            M.U32(vec, 0);
            M.U32(vec + 4, 0);
            M.U32(vec + 8, 0);
        }
    }
}
