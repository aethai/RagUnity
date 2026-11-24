// Synced from reference/port/src/RoClassicFx/Shared/mapfx-emitters/FxWorld.EffectTool.Emitters.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>One call of an EffectTool v2 reader to a Lua C function other than SetEmitterInfo: <c>SetEvilsPawInfo</c> / <c>SetAnimatedEmitterInfo</c> / <c>SetEZ2STRInfo</c> (package mapfx-emitters), <c>SetMagicFloorInfo</c> / <c>SetWindEffectInfo</c> (package mapfx-wind).</summary>
    public sealed class EffectInfoCall
    {
        /// <summary>The C function, e.g. "SetEvilsPawInfo" or "SetWindEffectInfo".</summary>
        public string Function;
        /// <summary>The table key as Lua passes it (lua_tonumber → the index; a string key that is not a number is index 0).</summary>
        public LuaValue Key;
        public int Msg;
        /// <summary>lua_tonumber of the stack slots after MSG (nil → 0), as many as the Lua call passes.</summary>
        public double[] Args;
        /// <summary>lua_tostring of slot 3 for the string messages (texture, STR file name); null otherwise.</summary>
        public string Text;
        /// <summary>An effect of the function's id with that index existed and was written.</summary>
        public bool Applied;

        public override string ToString() => $"{Function}({Key}, {Msg}{(Text != null ? ", \"" + Text + "\"" : "")}{(Args.Length > 0 && Text == null ? ", " + string.Join(", ", Args) : "")}){(Applied ? "" : " (no effect)")}";
    }

    /// <summary>
    /// The EffectTool readers of EF 1039, 1073 and 1412 (effectfunc.lub's ReadEvilsPawInfoz = function 2,
    /// ReadAnimatedEmitterInfoz = 3, ReadEZ2STRInfoz = 6; decomp/mapfx/effecttool/effectfunc_f2/f3/f6.txt) and the host API of
    /// their Lua C functions. Package mapfx-emitters.
    /// </summary>
    public sealed partial class FxWorld
    {
        /// <summary>The MSG ids (effecttool\effectmsgid) the three setters understand beyond <see cref="EmitterMsg"/>.</summary>
        public static class EffectInfoMsg
        {
            public const int Init = 0, Size = 8, Speed = 11, OffsetPos = 44, StrFileName = 47, RenderFlag = 48, ScaleRatio = 49, AlphaRatio = 50;
        }

        /// <summary>
        /// effectfunc.lub function 2, ReadEvilsPawInfoz(name):
        /// <code>
        /// local t = _G[name]
        /// if t ~= nil then for k, v in pairs(t) do
        ///   SetEvilsPawInfo(k, MSG_SET_OFFSET_POS, v.offsetPos[1], v.offsetPos[2], v.offsetPos[3])
        ///   SetEvilsPawInfo(k, MSG_SET_SIZE, v.Size[1])
        ///   SetEvilsPawInfo(k, MSG_SET_SPEED, v.Speed[1])
        ///   SetEvilsPawInfo(k, MSG_SET_INIT)
        /// end end
        /// </code>
        /// A Lua error ends the reader at the GETTABLE that fails (lua_pcall per reader).
        /// </summary>
        void ReadEvilsPawInfoz(EffectToolResult r, string table)
        {
            const string fn = "ReadEvilsPawInfoz", c = "SetEvilsPawInfo";
            ReadInfoTable(r, fn, table, (k, v) =>
            {
                Info(r, c, k, EffectInfoMsg.OffsetPos, Field(v, "offsetPos", 3));
                Info(r, c, k, EffectInfoMsg.Size, Field(v, "Size", 1));
                Info(r, c, k, EffectInfoMsg.Speed, Field(v, "Speed", 1));
                Info(r, c, k, EffectInfoMsg.Init, Array.Empty<LuaValue>());
            });
        }

        /// <summary>
        /// effectfunc.lub function 3, ReadAnimatedEmitterInfoz(name): per entry (pairs order) SetAnimatedEmitterInfo(k, …)
        /// with MAXPARTICLE maxcount[1], SPAWNDIR1 dir1[1..3], SPAWNDIR2 dir2[1..3], GRAVITY gravity[1..3], EMITRADIUS
        /// radius[1..3], COLOR color[1..4], EMITRATE rate[1..3], SIZE size[1..3], LIFETIME life[1..3], TEXTURE texture, SPEED
        /// speed[1], SRCMODE srcmode[1], DESTMODE destmode[1], ZENABLE zenable[1], then only when present BILLBOARD_OFF
        /// billboard_off[1] and ROTATE_ANGLE rotate_angle[1..3] (both ignored by the C function), and INIT. No ETERNITY / SCALE;
        /// <c>pos</c> is never sent.
        /// </summary>
        void ReadAnimatedEmitterInfoz(EffectToolResult r, string table)
        {
            const string fn = "ReadAnimatedEmitterInfoz", c = "SetAnimatedEmitterInfo";
            ReadInfoTable(r, fn, table, (k, v) =>
            {
                Info(r, c, k, EmitterMsg.MaxParticle, Field(v, "maxcount", 1));
                Info(r, c, k, EmitterMsg.SpawnDir1, Field(v, "dir1", 3));
                Info(r, c, k, EmitterMsg.SpawnDir2, Field(v, "dir2", 3));
                Info(r, c, k, EmitterMsg.Gravity, Field(v, "gravity", 3));
                Info(r, c, k, EmitterMsg.EmitRadius, Field(v, "radius", 3));
                Info(r, c, k, EmitterMsg.Color, Field(v, "color", 4));
                Info(r, c, k, EmitterMsg.EmitRate, Field(v, "rate", 3));
                Info(r, c, k, EmitterMsg.Size, Field(v, "size", 3));
                Info(r, c, k, EmitterMsg.LifeTime, Field(v, "life", 3));
                Info(r, c, k, EmitterMsg.Texture, new[] { Index(v, LuaValue.Of("texture"), "local 'v'") });
                Info(r, c, k, EmitterMsg.Speed, Field(v, "speed", 1));
                Info(r, c, k, EmitterMsg.SrcMode, Field(v, "srcmode", 1));
                Info(r, c, k, EmitterMsg.DestMode, Field(v, "destmode", 1));
                Info(r, c, k, EmitterMsg.ZEnable, Field(v, "zenable", 1));
                if (!Index(v, LuaValue.Of("billboard_off"), "local 'v'").IsNil) Info(r, c, k, EmitterMsg.BillboardOff, Field(v, "billboard_off", 1));
                if (!Index(v, LuaValue.Of("rotate_angle"), "local 'v'").IsNil) Info(r, c, k, EmitterMsg.RotateAngle, Field(v, "rotate_angle", 3));
                Info(r, c, k, EmitterMsg.Init, Array.Empty<LuaValue>());
            });
        }

        /// <summary>
        /// effectfunc.lub function 6, ReadEZ2STRInfoz(name):
        /// <code>
        /// SetEZ2STRInfo(k, MSG_SET_STR_FILENAME, v.str)
        /// SetEZ2STRInfo(k, MSG_SET_RENDER_FLAG, v.rednerflag)      -- sic
        /// if v.scaleratio ~= nil then SetEZ2STRInfo(k, MSG_SET_SCALE_RATIO, v.scaleratio) end
        /// if v.alpharatio ~= nil then SetEZ2STRInfo(k, MSG_SET_ALPHA_RATIO, v.alpharatio) end
        /// </code>
        /// The values are plain fields (strings like "2.0" in the data: lua_tonumber converts them).
        /// </summary>
        void ReadEZ2STRInfoz(EffectToolResult r, string table)
        {
            const string fn = "ReadEZ2STRInfoz", c = "SetEZ2STRInfo";
            ReadInfoTable(r, fn, table, (k, v) =>
            {
                Info(r, c, k, EffectInfoMsg.StrFileName, new[] { Index(v, LuaValue.Of("str"), "local 'v'") });
                Info(r, c, k, EffectInfoMsg.RenderFlag, new[] { Index(v, LuaValue.Of("rednerflag"), "local 'v'") });
                if (!Index(v, LuaValue.Of("scaleratio"), "local 'v'").IsNil)
                    Info(r, c, k, EffectInfoMsg.ScaleRatio, new[] { Index(v, LuaValue.Of("scaleratio"), "local 'v'") });
                if (!Index(v, LuaValue.Of("alpharatio"), "local 'v'").IsNil)
                    Info(r, c, k, EffectInfoMsg.AlphaRatio, new[] { Index(v, LuaValue.Of("alpharatio"), "local 'v'") });
            });
        }

        /// <summary>The frame the three readers share: <c>local t = _G[name]; if t ~= nil then for k, v in pairs(t) do … end end</c> in its own pcall.</summary>
        void ReadInfoTable(EffectToolResult r, string fn, string table, Action<LuaValue, LuaValue> entry)
        {
            r.Calls.Add((fn, table));
            var t = r.State.Get(table);
            if (t.IsNil) return;
            try
            {
                if (!t.IsTable) throw new ReadAbort($"bad argument #1 to 'pairs' (table expected, got {TypeName(t)})");
                var k = LuaValue.Nil;
                while (t.Table.Next(ref k, out var v)) entry(k, v);
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

        /// <summary>The C function <paramref name="function"/> called from Lua with (k, msg, args…).</summary>
        void Info(EffectToolResult r, string function, LuaValue k, int msg, LuaValue[] args)
        {
            var c = new EffectInfoCall { Function = function, Key = k, Msg = msg, Args = new double[args.Length] };
            for (int i = 0; i < args.Length; i++) c.Args[i] = args[i].ToNumber();
            var stack = new double[2 + args.Length];
            stack[0] = k.ToNumber();
            stack[1] = msg;
            Array.Copy(c.Args, 0, stack, 2, c.Args.Length);
            int index = FxOps.Cvtt(stack[0]);
            int id = InfoEffectId(function);
            bool usable = MapEffectUsable(id, index);
            bool text = (id == FxRuntime.AnimatedEmitterEffectId && msg == EmitterMsg.Texture)
                        || (id == FxRuntime.MapStrEffectId && msg == EffectInfoMsg.StrFileName);
            if (text)
            {
                c.Text = args[0].ToCString();
                if (c.Text == null && usable)
                    throw new InvalidOperationException($"EffectTool entry {k}: {function} MSG {msg} with a {TypeName(args[0])} — the client crashes (string from NULL)");
            }
            if (usable) c.Applied = CallInfo(id, stack, c.Text);
            r.InfoCalls.Add(c);
        }

        static int InfoEffectId(string function) => function switch
        {
            "SetEvilsPawInfo" => FxRuntime.EvilsPawEffectId,
            "SetAnimatedEmitterInfo" => FxRuntime.AnimatedEmitterEffectId,
            "SetEZ2STRInfo" => FxRuntime.MapStrEffectId,
            _ => throw new ArgumentException(function),
        };

        bool CallInfo(int id, double[] stack, string text) => id switch
        {
            FxRuntime.EvilsPawEffectId => Rt.SetEvilsPawInfo_aa7c50(stack),
            FxRuntime.AnimatedEmitterEffectId => Rt.SetAnimatedEmitterInfo_aa7040(stack, text),
            _ => Rt.SetEZ2STRInfo_aa7520(stack, text),
        };

        /// <summary>
        /// The index-th effect of <paramref name="efId"/> (creation order = RSW record order) exists and is still live: the client
        /// would write a retired one in its graveyard harmlessly, but FxWorld destroys retired effects after every tick.
        /// </summary>
        bool MapEffectUsable(int efId, int index)
        {
            uint e = Rt.EffectByIdIndex_a4de70(efId, (uint)index);
            return e != 0 && Rt.IsNewEffectLive(e);
        }

        static double[] InfoStack(int index, int msg, double[] args)
        {
            var stack = new double[2 + (args?.Length ?? 0)];
            stack[0] = index;
            stack[1] = msg;
            if (args != null) args.CopyTo(stack, 2);
            return stack;
        }

        /// <summary>Map EF 1073 effects created so far on this map (creation order = RSW record order).</summary>
        public int MapAnimatedEmitterCount => Rt.EffectCountById(FxRuntime.AnimatedEmitterEffectId);

        /// <summary>Map EF 1039 effects created so far on this map.</summary>
        public int MapEvilsPawCount => Rt.EffectCountById(FxRuntime.EvilsPawEffectId);

        /// <summary>Map EF 1412 effects created so far on this map.</summary>
        public int MapStrCount => Rt.EffectCountById(FxRuntime.MapStrEffectId);

        /// <summary>
        /// The Lua C function <c>SetAnimatedEmitterInfo(index, msg, values…)</c> (FUN_00aa7040) on the index-th EF 1073 effect;
        /// <paramref name="args"/> as lua_tonumber returns them. False when there is no (live) effect with that index.
        /// </summary>
        public bool SetAnimatedEmitterInfo(int index, int msg, params double[] args)
            => MapEffectUsable(FxRuntime.AnimatedEmitterEffectId, index) && Rt.SetAnimatedEmitterInfo_aa7040(InfoStack(index, msg, args));

        /// <summary><see cref="SetAnimatedEmitterInfo(int, int, double[])"/> with the string argument of MSG_SET_TEXTURE.</summary>
        public bool SetAnimatedEmitterInfo(int index, int msg, string text)
            => MapEffectUsable(FxRuntime.AnimatedEmitterEffectId, index) && Rt.SetAnimatedEmitterInfo_aa7040(InfoStack(index, msg, null), text);

        /// <summary>The Lua C function <c>SetEvilsPawInfo(index, msg, values…)</c> (FUN_00aa7c50) on the index-th EF 1039 effect.</summary>
        public bool SetEvilsPawInfo(int index, int msg, params double[] args)
            => MapEffectUsable(FxRuntime.EvilsPawEffectId, index) && Rt.SetEvilsPawInfo_aa7c50(InfoStack(index, msg, args));

        /// <summary>The Lua C function <c>SetEZ2STRInfo(index, msg, value)</c> (FUN_00aa7520) on the index-th EF 1412 effect.</summary>
        public bool SetEZ2STRInfo(int index, int msg, double value)
            => MapEffectUsable(FxRuntime.MapStrEffectId, index) && Rt.SetEZ2STRInfo_aa7520(InfoStack(index, msg, new[] { value }));

        /// <summary><see cref="SetEZ2STRInfo(int, int, double)"/> with the string argument of MSG_SET_STR_FILENAME (e.g. "water_splash\water_splash.str").</summary>
        public bool SetEZ2STRInfo(int index, int msg, string text)
            => MapEffectUsable(FxRuntime.MapStrEffectId, index) && Rt.SetEZ2STRInfo_aa7520(InfoStack(index, msg, null), text);
    }
}
