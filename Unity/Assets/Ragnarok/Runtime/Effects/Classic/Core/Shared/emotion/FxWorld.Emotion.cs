// Synced from reference/port/src/RoClassicFx/Shared/emotion/FxWorld.Emotion.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Emotion balloons (package emotion): ZC_EMOTION (0x00c0: AID, type byte) and its cash-pack form 0x0bea (AID, pack
    /// word, type word) — the packet handler sends actor msg 0xa0 to the actor (the local player's own object for its AID)
    /// and the actor makes a CMsgEffect type 1 balloon. The balloons are ordinary CMsgEffect glyphs: processed / drawn
    /// by the msgeffect passes (<see cref="MsgEffectObjects"/>, AddSprite through <see cref="FxRenderer.SpriteSubmitted"/>).
    /// See packages/emotion/NOTES.md for the host contract.
    /// </summary>
    public sealed partial class FxWorld
    {
        /// <summary>
        /// ZC_EMOTION / ZC 0x0bea → actor msg 0xa0: the emotion <paramref name="type"/> of <paramref name="pack"/> (0 for
        /// ZC_EMOTION) over the registered actor <paramref name="actorAid"/> (<see cref="FxWorld.EnsureActor"/>; unknown →
        /// nothing, as the client). A new balloon every call (a second emotion does not replace a running one); the
        /// chat-ban emotion (ET_CHAT_PROHIBIT 34) only when the actor has none. Returns the balloon (a CMsgEffect address,
        /// <c>new MsgEffect(Rt.M, a)</c>), 0 when none was made — no record for (pack, type) also sends mode msg 0x21
        /// (<see cref="FxRuntime.GameModeSendMsgImpl"/>).
        /// </summary>
        public uint ShowEmotion(uint actorAid, int type, int pack = 0)
        {
            uint actor = MsgActor(actorAid);
            if (actor == 0) return 0;
            int before = Rt.MsgEffectObjects.Count;
            Rt.ActorSendMsg_bd86c0(actor, 0, 0xa0, pack, type);
            return Rt.MsgEffectObjects.Count > before ? Rt.MsgEffectObjects[Rt.MsgEffectObjects.Count - 1] : 0;
        }

        /// <summary>Actor msg 0x76: deletes the actor's chat-ban balloon (slot +0x3a4), if any.</summary>
        public void RemoveChatProhibitEmotion(uint actorAid)
        {
            uint actor = MsgActor(actorAid);
            if (actor != 0) Rt.ActorRemoveChatProhibit_bd8816(actor);
        }

        /// <summary>Live emotion balloons (CMsgEffect type 1) over the actor <paramref name="actorAid"/>.</summary>
        public int EmotionCount(uint actorAid)
        {
            if (!actorsByAid.TryGetValue(actorAid, out uint actor)) return 0;
            int n = 0;
            foreach (uint g in Rt.MsgEffectObjects)
            {
                var e = new MsgEffect(Rt.M, g);
                if (e.Type == MsgEffect.TypeEmotion && e.Anchor == actor) n++;
            }
            return n;
        }
    }
}
