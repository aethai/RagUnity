// Synced from reference/port/src/RoClassicFx/Shared/actorfx/FxWorld.ActorLook.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public sealed partial class FxWorld
    {
        /// <summary>
        /// The actor side of the per-frame flag word: every registered actor (<see cref="EnsureActor"/>) runs the reset of
        /// its OnProcess prologue (FUN_00bd4210) at the start of each world pass, before the effects — in the client the
        /// actor is older than its effects in the world list, and the first of them to run on a ticked frame clears the
        /// word once after a render; effects then set their bits again. Called by <see cref="Pass"/>.
        /// </summary>
        void ProcessActorPrologues()
        {
            foreach (var kv in actorsByAid) Rt.ActorFlagReset_bd4210(kv.Value);
        }

        /// <summary>
        /// The actor was drawn this frame: +0xec = 1 (the tail of RenderBody 0xbd6080, of the CPc renderer 0x5e5bd0 and
        /// of 0xbd67c0 / 0xcc51e0). The next ticked pass clears the flag word; until then (frames without a logic tick)
        /// the renderer keeps seeing the same bits. Hosts must call this once per rendered frame for every visible actor
        /// (<see cref="GetActorLook"/> does it), or effect looks never switch off.
        /// </summary>
        public void MarkActorRendered(uint aid)
        {
            if (actorsByAid.TryGetValue(aid, out uint actor)) Rt.M.U8(actor + 0xec, 1);
        }

        /// <summary>
        /// The effect look of actor <paramref name="aid"/> for the frame being rendered (call after <see cref="Advance"/>);
        /// marks the actor rendered unless <paramref name="markRendered"/> is false. Default look for unknown actors.
        /// </summary>
        public FxActorLook GetActorLook(uint aid, bool markRendered = true)
        {
            if (!actorsByAid.TryGetValue(aid, out uint actor)) return new FxActorLook { Scale = 1.0f, Tint = 0xffffffffu };
            var look = FxActorLook.Decode(Rt, actor);
            if (markRendered) Rt.M.U8(actor + 0xec, 1);
            return look;
        }

        /// <summary>
        /// Actor message 0x6d (CAbleToMakeEffect::SendMsg 0xbd7fc0, case 6 @0xbd8354): the OLDEST CRagEffect with type
        /// <paramref name="efId"/> launched on <paramref name="owner"/> gets effect msg 0x18 (owner cleared → dies on the next
        /// pass) and leaves the actor's list. Only one instance. (A new-system id with a sender goes to FUN_00a4d760 instead.)
        /// Returns whether an effect was found.
        /// </summary>
        public bool RemoveEffectById(uint owner, int efId)
        {
            if (owner == 0) return false;
            for (int i = 0; i < effects.Count; i++)
            {
                uint a = effects[i].A;
                if (Rt.M.U32(a + 0x138) != owner || Rt.M.I32(a + 0x168) != efId) continue;
                Rt.M.U32(a + 0x138, 0);
                return true;
            }
            return false;
        }

        /// <summary>Actor message 0x8f (case 8 @0xbd84d4): every CRagEffect of type <paramref name="efId"/> on the owner gets msg 0x18.</summary>
        public int RemoveAllEffectsById(uint owner, int efId)
        {
            if (owner == 0) return 0;
            int n = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                uint a = effects[i].A;
                if (Rt.M.U32(a + 0x138) != owner || Rt.M.I32(a + 0x168) != efId) continue;
                Rt.M.U32(a + 0x138, 0);
                n++;
            }
            return n;
        }

        /// <summary>Actor message 0x78 (case 7 @0xbd8555): is an effect of type <paramref name="efId"/> alive on the owner?</summary>
        public bool HasEffect(uint owner, int efId)
        {
            if (owner == 0) return false;
            for (int i = 0; i < effects.Count; i++)
            {
                uint a = effects[i].A;
                if (Rt.M.U32(a + 0x138) == owner && Rt.M.I32(a + 0x168) == efId) return true;
            }
            return false;
        }
    }
}
