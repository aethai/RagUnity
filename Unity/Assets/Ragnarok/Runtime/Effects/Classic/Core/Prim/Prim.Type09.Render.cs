// Synced from reference/port/src/RoClassicFx/Prim/Prim.Type09.Render.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// CEffectPrim::fn_af2870 (0xaf2870) — Render for prim types 9 / 0x75: a square pyramid
        /// (base ±f_318, apex f_360) rotated by the prim matrix (f_190..f_1bc), four textured
        /// triangles written to the RP face buffer at f_2210 and submitted via AddRP.
        /// </summary>
        public void Render_af2870(FxRenderer renderer, MsvcRand rand, uint viewMatrix)
        {
            float s = f_318, h = f_360, n = -s;
            // Vertex list exactly as laid out on the stack: 4 triangles × (A, B, C).
            System.Span<float> v = stackalloc float[]
            {
                n, n, 0f,   0f, 0f, h,   s, n, 0f,
                n, n, 0f,   0f, 0f, h,   n, s, 0f,
                n, s, 0f,   0f, 0f, h,   s, s, 0f,
                s, s, 0f,   0f, 0f, h,   s, n, 0f,
            };
            float u = 0f;
            if (M.U8(A + 0x450) == 3) // CMP byte ptr [ECX+0x450],3
            {
                int r1 = rand.Next();
                int r2 = rand.Next();
                m_argb = (r2 % 0xb + (r1 % 0xb) * 0x100) * 0x100 - 0xd7d701;
            }

            uint face = A + 0x2210;
            for (int tri = 0; tri < 4; tri++, face += FxRenderer.FaceSize)
            {
                int b = tri * 9;
                EmitVertex(renderer, viewMatrix, face + 0 * FxRenderer.VertexSize, v[b + 0], v[b + 1], v[b + 2], u, 1.0f);
                EmitVertex(renderer, viewMatrix, face + 1 * FxRenderer.VertexSize, v[b + 3], v[b + 4], v[b + 5], u, 0.0f);
                u += 0.2f;
                EmitVertex(renderer, viewMatrix, face + 2 * FxRenderer.VertexSize, v[b + 6], v[b + 7], v[b + 8], u, 1.0f);
                M.U32(face + 0x60, M.U32((uint)f_144));
                int flags;
                if (M.U8(A + 0x450) == 3) // CMP byte ptr [ECX+0x450],3
                {
                    M.I32(face + 0x64, 5); // D3DBLEND_SRCALPHA
                    M.I32(face + 0x68, 2); // D3DBLEND_ONE
                    flags = 5;
                }
                else
                {
                    flags = 1;
                }
                renderer.AddRP(face, flags);
            }
        }

        /// <summary>Rotates a local vertex by the prim matrix, offsets by the prim position and projects it.</summary>
        void EmitVertex(FxRenderer renderer, uint viewMatrix, uint dst, float x, float y, float z, float tu, float tv)
        {
            float wx = y * f_19c + x * f_190 + z * f_1a8 + f_1b4;
            float wy = y * f_1a0 + x * f_194 + z * f_1ac + f_1b8;
            float wz = y * f_1a4 + x * f_198 + z * f_1b0 + f_1bc;
            renderer.ProjectVertex(M, viewMatrix, m_pos_x + wx, m_pos_y + wy, wz + m_pos_z,
                out float sx, out float sy, out float sz, out float rhw);
            M.TlVertex(dst, sx, sy, sz, rhw, (uint)m_argb, tu, tv);
        }
    }
}
