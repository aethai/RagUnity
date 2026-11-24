using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Effects {
    public struct RoEffectSpawnOptions {
        public Transform Parent;
        public Transform FollowTarget;
        public Vector3 FollowLocalOffset;
        public Transform SourceTarget;
        public Vector3 SourceWorldPosition;
        public bool HasSourceWorldPosition;
        public UnityEngine.Camera RenderCamera;
        public bool DisableFaceCamera;
        public bool DisableDepthTest;
        public bool OverrideRenderQueue;
        public int RenderQueueBase;
        public bool OverrideSortingOrder;
        public int SortingOrder;
        public RoWeatherWindSettings WeatherWindSettings;
        public float SkillVisualScale;
        /// <summary>Skill level of the cast that produced this effect (0 = unknown); the classic engine reads it (bolt counts, hit counts).</summary>
        public int SkillLevel;
        /// <summary>Account ids of the caster / the target (0 = none); the classic engine resolves them to its actor objects.</summary>
        public uint SourceActorId;
        public uint TargetActorId;
        /// <summary>
        /// The actor the effect is launched on (0 = a cell or none). With no <see cref="FollowTarget"/> (the actor is not
        /// presented yet) the classic engine still owns it by this actor, so it ends when the actor leaves.
        /// </summary>
        public uint OnActorId;
        /// <summary>The position the effect aims at (the other end of a caster→target skill), when known.</summary>
        public Vector3 TargetWorldPosition;
        public bool HasTargetWorldPosition;
        /// <summary>Classic engine: the effect's duration in 16 ms ticks set right after launch (CRagEffect +0x17c, what
        /// the client's vf18 SetDuration writes, e.g. a cast circle kept for the cast time); 0 = the id's default.</summary>
        public int ClassicDurationTicks;
        /// <summary>Classic engine: launch on this existing owner object (e.g. a skill unit's shared owner from
        /// <see cref="RoEffectManager.CreateClassicUnitOwner"/>) instead of an actor shim or a throw-away owner.</summary>
        public uint ClassicOwner;
        /// <summary>SPR effects: the ACT action the sprite starts on (the client's CMsgEffect picks one, e.g. the
        /// freeze overlay's action 1 = the ice breaking).</summary>
        public int SpriteActionIndex;

        public float EffectiveSkillVisualScale => SkillVisualScale > 0f ? SkillVisualScale : 1f;
        public bool FaceCamera => !DisableFaceCamera;
        public bool UseDepthTest => !DisableDepthTest;
    }
}
