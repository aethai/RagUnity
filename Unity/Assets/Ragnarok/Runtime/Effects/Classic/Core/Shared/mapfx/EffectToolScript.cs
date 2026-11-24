// Synced from reference/port/src/RoClassicFx/Shared/mapfx/EffectToolScript.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The Lua global table of the client's EffectTool state (<c>DAT_01553314</c>, one per session: map scripts are loaded
    /// once and their globals stay). Seeded with the <c>MSG_SET_*</c> numbers of <c>effecttool\effectmsgid.lub</c>, which
    /// the client loads (through effecttoolutil's dofile) before any map script. Package mapfx.
    /// </summary>
    public sealed class EffectToolLuaState
    {
        readonly Dictionary<string, LuaValue> globals = new Dictionary<string, LuaValue>(StringComparer.Ordinal);

        /// <summary>effecttool\effectmsgid.lub (decomp/mapfx/effecttool/effectmsgid.txt), in file order: MSG_SET_INIT = 0 … MSG_SET_NUMBER = 53.</summary>
        public static readonly string[] MessageIds =
        {
            "INIT", "SPAWNDIR1", "SPAWNDIR2", "GRAVITY", "POS", "EMITRADIUS", "COLOR", "EMITRATE", "SIZE", "LIFETIME", "TEXTURE",
            "SPEED", "SRCMODE", "DESTMODE", "MAXPARTICLE", "TYPE", "AREA", "ANGLE", "RISEANGLE", "ALPHA", "FDA",
            "HEIGHT0", "HEIGHT1", "HEIGHT2", "HEIGHT3", "HEIGHT4", "HEIGHT5", "HEIGHT6", "HEIGHT7", "HEIGHT8", "HEIGHT9",
            "HEIGHT10", "HEIGHT11", "HEIGHT12", "HEIGHT13", "HEIGHT14", "HEIGHT15", "HEIGHT16", "HEIGHT17", "HEIGHT18",
            "HEIGHT19", "HEIGHT20", "SIZE2", "ZENABLE", "OFFSET_POS", "BILLBOARD_OFF", "ROTATE_ANGLE", "STR_FILENAME",
            "RENDER_FLAG", "SCALE_RATIO", "ALPHA_RATIO", "ETERNITY", "SCALE", "NUMBER",
        };

        public EffectToolLuaState(bool withMessageIds = true)
        {
            if (withMessageIds)
                for (int i = 0; i < MessageIds.Length; i++) globals["MSG_SET_" + MessageIds[i]] = LuaValue.Of((double)i);
        }

        /// <summary><c>GetValue(name)</c> of effecttoolutil(_ver_2).lub: <c>_G[name]</c> (nil when absent).</summary>
        public LuaValue Get(string name) => globals.TryGetValue(name, out var v) ? v : LuaValue.Nil;

        /// <summary><c>IsExistValue(name)</c> of effecttoolutil_ver_2.lub: <c>_G[name] ~= nil</c>.</summary>
        public bool Exists(string name) => !Get(name).IsNil;

        public void Set(string name, LuaValue value)
        {
            if (value.IsNil) globals.Remove(name); else globals[name] = value;
        }

        public IEnumerable<string> Names => globals.Keys;
    }

    /// <summary>A construct of a script that this reader does not execute (it is not a Lua error: the client would run it).</summary>
    public sealed class EffectToolUnsupportedException : NotSupportedException
    {
        public EffectToolUnsupportedException(string message) : base(message) { }
    }

    /// <summary>
    /// A map's EffectTool script (<c>data\luafiles514\lua files\effecttool\&lt;map&gt;.lub</c>) read without a Lua runtime:
    /// Lua 5.1 bytecode main chunks that only build tables and set globals (LOADK, LOADNIL, LOADBOOL, MOVE, NEWTABLE,
    /// SETTABLE, SETLIST, GETGLOBAL, SETGLOBAL, UNM on numbers, RETURN), and plain-text chunks of global assignments
    /// <c>Name = exp</c> where exp is nil / true / false / a number (decimal, exponent, 0x…, unary minus folded) / a
    /// "quoted" or [[long]] string / a global name / a table constructor (<c>[exp] = exp</c>, <c>name = exp</c>, positional
    /// items, ',' or ';' separators, trailing separator), with -- and --[[ ]] comments. Tables are built exactly as the
    /// client's Lua builds them (<see cref="LuaTable"/>: NEWTABLE size hints, the parser's array / hash counts, SETLIST
    /// batches of 50 stored last-to-first, the per-chunk constant pool that merges 0 and −0), so <c>pairs</c> order is the
    /// client's. Anything else throws <see cref="EffectToolUnsupportedException"/>; a Lua error (a syntax error, a nil or
    /// NaN table index, a malformed number) is reported in <see cref="Error"/> with the globals set before it kept (the
    /// client's pcall leaves them too). Strings are byte strings (one char per byte). Package mapfx.
    /// </summary>
    public sealed class EffectToolScript
    {
        /// <summary>True for a compiled chunk ("\x1bLua", version 0x51).</summary>
        public bool IsBytecode { get; private set; }

        /// <summary>The Lua error that stopped loading or running the chunk (null when it ran to the end).</summary>
        public string Error { get; private set; }

        /// <summary>The globals the chunk assigned, in assignment order (a name assigned twice keeps its first position and last value).</summary>
        public IReadOnlyList<string> AssignedGlobals => assigned;

        readonly List<string> assigned = new List<string>();
        readonly EffectToolLuaState state;

        EffectToolScript(EffectToolLuaState state) { this.state = state; }

        /// <summary>The value of a global after the chunk ran (state lookup: <c>GetValue</c>).</summary>
        public LuaValue Global(string name) => state.Get(name);

        public EffectToolLuaState State => state;

        /// <summary>
        /// Loads and runs <paramref name="file"/> (luaL_loadbuffer + lua_pcall as FUN_00a281d0 does) into
        /// <paramref name="state"/> (a new state with the message ids when null).
        /// </summary>
        public static EffectToolScript Run(byte[] file, EffectToolLuaState state = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            var s = new EffectToolScript(state ?? new EffectToolLuaState());
            try
            {
                if (file.Length >= 4 && file[0] == 0x1b && file[1] == 'L' && file[2] == 'u' && file[3] == 'a')
                {
                    s.IsBytecode = true;
                    var main = new Undump(file).Main();
                    s.Execute(main);
                }
                else
                {
                    new TextChunk(s, file).Run();
                }
            }
            catch (LuaScriptError e)
            {
                s.Error = e.Message;
            }
            return s;
        }

        void SetGlobal(string name, LuaValue v)
        {
            if (!assigned.Contains(name)) assigned.Add(name);
            state.Set(name, v);
        }

        // ---- map names (FUN_00a64100, FUN_00a645e0) ---------------------------------------------------------------

        /// <summary>
        /// FUN_00a64100: the map key from the current map name (CGameMode SendMsg 9): when the last '@' is at index ≥ 4,
        /// the three characters 4 before it are erased (the 3-digit instance prefix of "001@tnm1" style names: "0011@tower"
        /// → "1@tower"); then the name is cut at its first '.' ("prontera.rsw" → "prontera").
        /// </summary>
        public static string MapKey(string mapName)
        {
            string s = mapName ?? "";
            int at = s.LastIndexOf('@');
            if (at >= 4)
            {
                int pos = at - 4;
                int n = Math.Min(3, s.Length - pos);
                s = s.Remove(pos, n);
            }
            int dot = s.IndexOf('.');
            return dot < 0 ? s : s.Substring(0, dot);
        }

        /// <summary>FUN_00a645e0 / FUN_00a64820 / FUN_00a642f0: "_" + key + "_" + suffix with the first '@' removed ("1@tnm1", "emitterInfo" → "_1tnm1_emitterInfo").</summary>
        public static string GlobalName(string mapKey, string suffix)
        {
            string s = "_" + mapKey + "_" + suffix;
            int at = s.IndexOf('@');
            return at < 0 ? s : s.Remove(at, 1);
        }

        /// <summary>
        /// The file FUN_00a62d60 loads for a map key: "Lua Files\EffectTool\" + key, which FUN_00a281d0 turns into
        /// "LuaFiles514\Lua Files\EffectTool\&lt;key&gt;.lub", falling back to the same name with ".lua" when that does not
        /// exist (in the GRF: data\luafiles514\lua files\effecttool\&lt;key&gt;.lub).
        /// </summary>
        public static string FilePath(string mapKey, bool lua = false) => "LuaFiles514\\Lua Files\\EffectTool\\" + mapKey + (lua ? ".lua" : ".lub");

        // ---- bytecode -------------------------------------------------------------------------------------------

        sealed class Proto
        {
            public uint[] Code;
            public LuaValue[] K;
            public int MaxStack;
            public int NumProtos;
        }

        /// <summary>lundump.c for the client's format: header 1b 4c 75 61 51 00, little endian, int / size_t / Instruction 4, lua_Number 8 (double).</summary>
        sealed class Undump
        {
            readonly byte[] b;
            int p;

            public Undump(byte[] b) { this.b = b; }

            public Proto Main()
            {
                if (b.Length < 12) throw new LuaScriptError("truncated precompiled chunk");
                if (b[4] != 0x51) throw new LuaScriptError("bad header in precompiled chunk (version)");
                if (b[5] != 0 || b[6] != 1 || b[7] != 4 || b[8] != 4 || b[9] != 4 || b[10] != 8 || b[11] != 0)
                    throw new LuaScriptError("bad header in precompiled chunk");
                p = 12;
                return Function();
            }

            void Need(int n) { if (p + n > b.Length) throw new LuaScriptError("unexpected end in precompiled chunk"); }
            int Int() { Need(4); int v = BitConverter.ToInt32(b, p); p += 4; return v; }
            byte Byte() { Need(1); return b[p++]; }

            string Str()
            {
                int n = Int();
                if (n == 0) return null;
                Need(n);
                var c = new char[n - 1];
                for (int i = 0; i < n - 1; i++) c[i] = (char)b[p + i];
                p += n;
                return new string(c);
            }

            Proto Function()
            {
                var f = new Proto();
                Str(); Int(); Int();                                  // source, linedefined, lastlinedefined
                Byte(); Byte(); Byte(); f.MaxStack = Byte();          // nups, numparams, is_vararg, maxstacksize
                int nc = Int();
                if (nc < 0) throw new LuaScriptError("bad code size");
                f.Code = new uint[nc];
                for (int i = 0; i < nc; i++) f.Code[i] = (uint)Int();
                int nk = Int();
                f.K = new LuaValue[nk];
                for (int i = 0; i < nk; i++)
                {
                    switch (Byte())
                    {
                        case 0: f.K[i] = LuaValue.Nil; break;
                        case 1: f.K[i] = LuaValue.Of(Byte() != 0); break;
                        case 3: Need(8); f.K[i] = LuaValue.Of(BitConverter.ToDouble(b, p)); p += 8; break;
                        case 4: f.K[i] = LuaValue.Of(Str() ?? ""); break;
                        default: throw new LuaScriptError("bad constant in precompiled chunk");
                    }
                }
                int np = Int();
                f.NumProtos = np;
                for (int i = 0; i < np; i++) Function();              // nested functions: read past (CLOSURE is refused)
                int nl = Int(); Need(nl * 4); p += nl * 4;            // lineinfo
                int nlv = Int();
                for (int i = 0; i < nlv; i++) { Str(); Int(); Int(); }
                int nu = Int();
                for (int i = 0; i < nu; i++) Str();
                return f;
            }
        }

        static readonly string[] OpNames =
        {
            "MOVE", "LOADK", "LOADBOOL", "LOADNIL", "GETUPVAL", "GETGLOBAL", "GETTABLE", "SETGLOBAL", "SETUPVAL", "SETTABLE",
            "NEWTABLE", "SELF", "ADD", "SUB", "MUL", "DIV", "MOD", "POW", "UNM", "NOT", "LEN", "CONCAT", "JMP", "EQ", "LT",
            "LE", "TEST", "TESTSET", "CALL", "TAILCALL", "RETURN", "FORLOOP", "FORPREP", "TFORLOOP", "SETLIST", "CLOSE",
            "CLOSURE", "VARARG",
        };

        /// <summary>Runs a main chunk made of table-building instructions (lvm.c semantics, no metatables).</summary>
        void Execute(Proto f)
        {
            var R = new LuaValue[Math.Max(f.MaxStack, 2) + 2];
            LuaValue RK(int x) => x >= 256 ? f.K[x - 256] : R[x];
            for (int pc = 0; pc < f.Code.Length; )
            {
                uint i = f.Code[pc++];
                int op = (int)(i & 0x3f), A = (int)((i >> 6) & 0xff), C = (int)((i >> 14) & 0x1ff), B = (int)(i >> 23);
                int Bx = (int)(i >> 14);
                switch (op)
                {
                    case 0: R[A] = R[B]; break;                                               // MOVE
                    case 1: R[A] = f.K[Bx]; break;                                            // LOADK
                    case 2: R[A] = LuaValue.Of(B != 0); if (C != 0) pc++; break;              // LOADBOOL
                    case 3: for (int r = A; r <= B; r++) R[r] = LuaValue.Nil; break;          // LOADNIL
                    case 5:                                                                   // GETGLOBAL
                        R[A] = state.Get(GlobalKey(f.K[Bx]));
                        break;
                    case 7:                                                                   // SETGLOBAL
                        SetGlobal(GlobalKey(f.K[Bx]), R[A]);
                        break;
                    case 9:                                                                   // SETTABLE
                    {
                        var t = R[A].AsTable ?? throw new EffectToolUnsupportedException($"SETTABLE on a {R[A].Type} (pc {pc - 1}): metatables are not emulated");
                        t.Set(RK(B), RK(C));
                        break;
                    }
                    case 10:                                                                  // NEWTABLE
                        R[A] = LuaValue.Of(new LuaTable(LuaTable.Fb2Int(B), LuaTable.Fb2Int(C)));
                        break;
                    case 18:                                                                  // UNM
                    {
                        var v = R[B];
                        if (!v.IsNumber) throw new EffectToolUnsupportedException($"UNM on a {v.Type} (pc {pc - 1})");
                        R[A] = LuaValue.Of(-v.Number);
                        break;
                    }
                    case 30:                                                                  // RETURN
                        return;
                    case 34:                                                                  // SETLIST
                    {
                        if (B == 0) throw new EffectToolUnsupportedException($"SETLIST with a variable count (pc {pc - 1})");
                        if (C == 0) { if (pc >= f.Code.Length) throw new LuaScriptError("SETLIST without count"); C = (int)f.Code[pc++]; }
                        var t = R[A].AsTable ?? throw new LuaScriptError("SETLIST on a non-table");
                        var vals = new LuaValue[B];
                        for (int k = 0; k < B; k++) vals[k] = R[A + 1 + k];
                        t.SetList(vals, C);
                        break;
                    }
                    default:
                        throw new EffectToolUnsupportedException($"opcode {(op < OpNames.Length ? OpNames[op] : op.ToString(CultureInfo.InvariantCulture))} at pc {pc - 1} (only table-building main chunks are read)");
                }
            }
        }

        static string GlobalKey(LuaValue k)
        {
            if (!k.IsString) throw new EffectToolUnsupportedException($"global with a {k.Type} name");
            return k.String;
        }

        // ---- text -----------------------------------------------------------------------------------------------

        /// <summary>
        /// llex.c + lparser.c for the statements EffectTool text files use. The chunk's constants share one pool keyed by
        /// value (luaK_numberK / addk), so a number equal to an earlier one (0 vs −0) takes the earlier one's bits.
        /// </summary>
        sealed class TextChunk
        {
            const int EOS = -1;
            readonly EffectToolScript s;
            readonly byte[] src;
            int p;
            int line = 1;
            readonly Dictionary<double, double> numberPool = new Dictionary<double, double>();

            // the current token
            enum Tk { Eos, Name, Number, String, Op, Keyword }
            Tk tk;
            string tkText;      // name / string / keyword / operator
            double tkNum;
            // one token of lookahead (luaX_lookahead)
            bool hasAhead;
            Tk aTk; string aText; double aNum;

            public TextChunk(EffectToolScript s, byte[] src)
            {
                this.s = s;
                this.src = src;
                // luaL_loadbuffer: a leading '#' line is NOT skipped (only luaL_loadfile does that)
            }

            int Cur => p < src.Length ? src[p] : EOS;
            int At(int k) => p + k < src.Length ? src[p + k] : EOS;

            LuaScriptError SyntaxError(string msg) => new LuaScriptError($"[string \"buf\"]:{line}: {msg} near '{(tk == Tk.Eos ? "<eof>" : tkText ?? tkNum.ToString(CultureInfo.InvariantCulture))}'");

            // ---- parser (compile phase: nothing runs until the whole chunk parsed, as luaL_loadbuffer) -------------

            abstract class Expr { }
            sealed class ConstExpr : Expr { public LuaValue V; }
            sealed class GlobalExpr : Expr { public string Name; }
            sealed class NegExpr : Expr { public Expr E; }                       // OP_UNM at run time (operand not a numeral)
            sealed class TableExpr : Expr
            {
                public readonly List<(bool List, Expr Key, Expr Val)> Items = new List<(bool, Expr, Expr)>();
                public int Na, Nh;
            }

            readonly List<(string Name, Expr E)> statements = new List<(string, Expr)>();

            public void Run()
            {
                Next();
                while (tk != Tk.Eos)
                {
                    Statement();
                    if (tk == Tk.Op && tkText == ";") Next();
                }
                foreach (var (name, e) in statements) s.SetGlobal(name, Eval(e));
            }

            void Statement()
            {
                if (tk == Tk.Keyword)
                    throw new EffectToolUnsupportedException($"line {line}: statement '{tkText}' (only global assignments are read)");
                if (tk != Tk.Name) throw SyntaxError("unexpected symbol");
                string name = tkText;
                Next();
                if (!(tk == Tk.Op && tkText == "="))
                    throw new EffectToolUnsupportedException($"line {line}: only 'Name = exp' statements are read");
                Next();
                var e = Expression();
                if (tk == Tk.Op && tkText == ",")
                    throw new EffectToolUnsupportedException($"line {line}: multiple assignment");
                statements.Add((name, e));
            }

            /// <summary>expr: a unary-minus chain over a simple expression; a numeral result goes to the constant pool.</summary>
            Expr Expression()
            {
                var e = Unary(out bool numeral, out double n);
                if (numeral) e = new ConstExpr { V = Pool(n) };
                if ((tk == Tk.Op && IsSuffixOrBinaryOp(tkText)) || tk == Tk.String)
                    throw new EffectToolUnsupportedException($"line {line}: '{tkText}' after an expression (only constants, globals and table constructors are read)");
                return e;
            }

            /// <summary>'-' folds a numeral (constfolding OP_UNM; the result stays a numeral), otherwise OP_UNM at run time.</summary>
            Expr Unary(out bool numeral, out double n)
            {
                if (tk == Tk.Op && tkText == "-")
                {
                    Next();
                    var inner = Unary(out numeral, out n);
                    if (numeral) { n = -n; return null; }
                    return new NegExpr { E = inner };
                }
                if ((tk == Tk.Keyword && tkText == "not") || (tk == Tk.Op && tkText == "#"))
                    throw new EffectToolUnsupportedException($"line {line}: unary '{tkText}'");
                return SimpleExp(out numeral, out n);
            }

            static bool IsSuffixOrBinaryOp(string op) =>
                op == "+" || op == "-" || op == "*" || op == "/" || op == "%" || op == "^" || op == ".." || op == "==" || op == "~="
                || op == "<" || op == "<=" || op == ">" || op == ">=" || op == "(" || op == "[" || op == "." || op == ":" || op == "{";

            LuaValue Pool(double n)
            {
                double key = n == 0 ? 0.0 : n;                                    // luaH_get: 0 and −0 are one key
                if (numberPool.TryGetValue(key, out double first)) return LuaValue.Of(first);
                numberPool[key] = n;
                return LuaValue.Of(n);
            }

            Expr SimpleExp(out bool numeral, out double n)
            {
                numeral = false;
                n = 0;
                switch (tk)
                {
                    case Tk.Number:
                        n = tkNum;
                        numeral = true;
                        Next();
                        return null;
                    case Tk.String:
                    {
                        var e = new ConstExpr { V = LuaValue.Of(tkText) };
                        Next();
                        return e;
                    }
                    case Tk.Keyword:
                        switch (tkText)
                        {
                            case "nil": Next(); return new ConstExpr { V = LuaValue.Nil };
                            case "true": Next(); return new ConstExpr { V = LuaValue.True };
                            case "false": Next(); return new ConstExpr { V = LuaValue.False };
                            case "function":
                                throw new EffectToolUnsupportedException($"line {line}: function expression");
                        }
                        throw SyntaxError("unexpected symbol");
                    case Tk.Name:
                    {
                        var e = new GlobalExpr { Name = tkText };
                        Next();
                        return e;
                    }
                    case Tk.Op:
                        if (tkText == "{") return Constructor();
                        if (tkText == "(" || tkText == "...")
                            throw new EffectToolUnsupportedException($"line {line}: '{tkText}' expression");
                        throw SyntaxError("unexpected symbol");
                    default:
                        throw SyntaxError("unexpected symbol");
                }
            }

            /// <summary>lparser.c constructor: record fields ('[' exp ']' '=' exp, Name '=' exp) count in nh, list items in na.</summary>
            Expr Constructor()
            {
                int startLine = line;
                Next();                                                           // '{'
                var t = new TableExpr();
                while (!(tk == Tk.Op && tkText == "}"))
                {
                    if (tk == Tk.Name && LookaheadIs("="))
                    {
                        var key = new ConstExpr { V = LuaValue.Of(tkText) };
                        Next(); Next();                                           // Name '='
                        t.Items.Add((false, key, Expression()));
                        t.Nh++;
                    }
                    else if (tk == Tk.Op && tkText == "[")
                    {
                        Next();
                        var key = Expression();
                        Expect("]");
                        Expect("=");
                        t.Items.Add((false, key, Expression()));
                        t.Nh++;
                    }
                    else
                    {
                        t.Items.Add((true, null, Expression()));
                        t.Na++;
                    }
                    if (tk == Tk.Op && (tkText == "," || tkText == ";")) { Next(); continue; }
                    break;
                }
                if (!(tk == Tk.Op && tkText == "}"))
                    throw SyntaxError(line == startLine ? "'}' expected" : $"'}}' expected (to close '{{' at line {startLine})");
                Next();
                return t;
            }

            // ---- run phase (lvm.c) ----------------------------------------------------------------------------

            LuaValue Eval(Expr e)
            {
                switch (e)
                {
                    case ConstExpr c: return c.V;
                    case GlobalExpr g: return s.state.Get(g.Name);                // GETGLOBAL
                    case NegExpr ng:
                    {
                        var v = Eval(ng.E);
                        if (!v.IsNumber) throw new EffectToolUnsupportedException($"unary minus on a {v.Type}");
                        return LuaValue.Of(-v.Number);
                    }
                    case TableExpr te: return LuaValue.Of(Build(te));
                    default: throw new InvalidOperationException();
                }
            }

            /// <summary>
            /// The constructor's code: NEWTABLE sized luaO_fb2int(luaO_int2fb(na)) / (nh); record fields stored as they come
            /// (SETTABLE, key and value evaluated first), list items held in registers and stored by SETLIST when the 51st
            /// pending item starts (closelistfield) and at the end (lastlistfield).
            /// </summary>
            LuaTable Build(TableExpr te)
            {
                var t = new LuaTable(LuaTable.Fb2Int(LuaTable.Int2Fb((uint)te.Na)), LuaTable.Fb2Int(LuaTable.Int2Fb((uint)te.Nh)));
                var pending = new List<LuaValue>();
                int stored = 0;
                foreach (var item in te.Items)
                {
                    if (pending.Count == 50) Flush(t, pending, ref stored);
                    if (item.List) pending.Add(Eval(item.Val));
                    else
                    {
                        var key = Eval(item.Key);
                        var val = Eval(item.Val);
                        t.Set(key, val);
                    }
                }
                if (pending.Count > 0) Flush(t, pending, ref stored);
                return t;
            }

            static void Flush(LuaTable t, List<LuaValue> pending, ref int stored)
            {
                stored += pending.Count;
                t.SetList(pending, (stored - 1) / 50 + 1);                        // luaK_setlist: c = (nelems − 1) / 50 + 1
                pending.Clear();
            }

            void Expect(string op)
            {
                if (!(tk == Tk.Op && tkText == op)) throw SyntaxError($"'{op}' expected");
                Next();
            }

            bool LookaheadIs(string op)
            {
                if (!hasAhead)
                {
                    var (st, sx, sn) = (tk, tkText, tkNum);
                    Lex();
                    (aTk, aText, aNum) = (tk, tkText, tkNum);
                    (tk, tkText, tkNum) = (st, sx, sn);
                    hasAhead = true;
                }
                return aTk == Tk.Op && aText == op;
            }

            void Next()
            {
                if (hasAhead)
                {
                    (tk, tkText, tkNum) = (aTk, aText, aNum);
                    hasAhead = false;
                    return;
                }
                Lex();
            }

            static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
            {
                "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if", "in", "local", "nil", "not",
                "or", "repeat", "return", "then", "true", "until", "while",
            };

            static bool IsAlpha(int c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
            static bool IsDigit(int c) => c >= '0' && c <= '9';
            static bool IsAlnum(int c) => IsAlpha(c) || IsDigit(c);
            static bool IsNewline(int c) => c == '\n' || c == '\r';

            void IncLine()
            {
                int old = Cur;
                p++;
                if (IsNewline(Cur) && Cur != old) p++;                            // \n\r or \r\n
                line++;
            }

            /// <summary>llex: the next token into tk / tkText / tkNum.</summary>
            void Lex()
            {
                tkText = null;
                for (;;)
                {
                    int c = Cur;
                    switch (c)
                    {
                        case EOS: tk = Tk.Eos; return;
                        case '\n': case '\r': IncLine(); continue;
                        case '-':
                            if (At(1) != '-') { p++; Op("-"); return; }
                            p += 2;
                            if (Cur == '[')
                            {
                                int sep = SkipSep();
                                if (sep >= 0) { ReadLongString(sep, false); continue; }
                            }
                            while (!IsNewline(Cur) && Cur != EOS) p++;
                            continue;
                        case '[':
                        {
                            int sep = SkipSep();
                            if (sep >= 0) { tk = Tk.String; tkText = ReadLongString(sep, true); return; }
                            if (sep == -1) { Op("["); return; }
                            throw new LuaScriptError($"[string \"buf\"]:{line}: invalid long string delimiter");
                        }
                        case '=': p++; if (Cur == '=') { p++; Op("=="); } else Op("="); return;
                        case '<': p++; if (Cur == '=') { p++; Op("<="); } else Op("<"); return;
                        case '>': p++; if (Cur == '=') { p++; Op(">="); } else Op(">"); return;
                        case '~': p++; if (Cur == '=') { p++; Op("~="); return; } Op("~"); return;
                        case '"': case '\'': tk = Tk.String; tkText = ReadString(c); return;
                        case '.':
                            if (At(1) == '.') { if (At(2) == '.') { p += 3; Op("..."); } else { p += 2; Op(".."); } return; }
                            if (!IsDigit(At(1))) { p++; Op("."); return; }
                            ReadNumeral();
                            return;
                        default:
                            if (c == ' ' || c == '\t' || c == '\v' || c == '\f') { p++; continue; }
                            if (IsDigit(c)) { ReadNumeral(); return; }
                            if (IsAlpha(c) || c == '_')
                            {
                                int st = p;
                                while (IsAlnum(Cur) || Cur == '_') p++;
                                string word = Bytes(st, p);
                                tk = Keywords.Contains(word) ? Tk.Keyword : Tk.Name;
                                tkText = word;
                                return;
                            }
                            p++;
                            Op(((char)c).ToString());
                            return;
                    }
                }
            }

            void Op(string op) { tk = Tk.Op; tkText = op; }

            string Bytes(int from, int to)
            {
                var c = new char[to - from];
                for (int i = from; i < to; i++) c[i - from] = (char)src[i];
                return new string(c);
            }

            /// <summary>read_numeral: digits and '.', an exponent with optional sign, then any alphanumerics / '_'; luaO_str2d.</summary>
            void ReadNumeral()
            {
                int st = p;
                while (IsDigit(Cur) || Cur == '.') p++;
                if (Cur == 'E' || Cur == 'e')
                {
                    p++;
                    if (Cur == '+' || Cur == '-') p++;
                }
                while (IsAlnum(Cur) || Cur == '_') p++;
                string text = Bytes(st, p);
                if (!LuaValue.StrToNumber(text, out double d))
                {
                    tkText = text;
                    tk = Tk.Number;
                    throw new LuaScriptError($"[string \"buf\"]:{line}: malformed number near '{text}'");
                }
                tk = Tk.Number;
                tkNum = d;
            }

            /// <summary>skip_sep: at '[' or ']', counts '='; returns the level when the bracket repeats, else −count − 1.</summary>
            int SkipSep()
            {
                int s0 = Cur;
                p++;
                int count = 0;
                while (Cur == '=') { p++; count++; }
                return Cur == s0 ? count : (-count) - 1;
            }

            string ReadLongString(int sep, bool keep)
            {
                var sb = keep ? new StringBuilder() : null;
                p++;                                                              // second '['
                if (IsNewline(Cur)) IncLine();                                    // skip the first newline
                for (;;)
                {
                    int c = Cur;
                    switch (c)
                    {
                        case EOS:
                            throw new LuaScriptError($"[string \"buf\"]:{line}: unfinished long {(keep ? "string" : "comment")} near '<eof>'");
                        case ']':
                        {
                            int save = p;
                            if (SkipSep() == sep)
                            {
                                p++;                                              // second ']'
                                return sb?.ToString();
                            }
                            for (int i = save; i < p; i++) sb?.Append((char)src[i]);
                            break;
                        }
                        case '\n': case '\r':
                            sb?.Append('\n');
                            IncLine();
                            break;
                        default:
                            sb?.Append((char)c);
                            p++;
                            break;
                    }
                }
            }

            string ReadString(int del)
            {
                var sb = new StringBuilder();
                p++;
                while (Cur != del)
                {
                    int c = Cur;
                    switch (c)
                    {
                        case EOS:
                            throw new LuaScriptError($"[string \"buf\"]:{line}: unfinished string near '<eof>'");
                        case '\n': case '\r':
                            throw new LuaScriptError($"[string \"buf\"]:{line}: unfinished string");
                        case '\\':
                        {
                            p++;
                            int e = Cur;
                            switch (e)
                            {
                                case 'a': sb.Append('\a'); p++; break;
                                case 'b': sb.Append('\b'); p++; break;
                                case 'f': sb.Append('\f'); p++; break;
                                case 'n': sb.Append('\n'); p++; break;
                                case 'r': sb.Append('\r'); p++; break;
                                case 't': sb.Append('\t'); p++; break;
                                case 'v': sb.Append('\v'); p++; break;
                                case '\n': case '\r': sb.Append('\n'); IncLine(); break;
                                case EOS: break;                                  // reported by the loop
                                default:
                                    if (!IsDigit(e)) { sb.Append((char)e); p++; break; }
                                    int v = 0, k = 0;
                                    do { v = 10 * v + (Cur - '0'); p++; } while (++k < 3 && IsDigit(Cur));
                                    if (v > 255) throw new LuaScriptError($"[string \"buf\"]:{line}: escape sequence too large");
                                    sb.Append((char)v);
                                    break;
                            }
                            break;
                        }
                        default:
                            sb.Append((char)c);
                            p++;
                            break;
                    }
                }
                p++;
                return sb.ToString();
            }
        }
    }
}
