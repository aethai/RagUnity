// Synced from reference/port/src/RoClassicFx/Shared/msgeffect/FxRuntime.MsgEffect.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Package msgeffect: damage numbers and combat messages of the original client (CMsgEffect objects, see
    /// <see cref="MsgEffect"/>): the actor messages that create them (CGameActor::SendMsg 0xbd86c0: 0x30, 0x58, 0x59,
    /// 0x5c, 0x5f, 0x7e, 0x83), the digit splitter FUN_00beda30, the MISS / burst factories FUN_00bfd3b0 / FUN_00bfd630
    /// and the sprite path they are drawn with (CRenderObject::Render 0xbd6040 → RenderBody 0xbd6080, on top of the
    /// actorfx renderer helpers). Oracle-verified in tests/Packages/msgeffect. See packages/msgeffect/NOTES.md.
    /// </summary>
    public sealed partial class FxRuntime
    {
        const string EffectDir = "ÀÌÆÑÆ®\\";   // 이팩트\ (CP949 bytes)

        // ---- damage skin and options (CSession 0x154dd00 fields, client globals) ---------------------------

        /// <summary>Session +0x7b0c (std::string at 0x155580c) — base name of the digit sprite (Lua damage skin): 이팩트\숫자.</summary>
        public string DamageNumberSprite = EffectDir + "¼ýÀÚ";

        string damageNumberNamesFor, damageNumberSprName, damageNumberActName;

        /// <summary><see cref="DamageNumberSprite"/> + ".spr" / ".act", built once per sprite (every digit concatenated them).</summary>
        public string DamageNumberSprName { get { UpdateDamageNumberNames(); return damageNumberSprName; } }
        public string DamageNumberActName { get { UpdateDamageNumberNames(); return damageNumberActName; } }

        void UpdateDamageNumberNames()
        {
            if (ReferenceEquals(damageNumberNamesFor, DamageNumberSprite)) return;
            damageNumberNamesFor = DamageNumberSprite;
            damageNumberSprName = DamageNumberSprite + ".spr";
            damageNumberActName = DamageNumberSprite + ".act";
        }

        /// <summary>Session +0x7b24 (0x1555824) — MISS / critical / LUCKY sprite and action base name: 이팩트\msg.</summary>
        public string DamageMessageSprite = EffectDir + "msg";

        /// <summary>Session +0x7b3c (0x155583c) — the Taekwon kick burst sprite base name: 이팩트\bluemsg.</summary>
        public string DamageBlueMessageSprite = EffectDir + "bluemsg";

        /// <summary>[0x15557fc] (session +0x7afc) — the blue burst uses its own .act; off: the literal 이팩트\msg.act.</summary>
        public bool DamageBlueMessageOwnAct;

        /// <summary>[0x1555804] (session +0x7b04) — number drift mode: 0 behind the actor's heading, 1..3 fixed vectors turned by the camera.</summary>
        public int DamageDriftMode;

        /// <summary>[0x1555808] (session +0x7b08) — 2 lays values ≥ 100 000 000 out with 3·(n−1) − 8i px offsets.</summary>
        public int DamageNumberMode;

        /// <summary>[0x15556c0] (session +0x79c0) — values ≥ 1e6 / 1e8 shown as thousands 'K' / millions 'M'; off: clamped to 999 999.</summary>
        public bool DamageNumberAbbreviate;

        /// <summary>[0x118232c] — number glyphs (types 0xd..0x10, 0x15, 0x16, 0x72) are drawn (static 1).</summary>
        public bool ShowDamageNumbers = true;

        /// <summary>[0x1552d9c] — MISS is shown (actor msg 0x59; a user toggle).</summary>
        public bool ShowMissText = true;

        /// <summary>FUN_00d015a0(7) (thiscall on CSession: an option word test) — numbers on maps flagged at world +0x4c / +0x50. Default off.</summary>
        public Func<int, bool> SessionOptionImpl;

        /// <summary>FUN_00d1e0c0(efst) (the local player's status list DAT_012c1fb8, 0x14-byte entries) — Illusion 0x22 / Pyrexia 0x15b. Default none.</summary>
        public Func<int, bool> LocalPlayerHasStatusImpl;

        bool SessionOption(int id) => SessionOptionImpl != null && SessionOptionImpl(id);

        bool LocalPlayerHasStatus(int efst) => LocalPlayerHasStatusImpl != null && LocalPlayerHasStatusImpl(efst);

        /// <summary>CMsgEffect objects in the world list (world +0x8), in creation order — processed / drawn by <see cref="FxWorld"/>.</summary>
        public readonly List<uint> MsgEffectObjects = new List<uint>();

        /// <summary>
        /// Glyphs that have had an OnProcess pass with a tick (host side, not in the client). The client creates glyphs
        /// inside a ticked world pass and processes them in the same pass, so their constructor state (MISS at zoom 5)
        /// is never drawn; the host creates them between frames, so <see cref="FxWorld"/> draws only these.
        /// </summary>
        public readonly HashSet<uint> MsgEffectsProcessed = new HashSet<uint>();

        /// <summary>
        /// FUN_00c0bb30 (thiscall on the mode, RET 4) / the std::list push_back FUN_00783f90 on mode +0xcc → world +0x8:
        /// registers a CMsgEffect as a world object.
        /// </summary>
        void AddWorldObject_c0bb30(uint obj)
        {
            uint world = M.U32(Mode + 0xcc);
            if (world == 0 || obj == 0) return;
            ListPushBack(world + 0x8, obj);
            MsgEffectObjects.Add(obj);
        }

        /// <summary>
        /// FUN_009fac00 (thiscall on the world, RET 4) — CWorld::RemoveObject for a CMsgEffect: finds <paramref name="obj"/>
        /// in the world list, deletes it (vf00(1): <see cref="MsgEffect.Destroy"/>), then unlinks and frees the node.
        /// </summary>
        public void RemoveWorldObject_9fac00(uint obj)
        {
            uint list = M.U32(Mode + 0xcc) + 0x8;
            uint head = M.U32(list);
            for (uint node = M.U32(head); node != head; node = M.U32(node))
            {
                if (M.U32(node + 8) != obj) continue;
                if (obj != 0) new MsgEffect(M, obj).Destroy(this);
                uint next = M.U32(node), prev = M.U32(node + 4);
                M.U32(prev, next);
                M.U32(next + 4, prev);
                M.U32(list + 4, M.U32(list + 4) - 1);
                M.Free(node);
                MsgEffectObjects.Remove(obj);
                MsgEffectsProcessed.Remove(obj);
                return;
            }
        }

        // ---- the actor side (CGameActor fields +0x37c..+0x3b4) ------------------------------------------------

        /// <summary>Gives an actor shim its CMsgEffect std::list (+0x3b0 head, +0x3b4 size), as the CGameActor ctor does.</summary>
        public void EnsureActorMsgEffectList(uint actor)
        {
            if (M.U32(actor + 0x3b0) == 0) InitList(actor + 0x3b0);
        }

        /// <summary>FUN_00be8650 (thiscall on actor +0x3b0, RET 4) — std::list push_back of a CMsgEffect.</summary>
        void ActorListPush_be8650(uint actor, uint obj) => ListPushBack(actor + 0x3b0, obj);

        static readonly uint[] ActorMsgSlots = { 0x380, 0x37c, 0x384, 0x388, 0x38c, 0x390, 0x394, 0x39c, 0x3a0, 0x398, 0x3a4, 0x3a8, 0x3ac };

        /// <summary>FUN_00bcfdc0 (thiscall on the actor, RET 4) — clears the first of the actor's message slots (+0x37c..+0x3ac) holding <paramref name="obj"/>.</summary>
        void ActorClearMsgSlot_bcfdc0(uint actor, uint obj)
        {
            foreach (uint off in ActorMsgSlots)
            {
                if (M.U32(actor + off) != obj) continue;
                M.U32(actor + off, 0);
                return;
            }
        }

        /// <summary>
        /// CGameActor::SendMsg msg 0x30 (0x00bd884f; sent by a dying CMsgEffect): the glyph's node in +0x3b0 is found,
        /// the slot holding it is cleared (as FUN_00bcfdc0), the node unlinked and freed. Not found: nothing.
        /// </summary>
        public void ActorMsgEffectGone_bd884f(uint actor, uint obj)
        {
            uint head = M.U32(actor + 0x3b0);
            uint node = M.U32(head);
            if (node == head) return;
            while (M.U32(node + 8) != obj)
            {
                node = M.U32(node);
                if (node == head) return;
            }
            ActorClearMsgSlot_bcfdc0(actor, M.U32(node + 8));
            uint prev = M.U32(node + 4), next = M.U32(node);
            M.U32(prev, next);
            M.U32(next + 4, prev);
            M.U32(actor + 0x3b4, M.U32(actor + 0x3b4) - 1);
            M.Free(node);
        }

        /// <summary>
        /// The CMsgEffect part of the actor's teardown (FUN_00bd3250 @0x00bd3374, run by ~CGameActor): every glyph in
        /// +0x3b0 gets msg 0x31 (detach: it stays where it is; heal numbers end), then the nodes are unlinked and freed.
        /// <paramref name="freeList"/> also frees the sentinel (the rest of ~CGameActor, 0x00bcdde8).
        /// </summary>
        public void ActorDetachMsgEffects_bd3374(uint actor, bool freeList)
        {
            uint head = M.U32(actor + 0x3b0);
            if (head == 0) return;
            for (uint n = M.U32(head); n != M.U32(actor + 0x3b0); n = M.U32(n))
                new MsgEffect(M, M.U32(n + 8)).SendMsg(this, 0, MsgEffect.MsgDetach);
            head = M.U32(actor + 0x3b0);
            uint first = M.U32(head);
            if (first != head)
            {
                uint prev = M.U32(first + 4);
                M.U32(prev, head);
                M.U32(head + 4, prev);
                uint count = 0;
                for (uint n = first; n != head;)
                {
                    uint next = M.U32(n);
                    M.Free(n);
                    count++;
                    n = next;
                }
                M.U32(actor + 0x3b4, M.U32(actor + 0x3b4) - count);
            }
            if (!freeList) return;
            M.U32(M.U32(head + 4), 0);                   // ~list: last->next = 0, walk, free the sentinel
            M.Free(head);
            M.U32(actor + 0x3b0, 0);
            M.U32(actor + 0x3b4, 0);
        }

        // ---- CGameActor::SendMsg (vf02 0x00bd86c0): the message cases of the hit path ----------------------

        /// <summary>
        /// CGameActor::SendMsg (0x00bd86c0) for the cases that create or drop CMsgEffects. <paramref name="p1"/> is the
        /// 64-bit first parameter (the value of msg 0x58), <paramref name="p2"/> / <paramref name="p3"/> the low dwords
        /// of the next two:
        /// <list type="bullet">
        /// <item>0x30 — a glyph of the actor is being deleted (<see cref="ActorMsgEffectGone_bd884f"/>);</item>
        /// <item>0x58 (value, ARGB, type) — a damage number (<see cref="ActorShowNumber_bd89d3"/>);</item>
        /// <item>0x59 (ARGB) — "Miss" on this actor (the attacker), when <see cref="ShowMissText"/>; counts +0x2f0;</item>
        /// <item>0x5c — the critical burst (type 0xf); 0x5f — "LUCKY!!" (type 0x11); 0x83 — the Taekwon kick burst (type 0x19);</item>
        /// <item>0x7e — EF 336 (EF_GUARD) on the actor (CAbleToMakeEffect::LaunchEffect, <see cref="LaunchEffectOn"/>).</item>
        /// </list>
        /// Other messages are not ported (<see cref="NotSupportedException"/>).
        /// </summary>
        public void ActorSendMsg_bd86c0(uint actor, uint sender, int msg, long p1 = 0, int p2 = 0, int p3 = 0)
        {
            switch (msg)
            {
                case 0x30:
                    ActorMsgEffectGone_bd884f(actor, sender);
                    return;
                case 0x58:
                    ActorShowNumber_bd89d3(actor, sender, p1, p2, p3);
                    return;
                case 0x59:
                {
                    if (!ShowMissText) return;
                    uint obj = MakeMissEffect_bfd3b0(actor + 0x10, (uint)p1, M.U32(actor + 0x110));
                    ActorListPush_be8650(actor, obj);
                    new MsgEffect(M, obj).SendMsg(this, actor, MsgEffect.MsgAttach);
                    M.I32(actor + 0x2f0, M.I32(actor + 0x2f0) + 1);
                    return;
                }
                case 0x5c: case 0x5f: case 0x83:
                {
                    int kind = msg == 0x5c ? 0xc : msg == 0x5f ? 0xd : 0x16;
                    uint obj = MakeMsgEffect_bfd630(kind, actor + 0x10, M.U32(actor + 0x110), 1);
                    ActorListPush_be8650(actor, obj);
                    new MsgEffect(M, obj).SendMsg(this, actor, MsgEffect.MsgAttach);
                    return;
                }
                case 0x7e:
                    LaunchEffectOn(actor, 0x150);
                    return;
                case 0x76:                               // package emotion
                    ActorRemoveChatProhibit_bd8816(actor);
                    return;
                case 0xa0:                               // package emotion: p1 pack, p2 emotion id
                    ActorEmotion_bd8c08(actor, (int)p1, p2);
                    return;
                default:
                    throw new NotSupportedException($"CGameActor msg 0x{msg:x} is not ported");
            }
        }

        /// <summary>
        /// Actor msg 0x58 (0x00bd89d3): on maps flagged at world +0x4c / +0x50 only with <see cref="SessionOptionImpl"/>(7);
        /// value −29999 (64-bit) shows nothing; on the local player under Illusion (EFST 0x22) or Pyrexia (0x15b) the value
        /// becomes rand() % 22001 + 10000; then FUN_00beda30.
        /// </summary>
        public void ActorShowNumber_bd89d3(uint actor, uint sender, long value, int argb, int type)
        {
            uint world = M.U32(Mode + 0xcc);
            if (M.U32(world + 0x4c) != 0 || M.U32(world + 0x50) != 0)
                if (!SessionOption(7)) return;
            if (value == -29999L) return;
            int v = (int)value;
            if (M.U32(actor + 0x110) == PlayerAid && (LocalPlayerHasStatus(0x22) || LocalPlayerHasStatus(0x15b)))
                v = Rand.Next() % 0x55f1 + 0x2710;
            ShowNumber_beda30(actor, v, (uint)argb, type);
        }

        /// <summary>
        /// FUN_00beda30 (thiscall on the actor, RET 0x10; sender, value, ARGB, type) — one CMsgEffect per digit:
        /// the value (negative → 0) is clamped to 999 999 or, with <see cref="DamageNumberAbbreviate"/>, shown in thousands
        /// 'K' (≥ 1e6) / millions 'M' (≥ 1e8); the glyphs go into a std::vector&lt;int&gt; least significant first (the K / M
        /// glyph at [0]). For type 0x16 the actor's previous 0x16 glyphs are deleted first. Each glyph: ctor, ARGB +0x98,
        /// msg 0x16 (AID, type), msg 0x40 (actor position, glyph, x offset 4·(n − 1 − 2i) px — mode 2 with values ≥ 1e8:
        /// 3n + 1 for i = 0, else 3·(n − 1) − 8i), msg 0x32 (attach), no ground lighting (+0xb4 = 0), type 0x16 half zoom
        /// (+0x50 = 0.5), into the world list and the actor's list +0x3b0. The vector's allocations are mirrored.
        /// </summary>
        public void ShowNumber_beda30(uint actor, int value, uint argb, int type)
        {
            int v = value >= 0 ? value : 0;
            uint begin = 0, end = 0, cap = 0;
            int rest = v;
            if (DamageNumberAbbreviate)
            {
                if (v >= 100000000) { IntVectorPush(ref begin, ref end, ref cap, 0xb); rest = v / 1000000; }
                else if (v >= 1000000) { IntVectorPush(ref begin, ref end, ref cap, 0xa); rest = v / 1000; }
            }
            else if (v > 999999) rest = 999999;
            do
            {
                int q = rest / 10;
                IntVectorPush(ref begin, ref end, ref cap, rest - q * 10);
                rest = q;
            } while (rest != 0);

            if (type == MsgEffect.TypeComboTotal && M.U32(actor + 0x3b4) > 0)
            {
                uint head = M.U32(actor + 0x3b0);
                for (uint node = M.U32(head); node != M.U32(actor + 0x3b0);)
                {
                    uint obj = M.U32(node + 8);
                    if (M.I32(obj + 0x1a8) != MsgEffect.TypeComboTotal)
                    {
                        node = M.U32(node);
                        continue;
                    }
                    ActorClearMsgSlot_bcfdc0(actor, obj);
                    new MsgEffect(M, obj).SendMsg(this, actor, MsgEffect.MsgOwnerGone);
                    uint next = M.U32(node), prev = M.U32(node + 4);
                    M.U32(prev, next);
                    M.U32(next + 4, prev);
                    M.U32(actor + 0x3b4, M.U32(actor + 0x3b4) - 1);
                    M.Free(node);
                    RemoveWorldObject_9fac00(obj);
                    node = next;
                }
            }

            int n = (int)(end - begin) >> 2;
            for (int i = 0; i < n; i++)
            {
                int glyph = M.I32(begin + (uint)i * 4);
                var e = new MsgEffect(M, M.Alloc(MsgEffect.SizeOf));
                e.Construct(this);
                M.U32(e.A + 0x98, argb);
                e.SendMsg(this, 0, MsgEffect.MsgOwner, (int)M.U32(actor + 0x110), type);
                int x;
                if (v >= 100000000 && DamageNumberMode == 2) x = i == 0 ? 3 * n + 1 : 3 * (n - 1) - 8 * i;
                else x = 4 * (n - 2 * i) - 4;
                e.SendMsg(this, 0, MsgEffect.MsgDigit, (int)(actor + 0x10), glyph, x);
                e.SendMsg(this, actor, MsgEffect.MsgAttach);
                M.U8(e.A + 0xb4, 0);
                if (type == MsgEffect.TypeComboTotal) M.F32(e.A + 0x50, 0.5f);
                AddWorldObject_c0bb30(e.A);
                ActorListPush_be8650(actor, e.A);
            }
            if (begin != 0) VectorDeallocate_6c8c70(begin, (cap - begin) >> 2);
        }

        /// <summary>std::vector&lt;int&gt;::push_back with the reallocation of FUN_004f00b0 (grow by half, at least to size + 1; new buffer, element, copy, free old).</summary>
        void IntVectorPush(ref uint begin, ref uint end, ref uint cap, int value)
        {
            if (end != cap)
            {
                M.I32(end, value);
                end += 4;
                return;
            }
            uint size = (end - begin) >> 2;
            uint capacity = (cap - begin) >> 2;
            uint newSize = size + 1;
            uint newCap = 0x3fffffffu - (capacity >> 1) < capacity ? 0x3fffffffu : Math.Max((capacity >> 1) + capacity, newSize);
            uint buf = VectorAllocate_6c8fa0(newCap);
            M.I32(buf + size * 4, value);
            if (size > 0) M.Copy(buf, begin, (int)size * 4);
            if (begin != 0) VectorDeallocate_6c8c70(begin, capacity);
            begin = buf;
            end = buf + newSize * 4;
            cap = buf + newCap * 4;
        }

        // ---- MISS / burst / LUCKY factories (CGameMode, thiscall on the mode) ---------------------------------

        /// <summary>
        /// FUN_00bfd3b0 (thiscall on the mode, RET 0xc; position pointer, ARGB, AID) — "Miss": the names
        /// <see cref="DamageMessageSprite"/> + ".spr" / ".act" (std::strings of the frame, built through temporaries), a new
        /// CMsgEffect in the world list, msg 0x16 (AID, type 0xd), msg 0xe (position, names, action 0), tint ARGB, no
        /// lighting. Returns the glyph.
        /// </summary>
        public uint MakeMissEffect_bfd3b0(uint pos, uint argb, uint aid)
        {
            string spr = DamageMessageSprite + ".spr", act = DamageMessageSprite + ".act";
            uint s1 = StdStringCopyOfTemporary(spr);
            uint s2 = StdStringCopyOfTemporary(act);
            var e = new MsgEffect(M, M.Alloc(MsgEffect.SizeOf));
            e.Construct(this);
            ListPushBack(M.U32(Mode + 0xcc) + 0x8, e.A);   // FUN_00783f90 on world +0x8
            MsgEffectObjects.Add(e.A);
            e.SendMsg(this, 0, MsgEffect.MsgOwner, (int)aid, MsgEffect.TypeMiss);
            e.Init_e(this, pos, spr, act, 0);
            M.U32(e.A + 0x98, argb);
            M.U8(e.A + 0xb4, 0);
            StdStringFree(s2, act.Length);
            StdStringFree(s1, spr.Length);
            return e.A;
        }

        /// <summary>
        /// FUN_00bfd630 (thiscall on the mode, RET 0x10; kind, position pointer, AID, visible) for the kinds the actor
        /// messages use: 0xc critical burst (msg 0x5c: <see cref="DamageMessageSprite"/>, action 2, type 0xf), 0xd LUCKY
        /// (msg 0x5f: action 3, type 0x11), 0x16 Taekwon burst (msg 0x83: <see cref="DamageBlueMessageSprite"/>.spr with
        /// 이팩트\msg.act unless <see cref="DamageBlueMessageOwnAct"/>, action 2, type 0x19). A new CMsgEffect in the world
        /// list, msg 0x16 (AID, type), visible flag +0x158, msg 0xe (position, names, action). Returns the glyph.
        /// </summary>
        public uint MakeMsgEffect_bfd630(int kind, uint pos, uint aid, byte visible)
        {
            string spr, act;
            uint s1, s2;
            int action, type;
            switch (kind)
            {
                case 0xc:
                case 0xd:
                    spr = DamageMessageSprite + ".spr";
                    act = DamageMessageSprite + ".act";
                    s1 = StdStringCopyOfTemporary(spr);
                    s2 = StdStringCopyOfTemporary(act);
                    action = kind == 0xc ? 2 : 3;
                    type = kind == 0xc ? MsgEffect.TypeCriticalBurst : MsgEffect.TypeLucky;
                    break;
                case 0x16:
                    spr = DamageBlueMessageSprite + ".spr";
                    s1 = StdStringCopyOfTemporary(spr);
                    if (!DamageBlueMessageOwnAct)
                    {
                        act = EffectDir + "msg.act";     // assign(literal 0xfff1c4, 0xe): inline
                        s2 = StdStringBuffer(act);
                    }
                    else
                    {
                        act = DamageBlueMessageSprite + ".act";
                        s2 = StdStringCopyOfTemporary(act);
                    }
                    action = 2;
                    type = MsgEffect.TypeComboBurst;
                    break;
                default:
                    throw new NotSupportedException($"FUN_00bfd630 kind 0x{kind:x} is not ported");
            }
            var e = new MsgEffect(M, M.Alloc(MsgEffect.SizeOf));
            e.Construct(this);
            ListPushBack(M.U32(Mode + 0xcc) + 0x8, e.A);   // FUN_00783f90 on world +0x8
            MsgEffectObjects.Add(e.A);
            e.SendMsg(this, 0, MsgEffect.MsgOwner, (int)aid, type);
            M.U8(e.A + 0x158, visible);
            e.Init_e(this, pos, spr, act, action);
            StdStringFree(s2, act.Length);
            StdStringFree(s1, spr.Length);
            return e.A;
        }

        // ---- MSVC std::string buffers (the heap side of the client's string temporaries) ----------------------

        /// <summary>
        /// The heap buffer an MSVC std::string of <paramref name="s"/> gets when it is assigned to a fresh (inline,
        /// capacity 15) string (FUN_004d9270): none up to 15 bytes; else (len | 15) + 1 bytes from FUN_004d89d0
        /// (operator new; 32-byte aligned with the raw pointer at [-4] from 0x1000 bytes), holding the bytes and a NUL.
        /// Returns the buffer, 0 when inline.
        /// </summary>
        uint StdStringBuffer(string s)
        {
            int n = s.Length;
            if (n < 0x10) return 0;
            uint bytes = ((uint)n | 0xf) + 1;
            uint buf;
            if (bytes < 0x1000) buf = M.Alloc(bytes);
            else
            {
                uint raw = M.Alloc(bytes + 0x23);
                buf = (raw + 0x23) & 0xffffffe0u;
                M.U32(buf - 4, raw);
            }
            for (int i = 0; i < n; i++) M.U8(buf + (uint)i, (byte)s[i]);
            M.U8(buf + (uint)n, 0);
            return buf;
        }

        /// <summary>std::string dtor (FUN_004d8220) of a string of <paramref name="length"/> bytes whose buffer is <paramref name="buf"/>.</summary>
        void StdStringFree(uint buf, int length)
        {
            if (buf == 0) return;
            uint bytes = ((uint)length | 0xf) + 1;
            M.Free(bytes < 0x1000 ? buf : M.U32(buf - 4));
        }

        /// <summary>
        /// FUN_00a21e90 ("%s.spr" into a temporary std::string) + assignment to a fresh frame string (FUN_00758e20 /
        /// FUN_004d9270) + the temporary's dtor: the temporary's buffer, the frame string's buffer, then the temporary
        /// freed. Returns the frame string's buffer (0 when inline).
        /// </summary>
        uint StdStringCopyOfTemporary(string s)
        {
            uint temp = StdStringBuffer(s);
            uint copy = StdStringBuffer(s);
            StdStringFree(temp, s.Length);
            return copy;
        }

        /// <summary>CRenderObject::SetSprName (0xbe6c80) of a name the client formats into a temporary std::string (FUN_00a21e90) first.</summary>
        public void SetSprNameViaStdString(uint obj, string name)
        {
            uint temp = StdStringBuffer(name);
            SetSprName(obj, name);
            StdStringFree(temp, name.Length);
        }

        /// <summary>CRenderObject::SetActName (0xbe3a70) of a name formatted into a temporary std::string first.</summary>
        public void SetActNameViaStdString(uint obj, string name)
        {
            uint temp = StdStringBuffer(name);
            SetActName(obj, name);
            StdStringFree(temp, name.Length);
        }
    }
}
