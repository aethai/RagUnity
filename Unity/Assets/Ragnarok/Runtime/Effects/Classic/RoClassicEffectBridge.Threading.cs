using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;

namespace Ragnarok.Effects.Classic {
    /// <summary>
    /// The engine's render pass (CRagEffect / CEffectPrim Render, then CRenderer::Flush's list replay) on a worker thread.
    /// The bridge's LateUpdate advances the world on the main thread, collects the actor layers and starts the job; the
    /// worker renders while the main thread submits the scene; the next frame starts by waiting for it (an EarlyUpdate
    /// hook, before any script can call the bridge), and that frame's LateUpdate draws the result. The engine's own order
    /// is kept (render, footprints, actor looks, draw, logic), one frame later: the vertex shader places the vertices with
    /// the camera the core projected with, so the world-anchored ones stay put when the camera moves, and the screen-space
    /// ones follow the current camera.
    /// Anything on the worker that needs Unity (a first texture load, a sprite texture, a file read) is handed to the main
    /// thread and waited for; sounds and other fire-and-forget calls are queued. Every public entry point waits for a
    /// running job first (<see cref="WaitForRenderJob"/>).
    /// </summary>
    public sealed partial class RoClassicEffectBridge {
        /// <summary>
        /// !toggle fxthread: the render pass on the worker thread (off: everything in LateUpdate, as before). On in play
        /// mode only: edit-mode tools (RoClassicFxCapture) step RunFrame and read its result at once.
        /// fixed-seed !bench 8, 300 players, two fresh runs each: + fxstorm 300 p50 26.2 / 21.0 -> 19.3 / 17.4 ms,
        /// + skillspam 40/s 31.3 / 27.8 -> 24.0 / 21.6 ms.
        /// </summary>
        public static bool ThreadedRender = true;

        bool UseRenderThread => ThreadedRender && Application.isPlaying;

        /// <summary>Diagnostics: main-thread Stopwatch ticks spent waiting for the job, and joins forced by a public call.</summary>
        public static long JoinWaitTicks, CallerJoins, MainRequestsServed;

        static readonly Unity.Profiling.ProfilerMarker JoinMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Join");
        static readonly Unity.Profiling.ProfilerMarker JobMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Job");

        sealed class MainRequest {
            public Action Work;
            public ManualResetEventSlim Done;
            public Exception Error;
        }

        Thread renderThread;
        readonly AutoResetEvent kickSignal = new AutoResetEvent(false);
        readonly AutoResetEvent wakeMain = new AutoResetEvent(false);
        readonly ConcurrentQueue<MainRequest> mainRequests = new ConcurrentQueue<MainRequest>();
        volatile bool jobRunning;
        volatile bool stopRenderThread;
        Exception jobError;
        /// <summary>A job's vertices and draws wait in the frame buffers for the next LateUpdate.</summary>
        bool jobResultsReady;
        int mainThreadId = -1;
        static bool joinHookInstalled;

        bool OnMainThread => mainThreadId < 0 || Thread.CurrentThread.ManagedThreadId == mainThreadId;

        void CaptureMainThread() {
            if (mainThreadId < 0) {
                mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        /// <summary>Runs <paramref name="work"/> on the main thread and waits for it (directly when already there).</summary>
        void RunOnMain(Action work) {
            if (OnMainThread) {
                work();
                return;
            }

            var request = new MainRequest { Work = work, Done = new ManualResetEventSlim(false) };
            mainRequests.Enqueue(request);
            wakeMain.Set();
            request.Done.Wait();
            request.Done.Dispose();
            if (request.Error != null) {
                throw new InvalidOperationException("main-thread request failed", request.Error);
            }
        }

        /// <summary>Queues <paramref name="work"/> for the main thread without waiting (directly when already there).</summary>
        void PostToMain(Action work) {
            if (OnMainThread) {
                work();
                return;
            }

            mainRequests.Enqueue(new MainRequest { Work = work });
            wakeMain.Set();
        }

        void ServeMainRequests() {
            while (mainRequests.TryDequeue(out MainRequest request)) {
                try {
                    request.Work();
                } catch (Exception e) {
                    request.Error = e;
                    if (request.Done == null) Debug.LogException(e);
                } finally {
                    MainRequestsServed++;
                    request.Done?.Set();
                }
            }
        }

        /// <summary>Waits for a running render job, serving its main-thread requests meanwhile.</summary>
        void JoinRenderJob() {
            if (!jobRunning) {
                ServeMainRequests();
                return;
            }

            using var join = JoinMarker.Auto();
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            while (jobRunning) {
                ServeMainRequests();
                wakeMain.WaitOne(2);
            }

            ServeMainRequests();
            JoinWaitTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            if (jobError != null) {
                Debug.LogError("[ClassicFx] render job failed, its frame is dropped: " + jobError);
                jobError = null;
                jobResultsReady = false;
            }
        }

        /// <summary>
        /// A public entry point touching the engine: a running job finishes first (counted: it should be rare), then the
        /// calls deferred meanwhile run, so this call sees them (call order kept).
        /// </summary>
        void WaitForRenderJob([System.Runtime.CompilerServices.CallerMemberName] string caller = "") {
            if (!OnMainThread) {
                return;
            }

            if (jobRunning) {
                CallerJoins++;
                JoinsByCaller.TryGetValue(caller, out int n);
                JoinsByCaller[caller] = n + 1;
                JoinRenderJob();
            }

            RunDeferredCalls();
        }

        /// <summary>
        /// !toggle fxdefer: while the render job runs, effect launches, numbers / messages and arrows are queued instead of
        /// waiting for it (a skill spam made ~300 joins in 400 frames); they run in call order at the bridge's LateUpdate,
        /// or earlier when another call needs the engine. A deferred launch gets its handle at once and counts as alive.
        /// </summary>
        public static bool DeferLaunches = true;

        /// <summary>Diagnostics: calls deferred while a job ran.</summary>
        public static long DeferredCalls;

        /// <summary>Diagnostics (!fxbridge): the bridge methods that waited for a running job, and how often.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, int> JoinsByCaller = new System.Collections.Generic.Dictionary<string, int>();

        readonly System.Collections.Generic.List<Action> deferredCalls = new System.Collections.Generic.List<Action>();
        readonly System.Collections.Generic.List<Action> runningCalls = new System.Collections.Generic.List<Action>();
        readonly System.Collections.Generic.HashSet<uint> deferredHandles = new System.Collections.Generic.HashSet<uint>();
        bool runningDeferred;

        bool ShouldDefer => DeferLaunches && jobRunning && !runningDeferred && OnMainThread;

        void Defer(Action call, [System.Runtime.CompilerServices.CallerMemberName] string caller = "") {
            deferredCalls.Add(call);
            DeferredCalls++;
            DeferredByCaller.TryGetValue(caller, out int c);
            DeferredByCaller[caller] = c + 1;
        }

        /// <summary>Diagnostics (!fxbridge): the bridge methods whose calls were queued, and how often.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, int> DeferredByCaller = new System.Collections.Generic.Dictionary<string, int>();

        void RunDeferredCalls() {
            if (deferredCalls.Count == 0 || runningDeferred) {
                return;
            }

            runningDeferred = true;
            runningCalls.AddRange(deferredCalls);
            deferredCalls.Clear();
            try {
                for (int i = 0; i < runningCalls.Count; i++) {
                    try {
                        runningCalls[i]();
                    } catch (Exception e) {
                        Debug.LogException(e);
                    }
                }
            } finally {
                runningCalls.Clear();
                runningDeferred = false;
            }
        }

        /// <summary>The running job advances the world first (<see cref="ThreadedLogic"/>).</summary>
        bool jobWithLogic;

        void KickRenderJob(bool withLogic) {
            jobWithLogic = withLogic;
            if (renderThread == null) {
                renderThread = new Thread(RenderThreadLoop) { IsBackground = true, Name = "RO classic effects" };
                renderThread.Start();
            }

            InstallJoinHook();
            jobResultsReady = true;
            jobRunning = true;
            kickSignal.Set();
        }

        void RenderThreadLoop() {
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("Ragnarok", "RO classic effects");
            try {
                while (true) {
                    kickSignal.WaitOne();
                    if (stopRenderThread) {
                        return;
                    }

                    try {
                        using var job = JobMarker.Auto();
                        RenderJob();
                    } catch (Exception e) {
                        jobError = e;
                    } finally {
                        jobRunning = false;
                        wakeMain.Set();
                    }
                }
            } finally {
                UnityEngine.Profiling.Profiler.EndThreadProfiling();
            }
        }

        void StopRenderThread() {
            JoinRenderJob();
            if (renderThread == null) {
                return;
            }

            stopRenderThread = true;
            kickSignal.Set();
            renderThread.Join(1000);
            renderThread = null;
        }

        /// <summary>The frame's start (EarlyUpdate, before any script): the previous frame's job is done before anything else runs.</summary>
        static void InstallJoinHook() {
            if (joinHookInstalled) {
                return;
            }

            joinHookInstalled = true;
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++) {
                if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate)) {
                    continue;
                }

                PlayerLoopSystem early = loop.subSystemList[i];
                var systems = new PlayerLoopSystem[early.subSystemList.Length + 1];
                systems[0] = new PlayerLoopSystem { type = typeof(RoClassicEffectBridge), updateDelegate = JoinAtFrameStart };
                Array.Copy(early.subSystemList, 0, systems, 1, early.subSystemList.Length);
                early.subSystemList = systems;
                loop.subSystemList[i] = early;
                PlayerLoop.SetPlayerLoop(loop);
                return;
            }
        }

        /// <summary>
        /// !toggle fxjoin: the frame's start waits for the previous frame's job (on: the job's slack is only the end of
        /// the previous frame). Off (default): the frame's start only serves the job's main-thread requests (a first
        /// texture load); the job keeps running through Update, and whoever needs the engine waits for it: every public
        /// bridge call (<see cref="WaitForRenderJob"/>, counted in <see cref="CallerJoins"/>) and the bridge's own
        /// LateUpdate. Nothing else reads the engine's state between the two.
        /// </summary>
        public static bool JoinAtFrameStartEnabled;

        static void JoinAtFrameStart() {
            RoClassicEffectBridge bridge = Instance;
            if (bridge == null) {
                return;
            }

            if (JoinAtFrameStartEnabled && bridge.jobRunning) {
                bridge.JoinRenderJob();
            } else {
                bridge.ServeMainRequests();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetJoinHook() => joinHookInstalled = false;
    }
}
