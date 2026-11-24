// Synced from reference/port/src/RoClassicFx/FxMemory.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Flat little-endian address space that mirrors the original client's heap layout.
    /// Ported objects (CRagEffect, CEffectPrim, arrays) live at addresses inside this region,
    /// so decompiled pointer arithmetic translates 1:1 and a byte-for-byte comparison
    /// against the x86 oracle is possible.
    /// </summary>
    /// <remarks>
    /// Every field access of the engine goes through the accessors below, so they are written for speed: one
    /// unsigned range check (offset = address − base, wrapping below the base) and a single load or store through a
    /// pointer (Span and BinaryPrimitives cost several times more under Unity's Mono JIT); the allocator keeps its free
    /// lists in an array indexed by size class and the live block sizes in an open-addressing table. Nothing here
    /// allocates managed memory after construction.
    /// A fixed heap (max size = size, the oracle tests diff it through <see cref="Raw"/>) is a pinned managed array.
    /// A growable heap is one native block of the max size: growing only moves the limit, nothing is copied (a 48 MB
    /// copy into a fresh 96 MB array used to cost a 50 ms frame mid-skill), and the OS backs only the pages in use. On
    /// Windows the block is reserved address space and only the usable part is committed: a committed block charges its
    /// whole size against the system commit limit even while its pages are untouched.
    /// Hosts are little-endian (x86, x64, ARM64, ARMv7).
    /// </remarks>
    public sealed unsafe class FxMemory : IDisposable
    {
        public const uint DefaultBase = 0x30000000;

        /// <summary>The managed heap of a fixed heap (pinned by <see cref="pin"/>); null for a native one.</summary>
        readonly byte[] ram;
        GCHandle pin;
        IntPtr native;
        /// <summary>The heap's first byte: every load and store goes through it.</summary>
        readonly byte* mem;
        /// <summary>Usable bytes (the range check limit); on a reserved heap also the committed part.</summary>
        uint length;
        /// <summary>The native block is reserved address space (VirtualAlloc), committed up to <see cref="length"/>.</summary>
        readonly bool reserved;
        readonly uint baseAddress;
        readonly uint maxSize;
        uint bump;
        /// <summary>
        /// Native heaps: no block at or above this address has ever been handed out, so that memory still holds the OS's
        /// zero pages and a fresh bump block needs no clearing (and its untouched pages are never committed: a 64 KB
        /// CEffectPrim usually writes a few KB of it). Only grows, so blocks below it (free-listed, or bump blocks again
        /// after <see cref="RestoreAllocator"/>) are cleared. Fixed heaps clear every block (uint.MaxValue).
        /// </summary>
        uint cleanFrom = uint.MaxValue;

        /// <summary>
        /// Native heaps: a worker commits the pages ahead of the bump pointer, so the first write to a fresh block does not
        /// take a page fault on the main thread (a launch of 2600 64 KB CEffectPrims took ~36 000 of them, ~66 ms). Pages up
        /// to <see cref="faultedTo"/> (an offset) are committed; the worker keeps it <see cref="RunwayBytes"/> past the bump
        /// pointer. It touches a page with an interlocked compare-exchange of 0 with 0: a write cycle that commits the page
        /// and can never change a value the main thread stored there.
        /// </summary>
        const uint RunwayBytes = 96u << 20;
        const uint PageBytes = 4096;
        uint faultedTo;
        int runwayBusy;
        volatile bool released;
        readonly BlockTable sizes = new BlockTable();
        /// <summary>Free blocks per size class (<see cref="ClassIndex"/>), each a LIFO stack.</summary>
        readonly uint[][] freeLists = new uint[ClassCount][];
        readonly int[] freeCounts = new int[ClassCount];

        /// <param name="size">Initial heap size.</param>
        /// <param name="maxSize">
        /// Growth limit: when the bump pointer runs past the end, the heap doubles up to this size (addresses are base +
        /// offset, so every pointer stays valid). 0 = <paramref name="size"/>, fixed (the oracle tests diff fixed heaps).
        /// The client's own resource caches (STR / sprite / texture objects, never freed, like CResMgr) make the working
        /// set grow with the number of distinct effects a session shows.
        /// </param>
        public FxMemory(uint size = 64 * 1024 * 1024, uint baseAddress = DefaultBase, uint maxSize = 0)
        {
            this.baseAddress = baseAddress;
            this.maxSize = Math.Max(size, maxSize);
            // the accessors check "address - base < length" in 32 bits: the heap must not wrap around 2^32
            if ((ulong)baseAddress + this.maxSize > 0x100000000UL)
                throw new ArgumentOutOfRangeException(nameof(baseAddress), "FxMemory must end below 2^32 (base + max size)");
            if (this.maxSize == size)
            {
                ram = new byte[size];
                pin = GCHandle.Alloc(ram, GCHandleType.Pinned);
                mem = (byte*)pin.AddrOfPinnedObject();
            }
            else
            {
                // fresh pages from the OS (a block this large is never recycled heap memory) read as zero; blocks are
                // cleared when allocated anyway
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    native = VirtualAlloc(IntPtr.Zero, (UIntPtr)this.maxSize, MemReserve, PageNoAccess);
                    if (native == IntPtr.Zero) throw new OutOfMemoryException("FxMemory: cannot reserve the heap");
                    reserved = true;
                    Commit(0, size);
                }
                else
                {
                    native = Marshal.AllocHGlobal((IntPtr)this.maxSize);   // malloc: backed lazily (overcommit)
                }
                mem = (byte*)native;
                new Span<byte>(mem, 0x10).Clear();
            }
            length = size;
            bump = baseAddress + 0x10; // keep address 0 region-relative unused (null)
            if (native != IntPtr.Zero)
            {
                cleanFrom = bump;
                ExtendRunway();
            }
        }

        /// <summary>Releases the heap (the native block or the pin) now instead of at finalization: the GC does not see a
        /// native heap's size, so short-lived worlds (warm-up, tools) dispose theirs. Any later access faults.</summary>
        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        ~FxMemory() => Release();

        /// <summary>
        /// Starts the worker unless it is running: it touches the pages up to the bump pointer + <see cref="RunwayBytes"/>.
        /// A reserved heap first grows (commits) that far: the worker must only touch committed pages.
        /// </summary>
        void ExtendRunway()
        {
            if (released) return;
            ulong want = Math.Min((ulong)(bump - baseAddress) + RunwayBytes, maxSize);
            if (reserved && want > length) Grow(want);
            uint target = (uint)Math.Min(want, reserved ? length : maxSize);
            if (target <= Volatile.Read(ref faultedTo) || Interlocked.CompareExchange(ref runwayBusy, 1, 0) != 0) return;
            ThreadPool.UnsafeQueueUserWorkItem(_ => Prefault(target), null);
        }

        /// <summary>A reserved heap commits in steps of this size.</summary>
        const uint CommitStep = 16u << 20;

        const uint MemCommit = 0x1000, MemReserve = 0x2000, MemRelease = 0x8000, PageNoAccess = 0x01, PageReadWrite = 0x04;

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protect);

        [DllImport("kernel32", SetLastError = true)]
        static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

        /// <summary>Commits the reserved pages of offsets [<paramref name="from"/>, <paramref name="to"/>).</summary>
        void Commit(uint from, uint to)
        {
            if (to <= from) return;
            if (VirtualAlloc(native + (int)from, (UIntPtr)(to - from), MemCommit, PageReadWrite) == IntPtr.Zero)
                throw new OutOfMemoryException("FxMemory: cannot commit " + (to - from) + " bytes");
        }

        void Prefault(uint target)
        {
            try
            {
                uint from = Volatile.Read(ref faultedTo);
                for (uint offset = from & ~(PageBytes - 1); offset < target && !released; offset += PageBytes)
                    Interlocked.CompareExchange(ref *(int*)(mem + offset), 0, 0);
                if (!released && target > from) Volatile.Write(ref faultedTo, target);
            }
            finally
            {
                Volatile.Write(ref runwayBusy, 0);
            }
        }

        void Release()
        {
            released = true;
            // the native block must outlive the worker's last touch
            while (Volatile.Read(ref runwayBusy) != 0) Thread.Sleep(1);
            length = 0;
            if (pin.IsAllocated) pin.Free();
            if (native != IntPtr.Zero)
            {
                if (reserved) VirtualFree(native, UIntPtr.Zero, MemRelease);
                else Marshal.FreeHGlobal(native);
            }
            native = IntPtr.Zero;
        }

        public uint Base => baseAddress;
        public uint Size => length;
        /// <summary>Bytes handed out from the bump region so far (live blocks plus free-listed ones).</summary>
        public uint BumpBytes => bump - baseAddress;
        /// <summary>The heap as a managed array: fixed heaps only (the oracle tests).</summary>
        public byte[] Raw => ram ?? throw new InvalidOperationException("a growable FxMemory lives in native memory");

        public bool Contains(uint address, uint length = 1)
            => address >= baseAddress && (ulong)address + length <= (ulong)baseAddress + (ulong)this.length;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowOutside(uint address, int length)
            => throw new AccessViolationException($"FxMemory access 0x{address:x8} (+{length}) outside heap");

        int Offset(uint address, int length)
        {
            uint o = address - baseAddress;
            if ((ulong)o + (uint)length > this.length) ThrowOutside(address, length);
            return (int)o;
        }

        // ---- allocator --------------------------------------------------------------------------

        /// <summary>
        /// Zero-initialised allocation (8-byte aligned). Blocks are rounded to a size class and freed blocks are reused
        /// per class: up to 128 bytes in 8-byte steps, above that four classes per power of two (at most 25 % slack). An
        /// exact-size free list never reused a block for another size, so a long session with variable-size data (STR
        /// files, per-count prim arrays) walked the bump pointer to the end of the heap.
        /// </summary>
        public uint Alloc(uint size)
        {
            if (size == 0) size = 1;
            uint rounded = SizeClass(size);
            uint address;
            int cls = ClassIndex(rounded);
            int n = freeCounts[cls];
            bool fresh = false;
            if (n > 0)
            {
                address = freeLists[cls][n - 1];
                freeCounts[cls] = n - 1;
            }
            else
            {
                address = bump;
                bump += rounded;
                if (!Contains(address, rounded) && !Grow((ulong)address + rounded - baseAddress))
                    throw new OutOfMemoryException("FxMemory exhausted");
                fresh = address >= cleanFrom;
                if (native != IntPtr.Zero && bump - baseAddress + RunwayBytes / 2 > Volatile.Read(ref faultedTo)) ExtendRunway();
            }
            sizes.Set(address, rounded);
            if (fresh) cleanFrom = address + rounded;
            else new Span<byte>(mem + Offset(address, (int)rounded), (int)rounded).Clear();
            return address;
        }

        /// <summary>Doubles the usable size until <paramref name="needed"/> bytes fit (up to the max size); false when they
        /// cannot. The native block already spans the max size: nothing moves (a reserved one commits the new part).</summary>
        bool Grow(ulong needed)
        {
            if (needed > maxSize) return false;
            ulong size = length;
            if (reserved) size = (needed + CommitStep - 1) & ~(ulong)(CommitStep - 1);   // commit in steps, not doublings
            else while (size < needed) size *= 2;
            if (size > maxSize) size = maxSize;
            if (reserved) Commit(length, (uint)size);
            length = (uint)size;
            return true;
        }

        /// <summary>The block size an allocation of <paramref name="size"/> bytes gets.</summary>
        public static uint SizeClass(uint size)
        {
            uint s = (size + 7u) & ~7u;
            if (s <= 128u) return s;
            uint p = 128u;
            while (p <= s >> 1) p <<= 1;   // highest power of two <= s
            uint step = p >> 2;
            return (s + step - 1u) & ~(step - 1u);
        }

        /// <summary>Number of distinct size classes (<see cref="ClassIndex"/> range).</summary>
        const int ClassCount = 16 + 25 * 4;

        /// <summary>
        /// Dense index of a size class value: 8..128 → 0..15; above, 2^e + k·2^(e−2) (k = 0..3) → 16 + (e − 7)·4 + k − 1.
        /// Wrapped (0) classes of absurd sizes share slot 0 of the small classes' table end; they never reach the free lists
        /// with a usable block anyway.
        /// </summary>
        static int ClassIndex(uint rounded)
        {
            if (rounded <= 128u) return rounded == 0 ? ClassCount - 1 : (int)(rounded >> 3) - 1;
            int e = 7;
            while (e < 31 && (rounded >> (e + 1)) != 0) e++;
            int k = (int)((rounded >> (e - 2)) & 3u);
            return 16 + (e - 7) * 4 + k - 1;
        }

        /// <summary>Bytes in live allocations (block sizes); with <see cref="BumpPointer"/> it shows fragmentation.</summary>
        public long LiveBytes => sizes.TotalValues();

        public void Free(uint address)
        {
            if (address == 0) return;
            if (!sizes.Remove(address, out uint size)) return;
            int cls = ClassIndex(size);
            uint[] list = freeLists[cls];
            int n = freeCounts[cls];
            if (list == null) freeLists[cls] = list = new uint[16];
            else if (n == list.Length) Array.Resize(ref freeLists[cls], n * 2);
            freeLists[cls][n] = address;
            freeCounts[cls] = n + 1;
        }

        public uint BumpPointer => bump;

        /// <summary>Opaque allocator state (bump pointer, live sizes, free lists) for snapshot/restore.</summary>
        public object CaptureAllocator()
        {
            var lists = new uint[ClassCount][];
            for (int i = 0; i < ClassCount; i++)
            {
                lists[i] = new uint[freeCounts[i]];
                if (freeCounts[i] > 0) Array.Copy(freeLists[i], lists[i], freeCounts[i]);
            }
            return new AllocatorState { Bump = bump, Sizes = sizes.Clone(), FreeLists = lists };
        }

        public void RestoreAllocator(object state)
        {
            var s = (AllocatorState)state;
            bump = s.Bump;
            sizes.CopyFrom(s.Sizes);
            for (int i = 0; i < ClassCount; i++)
            {
                uint[] src = s.FreeLists[i];
                freeCounts[i] = src.Length;
                if (src.Length == 0) continue;
                if (freeLists[i] == null || freeLists[i].Length < src.Length) freeLists[i] = new uint[Math.Max(16, src.Length)];
                Array.Copy(src, freeLists[i], src.Length);
            }
        }

        sealed class AllocatorState
        {
            public uint Bump;
            public BlockTable Sizes;
            public uint[][] FreeLists;
        }

        /// <summary>
        /// Live block sizes by address: open addressing with linear probing and backward-shift deletion (no
        /// tombstones), keys are block addresses (never 0).
        /// </summary>
        sealed class BlockTable
        {
            uint[] keys = new uint[1024];
            uint[] values = new uint[1024];
            int count;

            int Slot(uint key) => (int)((key >> 3) * 0x9E3779B1u >> 7) & (keys.Length - 1);

            public void Set(uint key, uint value)
            {
                if ((count + 1) * 4 > keys.Length * 3) Resize(keys.Length * 2);
                int mask = keys.Length - 1;
                for (int i = Slot(key); ; i = (i + 1) & mask)
                {
                    uint k = keys[i];
                    if (k == key) { values[i] = value; return; }
                    if (k != 0) continue;
                    keys[i] = key;
                    values[i] = value;
                    count++;
                    return;
                }
            }

            public bool Remove(uint key, out uint value)
            {
                int mask = keys.Length - 1;
                int i = Slot(key);
                for (; ; i = (i + 1) & mask)
                {
                    uint k = keys[i];
                    if (k == key) break;
                    if (k == 0) { value = 0; return false; }
                }
                value = values[i];
                count--;
                // backward-shift deletion: pull later entries of the cluster into the hole when their home allows
                int hole = i;
                for (int j = (i + 1) & mask; keys[j] != 0; j = (j + 1) & mask)
                {
                    int home = Slot(keys[j]);
                    bool movable = hole <= j ? home <= hole || home > j : home <= hole && home > j;
                    if (!movable) continue;
                    keys[hole] = keys[j];
                    values[hole] = values[j];
                    hole = j;
                }
                keys[hole] = 0;
                values[hole] = 0;
                return true;
            }

            void Resize(int capacity)
            {
                uint[] oldKeys = keys, oldValues = values;
                keys = new uint[capacity];
                values = new uint[capacity];
                count = 0;
                for (int i = 0; i < oldKeys.Length; i++)
                    if (oldKeys[i] != 0) Set(oldKeys[i], oldValues[i]);
            }

            public long TotalValues()
            {
                long total = 0;
                for (int i = 0; i < keys.Length; i++) if (keys[i] != 0) total += values[i];
                return total;
            }

            public BlockTable Clone()
            {
                var t = new BlockTable { keys = (uint[])keys.Clone(), values = (uint[])values.Clone(), count = count };
                return t;
            }

            public void CopyFrom(BlockTable other)
            {
                keys = (uint[])other.keys.Clone();
                values = (uint[])other.values.Clone();
                count = other.count;
            }
        }

        // ---- accessors ------------------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte U8(uint a)
        {
            uint o = a - baseAddress;
            if (o >= length) ThrowOutside(a, 1);
            return mem[o];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void U8(uint a, byte v)
        {
            uint o = a - baseAddress;
            if (o >= length) ThrowOutside(a, 1);
            mem[o] = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte I8(uint a) => (sbyte)U8(a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void I8(uint a, sbyte v) => U8(a, (byte)v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short I16(uint a)
        {
            uint o = a - baseAddress;
            if ((ulong)o + 2 > length) ThrowOutside(a, 2);
            return *(short*)(mem + o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void I16(uint a, short v)
        {
            uint o = a - baseAddress;
            if ((ulong)o + 2 > length) ThrowOutside(a, 2);
            *(short*)(mem + o) = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort U16(uint a) => (ushort)I16(a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void U16(uint a, ushort v) => I16(a, (short)v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int I32(uint a)
        {
            uint o = a - baseAddress;
            if ((ulong)o + 4 > length) ThrowOutside(a, 4);
            return *(int*)(mem + o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void I32(uint a, int v)
        {
            uint o = a - baseAddress;
            if ((ulong)o + 4 > length) ThrowOutside(a, 4);
            *(int*)(mem + o) = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint U32(uint a) => (uint)I32(a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void U32(uint a, uint v) => I32(a, (int)v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float F32(uint a) => BitConverter.Int32BitsToSingle(I32(a));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void F32(uint a, float v) => I32(a, BitConverter.SingleToInt32Bits(v));

        /// <summary>
        /// <paramref name="count"/> floats at <paramref name="address"/> as a span over the heap (one range
        /// check instead of one per element). Only where <see cref="CanSpanF32"/> holds — a little-endian host and a
        /// 4-aligned address (ARMv7 faults on unaligned float loads); callers fall back to <see cref="F32(uint)"/>
        /// otherwise. Valid until the next allocation.
        /// </summary>
        public Span<float> F32Span(uint address, int count)
            => new Span<float>(mem + Offset(address, count * 4), count);

        /// <summary>Whether <see cref="F32Span"/> may be used at <paramref name="address"/>.</summary>
        public static bool CanSpanF32(uint address) => BitConverter.IsLittleEndian && (address & 3) == 0;

        /// <summary>
        /// The bytes [<paramref name="address"/>, +<paramref name="length"/>) as a span over the heap (checked once).
        /// Valid until the next allocation (the heap may grow and move).
        /// </summary>
        public Span<byte> Span(uint address, int length) => new Span<byte>(mem + Offset(address, length), length);

        /// <summary>
        /// Pointer to [<paramref name="address"/>, +<paramref name="length"/>) (checked once) for hot paths that read a
        /// run of fields. Valid until the next allocation.
        /// </summary>
        public byte* Pointer(uint address, int length) => mem + Offset(address, length);

        /// <summary>
        /// One D3D TL vertex at <paramref name="a"/> (0x20 bytes): x, y, z, rhw, colour, specular 0xff000000, tu, tv — the
        /// eight stores every face / quad emitter does after ProjectVertex, with one range check.
        /// </summary>
        public void TlVertex(uint a, float x, float y, float z, float rhw, uint color, float tu, float tv)
        {
            int* v = (int*)(mem + Offset(a, 0x20));
            v[0] = BitConverter.SingleToInt32Bits(x);
            v[1] = BitConverter.SingleToInt32Bits(y);
            v[2] = BitConverter.SingleToInt32Bits(z);
            v[3] = BitConverter.SingleToInt32Bits(rhw);
            v[4] = (int)color;
            v[5] = unchecked((int)0xff000000u);
            v[6] = BitConverter.SingleToInt32Bits(tu);
            v[7] = BitConverter.SingleToInt32Bits(tv);
        }

        /// <summary>
        /// Reads back the fields of a TL vertex a host draws from (<see cref="TlVertex"/>'s layout): screen x / y, rhw,
        /// colour and texture coordinates, with one range check.
        /// </summary>
        public void ReadTlVertex(uint a, out float x, out float y, out float rhw, out uint color, out float tu, out float tv)
        {
            int* v = (int*)(mem + Offset(a, 0x20));   // integer loads: ARMv7 faults only on unaligned float loads
            x = BitConverter.Int32BitsToSingle(v[0]);
            y = BitConverter.Int32BitsToSingle(v[1]);
            rhw = BitConverter.Int32BitsToSingle(v[3]);
            color = (uint)v[4];
            tu = BitConverter.Int32BitsToSingle(v[6]);
            tv = BitConverter.Int32BitsToSingle(v[7]);
        }

        /// <summary>memmove within the heap (overlapping ranges behave like a copy through a temporary).</summary>
        public void Copy(uint dst, uint src, int length)
        {
            int s = Offset(src, length), d = Offset(dst, length);
            new ReadOnlySpan<byte>(mem + s, length).CopyTo(new Span<byte>(mem + d, length));
        }

        public void Fill(uint dst, byte value, int length)
        {
            int o = Offset(dst, length);
            new Span<byte>(mem + o, length).Fill(value);
        }

        public byte[] Read(uint address, int length)
        {
            var result = new byte[length];
            new ReadOnlySpan<byte>(mem + Offset(address, length), length).CopyTo(result);
            return result;
        }

        public void Write(uint address, byte[] data)
            => data.AsSpan().CopyTo(new Span<byte>(mem + Offset(address, data.Length), data.Length));
    }
}
