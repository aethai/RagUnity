using System.Collections.Generic;
using Ragnarok.UI;
using Ragnarok.World;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Maps {
    /// <summary>
    /// Custom's /range (its patch at 0x233a9e0, called from the frame at 0xc039bf; [0x0233a65c]): while a skill waits for
    /// its target (CGameMode+0x410) every cell around the player's within r = clamp(range, 0, 30) + 1 (dx² + dy² ≤ r² + 1;
    /// range = CGameMode+0x418, the skill's range) that is not of GAT type 1 or 5 (the walkable ones) is drawn as a ground
    /// tile in ARGB 0x5500FF80 (0x233a720). One mesh, rebuilt only when the player's cell or the range changes.
    /// </summary>
    internal sealed class RoSkillRangeOverlay {
        public static readonly Color Tint = new Color(0f, 1f, 128f / 255f, 0x55 / 255f);
        private const float Lift = 0.05f;
        private const int MaxRange = 30;

        private readonly GameObject host;
        private readonly Mesh mesh;
        private readonly MeshRenderer meshRenderer;
        private readonly List<Vector3> vertices = new List<Vector3>(1024);
        private readonly List<int> triangles = new List<int>(1536);
        private RoOnlineWorldController world;
        private GAT builtFor;
        private int builtX = int.MinValue, builtY = int.MinValue, builtRadius = -1;

        public RoSkillRangeOverlay(Transform parent, Material material) {
            host = new GameObject("RO Skill Range");
            host.transform.SetParent(parent, false);
            mesh = new Mesh { name = "RO Skill Range", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            host.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = host.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            RoOwnedRuntimeAssets.Own(host, mesh);
            RoOwnedRuntimeAssets.Own(host, material);
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        /// <summary>Per frame from the grid selector: shown, moved or hidden as /range and the targeting say.</summary>
        public void Update(GAT gat) {
            RoSkillTargetSelection targeting = RoSkillTargetSelection.Current;
            if (gat == null || gat.cells == null || targeting == null || !targeting.HasActiveSelection || !RoClassicCommandFlags.SkillRange ||
                !TryGetPlayerCell(out int px, out int py)) {
                if (meshRenderer.enabled) meshRenderer.enabled = false;
                return;
            }

            int radius = Mathf.Clamp(targeting.AttackRange, 0, MaxRange) + 1;
            if (px != builtX || py != builtY || radius != builtRadius || !ReferenceEquals(gat, builtFor)) Build(gat, px, py, radius);
            if (!meshRenderer.enabled) meshRenderer.enabled = true;
        }

        private bool TryGetPlayerCell(out int x, out int y) {
            x = y = 0;
            if (world == null) world = Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (world == null || world.OwnActorId == 0 || !world.State.TryGetActor(world.OwnActorId, out RoOnlineActorSnapshot own)) return false;
            x = own.CellX;
            y = own.CellY;
            return true;
        }

        private void Build(GAT gat, int px, int py, int radius) {
            builtFor = gat;
            builtX = px;
            builtY = py;
            builtRadius = radius;
            vertices.Clear();
            triangles.Clear();
            int limit = radius * radius + 1;
            for (int dx = -radius; dx <= radius; dx++) {
                for (int dy = -radius; dy <= radius; dy++) {
                    int x = px + dx, y = py + dy;
                    if (dx * dx + dy * dy > limit || x < 0 || y < 0 || x >= gat.width || y >= gat.height) continue;
                    if (!RoGridPathfinder.IsWalkable(gat, x, y)) continue;   // GAT types 1 and 5 stay bare
                    Vector4 h = gat.cells[x + y * gat.width].Heights;
                    int at = vertices.Count;
                    vertices.Add(RoMapCoordinateSystem.GatVertex(x, y + 1, h[0]) + Vector3.up * Lift);
                    vertices.Add(RoMapCoordinateSystem.GatVertex(x + 1, y + 1, h[1]) + Vector3.up * Lift);
                    vertices.Add(RoMapCoordinateSystem.GatVertex(x, y, h[2]) + Vector3.up * Lift);
                    vertices.Add(RoMapCoordinateSystem.GatVertex(x + 1, y, h[3]) + Vector3.up * Lift);
                    triangles.Add(at);
                    triangles.Add(at + 1);
                    triangles.Add(at + 2);
                    triangles.Add(at + 1);
                    triangles.Add(at + 3);
                    triangles.Add(at + 2);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
    }
}
