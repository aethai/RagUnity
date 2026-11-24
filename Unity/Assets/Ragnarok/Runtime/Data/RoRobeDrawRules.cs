using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoonSharp.Interpreter;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.Data {
    /// <summary>
    /// Whether the client draws a robe (garment) before or after the body, and on top of the headgear
    /// (reference packages/draw-order §1.3): 0x5e6770 runs the robe in the pass equal to Lua
    /// <c>_New_DrawOnTop(robe, sex, bodyJob, act, frame)</c> (FUN_00cc5570; <c>DrawOnTop</c> when the new one is missing):
    /// pass 0 before the body, pass 1 right after it; in pass 1 a robe in <c>RobeTopLayer</c> (IsTopLayer) takes key 400,
    /// after the headgear. The tables (spreditinfo/_new_*layerdir_*.lub) list, per robe size, sex, job and ACT action, the
    /// frames drawn behind the body, so the same robe changes sides with the direction and the frame (wings facing away
    /// sit behind a cloak in some frames).
    /// </summary>
    public static class RoRobeDrawRules {
        public enum Pass : byte {
            /// <summary>Drawn before the body (behind it).</summary>
            Behind = 0,
            /// <summary>Drawn right after the body.</summary>
            AfterBody = 1,
            /// <summary>After the body and on top of the headgear (RobeTopLayer, key 400).</summary>
            TopLayer = 2,
        }

        private const string LuaDir = "data/luafiles514/lua files/";
        private static readonly string[] LayerTableFiles = {
            "spreditinfo/_new_biglayerdir_female.lub", "spreditinfo/_new_biglayerdir_male.lub",
            "spreditinfo/_new_smalllayerdir_female.lub", "spreditinfo/_new_smalllayerdir_male.lub",
            "spreditinfo/_new_2dlayerdir_f.lub",
        };

        private static Rules rules;
        private static bool started;

        /// <summary>Starts loading the tables (once, on a worker thread); until they are ready every robe draws after the body.</summary>
        public static void Ensure(RoAssetDatabase assets) {
            if (started || assets == null) {
                return;
            }

            started = true;
            // the asset reads on this thread, the Lua work on a worker
            string jobIdentity = assets.ReadText(LuaDir + "skillinfoz/jobinheritlist.lub");
            string robeIds = assets.ReadText(LuaDir + "datainfo/spriterobeid.lub");
            string robeNames = assets.ReadText(LuaDir + "datainfo/spriterobename.lub");
            // the old text layer file: _New_DrawOnTop calls its GetLayerSizeType and uses its SEX_* / LAYER_* constants
            string layerDir = assets.ReadText(LuaDir + "spreditinfo/2dlayerdir_f.lub");
            var chunks = new List<(string, byte[])>(LayerTableFiles.Length);
            foreach (string file in LayerTableFiles) {
                chunks.Add((file, assets.ReadBytesQuiet(LuaDir + file)));
            }

            Task.Run(() => {
                try {
                    rules = Rules.Build(jobIdentity, robeIds, robeNames, layerDir, chunks);
                } catch (Exception e) {
                    Debug.LogWarning("[robe draw] the robe layer tables did not load, robes draw after the body: " + e.Message);
                }
            });
        }

        /// <summary>
        /// The pass of robe <paramref name="robeId"/> for a <paramref name="isMale"/> body of <paramref name="bodyJob"/> at ACT
        /// action <paramref name="actionIndex"/> (action·8 + direction) frame <paramref name="frameIndex"/>. Main thread.
        /// </summary>
        public static Pass Resolve(int robeId, bool isMale, int bodyJob, int actionIndex, int frameIndex) {
            Rules current = rules;
            return robeId <= 0 || current == null ? Pass.AfterBody : current.Resolve(robeId, isMale, bodyJob, actionIndex, frameIndex);
        }

        /// <summary>For tests and diagnostics: whether the tables are loaded.</summary>
        public static bool IsReady => rules != null;

        private sealed class Rules {
            private Lua51.LuaState lua;
            private Lua51.LuaValue drawOnTop;
            private readonly HashSet<int> topLayer = new HashSet<int>();
            private readonly Dictionary<long, bool> onTopCache = new Dictionary<long, bool>();
            private bool loggedCallFailure;

            public static Rules Build(string jobIdentity, string robeIds, string robeNames, string layerDir, List<(string Name, byte[] Bytes)> chunks) {
                var built = new Rules();
                // the text tables (JOBID, SPRITE_ROBE_IDs, RobeTopLayer) through MoonSharp; the compiled layer tables and
                // functions in the Lua 5.1 VM, with the constants copied in
                var script = new Script(CoreModules.Preset_SoftSandbox);
                script.DoString(jobIdentity ?? throw new InvalidOperationException("jobinheritlist.lub missing"));
                script.DoString(robeIds ?? throw new InvalidOperationException("spriterobeid.lub missing"));
                if (robeNames != null) {
                    script.DoString(robeNames);
                    Table top = script.Globals.Get("RobeTopLayer").Table;
                    if (top != null) {
                        foreach (TablePair pair in top.Pairs) {
                            if (pair.Value.Type == DataType.Number) built.topLayer.Add((int) pair.Value.Number);
                        }
                    }
                }

                built.lua = new Lua51.LuaState { InstructionLimit = 50_000_000 };
                CopyConstants(script, "JOBID", built.lua);
                CopyConstants(script, "SPRITE_ROBE_IDs", built.lua);
                CopyLayerSizeRules(script, layerDir, built.lua);
                foreach ((string name, byte[] bytes) in chunks) {
                    if (bytes == null) throw new InvalidOperationException(name + " missing");
                    built.lua.DoChunk(bytes, name);
                }

                built.drawOnTop = built.lua.GetGlobal("_New_DrawOnTop");
                if (!built.drawOnTop.IsFunction) built.drawOnTop = built.lua.GetGlobal("DrawOnTop");
                if (!built.drawOnTop.IsFunction) throw new InvalidOperationException("no DrawOnTop function");
                // a single per-call budget for the lookups below
                built.lua.InstructionLimit = 1_000_000;
                return built;
            }

            /// <summary>The text 2DLayerDir_F part the compiled functions call: its constants and GetLayerSizeType (LayerSizeTypeList, else big).</summary>
            private static void CopyLayerSizeRules(Script script, string layerDir, Lua51.LuaState lua) {
                script.DoString(layerDir ?? throw new InvalidOperationException("2dlayerdir_f.lub missing"));
                foreach (string name in new[] { "SEX_FEMALE", "SEX_MALE", "LAYER_BIG", "LAYER_SMALL" }) {
                    DynValue value = script.Globals.Get(name);
                    if (value.Type == DataType.Number) lua.SetGlobal(name, Lua51.LuaValue.Of(value.Number));
                }

                DynValue bigValue = script.Globals.Get("LAYER_BIG");
                double big = bigValue.Type == DataType.Number ? bigValue.Number : 0;
                var sizes = new Dictionary<int, double>();
                Table list = script.Globals.Get("LayerSizeTypeList").Table;
                if (list != null) {
                    foreach (TablePair pair in list.Pairs) {
                        if (pair.Key.Type == DataType.Number && pair.Value.Type == DataType.Number) sizes[(int) pair.Key.Number] = pair.Value.Number;
                    }
                }

                lua.Register("GetLayerSizeType", a => a[0].TryGetNumber(out double robe) && sizes.TryGetValue((int) robe, out double size) ? size : big);
            }

            private static void CopyConstants(Script script, string tableName, Lua51.LuaState lua) {
                Table source = script.Globals.Get(tableName).Table;
                var target = new Lua51.LuaTable(0, source != null ? source.Length + 64 : 0);
                if (source != null) {
                    foreach (TablePair pair in source.Pairs) {
                        if (pair.Key.Type == DataType.String && pair.Value.Type == DataType.Number) {
                            target.Set(pair.Key.String, Lua51.LuaValue.Of(pair.Value.Number));
                        }
                    }
                }

                lua.SetGlobal(tableName, Lua51.LuaValue.Of(target));
            }

            public Pass Resolve(int robeId, bool isMale, int bodyJob, int actionIndex, int frameIndex) {
                long key = ((long) (robeId & 0xffff) << 44) | ((long) (bodyJob & 0xffff) << 28) | ((long) (actionIndex & 0x3ff) << 18) |
                    ((long) (frameIndex & 0xffff) << 2) | (isMale ? 1L : 0L);
                if (!onTopCache.TryGetValue(key, out bool onTop)) {
                    try {
                        onTop = lua.Call1(drawOnTop, Lua51.LuaValue.Of(robeId), Lua51.LuaValue.Of(isMale ? 1 : 0), Lua51.LuaValue.Of(bodyJob),
                            Lua51.LuaValue.Of(actionIndex), Lua51.LuaValue.Of(frameIndex)).IsTruthy;
                    } catch (Exception e) {
                        onTop = true;   // the client's default
                        if (!loggedCallFailure) {
                            loggedCallFailure = true;
                            Debug.LogWarning("[robe draw] DrawOnTop(" + robeId + ", " + (isMale ? 1 : 0) + ", " + bodyJob + ", " + actionIndex + ", " +
                                frameIndex + ") failed, robes draw after the body: " + e.Message);
                        }
                    }

                    onTopCache[key] = onTop;
                }

                if (!onTop) return Pass.Behind;
                return topLayer.Contains(robeId) ? Pass.TopLayer : Pass.AfterBody;
            }
        }
    }
}
