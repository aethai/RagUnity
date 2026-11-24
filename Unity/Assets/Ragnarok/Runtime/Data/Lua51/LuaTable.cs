using System;
using System.Collections;
using System.Collections.Generic;

namespace Ragnarok.Data.Lua51
{
    /// <summary>
    /// A Lua 5.1 table with ltable.c's exact layout: array part, node vector of 2^lsizenode nodes with chained collisions
    /// (the shared dummy node when empty), lastfree, rehash (numusearray / numusehash / countint / computesizes / resize).
    /// <see cref="Next"/> (luaH_next) therefore enumerates in the same order as the client's Lua: array part by index, then
    /// the nodes in vector order. Main positions: numbers by hashnum (the double's two 32-bit words summed, modulo
    /// (size − 1) | 1; ±0 → node 0), strings by luaS_hash &amp; (size − 1), booleans by value, tables / functions /
    /// userdata by a per-object id (the C pointer hash cannot be reproduced; its order is arbitrary in Lua too).
    /// Integral keys go through lua_number2int. The garbage collector's dead keys are not emulated (a key set to nil stays
    /// in its node until the next rehash, as in Lua between collections). Indexers and Get/Set are raw (no metamethods).
    /// </summary>
    public sealed class LuaTable : IEnumerable<KeyValuePair<LuaValue, LuaValue>>
    {
        const int MaxBits = 26;                 // MAXBITS (LUAI_BITSINT 32 > 26)
        const int MaxASize = 1 << MaxBits;

        struct Node
        {
            public LuaValue Key, Val;
            public int Next;                    // node index, −1 = NULL
        }

        static readonly LuaValue[] EmptyArray = new LuaValue[0];

        LuaValue[] array = EmptyArray;
        int sizeArray;
        Node[] node;
        bool dummy;                             // node == dummynode (sizenode 1, never written)
        int lastFree;                           // lastfree as a node index (the vector size when all are free)

        internal readonly int Id = LuaConv.NewObjectId();

        /// <summary>The metatable (null when none). Setting it directly bypasses __metatable protection.</summary>
        public LuaTable Metatable;

        /// <summary>luaH_new(L, narray, nhash) — NEWTABLE uses luaO_fb2int of its B / C operands.</summary>
        public LuaTable(int narray = 0, int nhash = 0)
        {
            SetArrayVector(narray);
            SetNodeVector(nhash);
        }

        /// <summary>Size of the array part (t->sizearray).</summary>
        public int ArraySize => sizeArray;

        /// <summary>Number of nodes (sizenode(t); 0 for the dummy node).</summary>
        public int NodeCount => dummy ? 0 : node.Length;

        /// <summary>Number of non-nil entries (O(n)).</summary>
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

        public LuaValue this[LuaValue key]
        {
            get => Get(key);
            set => Set(key, value);
        }

        public LuaValue this[string key]
        {
            get => Get(LuaValue.Of(key));
            set => Set(LuaValue.Of(key), value);
        }

        public LuaValue this[int key]
        {
            get => GetInt(key);
            set => SetInt(key, value);
        }

        // ---- luaO helpers ------------------------------------------------------------------------------------------

        /// <summary>luaO_fb2int: the "floating point byte" of NEWTABLE's B / C operands.</summary>
        public static int Fb2Int(int x)
        {
            int e = (x >> 3) & 31;
            return e == 0 ? x : ((x & 7) + 8) << (e - 1);
        }

        static int Log2(uint x)
        {
            int l = -1;
            while (x != 0) { x >>= 1; l++; }
            return l;
        }

        static int CeilLog2(int x) => Log2((uint)(x - 1)) + 1;

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

        int HashNum(double n)
        {
            if (n == 0) return 0;               // luai_numeq(n, 0): avoid problems with −0
            long bits = BitConverter.DoubleToInt64Bits(n);
            uint a = unchecked((uint)bits + (uint)(bits >> 32));
            return (int)(a % (uint)((node.Length - 1) | 1));
        }

        int MainPosition(in LuaValue key)
        {
            switch (key.Type)
            {
                case LuaType.Number: return HashNum(key.N);
                case LuaType.String: return (int)((uint)key.N & (uint)(node.Length - 1));
                case LuaType.Boolean: return (key.N != 0 ? 1 : 0) & (node.Length - 1);
                default: return (int)((uint)LuaConv.ObjectId(key.O) % (uint)((node.Length - 1) | 1));
            }
        }

        // ---- get --------------------------------------------------------------------------------------------------

        /// <summary>luaH_get: the raw value of <paramref name="key"/> (nil when absent).</summary>
        public LuaValue Get(in LuaValue key)
        {
            switch (key.Type)
            {
                case LuaType.String:
                {
                    var s = (string)key.O;
                    double h = key.N;
                    for (int i = (int)((uint)h & (uint)(node.Length - 1)); i >= 0; i = node[i].Next)
                    {
                        ref Node nd = ref node[i];
                        if (nd.Key.Type == LuaType.String && (ReferenceEquals(nd.Key.O, s) || (nd.Key.N == h && string.Equals((string)nd.Key.O, s))))
                            return nd.Val;
                    }
                    return LuaValue.Nil;
                }
                case LuaType.Number:
                {
                    double n = key.N;
                    if (n >= -2147483648.0 && n <= 2147483647.0)
                    {
                        int k = (int)n;
                        if (k == n) return GetInt(k);
                    }
                    break;
                }
                case LuaType.Nil:
                    return LuaValue.Nil;
            }
            for (int i = MainPosition(key); i >= 0; i = node[i].Next)
                if (node[i].Key.Equals(key)) return node[i].Val;
            return LuaValue.Nil;
        }

        /// <summary>luaH_getstr for a string key.</summary>
        public LuaValue Get(string key) => Get(LuaValue.Of(key));

        /// <summary>luaH_getnum: the raw value at integer key <paramref name="key"/>.</summary>
        public LuaValue GetInt(int key)
        {
            if (unchecked((uint)(key - 1)) < (uint)sizeArray) return array[key - 1];
            double nk = key;
            for (int i = HashNum(nk); i >= 0; i = node[i].Next)
                if (node[i].Key.Type == LuaType.Number && node[i].Key.N == nk) return node[i].Val;
            return LuaValue.Nil;
        }

        /// <summary>The slot holding <paramref name="key"/> (its value may be nil) or index −1.</summary>
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
                    double n = key.N;
                    if (n >= -2147483648.0 && n <= 2147483647.0)
                    {
                        int k = (int)n;
                        if (k == n) { FindSlotNum(k, out inArray, out index); return; }
                    }
                    break;
                }
            }
            for (int i = MainPosition(key); i >= 0; i = node[i].Next)
                if (node[i].Key.Equals(key)) { index = i; return; }
        }

        void FindSlotNum(int key, out bool inArray, out int index)
        {
            if (unchecked((uint)(key - 1)) < (uint)sizeArray) { inArray = true; index = key - 1; return; }
            inArray = false;
            double nk = key;
            for (int i = HashNum(nk); i >= 0; i = node[i].Next)
                if (node[i].Key.Type == LuaType.Number && node[i].Key.N == nk) { index = i; return; }
            index = -1;
        }

        // ---- set --------------------------------------------------------------------------------------------------

        /// <summary>
        /// Raw <c>t[key] = value</c> (luaH_set + store): an absent key gets a node even when <paramref name="value"/> is nil,
        /// as in Lua. Throws "table index is nil" / "table index is NaN".
        /// </summary>
        public void Set(in LuaValue key, in LuaValue value)
        {
            if (key.Type == LuaType.String)
            {
                var s = (string)key.O;
                double h = key.N;
                for (int i = (int)((uint)h & (uint)(node.Length - 1)); i >= 0; i = node[i].Next)
                {
                    ref Node nd = ref node[i];
                    if (nd.Key.Type == LuaType.String && (ReferenceEquals(nd.Key.O, s) || (nd.Key.N == h && string.Equals((string)nd.Key.O, s))))
                    {
                        nd.Val = value;
                        return;
                    }
                }
                int idx = NewKey(key, out bool inArr);
                if (inArr) array[idx] = value; else node[idx].Val = value;
                return;
            }
            SetSlot(key, out bool inArray, out int index);
            if (inArray) array[index] = value; else node[index].Val = value;
        }

        public void Set(string key, LuaValue value) => Set(LuaValue.Of(key), value);

        /// <summary>luaH_setnum + store (OP_SETLIST, lua_rawseti).</summary>
        public void SetInt(int key, in LuaValue value)
        {
            if (unchecked((uint)(key - 1)) < (uint)sizeArray) { array[key - 1] = value; return; }
            FindSlotNum(key, out bool inArray, out int index);
            if (index < 0) index = NewKey(LuaValue.Of((double)key), out inArray);
            if (inArray) array[index] = value; else node[index].Val = value;
        }

        /// <summary>luaH_set without the store: makes sure <paramref name="key"/> has a slot (luaV_settable's primitive set before a __newindex call).</summary>
        internal void Touch(in LuaValue key) => SetSlot(key, out _, out _);

        void SetSlot(in LuaValue key, out bool inArray, out int index)
        {
            FindSlot(key, out inArray, out index);
            if (index >= 0) return;
            if (key.IsNil) throw new LuaException("table index is nil");
            if (key.Type == LuaType.Number && double.IsNaN(key.N)) throw new LuaException("table index is NaN");
            index = NewKey(key, out inArray);
        }

        /// <summary>newkey (ltable.c): main position, moving a colliding node that is not in its own main position, or into the free position lastfree finds; a full vector rehashes.</summary>
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
                double n = key.N;
                if (n >= -2147483648.0 && n <= 2147483647.0)
                {
                    int k = (int)n;
                    if (k == n) return k;
                }
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
            for (int i = node.Length - 1; i >= 0; i--)
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
        internal void ResizeArray(int nasize) => Resize(nasize, dummy ? 0 : node.Length);

        void Resize(int nasize, int nhsize)
        {
            int oldasize = sizeArray;
            Node[] nold = node;
            bool oldDummy = dummy;
            if (nasize > oldasize) SetArrayVector(nasize);
            SetNodeVector(nhsize);
            if (nasize < oldasize)
            {
                sizeArray = nasize;
                for (int i = nasize; i < oldasize; i++)
                    if (!array[i].IsNil) SetInt(i + 1, array[i]);
                Array.Resize(ref array, nasize);
            }
            if (!oldDummy)
            {
                for (int i = nold.Length - 1; i >= 0; i--)
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
            if (size == 0)
            {
                node = new Node[1];
                node[0].Next = -1;
                dummy = true;
                size = 0;
            }
            else
            {
                int lsize = CeilLog2(size);
                if (lsize > MaxBits) throw new LuaException("table overflow");
                size = 1 << lsize;
                node = new Node[size];
                for (int i = 0; i < size; i++) node[i].Next = -1;
                dummy = false;
            }
            lastFree = size;
        }

        // ---- length -----------------------------------------------------------------------------------------------

        /// <summary>luaH_getn: a border of the table (the # operator on a table, luaL_getn).</summary>
        public int Length
        {
            get
            {
                int j = sizeArray;
                if (j > 0 && array[j - 1].IsNil)
                {
                    int i = 0;
                    while (j - i > 1)
                    {
                        int m = (int)(((uint)i + (uint)j) / 2);
                        if (array[m - 1].IsNil) j = m; else i = m;
                    }
                    return i;
                }
                if (dummy) return j;
                return UnboundSearch(j);
            }
        }

        int UnboundSearch(int jj)
        {
            uint i = (uint)jj;
            uint j = i + 1;
            while (!GetIntU(j).IsNil)
            {
                i = j;
                j *= 2;
                if (j > int.MaxValue)
                {
                    i = 1;
                    while (!GetIntU(i).IsNil) i++;
                    return (int)(i - 1);
                }
            }
            while (j - i > 1)
            {
                uint m = (i + j) / 2;
                if (GetIntU(m).IsNil) j = m; else i = m;
            }
            return (int)i;
        }

        LuaValue GetIntU(uint k) => k <= int.MaxValue ? GetInt((int)k) : Get(LuaValue.Of((double)k));

        // ---- next -------------------------------------------------------------------------------------------------

        /// <summary>
        /// luaH_next: the entry after <paramref name="key"/> (nil = the first) — array part by index, then the node vector in
        /// order, skipping nil values. Returns false at the end. An absent key throws "invalid key to 'next'".
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
            if (!dummy)
            {
                for (i -= sizeArray; i < node.Length; i++)
                {
                    if (!node[i].Val.IsNil)
                    {
                        key = node[i].Key;
                        value = node[i].Val;
                        return true;
                    }
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
            if (!dummy)
            {
                for (int n = MainPosition(key); n >= 0; n = node[n].Next)
                    if (node[n].Key.Equals(key)) return n + sizeArray;
            }
            throw new LuaException("invalid key to 'next'");
        }

        // ---- enumeration ------------------------------------------------------------------------------------------

        /// <summary>Enumerates the non-nil entries in pairs() order. Do not add new keys while enumerating (as in Lua).</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<LuaValue, LuaValue>> IEnumerable<KeyValuePair<LuaValue, LuaValue>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<KeyValuePair<LuaValue, LuaValue>>
        {
            readonly LuaTable t;
            int index;                          // next slot to look at: array part, then nodes
            KeyValuePair<LuaValue, LuaValue> current;

            internal Enumerator(LuaTable t)
            {
                this.t = t;
                index = 0;
                current = default;
            }

            public KeyValuePair<LuaValue, LuaValue> Current => current;

            object IEnumerator.Current => current;

            public bool MoveNext()
            {
                while (index < t.sizeArray)
                {
                    int i = index++;
                    if (!t.array[i].IsNil)
                    {
                        current = new KeyValuePair<LuaValue, LuaValue>(LuaValue.Of((double)(i + 1)), t.array[i]);
                        return true;
                    }
                }
                if (t.dummy) return false;
                while (index - t.sizeArray < t.node.Length)
                {
                    int i = index++ - t.sizeArray;
                    if (!t.node[i].Val.IsNil)
                    {
                        current = new KeyValuePair<LuaValue, LuaValue>(t.node[i].Key, t.node[i].Val);
                        return true;
                    }
                }
                return false;
            }

            public void Reset() { index = 0; current = default; }

            public void Dispose() { }
        }

        public override string ToString() => "table: " + Id.ToString("X8");
    }
}
