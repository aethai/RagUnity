using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ragnarok.Data.Lua51
{
    /// <summary>Lua 5.1 type tags (lua.h numbering).</summary>
    public enum LuaType : byte
    {
        Nil = 0,
        Boolean = 1,
        LightUserData = 2,
        Number = 3,
        String = 4,
        Table = 5,
        Function = 6,
        UserData = 7,
    }

    /// <summary>
    /// A Lua 5.1 value. Strings are Lua byte strings held as .NET strings with one char per byte (0..255), exactly as the
    /// undumper reads them from a chunk; use <see cref="LuaValue.FromBytes"/> / <see cref="LuaValue.GetBytes"/> or an
    /// <see cref="Encoding"/> (e.g. CP949) to convert. A string value carries its precomputed Lua hash (luaS_hash), so table
    /// lookups do not rehash it. Equality (<see cref="Equals(LuaValue)"/>, ==) is Lua's raw equality.
    /// </summary>
    public readonly struct LuaValue : IEquatable<LuaValue>
    {
        internal readonly object O;     // string, LuaTable, LuaFunction, LuaUserData
        internal readonly double N;     // number, boolean (0/1), string hash
        public readonly LuaType Type;

        LuaValue(LuaType type, double n, object o) { Type = type; N = n; O = o; }

        public static readonly LuaValue Nil = default;
        public static readonly LuaValue True = new LuaValue(LuaType.Boolean, 1, null);
        public static readonly LuaValue False = new LuaValue(LuaType.Boolean, 0, null);

        public static LuaValue Of(double n) => new LuaValue(LuaType.Number, n, null);
        public static LuaValue Of(bool b) => b ? True : False;
        public static LuaValue Of(string s) => s == null ? Nil : new LuaValue(LuaType.String, LuaTable.StringHash(s), s);
        public static LuaValue Of(LuaTable t) => t == null ? Nil : new LuaValue(LuaType.Table, 0, t);
        public static LuaValue Of(LuaFunction f) => f == null ? Nil : new LuaValue(LuaType.Function, 0, f);
        public static LuaValue Of(LuaUserData u) => u == null ? Nil : new LuaValue(LuaType.UserData, 0, u);

        /// <summary>A full userdata wrapping a host object (with an optional metatable).</summary>
        public static LuaValue FromUserData(object value, LuaTable metatable = null) => Of(new LuaUserData(value, metatable));

        /// <summary>A Lua string from raw bytes (one char per byte).</summary>
        public static LuaValue FromBytes(byte[] bytes) => bytes == null ? Nil : Of(BytesToString(bytes, 0, bytes.Length));

        /// <summary>A Lua string from text encoded with <paramref name="encoding"/> (e.g. CP949 for Korean client text).</summary>
        public static LuaValue FromText(string text, Encoding encoding) => text == null ? Nil : FromBytes(encoding.GetBytes(text));

        public static implicit operator LuaValue(double n) => Of(n);
        public static implicit operator LuaValue(bool b) => Of(b);
        public static implicit operator LuaValue(string s) => Of(s);
        public static implicit operator LuaValue(LuaTable t) => Of(t);
        public static implicit operator LuaValue(LuaFunction f) => Of(f);

        public bool IsNil => Type == LuaType.Nil;
        public bool IsNumber => Type == LuaType.Number;
        public bool IsString => Type == LuaType.String;
        public bool IsTable => Type == LuaType.Table;
        public bool IsFunction => Type == LuaType.Function;
        public bool IsBoolean => Type == LuaType.Boolean;

        /// <summary>Lua truth: everything except nil and false.</summary>
        public bool IsTruthy => !(Type == LuaType.Nil || (Type == LuaType.Boolean && N == 0));

        /// <summary>Lua truth negated (l_isfalse).</summary>
        public bool IsFalsy => Type == LuaType.Nil || (Type == LuaType.Boolean && N == 0);

        /// <summary>The number (throws when the value is not a number; see <see cref="TryGetNumber"/> for coercion).</summary>
        public double Number => Type == LuaType.Number ? N : throw new InvalidOperationException("not a number: " + TypeName);

        public bool Boolean => Type == LuaType.Boolean ? N != 0 : throw new InvalidOperationException("not a boolean: " + TypeName);

        /// <summary>The string (null when the value is not a string; numbers are not converted — see <see cref="TryGetString"/>).</summary>
        public string AsString => O as string;

        public LuaTable AsTable => O as LuaTable;
        public LuaFunction AsFunction => O as LuaFunction;
        public LuaUserData AsUserData => O as LuaUserData;

        /// <summary>The raw object behind a reference value (string, table, function, userdata), null otherwise.</summary>
        public object RawObject => O;

        /// <summary>lua_tonumber: a number, or a string convertible by luaO_str2d.</summary>
        public bool TryGetNumber(out double n)
        {
            if (Type == LuaType.Number) { n = N; return true; }
            if (Type == LuaType.String) return LuaConv.StrToNumber((string)O, out n);
            n = 0;
            return false;
        }

        /// <summary>lua_tonumber semantics: the number, or 0 when not convertible.</summary>
        public double ToNumber() => TryGetNumber(out double n) ? n : 0;

        /// <summary>lua_tointeger on the client's MSVC x86 build (round to nearest even).</summary>
        public int ToInteger() => TryGetNumber(out double n) ? LuaConv.Number2Int(n) : 0;

        /// <summary>lua_tolstring: a string, or a number formatted with "%.14g".</summary>
        public bool TryGetString(out string s)
        {
            if (Type == LuaType.String) { s = (string)O; return true; }
            if (Type == LuaType.Number) { s = LuaConv.FormatNumber(N); return true; }
            s = null;
            return false;
        }

        /// <summary>The raw bytes of a string value (null when not a string).</summary>
        public byte[] GetBytes()
        {
            var s = O as string;
            if (s == null) return null;
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)s[i];
            return b;
        }

        /// <summary>A string value decoded from its bytes with <paramref name="encoding"/> (null when not a string).</summary>
        public string DecodeString(Encoding encoding)
        {
            var b = GetBytes();
            return b == null ? null : encoding.GetString(b);
        }

        /// <summary>The Lua type name ("nil", "boolean", "number", "string", "table", "function", "userdata").</summary>
        public string TypeName => LuaConv.TypeName(Type);

        /// <summary>Raw equality (luaO_rawequalObj).</summary>
        public bool Equals(LuaValue o)
        {
            if (Type != o.Type) return false;
            switch (Type)
            {
                case LuaType.Nil: return true;
                case LuaType.Number:
                case LuaType.Boolean: return N == o.N;
                case LuaType.String: return ReferenceEquals(O, o.O) || (N == o.N && string.Equals((string)O, (string)o.O));
                default: return ReferenceEquals(O, o.O);
            }
        }

        public override bool Equals(object obj) => obj is LuaValue v && Equals(v);

        public override int GetHashCode()
        {
            switch (Type)
            {
                case LuaType.Nil: return 0;
                case LuaType.Number: return N == 0 ? 0 : N.GetHashCode();
                case LuaType.Boolean: return N != 0 ? 1 : 2;
                case LuaType.String: return (int)(uint)N;
                default: return RuntimeHelpers.GetHashCode(O);
            }
        }

        public static bool operator ==(LuaValue a, LuaValue b) => a.Equals(b);
        public static bool operator !=(LuaValue a, LuaValue b) => !a.Equals(b);

        /// <summary>The value as Lua's tostring prints it without metamethods ("nil", "true", "1.5", the string itself, "table: 0000002A").</summary>
        public override string ToString()
        {
            switch (Type)
            {
                case LuaType.Nil: return "nil";
                case LuaType.Boolean: return N != 0 ? "true" : "false";
                case LuaType.Number: return LuaConv.FormatNumber(N);
                case LuaType.String: return (string)O;
                default: return TypeName + ": " + LuaConv.ObjectId(O).ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        internal static string BytesToString(byte[] b, int offset, int count)
        {
            if (count == 0) return "";
            var c = new char[count];
            for (int i = 0; i < count; i++) c[i] = (char)b[offset + i];
            return new string(c);
        }
    }

    /// <summary>A full userdata: a host object with an optional metatable.</summary>
    public sealed class LuaUserData
    {
        public object Value;
        public LuaTable Metatable;

        public LuaUserData(object value, LuaTable metatable = null)
        {
            Value = value;
            Metatable = metatable;
        }
    }

    /// <summary>A Lua error. <see cref="Value"/> is the error object (a string message for runtime errors and error("...")).</summary>
    public class LuaException : Exception
    {
        /// <summary>The Lua error object.</summary>
        public LuaValue Value { get; }

        /// <summary>The Lua call stack at the error ("chunk:line: in function ..." lines), set when the error leaves the VM.</summary>
        public string LuaTraceback { get; internal set; }

        public LuaException(string message) : base(message) { Value = LuaValue.Of(message); }

        public LuaException(string message, Exception inner) : base(message, inner) { Value = LuaValue.Of(message); }

        public LuaException(LuaValue value) : base(Describe(value)) { Value = value; }

        static string Describe(LuaValue v)
        {
            if (v.Type == LuaType.String) return (string)v.O;
            if (v.Type == LuaType.Number) return LuaConv.FormatNumber(v.N);
            if (v.Type == LuaType.Nil) return "nil";
            return "(error object is a " + v.TypeName + " value)";
        }

        public override string ToString() => LuaTraceback == null ? base.ToString() : Message + "\n" + LuaTraceback + "\n" + base.ToString();
    }

    /// <summary>The instruction budget (<see cref="LuaState.InstructionLimit"/>) ran out. pcall does not catch it.</summary>
    public sealed class LuaBudgetExceededException : LuaException
    {
        public LuaBudgetExceededException(long limit) : base("instruction budget exceeded (" + limit.ToString(CultureInfo.InvariantCulture) + " instructions)") { }
    }

    /// <summary>Lua 5.1 conversions between numbers and strings (lobject.c / lvm.c / luaconf.h of an MSVC x86 build).</summary>
    public static class LuaConv
    {
        static int nextObjectId;

        internal static int NewObjectId() => System.Threading.Interlocked.Increment(ref nextObjectId);

        /// <summary>A stable per-object id used for "%p"-style printing and table / function key hashing.</summary>
        internal static int ObjectId(object o)
        {
            switch (o)
            {
                case LuaTable t: return t.Id;
                case LuaFunction f: return f.Id;
                default: return o == null ? 0 : RuntimeHelpers.GetHashCode(o);
            }
        }

        public static string TypeName(LuaType t)
        {
            switch (t)
            {
                case LuaType.Nil: return "nil";
                case LuaType.Boolean: return "boolean";
                case LuaType.LightUserData: return "userdata";
                case LuaType.Number: return "number";
                case LuaType.String: return "string";
                case LuaType.Table: return "table";
                case LuaType.Function: return "function";
                case LuaType.UserData: return "userdata";
                default: return "?";
            }
        }

        /// <summary>lua_number2int on the client's x86 MSVC build: FISTP, round to nearest even; out of range gives int.MinValue.</summary>
        public static int Number2Int(double n)
        {
            if (n >= -2147483648.0 && n <= 2147483647.0)
            {
                int k = (int)n;
                if (k == n) return k;
                double r = Math.Round(n, MidpointRounding.ToEven);
                if (r >= -2147483648.0 && r <= 2147483647.0) return (int)r;
            }
            return int.MinValue;
        }

        /// <summary>
        /// luaO_str2d: C strtod over the whole string (leading white space, sign, digits, '.', exponent), a "0x…" form
        /// through strtoul(…, 16), trailing white space allowed, anything else left over = failure. "inf" / "nan" refused.
        /// </summary>
        public static bool StrToNumber(string s, out double result)
        {
            result = 0;
            if (s == null) return false;
            int p = 0, n = s.Length;
            while (p < n && IsSpace(s[p])) p++;
            int start = p;
            if (p < n && (s[p] == '+' || s[p] == '-')) p++;
            int digits = 0;
            while (p < n && s[p] >= '0' && s[p] <= '9') { p++; digits++; }
            if (p < n && s[p] == '.') { p++; while (p < n && s[p] >= '0' && s[p] <= '9') { p++; digits++; } }
            if (digits == 0) return false;
            if (p < n && (s[p] == 'e' || s[p] == 'E'))
            {
                int q = p + 1;
                if (q < n && (s[q] == '+' || s[q] == '-')) q++;
                if (q < n && s[q] >= '0' && s[q] <= '9') { while (q < n && s[q] >= '0' && s[q] <= '9') q++; p = q; }
            }
            double d;
            if (p < n && (s[p] == 'x' || s[p] == 'X'))
            {
                // strtoul(s, &endptr, 16): optional sign, "0x", hex digits
                int q = start;
                bool neg = false;
                if (q < n && (s[q] == '+' || s[q] == '-')) { neg = s[q] == '-'; q++; }
                if (q + 1 < n && s[q] == '0' && (s[q + 1] == 'x' || s[q + 1] == 'X')) q += 2;
                int h0 = q;
                ulong v = 0;
                bool overflow = false;
                while (q < n && HexValue(s[q]) >= 0)
                {
                    v = v * 16 + (ulong)HexValue(s[q]);
                    if (v > uint.MaxValue) { overflow = true; v = uint.MaxValue; }
                    q++;
                }
                if (q == h0) return false;
                uint u = overflow ? uint.MaxValue : (uint)v;
                if (neg) u = unchecked((uint)-(int)u);
                d = u;
                p = q;
            }
            else
            {
                if (!double.TryParse(s.Substring(start, p - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return false;
            }
            while (p < n && IsSpace(s[p])) p++;
            if (p != n) return false;
            result = d;
            return true;
        }

        internal static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        internal static bool IsSpace(char c) => c == ' ' || (c >= '\t' && c <= '\r');

        /// <summary>LUA_NUMBER_FMT "%.14g" (tostring / concatenation / lua_tolstring of a number).</summary>
        public static string FormatNumber(double d)
        {
            if (d == Math.Floor(d) && d > -1e14 && d < 1e14 && d != 0)
                return ((long)d).ToString(CultureInfo.InvariantCulture);
            return FormatG(d, 14, false, false);
        }

        // ---- C printf conversions with exact decimal rounding (round half to even on exact ties, as the CRT does) ----

        /// <summary>round_half_even(|d| · 10^scale) computed exactly from the double's binary value.</summary>
        static BigInteger ScaledRound(double d, int scale)
        {
            long bits = BitConverter.DoubleToInt64Bits(d) & long.MaxValue;
            int bexp = (int)(bits >> 52);
            long frac = bits & ((1L << 52) - 1);
            long m;
            int e;
            if (bexp == 0) { m = frac; e = -1074; }
            else { m = frac | (1L << 52); e = bexp - 1075; }
            BigInteger num = m, den = BigInteger.One;
            if (e > 0) num <<= e; else den <<= -e;
            if (scale > 0) num *= BigInteger.Pow(10, scale);
            else if (scale < 0) den *= BigInteger.Pow(10, -scale);
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            int c = (r << 1).CompareTo(den);
            if (c > 0 || (c == 0 && !q.IsEven)) q += BigInteger.One;
            return q;
        }

        /// <summary>The decimal digits of |d| rounded to <paramref name="precision"/> + 1 significant digits and the decimal exponent of the first one.</summary>
        static string SignificantDigits(double d, int precision, out int exp10)
        {
            if (d == 0)
            {
                exp10 = 0;
                return new string('0', precision + 1);
            }
            double ad = Math.Abs(d);
            int x = (int)Math.Floor(Math.Log10(ad));
            string s = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                s = ScaledRound(d, precision - x).ToString(CultureInfo.InvariantCulture);
                if (s.Length == precision + 1) { exp10 = x; return s; }
                // more digits: log10 underestimated or rounding carried (9.99 → 10.0); fewer: overestimated
                if (s.Length > precision + 1) x++; else x--;
            }
            exp10 = x;
            return s.Length > precision + 1 ? s.Substring(0, precision + 1) : s.PadRight(precision + 1, '0');
        }

        static bool IsNegative(double d) => BitConverter.DoubleToInt64Bits(d) < 0;

        /// <summary>C printf "%.{precision}f".</summary>
        public static string FormatF(double d, int precision, bool alternate)
        {
            if (double.IsNaN(d)) return "nan";
            if (double.IsInfinity(d)) return d < 0 ? "-inf" : "inf";
            string digits = ScaledRound(d, precision).ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= precision) digits = new string('0', precision + 1 - digits.Length) + digits;
            var sb = new StringBuilder(digits.Length + 2);
            if (IsNegative(d)) sb.Append('-');
            sb.Append(digits, 0, digits.Length - precision);
            if (precision > 0 || alternate) sb.Append('.');
            sb.Append(digits, digits.Length - precision, precision);
            return sb.ToString();
        }

        /// <summary>C printf "%.{precision}e" (two-digit minimum exponent, as the UCRT prints it).</summary>
        public static string FormatE(double d, int precision, bool alternate, bool upper)
        {
            if (double.IsNaN(d)) return upper ? "NAN" : "nan";
            if (double.IsInfinity(d)) return d < 0 ? (upper ? "-INF" : "-inf") : (upper ? "INF" : "inf");
            string digits = SignificantDigits(d, precision, out int exp);
            var sb = new StringBuilder(precision + 8);
            if (IsNegative(d)) sb.Append('-');
            sb.Append(digits[0]);
            if (precision > 0 || alternate) sb.Append('.');
            sb.Append(digits, 1, precision);
            AppendExponent(sb, exp, upper);
            return sb.ToString();
        }

        /// <summary>C printf "%.{precision}g" (alternate = '#', upper = 'G').</summary>
        public static string FormatG(double d, int precision, bool alternate, bool upper)
        {
            if (double.IsNaN(d)) return upper ? "NAN" : (IsNegative(d) ? "-nan(ind)" : "nan");
            if (double.IsInfinity(d)) return d < 0 ? (upper ? "-INF" : "-inf") : (upper ? "INF" : "inf");
            if (precision == 0) precision = 1;
            string digits = SignificantDigits(d, precision - 1, out int exp);
            var sb = new StringBuilder(24);
            if (IsNegative(d)) sb.Append('-');
            if (exp >= -4 && exp < precision)
            {
                // %f with precision − 1 − exp decimals, from the same rounded digits
                if (exp >= 0)
                {
                    sb.Append(digits, 0, exp + 1);
                    string fracPart = digits.Substring(exp + 1);
                    if (!alternate) fracPart = fracPart.TrimEnd('0');
                    if (fracPart.Length > 0 || alternate) sb.Append('.').Append(fracPart);
                }
                else
                {
                    string fracPart = alternate ? digits : digits.TrimEnd('0');
                    sb.Append('0');
                    if (fracPart.Length > 0 || alternate) sb.Append('.').Append('0', -exp - 1).Append(fracPart);
                }
            }
            else
            {
                sb.Append(digits[0]);
                string fracPart = digits.Substring(1);
                if (!alternate) fracPart = fracPart.TrimEnd('0');
                if (fracPart.Length > 0 || alternate) sb.Append('.').Append(fracPart);
                AppendExponent(sb, exp, upper);
            }
            return sb.ToString();
        }


        static void AppendExponent(StringBuilder sb, int exp, bool upper)
        {
            sb.Append(upper ? 'E' : 'e').Append(exp < 0 ? '-' : '+');
            int ax = Math.Abs(exp);
            if (ax < 10) sb.Append('0');
            sb.Append(ax.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>luaO_chunkid with LUA_IDSIZE 60: "@file" → file (left-truncated with "..."), "=name" → name, source → [string "..."].</summary>
        public static string ChunkId(string source)
        {
            const int IdSize = 60;
            if (string.IsNullOrEmpty(source)) return "?";
            if (source[0] == '=')
                return source.Length - 1 > IdSize - 1 ? source.Substring(1, IdSize - 1) : source.Substring(1);
            if (source[0] == '@')
            {
                string f = source.Substring(1);
                int max = IdSize - 8;
                return f.Length > max ? "..." + f.Substring(f.Length - max) : f;
            }
            int len = source.IndexOfAny(new[] { '\n', '\r' });
            if (len < 0) len = source.Length;
            int maxLen = IdSize - 15;
            bool cut = len < source.Length;
            if (len > maxLen) { len = maxLen; cut = true; }
            return "[string \"" + source.Substring(0, len) + (cut ? "...\"]" : "\"]");
        }
    }
}
