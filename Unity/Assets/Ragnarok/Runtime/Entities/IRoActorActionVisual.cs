namespace Ragnarok.Entities {
    /// <summary>
    /// Implemented by both the layered player/equipment visual (<see cref="RoActorLayeredVisual"/>) and a
    /// single sprite (<see cref="RoActorSpriteVisual"/>) so the shared action-timing policy in
    /// <see cref="RoActorActionPresenter"/> can fit either one's current action to a target duration
    /// without caring which concrete visual it is driving.
    /// </summary>
    public interface IRoActorActionVisual {
        bool TryGetCurrentActionBaseDurationSeconds(out float durationSeconds);
        bool TryScaleCurrentActionsToDurationSeconds(float durationSeconds, float fallbackScale = 1f);
        void SetAbsoluteFrameSeconds(float perFrameSeconds);
        void SetFrameDelayScale(float scale);
        /// <summary>frames × ACT delay of a motion at speed 1 (the length the original's motion clock plays it in).</summary>
        bool TryGetMotionBaseDurationSeconds(Ragnarok.Sprites.RoSpriteMotion motion, out float durationSeconds);
    }
}
