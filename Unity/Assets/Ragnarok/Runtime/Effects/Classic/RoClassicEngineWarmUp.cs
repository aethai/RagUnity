using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ragnarok.Effects.Classic {
    /// <summary>
    /// JIT-compiles the classic engine off the main thread. Every effect id used to pay for compiling its code (and the
    /// first one for the engine core) in the frame it first played: 10-50 ms single frames on Mono, more with the
    /// span-based heap access, whose generic instantiations cost more to compile. When the bridge is initialised a
    /// worker thread (below normal priority) runs every ported effect in throw-away worlds, so the real ones start
    /// compiled (a few seconds of one background core). Worlds share no mutable static state (each has its own heap and
    /// RNG), so the warm-up cannot change what the real world does.
    /// The worker cannot read the GRF, so sprite effects get a synthetic SPR/ACT pair and STR effects a STR file read
    /// beforehand on the main thread: without them an effect faults like the client does on a missing .act, and one
    /// fault used to stop the whole world it shared with other effects (most of the engine stayed cold).
    /// </summary>
    static class RoClassicEngineWarmUp {
        /// <summary>STR file read on the main thread and handed to every STR request of the warm-up.</summary>
        const string StrFile = "ThunderStorm.str";
        const int MaxEffectId = 0x700;
        const int EffectsPerWorld = 24;
        const int TicksPerWorld = 90;
        const uint WorldHeapBytes = 32 * 1024 * 1024;
        static int started;

        /// <summary>True once the worker has run every effect.</summary>
        public static bool Finished => Volatile.Read(ref finished) != 0;
        static int finished;

        /// <param name="readEffectFile">Reader of files under data/texture/effect (called here, on the main thread).</param>
        public static void Start(Func<string, byte[]> readEffectFile) {
            if (Interlocked.Exchange(ref started, 1) != 0) {
                return;
            }

            byte[] str = null;
            try {
                str = readEffectFile?.Invoke(StrFile);
            } catch (Exception) {
                // warm-up only: STR effects then stop early
            }

            var stub = new StubHost(str);
            new Thread(() => Run(stub)) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "RO classic FX warm-up" }.Start();
        }

        static void Run(StubHost stub) {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var ids = new List<int>();
            for (int id = 0; id < MaxEffectId; id++) {
                if (FxWorld.IsPorted(id)) {
                    ids.Add(id);
                }
            }

            // groups share a world; a group that faults is run again one effect per world, so a fault only stops the
            // effect that caused it
            int alone = 0, failed = 0;
            var group = new List<int>(EffectsPerWorld);
            for (int start = 0; start < ids.Count; start += EffectsPerWorld) {
                group.Clear();
                for (int i = start; i < ids.Count && i < start + EffectsPerWorld; i++) {
                    group.Add(ids[i]);
                }

                if (RunWorld(stub, group)) {
                    continue;
                }

                for (int i = 0; i < group.Count; i++) {
                    alone++;
                    if (!RunWorld(stub, group.GetRange(i, 1))) {
                        failed++;
                    }
                }
            }

            Volatile.Write(ref finished, 1);
            UnityEngine.Debug.Log($"RO classic FX warm-up done: {ids.Count} effects ({alone} run alone after a fault, {failed} still fault) in {watch.ElapsedMilliseconds} ms");
        }

        /// <summary>Launches <paramref name="effectIds"/> in a fresh world and runs it out. False when anything faulted.</summary>
        static bool RunWorld(StubHost stub, List<int> effectIds) {
            FxWorld world;
            try {
                world = NewWorld(stub);
            } catch (Exception) {
                return false;
            }

            try {
                for (int i = 0; i < effectIds.Count; i++) {
                    uint owner = world.CreateOwner(10f + i * 7f, 0f, 20f);
                    if (FxWorld.IsNewSystem(effectIds[i])) {
                        world.LaunchNew(effectIds[i], owner);
                    } else {
                        world.Launch(effectIds[i], owner);
                    }
                }

                for (int t = 0; t < TicksPerWorld && (world.EffectCount > 0 || world.PrimCount > 0 || world.NewEffectCount > 0); t++) {
                    world.Tick();
                    world.Render();
                }

                return true;
            } catch (Exception) {
                return false;
            } finally {
                world.Rt.M.Dispose();
            }
        }

        /// <summary>A fixed 32 MB heap: a managed array the GC accounts for, released when the world is done.</summary>
        static FxWorld NewWorld(StubHost stub) {
            var runtime = new FxRuntime(new FxMemory(WorldHeapBytes)) { Host = stub };
            runtime.Renderer.XFactor = runtime.Renderer.YFactor = 551.5f;
            runtime.Renderer.CenterX = runtime.Renderer.CenterX2 = 400;
            runtime.Renderer.CenterY = runtime.Renderer.CenterY2 = 300;
            runtime.Renderer.InitQuadPool(8);
            runtime.Renderer.FaceSubmitted = (face, flags) => { };
            runtime.Renderer.QuadSubmitted = (quad, flags) => { };
            runtime.Renderer.SpriteSubmitted = sprite => { };
            runtime.LoadDataFile = stub.LoadDataFile;
            // the sprite-particle renderer (EF 44 chimney smoke) draws only with a texture handle
            runtime.SprTextureHandleImpl = (img, palette) => 1u;
            float[] view = { 1f, 0f, 0f, 0f, 0.8f, 0.6f, 0f, -0.6f, 0.8f, 0f, 0f, 200f };
            for (int k = 0; k < view.Length; k++) {
                runtime.M.F32(runtime.View + 0x98 + (uint) k * 4, view[k]);
            }

            return new FxWorld(runtime, stub);
        }

        /// <summary>Textures resolve to placeholder handles, sounds to nothing, sprites to a synthetic SPR/ACT pair and
        /// STR files to the one read on the main thread (no Unity API, no GRF off the main thread).</summary>
        sealed class StubHost : IFxHost, IFxResourceProvider {
            static readonly byte[] Spr = SyntheticSpr();
            static readonly byte[] Act = SyntheticAct();
            readonly byte[] str;

            public StubHost(byte[] str) {
                this.str = str;
            }

            public bool TryGetTexture(string name, out uint handle, out int imageWidth, out int imageHeight, out int surfaceWidth, out int surfaceHeight) {
                handle = 1;
                imageWidth = imageHeight = surfaceWidth = surfaceHeight = 64;
                return true;
            }

            public void PlaySound3D(string wav, float x, float y, float z, int maxDist, int minDist, float factor, int loop) {
            }

            public byte[] LoadEffectFile(string strName) => str;

            public byte[] LoadDataFile(string path) =>
                path == null ? null
                : path.EndsWith(".act", StringComparison.OrdinalIgnoreCase) ? Act
                : path.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) ? Spr
                : path.EndsWith(".str", StringComparison.OrdinalIgnoreCase) ? str
                : null;

            /// <summary>SPR 2.1: one 2x2 palette image (RLE), one 2x2 RGBA image, a palette.</summary>
            static byte[] SyntheticSpr() {
                using var stream = new MemoryStream();
                using var w = new BinaryWriter(stream);
                w.Write((byte) 'S'); w.Write((byte) 'P'); w.Write((ushort) 0x201);
                w.Write((ushort) 1); w.Write((ushort) 1);
                w.Write((ushort) 2); w.Write((ushort) 2); w.Write((ushort) 4); w.Write(new byte[] { 1, 1, 1, 1 });
                w.Write((ushort) 2); w.Write((ushort) 2);
                for (int i = 0; i < 4; i++) w.Write(0xff808080u);
                for (int i = 0; i < 256; i++) w.Write(0x00808080u);
                return stream.ToArray();
            }

            /// <summary>ACT 2.4: 104 actions (every action index the effects ask for) of 4 motions with one clip each,
            /// clips resolved through the SPR like real ones.</summary>
            static byte[] SyntheticAct() {
                const int Actions = 104, Motions = 4;
                using var stream = new MemoryStream();
                using var w = new BinaryWriter(stream);
                w.Write((byte) 'A'); w.Write((byte) 'C'); w.Write((ushort) 0x204); w.Write((ushort) Actions);
                w.Write(new byte[10]);
                for (int a = 0; a < Actions; a++) {
                    w.Write(Motions);
                    for (int f = 0; f < Motions; f++) {
                        for (int k = 0; k < 8; k++) w.Write(0);   // range1, range2
                        w.Write(1);                                // one clip
                        w.Write(0); w.Write(0); w.Write(0); w.Write(0);   // x, y, sprite 0, no mirror
                        w.Write(0xffffffffu);                             // colour
                        w.Write(1f); w.Write(1f);                         // zoom x, y
                        w.Write(0); w.Write(0);                           // angle, palette image
                        w.Write(-1);                                      // no event
                        w.Write(0);                                       // no attach points
                    }
                }

                w.Write(0);   // no events
                for (int a = 0; a < Actions; a++) w.Write(4f);
                return stream.ToArray();
            }
        }
    }
}
