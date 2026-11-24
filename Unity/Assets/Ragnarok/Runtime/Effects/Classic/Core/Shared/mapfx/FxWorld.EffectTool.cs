// Synced from reference/port/src/RoClassicFx/Shared/mapfx/FxWorld.EffectTool.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>What <see cref="FxWorld.ApplyEffectTool"/> did.</summary>
    public enum EffectToolOutcome
    {
        /// <summary>CEffectMgr's per-id map (+0x20, size +0x24) is empty: FUN_00a62d60 returns before anything (no 974 / 1025 / 1039 / 1073 / 1412 / 2343 effect exists).</summary>
        NoPerIdEffects,
        /// <summary>The script was not loaded before and no file exists (FUN_00a281d0 fails): nothing configured.</summary>
        FileMissing,
        /// <summary>Loading or running the file raised a Lua error (FUN_00a281d0 fails): nothing configured (globals set before the error stay in the state).</summary>
        LoadError,
        /// <summary>The file uses a construct this reader does not execute (<see cref="EffectToolUnsupportedException"/>): nothing configured.</summary>
        Unsupported,
        /// <summary>The version's Read* functions ran (each may have stopped on a Lua error: <see cref="EffectToolResult.Errors"/>).</summary>
        Applied,
        /// <summary>The version is neither 0, 1 nor 2: FUN_00a62d60 calls nothing.</summary>
        UnknownVersion,
    }

    /// <summary>One <c>SetEmitterInfo(k, MSG, …)</c> call of ReadEmitterInfoz(_ver_1_0).</summary>
    public sealed class EmitterInfoCall
    {
        /// <summary>The emitterInfo key as Lua passes it (lua_tonumber → the index; a string key that is not a number is index 0).</summary>
        public LuaValue Key;
        public int Msg;
        /// <summary>lua_tonumber of the stack slots after MSG (nil → 0), as many as the Lua call passes.</summary>
        public double[] Args;
        /// <summary>lua_tostring of slot 3 for MSG_SET_TEXTURE (null when it is not a string or number).</summary>
        public string Text;
        /// <summary>A 974 effect with that index existed and was written.</summary>
        public bool Applied;

        public override string ToString() => $"SetEmitterInfo({Key}, {Msg}{(Text != null ? ", \"" + Text + "\"" : "")}{(Args.Length > 0 ? ", " + string.Join(", ", Args) : "")}){(Applied ? "" : " (no emitter)")}";
    }

    /// <summary>The result of <see cref="FxWorld.ApplyEffectTool"/>: what FUN_00a62d60 and the Lua Read* functions did.</summary>
    public sealed class EffectToolResult
    {
        public EffectToolOutcome Outcome;
        /// <summary>FUN_00a64100's map key ("1@tnm1", "prontera").</summary>
        public string MapKey;
        /// <summary>The file FUN_00a281d0 looks for (.lub, then .lua).</summary>
        public string FilePath;
        /// <summary>The script, when this call loaded one (null when the state already had the map's globals, or no file).</summary>
        public EffectToolScript Script;
        /// <summary>The load error (<see cref="EffectToolOutcome.LoadError"/> / <see cref="EffectToolOutcome.Unsupported"/>).</summary>
        public string LoadError;
        /// <summary>(int)lua_tonumber(_G["_&lt;key&gt;_effect_version"]) — 0 when absent.</summary>
        public int Version;
        /// <summary>The Lua functions called, in order, with their table name argument.</summary>
        public readonly List<(string Function, string Table)> Calls = new List<(string, string)>();
        /// <summary>The SetEmitterInfo calls made, in order.</summary>
        public readonly List<EmitterInfoCall> EmitterCalls = new List<EmitterInfoCall>();
        /// <summary>Lua errors that ended a Read* function early (the client logs them and goes on with the next function): (function, message).</summary>
        public readonly List<(string Function, string Message)> Errors = new List<(string, string)>();
        /// <summary>Entries of the emitter table that the Read* function visited (pairs order), and how many configured an existing 974 effect.</summary>
        public int EmitterEntries, EmitterEntriesApplied;
        /// <summary>
        /// The tables of the Read* functions the port only counts (v1's "Effect" table; the v2 readers' calls are in
        /// <see cref="InfoCalls"/>): name → entry count (pairs), −1 when the global is
        /// not a table (the Lua function errors on it), absent when the global is nil (the Lua function returns at once).
        /// </summary>
        public readonly Dictionary<string, int> OtherTables = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>The Lua state the call used (the caller's, or a fresh one).</summary>
        public EffectToolLuaState State;
        /// <summary>The calls of the v2 readers to their Lua C functions, in order: SetMagicFloorInfo / SetWindEffectInfo (package mapfx-wind), SetEvilsPawInfo / SetAnimatedEmitterInfo / SetEZ2STRInfo (package mapfx-emitters).</summary>
        public readonly List<EffectInfoCall> InfoCalls = new List<EffectInfoCall>();
    }

    /// <summary>The map's EffectTool script (FUN_00a62d60) — configures the map emitters (EF 974). Package mapfx.</summary>
    public sealed partial class FxWorld
    {
        /// <summary>
        /// FUN_00a62d60(1), which the world builder runs right after the RSW effect records: when CEffectMgr's per-id map is
        /// not empty, derive the map key (FUN_00a64100, <see cref="EffectToolScript.MapKey"/>) from
        /// <paramref name="mapName"/> (the RSW name, "prontera.rsw" or "prontera"); load the script unless the Lua state
        /// already has <c>_&lt;key&gt;_effect_version</c> or <c>_&lt;key&gt;_emitterInfo</c> (IsExistValue; the client's state
        /// lives for the whole session, so a map's file runs once); read the version
        /// <c>(int)lua_tonumber(GetValue("_&lt;key&gt;_effect_version"))</c> (nil → 0); version 0 or 1 → the Lua
        /// <c>ReadEmitterInfoz_ver_1_0("_&lt;key&gt;_emitterInfo")</c> and <c>ReadEffectInfoz("_&lt;key&gt;_Effect")</c> (FUN_00a631d0);
        /// version 2 → <c>ReadEmitterInfoz</c>, <c>ReadMagicFloorInfoz</c>, <c>ReadEvilsPawInfoz</c>,
        /// <c>ReadAnimatedEmitterInfoz</c>, <c>ReadEZ2STRInfoz</c>, <c>ReadWindEffectInfoz</c> with "_&lt;key&gt;_emitterInfo",
        /// "…_magicfloorInfo", "…_evilsPawInfo", "…_animatedEmitterInfo", "…_ez2strInfo", "…_windEffectInfo" (FUN_00a63370);
        /// another version → nothing. (Every name has its first '@' removed: "_1tnm1_emitterInfo".) The emitter readers
        /// are emulated call for call (<see cref="SetEmitterInfo(int, int, double[])"/> in pairs order of the table as the
        /// client's Lua builds it, rand() included); the other readers target effect classes that are not ported — their
        /// tables are only counted (<see cref="EffectToolResult.OtherTables"/>). A Lua error inside a reader (a missing
        /// mandatory key, a non-table entry) ends that reader at the call it happens in, as lua_pcall does; the next reader
        /// still runs.
        /// <paramref name="file"/>: the bytes of data\luafiles514\lua files\effecttool\&lt;key&gt;.lub (or .lua), null when
        /// neither exists. <paramref name="luaState"/>: the session's state (keep one across map loads to reproduce the
        /// client exactly; null = a fresh one, equivalent unless a script defines another map's globals).
        /// </summary>
        public EffectToolResult ApplyEffectTool(string mapName, byte[] file, EffectToolLuaState luaState = null)
        {
            var r = new EffectToolResult { State = luaState ?? new EffectToolLuaState() };
            if (Rt.EffectMgr == 0 || Rt.M.U32(Rt.EffectMgr + 0x24) == 0)
            {
                r.Outcome = EffectToolOutcome.NoPerIdEffects;
                return r;
            }
            string key = EffectToolScript.MapKey(mapName);
            r.MapKey = key;
            r.FilePath = EffectToolScript.FilePath(key);
            var lua = r.State;
            string Name(string suffix) => EffectToolScript.GlobalName(key, suffix);
            if (!lua.Exists(Name("effect_version")) && !lua.Exists(Name("emitterInfo")))
            {
                if (file == null)
                {
                    r.Outcome = EffectToolOutcome.FileMissing;
                    return r;
                }
                try
                {
                    r.Script = EffectToolScript.Run(file, lua);
                }
                catch (EffectToolUnsupportedException e)
                {
                    r.Outcome = EffectToolOutcome.Unsupported;
                    r.LoadError = e.Message;
                    return r;
                }
                if (r.Script.Error != null)
                {
                    r.Outcome = EffectToolOutcome.LoadError;
                    r.LoadError = r.Script.Error;
                    return r;
                }
            }
            r.Version = FtolVersion(lua.Get(Name("effect_version")).ToNumber());
            if (r.Version == 0 || r.Version == 1)
            {
                ReadEmitterInfoz(r, Name("emitterInfo"), true);
                CountOther(r, "ReadEffectInfoz", Name("Effect"));
            }
            else if (r.Version == 2)
            {
                ReadEmitterInfoz(r, Name("emitterInfo"), false);
                ReadMagicFloorInfoz(r, Name("magicfloorInfo"));                    // package mapfx-wind
                ReadEvilsPawInfoz(r, Name("evilsPawInfo"));                        // package mapfx-emitters
                ReadAnimatedEmitterInfoz(r, Name("animatedEmitterInfo"));          // package mapfx-emitters
                ReadEZ2STRInfoz(r, Name("ez2strInfo"));                            // package mapfx-emitters
                ReadWindEffectInfoz(r, Name("windEffectInfo"));                    // package mapfx-wind
            }
            else
            {
                r.Outcome = EffectToolOutcome.UnknownVersion;
                return r;
            }
            r.Outcome = EffectToolOutcome.Applied;
            return r;
        }

        /// <summary>FUN_00a26ed0's 'd' result: lua_tonumber then thunk_FUN_00d49730 (truncation; out of range 0x80000000).</summary>
        static int FtolVersion(double v) => FxOps.Cvtt(v);

        static void CountOther(EffectToolResult r, string function, string table)
        {
            r.Calls.Add((function, table));
            var t = r.State.Get(table);
            if (t.IsNil) return;
            if (!t.IsTable) { r.OtherTables[table] = -1; return; }
            int n = 0;
            foreach (var _ in t.Table.Pairs()) n++;
            r.OtherTables[table] = n;
        }

        /// <summary>A Lua error inside a Read* function (lua_pcall catches it in FUN_00a26ed0).</summary>
        sealed class ReadAbort : Exception
        {
            public ReadAbort(string message) : base(message) { }
        }

        /// <summary>
        /// GETTABLE without metatables except strings': indexing a table reads it; indexing a string reads the string library
        /// table (the client opens it, *inferred*: no EffectTool key is a string method) → nil; anything else is Lua's
        /// "attempt to index … (a … value)".
        /// </summary>
        static LuaValue Index(LuaValue obj, LuaValue key, string what)
        {
            if (obj.IsTable) return obj.Table.Get(key);
            if (obj.IsString) return LuaValue.Nil;
            throw new ReadAbort($"attempt to index {what} (a {TypeName(obj)} value)");
        }

        static string TypeName(LuaValue v) => v.Type switch
        {
            LuaType.Nil => "nil", LuaType.Boolean => "boolean", LuaType.Number => "number", LuaType.String => "string", _ => "table",
        };

        static readonly LuaValue K1 = LuaValue.Of(1.0);

        /// <summary>v.field[1], v.field[2] … (each GETTABLE of the Lua code, in order).</summary>
        static LuaValue[] Field(LuaValue v, string field, int count)
        {
            var res = new LuaValue[count];
            var fk = LuaValue.Of(field);
            for (int i = 0; i < count; i++)
            {
                var f = Index(v, fk, "local 'v'");
                res[i] = Index(f, LuaValue.Of((double)(i + 1)), $"field '{field}'");
            }
            return res;
        }

        /// <summary>
        /// effectfunc.lub's ReadEmitterInfoz (v2, function 0) / ReadEmitterInfoz_ver_1_0 (v1, function 4):
        /// <code>
        /// local t = _G[name]            -- (v1: local ver = _G[name .. "_version"])
        /// if t ~= nil then for k, v in pairs(t) do
        ///   SetEmitterInfo(k, MSG_SET_MAXPARTICLE, v.maxcount[1])
        ///   SetEmitterInfo(k, MSG_SET_SPAWNDIR1, v.dir1[1], v.dir1[2], v.dir1[3])   -- SPAWNDIR2 dir2, GRAVITY gravity, EMITRADIUS radius
        ///   SetEmitterInfo(k, MSG_SET_COLOR, v.color[1], v.color[2], v.color[3], v.color[4])
        ///   SetEmitterInfo(k, MSG_SET_EMITRATE, v.rate[1], v.rate[2], v.rate[3])     -- SIZE size, LIFETIME life
        ///   SetEmitterInfo(k, MSG_SET_TEXTURE, v.texture)
        ///   SetEmitterInfo(k, MSG_SET_SPEED, v.speed[1])                             -- SRCMODE srcmode, DESTMODE destmode
        ///   SetEmitterInfo(k, MSG_SET_ZENABLE, v.zenable[1])                         -- v1: only when ver == 1
        ///   v2 only, each when v.&lt;key&gt; ~= nil: BILLBOARD_OFF billboard_off[1], ROTATE_ANGLE rotate_angle[1..3],
        ///     ETERNITY eternity[1], SCALE scale[1..2]
        ///   SetEmitterInfo(k, MSG_SET_INIT)
        /// end end
        /// </code>
        /// pairs on a non-table raises "bad argument #1 to 'pairs'"; every other error aborts at the GETTABLE that fails,
        /// before that call. <c>pos</c> is never sent.
        /// </summary>
        void ReadEmitterInfoz(EffectToolResult r, string table, bool ver10)
        {
            string fn = ver10 ? "ReadEmitterInfoz_ver_1_0" : "ReadEmitterInfoz";
            r.Calls.Add((fn, table));
            var lua = r.State;
            var t = lua.Get(table);
            LuaValue ver = ver10 ? lua.Get(table + "_version") : LuaValue.Nil;
            if (t.IsNil) return;
            try
            {
                if (!t.IsTable) throw new ReadAbort($"bad argument #1 to 'pairs' (table expected, got {TypeName(t)})");
                var k = LuaValue.Nil;
                while (t.Table.Next(ref k, out var v))
                {
                    r.EmitterEntries++;
                    bool applied = false;
                    void Call(int msg, LuaValue[] args, bool text = false)
                    {
                        var c = Emit(r, k, msg, args, text);
                        applied |= c.Applied;
                    }
                    Call(FxWorld.EmitterMsg.MaxParticle, Field(v, "maxcount", 1));
                    Call(FxWorld.EmitterMsg.SpawnDir1, Field(v, "dir1", 3));
                    Call(FxWorld.EmitterMsg.SpawnDir2, Field(v, "dir2", 3));
                    Call(FxWorld.EmitterMsg.Gravity, Field(v, "gravity", 3));
                    Call(FxWorld.EmitterMsg.EmitRadius, Field(v, "radius", 3));
                    Call(FxWorld.EmitterMsg.Color, Field(v, "color", 4));
                    Call(FxWorld.EmitterMsg.EmitRate, Field(v, "rate", 3));
                    Call(FxWorld.EmitterMsg.Size, Field(v, "size", 3));
                    Call(FxWorld.EmitterMsg.LifeTime, Field(v, "life", 3));
                    Call(FxWorld.EmitterMsg.Texture, new[] { Index(v, LuaValue.Of("texture"), "local 'v'") }, true);
                    Call(FxWorld.EmitterMsg.Speed, Field(v, "speed", 1));
                    Call(FxWorld.EmitterMsg.SrcMode, Field(v, "srcmode", 1));
                    Call(FxWorld.EmitterMsg.DestMode, Field(v, "destmode", 1));
                    if (!ver10 || ver.Equals(K1))                                 // v1: EQ 0 R2 K(1) — a number equal to 1
                        Call(FxWorld.EmitterMsg.ZEnable, Field(v, "zenable", 1));
                    if (!ver10)
                    {
                        if (!Index(v, LuaValue.Of("billboard_off"), "local 'v'").IsNil) Call(FxWorld.EmitterMsg.BillboardOff, Field(v, "billboard_off", 1));
                        if (!Index(v, LuaValue.Of("rotate_angle"), "local 'v'").IsNil) Call(FxWorld.EmitterMsg.RotateAngle, Field(v, "rotate_angle", 3));
                        if (!Index(v, LuaValue.Of("eternity"), "local 'v'").IsNil) Call(FxWorld.EmitterMsg.Eternity, Field(v, "eternity", 1));
                        if (!Index(v, LuaValue.Of("scale"), "local 'v'").IsNil) Call(FxWorld.EmitterMsg.Scale, Field(v, "scale", 2));
                    }
                    Call(FxWorld.EmitterMsg.Init, Array.Empty<LuaValue>());
                    if (applied) r.EmitterEntriesApplied++;
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

        /// <summary>The C function SetEmitterInfo (FUN_00aa7640) called from Lua with (k, msg, args…).</summary>
        EmitterInfoCall Emit(EffectToolResult r, LuaValue k, int msg, LuaValue[] args, bool text)
        {
            var c = new EmitterInfoCall { Key = k, Msg = msg, Args = new double[args.Length] };
            for (int i = 0; i < args.Length; i++) c.Args[i] = args[i].ToNumber();
            var stack = new double[2 + args.Length];
            stack[0] = k.ToNumber();
            stack[1] = msg;
            Array.Copy(c.Args, 0, stack, 2, c.Args.Length);
            int index = FxOps.Cvtt(stack[0]);
            if (text)
            {
                c.Text = args[0].ToCString();
                if (c.Text == null)
                {
                    // lua_tostring → NULL; the client builds a std::string from it (strlen(NULL)): an access violation,
                    // not a Lua error. Only when the effect exists — without one FUN_00aa7640 returns before reading it.
                    if (EmitterUsable(index))
                        throw new InvalidOperationException($"EffectTool entry {k}: texture is a {TypeName(args[0])} — the client crashes in SetEmitterInfo (std::string from NULL)");
                    r.EmitterCalls.Add(c);
                    return c;
                }
            }
            c.Applied = EmitterUsable(index) && Rt.SetEmitterInfo_aa7640(stack, c.Text);
            r.EmitterCalls.Add(c);
            return c;
        }
    }
}
