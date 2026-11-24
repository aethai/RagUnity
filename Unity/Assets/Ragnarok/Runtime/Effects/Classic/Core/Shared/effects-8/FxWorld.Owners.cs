// Synced from reference/port/src/RoClassicFx/Shared/effects-8/FxWorld.Owners.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public sealed partial class FxWorld
    {
        /// <summary>
        /// What the client does when an effect owner is destroyed (e.g. a skill unit on ZC_SKILL_DISAPPEAR 0x120:
        /// the CSkill is deleted): CAbleToMakeEffect::~CAbleToMakeEffect (0xbcdc60) sends effect msg 0x18 to every
        /// effect launched on the object, and CRagEffect::SendMsg (vf16 0xb5e650, msg 0x18 → case 1) clears the owner
        /// (+0x138). The next OnProcess then returns false (master check of 0xb527f0) and the world deletes the effect;
        /// its prims die on their own next OnProcess (master cleared by DestroyEffect). Persistent unit effects such as
        /// Safety Wall (EF 315) and Warp Portal (EF 316 / 317) end this way when their unit disappears.
        /// The host still owns the owner shim itself (free it or keep it after this call).
        /// </summary>
        public void ReleaseOwner(uint owner)
        {
            if (owner == 0) return;
            for (int i = 0; i < effects.Count; i++)
                if (Rt.M.U32(effects[i].A + 0x138) == owner) Rt.M.U32(effects[i].A + 0x138, 0);
        }
    }
}
