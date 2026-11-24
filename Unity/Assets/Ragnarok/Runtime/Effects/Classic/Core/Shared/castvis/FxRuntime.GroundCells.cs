// Synced from reference/port/src/RoClassicFx/Shared/castvis/FxRuntime.GroundCells.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Ground-cell quads of the client (C3dGround helpers used by CGroundScope, the mouse-cursor grid, CGraffiSkill
    /// and the Custom /range overlay) and the per-skill ground-scope size table. The 3D ground object is the
    /// client's C3dGround at world +0x28: +0x4 attribute (GAT) object, +0x8/+0xc GND width/height (in GND cells,
    /// half the GAT resolution), +0x10 GND cell size ("zoom", 10). FxWorld creates a shim for it
    /// (<see cref="FxWorld.SetGroundGeometry"/>).
    /// </summary>
    public sealed partial class FxRuntime
    {
        public const uint GroundObjectSize = 0x20;

        /// <summary>
        /// FUN_006ec980 on the CSkillActionMgr singleton (FUN_006f09f0): the 4th-job CSkillAction registry's
        /// ground-scope size, reached by FUN_00d38400 for every skill its switch does not list. The registry is
        /// empty for pre-4th-job skills (0). Hosts that implement 4th-job skill actions can supply it.
        /// </summary>
        public Func<int, int> SkillActionGroundScopeSizeImpl = skid => 0;

        /// <summary>
        /// FUN_00d38400 — ground-scope size parameter for a skill (the scope draws this + 2 cells per side;
        /// −2 → no scope). Explicit switch values recovered from the jump tables @0x00d38580.. (68 skills);
        /// everything else asks <see cref="SkillActionGroundScopeSizeImpl"/>.
        /// </summary>
        public int GroundScopeSize_d38400(int skid)
        {
            switch (skid)
            {
                case 0x2e0: case 0x98f: case 0x991:
                    return -2;
                case 0xb: case 0x19: case 0x2f: case 0x45: case 0x50: case 0x56: case 0x58: case 0x6e:
                case 0x81: case 0x82: case 0x89: case 0x8c: case 0x17d: case 0x1e3: case 0x8d5:
                    return 3;
                case 0x11: case 0x15: case 0x46: case 0x5b: case 0x5c: case 0x1e4: case 0x302: case 0x7d5:
                case 0x7f0: case 0x7fc:
                    return 5;
                case 0x4f: case 0x1de: case 0x2dd: case 0x2e3: case 0x8bd: case 0x992: case 0x994: case 0x995:
                case 0x996: case 0x138e: case 0x1395: case 0x13a4: case 0x13b2: case 0x146b: case 0x146d: case 0x146e:
                case 0x149f:
                    return 7;
                case 0x53: case 0x59: case 0x2d3: case 0x2db: case 0x301: case 0x7d8: case 0x8d4: case 0x98e:
                case 0x138c: case 0x1462: case 0x1466: case 0x1493: case 0x14c7: case 0x14f9: case 0x14fa: case 0x14fb:
                case 0x14fc: case 0x14fd:
                    return 9;
                case 0x55: case 0x1461:
                    return 11;
                case 0x2c4: case 0x8a5:
                    return 18;
                case 0x149e:
                    return 19;
                default:
                    return SkillActionGroundScopeSizeImpl(skid);
            }
        }

        /// <summary>FUN_006c3250 (thiscall on the GAT attribute object): address of cell (x, y) (0x14 bytes), 0 outside.</summary>
        public uint GatCell_6c3250(uint attr, int x, int y)
        {
            if (x < 0 || x >= M.I32(attr + 0x110)) return 0;
            if (y < 0 || y >= M.I32(attr + 0x114)) return 0;
            return M.U32(attr + 0x11c) + (uint)((M.I32(attr + 0x110) * y + x) * 5) * 4;
        }

        /// <summary>
        /// FUN_009f1880 (thiscall on C3dGround): the four corners of the GND cell containing GAT cell (x, y) —
        /// x/z from the GND grid ((x&gt;&gt;1) − W/2)·zoom (C division), heights = that GAT cell's four corner heights
        /// (y down) in the order (x0,z0) (x1,z0) (x0,z1) (x1,z1). False (nothing drawn) outside the map.
        /// </summary>
        public bool GroundCellCorners_9f1880(uint ground, int x, int y, Span<float> v)
        {
            if (x < 0) return false;
            int gx = (int)((uint)x >> 1);
            int w = M.I32(ground + 8);
            if (gx >= w) return false;
            if (y < 0) return false;
            int gy = (int)((uint)y >> 1);
            int h = M.I32(ground + 0xc);
            if (gy >= h) return false;
            float zoom = M.F32(ground + 0x10);
            v[1] = 0f;
            v[0] = (float)(gx - w / 2) * zoom;
            v[2] = (float)(gy - h / 2) * zoom;
            zoom = M.F32(ground + 0x10);
            v[4] = 0f;
            v[5] = (float)(gy - h / 2) * zoom;
            v[3] = (float)(gx - w / 2 + 1) * zoom;
            zoom = M.F32(ground + 0x10);
            v[7] = 0f;
            v[8] = (float)(gy - h / 2 + 1) * zoom;
            v[6] = (float)(gx - w / 2) * zoom;
            v[10] = 0f;
            v[11] = (float)(gy - h / 2 + 1) * M.F32(ground + 0x10);
            v[9] = (float)(gx - w / 2 + 1) * M.F32(ground + 0x10);
            uint cell = GatCell_6c3250(M.U32(ground + 4), x, y);
            if (cell == 0) return false;
            v[1] = M.F32(cell);
            v[4] = M.F32(cell + 4);
            v[7] = M.F32(cell + 8);
            v[10] = M.F32(cell + 0xc);
            return true;
        }

        /// <summary>
        /// FUN_009f2510 (thiscall on C3dGround) — one GAT cell as a textured quad on the terrain: the GND cell
        /// from <see cref="GroundCellCorners_9f1880"/> halved towards the GAT cell's side (x/y parity), the GAT
        /// corner heights, projected (0x53b370) and pulled 2^-15 towards the camera in z; UV = texture
        /// image/surface ratio × <paramref name="uv"/> (4 pairs, vertex order as above); every vertex gets
        /// <paramref name="color"/>; SRCALPHA/INVSRCALPHA; AddQuad(quad, 1) (the depth-sorted alpha list, drawn with
        /// CLAMP addressing).
        /// </summary>
        public void RenderGroundCell_9f2510(uint ground, uint matrix, int x, int y, uint color, uint tex, ReadOnlySpan<float> uv)
        {
            Span<float> v = stackalloc float[12];
            v.Clear();
            if (!GroundCellCorners_9f1880(ground, x, y, v)) return;
            bool oddX = (x & 1) != 0, oddY = (y & 1) != 0;
            for (int k = 0; k < 4; k++)
            {
                float vx = v[k * 3];
                if (oddX)
                {
                    float x1 = v[3];
                    if (x1 > vx) v[k * 3] = (x1 - vx) * 0.5f + vx;
                }
                else
                {
                    float x0 = v[0];
                    if (vx > x0) v[k * 3] = vx - (vx - x0) * 0.5f;
                }
                float vz = v[k * 3 + 2];
                if (oddY)
                {
                    float z1 = v[8];
                    if (z1 > vz) v[k * 3 + 2] = (z1 - vz) * 0.5f + vz;
                }
                else
                {
                    float z0 = v[2];
                    if (vz > z0) v[k * 3 + 2] = vz - (vz - z0) * 0.5f;
                }
            }

            // GetTexture never fails in the client (it falls back to a default texture); the port skips the cell
            if (tex == 0) return;
            var r = Renderer;
            uint quad = r.AcquireQuad();                                  // FUN_00521e00
            for (int k = 0; k < 4; k++)
            {
                uint vtx = M.U32(quad) + (uint)k * 0x20;
                r.ProjectVertex(M, matrix, v[k * 3], v[k * 3 + 1], v[k * 3 + 2], out float sx, out float sy, out float sz, out float rhw);
                M.F32(vtx, sx); M.F32(vtx + 4, sy); M.F32(vtx + 8, sz); M.F32(vtx + 0xc, rhw);
                M.F32(vtx + 8, M.F32(vtx + 8) - 3.0517578e-05f);
            }
            // (float)(double)(uint): cvtdq2pd + the 2^32 fix-up at 0x00f481c0
            float su = (float)(double)M.U32(tex + 0x14) / (float)(double)M.U32(tex + 0xc);
            float sv = (float)(double)M.U32(tex + 0x18) / (float)(double)M.U32(tex + 0x10);
            uint verts = M.U32(quad);
            M.F32(verts + 0x18, su * uv[0]);
            M.F32(verts + 0x1c, sv * uv[1]);
            M.F32(verts + 0x38, su * uv[2]);
            M.F32(verts + 0x3c, sv * uv[3]);
            M.F32(verts + 0x58, su * uv[4]);
            M.F32(verts + 0x5c, sv * uv[5]);
            M.F32(verts + 0x78, su * uv[6]);
            M.F32(verts + 0x7c, sv * uv[7]);
            M.I32(quad + 0x18, 5);                                       // D3DBLEND_SRCALPHA
            M.I32(quad + 0x1c, 6);                                       // D3DBLEND_INVSRCALPHA
            M.U32(quad + 8, tex);
            for (uint k = 0; k < 4; k++) M.U32(M.U32(quad) + 0x10 + k * 0x20, color);
            r.AddQuad(quad, 1);
        }

        /// <summary>UV array at 0x0116a898 used by the grid and /range cells: (0,0) (1,0) (0,1) (1,1).</summary>
        public static readonly float[] FullCellUv = { 0f, 0f, 1f, 0f, 0f, 1f, 1f, 1f };

        /// <summary>
        /// C3dGround::vf14 (0x009f24c0) — the mouse-cursor cell highlight: GetTexture("grid.tga") on one GAT cell,
        /// full UVs, vertex colour <paramref name="color"/>. Called by CView::OnRender (@0x00a09cdb) every frame
        /// with the globals DAT_01182300/04 (cell) and DAT_0118230c (colour) unless DAT_01182308 (hidden) is set.
        /// </summary>
        public void RenderCursorGrid_9f24c0(uint ground, uint matrix, int x, int y, uint color)
            => RenderGroundCell_9f2510(ground, matrix, x, y, color, GetTexture("grid.tga"), FullCellUv);

        /// <summary>
        /// Cursor-cell colour and visibility as set by the mouse handler (@0x00c0371e), evaluated only while the
        /// grid option DAT_01552d98 is on (hotkey action 0x87 toggles it; off at start): outside the map, cell type
        /// 1 → red 0xfeff0000 hidden; type 5 → 0xfeffff00 hidden; other types → 0x6600ffaa shown (unless the
        /// handler's "over something" flag hides it). Returns the colour; <paramref name="hidden"/> = DAT_01182308.
        /// </summary>
        public uint CursorGridColor(int x, int y, out bool hidden)
        {
            uint attr = M.U32(M.U32(Mode + 0xcc) + 0x30);
            uint cell = x < 0 || attr == 0 ? 0u : GatCell_6c3250(attr, x, y);
            if (cell == 0 || M.I32(cell + 0x10) == 1) { hidden = true; return 0xfeff0000u; }
            if (M.I32(cell + 0x10) == 5) { hidden = true; return 0xfeffff00u; }
            hidden = false;
            return 0x6600ffaau;
        }

        /// <summary>
        /// FUN_0233a720 (Custom client diff, section .xdiff) — like <see cref="RenderCursorGrid_9f24c0"/> with the
        /// texture "range_full.tga".
        /// </summary>
        public void RenderRangeCell_233a720(uint ground, uint matrix, int x, int y, uint color)
            => RenderGroundCell_9f2510(ground, matrix, x, y, color, GetTexture("range_full.tga"), FullCellUv);

        /// <summary>
        /// FUN_00bf84b0 (CGameMode) — RO world position → GAT cell: floor(x / cellSize + width/2),
        /// floor(z / cellSize + height/2) (float math, floor in double, _ftol2).
        /// </summary>
        public void WorldToCell_bf84b0(float x, float z, out int cellX, out int cellY)
        {
            uint attr = M.U32(M.U32(Mode + 0xcc) + 0x30);
            float size = (float)M.I32(attr + 0x118);
            float fx = (float)(M.I32(attr + 0x110) / 2) + x / size;
            cellX = (int)(float)Math.Floor((double)fx);
            float fz = (float)(M.I32(attr + 0x114) / 2) + z / size;
            cellY = (int)(float)Math.Floor((double)fz);
        }

        /// <summary>
        /// The Custom "/range" overlay (.xdiff 0x0233a9e0, run from the patched entry of the mouse/input
        /// handler FUN_00c039b0): while a skill is armed (mode +0x410 != 0) and the /range option is on, every
        /// walkable GAT cell (type not 1 or 5) with dx² + dy² ≤ r² + 1, r = clamp(range, 0, 30) + 1, around the
        /// local player's cell gets a "range_full.tga" quad of colour 0x5500ff80. <paramref name="range"/> is the
        /// armed skill's attack range from ZC_SKILLINFO (mode +0x418). Returns false when nothing was drawn
        /// because a precondition failed.
        /// </summary>
        public bool RenderSkillRange_233a9e0(bool rangeOption, int skillUseState, int range)
        {
            if (!rangeOption || skillUseState == 0) return false;
            uint world = M.U32(Mode + 0xcc);
            if (world == 0) return false;
            uint player = M.U32(world + 0x2c);
            if (player == 0) return false;
            int r = range;
            if (r < 0) r = 0;
            if (r > 0x1e) r = 0x1e;
            r++;
            int r2 = r * r + 1;
            if (M.U32(world + 0x30) == 0) return false;   // the client would fault in FUN_00bf84b0
            WorldToCell_bf84b0(M.F32(player + 0x10), M.F32(player + 0x18), out int px, out int py);   // FUN_00bf8790
            uint ground = M.U32(world + 0x28);
            if (ground == 0) return false;
            uint view = M.U32(Mode + 0xd0);
            if (view == 0) return false;
            uint matrix = view + 0x98;
            uint attr = M.U32(world + 0x30);
            if (attr == 0) return false;
            int w = M.I32(attr + 0x110);
            if (w <= 0) return false;
            int h = M.I32(attr + 0x114);
            if (h <= 0) return false;
            uint cells = M.U32(attr + 0x11c);
            if (cells == 0) return false;
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = px + dx;
                    if (x < 0 || x >= w) continue;
                    int y = py + dy;
                    if (y < 0 || y >= h) continue;
                    uint cell = cells + (uint)((w * y + x) * 0x14);
                    int type = M.I32(cell + 0x10);
                    if (type == 1 || type == 5) continue;
                    RenderRangeCell_233a720(ground, matrix, x, y, 0x5500ff80u);
                }
            }
            return true;
        }
    }
}
