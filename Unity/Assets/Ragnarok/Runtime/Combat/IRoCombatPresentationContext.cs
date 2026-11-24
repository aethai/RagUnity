using Ragnarok.Effects;
using UnityEngine;

namespace Ragnarok.Combat {
    public interface IRoCombatPresentationContext {
        float NowSeconds { get; }
        RoCombatPresentationOptions CombatOptions { get; }
        RoCombatTrace CombatTrace { get; }
        RoEffectManager ResolveEffectManager();
        bool TryResolveActorFeedbackAnchor(uint actorId, out Transform followTarget, out Vector3 worldPosition);
        bool TryResolveCellFeedbackPosition(int x, int y, out Vector3 worldPosition);
        /// <summary>A CMsgEffect number over an actor (actor msg 0x58: value, ARGB, type).</summary>
        void ShowNumber(uint actorId, int value, uint argb, int type);
    }

    public interface IRoCombatActorPresentationContext {
        bool IsOwnActor(uint actorId);

        /// <summary>The actor's status render state changed (<see cref="RoStatusPresenter"/>): tint, visibility, shadow, frame hold.</summary>
        void ApplyActorStatusLook(uint actorId, in RoActorStatusLook look);
    }
}
