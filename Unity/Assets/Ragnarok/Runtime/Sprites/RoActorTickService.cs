using System.Collections.Generic;
using Ragnarok.Diagnostics;
using UnityEngine;
using Unity.Profiling;

namespace Ragnarok.Sprites {
    public interface IRoActorUpdateTick {
        void ActorUpdateTick(float now);
    }

    public interface IRoActorLateTick {
        void ActorLateTick(float now);
    }

    public interface IRoActorTickBudget {
        bool ShouldRunActorTick(float now, bool lateTick);
    }

    public enum RoActorTickVisibilityState {
        Unknown = 0,
        Visible = 1,
        Offscreen = 2,
        Inactive = 3
    }

    public interface IRoActorTickVisibility {
        RoActorTickVisibilityState ResolveActorTickVisibility(bool lateTick);
    }

    [DefaultExecutionOrder(10030)]
    public sealed class RoActorTickService : MonoBehaviour {
        private static RoActorTickService instance;
        private static readonly List<IRoActorUpdateTick> UpdateTicks = new List<IRoActorUpdateTick>(256);
        private static readonly List<IRoActorLateTick> LateTicks = new List<IRoActorLateTick>(256);
        private static readonly Dictionary<System.Type, string> TickTypeNames = new Dictionary<System.Type, string>(16);
        private static readonly ProfilerMarker ActorTickMarker = new ProfilerMarker("RO.Actor.Tick");
        private static readonly ProfilerMarker ActorLateTickMarker = new ProfilerMarker("RO.Actor.LateTick");
        private static int registeredAnimatorUpdateTicks;
        private static readonly Dictionary<System.Type, ProfilerMarker> TickTypeMarkers = new Dictionary<System.Type, ProfilerMarker>(16);
        private static readonly Dictionary<System.Type, ProfilerMarker> LateTickTypeMarkers = new Dictionary<System.Type, ProfilerMarker>(16);
        private static readonly Dictionary<System.Type, ProfilerMarker> LateCheckTypeMarkers = new Dictionary<System.Type, ProfilerMarker>(16);

        /// <summary>Diagnostics (!tickmarkers): wrap every tick in a per-type marker. Off by default — with ~1600 ticks a
        /// frame the markers' own cost distorts the frame being measured.</summary>
        public static bool SplitTypeMarkers { get; set; }

        static ProfilerMarker CheckMarker(object tick) {
            System.Type type = tick.GetType();
            if (!LateCheckTypeMarkers.TryGetValue(type, out ProfilerMarker marker)) {
                marker = new ProfilerMarker("RO.LateCheck." + type.Name);
                LateCheckTypeMarkers[type] = marker;
            }

            return marker;
        }

        /// <summary>A profiler marker per tick type ("RO.Tick.&lt;type&gt;" / "RO.LateTick.&lt;type&gt;"), so cpuprobe splits the actor ticks.</summary>
        static ProfilerMarker TypeMarker(object tick, bool late) {
            System.Type type = tick.GetType();
            Dictionary<System.Type, ProfilerMarker> markers = late ? LateTickTypeMarkers : TickTypeMarkers;
            if (!markers.TryGetValue(type, out ProfilerMarker marker)) {
                marker = new ProfilerMarker((late ? "RO.LateTick." : "RO.Tick.") + type.Name);
                markers[type] = marker;
            }

            return marker;
        }
        private readonly Dictionary<string, int> lateTickTypeCounts = new Dictionary<string, int>(16);
        private ActorTickVisibilityCounts updateVisibilityCounts;
        private ActorTickVisibilityCounts lateVisibilityCounts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() {
            instance = null;
            UpdateTicks.Clear();
            LateTicks.Clear();
            TickTypeNames.Clear();
            registeredAnimatorUpdateTicks = 0;
        }

        public static int RegisteredAnimatorUpdateTickCount => registeredAnimatorUpdateTicks;

        private const float CameraRevisionEpsilonDegrees = 0.1f;
        private static UnityEngine.Camera frameCamera;
        private static Quaternion revisionCameraRotation;

        /// <summary>The main camera this frame (read once per frame by the service, before the late ticks).</summary>
        public static UnityEngine.Camera FrameCamera => frameCamera;

        public static Quaternion FrameCameraRotation { get; private set; } = Quaternion.identity;

        /// <summary>Changes when the main camera turns (or is replaced); billboards re-orient only then.</summary>
        public static int CameraRevision { get; private set; } = 1;

        private static void UpdateFrameCamera() {
            if (frameCamera == null || !frameCamera.isActiveAndEnabled) {
                UnityEngine.Camera main = UnityEngine.Camera.main;
                if (main != frameCamera) {
                    frameCamera = main;
                    CameraRevision++;
                }
            }

            if (frameCamera == null) {
                return;
            }

            Quaternion rotation = frameCamera.transform.rotation;
            FrameCameraRotation = rotation;
            if (Quaternion.Angle(rotation, revisionCameraRotation) > CameraRevisionEpsilonDegrees) {
                revisionCameraRotation = rotation;
                CameraRevision++;
            }
        }

        public static void Register(IRoActorUpdateTick tick) {
            if (!Application.isPlaying || tick == null || UpdateTicks.Contains(tick)) {
                return;
            }

            EnsureInstance();
            UpdateTicks.Add(tick);
            if (tick is RoActSpriteAnimator) {
                registeredAnimatorUpdateTicks++;
            }
        }

        public static void Register(IRoActorLateTick tick) {
            if (!Application.isPlaying || tick == null || LateTicks.Contains(tick)) {
                return;
            }

            EnsureInstance();
            LateTicks.Add(tick);
        }

        /// <summary>An animator ticked by its owner (a layered actor) instead of this service still counts for the crowd budgets.</summary>
        public static void CountOwnerTickedAnimator(int delta) {
            if (!Application.isPlaying) {
                return;
            }

            registeredAnimatorUpdateTicks = Mathf.Max(0, registeredAnimatorUpdateTicks + delta);
        }

        public static void Unregister(IRoActorUpdateTick tick) {
            if (tick == null) {
                return;
            }

            if (UpdateTicks.Remove(tick) && tick is RoActSpriteAnimator) {
                registeredAnimatorUpdateTicks = Mathf.Max(0, registeredAnimatorUpdateTicks - 1);
            }
        }

        public static void Unregister(IRoActorLateTick tick) {
            if (tick == null) {
                return;
            }

            LateTicks.Remove(tick);
        }

        private static void EnsureInstance() {
            if (instance != null) {
                return;
            }

            var serviceObject = new GameObject("RO Actor Tick Service") {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(serviceObject);
            instance = serviceObject.AddComponent<RoActorTickService>();
        }

        private void Update() {
            long actorUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                using (RoProfilerMarkers.ActorTick.Auto())
                using (ActorTickMarker.Auto()) {
                    float now = Time.realtimeSinceStartup;
                    int executedTicks = 0;
                    int executedAnimatorTicks = 0;
                    bool collectDiagnostics = RoRuntimePerfCounters.DiagnosticsEnabled;
                    if (collectDiagnostics) {
                        updateVisibilityCounts.Clear();
                    }

                    RoRuntimePerfCounters.SetActiveAnimatorUpdateTickCount(registeredAnimatorUpdateTicks);
                    RoSpriteFrameGeometryCache.ProcessPrewarmBudget();
                    for (int i = UpdateTicks.Count - 1; i >= 0; i--) {
                        IRoActorUpdateTick tick = UpdateTicks[i];
                        if (IsDestroyed(tick)) {
                            UpdateTicks.RemoveAt(i);
                            continue;
                        }

                        if (collectDiagnostics) {
                            updateVisibilityCounts.Add(ResolveActorTickVisibility(tick, lateTick: false));
                        }

                        if (tick is IRoActorTickBudget budget && !budget.ShouldRunActorTick(now, lateTick: false)) {
                            continue;
                        }

                        if (SplitTypeMarkers) {
                            using (TypeMarker(tick, false).Auto()) {
                                tick.ActorUpdateTick(now);
                            }
                        } else {
                            tick.ActorUpdateTick(now);
                        }

                        executedTicks++;
                        if (tick is RoActSpriteAnimator) {
                            executedAnimatorTicks++;
                        }
                    }

                    RoRuntimePerfCounters.AddActorUpdateTicks(executedTicks);
                    RoRuntimePerfCounters.AddAnimatorTicks(executedAnimatorTicks);
                    if (collectDiagnostics) {
                        RoRuntimePerfCounters.SetActorUpdateVisibilityCounts(
                            updateVisibilityCounts.Registered,
                            updateVisibilityCounts.Visible,
                            updateVisibilityCounts.Offscreen,
                            updateVisibilityCounts.Inactive,
                            updateVisibilityCounts.Unknown);
                    }
                }
            } finally {
                RoRuntimePerfCounters.AddActorServiceUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(actorUpdateStartTicks));
            }
        }

        private void LateUpdate() {
            long actorLateUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                using (ActorLateTickMarker.Auto()) {
                    UpdateFrameCamera();
                    float now = Time.realtimeSinceStartup;
                    int executedTicks = 0;
                    bool collectDiagnostics = RoRuntimePerfCounters.DiagnosticsEnabled;
                    if (collectDiagnostics) {
                        lateTickTypeCounts.Clear();
                        lateVisibilityCounts.Clear();
                    }

                    for (int i = LateTicks.Count - 1; i >= 0; i--) {
                        IRoActorLateTick tick = LateTicks[i];
                        if (IsDestroyed(tick)) {
                            LateTicks.RemoveAt(i);
                            continue;
                        }

                        if (collectDiagnostics) {
                            lateVisibilityCounts.Add(ResolveActorTickVisibility(tick, lateTick: true));
                        }

                        bool run;
                        if (SplitTypeMarkers) {
                            using (CheckMarker(tick).Auto()) {
                                run = !(tick is IRoActorTickBudget budget) || budget.ShouldRunActorTick(now, lateTick: true);
                            }
                        } else {
                            run = !(tick is IRoActorTickBudget budget) || budget.ShouldRunActorTick(now, lateTick: true);
                        }

                        if (!run) {
                            continue;
                        }

                        if (SplitTypeMarkers) {
                            using (TypeMarker(tick, true).Auto()) {
                                tick.ActorLateTick(now);
                            }
                        } else {
                            tick.ActorLateTick(now);
                        }

                        executedTicks++;
                        if (collectDiagnostics) {
                            CountLateTickType(tick);
                        }
                    }

                    RoRuntimePerfCounters.AddActorLateTicks(executedTicks);
                    if (collectDiagnostics) {
                        RoRuntimePerfCounters.SetActorLateVisibilityCounts(
                            lateVisibilityCounts.Registered,
                            lateVisibilityCounts.Visible,
                            lateVisibilityCounts.Offscreen,
                            lateVisibilityCounts.Inactive,
                            lateVisibilityCounts.Unknown);
                        FlushLateTickTypeCounts();
                    }
                }
            } finally {
                RoRuntimePerfCounters.AddActorServiceLateUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(actorLateUpdateStartTicks));
            }
        }

        private void CountLateTickType(IRoActorLateTick tick) {
            string typeName = ResolveTickTypeName(tick);
            lateTickTypeCounts.TryGetValue(typeName, out int count);
            lateTickTypeCounts[typeName] = count + 1;
        }

        private void FlushLateTickTypeCounts() {
            foreach (KeyValuePair<string, int> count in lateTickTypeCounts) {
                RoRuntimePerfCounters.AddActorLateTickTypeCount(count.Key, count.Value);
            }
        }

        private static string ResolveTickTypeName(object tick) {
            if (tick == null) {
                return "unknown";
            }

            System.Type type = tick.GetType();
            if (TickTypeNames.TryGetValue(type, out string typeName)) {
                return typeName;
            }

            typeName = type.Name;
            TickTypeNames[type] = typeName;
            return typeName;
        }

        private static bool IsDestroyed(object tick) {
            return tick == null || tick is Object unityObject && unityObject == null;
        }

        private static RoActorTickVisibilityState ResolveActorTickVisibility(object tick, bool lateTick) {
            if (tick is IRoActorTickVisibility visibility) {
                return visibility.ResolveActorTickVisibility(lateTick);
            }

            if (tick is Behaviour behaviour &&
                (!behaviour.isActiveAndEnabled || !behaviour.gameObject.activeInHierarchy)) {
                return RoActorTickVisibilityState.Inactive;
            }

            return RoActorTickVisibilityState.Unknown;
        }

        private struct ActorTickVisibilityCounts {
            public int Registered;
            public int Visible;
            public int Offscreen;
            public int Inactive;
            public int Unknown;

            public void Clear() {
                Registered = 0;
                Visible = 0;
                Offscreen = 0;
                Inactive = 0;
                Unknown = 0;
            }

            public void Add(RoActorTickVisibilityState state) {
                Registered++;
                switch (state) {
                    case RoActorTickVisibilityState.Visible:
                        Visible++;
                        break;
                    case RoActorTickVisibilityState.Offscreen:
                        Offscreen++;
                        break;
                    case RoActorTickVisibilityState.Inactive:
                        Inactive++;
                        break;
                    default:
                        Unknown++;
                        break;
                }
            }
        }
    }
}
