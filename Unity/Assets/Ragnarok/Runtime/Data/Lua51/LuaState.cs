using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Data.Lua51
{
    /// <summary>
    /// A Lua 5.1 state: a bytecode interpreter for precompiled chunks (lvm.c / ldo.c semantics), its globals and the
    /// standard library subset of <see cref="LuaStdLib"/>. Not thread-safe; one state per thread.
    /// <para>
    /// Lua-to-Lua calls do not recurse on the C# stack (frames live in a call-info array, limit 20000 = LUAI_MAXCALLS);
    /// metamethods, generic-for iterators and host re-entry recurse (limit 200 = LUAI_MAXCCALLS, "C stack overflow").
    /// </para>
    /// </summary>
    public sealed partial class LuaState
    {
        const int MaxCalls = 20000;             // LUAI_MAXCALLS
        const int MaxCCalls = 200;              // LUAI_MAXCCALLS
        const int MaxStackSlots = 8 * 1024 * 1024;
        const int MinStack = 20;                // LUA_MINSTACK
        const int FieldsPerFlush = 50;          // LFIELDS_PER_FLUSH
        const int MaxTagLoop = 100;             // MAXTAGLOOP
        const int RegisterHeadroom = 1024;      // slots kept above a Lua frame: any A / B / C operand (≤ 511) stays inside the stack

        internal LuaValue[] stack = new LuaValue[1024];
        internal int top;

        sealed class CallInfo
        {
            public int Func;                    // stack index of the called value
            public int Base;                    // first register / first argument
            public int Top;                     // Lua: Base + maxstacksize; host: top + LUA_MINSTACK
            public int SavedPc;                 // Lua: index of the next instruction
            public int NResults;                // wanted results (−1 = all)
            public LuaClosure Closure;          // null for a host function
            public LuaHostFunction Host;
            public bool IsEntry;                // Execute returns when this frame returns
        }

        CallInfo[] cis = new CallInfo[32];
        int ciCount;
        int nCcalls;
        int apiDepth;
        LuaUpvalue openUpvals;
        long budgetLeft = long.MaxValue;
        long budgetStart = long.MaxValue;

        /// <summary>The global table (_G).</summary>
        public LuaTable Globals { get; }

        /// <summary>The metatable shared by all strings (its __index is the string library), so <c>s:upper()</c> works.</summary>
        public LuaTable StringMetatable { get; set; }

        /// <summary>Receives the lines of Lua's print (nothing is printed when null).</summary>
        public Action<string> Print { get; set; }

        /// <summary>Resolves a file name for dofile / require to a precompiled chunk (null = not found). Unset: dofile / require fail.</summary>
        public Func<string, byte[]> ChunkResolver { get; set; }

        /// <summary>
        /// When &gt; 0, every top-level <see cref="Call"/> / <see cref="DoChunk"/> may execute at most this many VM
        /// instructions; past it a <see cref="LuaBudgetExceededException"/> is thrown (pcall does not catch it).
        /// </summary>
        public long InstructionLimit { get; set; }

        /// <summary>VM instructions executed by the last completed top-level call.</summary>
        public long LastInstructionCount { get; private set; }

        /// <summary>Creates a state with the standard library subset (<see cref="LuaStdLib"/>) unless <paramref name="openLibs"/> is false.</summary>
        public LuaState(bool openLibs = true)
        {
            Globals = new LuaTable(0, 64);
            Globals.Set("_G", LuaValue.Of(Globals));
            if (openLibs) LuaStdLib.Open(this);
        }

        // ================================================================================================================
        // Public API
        // ================================================================================================================

        /// <summary>Loads a precompiled Lua 5.1 chunk as a function (luaL_loadbuffer) whose environment is <see cref="Globals"/>.</summary>
        public LuaClosure Load(byte[] chunk, string chunkName = null)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            string name = chunkName == null ? "=?" : (chunkName.Length > 0 && (chunkName[0] == '@' || chunkName[0] == '=') ? chunkName : "@" + chunkName);
            if (!LuaUndump.IsBytecode(chunk))
                throw new LuaException(LuaConv.ChunkId(name) + ": source chunks are not supported (only precompiled Lua 5.1 bytecode)");
            var proto = LuaUndump.Load(chunk, name);
            var cl = new LuaClosure(proto, Globals);
            for (int i = 0; i < cl.Upvals.Length; i++) cl.Upvals[i] = new LuaUpvalue(LuaValue.Nil);
            return cl;
        }

        /// <summary>Loads and runs a precompiled chunk, returning its results.</summary>
        public LuaValue[] DoChunk(byte[] chunk, string chunkName = null) => Call(LuaValue.Of(Load(chunk, chunkName)));

        /// <summary>Calls a Lua value (function, or a value with __call) with arguments and returns all its results.</summary>
        public LuaValue[] Call(LuaValue function, params LuaValue[] args)
        {
            args = args ?? Array.Empty<LuaValue>();
            int func = top;
            int savedCi = ciCount, savedNC = nCcalls;
            bool outer = apiDepth == 0;
            if (outer) StartBudget();
            apiDepth++;
            try
            {
                EnsureStack(func + args.Length + 1 + MinStack);
                stack[func] = function;
                for (int i = 0; i < args.Length; i++) stack[func + 1 + i] = args[i];
                top = func + 1 + args.Length;
                CallValue(func, -1);
                int n = top - func;
                var res = new LuaValue[n];
                Array.Copy(stack, func, res, 0, n);
                return res;
            }
            catch (LuaException e)
            {
                if (e.LuaTraceback == null) e.LuaTraceback = Traceback(savedCi);
                throw;
            }
            finally
            {
                CloseUpvalues(func);
                ciCount = savedCi;
                nCcalls = savedNC;
                int clearTo = Math.Min(stack.Length, Math.Max(top, func + 1 + args.Length));
                for (int i = func; i < clearTo; i++) stack[i] = LuaValue.Nil;
                top = func;
                apiDepth--;
                if (outer) EndBudget();
            }
        }

        /// <summary>Calls a Lua value and returns its first result (nil when none).</summary>
        public LuaValue Call1(LuaValue function, params LuaValue[] args)
        {
            var r = Call(function, args);
            return r.Length > 0 ? r[0] : LuaValue.Nil;
        }

        /// <summary>Creates a host function value.</summary>
        public static LuaHostFunction Function(string name, LuaHostFunc func) => new LuaHostFunction(name, func);

        /// <summary>Registers a host function as global <paramref name="name"/>.</summary>
        public LuaHostFunction Register(string name, LuaHostFunc func)
        {
            var f = new LuaHostFunction(name, func);
            Globals.Set(name, LuaValue.Of(f));
            return f;
        }

        /// <summary>Registers a host function in <paramref name="table"/>[<paramref name="name"/>].</summary>
        public static LuaHostFunction Register(LuaTable table, string name, LuaHostFunc func)
        {
            var f = new LuaHostFunction(name, func);
            table.Set(name, LuaValue.Of(f));
            return f;
        }

        /// <summary>The raw value of global <paramref name="name"/>.</summary>
        public LuaValue GetGlobal(string name) => Globals.Get(name);

        /// <summary>Sets global <paramref name="name"/> (raw).</summary>
        public void SetGlobal(string name, LuaValue value) => Globals.Set(name, value);

        /// <summary>Reads a dotted path of fields from the globals ("a.b.c"), raw, nil when a step is not a table.</summary>
        public LuaValue GetPath(string path)
        {
            LuaValue v = LuaValue.Of(Globals);
            foreach (var part in path.Split('.'))
            {
                var t = v.AsTable;
                if (t == null) return LuaValue.Nil;
                v = t.Get(part);
            }
            return v;
        }

        /// <summary><c>obj[key]</c> with metamethods (__index), as Lua code would read it.</summary>
        public LuaValue Index(LuaValue obj, LuaValue key) => Protected(() => GetTable(obj, key, -1));

        /// <summary><c>obj[key] = value</c> with metamethods (__newindex).</summary>
        public void SetIndex(LuaValue obj, LuaValue key, LuaValue value) => Protected(() => { SetTable(obj, key, value, -1); return LuaValue.Nil; });

        /// <summary>Lua's tostring (with __tostring).</summary>
        public string ToStringMeta(LuaValue v) => Protected(() => LuaValue.Of(ToStringValue(v))).AsString;

        /// <summary>Lua's == (with __eq).</summary>
        public bool ValuesEqual(LuaValue a, LuaValue b) => Protected(() => LuaValue.Of(EqualValues(a, b))).IsTruthy;

        /// <summary>Lua's &lt; (with __lt).</summary>
        public bool LessThan(LuaValue a, LuaValue b) => Protected(() => LuaValue.Of(LessThanValues(a, b))).IsTruthy;

        LuaValue Protected(Func<LuaValue> body)
        {
            int savedTop = top, savedCi = ciCount, savedNC = nCcalls;
            bool outer = apiDepth == 0;
            if (outer) StartBudget();
            apiDepth++;
            try
            {
                return body();
            }
            catch (LuaException e)
            {
                if (e.LuaTraceback == null) e.LuaTraceback = Traceback(savedCi);
                throw;
            }
            finally
            {
                CloseUpvalues(savedTop);
                ciCount = savedCi;
                nCcalls = savedNC;
                top = savedTop;
                apiDepth--;
                if (outer) EndBudget();
            }
        }

        void StartBudget()
        {
            budgetLeft = InstructionLimit > 0 ? InstructionLimit : long.MaxValue;
            budgetStart = budgetLeft;
        }

        void EndBudget()
        {
            LastInstructionCount = budgetStart - Math.Max(budgetLeft, 0);
            budgetLeft = long.MaxValue;
            budgetStart = long.MaxValue;
        }

        void BudgetExceeded()
        {
            budgetLeft = 0;
            throw new LuaBudgetExceededException(InstructionLimit);
        }

        // ================================================================================================================
        // Stack, call infos, upvalues
        // ================================================================================================================

        internal void EnsureStack(int needed)
        {
            if (needed <= stack.Length) return;
            if (needed > MaxStackSlots) throw RtError("stack overflow");
            int n = stack.Length;
            while (n < needed) n *= 2;
            Array.Resize(ref stack, Math.Min(n, MaxStackSlots));
        }

        CallInfo PushCi()
        {
            if (ciCount == cis.Length) Array.Resize(ref cis, cis.Length * 2);
            var ci = cis[ciCount] ?? (cis[ciCount] = new CallInfo());
            ciCount++;
            return ci;
        }

        LuaUpvalue FindUpval(int level)
        {
            LuaUpvalue prev = null, p = openUpvals;
            while (p != null && p.Index >= level)
            {
                if (p.Index == level) return p;
                prev = p;
                p = p.NextOpen;
            }
            var uv = new LuaUpvalue { State = this, Index = level, IsOpen = true, NextOpen = p };
            if (prev == null) openUpvals = uv; else prev.NextOpen = uv;
            return uv;
        }

        internal void CloseUpvalues(int level)
        {
            while (openUpvals != null && openUpvals.Index >= level)
            {
                var uv = openUpvals;
                uv.Closed = stack[uv.Index];
                uv.IsOpen = false;
                uv.State = null;
                openUpvals = uv.NextOpen;
                uv.NextOpen = null;
            }
        }

        // ================================================================================================================
        // Calls
        // ================================================================================================================

        /// <summary>luaD_call: calls stack[func] with the arguments up to top; results at func.. (top = func + n when nresults = −1).</summary>
        internal void CallValue(int func, int nresults)
        {
            if (++nCcalls >= MaxCCalls)
            {
                nCcalls--;
                throw RtError("C stack overflow");
            }
            try
            {
                var f = ResolveCallable(func);
                if (f is LuaClosure cl)
                {
                    PushLuaFrame(func, cl, nresults);
                    cis[ciCount - 1].IsEntry = true;
                    Execute();
                }
                else
                {
                    CallHost(func, (LuaHostFunction)f, nresults);
                }
            }
            finally
            {
                nCcalls--;
            }
        }

        /// <summary>The function at stack[func], inserting its __call metamethod first when it is not a function (tryfuncTM).</summary>
        LuaFunction ResolveCallable(int func)
        {
            var v = stack[func];
            if (v.Type == LuaType.Function) return (LuaFunction)v.O;
            var tm = GetMetamethod(v, TmCall);
            if (tm.Type != LuaType.Function) throw TypeError(v, "call", func);
            EnsureStack(top + 1 + MinStack);
            for (int p = top; p > func; p--) stack[p] = stack[p - 1];
            top++;
            stack[func] = tm;
            return (LuaFunction)tm.O;
        }

        /// <summary>luaD_precall for a Lua function: adjusts arguments / varargs and pushes the frame.</summary>
        void PushLuaFrame(int func, LuaClosure cl, int nresults)
        {
            var p = cl.Proto;
            if (ciCount >= MaxCalls) throw RtError("stack overflow");
            int nargs = top - func - 1;
            EnsureStack(func + 1 + Math.Max(nargs, p.NumParams) + p.NumParams + 2 + RegisterHeadroom + MinStack);
            var st = stack;
            int @base;
            if (p.IsVararg == 0)
            {
                @base = func + 1;
                if (top > @base + p.NumParams) top = @base + p.NumParams;
            }
            else
            {
                // adjust_varargs
                int actual = nargs;
                for (; actual < p.NumParams; actual++) st[top++] = LuaValue.Nil;
                LuaTable htab = null;
                if ((p.IsVararg & LuaProto.VarargNeedsArg) != 0)
                {
                    int nvar = actual - p.NumParams;
                    htab = new LuaTable(nvar, 1);
                    for (int k = 0; k < nvar; k++) htab.SetInt(k + 1, st[top - nvar + k]);
                    htab.Set("n", LuaValue.Of(nvar));
                }
                int fixedIdx = top - actual;
                @base = top;
                for (int k = 0; k < p.NumParams; k++)
                {
                    st[top++] = st[fixedIdx + k];
                    st[fixedIdx + k] = LuaValue.Nil;
                }
                if (htab != null) st[top++] = LuaValue.Of(htab);
            }
            var ci = PushCi();
            ci.Func = func;
            ci.Base = @base;
            ci.Top = @base + p.MaxStackSize;
            ci.SavedPc = 0;
            ci.NResults = nresults;
            ci.Closure = cl;
            ci.Host = null;
            ci.IsEntry = false;
            EnsureStack(ci.Top + RegisterHeadroom + MinStack);
            st = stack;
            for (int s = top; s < ci.Top; s++) st[s] = LuaValue.Nil;
            top = ci.Top;
        }

        /// <summary>Runs a host function at stack[func] with the arguments up to top and places its results (luaD_precall + luaD_poscall for C).</summary>
        void CallHost(int func, LuaHostFunction h, int nresults)
        {
            if (ciCount >= MaxCalls) throw RtError("stack overflow");
            EnsureStack(top + MinStack);
            var ci = PushCi();
            ci.Func = func;
            ci.Base = func + 1;
            ci.Top = top + MinStack;
            ci.SavedPc = 0;
            ci.NResults = nresults;
            ci.Closure = null;
            ci.Host = h;
            ci.IsEntry = false;
            LuaReturn r;
            try
            {
                r = h.Func(new LuaArgs(this, func + 1, top - func - 1, h));
            }
            catch (LuaException)
            {
                throw;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                throw new LuaException(Where(1) + h.Name + ": " + ex.GetType().Name + ": " + ex.Message, ex);
            }
            ciCount--;
            PlaceResults(func, r, nresults);
        }

        void PlaceResults(int res, in LuaReturn r, int wanted)
        {
            int n = r.Count;
            int count = wanted < 0 ? n : wanted;
            int have = Math.Min(n, count);
            EnsureStack(res + count + MinStack);
            var st = stack;
            switch (r.Kind)
            {
                case 0:
                    if (have > 0) st[res] = r.V0;
                    if (have > 1) st[res + 1] = r.V1;
                    break;
                case 1:
                    for (int k = 0; k < have; k++) st[res + k] = r.Many[k];
                    break;
                default:
                    if (r.Start != res) for (int k = 0; k < have; k++) st[res + k] = st[r.Start + k];
                    break;
            }
            for (int k = have; k < count; k++) st[res + k] = LuaValue.Nil;
            top = res + count;
        }

        // ================================================================================================================
        // The interpreter (luaV_execute)
        // ================================================================================================================

        void Execute()
        {
        reentry:
            CallInfo ci = cis[ciCount - 1];
            LuaClosure cl = ci.Closure;
            LuaProto proto = cl.Proto;
            uint[] code = proto.Code;
            LuaValue[] K = proto.K;
            int @base = ci.Base;
            int pc = ci.SavedPc;
            while (true)
            {
                if (--budgetLeft < 0) BudgetExceeded();
                uint i = code[pc++];
                ci.SavedPc = pc;
                LuaValue[] st = stack;
                int A = (int)((i >> 6) & 0xFF);
                int ra = @base + A;
                switch ((LuaOpCode)(i & 0x3F))
                {
                    case LuaOpCode.MOVE:
                        st[ra] = st[@base + (int)(i >> 23)];
                        continue;

                    case LuaOpCode.LOADK:
                        st[ra] = K[i >> 14];
                        continue;

                    case LuaOpCode.LOADBOOL:
                        st[ra] = (i >> 23) != 0 ? LuaValue.True : LuaValue.False;
                        if (((i >> 14) & 0x1FF) != 0) pc++;
                        continue;

                    case LuaOpCode.LOADNIL:
                    {
                        int rb = @base + (int)(i >> 23);
                        for (int r = ra; r <= rb; r++) st[r] = LuaValue.Nil;
                        continue;
                    }

                    case LuaOpCode.GETUPVAL:
                        st[ra] = cl.Upvals[i >> 23].Value;
                        continue;

                    case LuaOpCode.GETGLOBAL:
                    {
                        var key = K[i >> 14];
                        var env = cl.Env;
                        var v = env.Get(key);
                        if (v.Type == LuaType.Nil && env.Metatable != null)
                            v = GetTable(LuaValue.Of(env), key, -1);
                        stack[ra] = v;
                        continue;
                    }

                    case LuaOpCode.GETTABLE:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        LuaValue t = st[@base + b];
                        LuaValue key = c >= 256 ? K[c - 256] : st[@base + c];
                        if (t.Type == LuaType.Table)
                        {
                            var h = (LuaTable)t.O;
                            var v = h.Get(key);
                            if (v.Type != LuaType.Nil || h.Metatable == null) { st[ra] = v; continue; }
                        }
                        var res = GetTable(t, key, @base + b);
                        stack[ra] = res;
                        continue;
                    }

                    case LuaOpCode.SETGLOBAL:
                    {
                        var key = K[i >> 14];
                        var env = cl.Env;
                        if (env.Metatable == null) env.Set(key, st[ra]);
                        else SetTable(LuaValue.Of(env), key, st[ra], -1);
                        continue;
                    }

                    case LuaOpCode.SETUPVAL:
                        cl.Upvals[i >> 23].Value = st[ra];
                        continue;

                    case LuaOpCode.SETTABLE:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        LuaValue t = st[ra];
                        LuaValue key = b >= 256 ? K[b - 256] : st[@base + b];
                        LuaValue val = c >= 256 ? K[c - 256] : st[@base + c];
                        if (t.Type == LuaType.Table && ((LuaTable)t.O).Metatable == null)
                        {
                            if (key.Type == LuaType.Nil) throw RtError("table index is nil");
                            if (key.Type == LuaType.Number && double.IsNaN(key.N)) throw RtError("table index is NaN");
                            ((LuaTable)t.O).Set(key, val);
                            continue;
                        }
                        SetTable(t, key, val, ra);
                        continue;
                    }

                    case LuaOpCode.NEWTABLE:
                        st[ra] = LuaValue.Of(new LuaTable(LuaTable.Fb2Int((int)(i >> 23)), LuaTable.Fb2Int((int)((i >> 14) & 0x1FF))));
                        continue;

                    case LuaOpCode.SELF:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        LuaValue t = st[@base + b];
                        LuaValue key = c >= 256 ? K[c - 256] : st[@base + c];
                        st[ra + 1] = t;
                        LuaValue v;
                        if (t.Type == LuaType.Table)
                        {
                            var h = (LuaTable)t.O;
                            v = h.Get(key);
                            if (v.Type == LuaType.Nil && h.Metatable != null) v = GetTable(t, key, @base + b);
                        }
                        else v = GetTable(t, key, @base + b);
                        stack[ra] = v;
                        continue;
                    }

                    case LuaOpCode.ADD:
                    case LuaOpCode.SUB:
                    case LuaOpCode.MUL:
                    case LuaOpCode.DIV:
                    case LuaOpCode.MOD:
                    case LuaOpCode.POW:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        LuaValue rb = b >= 256 ? K[b - 256] : st[@base + b];
                        LuaValue rc = c >= 256 ? K[c - 256] : st[@base + c];
                        var op = (LuaOpCode)(i & 0x3F);
                        if (rb.Type == LuaType.Number && rc.Type == LuaType.Number)
                        {
                            st[ra] = LuaValue.Of(ArithNumbers(op, rb.N, rc.N));
                            continue;
                        }
                        var res = Arith(op, rb, rc, b >= 256 ? -1 : @base + b, c >= 256 ? -1 : @base + c);
                        stack[ra] = res;
                        continue;
                    }

                    case LuaOpCode.UNM:
                    {
                        int b = (int)(i >> 23);
                        LuaValue rb = st[@base + b];
                        if (rb.Type == LuaType.Number) { st[ra] = LuaValue.Of(-rb.N); continue; }
                        var res = Arith(LuaOpCode.UNM, rb, rb, @base + b, @base + b);
                        stack[ra] = res;
                        continue;
                    }

                    case LuaOpCode.NOT:
                        st[ra] = st[@base + (int)(i >> 23)].IsFalsy ? LuaValue.True : LuaValue.False;
                        continue;

                    case LuaOpCode.LEN:
                    {
                        int b = (int)(i >> 23);
                        LuaValue rb = st[@base + b];
                        if (rb.Type == LuaType.Table) { st[ra] = LuaValue.Of(((LuaTable)rb.O).Length); continue; }
                        if (rb.Type == LuaType.String) { st[ra] = LuaValue.Of(((string)rb.O).Length); continue; }
                        var tm = GetMetamethod(rb, TmLen);
                        if (tm.IsNil) throw TypeError(rb, "get length of", @base + b);
                        var res = CallTMRes(tm, rb, LuaValue.Nil);
                        stack[ra] = res;
                        continue;
                    }

                    case LuaOpCode.CONCAT:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        Concat(@base, c - b + 1, c);
                        st = stack;
                        st[ra] = st[@base + b];
                        continue;
                    }

                    case LuaOpCode.JMP:
                        pc += (int)(i >> 14) - 131071;
                        continue;

                    case LuaOpCode.EQ:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        LuaValue rb = b >= 256 ? K[b - 256] : st[@base + b];
                        LuaValue rc = c >= 256 ? K[c - 256] : st[@base + c];
                        bool eq;
                        if (rb.Type != rc.Type) eq = false;
                        else if (rb.Type == LuaType.Number) eq = rb.N == rc.N;
                        else if (rb.Type == LuaType.Table || rb.Type == LuaType.UserData) eq = EqualValues(rb, rc);
                        else eq = rb.Equals(rc);
                        if (eq == (A != 0)) pc += (int)(code[pc] >> 14) - 131071;
                        pc++;
                        continue;
                    }

                    case LuaOpCode.LT:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        LuaValue rb = b >= 256 ? K[b - 256] : st[@base + b];
                        LuaValue rc = c >= 256 ? K[c - 256] : st[@base + c];
                        bool lt = rb.Type == LuaType.Number && rc.Type == LuaType.Number ? rb.N < rc.N : LessThanValues(rb, rc);
                        if (lt == (A != 0)) pc += (int)(code[pc] >> 14) - 131071;
                        pc++;
                        continue;
                    }

                    case LuaOpCode.LE:
                    {
                        int b = (int)(i >> 23), c = (int)((i >> 14) & 0x1FF);
                        LuaValue rb = b >= 256 ? K[b - 256] : st[@base + b];
                        LuaValue rc = c >= 256 ? K[c - 256] : st[@base + c];
                        bool le = rb.Type == LuaType.Number && rc.Type == LuaType.Number ? rb.N <= rc.N : LessEqualValues(rb, rc);
                        if (le == (A != 0)) pc += (int)(code[pc] >> 14) - 131071;
                        pc++;
                        continue;
                    }

                    case LuaOpCode.TEST:
                        if (st[ra].IsFalsy != (((i >> 14) & 0x1FF) != 0)) pc += (int)(code[pc] >> 14) - 131071;
                        pc++;
                        continue;

                    case LuaOpCode.TESTSET:
                    {
                        LuaValue rb = st[@base + (int)(i >> 23)];
                        if (rb.IsFalsy != (((i >> 14) & 0x1FF) != 0))
                        {
                            st[ra] = rb;
                            pc += (int)(code[pc] >> 14) - 131071;
                        }
                        pc++;
                        continue;
                    }

                    case LuaOpCode.CALL:
                    {
                        int b = (int)(i >> 23);
                        int nresults = (int)((i >> 14) & 0x1FF) - 1;
                        if (b != 0) top = ra + b;
                        var f = ResolveCallable(ra);
                        if (f is LuaClosure ncl)
                        {
                            PushLuaFrame(ra, ncl, nresults);
                            goto reentry;
                        }
                        CallHost(ra, (LuaHostFunction)f, nresults);
                        if (nresults >= 0) top = ci.Top;
                        continue;
                    }

                    case LuaOpCode.TAILCALL:
                    {
                        int b = (int)(i >> 23);
                        if (b != 0) top = ra + b;
                        var f = ResolveCallable(ra);
                        if (f is LuaClosure ncl)
                        {
                            if (openUpvals != null) CloseUpvalues(@base);
                            int func = ci.Func;
                            int n = top - ra;
                            st = stack;
                            for (int j = 0; j < n; j++) st[func + j] = st[ra + j];
                            top = func + n;
                            int nres = ci.NResults;
                            bool entry = ci.IsEntry;
                            ciCount--;
                            PushLuaFrame(func, ncl, nres);
                            cis[ciCount - 1].IsEntry = entry;
                            goto reentry;
                        }
                        CallHost(ra, (LuaHostFunction)f, -1);
                        continue;                                   // the next instruction is RETURN A 0
                    }

                    case LuaOpCode.RETURN:
                    {
                        int b = (int)(i >> 23);
                        if (b != 0) top = ra + b - 1;
                        if (openUpvals != null) CloseUpvalues(@base);
                        int res = ci.Func;
                        int wanted = ci.NResults;
                        int n = top - ra;
                        st = stack;
                        int j = 0;
                        if (wanted < 0)
                        {
                            for (; j < n; j++) st[res + j] = st[ra + j];
                            top = res + n;
                        }
                        else
                        {
                            for (; j < wanted && j < n; j++) st[res + j] = st[ra + j];
                            for (; j < wanted; j++) st[res + j] = LuaValue.Nil;
                            top = res + wanted;
                        }
                        bool entry = ci.IsEntry;
                        ci.Closure = null;
                        ciCount--;
                        if (entry) return;
                        if (wanted >= 0) top = cis[ciCount - 1].Top;
                        goto reentry;
                    }

                    case LuaOpCode.FORLOOP:
                    {
                        double step = st[ra + 2].N;
                        double idx = st[ra].N + step;
                        double limit = st[ra + 1].N;
                        if (step > 0 ? idx <= limit : limit <= idx)
                        {
                            pc += (int)(i >> 14) - 131071;
                            var v = LuaValue.Of(idx);
                            st[ra] = v;
                            st[ra + 3] = v;
                        }
                        continue;
                    }

                    case LuaOpCode.FORPREP:
                    {
                        if (!st[ra].TryGetNumber(out double init)) throw RtError("'for' initial value must be a number");
                        if (!st[ra + 1].TryGetNumber(out double limit)) throw RtError("'for' limit must be a number");
                        if (!st[ra + 2].TryGetNumber(out double step)) throw RtError("'for' step must be a number");
                        st[ra + 1] = LuaValue.Of(limit);
                        st[ra + 2] = LuaValue.Of(step);
                        st[ra] = LuaValue.Of(init - step);
                        pc += (int)(i >> 14) - 131071;
                        continue;
                    }

                    case LuaOpCode.TFORLOOP:
                    {
                        int nres = (int)((i >> 14) & 0x1FF);
                        int cb = ra + 3;
                        LuaValue fn = st[ra];
                        if (ReferenceEquals(fn.O, LuaStdLib.NextFunction) && st[ra + 1].Type == LuaType.Table)
                        {
                            // pairs: next(t, k) without a host call
                            var key = st[ra + 2];
                            if (((LuaTable)st[ra + 1].O).Next(ref key, out var val))
                            {
                                st[cb] = key;
                                if (nres > 1) st[cb + 1] = val;
                                for (int j = 2; j < nres; j++) st[cb + j] = LuaValue.Nil;
                                st[cb - 1] = key;
                                pc += (int)(code[pc] >> 14) - 131071;
                            }
                            else
                            {
                                for (int j = 0; j < nres; j++) st[cb + j] = LuaValue.Nil;
                            }
                            pc++;
                            continue;
                        }
                        if (ReferenceEquals(fn.O, LuaStdLib.IpairsAuxFunction) && st[ra + 1].Type == LuaType.Table && st[ra + 2].Type == LuaType.Number)
                        {
                            int k = LuaConv.Number2Int(st[ra + 2].N) + 1;
                            var val = ((LuaTable)st[ra + 1].O).GetInt(k);
                            if (val.Type != LuaType.Nil)
                            {
                                var kv = LuaValue.Of(k);
                                st[cb] = kv;
                                if (nres > 1) st[cb + 1] = val;
                                for (int j = 2; j < nres; j++) st[cb + j] = LuaValue.Nil;
                                st[cb - 1] = kv;
                                pc += (int)(code[pc] >> 14) - 131071;
                            }
                            else
                            {
                                for (int j = 0; j < nres; j++) st[cb + j] = LuaValue.Nil;
                            }
                            pc++;
                            continue;
                        }
                        st[cb + 2] = st[ra + 2];
                        st[cb + 1] = st[ra + 1];
                        st[cb] = fn;
                        top = cb + 3;
                        CallValue(cb, nres);
                        top = ci.Top;
                        st = stack;
                        if (st[cb].Type != LuaType.Nil)
                        {
                            st[cb - 1] = st[cb];
                            pc += (int)(code[pc] >> 14) - 131071;
                        }
                        pc++;
                        continue;
                    }

                    case LuaOpCode.SETLIST:
                    {
                        int n = (int)(i >> 23);
                        int c = (int)((i >> 14) & 0x1FF);
                        if (n == 0)
                        {
                            n = top - ra - 1;
                            top = ci.Top;
                        }
                        if (c == 0) c = (int)code[pc++];
                        if (st[ra].Type != LuaType.Table) throw RtError("SETLIST target is not a table");
                        var h = (LuaTable)st[ra].O;
                        int last = (c - 1) * FieldsPerFlush + n;
                        if (last > h.ArraySize) h.ResizeArray(last);
                        for (; n > 0; n--) h.SetInt(last--, st[ra + n]);
                        continue;
                    }

                    case LuaOpCode.CLOSE:
                        CloseUpvalues(ra);
                        continue;

                    case LuaOpCode.CLOSURE:
                    {
                        var np = proto.Protos[i >> 14];
                        var ncl = new LuaClosure(np, cl.Env);
                        for (int j = 0; j < np.NumUpvalues; j++)
                        {
                            uint pi = code[pc++];
                            int pb = (int)(pi >> 23);
                            if ((LuaOpCode)(pi & 0x3F) == LuaOpCode.GETUPVAL) ncl.Upvals[j] = cl.Upvals[pb];
                            else ncl.Upvals[j] = FindUpval(@base + pb);
                        }
                        ci.SavedPc = pc;
                        st[ra] = LuaValue.Of(ncl);
                        continue;
                    }

                    case LuaOpCode.VARARG:
                    {
                        int b = (int)(i >> 23) - 1;
                        int n = ci.Base - ci.Func - proto.NumParams - 1;
                        if (b < 0)
                        {
                            EnsureStack(ra + n + MinStack);
                            st = stack;
                            b = n;
                            top = ra + n;
                        }
                        for (int j = 0; j < b; j++) st[ra + j] = j < n ? st[ci.Base - n + j] : LuaValue.Nil;
                        continue;
                    }

                    default:
                        throw RtError("bad opcode " + (i & 0x3F));
                }
            }
        }

        // ================================================================================================================
        // Metamethods and operators
        // ================================================================================================================

        internal static readonly LuaValue TmIndex = LuaValue.Of("__index");
        internal static readonly LuaValue TmNewIndex = LuaValue.Of("__newindex");
        internal static readonly LuaValue TmCall = LuaValue.Of("__call");
        internal static readonly LuaValue TmEq = LuaValue.Of("__eq");
        internal static readonly LuaValue TmLt = LuaValue.Of("__lt");
        internal static readonly LuaValue TmLe = LuaValue.Of("__le");
        internal static readonly LuaValue TmLen = LuaValue.Of("__len");
        internal static readonly LuaValue TmConcat = LuaValue.Of("__concat");
        internal static readonly LuaValue TmUnm = LuaValue.Of("__unm");
        internal static readonly LuaValue TmToString = LuaValue.Of("__tostring");
        internal static readonly LuaValue TmMetatable = LuaValue.Of("__metatable");
        static readonly LuaValue[] ArithEvents =
        {
            LuaValue.Of("__add"), LuaValue.Of("__sub"), LuaValue.Of("__mul"), LuaValue.Of("__div"), LuaValue.Of("__mod"), LuaValue.Of("__pow"), TmUnm,
        };

        /// <summary>The metatable of a value (tables and userdata: their own; strings: <see cref="StringMetatable"/>).</summary>
        public LuaTable GetMetatable(LuaValue v)
        {
            switch (v.Type)
            {
                case LuaType.Table: return ((LuaTable)v.O).Metatable;
                case LuaType.UserData: return ((LuaUserData)v.O).Metatable;
                case LuaType.String: return StringMetatable;
                default: return null;
            }
        }

        internal LuaValue GetMetamethod(in LuaValue v, in LuaValue ev)
        {
            var mt = GetMetatable(v);
            return mt == null ? LuaValue.Nil : mt.Get(ev);
        }

        /// <summary>callTMres: f(a, b) → first result.</summary>
        internal LuaValue CallTMRes(in LuaValue f, in LuaValue a, in LuaValue b)
        {
            int func = top;
            EnsureStack(func + 3 + MinStack);
            stack[func] = f;
            stack[func + 1] = a;
            stack[func + 2] = b;
            top = func + 3;
            CallValue(func, 1);
            var r = stack[func];
            top = func;
            return r;
        }

        /// <summary>callTM: f(a, b, c), no results.</summary>
        void CallTM(in LuaValue f, in LuaValue a, in LuaValue b, in LuaValue c)
        {
            int func = top;
            EnsureStack(func + 4 + MinStack);
            stack[func] = f;
            stack[func + 1] = a;
            stack[func + 2] = b;
            stack[func + 3] = c;
            top = func + 4;
            CallValue(func, 0);
            top = func;
        }

        /// <summary>luaV_gettable. <paramref name="tIndex"/> is the stack slot of <paramref name="t"/> (for error names) or −1.</summary>
        internal LuaValue GetTable(LuaValue t, LuaValue key, int tIndex)
        {
            for (int loop = 0; loop < MaxTagLoop; loop++)
            {
                LuaValue tm;
                if (t.Type == LuaType.Table)
                {
                    var h = (LuaTable)t.O;
                    var res = h.Get(key);
                    if (res.Type != LuaType.Nil) return res;
                    var mt = h.Metatable;
                    if (mt == null) return res;
                    tm = mt.Get(TmIndex);
                    if (tm.IsNil) return res;
                }
                else
                {
                    tm = GetMetamethod(t, TmIndex);
                    if (tm.IsNil) throw TypeError(t, "index", loop == 0 ? tIndex : -1);
                }
                if (tm.Type == LuaType.Function) return CallTMRes(tm, t, key);
                t = tm;
            }
            throw RtError("loop in gettable");
        }

        /// <summary>luaV_settable.</summary>
        internal void SetTable(LuaValue t, LuaValue key, LuaValue val, int tIndex)
        {
            for (int loop = 0; loop < MaxTagLoop; loop++)
            {
                LuaValue tm;
                if (t.Type == LuaType.Table)
                {
                    var h = (LuaTable)t.O;
                    var mt = h.Metatable;
                    if (mt == null) { SetRaw(h, key, val); return; }
                    var old = h.Get(key);
                    if (old.Type != LuaType.Nil) { SetRaw(h, key, val); return; }
                    tm = mt.Get(TmNewIndex);
                    if (tm.IsNil) { SetRaw(h, key, val); return; }
                    TouchRaw(h, key);                                 // luaH_set made the slot before the TM check
                }
                else
                {
                    tm = GetMetamethod(t, TmNewIndex);
                    if (tm.IsNil) throw TypeError(t, "index", loop == 0 ? tIndex : -1);
                }
                if (tm.Type == LuaType.Function)
                {
                    CallTM(tm, t, key, val);
                    return;
                }
                t = tm;
            }
            throw RtError("loop in settable");
        }

        void SetRaw(LuaTable h, in LuaValue key, in LuaValue val)
        {
            try { h.Set(key, val); }
            catch (LuaException e) when (e.LuaTraceback == null && (e.Message == "table index is nil" || e.Message == "table index is NaN")) { throw RtError(e.Message); }
        }

        void TouchRaw(LuaTable h, in LuaValue key)
        {
            try { h.Touch(key); }
            catch (LuaException e) when (e.LuaTraceback == null && (e.Message == "table index is nil" || e.Message == "table index is NaN")) { throw RtError(e.Message); }
        }

        static double ArithNumbers(LuaOpCode op, double x, double y)
        {
            switch (op)
            {
                case LuaOpCode.ADD: return x + y;
                case LuaOpCode.SUB: return x - y;
                case LuaOpCode.MUL: return x * y;
                case LuaOpCode.DIV: return x / y;
                case LuaOpCode.MOD: return x - Math.Floor(x / y) * y;
                case LuaOpCode.POW: return Math.Pow(x, y);
                case LuaOpCode.UNM: return -x;
                default: return 0;
            }
        }

        /// <summary>luaV_arith (Arith in lvm.c): string coercion, then the __add.. metamethods, then the error.</summary>
        LuaValue Arith(LuaOpCode op, in LuaValue rb, in LuaValue rc, int ib, int ic)
        {
            if (rb.TryGetNumber(out double x) && rc.TryGetNumber(out double y)) return LuaValue.Of(ArithNumbers(op, x, y));
            var ev = ArithEvents[op - LuaOpCode.ADD];
            var tm = GetMetamethod(rb, ev);
            if (tm.IsNil) tm = GetMetamethod(rc, ev);
            if (!tm.IsNil) return CallTMRes(tm, rb, rc);
            if (!rb.TryGetNumber(out _)) throw TypeError(rb, "perform arithmetic on", ib);
            throw TypeError(rc, "perform arithmetic on", ic);
        }

        /// <summary>get_compTM: the metamethod both metatables agree on.</summary>
        LuaValue GetCompTM(LuaTable mt1, LuaTable mt2, in LuaValue ev)
        {
            if (mt1 == null) return LuaValue.Nil;
            var tm1 = mt1.Get(ev);
            if (tm1.IsNil) return LuaValue.Nil;
            if (mt1 == mt2) return tm1;
            if (mt2 == null) return LuaValue.Nil;
            var tm2 = mt2.Get(ev);
            if (tm2.IsNil) return LuaValue.Nil;
            return tm1.Equals(tm2) ? tm1 : LuaValue.Nil;
        }

        /// <summary>luaV_equalval (with __eq for tables / userdata).</summary>
        internal bool EqualValues(in LuaValue a, in LuaValue b)
        {
            if (a.Type != b.Type) return false;
            if (a.Type != LuaType.Table && a.Type != LuaType.UserData) return a.Equals(b);
            if (ReferenceEquals(a.O, b.O)) return true;
            var tm = GetCompTM(GetMetatable(a), GetMetatable(b), TmEq);
            if (tm.IsNil) return false;
            return CallTMRes(tm, a, b).IsTruthy;
        }

        /// <summary>call_orderTM: −1 when there is no common metamethod.</summary>
        int CallOrderTM(in LuaValue a, in LuaValue b, in LuaValue ev)
        {
            var tm1 = GetMetamethod(a, ev);
            if (tm1.IsNil) return -1;
            var tm2 = GetMetamethod(b, ev);
            if (!tm1.Equals(tm2)) return -1;
            return CallTMRes(tm1, a, b).IsTruthy ? 1 : 0;
        }

        /// <summary>luaV_lessthan.</summary>
        internal bool LessThanValues(in LuaValue a, in LuaValue b)
        {
            if (a.Type != b.Type) throw OrderError(a, b);
            if (a.Type == LuaType.Number) return a.N < b.N;
            if (a.Type == LuaType.String) return string.CompareOrdinal((string)a.O, (string)b.O) < 0;
            int res = CallOrderTM(a, b, TmLt);
            if (res != -1) return res != 0;
            throw OrderError(a, b);
        }

        /// <summary>lessequal (lvm.c).</summary>
        internal bool LessEqualValues(in LuaValue a, in LuaValue b)
        {
            if (a.Type != b.Type) throw OrderError(a, b);
            if (a.Type == LuaType.Number) return a.N <= b.N;
            if (a.Type == LuaType.String) return string.CompareOrdinal((string)a.O, (string)b.O) <= 0;
            int res = CallOrderTM(a, b, TmLe);
            if (res != -1) return res != 0;
            res = CallOrderTM(b, a, TmLt);
            if (res != -1) return res == 0;
            throw OrderError(a, b);
        }

        LuaException OrderError(in LuaValue a, in LuaValue b)
        {
            string t1 = a.TypeName, t2 = b.TypeName;
            return t1[2] == t2[2] ? RtError("attempt to compare two " + t1 + " values") : RtError("attempt to compare " + t1 + " with " + t2);
        }

        /// <summary>luaV_concat over registers base+last-total+1 .. base+last; the result lands in the first of them.</summary>
        void Concat(int @base, int total, int last)
        {
            do
            {
                int topIdx = @base + last + 1;
                int n = 2;
                var st = stack;
                LuaValue v2 = st[topIdx - 2], v1 = st[topIdx - 1];
                if (!(v2.Type == LuaType.String || v2.Type == LuaType.Number) || !(v1.Type == LuaType.String || v1.Type == LuaType.Number))
                {
                    var tm = GetMetamethod(v2, TmConcat);
                    if (tm.IsNil) tm = GetMetamethod(v1, TmConcat);
                    if (tm.IsNil)
                    {
                        bool firstOk = v2.Type == LuaType.String || v2.Type == LuaType.Number;
                        throw TypeError(firstOk ? v1 : v2, "concatenate", firstOk ? topIdx - 1 : topIdx - 2);
                    }
                    var r = CallTMRes(tm, v2, v1);
                    stack[topIdx - 2] = r;
                }
                else
                {
                    // collect as many string / number values as possible
                    while (n < total)
                    {
                        var v = st[topIdx - n - 1];
                        if (v.Type != LuaType.String && v.Type != LuaType.Number) break;
                        n++;
                    }
                    string result;
                    if (n == 2)
                    {
                        result = string.Concat(ToStr(v2), ToStr(v1));
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        for (int k = n; k > 0; k--) sb.Append(ToStr(st[topIdx - k]));
                        result = sb.ToString();
                    }
                    st[topIdx - n] = LuaValue.Of(result);
                }
                total -= n - 1;
                last -= n - 1;
            } while (total > 1);
        }

        static string ToStr(in LuaValue v) => v.Type == LuaType.String ? (string)v.O : LuaConv.FormatNumber(v.N);

        /// <summary>luaB_tostring's conversion (__tostring, then the default formats).</summary>
        internal string ToStringValue(in LuaValue v)
        {
            var tm = GetMetamethod(v, TmToString);
            if (!tm.IsNil)
            {
                var r = CallTMRes(tm, v, LuaValue.Nil);
                if (!r.TryGetString(out string s)) throw ErrorWithWhere("'__tostring' must return a string", 1);
                return s;
            }
            return v.ToString();
        }

        // ================================================================================================================
        // Errors and debug information
        // ================================================================================================================

        /// <summary>luaG_runerror: the message with the position of the running Lua function ("chunk:line: ").</summary>
        internal LuaException RtError(string msg)
        {
            if (ciCount > 0)
            {
                var ci = cis[ciCount - 1];
                if (ci.Closure != null)
                {
                    var p = ci.Closure.Proto;
                    msg = LuaConv.ChunkId(p.Source) + ":" + p.GetLine(ci.SavedPc - 1) + ": " + msg;
                }
            }
            return new LuaException(msg);
        }

        /// <summary>luaL_where(level): "chunk:line: " of the function <paramref name="level"/> levels up (1 = the caller of the running host function), or "".</summary>
        internal string Where(int level)
        {
            int idx = ciCount - 1 - level;
            if (idx < 0 || idx >= ciCount) return "";
            var ci = cis[idx];
            if (ci.Closure == null) return "";
            var p = ci.Closure.Proto;
            int line = p.GetLine(ci.SavedPc - 1);
            return line > 0 ? LuaConv.ChunkId(p.Source) + ":" + line + ": " : "";
        }

        /// <summary>luaL_error from a host function: the message prefixed with <see cref="Where"/>(level).</summary>
        internal LuaException ErrorWithWhere(string msg, int level) => new LuaException(Where(level) + msg);

        /// <summary>luaG_typeerror: "attempt to {op} {global|local|field|upvalue|method} 'name' (a {type} value)".</summary>
        LuaException TypeError(in LuaValue v, string op, int stackIndex)
        {
            string kind = null, name = null;
            if (stackIndex >= 0 && ciCount > 0)
            {
                var ci = cis[ciCount - 1];
                if (ci.Closure != null && stackIndex >= ci.Base && stackIndex < ci.Top)
                    kind = GetObjName(ci.Closure.Proto, ci.SavedPc - 1, stackIndex - ci.Base, out name);
            }
            return kind != null
                ? RtError("attempt to " + op + " " + kind + " '" + name + "' (a " + v.TypeName + " value)")
                : RtError("attempt to " + op + " a " + v.TypeName + " value");
        }

        /// <summary>getobjname (ldebug.c): how register <paramref name="reg"/> got its value at <paramref name="pc"/>.</summary>
        static string GetObjName(LuaProto p, int pc, int reg, out string name)
        {
            name = p.GetLocalName(reg + 1, pc);
            if (name != null) return "local";
            uint i = SymbExec(p, pc, reg);
            switch ((LuaOpCode)(i & 0x3F))
            {
                case LuaOpCode.GETGLOBAL:
                    name = p.K[i >> 14].AsString ?? "?";
                    return "global";
                case LuaOpCode.MOVE:
                {
                    int a = (int)((i >> 6) & 0xFF), b = (int)(i >> 23);
                    if (b < a) return GetObjName(p, pc, b, out name);
                    break;
                }
                case LuaOpCode.GETTABLE:
                    name = KName(p, (int)((i >> 14) & 0x1FF));
                    return "field";
                case LuaOpCode.GETUPVAL:
                {
                    int u = (int)(i >> 23);
                    name = p.UpvalueNames != null && u < p.UpvalueNames.Length ? p.UpvalueNames[u] : "?";
                    return "upvalue";
                }
                case LuaOpCode.SELF:
                    name = KName(p, (int)((i >> 14) & 0x1FF));
                    return "method";
            }
            name = null;
            return null;
        }

        static string KName(LuaProto p, int c) => c >= 256 && p.K[c - 256].IsString ? p.K[c - 256].AsString : "?";

        /// <summary>symbexec without verification: the last instruction before <paramref name="lastpc"/> that set <paramref name="reg"/>.</summary>
        static uint SymbExec(LuaProto p, int lastpc, int reg)
        {
            var code = p.Code;
            int last = code.Length - 1;
            for (int pc = 0; pc < lastpc && pc < code.Length; pc++)
            {
                uint i = code[pc];
                var op = (LuaOpCode)(i & 0x3F);
                int a = (int)((i >> 6) & 0xFF);
                int b = (int)(i >> 23);
                int c = (int)((i >> 14) & 0x1FF);
                switch (op)
                {
                    case LuaOpCode.LOADNIL:
                        if (a <= reg && reg <= b) last = pc;
                        break;
                    case LuaOpCode.TFORLOOP:
                        if (reg >= a + 2) last = pc;
                        break;
                    case LuaOpCode.CALL:
                    case LuaOpCode.TAILCALL:
                        if (reg >= a) last = pc;
                        break;
                    case LuaOpCode.JMP:
                    {
                        int dest = pc + 1 + ((int)(i >> 14) - 131071);
                        if (pc < dest && dest <= lastpc) pc += (int)(i >> 14) - 131071;
                        break;
                    }
                    case LuaOpCode.CLOSURE:
                        if (a == reg) last = pc;
                        pc += p.Protos[i >> 14].NumUpvalues;
                        break;
                    case LuaOpCode.SETLIST:
                        if (c == 0) pc++;
                        break;
                    case LuaOpCode.SELF:
                        if (reg == a || reg == a + 1) last = pc;
                        break;
                    case LuaOpCode.SETGLOBAL:
                    case LuaOpCode.SETUPVAL:
                    case LuaOpCode.SETTABLE:
                    case LuaOpCode.EQ:
                    case LuaOpCode.LT:
                    case LuaOpCode.LE:
                    case LuaOpCode.TEST:
                    case LuaOpCode.RETURN:
                    case LuaOpCode.CLOSE:
                        break;
                    case LuaOpCode.VARARG:
                        if (reg >= a && (b == 0 || reg < a + b - 1)) last = pc;
                        break;
                    default:
                        if (a == reg) last = pc;
                        break;
                }
            }
            return code[last];
        }

        /// <summary>The Lua call stack from the innermost frame down to frame <paramref name="downTo"/>, one line per frame.</summary>
        string Traceback(int downTo)
        {
            var sb = new StringBuilder("stack traceback:");
            int shown = 0;
            for (int k = ciCount - 1; k >= Math.Max(downTo, 0); k--)
            {
                var ci = cis[k];
                if (shown++ > 22) { sb.Append("\n\t..."); break; }
                if (ci.Closure != null)
                {
                    var p = ci.Closure.Proto;
                    string src = LuaConv.ChunkId(p.Source);
                    sb.Append("\n\t").Append(src).Append(':').Append(p.GetLine(ci.SavedPc - 1)).Append(": in ");
                    if (p.LineDefined == 0) sb.Append("main chunk");
                    else sb.Append("function <").Append(src).Append(':').Append(p.LineDefined).Append('>');
                }
                else
                {
                    sb.Append("\n\t[C]: in function '").Append(ci.Host?.Name ?? "?").Append('\'');
                }
            }
            return sb.ToString();
        }

        // ================================================================================================================
        // Protected calls (used by pcall / xpcall)
        // ================================================================================================================

        /// <summary>pcall: calls args[0] with args[1..]; true + results, or false + the error object.</summary>
        internal LuaReturn PCall(LuaArgs args)
        {
            args.CheckAny(0);
            int func = args.Base;
            int savedCi = ciCount, savedNC = nCcalls;
            top = func + args.Count;
            try
            {
                CallValue(func, -1);
            }
            catch (LuaException e) when (!(e is LuaBudgetExceededException))
            {
                CloseUpvalues(func);
                ciCount = savedCi;
                nCcalls = savedNC;
                top = func;
                return LuaReturn.Of(LuaValue.False, e.Value);
            }
            int n = top - func;
            stack[func - 1] = LuaValue.True;
            return LuaReturn.StackRange(func - 1, n + 1);
        }

        /// <summary>xpcall(f, handler): f() protected; on error, false + handler(error object).</summary>
        internal LuaReturn XPCall(LuaArgs args)
        {
            args.CheckAny(1);
            int func = args.Base + args.Count;
            var f = args[0];
            var handler = args[1];
            int savedCi = ciCount, savedNC = nCcalls;
            EnsureStack(func + 1 + MinStack);
            stack[func] = f;
            top = func + 1;
            try
            {
                CallValue(func, -1);
            }
            catch (LuaException e) when (!(e is LuaBudgetExceededException))
            {
                CloseUpvalues(func);
                ciCount = savedCi;
                nCcalls = savedNC;
                top = func;
                LuaValue r;
                try
                {
                    r = CallTMRes(handler, e.Value, LuaValue.Nil);
                }
                catch (LuaException e2) when (!(e2 is LuaBudgetExceededException))
                {
                    CloseUpvalues(func);
                    ciCount = savedCi;
                    nCcalls = savedNC;
                    top = func;
                    r = e2.Value;
                }
                return LuaReturn.Of(LuaValue.False, r);
            }
            int n = top - func;
            stack[func - 1] = LuaValue.True;
            return LuaReturn.StackRange(func - 1, n + 1);
        }

        /// <summary>The environment of the Lua function <paramref name="level"/> levels up (getfenv(level)); null for host frames.</summary>
        internal LuaClosure ClosureAtLevel(int level)
        {
            int idx = ciCount - 1 - level;
            if (idx < 0 || idx >= ciCount) return null;
            return cis[idx].Closure;
        }

        internal bool HasLevel(int level) => level >= 0 && ciCount - 1 - level >= 0;
    }
}
