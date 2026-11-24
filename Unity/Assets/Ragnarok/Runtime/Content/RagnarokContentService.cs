using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Maps;
using Unity.Profiling;

namespace Ragnarok.Content {
    public sealed class RagnarokContentService : IDisposable {
        private readonly RoAssetDatabase assets = new RoAssetDatabase();
        private readonly object initializeSync = new object();
        private Task initializeTask;
        private RagnarokContentInitializationState initializationState = RagnarokContentInitializationState.NotStarted;
        private Exception initializationException;

        private static readonly ProfilerMarker ContentPreloadMarker = new ProfilerMarker("Content.Preload");

        public bool IsInitialized => assets.IsInitialized;
        public bool ContentReady => assets.IsInitialized && InitializationState == RagnarokContentInitializationState.Ready;
        public RoAssetDatabase Assets => assets;
        public RagnarokContentInitializationState InitializationState => initializationState;
        public Exception InitializationException => initializationException;

        public void Initialize(RagnarokContentSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            Initialize(settings.RootPath, settings.GrfFiles);
        }

        public Task InitializeAsync(RagnarokContentSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            return InitializeAsync(settings.RootPath, settings.GrfFiles);
        }

        public Task BeginInitializeForStartup(RagnarokContentSettings settings) {
            return InitializeAsync(settings);
        }

        public void Initialize(RagnarokContentProfile profile) {
            if (profile == null) {
                throw new ArgumentNullException(nameof(profile));
            }

            Initialize(profile.RootPath, profile.GrfFiles);
        }

        public Task InitializeAsync(RagnarokContentProfile profile) {
            if (profile == null) {
                throw new ArgumentNullException(nameof(profile));
            }

            return InitializeAsync(profile.RootPath, profile.GrfFiles);
        }

        public Task BeginInitializeForStartup(RagnarokContentProfile profile) {
            return InitializeAsync(profile);
        }

        public Task PreloadAsync(RagnarokContentSettings settings) {
            return InitializeAsync(settings);
        }

        public Task PreloadAsync(RagnarokContentProfile profile) {
            return InitializeAsync(profile);
        }

        public Task PreloadAsync(string rootPath, IReadOnlyList<string> grfFiles) {
            return InitializeAsync(rootPath, grfFiles);
        }

        public Task BeginInitializeForStartup(string rootPath, IReadOnlyList<string> grfFiles) {
            return InitializeAsync(rootPath, grfFiles);
        }

        public bool TryInitializeSavedProfile(out string report) {
            if (!RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out report)) {
                return false;
            }

            try {
                Initialize(profile);
                return true;
            } catch (Exception ex) {
                report = $"Could not initialize saved RO content profile: {ex.Message}";
                return false;
            }
        }

        public bool BeginInitializeSavedProfileForStartup(out string report) {
            if (!RagnarokContentProfileStore.TryLoad(out RagnarokContentProfile profile, out report)) {
                return false;
            }

            try {
                BeginInitializeForStartup(profile);
                return true;
            } catch (Exception ex) {
                report = $"Could not start RO content profile initialization: {ex.Message}";
                SetFailed(ex);
                return false;
            }
        }

        public void Initialize(string rootPath, IReadOnlyList<string> grfFiles) {
            ValidateContentConfig(rootPath, grfFiles);

            lock (initializeSync) {
                initializeTask = null;
                RoRuntimeMapAssetCache.Clear(assets);
                string cacheRootPath = RoAssetDiskCache.DefaultRootPath;
                SetStartupState(rootPath, grfFiles, cacheRootPath);
                try {
                    InitializeCore(rootPath, grfFiles, MarkGrfIndexRebuildStarted, cacheRootPath);
                    SetReady();
                } catch (Exception ex) {
                    SetFailed(ex);
                    throw;
                }
            }
        }

        public Task InitializeAsync(string rootPath, IReadOnlyList<string> grfFiles) {
            ValidateContentConfig(rootPath, grfFiles);

            lock (initializeSync) {
                if (assets.IsInitialized) {
                    SetReady();
                    return Task.CompletedTask;
                }

                if (initializeTask != null && !IsTerminal(initializeTask)) {
                    return initializeTask;
                }

                string cacheRootPath = RoAssetDiskCache.DefaultRootPath;
                RoRuntimeMapAssetCache.Clear(assets);
                SetStartupState(rootPath, grfFiles, cacheRootPath);
                initializeTask = Task.Run(() => {
                    using (ContentPreloadMarker.Auto()) {
                        lock (initializeSync) {
                            try {
                                InitializeCore(rootPath, grfFiles, MarkGrfIndexRebuildStarted, cacheRootPath);
                                SetReady();
                            } catch (Exception ex) {
                                SetFailed(ex);
                                throw;
                            }
                        }
                    }
                });
                return initializeTask;
            }
        }

        private void InitializeCore(
            string rootPath,
            IReadOnlyList<string> grfFiles,
            Action<string> grfIndexRebuildStarted,
            string cacheRootPath) {
            assets.Initialize(rootPath, grfFiles, grfIndexRebuildStarted, cacheRootPath);
        }

        private static void ValidateContentConfig(string rootPath, IReadOnlyList<string> grfFiles) {
            if (string.IsNullOrWhiteSpace(rootPath)) {
                throw new ArgumentException("Content root path is required.", nameof(rootPath));
            }

            if (grfFiles == null || grfFiles.Count == 0) {
                throw new ArgumentException("At least one GRF file is required.", nameof(grfFiles));
            }
        }

        public T Load<T>(string path) where T : class {
            EnsureInitialized();
            return assets.Load<T>(path);
        }

        public void Dispose() {
            Task taskToWait = null;
            lock (initializeSync) {
                if (initializeTask != null && !IsTerminal(initializeTask)) {
                    taskToWait = initializeTask;
                }
            }

            if (taskToWait != null && taskToWait.Id != Task.CurrentId) {
                try {
                    taskToWait.Wait();
                } catch {
                    // Disposal should still release partially initialized content after a failed preload wait.
                }
            }

            lock (initializeSync) {
                RoRuntimeMapAssetCache.Clear(assets);
                assets.Dispose();
                initializeTask = null;
                initializationState = RagnarokContentInitializationState.NotStarted;
                initializationException = null;
            }
        }

        private static bool IsTerminal(Task task) {
            return task == null || task.IsCompleted || task.IsCanceled || task.IsFaulted;
        }

        private void SetStartupState(string rootPath, IReadOnlyList<string> grfFiles, string cacheRootPath) {
            initializationException = null;
            initializationState = RoAssetDatabase.CanLoadGrfIndexesFromCache(rootPath, grfFiles, cacheRootPath)
                ? RagnarokContentInitializationState.LoadingCachedIndex
                : RagnarokContentInitializationState.RebuildingIndex;
        }

        private void MarkGrfIndexRebuildStarted(string _) {
            initializationState = RagnarokContentInitializationState.RebuildingIndex;
        }

        private void SetReady() {
            initializationException = null;
            initializationState = RagnarokContentInitializationState.Ready;
            Ragnarok.Audio.RoAudioSystem.UseAssets(assets);   // sounds from the login screen on
        }

        private void SetFailed(Exception exception) {
            initializationException = exception;
            initializationState = RagnarokContentInitializationState.Failed;
        }

        private void EnsureInitialized() {
            if (!assets.IsInitialized) {
                throw new InvalidOperationException("Ragnarok content service has not been initialized.");
            }
        }
    }
}
