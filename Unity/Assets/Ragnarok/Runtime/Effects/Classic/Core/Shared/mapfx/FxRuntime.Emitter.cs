// Synced from reference/port/src/RoClassicFx/Shared/mapfx/FxRuntime.Emitter.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CEffectMgr's per-id effect vectors (+0x20: std::map&lt;int, std::vector&lt;CEffect*&gt;&gt;, node 0x20: key +0x10, vector
    /// +0x14..+0x1c) and the Lua C function <c>SetEmitterInfo</c> (FUN_00aa7640) that configures the map emitters (EF 974)
    /// through them. Package mapfx.
    /// </summary>
    public sealed partial class FxRuntime
    {
        /// <summary>EF 974 (CEmitterEffect), the id SetEmitterInfo looks up.</summary>
        public const int EmitterEffectId = 0x3ce;

        /// <summary>
        /// The per-id half of FUN_00a4c7d0 (after the owner list): ids 0x3ce, 0x401, 0x40f, 0x431, 0x584 and 0x927 are
        /// also appended to their vector in the map at +0x20 (operator[]: a node of 0x20 bytes with an empty vector is
        /// inserted for a new id, FUN_00a63c70; push_back with FUN_00a41d30 when full). The client never removes an
        /// effect from these vectors before the map change (FUN_00a4db00 erases the whole map).
        /// </summary>
        void PerIdVectorPushBack_a4c7d0(int id, uint effect)
        {
            uint tree = EffectMgr + 0x20;
            TreeFindLowerBound(tree, id, out uint parent, out bool left, out uint bound);
            uint node = bound;
            if (M.U8(bound + 0xd) != 0 || id < M.I32(bound + 0x10))
            {
                if (M.U32(tree + 4) == 0x7ffffff) throw new InvalidOperationException("map/set too long");
                uint head = M.U32(tree);
                node = M.Alloc(0x20);
                M.I32(node + 0x10, id);
                M.U32(node + 0x14, 0);
                M.U32(node + 0x18, 0);
                M.U32(node + 0x1c, 0);
                M.U32(node, head);
                M.U32(node + 4, head);
                M.U32(node + 8, head);
                M.U16(node + 0xc, 0);
                TreeInsertNode(tree, parent, left, node);
            }
            PtrVectorPushBack(node + 0x14, effect);
        }

        /// <summary>
        /// FUN_00a4de70 (thiscall on CEffectMgr, RET 8) — the <paramref name="index"/>-th effect (creation order) of id
        /// <paramref name="efId"/> in the per-id vectors, or 0 (unknown id, index ≥ count; the index is unsigned).
        /// </summary>
        public uint EffectByIdIndex_a4de70(int efId, uint index)
        {
            if (EffectMgr == 0) return 0;
            uint tree = EffectMgr + 0x20;
            TreeFindLowerBound(tree, efId, out _, out _, out uint bound);
            uint head = M.U32(tree);
            uint node = M.U8(bound + 0xd) == 0 && efId >= M.I32(bound + 0x10) ? bound : head;
            if (node == head) return 0;
            uint begin = M.U32(node + 0x14);
            uint count = (uint)((int)(M.U32(node + 0x18) - begin) >> 2);
            return index < count ? M.U32(begin + index * 4) : 0u;
        }

        /// <summary>Number of effects of <paramref name="efId"/> in the per-id vectors (map emitters: one per RSW 974 record).</summary>
        public int EffectCountById(int efId)
        {
            if (EffectMgr == 0) return 0;
            uint node = TreeFind(EffectMgr + 0x20, efId);
            return node == 0 ? 0 : (int)(M.U32(node + 0x18) - M.U32(node + 0x14)) >> 2;
        }

        /// <summary>
        /// FUN_00aa7640 — the Lua C function <c>SetEmitterInfo(index, MSG, values…)</c> (registered by FUN_00a28770; the
        /// EffectTool's <c>ReadEmitterInfoz</c> calls it). <paramref name="stack"/>[i] is what FUN_005023a0 (lua_tonumber)
        /// returns for Lua stack slot i + 1 (a missing slot or nil = 0); <paramref name="text"/> is FUN_00502340
        /// (lua_tostring) of slot 3, read only by MSG_SET_TEXTURE. The index and MSG are truncated (CVTTSD2SI), the effect
        /// is <c>FUN_00a4de70(974, index)</c>; nothing happens without one (returns false). Messages:
        /// 0 INIT → vf07; 1 SPAWNDIR1, 2 SPAWNDIR2, 3 GRAVITY, 4 POS (the effect's position +8), 5 EMITRADIUS → three
        /// floats (double → float, FSTP) at +0xcc / +0xd8 / +0xe4 / +8 / +0xf0; 6 COLOR → four ints (truncated, &gt; 255 →
        /// 255, negatives kept) r, g, b, a at +0x124..+0x130; 7 EMITRATE, 8 SIZE, 9 LIFETIME → two floats at +0x114 /
        /// +0x104 / +0x10c; 10 TEXTURE → +0x134; 11 SPEED → FUN_00a78870 (float); 12 SRCMODE, 13 DESTMODE → _ftol2
        /// (FUN_00d49720) at +0x14c / +0x150; 14 MAXPARTICLE → FUN_00a78840 (truncated int); 43 ZENABLE → byte +0x15c =
        /// (int)v != 0; 45 BILLBOARD_OFF → byte +0x15d = (int)v != 0; 46 ROTATE_ANGLE → three floats +0x160..+0x168;
        /// 51 ETERNITY → byte +0x16c = (int)v == 1; 52 SCALE → two floats +0x11c / +0x120. Every other MSG (15..42, 44,
        /// 47..50, above 52, negative) reads nothing more and does nothing. The Lua function returns 0 results either way.
        /// </summary>
        public bool SetEmitterInfo_aa7640(double[] stack, string text = null)
        {
            double Num(int slot) => stack != null && slot - 1 < stack.Length ? stack[slot - 1] : 0.0;
            uint e = EffectByIdIndex_a4de70(EmitterEffectId, (uint)FxOps.Cvtt(Num(1)));
            if (e == 0) return false;
            var fx = new CEffect(M, e);
            int msg = FxOps.Cvtt(Num(2));
            if ((uint)msg > 0x34) return true;
            switch (msg)
            {
                case 0:
                    fx.Vf07Reinit(this);
                    break;
                case 1: case 2: case 3: case 4: case 5:
                {
                    float a = (float)Num(3), b = (float)Num(4), c = (float)Num(5);
                    uint at = msg == 1 ? 0xccu : msg == 2 ? 0xd8u : msg == 3 ? 0xe4u : msg == 4 ? 8u : 0xf0u;
                    M.F32(e + at, a);
                    M.F32(e + at + 4, b);
                    M.F32(e + at + 8, c);
                    break;
                }
                case 6:
                {
                    int r = Math.Min(FxOps.Cvtt(Num(3)), 0xff);
                    int g = Math.Min(FxOps.Cvtt(Num(4)), 0xff);
                    int b = Math.Min(FxOps.Cvtt(Num(5)), 0xff);
                    int a = Math.Min(FxOps.Cvtt(Num(6)), 0xff);
                    M.I32(e + 0x130, a);
                    M.I32(e + 0x124, r);
                    M.I32(e + 0x128, g);
                    M.I32(e + 0x12c, b);
                    break;
                }
                case 7: case 8: case 9:
                {
                    float a = (float)Num(3), b = (float)Num(4);
                    uint at = msg == 7 ? 0x114u : msg == 8 ? 0x104u : 0x10cu;
                    M.F32(e + at, a);
                    M.F32(e + at + 4, b);
                    break;
                }
                case 10:
                    if (text == null) throw new ArgumentNullException(nameof(text), "MSG_SET_TEXTURE without a string: the client builds a std::string from a null pointer");
                    fx.EmitterSetTextureMsg(this, text);
                    break;
                case 11:
                    fx.EmitterSetSpeedMsg((float)Num(3));
                    break;
                case 12:
                    M.I32(e + 0x14c, Ftol_d49720(Num(3)));
                    break;
                case 13:
                    M.I32(e + 0x150, Ftol_d49720(Num(3)));
                    break;
                case 14:
                    fx.EmitterSetMaxCountMsg(this, FxOps.Cvtt(Num(3)));
                    break;
                case 0x2b:
                    M.U8(e + 0x15c, FxOps.Cvtt(Num(3)) != 0 ? (byte)1 : (byte)0);
                    break;
                case 0x2d:
                    M.U8(e + 0x15d, FxOps.Cvtt(Num(3)) != 0 ? (byte)1 : (byte)0);
                    break;
                case 0x2e:
                {
                    float a = (float)Num(3), b = (float)Num(4), c = (float)Num(5);
                    M.F32(e + 0x160, a);
                    M.F32(e + 0x164, b);
                    M.F32(e + 0x168, c);
                    break;
                }
                case 0x33:
                    M.U8(e + 0x16c, FxOps.Cvtt(Num(3)) == 1 ? (byte)1 : (byte)0);
                    break;
                case 0x34:
                {
                    float a = (float)Num(3), b = (float)Num(4);
                    M.F32(e + 0x11c, a);
                    M.F32(e + 0x120, b);
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// FUN_00d49720 (_ftol2 on ST0): truncation. The client's SSE3 path (FISTTP dword) gives 0x80000000 out of range,
        /// as here; its legacy path (taken when the CPU level at 0x15562a4 is below 2) would give the low dword of the
        /// 64-bit result instead — the two differ only for |v| ≥ 2^31.
        /// </summary>
        static int Ftol_d49720(double v) => FxOps.Cvtt(v);
    }

    public readonly partial struct CEffect
    {
        /// <summary>vf07 (slot +0x1c): the re-init SetEmitterInfo's MSG_SET_INIT calls.</summary>
        public void Vf07Reinit(FxRuntime rt)
        {
            if (WindFloorReinit(rt)) return;                                // CWindEffect / CMagicFloorEffect (package mapfx-wind)
            if (Vtable == VtCEvilsPawEffect) { EvilsPawReinitMsg(); return; }   // package mapfx-emitters (1073 shares CEmitterEffect's)
            if (Vtable != VtCEmitterEffect && Vtable != VtCAnimatedEmitterEffect) throw Unported(7);
            EmitterReinit_a77f00(rt);
        }

        internal void EmitterSetTextureMsg(FxRuntime rt, string name) => EmitterSetTexture_aa6fd0(rt, name);
        internal void EmitterSetSpeedMsg(float speed) => EmitterSetSpeed_a78870(speed);
        internal void EmitterSetMaxCountMsg(FxRuntime rt, int count) => EmitterSetMaxCount_a78840(rt, count);
    }
}
