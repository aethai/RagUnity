using Ragnarok.Assets;
using Ragnarok.Diagnostics;
using Ragnarok.Input;
using Ragnarok.UI;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.Maps {
    public sealed class RoGridSelector : MonoBehaviour {
        private const float CellSize = 1f;
        private const float SelectorLift = 0.06f;
        private const int SelectorRenderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent - 10;
        private const int SelectorSortingOrder = -1000;

        [SerializeField] private UnityEngine.Camera inputCamera;
        [SerializeField] private Collider groundCollider;
        [SerializeField] private GAT altitude;
        [SerializeField] private RoAssetDatabase assets;
        [Header("Input Actions")]
        [SerializeField] private InputActionReference pointerPositionAction;

        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private readonly Vector3[] selectorVertices = new Vector3[4];
        private readonly Vector2[] selectorUvs = {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };
        private readonly int[] selectorTriangles = { 0, 1, 2, 1, 3, 2 };
        private bool hasCell;
        private Vector2Int currentCell;
        private Vector3 currentDestination;
        private bool ownsPointerPositionActionEnable;
        private RoSkillRangeOverlay skillRange;   // Custom's /range cells
        private UnityEngine.Camera cachedFallbackCamera;
        private int cachedFallbackCameraFrame = -1;

        public GAT Altitude => altitude;
        public bool HasCell => hasCell;
        public Vector2Int CurrentCell => currentCell;
        public Vector3 CurrentDestination => transform.TransformPoint(currentDestination);

        public void Initialize(UnityEngine.Camera camera, Collider ground, GAT gat, RoAssetDatabase assetDatabase) {
            inputCamera = camera;
            cachedFallbackCamera = null;
            cachedFallbackCameraFrame = -1;
            groundCollider = ground;
            altitude = gat;
            assets = assetDatabase;
            RoGridPathfinder.PrepareSearchWorkspace(gat);
            BuildRenderer();
            EnableConfiguredActions();
        }

        public void SetPointerPositionAction(InputActionReference pointerPosition) {
            DisableConfiguredActions();
            pointerPositionAction = pointerPosition;
            EnableConfiguredActions();
        }

        private void OnEnable() {
            useGUILayout = false;
            EnableConfiguredActions();
        }

        private void OnDisable() {
            DisableConfiguredActions();
        }

        private void Update() {
            long gridSelectorUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                UpdateHover();
                skillRange?.Update(altitude);
            } finally {
                RoRuntimePerfCounters.AddGridSelectorUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(gridSelectorUpdateStartTicks));
            }
        }

        private void OnGUI() {
            long onGuiStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            Event current = Event.current;
            RoRuntimePerfCounters.IncrementOnGuiEvent(current != null ? (int)current.type : -1);
            try {
                if (IsPointerPositionActionActive()) {
                    return;
                }

                RoPointerScreenPosition.ReportGuiMousePosition(current);
            } finally {
                long elapsedTicks = RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(onGuiStartTicks);
                RoRuntimePerfCounters.AddOnGuiTicks(elapsedTicks);
                RoRuntimePerfCounters.AddGridSelectorOnGuiTicks(elapsedTicks);
            }
        }

        private void BuildRenderer() {
            mesh = new Mesh {
                name = "RO Grid Selector"
            };
            mesh.MarkDynamic();

            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = CreateMaterial(new Color(50 / 255f, 240 / 255f, 160 / 255f, 0.6f), LoadGridTexture());
            RoOwnedRuntimeAssets.Own(gameObject, mesh);
            RoOwnedRuntimeAssets.Own(gameObject, meshRenderer.sharedMaterial);
            if (skillRange == null) skillRange = new RoSkillRangeOverlay(transform, CreateMaterial(RoSkillRangeOverlay.Tint, Texture2D.whiteTexture));
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = SelectorSortingOrder;
            meshRenderer.enabled = false;
        }

        private void UpdateHover() {
            UnityEngine.Camera camera = ResolveInputCamera();
            if (camera == null || altitude == null || !TryReadPointerPosition(out Vector2 pointerPosition)) {
                SetVisible(false);
                return;
            }

            if (RoUiPointerCapture.IsPointerCaptured(pointerPosition)) {
                SetVisible(false);
                return;
            }

            if (meshRenderer == null || meshRenderer.sharedMaterial == null) {
                SetVisible(false);
                return;
            }

            Ray ray = camera.ScreenPointToRay(pointerPosition);
            if (!TryGetPoint(ray, out Vector3 worldPoint)) {
                SetVisible(false);
                return;
            }

            if (!TryGetCellAtWorld(worldPoint, out Vector2Int cell) || !IsCellWalkable(cell)) {
                SetVisible(false);
                return;
            }

            bool cellChanged = !hasCell || currentCell != cell;
            if (cellChanged) {
                currentCell = cell;
                currentDestination = CellToLocalCenter(cell, 2f);
                RenderCell(cell.x, cell.y);
            }

            SetVisible(true);
        }

        private UnityEngine.Camera ResolveInputCamera() {
            if (inputCamera != null) {
                return inputCamera;
            }

            int frame = Time.frameCount;
            if (cachedFallbackCameraFrame != frame || cachedFallbackCamera == null) {
                cachedFallbackCamera = UnityEngine.Camera.main;
                cachedFallbackCameraFrame = frame;
            }

            return cachedFallbackCamera;
        }

        private void EnableConfiguredActions() {
            EnableAction(pointerPositionAction, ref ownsPointerPositionActionEnable);
        }

        private void DisableConfiguredActions() {
            DisableAction(pointerPositionAction, ref ownsPointerPositionActionEnable);
        }

        private static void EnableAction(InputActionReference actionReference, ref bool ownsEnable) {
            InputAction action = actionReference != null ? actionReference.action : null;
            if (action != null && !action.enabled) {
                action.Enable();
                ownsEnable = true;
            }
        }

        private static void DisableAction(InputActionReference actionReference, ref bool ownsEnable) {
            InputAction action = actionReference != null ? actionReference.action : null;
            if (ownsEnable && action != null && action.enabled) {
                action.Disable();
            }

            ownsEnable = false;
        }

        private bool TryReadPointerPosition(out Vector2 pointerPosition) {
            return RoPointerScreenPosition.TryRead(pointerPositionAction, out pointerPosition);
        }

        private bool IsPointerPositionActionActive() {
            InputAction action = pointerPositionAction != null ? pointerPositionAction.action : null;
            return action != null && action.enabled;
        }

        public bool TryGetCellAtWorld(Vector3 worldPoint, out Vector2Int cell) {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            cell = LocalToCell(localPoint);
            return IsCellAvailable(cell);
        }

        public bool IsCellAvailable(Vector2Int cell) {
            return altitude != null &&
                   altitude.cells != null &&
                   cell.x >= 0 &&
                   cell.y >= 0 &&
                   cell.x < altitude.width &&
                   cell.y < altitude.height;
        }

        public bool IsCellWalkable(Vector2Int cell) {
            return RoGridPathfinder.IsWalkable(altitude, cell.x, cell.y);
        }

        public Vector3 GetCellWorldCenter(Vector2Int cell, float yOffset) {
            return transform.TransformPoint(CellToLocalCenter(cell, yOffset));
        }

        public bool TryGetPoint(Ray ray, out Vector3 point) {
            if (!RoMapCoordinateSystem.TryNormalizeRay(ray, out Ray normalizedRay)) {
                point = default;
                return false;
            }

            if (RoMapCoordinateSystem.TryResolveGatWalkSurfaceIntersection(
                    altitude,
                    transform.worldToLocalMatrix,
                    transform.localToWorldMatrix,
                    normalizedRay,
                    out point)) {
                return true;
            }

            if (groundCollider != null && groundCollider.Raycast(normalizedRay, out RaycastHit hit, 2000f)) {
                point = hit.point;
                return true;
            }

            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(normalizedRay, out float distance)) {
                point = normalizedRay.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private Vector2Int LocalToCell(Vector3 local) {
            int x = Mathf.FloorToInt(local.x / CellSize);
            int y = Mathf.FloorToInt(local.z / CellSize);
            return new Vector2Int(x, y);
        }

        private Vector3 CellToLocalCenter(Vector2Int cell, float yOffset) {
            return RoGridPathfinder.CellToLocalCenter(altitude, cell, yOffset);
        }

        private void RenderCell(int x, int y) {
            GAT.Cell cell = GetCell(x, y);
            Vector4 h = cell.Heights;
            selectorVertices[0] = RoMapCoordinateSystem.GatVertex(x, y + 1, h[0]) + Vector3.up * SelectorLift;
            selectorVertices[1] = RoMapCoordinateSystem.GatVertex(x + 1, y + 1, h[1]) + Vector3.up * SelectorLift;
            selectorVertices[2] = RoMapCoordinateSystem.GatVertex(x, y, h[2]) + Vector3.up * SelectorLift;
            selectorVertices[3] = RoMapCoordinateSystem.GatVertex(x + 1, y, h[3]) + Vector3.up * SelectorLift;

            mesh.Clear();
            mesh.SetVertices(selectorVertices);
            mesh.SetUVs(0, selectorUvs);
            mesh.SetTriangles(selectorTriangles, 0);
            mesh.RecalculateBounds();
        }

        private GAT.Cell GetCell(int x, int y) {
            return altitude.cells[x + y * altitude.width];
        }

        private void SetVisible(bool visible) {
            if (hasCell == visible && (meshRenderer == null || meshRenderer.enabled == visible)) {
                return;
            }

            hasCell = visible;
            if (meshRenderer != null) {
                meshRenderer.enabled = visible;
            }
        }

        private Material CreateMaterial(Color tint, Texture texture) {
            Shader shader = ResolveGridShader();
            if (shader == null) {
                Debug.LogWarning("Could not create RO grid selector material because no supported shader is available in this build.");
                return null;
            }

            var material = new Material(shader);
            material.name = "RO Grid Selector Material";
            material.color = tint;
            material.mainTexture = texture;
            if (material.HasProperty("_BaseColor")) {
                material.SetColor("_BaseColor", tint);
            }

            if (material.HasProperty("_Color")) {
                material.SetColor("_Color", tint);
            }

            if (material.HasProperty("_BaseMap")) {
                material.SetTexture("_BaseMap", material.mainTexture);
            }

            if (material.HasProperty("_Cutoff")) {
                material.SetFloat("_Cutoff", 0.01f);
            }

            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", (float) UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_ZTest", (float) UnityEngine.Rendering.CompareFunction.LessEqual);
            if (material.HasProperty("_DepthCorrection")) {
                material.SetFloat("_DepthCorrection", 0f);
            }

            material.renderQueue = SelectorRenderQueue;
            if (material.HasProperty("_Surface")) {
                material.SetFloat("_Surface", 1f);
            }

            return material;
        }

        private static Shader ResolveGridShader() {
            string[] shaderNames = {
                "Ragnarok/RO Cursor Unlit",
                "Universal Render Pipeline/Unlit",
                "Ragnarok/RO Sprite Unlit",
                "Universal Render Pipeline/Lit",
                "Sprites/Default",
                "UI/Default"
            };

            for (int i = 0; i < shaderNames.Length; i++) {
                Shader shader = Shader.Find(shaderNames[i]);
                if (shader != null) {
                    return shader;
                }
            }

            return null;
        }

        private Texture2D LoadGridTexture() {
            const string roBrowserGridTexturePath = "data/texture/grid.tga";
            const string legacyFallbackGridTexturePath = "data/texture/grid.png";
            Texture2D texture = TryLoadGridTexture(roBrowserGridTexturePath);
            if (texture != null) {
                return texture;
            }

            texture = TryLoadGridTexture(legacyFallbackGridTexturePath);
            if (texture != null) {
                return texture;
            }

            Debug.LogWarning("Could not load data/texture/grid.tga. Using fallback grid selector texture.");
            var fallback = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            fallback.name = "RO Grid Selector Fallback";
            fallback.filterMode = FilterMode.Bilinear;
            fallback.wrapMode = TextureWrapMode.Clamp;

            Color32 clear = new Color32(255, 255, 255, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            Color32[] pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = clear;
            }

            for (int y = 0; y < 32; y++) {
                for (int x = 0; x < 32; x++) {
                    bool top = y <= 3 || (y <= 8 && (x <= 3 || x >= 28));
                    bool bottom = y >= 28 || (y >= 23 && (x <= 3 || x >= 28));
                    bool leftCorner = x <= 8 || x <= 3;
                    bool rightCorner = x >= 23 || x >= 28;

                    if ((top || bottom) && (leftCorner || rightCorner)) {
                        pixels[x + y * 32] = white;
                    }
                }
            }

            fallback.SetPixels32(pixels);
            fallback.Apply();
            return fallback;
        }

        private Texture2D TryLoadGridTexture(string path) {
            try {
                Texture2D texture = assets != null ? assets.LoadTexture(path) : null;

                if (texture != null) {
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                    if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                        RoRuntimeDiagnostics.IncrementDebugLogLine();
                        Debug.Log("Loaded RO grid selector texture from " + path);
                    }
                    return texture;
                }
            } catch (System.Exception ex) {
                Debug.LogWarning($"Could not load {path}: {ex.Message}", this);
            }

            return null;
        }
    }
}
