// Synced from reference/port/src/RoClassicFx/Shared/msgeffect/FxWorld.MsgEffect.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Damage numbers and combat messages owned by the world (CMsgEffect, package msgeffect): the actor messages the
    /// client's hit handler sends at a hit's due time, the per-tick pass and the render pass. The glyphs follow their
    /// actor shim's x / z every tick and draw through <see cref="FxRenderer.SpriteSubmitted"/> (AddSprite, with the
    /// glyph's +0x124 = 0x200 render flags). See packages/msgeffect/NOTES.md for the host contract.
    /// </summary>
    public sealed partial class FxWorld
    {
        /// <summary>Live CMsgEffect glyphs (one per digit / message), in creation order.</summary>
        public int MsgEffectCount => Rt.MsgEffectObjects.Count;

        public IReadOnlyList<uint> MsgEffects => Rt.MsgEffectObjects;

        uint MsgActor(uint actorAid)
        {
            if (!actorsByAid.TryGetValue(actorAid, out uint actor)) return 0;
            Rt.EnsureActorMsgEffectList(actor);
            return actor;
        }

        /// <summary>
        /// Actor msg 0x58 (value, ARGB, type) → FUN_00beda30: the number over the actor <paramref name="actorAid"/> (a
        /// registered actor, <see cref="FxWorld.EnsureActor"/>). <paramref name="type"/>: 0xe normal, 0x10 critical,
        /// 0x15 multi-hit part, 0x16 combo total (replaces the actor's previous total), 0x72 heal. Value −29999 shows
        /// nothing; negative values show 0; values above 999 999 are clamped (or abbreviated, <see cref="FxRuntime.DamageNumberAbbreviate"/>).
        /// Returns the number of glyphs created (0: unknown actor or a type the port does not draw).
        /// </summary>
        public int ShowNumber(uint actorAid, int value, uint argb, int type)
        {
            if (!MsgEffect.IsNumberType(type)) return 0;
            uint actor = MsgActor(actorAid);
            if (actor == 0) return 0;
            int before = Rt.MsgEffectObjects.Count;
            Rt.ActorSendMsg_bd86c0(actor, 0, 0x58, value, (int)argb, type);
            return Rt.MsgEffectObjects.Count - before;
        }

        /// <summary>Actor msg 0x59 (ARGB) → "Miss" over <paramref name="actorAid"/> (the client shows it on the attacker). Returns the glyph (0: none).</summary>
        public uint ShowMiss(uint actorAid, uint argb)
        {
            uint actor = MsgActor(actorAid);
            if (actor == 0) return 0;
            int before = Rt.MsgEffectObjects.Count;
            Rt.ActorSendMsg_bd86c0(actor, 0, 0x59, (int)argb);
            return Rt.MsgEffectObjects.Count > before ? Rt.MsgEffectObjects[Rt.MsgEffectObjects.Count - 1] : 0;
        }

        /// <summary>Actor msg 0x5c → the critical burst (msg.act action 2, type 0xf) behind a critical number. Returns the glyph.</summary>
        public uint ShowCriticalBurst(uint actorAid) => ShowMessage(actorAid, 0x5c);

        /// <summary>Actor msg 0x5f → "LUCKY!!" (msg.act action 3, type 0x11) for a lucky dodge. Returns the glyph.</summary>
        public uint ShowLucky(uint actorAid) => ShowMessage(actorAid, 0x5f);

        /// <summary>Actor msg 0x83 → the Taekwon kick burst (bluemsg.spr, type 0x19) before a kick's combo total. Returns the glyph.</summary>
        public uint ShowComboBurst(uint actorAid) => ShowMessage(actorAid, 0x83);

        uint ShowMessage(uint actorAid, int msg)
        {
            uint actor = MsgActor(actorAid);
            if (actor == 0) return 0;
            Rt.ActorSendMsg_bd86c0(actor, 0, msg);
            return Rt.MsgEffectObjects[Rt.MsgEffectObjects.Count - 1];
        }

        /// <summary>
        /// Actor msg 0x7e → EF 336 (EF_GUARD) on the actor, through CAbleToMakeEffect::LaunchEffect (<see cref="Launch"/>):
        /// what a miss by a monster on the local player under Kyrie Eleison / Parrying shows instead of "Miss".
        /// </summary>
        public RagEffect? ShowGuard(uint actorAid)
        {
            if (!actorsByAid.TryGetValue(actorAid, out uint actor)) return null;
            return Launch(0x150, actor);
        }

        /// <summary>Deletes every glyph now (map change). The client never does this itself: its world is rebuilt.</summary>
        public void ClearMsgEffects()
        {
            var list = Rt.MsgEffectObjects;
            while (list.Count > 0) Rt.RemoveWorldObject_9fac00(list[list.Count - 1]);
        }

        /// <summary>World pass part: CMsgEffect::OnProcess (vf01) for each glyph; dead ones are deleted (FUN_009fac00).</summary>
        void ProcessMsgEffects()
        {
            var list = Rt.MsgEffectObjects;
            for (int i = 0; i < list.Count; i++)
            {
                uint obj = list[i];
                if (new MsgEffect(Rt.M, obj).OnProcess(Rt))
                {
                    if (Rt.TicksThisFrame > 0) Rt.MsgEffectsProcessed.Add(obj);
                    continue;
                }
                Rt.RemoveWorldObject_9fac00(obj);
                i--;
            }
        }

        /// <summary>Draws the glyphs that have been processed on a tick (<see cref="FxRuntime.MsgEffectsProcessed"/>).</summary>
        void RenderMsgEffects(uint matrix)
        {
            var list = Rt.MsgEffectObjects;
            for (int i = 0; i < list.Count; i++)
            {
                if (Rt.MsgEffectsProcessed.Contains(list[i])) new MsgEffect(Rt.M, list[i]).Render(Rt, matrix);
            }
        }

        /// <summary>The CMsgEffect part of the actor teardown (<see cref="RemoveActor"/>): its glyphs are detached (msg 0x31) and stay where they are.</summary>
        void DetachMsgEffects(uint actor)
        {
            if (Rt.M.U32(actor + 0x3b0) != 0) Rt.ActorDetachMsgEffects_bd3374(actor, true);
        }
    }
}
