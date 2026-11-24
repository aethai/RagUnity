using System;
using System.Globalization;
using System.Text;

namespace Ragnarok.Data.Lua51
{
    /// <summary>
    /// The Lua 5.1 standard library subset (lbaselib / lmathlib / lstrlib / ltablib semantics):
    /// <list type="bullet">
    /// <item>base: assert, collectgarbage (stub), dofile / require (through <see cref="LuaState.ChunkResolver"/>), error,
    /// gcinfo, getfenv, getmetatable, ipairs, load / loadstring (bytecode only), next, pairs, pcall, print (to
    /// <see cref="LuaState.Print"/>), rawequal, rawget, rawset, select, setfenv, setmetatable, tonumber, tostring, type,
    /// unpack, xpcall, _G, _VERSION;</item>
    /// <item>math: abs acos asin atan atan2 ceil cos cosh deg exp floor fmod frexp ldexp log log10 max min mod modf pow rad
    /// random randomseed sin sinh sqrt tan tanh huge pi (random = the MSVC CRT rand() LCG, seed 1: deterministic);</item>
    /// <item>string: byte char find format gfind gmatch gsub len lower match rep reverse sub upper (full Lua patterns), and
    /// the string metatable;</item>
    /// <item>table: concat foreach foreachi getn insert maxn remove setn sort (Lua's quicksort).</item>
    /// </list>
    /// Not provided: coroutine, io, os, debug, package searchers, string.dump.
    /// </summary>
    public static class LuaStdLib
    {
        internal static readonly LuaHostFunction NextFunction = new LuaHostFunction("next", Next);
        internal static readonly LuaHostFunction IpairsAuxFunction = new LuaHostFunction("ipairs_aux", IpairsAux);

        /// <summary>Opens all libraries of this subset into <paramref name="L"/>'s globals.</summary>
        public static void Open(LuaState L)
        {
            var G = L.Globals;
            G.Set("_G", LuaValue.Of(G));
            G.Set("_VERSION", LuaValue.Of("Lua 5.1"));
            Reg(G, "assert", Assert);
            Reg(G, "collectgarbage", a => LuaValue.Of(0));
            Reg(G, "gcinfo", a => LuaValue.Of(0));
            Reg(G, "dofile", DoFile);
            Reg(G, "error", Error);
            Reg(G, "getfenv", GetFEnv);
            Reg(G, "setfenv", SetFEnv);
            Reg(G, "getmetatable", GetMetatable);
            Reg(G, "setmetatable", SetMetatable);
            Reg(G, "ipairs", Ipairs);
            Reg(G, "pairs", Pairs);
            G.Set("next", LuaValue.Of(NextFunction));
            Reg(G, "load", a => LuaReturn.Of(LuaValue.Nil, LuaValue.Of("load: only precompiled chunks are supported")));
            Reg(G, "loadstring", LoadString);
            Reg(G, "pcall", a => a.State.PCall(a));
            Reg(G, "xpcall", a => a.State.XPCall(a));
            Reg(G, "print", Print);
            Reg(G, "rawequal", a => { a.CheckAny(0); a.CheckAny(1); return LuaValue.Of(a[0].Equals(a[1])); });
            Reg(G, "rawget", a => { var t = a.CheckTable(0); a.CheckAny(1); return t.Get(a[1]); });
            Reg(G, "rawset", RawSet);
            Reg(G, "select", Select);
            Reg(G, "tonumber", ToNumber);
            Reg(G, "tostring", a => { a.CheckAny(0); return LuaValue.Of(a.State.ToStringValue(a[0])); });
            Reg(G, "type", a => { a.CheckAny(0); return LuaValue.Of(a[0].TypeName); });
            Reg(G, "unpack", Unpack);
            Reg(G, "require", Require);

            var package = new LuaTable();
            package.Set("loaded", LuaValue.Of(new LuaTable()));
            G.Set("package", LuaValue.Of(package));

            G.Set("math", LuaValue.Of(OpenMath()));
            var str = OpenString();
            G.Set("string", LuaValue.Of(str));
            var smeta = new LuaTable();
            smeta.Set("__index", LuaValue.Of(str));
            L.StringMetatable = smeta;
            G.Set("table", LuaValue.Of(OpenTable()));

            var loaded = package.Get("loaded").AsTable;
            foreach (var lib in new[] { "_G", "math", "string", "table", "package" }) loaded.Set(lib, G.Get(lib));
        }

        static void Reg(LuaTable t, string name, LuaHostFunc f) => t.Set(name, LuaValue.Of(new LuaHostFunction(name, f)));

        // ================================================================================================================
        // base
        // ================================================================================================================

        static LuaReturn Assert(LuaArgs a)
        {
            a.CheckAny(0);
            if (a[0].IsFalsy) throw a.Error(a.OptString(1, "assertion failed!"));
            return LuaReturn.StackRange(a.Base, a.Count);
        }

        static LuaReturn Error(LuaArgs a)
        {
            int level = a.OptInt(1, 1);
            var v = a[0];
            if ((v.IsString || v.IsNumber) && level > 0)
                v = LuaValue.Of(a.State.Where(level) + v.ToString());
            throw new LuaException(v);
        }

        static LuaReturn Print(LuaArgs a)
        {
            var L = a.State;
            if (L.Print == null) return LuaReturn.None;
            var tostring = L.Globals.Get("tostring");
            var sb = new StringBuilder();
            for (int i = 0; i < a.Count; i++)
            {
                var r = L.CallTMRes(tostring, a[i], LuaValue.Nil);
                if (!r.TryGetString(out string s)) throw a.Error("'tostring' must return a string to 'print'");
                if (i > 0) sb.Append('\t');
                sb.Append(s);
            }
            L.Print(sb.ToString());
            return LuaReturn.None;
        }

        static LuaReturn GetMetatable(LuaArgs a)
        {
            a.CheckAny(0);
            var mt = a.State.GetMetatable(a[0]);
            if (mt == null) return LuaValue.Nil;
            var protectedMt = mt.Get(LuaState.TmMetatable);
            return protectedMt.IsNil ? LuaValue.Of(mt) : protectedMt;
        }

        static LuaReturn SetMetatable(LuaArgs a)
        {
            var t = a.CheckTable(0);
            var m = a[1];
            if (!(m.IsNil || m.IsTable)) throw a.ArgError(1, "nil or table expected");
            if (t.Metatable != null && !t.Metatable.Get(LuaState.TmMetatable).IsNil) throw a.Error("cannot change a protected metatable");
            t.Metatable = m.AsTable;
            return a[0];
        }

        static LuaClosure FunctionForEnv(LuaArgs a, out bool isLevelZero)
        {
            isLevelZero = false;
            if (a[0].IsFunction)
            {
                return a[0].AsFunction as LuaClosure;
            }
            int level = a.OptInt(0, 1);
            if (level < 0) throw a.ArgError(0, "level must be non-negative");
            if (level == 0) { isLevelZero = true; return null; }
            if (!a.State.HasLevel(level)) throw a.ArgError(0, "invalid level");
            return a.State.ClosureAtLevel(level);
        }

        static LuaReturn GetFEnv(LuaArgs a)
        {
            var cl = FunctionForEnv(a, out _);
            return LuaValue.Of(cl != null ? cl.Env : a.State.Globals);
        }

        static LuaReturn SetFEnv(LuaArgs a)
        {
            var t = a.CheckTable(1);
            var cl = FunctionForEnv(a, out bool zero);
            if (zero) throw a.Error("'setfenv' cannot change the thread environment in this VM");
            if (cl == null) throw a.Error("'setfenv' cannot change environment of given object");
            cl.Env = t;
            return a[0].IsFunction ? a[0] : LuaValue.Of(cl);
        }

        static LuaReturn Pairs(LuaArgs a)
        {
            a.CheckTable(0);
            return LuaReturn.Of(LuaValue.Of(NextFunction), a[0], LuaValue.Nil);
        }

        static LuaReturn Ipairs(LuaArgs a)
        {
            a.CheckTable(0);
            return LuaReturn.Of(LuaValue.Of(IpairsAuxFunction), a[0], LuaValue.Of(0));
        }

        static LuaReturn Next(LuaArgs a)
        {
            var t = a.CheckTable(0);
            var k = a[1];
            LuaValue v;
            bool more;
            try { more = t.Next(ref k, out v); }
            catch (LuaException e) { throw a.Error(e.Message); }
            return more ? LuaReturn.Of(k, v) : LuaReturn.Of(LuaValue.Nil);
        }

        static LuaReturn IpairsAux(LuaArgs a)
        {
            int i = a.CheckInt(1);
            var t = a.CheckTable(0);
            i++;
            var v = t.GetInt(i);
            return v.IsNil ? LuaReturn.None : LuaReturn.Of(LuaValue.Of(i), v);
        }

        static LuaReturn RawSet(LuaArgs a)
        {
            var t = a.CheckTable(0);
            a.CheckAny(1);
            a.CheckAny(2);
            try { t.Set(a[1], a[2]); }
            catch (LuaException e) { throw a.Error(e.Message); }
            return a[0];
        }

        static LuaReturn Select(LuaArgs a)
        {
            int n = a.Count;
            if (a[0].IsString && a[0].AsString.Length > 0 && a[0].AsString[0] == '#') return LuaValue.Of(n - 1);
            int i = a.CheckInt(0);
            if (i < 0) i = n + i;
            else if (i > n) i = n;
            if (i < 1) throw a.ArgError(0, "index out of range");
            return LuaReturn.StackRange(a.Base + i, n - i);
        }

        static LuaReturn ToNumber(LuaArgs a)
        {
            int b = a.OptInt(1, 10);
            if (b == 10)
            {
                a.CheckAny(0);
                if (a[0].TryGetNumber(out double d)) return LuaValue.Of(d);
                return LuaValue.Nil;
            }
            string s = a.CheckString(0);
            if (b < 2 || b > 36) throw a.ArgError(1, "base out of range");
            if (StrToUL(s, b, out uint n, out int end))
            {
                while (end < s.Length && LuaConv.IsSpace(s[end])) end++;
                if (end == s.Length) return LuaValue.Of((double)n);
            }
            return LuaValue.Nil;
        }

        /// <summary>C strtoul: white space, sign, optional 0x for base 16, digits; overflow saturates.</summary>
        static bool StrToUL(string s, int b, out uint result, out int end)
        {
            result = 0;
            int p = 0;
            while (p < s.Length && LuaConv.IsSpace(s[p])) p++;
            bool neg = false;
            if (p < s.Length && (s[p] == '+' || s[p] == '-')) { neg = s[p] == '-'; p++; }
            if (b == 16 && p + 1 < s.Length && s[p] == '0' && (s[p + 1] == 'x' || s[p + 1] == 'X') && p + 2 < s.Length && DigitValue(s[p + 2]) < 16) p += 2;
            int start = p;
            ulong v = 0;
            bool overflow = false;
            while (p < s.Length)
            {
                int d = DigitValue(s[p]);
                if (d >= b) break;
                v = v * (ulong)b + (ulong)d;
                if (v > uint.MaxValue) { overflow = true; v = uint.MaxValue; }
                p++;
            }
            end = p;
            if (p == start) { end = 0; return false; }
            uint u = overflow ? uint.MaxValue : (uint)v;
            if (neg && !overflow) u = unchecked((uint)-(int)u);
            result = u;
            return true;
        }

        static int DigitValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'z') return c - 'a' + 10;
            if (c >= 'A' && c <= 'Z') return c - 'A' + 10;
            return 99;
        }

        static LuaReturn Unpack(LuaArgs a)
        {
            var t = a.CheckTable(0);
            int i = a.OptInt(1, 1);
            int e = a[2].IsNil ? t.Length : a.CheckInt(2);
            if (i > e) return LuaReturn.None;
            long n = (long)e - i + 1;
            if (n <= 0 || n >= 8000) throw a.Error("too many results to unpack");
            var vals = new LuaValue[n];
            for (int k = 0; k < n; k++) vals[k] = t.GetInt(i + k);
            return LuaReturn.FromArray(vals, (int)n);
        }

        static LuaReturn LoadString(LuaArgs a)
        {
            string s = a.CheckString(0);
            string name = a.OptString(1, s);
            if (s.Length >= 4 && s[0] == '\x1b' && s[1] == 'L' && s[2] == 'u' && s[3] == 'a')
            {
                var bytes = new byte[s.Length];
                for (int i = 0; i < s.Length; i++) bytes[i] = (byte)s[i];
                try
                {
                    return LuaValue.Of(a.State.Load(bytes, "=" + name));
                }
                catch (LuaException e)
                {
                    return LuaReturn.Of(LuaValue.Nil, LuaValue.Of(e.Message));
                }
            }
            return LuaReturn.Of(LuaValue.Nil, LuaValue.Of("[string \"" + name + "\"]: source compilation is not supported (precompiled chunks only)"));
        }

        static LuaReturn RunChunk(LuaArgs a, LuaClosure cl, LuaValue arg, bool passArg)
        {
            var L = a.State;
            int func = L.top;
            L.EnsureStack(func + 2 + 20);
            L.stack[func] = LuaValue.Of(cl);
            if (passArg) L.stack[func + 1] = arg;
            L.top = func + (passArg ? 2 : 1);
            L.CallValue(func, -1);
            return LuaReturn.StackRange(func, L.top - func);
        }

        static LuaClosure LoadByName(LuaArgs a, string name, out string error)
        {
            error = null;
            var resolver = a.State.ChunkResolver;
            byte[] bytes = resolver?.Invoke(name);
            if (bytes == null) { error = "cannot open " + name; return null; }
            return a.State.Load(bytes, "@" + name);
        }

        static LuaReturn DoFile(LuaArgs a)
        {
            string name = a.CheckString(0);
            var cl = LoadByName(a, name, out string err);
            if (cl == null) throw a.Error(err);
            return RunChunk(a, cl, LuaValue.Nil, false);
        }

        static LuaReturn Require(LuaArgs a)
        {
            string name = a.CheckString(0);
            var L = a.State;
            var package = L.Globals.Get("package").AsTable;
            var loaded = package?.Get("loaded").AsTable;
            if (loaded == null) throw a.Error("'package.loaded' must be a table");
            var have = loaded.Get(name);
            if (have.IsTruthy) return have;
            var cl = LoadByName(a, name, out string err);
            if (cl == null) throw a.Error("module '" + name + "' not found:\n\t" + err);
            var r = RunChunk(a, cl, LuaValue.Of(name), true);
            LuaValue result = r.Count > 0 ? L.stack[r.Start] : LuaValue.Nil;
            if (!result.IsNil) loaded.Set(name, result);
            if (loaded.Get(name).IsNil) loaded.Set(name, LuaValue.True);
            return loaded.Get(name);
        }

        // ================================================================================================================
        // math
        // ================================================================================================================

        static LuaTable OpenMath()
        {
            var m = new LuaTable();
            Reg(m, "abs", a => Math.Abs(a.CheckNumber(0)));
            Reg(m, "acos", a => Math.Acos(a.CheckNumber(0)));
            Reg(m, "asin", a => Math.Asin(a.CheckNumber(0)));
            Reg(m, "atan", a => Math.Atan(a.CheckNumber(0)));
            Reg(m, "atan2", a => Math.Atan2(a.CheckNumber(0), a.CheckNumber(1)));
            Reg(m, "ceil", a => Math.Ceiling(a.CheckNumber(0)));
            Reg(m, "cos", a => Math.Cos(a.CheckNumber(0)));
            Reg(m, "cosh", a => Math.Cosh(a.CheckNumber(0)));
            Reg(m, "deg", a => a.CheckNumber(0) / (Math.PI / 180.0));
            Reg(m, "exp", a => Math.Exp(a.CheckNumber(0)));
            Reg(m, "floor", a => Math.Floor(a.CheckNumber(0)));
            LuaHostFunc fmod = a => CFmod(a.CheckNumber(0), a.CheckNumber(1));
            Reg(m, "fmod", fmod);
            Reg(m, "mod", fmod);
            Reg(m, "frexp", a =>
            {
                double x = a.CheckNumber(0);
                Frexp(x, out double mant, out int e);
                return LuaReturn.Of(LuaValue.Of(mant), LuaValue.Of(e));
            });
            Reg(m, "ldexp", a => a.CheckNumber(0) * Math.Pow(2, a.CheckInt(1)));
            Reg(m, "log", a => Math.Log(a.CheckNumber(0)));
            Reg(m, "log10", a => Math.Log10(a.CheckNumber(0)));
            Reg(m, "max", a =>
            {
                double d = a.CheckNumber(0);
                for (int i = 1; i < a.Count; i++) { double x = a.CheckNumber(i); if (x > d) d = x; }
                return d;
            });
            Reg(m, "min", a =>
            {
                double d = a.CheckNumber(0);
                for (int i = 1; i < a.Count; i++) { double x = a.CheckNumber(i); if (x < d) d = x; }
                return d;
            });
            Reg(m, "modf", a =>
            {
                double x = a.CheckNumber(0);
                double ip = double.IsInfinity(x) ? x : Math.Truncate(x);
                double fp = double.IsInfinity(x) ? 0.0 : x - ip;
                return LuaReturn.Of(LuaValue.Of(ip), LuaValue.Of(fp));
            });
            Reg(m, "pow", a => Math.Pow(a.CheckNumber(0), a.CheckNumber(1)));
            Reg(m, "rad", a => a.CheckNumber(0) * (Math.PI / 180.0));
            Reg(m, "sin", a => Math.Sin(a.CheckNumber(0)));
            Reg(m, "sinh", a => Math.Sinh(a.CheckNumber(0)));
            Reg(m, "sqrt", a => Math.Sqrt(a.CheckNumber(0)));
            Reg(m, "tan", a => Math.Tan(a.CheckNumber(0)));
            Reg(m, "tanh", a => Math.Tanh(a.CheckNumber(0)));
            Reg(m, "random", Random);
            Reg(m, "randomseed", a => { a.State.RandSeed = unchecked((uint)a.CheckInt(0)); return LuaReturn.None; });
            m.Set("pi", LuaValue.Of(Math.PI));
            m.Set("huge", LuaValue.Of(double.PositiveInfinity));
            return m;
        }

        /// <summary>C fmod: x − trunc(x / y)·y with the sign of x.</summary>
        static double CFmod(double x, double y) => x % y;

        static void Frexp(double x, out double mant, out int e)
        {
            e = 0;
            if (x == 0 || double.IsNaN(x) || double.IsInfinity(x)) { mant = x; return; }
            long bits = BitConverter.DoubleToInt64Bits(x);
            int exp = (int)((bits >> 52) & 0x7FF);
            if (exp == 0)
            {
                // subnormal: scale up first
                x *= Math.Pow(2, 54);
                bits = BitConverter.DoubleToInt64Bits(x);
                exp = (int)((bits >> 52) & 0x7FF) - 54;
            }
            e = exp - 1022;
            bits = (bits & ~(0x7FFL << 52)) | (1022L << 52);
            mant = BitConverter.Int64BitsToDouble(bits);
        }

        /// <summary>math.random over MSVC's rand(): seed = seed·214013 + 2531011, value = (seed &gt;&gt; 16) &amp; 0x7FFF; RAND_MAX 32767.</summary>
        static LuaReturn Random(LuaArgs a)
        {
            var L = a.State;
            L.RandSeed = unchecked(L.RandSeed * 214013u + 2531011u);
            int rnd = (int)((L.RandSeed >> 16) & 0x7FFF);
            double r = (double)(rnd % 32767) / 32767.0;
            switch (a.Count)
            {
                case 0:
                    return r;
                case 1:
                {
                    int u = a.CheckInt(0);
                    if (u < 1) throw a.ArgError(0, "interval is empty");
                    return Math.Floor(r * u) + 1;
                }
                case 2:
                {
                    int l = a.CheckInt(0);
                    int u = a.CheckInt(1);
                    if (l > u) throw a.ArgError(1, "interval is empty");
                    return Math.Floor(r * ((double)u - l + 1)) + l;
                }
                default:
                    throw a.Error("wrong number of arguments");
            }
        }

        // ================================================================================================================
        // string
        // ================================================================================================================

        static LuaTable OpenString()
        {
            var s = new LuaTable();
            Reg(s, "len", a => a.CheckString(0).Length);
            Reg(s, "sub", StrSub);
            Reg(s, "upper", a => AsciiCase(a.CheckString(0), true));
            Reg(s, "lower", a => AsciiCase(a.CheckString(0), false));
            Reg(s, "rep", a =>
            {
                string str = a.CheckString(0);
                int n = a.CheckInt(1);
                if (n <= 0 || str.Length == 0) return "";
                if ((long)str.Length * n > 256L * 1024 * 1024) throw a.Error("resulting string too large");
                var sb = new StringBuilder(str.Length * n);
                for (int i = 0; i < n; i++) sb.Append(str);
                return sb.ToString();
            });
            Reg(s, "reverse", a =>
            {
                var c = a.CheckString(0).ToCharArray();
                Array.Reverse(c);
                return new string(c);
            });
            Reg(s, "byte", StrByte);
            Reg(s, "char", a =>
            {
                var c = new char[a.Count];
                for (int i = 0; i < a.Count; i++)
                {
                    int v = a.CheckInt(i);
                    if ((uint)v > 255) throw a.ArgError(i, "invalid value");
                    c[i] = (char)v;
                }
                return new string(c);
            });
            Reg(s, "format", StrFormat);
            Reg(s, "find", a => StrFindAux(a, true));
            Reg(s, "match", a => StrFindAux(a, false));
            Reg(s, "gmatch", GMatch);
            Reg(s, "gfind", GMatch);
            Reg(s, "gsub", GSub);
            Reg(s, "dump", a => throw a.Error("unable to dump given function"));
            return s;
        }

        static string AsciiCase(string s, bool upper)
        {
            var c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                char ch = c[i];
                if (upper) { if (ch >= 'a' && ch <= 'z') c[i] = (char)(ch - 32); }
                else if (ch >= 'A' && ch <= 'Z') c[i] = (char)(ch + 32);
            }
            return new string(c);
        }

        static int PosRelat(int pos, int len)
        {
            if (pos < 0) pos += len + 1;
            return pos >= 0 ? pos : 0;
        }

        static LuaReturn StrSub(LuaArgs a)
        {
            string s = a.CheckString(0);
            int l = s.Length;
            int start = PosRelat(a.CheckInt(1), l);
            int end = PosRelat(a.OptInt(2, -1), l);
            if (start < 1) start = 1;
            if (end > l) end = l;
            return start <= end ? s.Substring(start - 1, end - start + 1) : "";
        }

        static LuaReturn StrByte(LuaArgs a)
        {
            string s = a.CheckString(0);
            int l = s.Length;
            int posi = PosRelat(a.OptInt(1, 1), l);
            int pose = PosRelat(a.OptInt(2, posi), l);
            if (posi <= 0) posi = 1;
            if (pose > l) pose = l;
            if (posi > pose) return LuaReturn.None;
            int n = pose - posi + 1;
            if (n == 1) return (double)(byte)s[posi - 1];
            var r = new LuaValue[n];
            for (int i = 0; i < n; i++) r[i] = LuaValue.Of((byte)s[posi + i - 1]);
            return LuaReturn.FromArray(r, n);
        }

        // ---- string.format ----------------------------------------------------------------------------------------

        static LuaReturn StrFormat(LuaArgs a)
        {
            string fmt = a.CheckString(0);
            var sb = new StringBuilder(fmt.Length + 16);
            int arg = 0;
            int p = 0, len = fmt.Length;
            while (p < len)
            {
                char ch = fmt[p++];
                if (ch != '%') { sb.Append(ch); continue; }
                if (p < len && fmt[p] == '%') { sb.Append('%'); p++; continue; }
                arg++;
                int fs = p;
                while (p < len && "-+ #0".IndexOf(fmt[p]) >= 0) p++;
                if (p - fs > 5) throw a.Error("invalid format (repeated flags)");
                string flags = fmt.Substring(fs, p - fs);
                int width = -1, prec = -1;
                int ws = p;
                if (p < len && IsDigit(fmt[p])) p++;
                if (p < len && IsDigit(fmt[p])) p++;
                if (p > ws) width = int.Parse(fmt.Substring(ws, p - ws), CultureInfo.InvariantCulture);
                if (p < len && fmt[p] == '.')
                {
                    p++;
                    int ps = p;
                    if (p < len && IsDigit(fmt[p])) p++;
                    if (p < len && IsDigit(fmt[p])) p++;
                    prec = p > ps ? int.Parse(fmt.Substring(ps, p - ps), CultureInfo.InvariantCulture) : 0;
                }
                if (p < len && IsDigit(fmt[p])) throw a.Error("invalid format (width or precision too long)");
                char conv = p < len ? fmt[p++] : '\0';
                switch (conv)
                {
                    case 'c':
                    {
                        int v = TruncToInt(a.CheckNumber(arg));
                        sb.Append(Pad(((char)(byte)v).ToString(), flags, width, false));
                        break;
                    }
                    case 'd':
                    case 'i':
                    {
                        long v = TruncToInt(a.CheckNumber(arg));
                        bool neg = v < 0;
                        string digits = (neg ? -v : v).ToString(CultureInfo.InvariantCulture);
                        sb.Append(FormatInteger(neg, v == 0, digits, flags, width, prec, ""));
                        break;
                    }
                    case 'o':
                    case 'u':
                    case 'x':
                    case 'X':
                    {
                        uint v = TruncToUInt(a.CheckNumber(arg));
                        string digits;
                        string prefix = "";
                        bool alt = flags.IndexOf('#') >= 0;
                        if (conv == 'o')
                        {
                            digits = Convert.ToString((long)v, 8);
                            if (alt && digits[0] != '0') digits = "0" + digits;
                        }
                        else if (conv == 'u') digits = v.ToString(CultureInfo.InvariantCulture);
                        else
                        {
                            digits = v.ToString(conv == 'x' ? "x" : "X", CultureInfo.InvariantCulture);
                            if (alt && v != 0) prefix = conv == 'x' ? "0x" : "0X";
                        }
                        sb.Append(FormatInteger(false, v == 0, digits, flags.Replace("+", "").Replace(" ", ""), width, prec, prefix));
                        break;
                    }
                    case 'e':
                    case 'E':
                    case 'f':
                    case 'g':
                    case 'G':
                    {
                        double d = a.CheckNumber(arg);
                        bool alt = flags.IndexOf('#') >= 0;
                        int pr = prec < 0 ? 6 : prec;
                        string body;
                        if (conv == 'f') body = LuaConv.FormatF(d, pr, alt);
                        else if (conv == 'e' || conv == 'E') body = LuaConv.FormatE(d, pr, alt, conv == 'E');
                        else body = LuaConv.FormatG(d, pr, alt, conv == 'G');
                        sb.Append(FormatFloatBody(body, flags, width, !double.IsNaN(d) && !double.IsInfinity(d)));
                        break;
                    }
                    case 'q':
                        AddQuoted(sb, a.CheckString(arg));
                        break;
                    case 's':
                    {
                        string s = a.CheckString(arg);
                        if (prec < 0 && s.Length >= 100) { sb.Append(s); break; }
                        int z = s.IndexOf('\0');
                        if (z >= 0) s = s.Substring(0, z);                      // sprintf stops at NUL
                        if (prec >= 0 && s.Length > prec) s = s.Substring(0, prec);
                        sb.Append(Pad(s, flags, width, false));
                        break;
                    }
                    default:
                        throw a.Error("invalid option '%" + conv + "' to 'format'");
                }
            }
            return sb.ToString();
        }

        static bool IsDigit(char c) => c >= '0' && c <= '9';

        /// <summary>The (long) cast of the client's 32-bit MSVC build: truncation, out of range → 0x80000000.</summary>
        static int TruncToInt(double d)
        {
            if (double.IsNaN(d) || d <= -2147483649.0 || d >= 2147483648.0) return int.MinValue;
            return (int)d;
        }

        static uint TruncToUInt(double d)
        {
            if (double.IsNaN(d) || d <= -9.2e18 || d >= 9.2e18) return 0;
            return unchecked((uint)(long)d);
        }

        static string FormatInteger(bool neg, bool zero, string digits, string flags, int width, int prec, string prefix)
        {
            if (prec >= 0)
            {
                if (prec == 0 && zero) digits = "";
                else if (digits.Length < prec) digits = new string('0', prec - digits.Length) + digits;
            }
            string sign = neg ? "-" : flags.IndexOf('+') >= 0 ? "+" : flags.IndexOf(' ') >= 0 ? " " : "";
            string body = sign + prefix + digits;
            if (width > body.Length)
            {
                if (flags.IndexOf('-') >= 0) return body.PadRight(width);
                if (flags.IndexOf('0') >= 0 && prec < 0) return sign + prefix + new string('0', width - body.Length) + digits;
                return body.PadLeft(width);
            }
            return body;
        }

        static string FormatFloatBody(string body, string flags, int width, bool finite)
        {
            bool neg = body.Length > 0 && body[0] == '-';
            string sign = neg ? "-" : flags.IndexOf('+') >= 0 ? "+" : flags.IndexOf(' ') >= 0 ? " " : "";
            string digits = neg ? body.Substring(1) : body;
            string full = sign + digits;
            if (width > full.Length)
            {
                if (flags.IndexOf('-') >= 0) return full.PadRight(width);
                if (flags.IndexOf('0') >= 0 && finite) return sign + new string('0', width - full.Length) + digits;
                return full.PadLeft(width);
            }
            return full;
        }

        static string Pad(string s, string flags, int width, bool zeroAllowed)
        {
            if (width <= s.Length) return s;
            if (flags.IndexOf('-') >= 0) return s.PadRight(width);
            return s.PadLeft(width, zeroAllowed && flags.IndexOf('0') >= 0 ? '0' : ' ');
        }

        static void AddQuoted(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':
                    case '\\':
                    case '\n':
                        sb.Append('\\').Append(c);
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\0':
                        sb.Append("\\000");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- patterns (lstrlib.c 5.1) -----------------------------------------------------------------------------

        const int MaxCaptures = 32;             // LUA_MAXCAPTURES
        const int CapUnfinished = -1, CapPosition = -2;
        const string Specials = "^$*+?.([%-";

        sealed class MatchState
        {
            public string Src;
            public string Pat;
            public LuaArgs Args;
            public int Level;
            public readonly int[] CapInit = new int[MaxCaptures];
            public readonly int[] CapLen = new int[MaxCaptures];
            int depth;

            char P(int i) => i < Pat.Length ? Pat[i] : '\0';
            char S(int i) => i < Src.Length ? Src[i] : '\0';

            int CheckCapture(int l)
            {
                l -= '1';
                if (l < 0 || l >= Level || CapLen[l] == CapUnfinished) throw Args.Error("invalid capture index");
                return l;
            }

            int CaptureToClose()
            {
                int level = Level;
                for (level--; level >= 0; level--)
                    if (CapLen[level] == CapUnfinished) return level;
                throw Args.Error("invalid pattern capture");
            }

            int ClassEnd(int p)
            {
                char c = P(p++);
                if (c == '%')
                {
                    if (p >= Pat.Length) throw Args.Error("malformed pattern (ends with '%')");
                    return p + 1;
                }
                if (c == '[')
                {
                    if (P(p) == '^') p++;
                    do
                    {
                        if (p >= Pat.Length) throw Args.Error("malformed pattern (missing ']')");
                        char cc = Pat[p++];
                        if (cc == '%' && p < Pat.Length) p++;
                    } while (P(p) != ']');
                    return p + 1;
                }
                return p;
            }

            static bool IsAlpha(int c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
            static bool IsDigitC(int c) => c >= '0' && c <= '9';
            static bool IsLower(int c) => c >= 'a' && c <= 'z';
            static bool IsUpper(int c) => c >= 'A' && c <= 'Z';
            static bool IsSpaceC(int c) => c == ' ' || (c >= '\t' && c <= '\r');
            static bool IsCntrl(int c) => c < 32 || c == 127;
            static bool IsPunct(int c) => (c >= 33 && c <= 47) || (c >= 58 && c <= 64) || (c >= 91 && c <= 96) || (c >= 123 && c <= 126);
            static bool IsXDigit(int c) => IsDigitC(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            static bool MatchClass(int c, int cl)
            {
                bool res;
                switch (IsUpper(cl) ? cl + 32 : cl)
                {
                    case 'a': res = IsAlpha(c); break;
                    case 'c': res = IsCntrl(c); break;
                    case 'd': res = IsDigitC(c); break;
                    case 'l': res = IsLower(c); break;
                    case 'p': res = IsPunct(c); break;
                    case 's': res = IsSpaceC(c); break;
                    case 'u': res = IsUpper(c); break;
                    case 'w': res = IsAlpha(c) || IsDigitC(c); break;
                    case 'x': res = IsXDigit(c); break;
                    case 'z': res = c == 0; break;
                    default: return cl == c;
                }
                return IsLower(cl) ? res : !res;
            }

            bool MatchBracketClass(int c, int p, int ec)
            {
                bool sig = true;
                if (P(p + 1) == '^')
                {
                    sig = false;
                    p++;
                }
                while (++p < ec)
                {
                    if (P(p) == '%')
                    {
                        p++;
                        if (MatchClass(c, P(p))) return sig;
                    }
                    else if (P(p + 1) == '-' && p + 2 < ec)
                    {
                        p += 2;
                        if (P(p - 2) <= c && c <= P(p)) return sig;
                    }
                    else if (P(p) == c) return sig;
                }
                return !sig;
            }

            bool SingleMatch(int c, int p, int ep)
            {
                switch (P(p))
                {
                    case '.': return true;
                    case '%': return MatchClass(c, P(p + 1));
                    case '[': return MatchBracketClass(c, p, ep - 1);
                    default: return P(p) == c;
                }
            }

            int MatchBalance(int s, int p)
            {
                if (p >= Pat.Length || p + 1 >= Pat.Length) throw Args.Error("unbalanced pattern");
                if (S(s) != P(p) || s >= Src.Length) return -1;
                char b = P(p), e = P(p + 1);
                int cont = 1;
                while (++s < Src.Length)
                {
                    char c = Src[s];
                    if (c == e)
                    {
                        if (--cont == 0) return s + 1;
                    }
                    else if (c == b) cont++;
                }
                return -1;
            }

            int MaxExpand(int s, int p, int ep)
            {
                int i = 0;
                while (s + i < Src.Length && SingleMatch(Src[s + i], p, ep)) i++;
                while (i >= 0)
                {
                    int res = Match(s + i, ep + 1);
                    if (res != -1) return res;
                    i--;
                }
                return -1;
            }

            int MinExpand(int s, int p, int ep)
            {
                for (;;)
                {
                    int res = Match(s, ep + 1);
                    if (res != -1) return res;
                    if (s < Src.Length && SingleMatch(Src[s], p, ep)) s++;
                    else return -1;
                }
            }

            int StartCapture(int s, int p, int what)
            {
                int level = Level;
                if (level >= MaxCaptures) throw Args.Error("too many captures");
                CapInit[level] = s;
                CapLen[level] = what;
                Level = level + 1;
                int res = Match(s, p);
                if (res == -1) Level--;
                return res;
            }

            int EndCapture(int s, int p)
            {
                int l = CaptureToClose();
                CapLen[l] = s - CapInit[l];
                int res = Match(s, p);
                if (res == -1) CapLen[l] = CapUnfinished;
                return res;
            }

            int MatchCapture(int s, int l)
            {
                l = CheckCapture(l);
                int len = CapLen[l];
                if (Src.Length - s >= len && string.CompareOrdinal(Src, CapInit[l], Src, s, len) == 0) return s + len;
                return -1;
            }

            /// <summary>match: the end of the match of pattern position <paramref name="p"/> at subject position <paramref name="s"/>, or −1.</summary>
            public int Match(int s, int p)
            {
                if (++depth > 200) { depth--; throw Args.Error("pattern too complex"); }
                try
                {
                    while (true)
                    {
                        switch (P(p))
                        {
                            case '(':
                                if (P(p + 1) == ')') return StartCapture(s, p + 2, CapPosition);
                                return StartCapture(s, p + 1, CapUnfinished);
                            case ')':
                                return EndCapture(s, p + 1);
                            case '\0':
                                if (p >= Pat.Length) return s;
                                goto default;
                            case '$':
                                if (p + 1 == Pat.Length) return s == Src.Length ? s : -1;
                                goto default;
                            case '%':
                            {
                                char n = P(p + 1);
                                if (n == 'b')
                                {
                                    s = MatchBalance(s, p + 2);
                                    if (s == -1) return -1;
                                    p += 4;
                                    continue;
                                }
                                if (n == 'f')
                                {
                                    p += 2;
                                    if (P(p) != '[') throw Args.Error("missing '[' after '%f' in pattern");
                                    int ep2 = ClassEnd(p);
                                    char previous = s == 0 ? '\0' : Src[s - 1];
                                    if (MatchBracketClass(previous, p, ep2 - 1) || !MatchBracketClass(S(s), p, ep2 - 1)) return -1;
                                    p = ep2;
                                    continue;
                                }
                                if (IsDigitC(n))
                                {
                                    s = MatchCapture(s, n);
                                    if (s == -1) return -1;
                                    p += 2;
                                    continue;
                                }
                                goto default;
                            }
                            default:
                            {
                                int ep = ClassEnd(p);
                                bool m = s < Src.Length && SingleMatch(Src[s], p, ep);
                                switch (P(ep))
                                {
                                    case '?':
                                    {
                                        if (m)
                                        {
                                            int res = Match(s + 1, ep + 1);
                                            if (res != -1) return res;
                                        }
                                        p = ep + 1;
                                        continue;
                                    }
                                    case '*':
                                        return MaxExpand(s, p, ep);
                                    case '+':
                                        return m ? MaxExpand(s + 1, p, ep) : -1;
                                    case '-':
                                        return MinExpand(s, p, ep);
                                    default:
                                        if (!m) return -1;
                                        s++;
                                        p = ep;
                                        continue;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    depth--;
                }
            }

            public LuaValue GetCapture(int i, int s, int e)
            {
                if (i >= Level)
                {
                    if (i == 0) return LuaValue.Of(Src.Substring(s, e - s));
                    throw Args.Error("invalid capture index");
                }
                int l = CapLen[i];
                if (l == CapUnfinished) throw Args.Error("unfinished capture");
                if (l == CapPosition) return LuaValue.Of(CapInit[i] + 1);
                return LuaValue.Of(Src.Substring(CapInit[i], l));
            }

            /// <summary>push_captures: whole match (when no captures and s ≥ 0) or every capture.</summary>
            public LuaValue[] Captures(int s, int e, bool wholeIfNone)
            {
                int n = (Level == 0 && wholeIfNone) ? 1 : Level;
                var r = new LuaValue[n];
                for (int i = 0; i < n; i++) r[i] = GetCapture(i, s, e);
                return r;
            }
        }

        static LuaReturn StrFindAux(LuaArgs a, bool find)
        {
            string s = a.CheckString(0);
            string p = a.CheckString(1);
            int l1 = s.Length;
            int init = PosRelat(a.OptInt(2, 1), l1) - 1;
            if (init < 0) init = 0;
            else if (init > l1) init = l1;
            if (find && (a[3].IsTruthy || p.IndexOfAny(Specials.ToCharArray()) < 0))
            {
                int idx = p.Length == 0 ? init : s.IndexOf(p, init, StringComparison.Ordinal);
                if (idx >= 0) return LuaReturn.Of(LuaValue.Of(idx + 1), LuaValue.Of(idx + p.Length));
                return LuaValue.Nil;
            }
            var ms = new MatchState { Src = s, Args = a };
            bool anchor = p.Length > 0 && p[0] == '^';
            ms.Pat = anchor ? p.Substring(1) : p;
            int s1 = init;
            do
            {
                ms.Level = 0;
                int res = ms.Match(s1, 0);
                if (res != -1)
                {
                    if (find)
                    {
                        var caps = ms.Captures(-1, -1, false);
                        var r = new LuaValue[2 + caps.Length];
                        r[0] = LuaValue.Of(s1 + 1);
                        r[1] = LuaValue.Of(res);
                        Array.Copy(caps, 0, r, 2, caps.Length);
                        return LuaReturn.FromArray(r, r.Length);
                    }
                    var c = ms.Captures(s1, res, true);
                    return LuaReturn.FromArray(c, c.Length);
                }
            } while (s1++ < l1 && !anchor);
            return LuaValue.Nil;
        }

        static LuaReturn GMatch(LuaArgs a)
        {
            string s = a.CheckString(0);
            string p = a.CheckString(1);
            int pos = 0;
            var iter = new LuaHostFunction("gmatch_aux", ia =>
            {
                var ms = new MatchState { Src = s, Pat = p, Args = ia };
                for (int src = pos; src <= s.Length; src++)
                {
                    ms.Level = 0;
                    int e = ms.Match(src, 0);
                    if (e != -1)
                    {
                        int newstart = e;
                        if (e == src) newstart++;
                        pos = newstart;
                        var c = ms.Captures(src, e, true);
                        return LuaReturn.FromArray(c, c.Length);
                    }
                }
                pos = s.Length + 1;
                return LuaReturn.None;
            });
            return LuaValue.Of(iter);
        }

        static LuaReturn GSub(LuaArgs a)
        {
            string src = a.CheckString(0);
            string p = a.CheckString(1);
            var repl = a[2];
            var tr = repl.Type;
            int maxS = a.OptInt(3, src.Length + 1);
            if (!(tr == LuaType.Number || tr == LuaType.String || tr == LuaType.Function || tr == LuaType.Table))
                throw a.ArgError(2, "string/function/table expected");
            bool anchor = p.Length > 0 && p[0] == '^';
            var ms = new MatchState { Src = src, Pat = anchor ? p.Substring(1) : p, Args = a };
            string news = tr == LuaType.String || tr == LuaType.Number ? (repl.TryGetString(out string rs) ? rs : "") : null;
            var b = new StringBuilder(src.Length);
            int s = 0, n = 0;
            while (n < maxS)
            {
                ms.Level = 0;
                int e = ms.Match(s, 0);
                if (e != -1)
                {
                    n++;
                    AddValue(a, ms, b, s, e, repl, news);
                }
                if (e != -1 && e > s) s = e;
                else if (s < src.Length) b.Append(src[s++]);
                else break;
                if (anchor) break;
            }
            if (s < src.Length) b.Append(src, s, src.Length - s);
            return LuaReturn.Of(LuaValue.Of(b.ToString()), LuaValue.Of(n));
        }

        static void AddValue(LuaArgs a, MatchState ms, StringBuilder b, int s, int e, LuaValue repl, string news)
        {
            LuaValue v;
            if (news != null)
            {
                int l = news.Length;
                for (int i = 0; i < l; i++)
                {
                    char c = news[i];
                    if (c != '%') { b.Append(c); continue; }
                    i++;
                    char d = i < l ? news[i] : '\0';
                    if (!IsDigit(d)) b.Append(d);
                    else if (d == '0') b.Append(ms.Src, s, e - s);
                    else
                    {
                        var cap = ms.GetCapture(d - '1', s, e);
                        cap.TryGetString(out string cs);
                        b.Append(cs);
                    }
                }
                return;
            }
            var L = a.State;
            if (repl.Type == LuaType.Function)
            {
                var caps = ms.Captures(s, e, true);
                int func = L.top;
                L.EnsureStack(func + caps.Length + 1 + 20);
                L.stack[func] = repl;
                for (int k = 0; k < caps.Length; k++) L.stack[func + 1 + k] = caps[k];
                L.top = func + 1 + caps.Length;
                L.CallValue(func, 1);
                v = L.stack[func];
                L.top = func;
            }
            else
            {
                v = L.GetTable(repl, ms.GetCapture(0, s, e), -1);
            }
            if (v.IsFalsy)
            {
                b.Append(ms.Src, s, e - s);
                return;
            }
            if (!v.TryGetString(out string str)) throw a.Error("invalid replacement value (a " + v.TypeName + ")");
            b.Append(str);
        }

        // ================================================================================================================
        // table
        // ================================================================================================================

        static LuaTable OpenTable()
        {
            var t = new LuaTable();
            Reg(t, "concat", TConcat);
            Reg(t, "foreach", TForeach);
            Reg(t, "foreachi", TForeachi);
            Reg(t, "getn", a => a.CheckTable(0).Length);
            Reg(t, "setn", a => { a.CheckTable(0); throw a.Error("'setn' is obsolete"); });
            Reg(t, "maxn", a =>
            {
                var tab = a.CheckTable(0);
                double max = 0;
                foreach (var kv in tab)
                    if (kv.Key.IsNumber && kv.Key.N > max) max = kv.Key.N;
                return max;
            });
            Reg(t, "insert", TInsert);
            Reg(t, "remove", TRemove);
            Reg(t, "sort", TSort);
            return t;
        }

        static LuaReturn TConcat(LuaArgs a)
        {
            string sep = a.OptString(1, "");
            var t = a.CheckTable(0);
            int i = a.OptInt(2, 1);
            int last = a[3].IsNil ? t.Length : a.CheckInt(3);
            var b = new StringBuilder();
            for (; i < last; i++)
            {
                AddField(a, b, t, i);
                b.Append(sep);
            }
            if (i == last) AddField(a, b, t, i);
            return b.ToString();
        }

        static void AddField(LuaArgs a, StringBuilder b, LuaTable t, int i)
        {
            var v = t.GetInt(i);
            if (!v.TryGetString(out string s)) throw a.Error("invalid value (at index " + i + ") in table for 'concat'");
            b.Append(s);
        }

        static LuaReturn TForeachi(LuaArgs a)
        {
            var t = a.CheckTable(0);
            var f = a.CheckFunction(1);
            int n = t.Length;
            for (int i = 1; i <= n; i++)
            {
                var r = a.State.CallTMRes(LuaValue.Of(f), LuaValue.Of(i), t.GetInt(i));
                if (!r.IsNil) return r;
            }
            return LuaReturn.None;
        }

        static LuaReturn TForeach(LuaArgs a)
        {
            var t = a.CheckTable(0);
            var f = a.CheckFunction(1);
            var k = LuaValue.Nil;
            while (t.Next(ref k, out var v))
            {
                var r = a.State.CallTMRes(LuaValue.Of(f), k, v);
                if (!r.IsNil) return r;
            }
            return LuaReturn.None;
        }

        static LuaReturn TInsert(LuaArgs a)
        {
            var t = a.CheckTable(0);
            int e = t.Length + 1;
            int pos;
            switch (a.Count)
            {
                case 2:
                    pos = e;
                    break;
                case 3:
                {
                    pos = a.CheckInt(1);
                    if (pos > e) e = pos;
                    for (int i = e; i > pos; i--) t.SetInt(i, t.GetInt(i - 1));
                    break;
                }
                default:
                    throw a.Error("wrong number of arguments to 'insert'");
            }
            t.SetInt(pos, a[a.Count - 1]);
            return LuaReturn.None;
        }

        static LuaReturn TRemove(LuaArgs a)
        {
            var t = a.CheckTable(0);
            int e = t.Length;
            int pos = a.OptInt(1, e);
            if (!(1 <= pos && pos <= e)) return LuaReturn.None;
            var result = t.GetInt(pos);
            for (; pos < e; pos++) t.SetInt(pos, t.GetInt(pos + 1));
            t.SetInt(e, LuaValue.Nil);
            return result;
        }

        static LuaReturn TSort(LuaArgs a)
        {
            var t = a.CheckTable(0);
            int n = t.Length;
            if (!a[1].IsNil) a.CheckFunction(1);
            var cmp = a[1];
            AuxSort(a, t, cmp, 1, n);
            return LuaReturn.None;
        }

        static bool SortComp(LuaArgs a, in LuaValue cmp, in LuaValue x, in LuaValue y)
        {
            if (!cmp.IsNil) return a.State.CallTMRes(cmp, x, y).IsTruthy;
            return a.State.LessThanValues(x, y);
        }

        /// <summary>ltablib.c auxsort (5.1): median-of-three quicksort, recursing into the smaller half.</summary>
        static void AuxSort(LuaArgs a, LuaTable t, LuaValue cmp, int l, int u)
        {
            while (l < u)
            {
                LuaValue al = t.GetInt(l), au = t.GetInt(u);
                if (SortComp(a, cmp, au, al)) { t.SetInt(l, au); t.SetInt(u, al); }
                if (u - l == 1) break;
                int i = (l + u) / 2;
                LuaValue ai = t.GetInt(i);
                al = t.GetInt(l);
                if (SortComp(a, cmp, ai, al)) { t.SetInt(i, al); t.SetInt(l, ai); }
                else
                {
                    au = t.GetInt(u);
                    if (SortComp(a, cmp, au, ai)) { t.SetInt(i, au); t.SetInt(u, ai); }
                }
                if (u - l == 2) break;
                LuaValue pivot = t.GetInt(i);
                LuaValue au1 = t.GetInt(u - 1);
                t.SetInt(i, au1);
                t.SetInt(u - 1, pivot);
                i = l;
                int j = u - 1;
                for (;;)
                {
                    LuaValue vi, vj;
                    while (true)
                    {
                        vi = t.GetInt(++i);
                        if (!SortComp(a, cmp, vi, pivot)) break;
                        if (i > u) throw a.Error("invalid order function for sorting");
                    }
                    while (true)
                    {
                        vj = t.GetInt(--j);
                        if (!SortComp(a, cmp, pivot, vj)) break;
                        if (j < l) throw a.Error("invalid order function for sorting");
                    }
                    if (j < i) break;
                    t.SetInt(i, vj);
                    t.SetInt(j, vi);
                }
                LuaValue x = t.GetInt(u - 1), y = t.GetInt(i);
                t.SetInt(u - 1, y);
                t.SetInt(i, x);
                if (i - l < u - i)
                {
                    j = l;
                    i = i - 1;
                    l = i + 2;
                }
                else
                {
                    j = i + 1;
                    i = u;
                    u = j - 2;
                }
                AuxSort(a, t, cmp, j, i);
            }
        }
    }

    public sealed partial class LuaState
    {
        /// <summary>The state of math.random (MSVC srand / rand; srand(1) at start).</summary>
        internal uint RandSeed = 1;
    }
}
