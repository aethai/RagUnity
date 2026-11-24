using System;
using Ragnarok.Effects;
using UnityEngine;

namespace Ragnarok.Combat {
    /// <summary>
    /// The render state the original client keeps on an actor for its statuses (CGameActor fields the status handlers
    /// write, reference packages/status-effects/UNITY_FORMAT.md "actor"): the status colour while <see cref="UseColor"/>
    /// (+0xa3), <see cref="DrawBody"/> (+0xa0) and <see cref="Visible"/> (+0xa1) — drawn only when both are set —,
    /// <see cref="Shadow"/> (+0xa4), <see cref="FreezeFrame"/> (+0x9d, the animation stops on its current frame) and
    /// <see cref="LockAction"/> (+0x9e, the current action is kept). Each status step changes only the fields it lists.
    /// </summary>
    public struct RoActorStatusLook : IEquatable<RoActorStatusLook> {
        public Color32 Color;
        public bool UseColor;
        public bool DrawBody;
        public bool Visible;
        public bool Shadow;
        public bool FreezeFrame;
        public bool LockAction;

        /// <summary>An actor without status: white, drawn, with its shadow, animating.</summary>
        public static RoActorStatusLook Default => new RoActorStatusLook {
            Color = new Color32(255, 255, 255, 255),
            DrawBody = true,
            Visible = true,
            Shadow = true,
        };

        public bool IsDrawn => DrawBody && Visible;

        /// <summary>The colour the actor's layers are tinted with: the status colour while it is used, else white.</summary>
        public Color Tint => UseColor ? (Color) Color : UnityEngine.Color.white;

        /// <summary>Applies one status step's actor change (only the fields it sets).</summary>
        public void Apply(RoStatusActorState change) {
            if (change == null) {
                return;
            }

            if (change.Color.HasValue) Color = change.Color.Value;
            if (change.UseColor.HasValue) UseColor = change.UseColor.Value;
            if (change.DrawBody.HasValue) DrawBody = change.DrawBody.Value;
            if (change.Visible.HasValue) Visible = change.Visible.Value;
            if (change.Shadow.HasValue) Shadow = change.Shadow.Value;
            if (change.FreezeFrame.HasValue) FreezeFrame = change.FreezeFrame.Value;
            if (change.LockAction.HasValue) {
                LockAction = change.LockAction.Value;
                // the stone / freeze off lists release the action but never write +0x9d back: the client's next
                // SetAction (the actor resumes) starts the animation again
                if (!LockAction && !change.FreezeFrame.HasValue) FreezeFrame = false;
            }
        }

        public bool Equals(RoActorStatusLook other) =>
            Color.r == other.Color.r && Color.g == other.Color.g && Color.b == other.Color.b && Color.a == other.Color.a &&
            UseColor == other.UseColor && DrawBody == other.DrawBody && Visible == other.Visible && Shadow == other.Shadow &&
            FreezeFrame == other.FreezeFrame && LockAction == other.LockAction;

        public override bool Equals(object obj) => obj is RoActorStatusLook other && Equals(other);

        public override int GetHashCode() =>
            ((Color.r << 24) | (Color.g << 16) | (Color.b << 8) | Color.a) ^
            ((UseColor ? 1 : 0) | (DrawBody ? 2 : 0) | (Visible ? 4 : 0) | (Shadow ? 8 : 0) | (FreezeFrame ? 16 : 0) | (LockAction ? 32 : 0));
    }
}
