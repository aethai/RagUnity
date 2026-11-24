using System;
using System.Collections.Generic;

namespace Ragnarok.Data.Lua51
{
    /// <summary>A host function callable from Lua. Read the arguments from <paramref name="args"/>, return the results.</summary>
    public delegate LuaReturn LuaHostFunc(LuaArgs args);

    /// <summary>A Lua function value: a <see cref="LuaClosure"/> (bytecode) or a <see cref="LuaHostFunction"/> (C#).</summary>
    public abstract class LuaFunction
    {
        internal readonly int Id = LuaConv.NewObjectId();

        public override string ToString() => "function: " + Id.ToString("X8");
    }

    /// <summary>A C# function registered in Lua (the equivalent of a lua_CFunction).</summary>
    public sealed class LuaHostFunction : LuaFunction
    {
        /// <summary>The name used in "bad argument #n to 'name'" messages.</summary>
        public readonly string Name;
        public readonly LuaHostFunc Func;

        public LuaHostFunction(string name, LuaHostFunc func)
        {
            Name = name ?? "?";
            Func = func ?? throw new ArgumentNullException(nameof(func));
        }
    }

    /// <summary>A Lua closure: a function prototype plus its upvalues and environment table.</summary>
    public sealed class LuaClosure : LuaFunction
    {
        public readonly LuaProto Proto;
        internal readonly LuaUpvalue[] Upvals;

        /// <summary>The environment (globals) of the function (getfenv / setfenv).</summary>
        public LuaTable Env;

        internal LuaClosure(LuaProto proto, LuaTable env)
        {
            Proto = proto;
            Env = env;
            Upvals = proto.NumUpvalues == 0 ? NoUpvals : new LuaUpvalue[proto.NumUpvalues];
        }

        static readonly LuaUpvalue[] NoUpvals = new LuaUpvalue[0];

        public int UpvalueCount => Upvals.Length;

        public LuaValue GetUpvalue(int i) => Upvals[i].Value;

        public void SetUpvalue(int i, LuaValue v) => Upvals[i].Value = v;
    }

    /// <summary>An upvalue: open (pointing at a live stack slot) or closed (holding its own value).</summary>
    public sealed class LuaUpvalue
    {
        internal LuaState State;
        internal int Index;                     // stack slot while open
        internal LuaValue Closed;
        internal bool IsOpen;
        internal LuaUpvalue NextOpen;           // open list, sorted by descending Index

        internal LuaUpvalue() { }

        internal LuaUpvalue(LuaValue closedValue) { Closed = closedValue; }

        public LuaValue Value
        {
            get => IsOpen ? State.stack[Index] : Closed;
            set { if (IsOpen) State.stack[Index] = value; else Closed = value; }
        }
    }

    /// <summary>A local variable's debug info (LocVar).</summary>
    public readonly struct LuaLocalVar
    {
        public readonly string Name;
        public readonly int StartPc, EndPc;

        public LuaLocalVar(string name, int startPc, int endPc) { Name = name; StartPc = startPc; EndPc = endPc; }
    }

    /// <summary>A Lua 5.1 function prototype (Proto) as loaded from a precompiled chunk.</summary>
    public sealed class LuaProto
    {
        public string Source;
        public int LineDefined, LastLineDefined;
        public int NumUpvalues, NumParams, IsVararg, MaxStackSize;
        public uint[] Code;
        public LuaValue[] K;
        public LuaProto[] Protos;
        public int[] LineInfo;
        public LuaLocalVar[] LocVars;
        public string[] UpvalueNames;

        internal const int VarargHasArg = 1, VarargIsVararg = 2, VarargNeedsArg = 4;

        /// <summary>The source line of instruction <paramref name="pc"/> (0 when stripped).</summary>
        public int GetLine(int pc) => LineInfo != null && pc >= 0 && pc < LineInfo.Length ? LineInfo[pc] : 0;

        /// <summary>luaF_getlocalname: the name of the n-th (1-based) active local at <paramref name="pc"/>.</summary>
        public string GetLocalName(int n, int pc)
        {
            if (LocVars == null) return null;
            for (int i = 0; i < LocVars.Length && LocVars[i].StartPc <= pc; i++)
            {
                if (pc < LocVars[i].EndPc)
                {
                    n--;
                    if (n == 0) return LocVars[i].Name;
                }
            }
            return null;
        }
    }

    /// <summary>lundump.c: reads a precompiled Lua 5.1 chunk ("\x1bLua", version 0x51, format 0, little endian).</summary>
    public static class LuaUndump
    {
        /// <summary>True when <paramref name="b"/> starts with the Lua binary signature "\x1bLua".</summary>
        public static bool IsBytecode(byte[] b) => b != null && b.Length >= 4 && b[0] == 0x1b && b[1] == 'L' && b[2] == 'u' && b[3] == 'a';

        /// <summary>
        /// Loads the main function prototype. Accepted header: version 0x51, format 0, little endian, sizeof(int) 4,
        /// sizeof(size_t) 4 or 8, sizeof(Instruction) 4, sizeof(lua_Number) 8, floating-point numbers.
        /// </summary>
        public static LuaProto Load(byte[] chunk, string chunkName = null)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            var r = new Reader(chunk, chunkName ?? "=?");
            return r.Main();
        }

        sealed class Reader
        {
            readonly byte[] b;
            readonly string name;
            int p;
            int sizeT;

            public Reader(byte[] b, string name) { this.b = b; this.name = name; }

            LuaException Error(string why) => new LuaException(LuaConv.ChunkId(name) + ": bad binary format (" + why + ")");

            public LuaProto Main()
            {
                if (!IsBytecode(b)) throw Error("not a precompiled chunk");
                if (b.Length < 12) throw Error("truncated header");
                if (b[4] != 0x51) throw new LuaException(LuaConv.ChunkId(name) + ": bad header in precompiled chunk (version mismatch)");
                if (b[5] != 0) throw Error("format mismatch");
                if (b[6] != 1) throw Error("big endian chunks are not supported");
                if (b[7] != 4 || (b[8] != 4 && b[8] != 8) || b[9] != 4 || b[10] != 8 || b[11] != 0)
                    throw Error("unsupported sizes (int / size_t / Instruction / lua_Number / integral)");
                sizeT = b[8];
                p = 12;
                return Function(null);
            }

            void Need(int n)
            {
                if (n < 0 || p + n > b.Length) throw Error("unexpected end");
            }

            int Int()
            {
                Need(4);
                int v = b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24);
                p += 4;
                return v;
            }

            byte Byte() { Need(1); return b[p++]; }

            int SizeT()
            {
                if (sizeT == 4) return Int();
                int lo = Int(), hi = Int();
                if (hi != 0 || lo < 0) throw Error("string too long");
                return lo;
            }

            string Str()
            {
                int n = SizeT();
                if (n == 0) return null;
                Need(n);
                string s = LuaValue.BytesToString(b, p, n - 1);
                p += n;
                return s;
            }

            LuaProto Function(string parentSource)
            {
                var f = new LuaProto();
                f.Source = Str() ?? parentSource ?? name;
                f.LineDefined = Int();
                f.LastLineDefined = Int();
                f.NumUpvalues = Byte();
                f.NumParams = Byte();
                f.IsVararg = Byte();
                f.MaxStackSize = Byte();
                int nc = Int();
                if (nc < 0) throw Error("bad code size");
                Need(nc * 4);
                f.Code = new uint[nc];
                for (int i = 0; i < nc; i++) f.Code[i] = (uint)Int();
                int nk = Int();
                if (nk < 0) throw Error("bad constant count");
                f.K = new LuaValue[nk];
                for (int i = 0; i < nk; i++)
                {
                    switch (Byte())
                    {
                        case 0: f.K[i] = LuaValue.Nil; break;
                        case 1: f.K[i] = LuaValue.Of(Byte() != 0); break;
                        case 3: Need(8); f.K[i] = LuaValue.Of(BitConverter.Int64BitsToDouble(BitConverter.ToInt64(b, p))); p += 8; break;
                        case 4: f.K[i] = LuaValue.Of(Str() ?? ""); break;
                        default: throw Error("bad constant");
                    }
                }
                int np = Int();
                if (np < 0) throw Error("bad prototype count");
                f.Protos = new LuaProto[np];
                for (int i = 0; i < np; i++) f.Protos[i] = Function(f.Source);
                int nl = Int();
                if (nl < 0) throw Error("bad line info");
                Need(nl * 4);
                f.LineInfo = new int[nl];
                for (int i = 0; i < nl; i++) f.LineInfo[i] = Int();
                int nlv = Int();
                if (nlv < 0) throw Error("bad local count");
                f.LocVars = new LuaLocalVar[nlv];
                for (int i = 0; i < nlv; i++)
                {
                    string ln = Str();
                    int s = Int(), e = Int();
                    f.LocVars[i] = new LuaLocalVar(ln, s, e);
                }
                int nu = Int();
                if (nu < 0) throw Error("bad upvalue count");
                f.UpvalueNames = new string[nu];
                for (int i = 0; i < nu; i++) f.UpvalueNames[i] = Str();
                Verify(f);
                return f;
            }

            /// <summary>The structural checks the VM relies on (a subset of luaG_checkcode): operands within the frame / constant / prototype ranges.</summary>
            void Verify(LuaProto f)
            {
                if (f.MaxStackSize < 2 && f.Code.Length > 0) { /* luac always reserves 2; tolerate */ }
                if (f.NumParams > f.MaxStackSize && f.MaxStackSize > 0) throw Error("too many parameters");
                int n = f.Code.Length;
                if (n == 0) throw Error("empty function");
                for (int pc = 0; pc < n; pc++)
                {
                    uint i = f.Code[pc];
                    int op = (int)(i & 0x3F);
                    int a = (int)((i >> 6) & 0xFF);
                    int c = (int)((i >> 14) & 0x1FF);
                    int bb = (int)(i >> 23);
                    int bx = (int)(i >> 14);
                    if (op > (int)LuaOpCode.VARARG) throw Error("bad opcode " + op + " at pc " + pc);
                    switch ((LuaOpCode)op)
                    {
                        case LuaOpCode.LOADK:
                        case LuaOpCode.GETGLOBAL:
                        case LuaOpCode.SETGLOBAL:
                            if (bx >= f.K.Length) throw Error("constant index out of range at pc " + pc);
                            if (op != (int)LuaOpCode.LOADK && !f.K[bx].IsString) throw Error("global name is not a string at pc " + pc);
                            break;
                        case LuaOpCode.CLOSURE:
                            if (bx >= f.Protos.Length) throw Error("prototype index out of range at pc " + pc);
                            if (pc + f.Protos[bx].NumUpvalues >= n) throw Error("missing upvalue pseudo-instructions at pc " + pc);
                            break;
                        case LuaOpCode.JMP:
                        case LuaOpCode.FORLOOP:
                        case LuaOpCode.FORPREP:
                        {
                            int dest = pc + 1 + (bx - 131071);
                            if (dest < 0 || dest >= n) throw Error("jump out of range at pc " + pc);
                            break;
                        }
                        case LuaOpCode.EQ:
                        case LuaOpCode.LT:
                        case LuaOpCode.LE:
                        case LuaOpCode.TEST:
                        case LuaOpCode.TESTSET:
                        case LuaOpCode.TFORLOOP:
                            // the VM takes the jump from the next instruction (luaG_checkcode: it must be a JMP)
                            if (pc + 1 >= n || (LuaOpCode)(f.Code[pc + 1] & 0x3F) != LuaOpCode.JMP) throw Error("conditional not followed by a jump at pc " + pc);
                            break;
                        case LuaOpCode.LOADBOOL:
                            if (c != 0 && pc + 2 >= n) throw Error("LOADBOOL skips past the end at pc " + pc);
                            break;
                        case LuaOpCode.GETUPVAL:
                        case LuaOpCode.SETUPVAL:
                            if (bb >= f.NumUpvalues) throw Error("upvalue index out of range at pc " + pc);
                            break;
                    }
                    if (RkB(op) && bb >= 256 && bb - 256 >= f.K.Length) throw Error("constant index out of range at pc " + pc);
                    if (RkC(op) && c >= 256 && c - 256 >= f.K.Length) throw Error("constant index out of range at pc " + pc);
                    if (a >= Math.Max(f.MaxStackSize, 1) && op != (int)LuaOpCode.JMP && op != (int)LuaOpCode.EQ && op != (int)LuaOpCode.LT && op != (int)LuaOpCode.LE)
                    {
                        // pseudo-instructions after CLOSURE use A = 0; anything else must address a register of the frame
                        throw Error("register out of range at pc " + pc);
                    }
                    if (op == (int)LuaOpCode.CLOSURE)
                    {
                        // pseudo-instructions: MOVE 0 B (a register of this frame) or GETUPVAL 0 B (an upvalue of this function)
                        for (int u = 0; u < f.Protos[bx].NumUpvalues; u++)
                        {
                            uint pi = f.Code[++pc];
                            int pop = (int)(pi & 0x3F), pb = (int)(pi >> 23);
                            if (pop == (int)LuaOpCode.MOVE) { if (pb >= f.MaxStackSize) throw Error("bad upvalue register at pc " + pc); }
                            else if (pop == (int)LuaOpCode.GETUPVAL) { if (pb >= f.NumUpvalues) throw Error("bad upvalue index at pc " + pc); }
                            else throw Error("bad upvalue pseudo-instruction at pc " + pc);
                        }
                    }
                    else if (op == (int)LuaOpCode.SETLIST && c == 0)
                    {
                        if (pc + 1 >= n) throw Error("SETLIST without its batch number at pc " + pc);
                        pc++;                                                                       // the next word is the batch number
                    }
                }
                if ((LuaOpCode)(f.Code[n - 1] & 0x3F) != LuaOpCode.RETURN) throw Error("function does not end with RETURN");
            }

            static bool RkB(int op) => op == (int)LuaOpCode.SETTABLE || (op >= (int)LuaOpCode.ADD && op <= (int)LuaOpCode.POW) || op == (int)LuaOpCode.EQ || op == (int)LuaOpCode.LT || op == (int)LuaOpCode.LE;

            static bool RkC(int op) => op == (int)LuaOpCode.GETTABLE || op == (int)LuaOpCode.SETTABLE || op == (int)LuaOpCode.SELF || (op >= (int)LuaOpCode.ADD && op <= (int)LuaOpCode.POW) || op == (int)LuaOpCode.EQ || op == (int)LuaOpCode.LT || op == (int)LuaOpCode.LE;
        }
    }

    /// <summary>Lua 5.1 opcodes (lopcodes.h order).</summary>
    public enum LuaOpCode
    {
        MOVE, LOADK, LOADBOOL, LOADNIL, GETUPVAL, GETGLOBAL, GETTABLE, SETGLOBAL, SETUPVAL, SETTABLE, NEWTABLE, SELF,
        ADD, SUB, MUL, DIV, MOD, POW, UNM, NOT, LEN, CONCAT, JMP, EQ, LT, LE, TEST, TESTSET, CALL, TAILCALL, RETURN,
        FORLOOP, FORPREP, TFORLOOP, SETLIST, CLOSE, CLOSURE, VARARG,
    }

    /// <summary>
    /// The arguments of a host function call: a view of the VM stack, valid only during the call. Indices are 0-based
    /// (argument #1 is <c>args[0]</c>); reading past <see cref="Count"/> gives nil. The Check*/Opt* helpers raise the
    /// standard "bad argument #n to 'name' (...)" Lua errors.
    /// </summary>
    public readonly struct LuaArgs
    {
        readonly LuaState state;
        readonly int baseIndex;
        readonly LuaHostFunction function;

        /// <summary>Number of arguments passed.</summary>
        public readonly int Count;

        internal LuaArgs(LuaState state, int baseIndex, int count, LuaHostFunction function)
        {
            this.state = state;
            this.baseIndex = baseIndex;
            Count = count;
            this.function = function;
        }

        public LuaState State => state;

        internal int Base => baseIndex;

        /// <summary>The name of the called host function.</summary>
        public string FunctionName => function?.Name ?? "?";

        public LuaValue this[int i] => (uint)i < (uint)Count ? state.stack[baseIndex + i] : LuaValue.Nil;

        /// <summary>A copy of all arguments.</summary>
        public LuaValue[] ToArray()
        {
            var a = new LuaValue[Count];
            Array.Copy(state.stack, baseIndex, a, 0, Count);
            return a;
        }

        /// <summary>luaL_error: raises a Lua error with the calling Lua function's position ("chunk:line: msg").</summary>
        public LuaException Error(string message) => state.ErrorWithWhere(message, 1);

        /// <summary>luaL_argerror: "bad argument #n to 'name' (extra)".</summary>
        public LuaException ArgError(int i, string extra) => Error("bad argument #" + (i + 1) + " to '" + FunctionName + "' (" + extra + ")");

        LuaException TypeError(int i, string expected)
        {
            string got = i < Count ? this[i].TypeName : "no value";
            return ArgError(i, expected + " expected, got " + got);
        }

        public void CheckAny(int i)
        {
            if (i >= Count) throw ArgError(i, "value expected");
        }

        public double CheckNumber(int i)
        {
            var v = this[i];
            if (v.Type == LuaType.Number) return v.N;
            if (v.TryGetNumber(out double d)) return d;
            throw TypeError(i, "number");
        }

        /// <summary>luaL_checkint (lua_number2int rounding of the client's build).</summary>
        public int CheckInt(int i) => LuaConv.Number2Int(CheckNumber(i));

        public double OptNumber(int i, double def) => this[i].IsNil ? def : CheckNumber(i);

        public int OptInt(int i, int def) => this[i].IsNil ? def : CheckInt(i);

        /// <summary>luaL_checklstring: a string, or a number converted with "%.14g".</summary>
        public string CheckString(int i)
        {
            var v = this[i];
            if (v.Type == LuaType.String) return (string)v.O;
            if (v.Type == LuaType.Number)
            {
                // lua_tolstring converts the stack slot in place
                string s = LuaConv.FormatNumber(v.N);
                state.stack[baseIndex + i] = LuaValue.Of(s);
                return s;
            }
            throw TypeError(i, "string");
        }

        public string OptString(int i, string def) => this[i].IsNil ? def : CheckString(i);

        public LuaTable CheckTable(int i)
        {
            var v = this[i];
            if (v.Type == LuaType.Table) return (LuaTable)v.O;
            throw TypeError(i, "table");
        }

        public LuaFunction CheckFunction(int i)
        {
            var v = this[i];
            if (v.Type == LuaType.Function) return (LuaFunction)v.O;
            throw TypeError(i, "function");
        }

        /// <summary>luaL_checktype.</summary>
        public void CheckType(int i, LuaType t)
        {
            if (this[i].Type != t || i >= Count) throw TypeError(i, LuaConv.TypeName(t));
        }
    }

    /// <summary>
    /// The results of a host function: none, one, two, or many values. Implicitly converts from a value / number / string /
    /// bool / table, so a host function can simply <c>return 5;</c>.
    /// </summary>
    public readonly struct LuaReturn
    {
        internal readonly byte Kind;            // 0 inline (Count 0..2), 1 array, 2 stack range [Start, Start + Count)
        internal readonly int Count;
        internal readonly LuaValue V0, V1;
        internal readonly LuaValue[] Many;
        internal readonly int Start;

        LuaReturn(int count, LuaValue v0, LuaValue v1)
        {
            Kind = 0; Count = count; V0 = v0; V1 = v1; Many = null; Start = 0;
        }

        LuaReturn(LuaValue[] many, int count)
        {
            Kind = 1; Count = count; V0 = default; V1 = default; Many = many; Start = 0;
        }

        LuaReturn(int start, int count)
        {
            Kind = 2; Count = count; V0 = default; V1 = default; Many = null; Start = start;
        }

        /// <summary>No results.</summary>
        public static readonly LuaReturn None = default;

        public int ResultCount => Count;

        public static LuaReturn Of(LuaValue v) => new LuaReturn(1, v, default);

        public static LuaReturn Of(LuaValue a, LuaValue b) => new LuaReturn(2, a, b);

        public static LuaReturn Of(params LuaValue[] values) => values == null ? None : new LuaReturn(values, values.Length);

        /// <summary>The first <paramref name="count"/> values of <paramref name="values"/> (the array is not copied).</summary>
        public static LuaReturn FromArray(LuaValue[] values, int count) => new LuaReturn(values, count);

        public static LuaReturn FromList(List<LuaValue> values) => values == null ? None : new LuaReturn(values.ToArray(), values.Count);

        internal static LuaReturn StackRange(int start, int count) => new LuaReturn(start, count);

        public static implicit operator LuaReturn(LuaValue v) => Of(v);
        public static implicit operator LuaReturn(double n) => Of(LuaValue.Of(n));
        public static implicit operator LuaReturn(bool b) => Of(LuaValue.Of(b));
        public static implicit operator LuaReturn(string s) => Of(LuaValue.Of(s));
        public static implicit operator LuaReturn(LuaTable t) => Of(LuaValue.Of(t));
    }
}
