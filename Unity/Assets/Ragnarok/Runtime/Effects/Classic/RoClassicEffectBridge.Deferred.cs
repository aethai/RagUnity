using UnityEngine;

namespace Ragnarok.Effects.Classic {
    public sealed partial class RoClassicEffectBridge {
        // The queued calls of the public entry points (DeferLaunches), each in its own method: a lambda inside an entry
        // point that captures its parameters allocates the closure when the method is entered, on every call, also
        // when nothing is queued (!woe 300: ~250 closures a frame with SpriteTexture's).

        void DeferSetLevelAura(uint aid, Transform anchor, int mainId, int subId, bool blocked) => Defer(() => SetLevelAura(aid, anchor, mainId, subId, blocked), nameof(SetLevelAura));

        void DeferEndEffect(uint handle) => Defer(() => EndEffect(handle), nameof(EndEffect));

        void DeferRemoveAllEffectsById(uint aid, int effectId) => Defer(() => RemoveAllEffectsById(aid, effectId), nameof(RemoveAllEffectsById));

        void DeferRemoveEffectById(uint aid, int effectId) => Defer(() => RemoveEffectById(aid, effectId), nameof(RemoveEffectById));

        void DeferSetActorOption(uint aid, int option, Transform anchor) => Defer(() => SetActorOption(aid, option, anchor), nameof(SetActorOption));

        void DeferSetActorArgb(uint aid, int a, int r, int g, int b, Transform anchor) => Defer(() => SetActorArgb(aid, a, r, g, b, anchor), nameof(SetActorArgb));

        void DeferReleaseSharedOwner(uint owner) => Defer(() => ReleaseSharedOwner(owner), nameof(ReleaseSharedOwner));

        void DeferLaunchGroundScope(int cellX, int cellY, int skillId, int delayMs, uint casterAid) => Defer(() => LaunchGroundScope(cellX, cellY, skillId, delayMs, casterAid), nameof(LaunchGroundScope));

        void DeferRegisterActorAnchor(uint aid, Transform anchor) => Defer(() => RegisterActorAnchor(aid, anchor), nameof(RegisterActorAnchor));

        void DeferLaunchArrow(uint sourceAid, Transform source, uint targetAid, Transform target, float releaseTicks, int spriteClass) => Defer(() => LaunchArrow(sourceAid, source, targetAid, target, releaseTicks, spriteClass), nameof(LaunchArrow));

        void DeferShowNumber(uint aid, Transform anchor, int value, uint argb, int type) => Defer(() => ShowNumber(aid, anchor, value, argb, type), nameof(ShowNumber));

        void DeferShowMiss(uint aid, Transform anchor, uint argb) => Defer(() => ShowMiss(aid, anchor, argb), nameof(ShowMiss));

        void DeferShowCriticalBurst(uint aid, Transform anchor) => Defer(() => ShowCriticalBurst(aid, anchor), nameof(ShowCriticalBurst));

        void DeferShowComboBurst(uint aid, Transform anchor) => Defer(() => ShowComboBurst(aid, anchor), nameof(ShowComboBurst));

        void DeferShowLucky(uint aid, Transform anchor) => Defer(() => ShowLucky(aid, anchor), nameof(ShowLucky));

        void DeferForgetActor(uint actorId, Transform root) => Defer(() => ForgetActor(actorId, root), nameof(ForgetActor));

        void DeferLaunchGroundEffect(int effectId, Vector3 worldPosition, uint reserved) => Defer(() => {
            if (!deferredHandles.Remove(reserved)) return;
            EnsureWorld();
            Vector3 at = ToRo(worldPosition);
            LaunchGround(at.x, at.y, at.z, effectId, 0f, reserved, out _);
        }, nameof(LaunchGroundEffect));

        void DeferTrySpawn(int effectId, Vector3 worldPosition, RoEffectSpawnOptions options, uint reserved) => Defer(() => {
            if (deferredHandles.Remove(reserved)) SpawnNow(effectId, worldPosition, options, reserved, out _);
        }, nameof(TrySpawn));
    }
}
