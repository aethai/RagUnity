// Synced from reference/port/src/RoClassicFx/FxRenderer.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Buffers.Binary;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// The slice of the client's renderer (g_renderer @ [0x011a60e0]) used by the effect engine.
    /// The renderer object lives in FxMemory at <see cref="Address"/> so ported and original code
    /// read the same fields: +0x4/+0x8 x/y factor, +0x20/+0x24 and +0x30/+0x34 screen centres,
    /// +0x70..+0x8c the RP quad pool (deque).
    /// </summary>
    public sealed partial class FxRenderer
    {
        public const int FaceSize = 0x6c;
        public const int VertexSize = 0x20;
        public const int QuadSize = 0xa8;
        public const uint RendererObjectSize = 0x100;

        public readonly FxMemory M;
        public readonly uint Address;

        /// <summary>AddRP (0x537dd0) triangle faces: (face address, flags).</summary>
        public Action<uint, int> FaceSubmitted;

        /// <summary>AddRP quad (0x537b00): (quad address, flags). Quad: +0 vertex ptr, +4 count, +8 texture, +0x18 src, +0x1c dst, +0x20 mtpreset.</summary>
        public Action<uint, int> QuadSubmitted;

        public FxRenderer(FxMemory memory)
        {
            M = memory;
            Address = memory.Alloc(RendererObjectSize);
        }

        public float XFactor { get => M.F32(Address + 0x4); set => M.F32(Address + 0x4, value); }
        public float YFactor { get => M.F32(Address + 0x8); set => M.F32(Address + 0x8, value); }
        public int CenterX { get => M.I32(Address + 0x30); set => M.I32(Address + 0x30, value); }
        public int CenterY { get => M.I32(Address + 0x34); set => M.I32(Address + 0x34, value); }
        /// <summary>Screen centre used by ProjectVertex2 (0x53b1a0), fields +0x20/+0x24.</summary>
        public int CenterX2 { get => M.I32(Address + 0x20); set => M.I32(Address + 0x20, value); }
        public int CenterY2 { get => M.I32(Address + 0x24); set => M.I32(Address + 0x24, value); }

        public void AddRP(uint face, int flags) => FaceSubmitted?.Invoke(face, flags);
        public void AddQuad(uint quad, int flags) => QuadSubmitted?.Invoke(quad, flags);

        /// <summary>
        /// ProjectVertex (0x53b370): world point → screen x/y, depth z and rhw, using a 4x3 view
        /// matrix at <paramref name="matrix"/>. Operation order follows the SSE code exactly.
        /// </summary>
        public unsafe void ProjectVertex(FxMemory m, uint matrix, float vx, float vy, float vz,
            out float sx, out float sy, out float sz, out float rhw)
        {
            if (!FxMemory.CanSpanF32(matrix))
            {
                ProjectVertexSlow(m, matrix, vx, vy, vz, out sx, out sy, out sz, out rhw);
                return;
            }
            // the matrix read once through a pointer (one range check), the renderer fields likewise; same arithmetic
            float* mat = (float*)m.Pointer(matrix, 48);
            byte* rf = M.Pointer(Address + 4, 0x34);   // +4 x factor, +8 y factor, +0x30 / +0x34 centre
            float xFactor = *(float*)rf;
            float yFactor = *(float*)(rf + 4);
            int centerX = *(int*)(rf + 0x2c);
            int centerY = *(int*)(rf + 0x30);
            float den = mat[5] * vy + mat[2] * vx;
            den += mat[8] * vz;
            den += mat[11];
            rhw = 1.0f / den;

            float x = mat[3] * vy + vx * mat[0];
            float w = 1.0f / rhw;
            x += mat[6] * vz;
            w -= 10.0f;
            x += mat[9];
            w *= 1.0067114f;
            x *= rhw;
            w *= rhw;
            x *= xFactor;
            x += (float)centerX;
            sx = x;

            float y = mat[4] * vy + mat[1] * vx;
            y += mat[7] * vz;
            y += mat[10];
            y *= rhw;
            y *= yFactor;
            sz = w;
            y += (float)centerY;
            sy = y;
        }

        void ProjectVertexSlow(FxMemory m, uint matrix, float vx, float vy, float vz,
            out float sx, out float sy, out float sz, out float rhw)
        {
            float den = m.F32(matrix + 0x14) * vy + m.F32(matrix + 0x8) * vx;
            den += m.F32(matrix + 0x20) * vz;
            den += m.F32(matrix + 0x2c);
            rhw = 1.0f / den;

            float x = m.F32(matrix + 0xc) * vy + vx * m.F32(matrix + 0x0);
            float w = 1.0f / rhw;
            x += m.F32(matrix + 0x18) * vz;
            w -= 10.0f;
            x += m.F32(matrix + 0x24);
            w *= 1.0067114f;
            x *= rhw;
            w *= rhw;
            x *= XFactor;
            x += (float)CenterX;
            sx = x;

            float y = m.F32(matrix + 0x10) * vy + m.F32(matrix + 0x4) * vx;
            y += m.F32(matrix + 0x1c) * vz;
            y += m.F32(matrix + 0x28);
            y *= rhw;
            y *= YFactor;
            sz = w;
            y += (float)CenterY;
            sy = y;
        }

        /// <summary>
        /// FUN_0053b1a0: screen x/y and rhw only, centre from +0x20/+0x24.
        /// Expression order as decompiled (verified by the oracle).
        /// </summary>
        public void ProjectVertex2(FxMemory m, uint matrix, float vx, float vy, float vz,
            out float sx, out float sy, out float rhw)
        {
            rhw = 1.0f / (m.F32(matrix + 0x8) * vx + m.F32(matrix + 0x14) * vy + m.F32(matrix + 0x20) * vz + m.F32(matrix + 0x2c));
            sx = (m.F32(matrix + 0xc) * vy + vx * m.F32(matrix) + m.F32(matrix + 0x18) * vz + m.F32(matrix + 0x24)) * rhw * XFactor + (float)CenterX2;
            sy = (m.F32(matrix + 0x4) * vx + m.F32(matrix + 0x10) * vy + m.F32(matrix + 0x1c) * vz + m.F32(matrix + 0x28)) * rhw * YFactor + (float)CenterY2;
        }

        /// <summary>
        /// The RP quad pool inlined in fn_b5d690: a std::deque&lt;RPQuad*&gt; at +0x70 (+0x74 map,
        /// +0x78 map size, +0x7c offset, +0x80 size) with an iterator at +0x84 (container proxy)
        /// / +0x8c (cursor). Pooled quads are reused after <see cref="BeginFrame"/>.
        /// </summary>
        public uint AcquireQuad()
        {
            uint r = Address;
            uint off = M.U32(r + 0x7c), size = M.U32(r + 0x80);
            uint quad;
            if (off + size == M.U32(r + 0x8c))
            {
                uint mapSize = M.U32(r + 0x78);
                if (mapSize <= size + 1)
                {
                    GrowQuadMap();
                    mapSize = M.U32(r + 0x78); off = M.U32(r + 0x7c); size = M.U32(r + 0x80);
                }
                off &= mapSize - 1;
                M.U32(r + 0x7c, off);
                uint slot = M.U32(r + 0x74) + ((size + off) & (mapSize - 1)) * 4;
                if (M.U32(slot) == 0) M.U32(slot, M.Alloc(QuadSize));
                InitQuad(M.U32(slot));
                size++;
                M.U32(r + 0x80, size);
                M.U32(r + 0x8c, off + size);
                M.U32(r + 0x84, M.U32(r + 0x70));
                quad = M.U32(M.U32(r + 0x74) + ((M.U32(r + 0x78) - 1) & (size + off - 1)) * 4);
            }
            else
            {
                uint proxy = M.U32(r + 0x84);
                uint container = proxy == 0 ? 0 : M.U32(proxy);
                uint cursor = M.U32(r + 0x8c);
                quad = M.U32(M.U32(container + 4) + ((M.U32(container + 8) - 1) & cursor) * 4);
                M.U32(r + 0x8c, cursor + 1);
                InitQuad(quad);
            }
            return quad;
        }

        /// <summary>Rewinds the quad cursor so pooled quads are reused for the next frame.</summary>
        public void BeginFrame() => M.U32(Address + 0x8c, M.U32(Address + 0x7c));

        /// <summary>Initialises an empty pool whose container proxy points at +0x70.</summary>
        public void InitQuadPool(uint capacity)
        {
            uint r = Address;
            uint map = M.Alloc(capacity * 4);
            M.U32(r + 0x70, r + 0x70);   // proxy → container (the deque object at +0x70)
            M.U32(r + 0x74, map);
            M.U32(r + 0x78, capacity);
            M.U32(r + 0x7c, 0);
            M.U32(r + 0x80, 0);
            M.U32(r + 0x84, r + 0x70);
            M.U32(r + 0x8c, 0);
        }

        /// <summary>Grows the map (FUN_00523320 equivalent: new power-of-two size, logical order kept).</summary>
        void GrowQuadMap()
        {
            uint r = Address;
            uint oldSize = M.U32(r + 0x78), oldMap = M.U32(r + 0x74), off = M.U32(r + 0x7c);
            uint newSize = oldSize == 0 ? 8 : oldSize * 2;
            if (newSize < 8) newSize = 8;
            uint newMap = MapAllocate(newSize * 4);
            for (uint j = 0; j < oldSize; j++) M.U32(newMap + j * 4, M.U32(oldMap + ((off + j) & (oldSize - 1)) * 4));
            if (oldMap != 0) M.Free(oldSize * 4 >= 0x1000 ? M.U32(oldMap - 4) : oldMap);
            M.U32(r + 0x74, newMap);
            M.U32(r + 0x78, newSize);
            M.U32(r + 0x8c, M.U32(r + 0x8c) - off);
            M.U32(r + 0x7c, 0);
        }

        /// <summary>
        /// std::_Allocate of the map (in FUN_00523320): blocks of 0x1000 bytes and more (1024+ pooled quads) are
        /// over-allocated by 0x23, aligned to 32 and keep the raw pointer at [-4] (released through it).
        /// </summary>
        uint MapAllocate(uint bytes)
        {
            if (bytes < 0x1000) return M.Alloc(bytes);
            uint raw = M.Alloc(bytes + 0x23);
            uint p = (raw + 0x23) & 0xffffffe0u;
            M.U32(p - 4, raw);
            return p;
        }

        /// <summary>
        /// FUN_00547010 — RP quad init: vertex pointer → inline 4 vertices at +0x28. Written through one span over the
        /// quad (the same stores; bytes +0x16/+0x17 stay untouched as before).
        /// </summary>
        void InitQuad(uint q)
        {
            Span<byte> s = M.Span(q, QuadSize);
            BinaryPrimitives.WriteUInt32LittleEndian(s.Slice(4), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(s, q + 0x28);
            s.Slice(8, 0xc).Clear();                                         // +8, +0xc, +0x10
            BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(0x14), 1);
            s.Slice(0x18, 0xc).Clear();                                      // +0x18, +0x1c, +0x20
            s.Slice(0x28, 4 * VertexSize).Clear();
            for (int k = 0; k < 4; k++) BinaryPrimitives.WriteUInt32LittleEndian(s.Slice(0x28 + k * VertexSize + 0x14), 0xff000000u);
            BinaryPrimitives.WriteUInt32LittleEndian(s.Slice(0x24), 5);
        }

        /// <summary>FUN_0053ae70: xfactor * rhw / 7 (x87 extended precision, returned as float).</summary>
        public float ScreenScale(float rhw) => (float)((double)XFactor * rhw / 7.0);
    }
}
