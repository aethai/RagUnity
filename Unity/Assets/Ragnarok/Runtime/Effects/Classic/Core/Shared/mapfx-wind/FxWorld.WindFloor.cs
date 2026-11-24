// Synced from reference/port/src/RoClassicFx/Shared/mapfx-wind/FxWorld.WindFloor.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The map's wind streaks (EF 2343, CWindEffect) and magic floors (EF 1025, CMagicFloorEffect): their EffectTool
    /// readers and setters. Both are launched by <see cref="LoadMapEffects"/> (new-system records) and configured by
    /// <see cref="ApplyEffectTool"/> (version 2 scripts). Package mapfx-wind, see packages/mapfx-wind/NOTES.md.
    /// </summary>
    public sealed partial class FxWorld
    {
        /// <summary>SetWindEffectInfo / SetMagicFloorInfo message ids (effecttool\effectmsgid: MSG_SET_*).</summary>
        public static class WindFloorMsg
        {
            public const int Init = 0, Pos = 4, Color = 6, Size = 8, Texture = 10, Speed = 11, SrcMode = 12, DestMode = 13,
                Area = 16, Angle = 17, RiseAngle = 18, Alpha = 19, Fda = 20, Height0 = 21, Height20 = 41, Size2 = 42,
                RotateAngle = 46, Number = 53;
        }

        /// <summary>Wind effects (EF 2343) created so far on this map, in creation order (= RSW record order): the indices of <see cref="SetWindEffectInfo"/>.</summary>
        public int MapWindCount => Rt.EffectCountById(FxRuntime.WindEffectId);

        /// <summary>Magic floors (EF 1025) created so far on this map, in creation order: the indices of <see cref="SetMagicFloorInfo"/>.</summary>
        public int MapMagicFloorCount => Rt.EffectCountById(FxRuntime.MagicFloorEffectId);

        /// <summary>
        /// The Lua C function <c>SetWindEffectInfo(index, msg, values…)</c> (FUN_00aa8100): <paramref name="args"/> are the
        /// numeric values after the message as lua_tonumber returns them. Writes the index-th 2343 effect of the map;
        /// false when there is none (or it retired). ReadWindEffectInfoz sends, per entry: POS, NUMBER, COLOR, SIZE,
        /// RISEANGLE, HEIGHT0, SPEED, FDA, ROTATE_ANGLE, SRCMODE, DESTMODE, TEXTURE, INIT (INIT rebuilds the streaks: rand()).
        /// </summary>
        public bool SetWindEffectInfo(int index, int msg, params double[] args)
            => Usable(FxRuntime.WindEffectId, index) && Rt.SetWindEffectInfo_aa8100(Stack(index, msg, args));

        /// <summary><see cref="SetWindEffectInfo(int, int, double[])"/> with the texture name (MSG_SET_TEXTURE, a byte string).</summary>
        public bool SetWindEffectInfo(int index, int msg, string text)
            => Usable(FxRuntime.WindEffectId, index) && Rt.SetWindEffectInfo_aa8100(new double[] { index, msg }, text);

        /// <summary>
        /// The Lua C function <c>SetMagicFloorInfo(index, msg, value)</c> (FUN_00aa7db0). ReadMagicFloorInfoz sends, per
        /// entry: AREA (the entry's <c>Speed</c>), SIZE2, ANGLE, RISEANGLE, ALPHA, FDA, HEIGHT0..20, INIT.
        /// </summary>
        public bool SetMagicFloorInfo(int index, int msg, params double[] args)
            => Usable(FxRuntime.MagicFloorEffectId, index) && Rt.SetMagicFloorInfo_aa7db0(Stack(index, msg, args));

        static double[] Stack(int index, int msg, double[] args)
        {
            var stack = new double[2 + (args?.Length ?? 0)];
            stack[0] = index;
            stack[1] = msg;
            if (args != null) args.CopyTo(stack, 2);
            return stack;
        }

        bool Usable(int efId, int index)
        {
            uint e = Rt.EffectByIdIndex_a4de70(efId, (uint)index);
            return e != 0 && Rt.IsNewEffectLive(e);
        }

        static readonly string[] MagicFloorKeys =
        {
            "Speed", "Size", "Angle", "RiseAngle", "Alpha", "FullDisplayAngle",
            "Height0", "Height1", "Height2", "Height3", "Height4", "Height5", "Height6", "Height7", "Height8", "Height9", "Height10",
            "Height11", "Height12", "Height13", "Height14", "Height15", "Height16", "Height17", "Height18", "Height19", "Height20",
        };

        static readonly int[] MagicFloorMsgs =
        {
            WindFloorMsg.Area, WindFloorMsg.Size2, WindFloorMsg.Angle, WindFloorMsg.RiseAngle, WindFloorMsg.Alpha, WindFloorMsg.Fda,
            21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41,
        };

        /// <summary>
        /// effectfunc.lub's ReadMagicFloorInfoz (function 1, decomp/mapfx/effecttool/effectfunc_f1.txt):
        /// <code>
        /// local t = _G[name]
        /// if t ~= nil then for k, v in pairs(t) do
        ///   SetMagicFloorInfo(k, MSG_SET_AREA, v.Speed[1])         -- SIZE2 Size, ANGLE Angle, RISEANGLE RiseAngle,
        ///   …                                                     -- ALPHA Alpha, FDA FullDisplayAngle, HEIGHT0..20 Height0..20
        ///   SetMagicFloorInfo(k, MSG_SET_INIT)
        /// end end
        /// </code>
        /// A missing key aborts the reader at that GETTABLE (lua_pcall per reader), as for the emitter reader.
        /// </summary>
        void ReadMagicFloorInfoz(EffectToolResult r, string table)
        {
            const string fn = "ReadMagicFloorInfoz";
            r.Calls.Add((fn, table));
            var t = r.State.Get(table);
            if (t.IsNil) return;
            try
            {
                if (!t.IsTable) throw new ReadAbort($"bad argument #1 to 'pairs' (table expected, got {TypeName(t)})");
                var k = LuaValue.Nil;
                while (t.Table.Next(ref k, out var v))
                {
                    for (int i = 0; i < MagicFloorKeys.Length; i++)
                        InfoCall(r, "SetMagicFloorInfo", k, MagicFloorMsgs[i], Field(v, MagicFloorKeys[i], 1));
                    InfoCall(r, "SetMagicFloorInfo", k, WindFloorMsg.Init, Array.Empty<LuaValue>());
                }
            }
            catch (ReadAbort e)
            {
                r.Errors.Add((fn, e.Message));
            }
            catch (LuaScriptError e)
            {
                r.Errors.Add((fn, e.Message));
            }
        }

        /// <summary>
        /// effectfunc.lub's ReadWindEffectInfoz (function 7, decomp/mapfx/effecttool/effectfunc_f7.txt):
        /// <code>
        /// local t = _G[name]
        /// if t ~= nil then for k, v in pairs(t) do
        ///   SetWindEffectInfo(k, MSG_SET_POS, v.pos[1], v.pos[2], v.pos[3])
        ///   SetWindEffectInfo(k, MSG_SET_NUMBER, v.particleNum)
        ///   SetWindEffectInfo(k, MSG_SET_COLOR, v.color[1], v.color[2], v.color[3], v.color[4])
        ///   SetWindEffectInfo(k, MSG_SET_SIZE, v.radius)               -- RISEANGLE thickness, HEIGHT0 height, SPEED speed,
        ///   …                                                          -- FDA fullAngle (plain values, not tables)
        ///   SetWindEffectInfo(k, MSG_SET_ROTATE_ANGLE, v.rotateVector[1], v.rotateVector[2])
        ///   SetWindEffectInfo(k, MSG_SET_SRCMODE, v.srcMode)           -- DESTMODE destMode
        ///   SetWindEffectInfo(k, MSG_SET_TEXTURE, v.texture)
        ///   SetWindEffectInfo(k, MSG_SET_INIT)
        /// end end
        /// </code>
        /// Unlike the emitters, the position is sent: the streaks move to the script's <c>pos</c>.
        /// </summary>
        void ReadWindEffectInfoz(EffectToolResult r, string table)
        {
            const string fn = "ReadWindEffectInfoz";
            r.Calls.Add((fn, table));
            var t = r.State.Get(table);
            if (t.IsNil) return;
            try
            {
                if (!t.IsTable) throw new ReadAbort($"bad argument #1 to 'pairs' (table expected, got {TypeName(t)})");
                var k = LuaValue.Nil;
                while (t.Table.Next(ref k, out var v))
                {
                    const string S = "SetWindEffectInfo";
                    LuaValue[] Plain(string key) => new[] { Index(v, LuaValue.Of(key), "local 'v'") };
                    InfoCall(r, S, k, WindFloorMsg.Pos, Field(v, "pos", 3));
                    InfoCall(r, S, k, WindFloorMsg.Number, Plain("particleNum"));
                    InfoCall(r, S, k, WindFloorMsg.Color, Field(v, "color", 4));
                    InfoCall(r, S, k, WindFloorMsg.Size, Plain("radius"));
                    InfoCall(r, S, k, WindFloorMsg.RiseAngle, Plain("thickness"));
                    InfoCall(r, S, k, WindFloorMsg.Height0, Plain("height"));
                    InfoCall(r, S, k, WindFloorMsg.Speed, Plain("speed"));
                    InfoCall(r, S, k, WindFloorMsg.Fda, Plain("fullAngle"));
                    InfoCall(r, S, k, WindFloorMsg.RotateAngle, Field(v, "rotateVector", 2));
                    InfoCall(r, S, k, WindFloorMsg.SrcMode, Plain("srcMode"));
                    InfoCall(r, S, k, WindFloorMsg.DestMode, Plain("destMode"));
                    InfoCall(r, S, k, WindFloorMsg.Texture, Plain("texture"), true);
                    InfoCall(r, S, k, WindFloorMsg.Init, Array.Empty<LuaValue>());
                }
            }
            catch (ReadAbort e)
            {
                r.Errors.Add((fn, e.Message));
            }
            catch (LuaScriptError e)
            {
                r.Errors.Add((fn, e.Message));
            }
        }

        /// <summary>The C function SetMagicFloorInfo / SetWindEffectInfo called from Lua with (k, msg, args…).</summary>
        void InfoCall(EffectToolResult r, string setter, LuaValue k, int msg, LuaValue[] args, bool text = false)
        {
            var c = new EffectInfoCall { Function = setter, Key = k, Msg = msg, Args = new double[args.Length] };
            for (int i = 0; i < args.Length; i++) c.Args[i] = args[i].ToNumber();
            var stack = new double[2 + args.Length];
            stack[0] = k.ToNumber();
            stack[1] = msg;
            Array.Copy(c.Args, 0, stack, 2, c.Args.Length);
            bool wind = setter == "SetWindEffectInfo";
            int efId = wind ? FxRuntime.WindEffectId : FxRuntime.MagicFloorEffectId;
            int index = FxOps.Cvtt(stack[0]);
            if (text)
            {
                c.Text = args[0].ToCString();
                if (c.Text == null)
                {
                    // lua_tostring → NULL; the client builds a std::string from it: an access violation, not a Lua error.
                    // Only when the effect exists — without one FUN_00aa8100 returns before reading it.
                    if (Usable(efId, index))
                        throw new InvalidOperationException($"EffectTool entry {k}: texture is a {TypeName(args[0])} — the client crashes in SetWindEffectInfo (std::string from NULL)");
                    r.InfoCalls.Add(c);
                    return;
                }
            }
            c.Applied = Usable(efId, index) && (wind ? Rt.SetWindEffectInfo_aa8100(stack, c.Text) : Rt.SetMagicFloorInfo_aa7db0(stack));
            r.InfoCalls.Add(c);
        }
    }
}
