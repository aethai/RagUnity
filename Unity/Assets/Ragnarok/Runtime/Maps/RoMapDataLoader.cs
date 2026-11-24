using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ragnarok.Assets;
using ROIO;
using ROIO.Loaders;
using ROIO.Models.FileTypes;

namespace Ragnarok.Maps {
    public sealed class RoMapDataLoader {
        private static readonly SemaphoreSlim LoadLock = new SemaphoreSlim(1, 1);
        private readonly RoAssetDatabase assets;

        public RoMapDataLoader(RoAssetDatabase assets) {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        public Task<AsyncMapLoader.GameMapData> Load(string mapName) {
            return Load(mapName, null);
        }

        internal async Task<AsyncMapLoader.GameMapData> Load(string mapName, RoMapDataLoadTiming timing) {
            long waitStart = RoMapTiming.Timestamp();
            await LoadLock.WaitAsync();
            timing ??= new RoMapDataLoadTiming();
            timing.WaitForLoadLockMilliseconds = RoMapTiming.ElapsedMilliseconds(waitStart);
            long totalStart = RoMapTiming.Timestamp();
            try {
                RSW world = await LoadWorld(mapName, timing);
                Task<GAT> altitudeTask = LoadAltitude(mapName, world, timing);
                Task<GND> groundTask = LoadGround(mapName, world, timing);
                Task<RSM.CompiledModel[]> compiledModelsTask = LoadCompiledModels(world.modelDescriptors, timing);

                GND ground = await groundTask;
                Task<GND.Mesh> compiledGroundTask = CompileGroundMesh(mapName, ground, world, timing);
                GAT altitude = await altitudeTask;
                GND.Mesh compiledGround = await compiledGroundTask;
                RSM.CompiledModel[] compiledModels = await compiledModelsTask;
                RSM[] models = BuildModelViews(compiledModels);
                timing.TotalMilliseconds = RoMapTiming.ElapsedMilliseconds(totalStart);

                return new AsyncMapLoader.GameMapData {
                    Name = mapName,
                    World = world,
                    Ground = ground,
                    Altitude = altitude,
                    Models = models,
                    CompiledModels = compiledModels,
                    CompiledGround = compiledGround
                };
            } finally {
                if (timing.TotalMilliseconds == 0) {
                    timing.TotalMilliseconds = RoMapTiming.ElapsedMilliseconds(totalStart);
                }

                LoadLock.Release();
            }
        }

        private async Task<RSW> LoadWorld(string mapName, RoMapDataLoadTiming timing) {
            string path = "data/" + GetFilePath(mapName);
            long start = RoMapTiming.Timestamp();
            RSW world = await Task.Run(() => RoRuntimeMapAssetCache.GetOrLoadMapAsset(
                assets,
                path,
                () => assets.Load<RSW>(path)));
            timing.WorldMilliseconds = RoMapTiming.ElapsedMilliseconds(start);
            if (world == null) {
                throw new Exception("Could not load rsw for " + mapName);
            }

            return world;
        }

        private async Task<GAT> LoadAltitude(string mapName, RSW world, RoMapDataLoadTiming timing) {
            string path = "data/" + GetFilePath(world.files.gat);
            long start = RoMapTiming.Timestamp();
            GAT altitude = await Task.Run(() => RoRuntimeMapAssetCache.GetOrLoadMapAsset(
                assets,
                path,
                () => assets.Load<GAT>(path)));
            timing.AltitudeMilliseconds = RoMapTiming.ElapsedMilliseconds(start);
            if (altitude == null) {
                throw new Exception("Could not load gat for " + mapName);
            }

            return altitude;
        }

        private async Task<GND> LoadGround(string mapName, RSW world, RoMapDataLoadTiming timing) {
            string path = "data/" + GetFilePath(world.files.gnd);
            long start = RoMapTiming.Timestamp();
            GND ground = await Task.Run(() => RoRuntimeMapAssetCache.GetOrLoadMapAsset(
                assets,
                path,
                () => assets.Load<GND>(path)));
            timing.GroundMilliseconds = RoMapTiming.ElapsedMilliseconds(start);
            if (ground == null) {
                throw new Exception("Could not load gnd for " + mapName);
            }

            return ground;
        }

        private async Task<GND.Mesh> CompileGroundMesh(string mapName, GND ground, RSW world, RoMapDataLoadTiming timing) {
            string cacheKey = "data/" + GetFilePath(mapName);
            long start = RoMapTiming.Timestamp();
            GND.Mesh compiledGround = await Task.Run(() => RoRuntimeMapAssetCache.GetOrBuildCompiledGround(
                assets,
                cacheKey,
                () => {
                    TryApplyGroundWaterSettings(ground, world);
                    GND.Mesh compiledGround = GroundLoader.Compile(ground, world.water.level, world.water.waveHeight);
                    PrepareTexturePaths(world, compiledGround);
                    return compiledGround;
                }));
            timing.CompileGroundMilliseconds = RoMapTiming.ElapsedMilliseconds(start);
            return compiledGround;
        }

        public static bool TryApplyGroundWaterSettings(GND ground, RSW world) {
            if (ground?.water == null || !ground.water.isDefined || world == null) {
                return false;
            }

            world.water.level = ground.water.level;
            world.water.type = Math.Max(0, ground.water.type);
            world.water.waveHeight = ground.water.waveHeight;
            world.water.waveSpeed = ground.water.waveSpeed;
            world.water.wavePitch = ground.water.wavePitch;
            world.water.animSpeed = ground.water.animSpeed;
            return true;
        }

        private async Task<RSM.CompiledModel[]> LoadCompiledModels(List<RSW.ModelDescriptor> modelDescriptors, RoMapDataLoadTiming timing) {
            long specsStart = RoMapTiming.Timestamp();
            List<ModelLoadSpec> specs = BuildModelLoadSpecs(modelDescriptors);
            timing.ModelDescriptorCount = modelDescriptors?.Count ?? 0;
            timing.UniqueModelCount = specs.Count;
            timing.ModelSpecsMilliseconds = RoMapTiming.ElapsedMilliseconds(specsStart);
            long start = RoMapTiming.Timestamp();
            var tasks = new List<Task<RSM.CompiledModel>>(specs.Count);
            for (int i = 0; i < specs.Count; i++) {
                ModelLoadSpec spec = specs[i];
                tasks.Add(Task.Run(() => RoRuntimeMapAssetCache.GetOrCompileModelView(
                    assets,
                    spec.Path,
                    spec.Filename,
                    spec.Instances,
                    () => assets.Load<RSM>(spec.Path))));
            }

            RSM.CompiledModel[] loaded = await Task.WhenAll(tasks);
            var compiledModels = new List<RSM.CompiledModel>(loaded.Length);
            for (int i = 0; i < loaded.Length; i++) {
                if (loaded[i] != null) {
                    compiledModels.Add(loaded[i]);
                }
            }

            RSM.CompiledModel[] result = compiledModels.ToArray();
            timing.LoadedModelCount = result.Length;
            timing.ModelsMilliseconds = RoMapTiming.ElapsedMilliseconds(start);
            return result;
        }

        private static RSM[] BuildModelViews(RSM.CompiledModel[] compiledModels) {
            if (compiledModels == null || compiledModels.Length == 0) {
                return Array.Empty<RSM>();
            }

            var models = new RSM[compiledModels.Length];
            for (int i = 0; i < compiledModels.Length; i++) {
                models[i] = compiledModels[i].rsm;
            }

            return models;
        }

        private static List<ModelLoadSpec> BuildModelLoadSpecs(List<RSW.ModelDescriptor> modelDescriptors) {
            var specsByPath = new Dictionary<string, ModelLoadSpec>(StringComparer.OrdinalIgnoreCase);
            var orderedSpecs = new List<ModelLoadSpec>();
            if (modelDescriptors == null) {
                return orderedSpecs;
            }

            for (int i = 0; i < modelDescriptors.Count; i++) {
                RSW.ModelDescriptor descriptor = modelDescriptors[i];
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.filename)) {
                    continue;
                }

                string filename = descriptor.filename.Trim().Replace('\\', '/');
                string path = "data/model/" + filename;
                if (!specsByPath.TryGetValue(path, out ModelLoadSpec spec)) {
                    spec = new ModelLoadSpec(path, filename);
                    specsByPath.Add(path, spec);
                    orderedSpecs.Add(spec);
                }

                spec.Instances.Add(descriptor);
            }

            return orderedSpecs;
        }

        private static void PrepareTexturePaths(RSW world, GND.Mesh ground) {
            var texturePaths = new LinkedList<string>();

            if (ground.waterVertCount > 0) {
                string path = "data/texture/\xbf\xf6\xc5\xcd/water" + world.water.type;
                for (int i = 0; i < 32; i++) {
                    string num = i < 10 ? "0" + i : i.ToString();
                    texturePaths.AddLast(path + num + ".jpg");
                }
            }

            for (int i = 0; i < ground.textures.Length; i++) {
                texturePaths.AddLast(BuildGroundTexturePath(ground.textures[i]));
            }

            var waterTextures = new List<string>();
            if (ground.waterVertCount > 0) {
                for (int i = 0; i < 32 && texturePaths.Count > 0; i++) {
                    waterTextures.Add(texturePaths.First.Value);
                    texturePaths.RemoveFirst();
                }
            }

            waterTextures.CopyTo(world.water.images);
            ground.textures = new string[texturePaths.Count];
            texturePaths.CopyTo(ground.textures, 0);
        }

        private string GetFilePath(string path) {
            return assets.Tables.TryResolveResourceName(path, out string resolvedPath)
                ? resolvedPath
                : path;
        }

        private static string BuildGroundTexturePath(string texturePath) {
            if (string.IsNullOrWhiteSpace(texturePath)) {
                return texturePath;
            }

            texturePath = texturePath.Trim().Replace('\\', '/');
            return texturePath.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                ? texturePath
                : "data/texture/" + texturePath;
        }

        private sealed class ModelLoadSpec {
            public ModelLoadSpec(string path, string filename) {
                Path = path;
                Filename = filename;
                Instances = new List<RSW.ModelDescriptor>();
            }

            public string Path { get; }
            public string Filename { get; }
            public List<RSW.ModelDescriptor> Instances { get; }
        }
    }
}
