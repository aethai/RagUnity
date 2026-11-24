using Ragnarok.Effects;
using UnityEngine;

namespace Ragnarok.Combat {
    /// <summary>Spawn options shared by the combat presenters.</summary>
    public static class RoCombatEffectOptions {
        /// <summary>Lets the effect know its caster: the caster's anchor and position (projectiles start there, effects
        /// face it).</summary>
        public static void ApplySource(ref RoEffectSpawnOptions options, IRoCombatPresentationContext context, uint casterId) {
            if (context == null || casterId == 0 || !context.TryResolveActorFeedbackAnchor(casterId, out Transform sourceTarget, out Vector3 sourcePosition)) {
                return;
            }

            options.SourceTarget = sourceTarget;
            options.SourceWorldPosition = sourcePosition;
            options.HasSourceWorldPosition = true;
        }
    }
}
