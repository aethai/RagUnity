// Synced from reference/port/src/RoClassicFx/Shared/mapfx/LuaTable.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ragnarok.Effects.Classic
{
    /// <summary>Lua 5.1 type tags (lua.h) of the values an EffectTool script can hold.</summary>
    public enum LuaType : byte { Nil = 0, Boolean = 1, Number = 3, String = 4, Table = 5 }

    /// <summary>
    /// A Lua 5.1 value as EffectTool scripts use them: nil, boolean, number (double), string (a byte string: one char per
    /// byte, like every string of the port) or table. Equality is Lua's raw equality (numbers by value, so 0 == −0;
    /// strings by content — Lua interns them; tables by reference). Package mapfx.
    /// </summary>
    public readonly struct LuaValue : IEquatable<LuaValue>
    {
        public readonly LuaType Type;
        readonly double num;
        readonly object obj;

        LuaValue(LuaType type, double n, object o) { Type = type; num = n; obj = o; }

        public static readonly LuaValue Nil = default;
        public static readonly LuaValue True = new LuaValue(LuaType.Boolean, 1, null);
        public static readonly LuaValue False = new LuaValue(LuaType.Boolean, 0, null);

        public static LuaValue Of(double n) => new LuaValue(LuaType.Number, n, null);
        public static LuaValue Of(bool b) => b ? True : False;
        public static LuaValue Of(string s) => s == null ? Nil : new LuaValue(LuaType.String, 0, s);
        public static LuaValue Of(LuaTable t) => t == null ? Nil : new LuaValue(LuaType.Table, 0, t);

        public bool IsNil => Type == LuaType.Nil;
        public bool IsNumber => Type == LuaType.Number;
        public bool IsString => Type == LuaType.String;
        public bool IsTable => Type == LuaType.Table;
        public double Number => Type == LuaType.Number ? num : throw new InvalidOperationException($"not a number: {this}");
        public bool Boolean => Type == LuaType.Boolean ? num != 0 : throw new InvalidOperationException($"not a boolean: {this}");
        public string String => obj as string ?? throw new InvalidOperationException($"not a string: {this}");
        public LuaTable Table => obj as LuaTable ?? throw new InvalidOperationException($"not a table: {this}");
        /// <summary>The table, or null for any other type.</summary>
        public LuaTable AsTable => obj as LuaTable;

        /// <summary>Lua's truth: everything but nil and false.</summary>
        public bool IsTruthy => !(Type == LuaType.Nil || (Type == LuaType.Boolean && num == 0));

        /// <summary>
        /// lua_tonumber (lapi.c, FUN_005023a0 / FUN_00501c70 in the client): a number as is, a string converted with
        /// luaO_str2d (<see cref="StrToNumber"/>), anything else (and an unconvertible string) 0.
        /// </summary>
        public double ToNumber() => Type == LuaType.Number ? num : Type == LuaType.String && StrToNumber((string)obj, out double d) ? d : 0.0;

        /// <summary>
        /// lua_tolstring as a C string: a string as is (cut at an embedded NUL, as the client's <c>const char*</c> use cuts
        /// it), a number formatted with LUA_NUMBER_FMT "%.14g", anything else null (the C API returns NULL).
        /// </summary>
        public string ToCString()
        {
            if (Type == LuaType.String)
            {
                var s = (string)obj;
                int z = s.IndexOf('\0');
                return z < 0 ? s : s.Substring(0, z);
            }
            return Type == LuaType.Number ? FormatNumber(num) : null;
        }

        /// <summary>Raw equality (luaO_rawequalObj).</summary>
        public bool Equals(LuaValue o)
        {
            if (Type != o.Type) return false;
            switch (Type)
            {
                case LuaType.Nil: return true;
                case LuaType.Number: case LuaType.Boolean: return num == o.num;
                case LuaType.String: return string.Equals((string)obj, (string)o.obj, StringComparison.Ordinal);
                default: return ReferenceEquals(obj, o.obj);
            }
        }

        public override bool Equals(object o) => o is LuaValue v && Equals(v);
        public override int GetHashCode() => Type == LuaType.Number ? (num == 0 ? 0 : num.GetHashCode()) : obj?.GetHashCode() ?? (int)num;
        public static bool operator ==(LuaValue a, LuaValue b) => a.Equals(b);
        public static bool operator !=(LuaValue a, LuaValue b) => !a.Equals(b);

        public override string ToString()
        {
            switch (Type)
            {
                case LuaType.Nil: return "nil";
                case LuaType.Boolean: return num != 0 ? "true" : "false";
                case LuaType.Number: return FormatNumber(num);
                case LuaType.String: return "\"" + (string)obj + "\"";
                default: return "table";
            }
        }

        /// <summary>
        /// luaO_str2d (lobject.c): C strtod over the whole string (leading white space, sign, digits, '.', exponent),
        /// a "0x…" form through strtoul(…, 16), trailing white space allowed, anything else left over = failure.
        /// Differences: the client's UCRT strtod also accepts "inf" / "nan" / hexadecimal floats — refused here
        /// (no EffectTool file has a string where a number is read).
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
            if (digits == 0) return false;                      // strtod consumed nothing: endptr == s
            if (p < n && (s[p] == 'e' || s[p] == 'E'))
            {
                int q = p + 1;
                if (q < n && (s[q] == '+' || s[q] == '-')) q++;
                if (q < n && s[q] >= '0' && s[q] <= '9') { while (q < n && s[q] >= '0' && s[q] <= '9') q++; p = q; }
            }
            double d = double.Parse(s.Substring(start, p - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            if (p < n && (s[p] == 'x' || s[p] == 'X'))
            {
                // strtoul(s, &endptr, 16) from the start: optional sign, "0x", hex digits
                int q = start;
                bool neg = false;
                if (q < n && (s[q] == '+' || s[q] == '-')) { neg = s[q] == '-'; q++; }
                if (q + 1 < n && s[q] == '0' && (s[q + 1] == 'x' || s[q + 1] == 'X')) q += 2;
                int h0 = q;
                ulong v = 0;
                bool overflow = false;
                while (q < n && Uri.IsHexDigit(s[q]))
                {
                    v = v * 16 + (ulong)Convert.ToInt32(s[q].ToString(), 16);
                    if (v > uint.MaxValue) overflow = true;
                    q++;
                }
                if (q == h0) return false;
                uint u = overflow ? uint.MaxValue : (uint)v;
                if (neg) u = unchecked((uint)-(int)u);
                d = u;
                p = q;
            }
            while (p < n && IsSpace(s[p])) p++;
            if (p != n) return false;
            result = d;
            return true;
        }

        static bool IsSpace(char c) => c == ' ' || (c >= '\t' && c <= '\r');

        /// <summary>LUA_NUMBER_FMT "%.14g" (luaO_tostr / lua_tolstring of a number).</summary>
        public static string FormatNumber(double d)
        {
            if (double.IsNaN(d)) return BitConverter.DoubleToInt64Bits(d) < 0 ? "-nan(ind)" : "nan";
            if (double.IsInfinity(d)) return d < 0 ? "-inf" : "inf";
            if (d == 0) return BitConverter.DoubleToInt64Bits(d) < 0 ? "-0" : "0";
            // %.14g: exponent X of the value rounded to 14 significant digits; fixed when -4 <= X < 14
            string e = d.ToString("E13", CultureInfo.InvariantCulture);   // d.ddddddddddddddE+xxx
            int ePos = e.IndexOf('E');
            int exp = int.Parse(e.Substring(ePos + 1), CultureInfo.InvariantCulture);
            string mant = e.Substring(0, ePos);
            bool neg = mant[0] == '-';
            string digits = mant.Replace("-", "").Replace(".", "");          // 14 digits
            var sb = new StringBuilder();
            if (neg) sb.Append('-');
            if (exp >= -4 && exp < 14)
            {
                if (exp >= 0)
                {
                    sb.Append(digits, 0, exp + 1);
                    string frac = digits.Substring(exp + 1).TrimEnd('0');
                    if (frac.Length > 0) sb.Append('.').Append(frac);
                }
                else
                {
                    sb.Append("0.").Append('0', -exp - 1).Append(digits.TrimEnd('0'));
                }
            }
            else
            {
                sb.Append(digits[0]);
                string frac = digits.Substring(1).TrimEnd('0');
                if (frac.Length > 0) sb.Append('.').Append(frac);
                sb.Append('e').Append(exp < 0 ? '-' : '+');
                int ax = Math.Abs(exp);
                sb.Append(ax < 10 ? "0" + ax.ToString(CultureInfo.InvariantCulture) : ax.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }

    /// <summary>A Lua error (the client's pcall catches it: luaL_loadbuffer / lua_pcall return non-zero).</summary>
    public sealed class LuaScriptError : Exception
    {
        public LuaScriptError(string message) : base(message) { }
    }

    /// <summary>
    /// A Lua 5.1 table with ltable.c's exact layout — array part, node vector (2^lsizenode nodes with chained
    /// collisions, the shared dummy node when empty), lastfree, rehash (numusearray / numusehash / countint /
    /// computesizes / resize) — so that <see cref="Next"/> (luaH_next) and <see cref="Pairs"/> enumerate in the order the
    /// client's Lua does: array part by index, then the nodes in vector order. Main positions: numbers by hashnum (the
    /// double's two 32-bit words summed, modulo (size − 1) | 1; ±0 → node 0), strings by luaS_hash (seed = length, step =
    /// (length &gt;&gt; 5) + 1) &amp; (size − 1), booleans by their value &amp; (size − 1). Integer keys go through
    /// lua_number2int = x87 FISTP (round to nearest) as in the client's MSVC build. Not emulated: the garbage collector's dead
    /// keys (only reachable by setting a key to nil and later re-setting it after a collection; no EffectTool script
    /// does), metatables, table / function keys (pointer hashes — refused). Package mapfx.
    /// </summary>
    public sealed class LuaTable
    {
        const int MaxBits = 26;                 // MAXBITS (LUAI_BITSINT 32 > 26)
        const int MaxASize = 1 << MaxBits;

        struct Node
        {
            public LuaValue Key, Val;
            public int Next;                    // node index, −1 = NULL
        }

        LuaValue[] array = Array.Empty<LuaValue>();
        int sizeArray;
        Node[] node;
        int lsizenode;
        bool dummy;                             // node == dummynode (sizenode 1, never written)
        int lastFree;                           // lastfree as a node index (the vector size when all are free)

        /// <summary>luaH_new(L, narray, nhash) — NEWTABLE with luaO_fb2int(B), luaO_fb2int(C).</summary>
        public LuaTable(int narray = 0, int nhash = 0)
        {
            SetArrayVector(narray);
            SetNodeVector(nhash);
        }

        /// <summary>Size of the array part (t->sizearray).</summary>
        public int ArraySize => sizeArray;
        /// <summary>Number of nodes (sizenode(t) = 2^lsizenode; 0 for the dummy node).</summary>
        public int NodeCount => dummy ? 0 : node.Length;
        /// <summary>t->lastfree as a node index.</summary>
        public int LastFree => lastFree;
        public LuaValue ArrayAt(int i) => array[i];
        public LuaValue NodeKey(int i) => node[i].Key;
        public LuaValue NodeValue(int i) => node[i].Val;
        public int NodeNext(int i) => node[i].Next;

        /// <summary>Number of non-nil entries.</summary>
        public int Count
        {
            get
            {
                int n = 0;
                for (int i = 0; i < sizeArray; i++) if (!array[i].IsNil) n++;
                if (!dummy) for (int i = 0; i < node.Length; i++) if (!node[i].Val.IsNil) n++;
                return n;
            }
        }

        public LuaValue this[string key] => Get(LuaValue.Of(key));
        public LuaValue this[double key] => Get(LuaValue.Of(key));

        // ---- luaO helpers ------------------------------------------------------------------------------------------

        /// <summary>luaO_fb2int: the "floating point byte" of NEWTABLE's B / C operands.</summary>
        public static int Fb2Int(int x)
        {
            int e = (x >> 3) & 31;
            return e == 0 ? x : ((x & 7) + 8) << (e - 1);
        }

        /// <summary>luaO_int2fb (the parser's encoding of a constructor's item counts).</summary>
        public static int Int2Fb(uint x)
        {
            int e = 0;
            while (x >= 16) { x = (x + 1) >> 1; e++; }
            return x < 8 ? (int)x : ((e + 1) << 3) | ((int)x - 8);
        }

        /// <summary>luaO_log2 (floor; log2(0) = −1).</summary>
        static int Log2(uint x)
        {
            int l = -1;
            while (x != 0) { x >>= 1; l++; }
            return l;
        }

        static int CeilLog2(int x) => Log2((uint)(x - 1)) + 1;

        /// <summary>lua_number2int on the client's x86 MSVC build: FLD / FISTP (round to nearest even; out of range → 0x80000000).</summary>
        public static int Number2Int(double n)
        {
            if (double.IsNaN(n)) return int.MinValue;
            double r = Math.Round(n, MidpointRounding.ToEven);
            return r >= -2147483648.0 && r <= 2147483647.0 ? (int)r : int.MinValue;
        }

        /// <summary>luaS_hash as luaS_newlstr computes it (over the bytes; one char per byte).</summary>
        public static uint StringHash(string s)
        {
            uint l = (uint)s.Length;
            uint h = l;
            uint step = (l >> 5) + 1;
            for (uint l1 = l; l1 >= step; l1 -= step)
                h ^= (h << 5) + (h >> 2) + (byte)s[(int)l1 - 1];
            return h;
        }

        // ---- main positions ---------------------------------------------------------------------------------------

        int SizeNode => node.Length;            // the dummy vector has one node, like sizenode(dummy) = 1

        int HashNum(double n)
        {
            if (n == 0) return 0;               // luai_numeq(n, 0): avoid problems with −0
            long bits = BitConverter.DoubleToInt64Bits(n);
            uint a = unchecked((uint)bits + (uint)(bits >> 32));
            return (int)(a % (uint)((SizeNode - 1) | 1));
        }

        int MainPosition(in LuaValue key)
        {
            switch (key.Type)
            {
                case LuaType.Number: return HashNum(key.Number);
                case LuaType.String: return (int)(StringHash(key.String) & (uint)(SizeNode - 1));
                case LuaType.Boolean: return (key.Boolean ? 1 : 0) & (SizeNode - 1);
                default: throw new NotSupportedException($"a {key.Type} key (pointer hash) cannot be emulated");
            }
        }

        // ---- get --------------------------------------------------------------------------------------------------

        /// <summary>luaH_get: the value of <paramref name="key"/> (nil when absent).</summary>
        public LuaValue Get(LuaValue key)
        {
            FindSlot(key, out bool inArray, out int index);
            if (index < 0) return LuaValue.Nil;
            return inArray ? array[index] : node[index].Val;
        }

        public LuaValue GetInt(int key)
        {
            FindSlotNum(key, out bool inArray, out int index);
            if (index < 0) return LuaValue.Nil;
            return inArray ? array[index] : node[index].Val;
        }

        /// <summary>luaH_get's lookup: the slot holding <paramref name="key"/> (its value may be nil) or index −1.</summary>
        void FindSlot(in LuaValue key, out bool inArray, out int index)
        {
            inArray = false;
            index = -1;
            switch (key.Type)
            {
                case LuaType.Nil:
                    return;
                case LuaType.Number:
                {
                    double n = key.Number;
                    int k = Number2Int(n);
                    if ((double)k == n) { FindSlotNum(k, out inArray, out index); return; }
                    break;
                }
            }
            for (int i = MainPosition(key); i >= 0; i = node[i].Next)
                if (node[i].Key.Equals(key)) { index = i; return; }
        }

        /// <summary>luaH_getnum's lookup.</summary>
        void FindSlotNum(int key, out bool inArray, out int index)
        {
            if (unchecked((uint)(key - 1)) < (uint)sizeArray) { inArray = true; index = key - 1; return; }
            inArray = false;
            double nk = key;
            for (int i = HashNum(nk); i >= 0; i = node[i].Next)
                if (node[i].Key.Type == LuaType.Number && node[i].Key.Number == nk) { index = i; return; }
            index = -1;
        }

        // ---- set --------------------------------------------------------------------------------------------------

        /// <summary>
        /// <c>t[key] = value</c> without metatables (luaV_settable → luaH_set, then the store): an absent key gets a node
        /// even when <paramref name="value"/> is nil. Errors as the client's Lua: "table index is nil" / "… is NaN".
        /// </summary>
        public void Set(LuaValue key, LuaValue value)
        {
            SetSlot(key, out bool inArray, out int index);
            if (inArray) array[index] = value; else node[index].Val = value;
        }

        /// <summary>luaH_setnum + store (OP_SETLIST, the array re-insertion of resize).</summary>
        public void SetInt(int key, LuaValue value)
        {
            FindSlotNum(key, out bool inArray, out int index);
            if (index < 0) index = NewKey(LuaValue.Of((double)key), out inArray);
            if (inArray) array[index] = value; else node[index].Val = value;
        }

        void SetSlot(in LuaValue key, out bool inArray, out int index)
        {
            FindSlot(key, out inArray, out index);
            if (index >= 0) return;
            if (key.IsNil) throw new LuaScriptError("table index is nil");
            if (key.IsNumber && double.IsNaN(key.Number)) throw new LuaScriptError("table index is NaN");
            index = NewKey(key, out inArray);
        }

        /// <summary>newkey (ltable.c): place <paramref name="key"/> at its main position, moving a colliding node that is not in its own main position, or into the free position lastfree finds; a full vector rehashes and re-inserts.</summary>
        int NewKey(in LuaValue key, out bool inArray)
        {
            inArray = false;
            int mp = MainPosition(key);
            if (!node[mp].Val.IsNil || dummy)
            {
                int n = GetFreePos();
                if (n < 0)
                {
                    Rehash(key);
                    SetSlot(key, out inArray, out int index);   // luaH_set: an integer key may now be in the array part
                    return index;
                }
                int othern = MainPosition(node[mp].Key);
                if (othern != mp)
                {
                    while (node[othern].Next != mp) othern = node[othern].Next;
                    node[othern].Next = n;
                    node[n] = node[mp];
                    node[mp].Next = -1;
                    node[mp].Val = LuaValue.Nil;
                }
                else
                {
                    node[n].Next = node[mp].Next;
                    node[mp].Next = n;
                    mp = n;
                }
            }
            node[mp].Key = key;
            return mp;
        }

        /// <summary>getfreepos: lastfree walks down to a node whose key is nil.</summary>
        int GetFreePos()
        {
            while (lastFree > 0)
            {
                lastFree--;
                if (node[lastFree].Key.IsNil) return lastFree;
            }
            return -1;
        }

        // ---- rehash -----------------------------------------------------------------------------------------------

        static int ArrayIndex(in LuaValue key)
        {
            if (key.Type == LuaType.Number)
            {
                double n = key.Number;
                int k = Number2Int(n);
                if ((double)k == n) return k;
            }
            return -1;
        }

        static int CountInt(in LuaValue key, int[] nums)
        {
            int k = ArrayIndex(key);
            if (0 < k && k <= MaxASize) { nums[CeilLog2(k)]++; return 1; }
            return 0;
        }

        int NumUseArray(int[] nums)
        {
            int ause = 0, i = 1;
            for (int lg = 0, ttlg = 1; lg <= MaxBits; lg++, ttlg *= 2)
            {
                int lc = 0, lim = ttlg;
                if (lim > sizeArray)
                {
                    lim = sizeArray;
                    if (i > lim) break;
                }
                for (; i <= lim; i++) if (!array[i - 1].IsNil) lc++;
                nums[lg] += lc;
                ause += lc;
            }
            return ause;
        }

        int NumUseHash(int[] nums, ref int nasize)
        {
            int totaluse = 0, ause = 0;
            for (int i = SizeNode - 1; i >= 0; i--)
            {
                if (!node[i].Val.IsNil)
                {
                    ause += CountInt(node[i].Key, nums);
                    totaluse++;
                }
            }
            nasize += ause;
            return totaluse;
        }

        static int ComputeSizes(int[] nums, ref int narray)
        {
            int a = 0, na = 0, n = 0;
            for (int i = 0, twotoi = 1; twotoi / 2 < narray; i++, twotoi *= 2)
            {
                if (nums[i] > 0)
                {
                    a += nums[i];
                    if (a > twotoi / 2) { n = twotoi; na = a; }
                }
                if (a == narray) break;
            }
            narray = n;
            return na;
        }

        void Rehash(in LuaValue ek)
        {
            var nums = new int[MaxBits + 1];
            int nasize = NumUseArray(nums);
            int totaluse = nasize;
            totaluse += NumUseHash(nums, ref nasize);
            nasize += CountInt(ek, nums);
            totaluse++;
            int na = ComputeSizes(nums, ref nasize);
            Resize(nasize, totaluse - na);
        }

        /// <summary>luaH_resizearray (OP_SETLIST pre-allocation): the node part keeps its size but is rebuilt.</summary>
        void ResizeArray(int nasize) => Resize(nasize, dummy ? 0 : SizeNode);

        void Resize(int nasize, int nhsize)
        {
            int oldasize = sizeArray;
            Node[] nold = node;
            if (nasize > oldasize) SetArrayVector(nasize);
            SetNodeVector(nhsize);
            if (nasize < oldasize)
            {
                sizeArray = nasize;
                for (int i = nasize; i < oldasize; i++)
                    if (!array[i].IsNil) SetInt(i + 1, array[i]);
                Array.Resize(ref array, nasize);
            }
            for (int i = nold.Length - 1; i >= 0; i--)
            {
                if (!nold[i].Val.IsNil) Set(nold[i].Key, nold[i].Val);
            }
        }

        void SetArrayVector(int size)
        {
            if (size > array.Length) Array.Resize(ref array, size);
            for (int i = sizeArray; i < size; i++) array[i] = LuaValue.Nil;
            sizeArray = size;
        }

        void SetNodeVector(int size)
        {
            int lsize;
            if (size == 0)
            {
                node = new Node[1];
                node[0].Next = -1;
                dummy = true;
                lsize = 0;
                size = 0;
            }
            else
            {
                lsize = CeilLog2(size);
                if (lsize > MaxBits) throw new LuaScriptError("table overflow");
                size = 1 << lsize;
                node = new Node[size];
                for (int i = 0; i < size; i++) node[i].Next = -1;
                dummy = false;
            }
            lsizenode = lsize;
            lastFree = size;
        }

        // ---- OP_SETLIST -------------------------------------------------------------------------------------------

        /// <summary>
        /// OP_SETLIST: <paramref name="values"/> are R(A+1)…R(A+n); they go to keys (c − 1)·50 + 1 … in descending key
        /// order (luaH_setnum(last--)) after a luaH_resizearray when the last key exceeds the array part.
        /// </summary>
        public void SetList(IReadOnlyList<LuaValue> values, int c)
        {
            int n = values.Count;
            int last = (c - 1) * 50 + n;
            if (last > sizeArray) ResizeArray(last);
            for (; n > 0; n--) SetInt(last--, values[n - 1]);
        }

        // ---- next -------------------------------------------------------------------------------------------------

        /// <summary>
        /// luaH_next: the entry after <paramref name="key"/> (nil = the first) — array part by index, then the node vector in
        /// order, skipping nil values. Returns false at the end. An absent key is Lua's "invalid key to 'next'".
        /// </summary>
        public bool Next(ref LuaValue key, out LuaValue value)
        {
            int i = FindIndex(key);
            for (i++; i < sizeArray; i++)
            {
                if (!array[i].IsNil)
                {
                    key = LuaValue.Of((double)(i + 1));
                    value = array[i];
                    return true;
                }
            }
            for (i -= sizeArray; i < SizeNode; i++)
            {
                if (!node[i].Val.IsNil)
                {
                    key = node[i].Key;
                    value = node[i].Val;
                    return true;
                }
            }
            value = LuaValue.Nil;
            return false;
        }

        int FindIndex(in LuaValue key)
        {
            if (key.IsNil) return -1;
            int i = ArrayIndex(key);
            if (0 < i && i <= sizeArray) return i - 1;
            for (int n = MainPosition(key); n >= 0; n = node[n].Next)
                if (node[n].Key.Equals(key)) return n + sizeArray;
            throw new LuaScriptError("invalid key to 'next'");
        }

        /// <summary>The entries in <c>pairs</c> order (successive <see cref="Next"/> calls).</summary>
        public IEnumerable<KeyValuePair<LuaValue, LuaValue>> Pairs()
        {
            var k = LuaValue.Nil;
            while (Next(ref k, out var v)) yield return new KeyValuePair<LuaValue, LuaValue>(k, v);
        }
    }
}
