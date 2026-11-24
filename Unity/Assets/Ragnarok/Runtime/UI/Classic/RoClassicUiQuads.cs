using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>28-byte UI vertex: screen pixels (origin top-left, y down), anchor slot, colour, atlas uv + page.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RoClassicUiVertex {
        public float X;
        public float Y;
        public float Anchor;
        public Color32 Color;
        public float U;
        public float V;
        public float Page;
    }

    /// <summary>
    /// Quad list for one UI layer. 4 vertices per quad (TL, TR, BR, BL). Optional CPU clip rectangle (scroll areas)
    /// trims geometry and UVs, so clipping never needs a scissor/state change. <see cref="Anchor"/> is stamped on
    /// every emitted vertex: 0 = screen space, n = moved each frame by the renderer's anchor n (world overlays).
    /// </summary>
    public sealed class RoClassicUiQuads {
        public RoClassicUiVertex[] Vertices = new RoClassicUiVertex[256];
        public int VertexCount;
        public float Anchor;

        /// <summary>
        /// Emit without the RGB555 quantisation every classic window bitmap goes through (TGA/PNG windows, which
        /// the original keeps in A8R8G8B8). Encoded in the page sign: page >= 0 quantised, -(page + 1) full colour.
        /// </summary>
        public bool FullColor;

        private bool clipping;
        private float clipX0;
        private float clipY0;
        private float clipX1;
        private float clipY1;

        public int QuadCount => VertexCount >> 2;

        public void Clear() {
            VertexCount = 0;
            clipping = false;
        }

        public void SetClip(float x, float y, float width, float height) {
            clipping = true;
            clipX0 = x;
            clipY0 = y;
            clipX1 = x + width;
            clipY1 = y + height;
        }

        public void ClearClip() => clipping = false;

        private void Ensure(int more) {
            int need = VertexCount + more;
            if (need > Vertices.Length) {
                Array.Resize(ref Vertices, Math.Max(need, Vertices.Length * 2));
            }
        }

        public void Image(float x, float y, in RoClassicUiImage image, Color32 color) =>
            Quad(x, y, image.Width, image.Height, image.Uv, image.Page, color);

        public void Image(float x, float y, float width, float height, in RoClassicUiImage image, Color32 color) =>
            Quad(x, y, width, height, image.Uv, image.Page, color);

        /// <param name="uv">(u0, vTop, u1, vBottom)</param>
        public void Quad(float x, float y, float w, float h, Vector4 uv, float page, Color32 color) {
            float x0 = x, y0 = y, x1 = x + w, y1 = y + h;
            float u0 = uv.x, vt = uv.y, u1 = uv.z, vb = uv.w;
            if (clipping) {
                if (x1 <= clipX0 || y1 <= clipY0 || x0 >= clipX1 || y0 >= clipY1) return;
                if (x0 < clipX0) { u0 += (u1 - u0) * (clipX0 - x0) / w; x0 = clipX0; }
                if (x1 > clipX1) { u1 -= (u1 - u0) * (x1 - clipX1) / (x1 - x0); x1 = clipX1; }
                if (y0 < clipY0) { vt += (vb - vt) * (clipY0 - y0) / h; y0 = clipY0; }
                if (y1 > clipY1) { vb -= (vb - vt) * (y1 - clipY1) / (y1 - y0); y1 = clipY1; }
            }

            Ensure(4);
            RoClassicUiVertex[] v = Vertices;
            int i = VertexCount;
            float a = Anchor;
            if (FullColor) page = -page - 1f;
            v[i].X = x0; v[i].Y = y0; v[i].Anchor = a; v[i].Color = color; v[i].U = u0; v[i].V = vt; v[i].Page = page;
            v[i + 1].X = x1; v[i + 1].Y = y0; v[i + 1].Anchor = a; v[i + 1].Color = color; v[i + 1].U = u1; v[i + 1].V = vt; v[i + 1].Page = page;
            v[i + 2].X = x1; v[i + 2].Y = y1; v[i + 2].Anchor = a; v[i + 2].Color = color; v[i + 2].U = u1; v[i + 2].V = vb; v[i + 2].Page = page;
            v[i + 3].X = x0; v[i + 3].Y = y1; v[i + 3].Anchor = a; v[i + 3].Color = color; v[i + 3].U = u0; v[i + 3].V = vb; v[i + 3].Page = page;
            VertexCount = i + 4;
        }

        /// <summary>
        /// A quad with free corners (clockwise from the top-left of the texture: p0 ↔ (u0, vTop), p1 ↔ (u1, vTop),
        /// p2 ↔ (u1, vBottom), p3 ↔ (u0, vBottom)) — rotated renderer quads (DrawQuad 0xa036f0 with an angle) and
        /// triangles (p3 = p2). Not clipped.
        /// </summary>
        public void QuadCorners(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector4 uv, float page, Color32 color) {
            Ensure(4);
            RoClassicUiVertex[] v = Vertices;
            int i = VertexCount;
            float a = Anchor;
            if (FullColor) page = -page - 1f;
            v[i].X = p0.x; v[i].Y = p0.y; v[i].Anchor = a; v[i].Color = color; v[i].U = uv.x; v[i].V = uv.y; v[i].Page = page;
            v[i + 1].X = p1.x; v[i + 1].Y = p1.y; v[i + 1].Anchor = a; v[i + 1].Color = color; v[i + 1].U = uv.z; v[i + 1].V = uv.y; v[i + 1].Page = page;
            v[i + 2].X = p2.x; v[i + 2].Y = p2.y; v[i + 2].Anchor = a; v[i + 2].Color = color; v[i + 2].U = uv.z; v[i + 2].V = uv.w; v[i + 2].Page = page;
            v[i + 3].X = p3.x; v[i + 3].Y = p3.y; v[i + 3].Anchor = a; v[i + 3].Color = color; v[i + 3].U = uv.x; v[i + 3].V = uv.w; v[i + 3].Page = page;
            VertexCount = i + 4;
        }

        /// <summary>
        /// A D3D triangle strip of four pre-transformed vertices (triangles v0 v1 v2 and v1 v3 v2) with per-vertex texture
        /// coordinates in 0..1 of <paramref name="image"/> (0, 0 = its top-left): the client's free quads (0x875710, 0x52bbf0)
        /// whose corners are not a rectangle's. Not clipped.
        /// </summary>
        public void Strip(Vector2 v0, Vector2 t0, Vector2 v1, Vector2 t1, Vector2 v2, Vector2 t2, Vector2 v3, Vector2 t3, in RoClassicUiImage image, Color32 color) {
            Ensure(4);
            RoClassicUiVertex[] v = Vertices;
            int i = VertexCount;
            float page = FullColor ? -image.Page - 1f : image.Page;
            Vector4 uv = image.Uv;
            // a quad is drawn as (0, 1, 2) and (0, 2, 3): v1 v3 v2 v0 gives the strip's two triangles
            StripVertex(ref v[i], v1, t1, uv, page, color);
            StripVertex(ref v[i + 1], v3, t3, uv, page, color);
            StripVertex(ref v[i + 2], v2, t2, uv, page, color);
            StripVertex(ref v[i + 3], v0, t0, uv, page, color);
            VertexCount = i + 4;
        }

        private void StripVertex(ref RoClassicUiVertex vertex, Vector2 p, Vector2 t, Vector4 uv, float page, Color32 color) {
            vertex.X = p.x;
            vertex.Y = p.y;
            vertex.Anchor = Anchor;
            vertex.Color = color;
            vertex.U = uv.x + (uv.z - uv.x) * t.x;
            vertex.V = uv.y + (uv.w - uv.y) * t.y;
            vertex.Page = page;
        }

        /// <summary>The rectangle (x, y, w, h) turned <paramref name="clockwiseDegrees"/> about its centre (screen y down).</summary>
        public void RotatedQuad(float x, float y, float w, float h, float clockwiseDegrees, Vector4 uv, float page, Color32 color) {
            float cx = x + w * 0.5f, cy = y + h * 0.5f;
            float r = clockwiseDegrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            Vector2 Turn(float dx, float dy) => new Vector2(cx + dx * c - dy * s, cy + dx * s + dy * c);
            float hw = w * 0.5f, hh = h * 0.5f;
            QuadCorners(Turn(-hw, -hh), Turn(hw, -hh), Turn(hw, hh), Turn(-hw, hh), uv, page, color);
        }

        /// <summary>Draws text with its top-left pen at (x, y) like GDI TextOut; returns the advance width.</summary>
        public int Text(RoClassicGdiFont font, int x, int y, string text, Color32 color) {
            int pen = x;
            for (int i = 0; i < text.Length; i++) {
                font.GetGlyph(text[i], out RoClassicGdiFont.Glyph g);
                if (!g.Empty) Quad(pen + g.OffsetX, y + g.OffsetY, g.Width, g.Height, g.Uv, g.Page, color);
                pen += g.Advance;
            }

            return pen - x;
        }

        public int Text(RoClassicGdiFont font, int x, int y, char[] buffer, int start, int length, Color32 color) {
            int pen = x;
            for (int i = start; i < start + length; i++) {
                font.GetGlyph(buffer[i], out RoClassicGdiFont.Glyph g);
                if (!g.Empty) Quad(pen + g.OffsetX, y + g.OffsetY, g.Width, g.Height, g.Uv, g.Page, color);
                pen += g.Advance;
            }

            return pen - x;
        }

        private RoClassicUiVertex[] punchScratch = new RoClassicUiVertex[64];

        /// <summary>
        /// Cuts a transparent hole (x0, y0)–(x1, y1) into the quads [<paramref name="fromVertex"/>,
        /// <paramref name="untilVertex"/>): what a client surface shows where a raw copy (a bitmap blitted without key
        /// skipping, or a key-colour fill) left magenta over earlier drawing — transparent once composed. Each
        /// overlapping quad is replaced in place, keeping the draw order, by up to four pieces around the hole with
        /// interpolated UVs. Returns how many vertices the range grew by (quads after it moved).
        /// </summary>
        public int Punch(int fromVertex, int untilVertex, float x0, float y0, float x1, float y1) {
            int count = 0;
            RoClassicUiVertex[] v = Vertices;
            for (int i = fromVertex; i < untilVertex; i += 4) {
                float qx0 = v[i].X, qy0 = v[i].Y, qx1 = v[i + 2].X, qy1 = v[i + 2].Y;
                if (qx1 <= x0 || qx0 >= x1 || qy1 <= y0 || qy0 >= y1) {
                    EnsureScratch(count + 4);
                    Array.Copy(v, i, punchScratch, count, 4);
                    count += 4;
                    continue;
                }

                float my0 = Math.Max(qy0, y0), my1 = Math.Min(qy1, y1);
                if (y0 > qy0) count = Piece(i, qx0, qy0, qx1, y0, count);
                if (x0 > qx0) count = Piece(i, qx0, my0, x0, my1, count);
                if (x1 < qx1) count = Piece(i, x1, my0, qx1, my1, count);
                if (y1 < qy1) count = Piece(i, qx0, y1, qx1, qy1, count);
            }

            int delta = count - (untilVertex - fromVertex);
            int tail = VertexCount - untilVertex;
            if (delta > 0) Ensure(delta);
            v = Vertices;
            Array.Copy(v, untilVertex, v, untilVertex + delta, tail);
            Array.Copy(punchScratch, 0, v, fromVertex, count);
            VertexCount += delta;
            return delta;
        }

        /// <summary>The part (x0, y0)–(x1, y1) of quad <paramref name="q"/>, UVs interpolated, appended to the scratch.</summary>
        private int Piece(int q, float x0, float y0, float x1, float y1, int count) {
            EnsureScratch(count + 4);
            RoClassicUiVertex[] v = Vertices;
            float qx0 = v[q].X, qy0 = v[q].Y, qx1 = v[q + 2].X, qy1 = v[q + 2].Y;
            float u0 = v[q].U, vt = v[q].V, u1 = v[q + 2].U, vb = v[q + 2].V;
            float pu0 = u0 + (u1 - u0) * (x0 - qx0) / (qx1 - qx0), pu1 = u0 + (u1 - u0) * (x1 - qx0) / (qx1 - qx0);
            float pvt = vt + (vb - vt) * (y0 - qy0) / (qy1 - qy0), pvb = vt + (vb - vt) * (y1 - qy0) / (qy1 - qy0);
            RoClassicUiVertex a = v[q];
            a.X = x0; a.Y = y0; a.U = pu0; a.V = pvt; punchScratch[count] = a;
            a.X = x1; a.Y = y0; a.U = pu1; a.V = pvt; punchScratch[count + 1] = a;
            a.X = x1; a.Y = y1; a.U = pu1; a.V = pvb; punchScratch[count + 2] = a;
            a.X = x0; a.Y = y1; a.U = pu0; a.V = pvb; punchScratch[count + 3] = a;
            return count + 4;
        }

        private void EnsureScratch(int need) {
            if (need > punchScratch.Length) Array.Resize(ref punchScratch, Math.Max(need, punchScratch.Length * 2));
        }

        /// <summary>Appends <paramref name="source"/> translated by (dx, dy), keeping its anchors.</summary>
        public void Append(RoClassicUiQuads source, float dx, float dy) {
            int n = source.VertexCount;
            Ensure(n);
            RoClassicUiVertex[] src = source.Vertices;
            RoClassicUiVertex[] dst = Vertices;
            int o = VertexCount;
            for (int i = 0; i < n; i++) {
                RoClassicUiVertex v = src[i];
                v.X += dx;
                v.Y += dy;
                dst[o + i] = v;
            }

            VertexCount = o + n;
        }
    }
}
