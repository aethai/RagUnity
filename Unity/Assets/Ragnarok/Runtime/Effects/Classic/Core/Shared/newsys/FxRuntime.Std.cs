// Synced from reference/port/src/RoClassicFx/Shared/newsys/FxRuntime.Std.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The MSVC 2019 STL idioms the new C++ effect system (CEffectMgr / CEffect, package newsys) runs on,
    /// operating on FxMemory with the client's exact allocation pattern: std::_Allocate (big blocks 32-byte
    /// aligned with the raw pointer at [-4]), std::string (16-byte SSO buffer, size +0x10, capacity +0x14),
    /// std::map red-black trees (_Find_lower_bound / _Insert_node / iterator ++) and std::list nodes.
    /// Every allocation and release happens in the same order and with the same size as in game.exe, so the
    /// oracle's whole-heap diff can compare the two.
    /// </summary>
    public sealed partial class FxRuntime
    {
        // ---- std::_Allocate / _Deallocate ------------------------------------------------------

        /// <summary>
        /// FUN_004d89d0 (std::_Allocate&lt;16&gt;): n bytes from operator new; blocks of 0x1000 bytes and more are
        /// over-allocated by 0x23, aligned to 32 and store the raw pointer at [-4]. 0 bytes → null.
        /// </summary>
        public uint StdAllocate(uint bytes)
        {
            if (bytes >= 0x1000)
            {
                uint raw = M.Alloc(bytes + 0x23);
                uint p = (raw + 0x23) & 0xffffffe0u;
                M.U32(p - 4, raw);
                return p;
            }
            return bytes == 0 ? 0u : M.Alloc(bytes);
        }

        /// <summary>The matching release (operator delete, FUN_00d48a81) of a block of <paramref name="bytes"/> bytes.</summary>
        public void StdDeallocate(uint p, uint bytes)
        {
            if (p == 0) return;
            M.Free(bytes >= 0x1000 ? M.U32(p - 4) : p);
        }

        // ---- std::string -----------------------------------------------------------------------

        /// <summary>Data pointer of the std::string at <paramref name="s"/> (inline buffer while capacity &lt; 16).</summary>
        public uint StrData(uint s) => M.U32(s + 0x14) >= 0x10 ? M.U32(s) : s;

        public int StrSize(uint s) => M.I32(s + 0x10);

        /// <summary>The byte string held by a std::string (one char per byte).</summary>
        public string StrRead(uint s)
        {
            uint d = StrData(s);
            int n = StrSize(s);
            var c = new char[n];
            for (int i = 0; i < n; i++) c[i] = (char)M.U8(d + (uint)i);
            return new string(c);
        }

        /// <summary>Default construction as inlined by the client: dword 0 at +0, size 0, capacity 15, buf[0] = 0.</summary>
        public void StrInitEmpty(uint s)
        {
            M.U32(s, 0);
            M.U32(s + 0x10, 0);
            M.U32(s + 0x14, 0xf);
            M.U8(s, 0);
        }

        /// <summary>
        /// std::string::assign(const char*, n) (FUN_004d9270): in place when it fits; otherwise grows to
        /// max(n | 15, cap + cap / 2) (clamped to 0x7fffffff), allocates cap + 1 (FUN_004d89d0), copies, then
        /// releases the old heap buffer.
        /// </summary>
        public void StrAssign(uint s, string bytes)
        {
            uint n = (uint)bytes.Length;
            uint cap = M.U32(s + 0x14);
            if (n <= cap)
            {
                uint d = cap >= 0x10 ? M.U32(s) : s;
                M.U32(s + 0x10, n);
                WriteBytes(d, bytes);
                M.U8(d + n, 0);
                return;
            }
            uint newCap = n | 0xf;
            if (newCap >= 0x80000000u) newCap = 0x7fffffff;
            else if (cap > 0x7fffffffu - (cap >> 1)) newCap = 0x7fffffff;
            else
            {
                uint grown = (cap >> 1) + cap;
                if (newCap < grown) newCap = grown;
            }
            uint buf = StdAllocate(newCap + 1);
            M.U32(s + 0x10, n);
            M.U32(s + 0x14, newCap);
            WriteBytes(buf, bytes);
            M.U8(buf + n, 0);
            if (cap >= 0x10) StdDeallocate(M.U32(s), cap + 1);
            M.U32(s, buf);
        }

        /// <summary>
        /// std::string copy construction (FUN_004ccbd0): a string shorter than 16 copies the whole 16-byte
        /// inline buffer (bytes after the terminator included) with capacity 15; a longer one allocates
        /// (size | 15) + 1 and copies size + 1 bytes.
        /// </summary>
        public void StrCopyConstruct(uint dst, uint src)
        {
            M.U32(dst, 0);
            M.U32(dst + 0x10, 0);
            M.U32(dst + 0x14, 0);
            uint size = M.U32(src + 0x10);
            uint data = M.U32(src + 0x14) >= 0x10 ? M.U32(src) : src;
            if (size < 0x10)
            {
                M.Copy(dst, data, 0x10);
                M.U32(dst + 0x10, size);
                M.U32(dst + 0x14, 0xf);
                return;
            }
            uint cap = size | 0xf;
            if (cap > 0x7fffffff) cap = 0x7fffffff;
            uint buf = StdAllocate(cap + 1);
            M.U32(dst, buf);
            M.Copy(buf, data, (int)size + 1);
            M.U32(dst + 0x10, size);
            M.U32(dst + 0x14, cap);
        }

        /// <summary>
        /// std::string move construction as inlined in the vector relocation (FUN_00a75610): the 16-byte
        /// buffer and size/capacity are taken over, the source is left empty (size 0, capacity 15, buf[0] = 0).
        /// </summary>
        public void StrMoveConstruct(uint dst, uint src)
        {
            M.U32(dst, 0);
            M.U32(dst + 0x10, 0);
            M.U32(dst + 0x14, 0);
            M.Copy(dst, src, 0x10);
            M.U32(dst + 0x10, M.U32(src + 0x10));
            M.U32(dst + 0x14, M.U32(src + 0x14));
            M.U32(src + 0x10, 0);
            M.U32(src + 0x14, 0xf);
            M.U8(src, 0);
        }

        /// <summary>std::string destructor / _Tidy_deallocate (FUN_004d8220).</summary>
        public void StrDestroy(uint s)
        {
            uint cap = M.U32(s + 0x14);
            if (cap >= 0x10) StdDeallocate(M.U32(s), cap + 1);
            M.U32(s + 0x10, 0);
            M.U32(s + 0x14, 0xf);
            M.U8(s, 0);
        }

        // A std::string local of the client (on the stack, outside the heap): only its heap buffer is modelled.

        /// <summary>
        /// assign() of <paramref name="bytes"/> to a freshly constructed local std::string: names of 16 bytes and
        /// more get a heap buffer of (n | 15) + 1 (returned), shorter ones stay in the stack buffer (0).
        /// </summary>
        public uint StrLocalAssign(string bytes)
        {
            if (bytes.Length < 0x10) return 0;
            uint cap = (uint)bytes.Length | 0xf;
            uint buf = StdAllocate(cap + 1);
            WriteBytes(buf, bytes);
            M.U8(buf + (uint)bytes.Length, 0);
            return buf;
        }

        /// <summary><see cref="StrLocalAssign(string)"/> of the <paramref name="length"/> bytes at <paramref name="src"/> (a C string in the heap).</summary>
        public uint StrLocalAssign(uint src, uint length)
        {
            if (length < 0x10) return 0;
            uint cap = length | 0xf;
            uint buf = StdAllocate(cap + 1);
            M.Copy(buf, src, (int)length);
            M.U8(buf + length, 0);
            return buf;
        }

        /// <summary>Destruction of a local std::string of <paramref name="length"/> bytes (<see cref="StrLocalAssign(uint, uint)"/>).</summary>
        public void StrLocalDestroy(uint length, uint buf)
        {
            if (buf != 0) StdDeallocate(buf, (length | 0xf) + 1);
        }

        /// <summary>
        /// FUN_004ccbd0 from a local std::string holding <paramref name="bytes"/>. For a short string the client
        /// copies the whole 16-byte stack buffer, whose bytes after the terminator are whatever the stack held;
        /// the port writes zeros there (no ported effect schedules a name shorter than 15 bytes).
        /// </summary>
        public void StrCopyConstructLocal(uint dst, string bytes)
        {
            uint n = (uint)bytes.Length;
            M.U32(dst, 0);
            M.U32(dst + 0x10, 0);
            M.U32(dst + 0x14, 0);
            if (n < 0x10)
            {
                for (uint i = 0; i < 0x10; i++) M.U8(dst + i, i < n ? (byte)bytes[(int)i] : (byte)0);
                M.U32(dst + 0x10, n);
                M.U32(dst + 0x14, 0xf);
                return;
            }
            uint cap = n | 0xf;
            uint buf = StdAllocate(cap + 1);
            M.U32(dst, buf);
            WriteBytes(buf, bytes);
            M.U8(buf + n, 0);
            M.U32(dst + 0x10, n);
            M.U32(dst + 0x14, cap);
        }

        /// <summary>Destruction of the local std::string made by <see cref="StrLocalAssign"/>.</summary>
        public void StrLocalDestroy(string bytes, uint buf)
        {
            if (buf != 0) StdDeallocate(buf, ((uint)bytes.Length | 0xf) + 1);
        }

        /// <summary>
        /// std::vector&lt;T*&gt;::push_back as inlined by the effects: in place while end != capacity, otherwise
        /// _Emplace_reallocate (FUN_00678110): capacity grows by half (at least size + 1), new buffer first,
        /// element stored, old contents moved, old buffer released.
        /// </summary>
        public void PtrVectorPushBack(uint vec, uint value)
        {
            uint end = M.U32(vec + 4);
            if (end != M.U32(vec + 8))
            {
                M.U32(end, value);
                M.U32(vec + 4, end + 4);
                return;
            }
            uint begin = M.U32(vec);
            int size = (int)(end - begin) >> 2;
            if (size == 0x3fffffff) throw new InvalidOperationException("vector too long");
            uint newSize = (uint)size + 1;
            uint cap = (uint)((int)(M.U32(vec + 8) - begin) >> 2);
            if (0x3fffffffu - (cap >> 1) < cap) throw new InvalidOperationException("vector too long");
            uint newCap = (cap >> 1) + cap;
            if (newCap < newSize) newCap = newSize;
            uint buf = StdAllocate(newCap * 4);
            M.U32(buf + (uint)size * 4, value);
            if (size > 0) M.Copy(buf, begin, size * 4);
            if (begin != 0) StdDeallocate(begin, (M.U32(vec + 8) - begin) & 0xfffffffcu);
            M.U32(vec, buf);
            M.U32(vec + 4, buf + newSize * 4);
            M.U32(vec + 8, buf + newCap * 4);
        }

        void WriteBytes(uint dst, string bytes)
        {
            for (int i = 0; i < bytes.Length; i++) M.U8(dst + (uint)i, (byte)bytes[i]);
        }

        /// <summary>
        /// A temporary std::string built from a C string and destroyed again (the client's by-value string
        /// arguments and locals): the heap sees exactly the allocation and release of its buffer.
        /// </summary>
        public void StrTemporary(string bytes)
        {
            if (bytes.Length < 0x10) return;
            uint cap = (uint)bytes.Length | 0xf;
            uint buf = StdAllocate(cap + 1);
            WriteBytes(buf, bytes);
            M.U8(buf + (uint)bytes.Length, 0);
            StdDeallocate(buf, cap + 1);
        }

        // ---- std::map (red-black tree) ----------------------------------------------------------
        // Node: +0 left, +4 parent, +8 right, +0xc color (0 red, 1 black), +0xd isnil, +0x10 key (int), value after.
        // Tree object: +0 head, +4 size; head: left = leftmost, parent = root, right = rightmost, isnil = 1.

        /// <summary>
        /// std::_Tree::_Find_lower_bound on an int key (FUN_00a42110 / FUN_00a420c0 / FUN_00a42160):
        /// the insertion location (parent, left child?) and the lower bound (head when none).
        /// </summary>
        public void TreeFindLowerBound(uint tree, int key, out uint parent, out bool left, out uint bound)
        {
            uint head = M.U32(tree);
            uint node = M.U32(head + 4);
            parent = node;
            left = false;
            bound = head;
            while (M.U8(node + 0xd) == 0)
            {
                parent = node;
                if (key <= M.I32(node + 0x10))
                {
                    left = true;
                    bound = node;
                    node = M.U32(node);
                }
                else
                {
                    left = false;
                    node = M.U32(node + 8);
                }
            }
        }

        /// <summary>The node holding <paramref name="key"/> or 0 (lower bound + key equality).</summary>
        public uint TreeFind(uint tree, int key)
        {
            TreeFindLowerBound(tree, key, out _, out _, out uint bound);
            if (M.U8(bound + 0xd) == 0 && key >= M.I32(bound + 0x10)) return bound;
            return 0;
        }

        /// <summary>std::_Tree_val::_Insert_node (0xa63850 / 0xa63a60): links, then the red-black fix-up.</summary>
        public void TreeInsertNode(uint tree, uint parent, bool left, uint node)
        {
            uint head = M.U32(tree);
            M.U32(tree + 4, M.U32(tree + 4) + 1);
            M.U32(node + 4, parent);
            if (parent == head)
            {
                M.U32(head, node);
                M.U32(head + 4, node);
                M.U32(head + 8, node);
                M.U8(node + 0xc, 1);
                return;
            }
            if (!left)
            {
                M.U32(parent + 8, node);
                if (parent == M.U32(head + 8)) M.U32(head + 8, node);
            }
            else
            {
                M.U32(parent, node);
                if (parent == M.U32(head)) M.U32(head, node);
            }
            for (uint p = node; M.U8(M.U32(p + 4) + 0xc) == 0;)
            {
                uint par = M.U32(p + 4), grand = M.U32(par + 4);
                if (par == M.U32(grand))
                {
                    uint sib = M.U32(grand + 8);
                    if (M.U8(sib + 0xc) == 0)
                    {
                        M.U8(par + 0xc, 1);
                        M.U8(sib + 0xc, 1);
                        M.U8(grand + 0xc, 0);
                        p = grand;
                    }
                    else
                    {
                        if (p == M.U32(par + 8))
                        {
                            p = par;
                            TreeRotateLeft(head, p);
                        }
                        M.U8(M.U32(p + 4) + 0xc, 1);
                        M.U8(M.U32(M.U32(p + 4) + 4) + 0xc, 0);
                        TreeRotateRight(head, M.U32(M.U32(p + 4) + 4));
                    }
                }
                else
                {
                    uint sib = M.U32(grand);
                    if (M.U8(sib + 0xc) == 0)
                    {
                        M.U8(par + 0xc, 1);
                        M.U8(sib + 0xc, 1);
                        M.U8(grand + 0xc, 0);
                        p = grand;
                    }
                    else
                    {
                        if (p == M.U32(par))
                        {
                            p = par;
                            TreeRotateRight(head, p);
                        }
                        M.U8(M.U32(p + 4) + 0xc, 1);
                        M.U8(M.U32(M.U32(p + 4) + 4) + 0xc, 0);
                        TreeRotateLeft(head, M.U32(M.U32(p + 4) + 4));
                    }
                }
            }
            M.U8(M.U32(head + 4) + 0xc, 1);
        }

        void TreeRotateLeft(uint head, uint w)
        {
            uint p = M.U32(w + 8);
            M.U32(w + 8, M.U32(p));
            if (M.U8(M.U32(p) + 0xd) == 0) M.U32(M.U32(p) + 4, w);
            M.U32(p + 4, M.U32(w + 4));
            if (w == M.U32(head + 4)) M.U32(head + 4, p);
            else if (w == M.U32(M.U32(w + 4))) M.U32(M.U32(w + 4), p);
            else M.U32(M.U32(w + 4) + 8, p);
            M.U32(p, w);
            M.U32(w + 4, p);
        }

        void TreeRotateRight(uint head, uint w)
        {
            uint p = M.U32(w);
            M.U32(w, M.U32(p + 8));
            if (M.U8(M.U32(p + 8) + 0xd) == 0) M.U32(M.U32(p + 8) + 4, w);
            M.U32(p + 4, M.U32(w + 4));
            if (w == M.U32(head + 4)) M.U32(head + 4, p);
            else if (w == M.U32(M.U32(w + 4) + 8)) M.U32(M.U32(w + 4) + 8, p);
            else M.U32(M.U32(w + 4), p);
            M.U32(p + 8, w);
            M.U32(w + 4, p);
        }

        /// <summary>Tree iterator ++ (FUN_00a46b90): the in-order successor of <paramref name="node"/>.</summary>
        public uint TreeNext(uint node)
        {
            uint right = M.U32(node + 8);
            if (M.U8(right + 0xd) == 0)
            {
                uint n = right;
                for (uint l = M.U32(n); M.U8(l + 0xd) == 0; l = M.U32(n)) n = l;
                return n;
            }
            uint parent = M.U32(node + 4);
            while (M.U8(parent + 0xd) == 0 && node == M.U32(parent + 8))
            {
                node = parent;
                parent = M.U32(parent + 4);
            }
            return parent;
        }

        /// <summary>A fresh tree head (operator new(<paramref name="nodeSize"/>), self-linked, color/isnil = 0x101).</summary>
        public uint TreeNewHead(uint nodeSize)
        {
            uint h = M.Alloc(nodeSize);
            M.U32(h, h);
            M.U32(h + 4, h);
            M.U32(h + 8, h);
            M.U16(h + 0xc, 0x101);
            return h;
        }
    }
}
