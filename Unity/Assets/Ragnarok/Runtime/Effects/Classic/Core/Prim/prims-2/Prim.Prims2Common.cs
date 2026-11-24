// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Prims2Common.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        /// <summary>
        /// The stack TL-vertex idiom of the prims-2 render functions: FUN_0053b370 (ProjectVertex) into a
        /// zeroed {x, y, z, rhw} block, then the vertex copied to <paramref name="dst"/> with
        /// color / specular 0xff000000 / tu / tv.
        /// </summary>
        void P2Vertex(FxRuntime rt, uint viewMatrix, uint dst, float x, float y, float z, uint color, float tu, float tv)
        {
            rt.Renderer.ProjectVertex(M, viewMatrix, x, y, z, out float sx, out float sy, out float sz, out float rhw);
            M.TlVertex(dst, sx, sy, sz, rhw, color, tu, tv);
        }

        /// <summary>((alpha * 0x100 + r) * 0x100 + g) * 0x100 + b with the prim's colour bytes f_23c/f_240/f_244.</summary>
        uint P2Argb(float alpha) =>
            (uint)(((FxOps.Cvtt(alpha) * 0x100 + M.U8(A + 0x23c)) * 0x100 + M.U8(A + 0x240)) * 0x100 + M.U8(A + 0x244));
    }
}
