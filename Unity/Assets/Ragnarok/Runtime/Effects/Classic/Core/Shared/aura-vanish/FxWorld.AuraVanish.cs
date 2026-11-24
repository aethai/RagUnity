// Synced from reference/port/src/RoClassicFx/Shared/aura-vanish/FxWorld.AuraVanish.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public sealed partial class FxRuntime
    {
        /// <summary>
        /// FUN_00a4d760 (thiscall on CEffectMgr (owner AID, id), RET 8) — retires the owner's FIRST effect with id
        /// <paramref name="efId"/> (FUN_00a4dd90): it joins the graveyard and leaves its owner's list. Nothing when the
        /// owner has none. Reached from actor msg 0x6d (CAbleToMakeEffect::SendMsg 0xbd8354) for a new-system id with a
        /// sender — the level aura's removal (RemoveAll FUN_00bcfa70 sends it for 200, 976, 977, …).
        /// </summary>
        public void KillOwnerEffect_a4d760(uint ownerAid, int efId)
        {
            if (EffectMgr == 0) return;
            KillEffect_a4d9e0(FindEffectOfOwner_a4dd90(ownerAid, efId), true);
        }
    }

    public sealed partial class FxWorld
    {
        /// <summary>
        /// FUN_00a4d760 for an actor shim (<see cref="EnsureActor"/>): retires its first new-system effect with id
        /// <paramref name="efId"/> (actor msg 0x6d). The level aura (EF 200 / 976 / 977) is launched like the client's
        /// actor message handlers do (0xbdb9b6 / 0xbdbc73 / …): <c>LaunchNew(id, actor, 0)</c> — FUN_00a4d090(actor AID, id,
        /// actor position, target 0, -1, 0, 0), not attached — and ends with this call, with <see cref="RemoveActor"/> (it
        /// retires on the next tick) or with <see cref="ClearNewEffects"/> (map change).
        /// </summary>
        public void RemoveNewEffect(uint owner, int efId)
        {
            if (owner == 0) return;
            Rt.KillOwnerEffect_a4d760(Rt.M.U32(owner + 0x110), efId);
        }
    }
}
