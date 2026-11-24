// Synced from reference/port/src/RoClassicFx/Prim/prims-3/Prim.Prims3.RenderHelpers.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>prims-3: vertex helper of the sprite renderer (the quad emitters fn_b0a900 / fn_b0b320 are the prims-1 ports).</summary>
    public readonly partial struct Prim
    {
        /// <summary>ProjectVertex into an RP face vertex (x, y, z, rhw, colour, specular 0xff000000, tu, tv).</summary>
        void P3Vertex(FxRenderer r, uint matrix, uint dst, float x, float y, float z, int color, float tu, float tv)
        {
            r.ProjectVertex(M, matrix, x, y, z, out float sx, out float sy, out float sz, out float rhw);
            M.TlVertex(dst, sx, sy, sz, rhw, (uint)color, tu, tv);
        }
    }
}
