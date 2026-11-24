using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Diagnostics;
using Ragnarok.Maps;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Audio {
    /// <summary>
    /// A map's sounds: its BGM (through <see cref="RoAudioSystem"/>: the same track keeps playing across maps) and the RSW
    /// ambient sources, played like the client's CSoundMaker (OnProcess 0xcc0490, reference packages/sound §4.3): every
    /// source on its own starts a one-shot every <c>cycle</c> seconds while the local player is near it, from the point of
    /// its w × h box toward the player, through the shared voice pool with PlaySound3D's rule (max = range, min =
    /// range / 6, factor = the RSW volume). A running sound is never stopped. Sources with width 0 are silent, as in the
    /// client. The client's edge point jumps away when the player is south of a source (two clamps), which silences those
    /// sources from that side; that bug is not reproduced here.
    /// </summary>
    public sealed class RoMapAudioPlayer : MonoBehaviour {
        sealed class Source {
            /// <summary>World position (Unity units) of the source's centre.</summary>
            public Vector3 Position;
            public float Range;
            public float Volume;
            public float CycleSeconds;
            public float Width;
            public float Height;
            public float Diagonal;
            public string File;
            public float LastStart = float.NegativeInfinity;
        }

        readonly List<Source> sources = new List<Source>();
        RoAssetDatabase assets;
        UnityEngine.Object logContext;

        public void Initialize(
            AsyncMapLoader.GameMapData mapData,
            RoAssetDatabase assetDatabase,
            RoAudioSettings audioSettings,
            bool enableBgm,
            bool enableAmbient,
            int maxAmbientSoundEmitters,
            UnityEngine.Object context = null
        ) {
            assets = assetDatabase;
            logContext = context != null ? context : this;
            RoAudioSystem.UseAssets(assetDatabase);
            if (enableBgm) {
                StartMapBgm(mapData.Name);
            }

            if (enableAmbient) {
                CollectSources(mapData.World, mapData.CompiledGround);
            }
        }

        void StartMapBgm(string mapName) {
            if (!RoMapBgmResolver.TryResolve(mapName, assets.Tables, out string mp3File, out string tableKey)) {
                Debug.LogWarning($"RO BGM: no mp3nametable entry found for map '{mapName}'.", logContext);
                return;
            }

            foreach (string candidate in RoMapBgmResolver.BuildBgmPathCandidates(mp3File)) {
                if (assets.TryResolveLooseFilePath(candidate, out string fullPath)) {
                    RoAudioSystem.PlayBgm(fullPath);
                    if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                        RoRuntimeDiagnostics.IncrementDebugLogLine();
                        Debug.Log($"RO BGM resolved '{mapName}' via table key '{tableKey}' -> {candidate}.", logContext);
                    }

                    return;
                }
            }

            Debug.LogWarning($"RO BGM '{mp3File}' for map '{mapName}' (table key '{tableKey}') has no loose file in BGM/.", logContext);
        }

        void CollectSources(RSW world, GND.Mesh compiledGround) {
            sources.Clear();
            if (world?.sounds == null) {
                return;
            }

            Vector3 modelRoot = RoMapCoordinateSystem.ModelRootLocalPosition(compiledGround);
            foreach (RSW.Sound sound in world.sounds) {
                if (sound == null || string.IsNullOrWhiteSpace(sound.file) || sound.width == 0) {
                    continue;
                }

                sources.Add(new Source {
                    Position = transform.TransformPoint(modelRoot + ToRoLocalPosition(sound.pos)),
                    Range = sound.range,
                    Volume = sound.vol,
                    CycleSeconds = sound.cycle,
                    Width = sound.width,
                    Height = sound.height,
                    Diagonal = Mathf.Sqrt(sound.width * sound.width + sound.height * sound.height),
                    File = sound.file
                });
            }
        }

        void Update() {
            Transform player = RoAudioSystem.ListenerAnchor;
            // the map behind the pre-game screens (login, character select) stays silent
            if (sources.Count == 0 || player == null || Ragnarok.UI.RoClassicPreGame.CoversWorld) {
                return;
            }

            float now = Time.unscaledTime;
            Vector3 p = player.position;
            const float ro = RoAudioSystem.RoUnitsPerUnity;
            foreach (Source source in sources) {
                // RO units, relative to the source (x east, z the map's other axis)
                float dx = (p.x - source.Position.x) * ro, dz = (p.z - source.Position.z) * ro;
                float centre = Mathf.Sqrt(dx * dx + dz * dz);
                if (centre - (source.Diagonal + source.Range) > 130f) {
                    continue;   // the client's cheap pre-gate
                }

                // the point of the w × h box on the way from its centre to the player (the client's intent)
                float ex, ez;
                if (Mathf.Abs(dz) * source.Width > Mathf.Abs(dx) * source.Height) {
                    ez = Mathf.Sign(dz) * source.Height * 0.5f;
                    ex = dz != 0f ? ez * dx / dz : 0f;
                } else {
                    ex = Mathf.Sign(dx) * source.Width * 0.5f;
                    ez = dx != 0f ? ex * dz / dx : 0f;
                }

                float offsetX = ex - dx, offsetZ = ez - dz;   // edge point relative to the player
                if (Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ) - source.Range > 75f) {
                    continue;
                }

                if (!(now > source.LastStart + source.CycleSeconds)) {
                    continue;
                }

                source.LastStart = now;
                RoAudioEvents.PlayEffectRelative(source.File, offsetX, 0f, offsetZ, (int) source.Range, (int) (source.Range / 6f), source.Volume);
            }
        }

        static Vector3 ToRoLocalPosition(float[] position) {
            if (position == null || position.Length < 3) {
                return Vector3.zero;
            }

            return new Vector3(position[0], -position[1], position[2]);
        }
    }
}
