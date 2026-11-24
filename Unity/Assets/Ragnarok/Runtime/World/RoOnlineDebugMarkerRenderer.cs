using System.Collections.Generic;
using Ragnarok.Maps;
using Ragnarok.Network;
using UnityEngine;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace Ragnarok.World {
    public sealed class RoOnlineDebugMarkerRenderer : MonoBehaviour {
        private const float DefaultMarkerLift = 0.08f;
        private const float DefaultMarkerHeight = 1.2f;
        private const float DependencyLookupRetrySeconds = 0.5f;
        private static readonly ProfilerMarker SyncNowMarker = new ProfilerMarker("DebugMarkerRenderer.SyncNow");

        [SerializeField] private RoOnlineWorldController worldController;
        [SerializeField] private RoGridSelector gridSelector;
        [SerializeField] private bool includeSelf;
        [SerializeField] private float markerLift = DefaultMarkerLift;
        [SerializeField] private float markerHeight = DefaultMarkerHeight;

        private readonly Dictionary<uint, GameObject> markersByServerId = new Dictionary<uint, GameObject>();
        private readonly List<RoOnlineActorSnapshot> markerSnapshotBuffer = new List<RoOnlineActorSnapshot>(256);
        private readonly HashSet<uint> markerSeenServerIds = new HashSet<uint>();
        private readonly List<uint> markerRemoveServerIds = new List<uint>(64);
        private Transform markerRoot;
        private Material selfMaterial;
        private Material playerMaterial;
        private Material npcMaterial;
        private Material monsterMaterial;
        private Material itemMaterial;
        private Material unknownMaterial;
        private float nextWorldControllerLookupTime;
        private float nextGridSelectorLookupTime;

        public int MarkerCount => markersByServerId.Count;

        public bool IncludeSelf {
            get => includeSelf;
            set {
                if (includeSelf == value) {
                    return;
                }

                includeSelf = value;
                SyncNow();
            }
        }

        public void SetWorldController(RoOnlineWorldController controller) {
            if (worldController == controller) {
                return;
            }

            worldController = controller;
            SyncNow();
        }

        public void SetGridSelector(RoGridSelector selector) {
            if (gridSelector == selector) {
                return;
            }

            gridSelector = selector;
            SyncNow();
        }

        public void Configure(RoOnlineWorldController controller, RoGridSelector selector, bool includeSelfActors) {
            worldController = controller;
            gridSelector = selector;
            includeSelf = includeSelfActors;
        }

        public void SyncNow() {
            using (SyncNowMarker.Auto()) {
                ResolveWorldController();

                if (worldController == null) {
                    ClearMarkers();
                    return;
                }

                ResolveGridSelector();

                worldController.SnapshotActors(markerSnapshotBuffer);
                markerSeenServerIds.Clear();
                for (int i = 0; i < markerSnapshotBuffer.Count; i++) {
                    RoOnlineActorSnapshot actor = markerSnapshotBuffer[i];
                    if (actor.IsSelf && !includeSelf) {
                        continue;
                    }

                    markerSeenServerIds.Add(actor.ServerId);
                    GameObject marker = GetOrCreateMarker(actor);
                    UpdateMarker(marker, actor);
                }

                RemoveUnseenMarkers(markerSeenServerIds);
            }
        }

        public void ClearMarkers() {
            foreach (GameObject marker in markersByServerId.Values) {
                DestroyRuntimeObject(marker);
            }

            markersByServerId.Clear();
            if (markerRoot != null) {
                DestroyRuntimeObject(markerRoot.gameObject);
                markerRoot = null;
            }
        }

        private void LateUpdate() {
            if (markersByServerId.Count == 0) {
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) {
                return;
            }

            foreach (GameObject marker in markersByServerId.Values) {
                TextMesh label = marker.GetComponentInChildren<TextMesh>();
                if (label != null) {
                    label.transform.rotation = camera.transform.rotation;
                }
            }
        }

        private void OnDestroy() {
            ClearMarkers();
            DestroyMaterial(selfMaterial);
            DestroyMaterial(playerMaterial);
            DestroyMaterial(npcMaterial);
            DestroyMaterial(monsterMaterial);
            DestroyMaterial(itemMaterial);
            DestroyMaterial(unknownMaterial);
        }

        private GameObject GetOrCreateMarker(RoOnlineActorSnapshot actor) {
            if (markersByServerId.TryGetValue(actor.ServerId, out GameObject marker) && marker != null) {
                return marker;
            }

            EnsureMarkerRoot();
            marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = BuildMarkerName(actor);
            marker.transform.SetParent(markerRoot, true);
            marker.transform.localScale = new Vector3(0.55f, Mathf.Max(0.1f, markerHeight), 0.55f);

            Collider markerCollider = marker.FindComponent<Collider>();
            if (markerCollider != null) {
                DestroyRuntimeObject(markerCollider);
            }

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(marker.transform, false);
            labelObject.transform.localPosition = Vector3.up * 0.85f;
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.18f;
            label.fontSize = 32;
            label.color = Color.white;

            markersByServerId[actor.ServerId] = marker;
            return marker;
        }

        private void UpdateMarker(GameObject marker, RoOnlineActorSnapshot actor) {
            marker.name = BuildMarkerName(actor);
            marker.transform.position = ResolveActorWorldPosition(actor);

            Renderer renderer = marker.FindComponent<Renderer>();
            if (renderer != null) {
                renderer.sharedMaterial = GetMaterial(actor);
            }

            TextMesh label = marker.GetComponentInChildren<TextMesh>();
            if (label != null) {
                label.text = BuildMarkerLabel(actor);
            }
        }

        private Vector3 ResolveActorWorldPosition(RoOnlineActorSnapshot actor) {
            ResolveGridSelector();

            if (gridSelector != null && IsGridCellAvailable(actor.CellX, actor.CellY)) {
                Vector3 position = gridSelector.GetCellWorldCenter(new Vector2Int(actor.CellX, actor.CellY), markerLift);
                return actor.ObjectType == RoMapActorObjectType.Item
                    ? position + new Vector3(actor.ItemCellOffsetX, 0f, actor.ItemCellOffsetY)
                    : position;
            }

            Vector3 fallback = new Vector3(actor.CellX + 0.5f, markerLift, actor.CellY + 0.5f);
            return actor.ObjectType == RoMapActorObjectType.Item
                ? fallback + new Vector3(actor.ItemCellOffsetX, 0f, actor.ItemCellOffsetY)
                : fallback;
        }

        private RoOnlineWorldController ResolveWorldController() {
            if (worldController != null) {
                return worldController;
            }

            worldController = GetComponentInParent<RoOnlineWorldController>();
            if (worldController != null) {
                nextWorldControllerLookupTime = 0f;
                return worldController;
            }

            float now = Time.unscaledTime;
            if (now < nextWorldControllerLookupTime) {
                return null;
            }

            worldController = FindFirstObjectByType<RoOnlineWorldController>();
            nextWorldControllerLookupTime = worldController != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return worldController;
        }

        private RoGridSelector ResolveGridSelector() {
            if (gridSelector != null) {
                return gridSelector;
            }

            if (worldController != null) {
                gridSelector = worldController.GridSelector;
                if (gridSelector != null) {
                    nextGridSelectorLookupTime = 0f;
                    return gridSelector;
                }
            }

            float now = Time.unscaledTime;
            if (now < nextGridSelectorLookupTime) {
                return null;
            }

            gridSelector = FindFirstObjectByType<RoGridSelector>();
            nextGridSelectorLookupTime = gridSelector != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return gridSelector;
        }

        private bool IsGridCellAvailable(int x, int y) {
            if (gridSelector == null || gridSelector.Altitude == null) {
                return false;
            }

            return x >= 0 &&
                y >= 0 &&
                x < gridSelector.Altitude.width &&
                y < gridSelector.Altitude.height;
        }

        private void RemoveUnseenMarkers(HashSet<uint> seen) {
            if (markersByServerId.Count == 0) {
                return;
            }

            markerRemoveServerIds.Clear();
            foreach (uint serverId in markersByServerId.Keys) {
                if (!seen.Contains(serverId)) {
                    markerRemoveServerIds.Add(serverId);
                }
            }

            for (int i = 0; i < markerRemoveServerIds.Count; i++) {
                uint serverId = markerRemoveServerIds[i];
                if (markersByServerId.TryGetValue(serverId, out GameObject marker)) {
                    DestroyRuntimeObject(marker);
                }

                markersByServerId.Remove(serverId);
            }
        }

        private void EnsureMarkerRoot() {
            if (markerRoot != null) {
                return;
            }

            var rootObject = new GameObject("RO Online Actor Debug Markers");
            rootObject.transform.SetParent(transform, false);
            markerRoot = rootObject.transform;
        }

        private Material GetMaterial(RoOnlineActorSnapshot actor) {
            if (actor.IsSelf) {
                return selfMaterial ??= CreateMaterial("RO Online Self Debug Material", new Color(0.2f, 0.75f, 1f, 1f));
            }

            return actor.ObjectType switch {
                RoMapActorObjectType.Player => playerMaterial ??= CreateMaterial("RO Online Player Debug Material", new Color(0.25f, 0.9f, 0.45f, 1f)),
                RoMapActorObjectType.Npc or RoMapActorObjectType.Npc2 => npcMaterial ??= CreateMaterial("RO Online NPC Debug Material", new Color(1f, 0.9f, 0.25f, 1f)),
                RoMapActorObjectType.Monster => monsterMaterial ??= CreateMaterial("RO Online Monster Debug Material", new Color(1f, 0.25f, 0.25f, 1f)),
                RoMapActorObjectType.Item => itemMaterial ??= CreateMaterial("RO Online Item Debug Material", new Color(0.9f, 0.55f, 1f, 1f)),
                _ => unknownMaterial ??= CreateMaterial("RO Online Unknown Debug Material", new Color(0.75f, 0.75f, 0.75f, 1f))
            };
        }

        private static Material CreateMaterial(string name, Color color) {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
            }

            return new Material(shader) {
                name = name,
                color = color
            };
        }

        private static string BuildMarkerName(RoOnlineActorSnapshot actor) {
            string name = string.IsNullOrWhiteSpace(actor.Name) ? actor.ObjectType.ToString() : actor.Name;
            return $"RO Online Actor {actor.ServerId} - {name}";
        }

        private static string BuildMarkerLabel(RoOnlineActorSnapshot actor) {
            if (actor.ObjectType == RoMapActorObjectType.Item) {
                string suffix = actor.ItemCount > 1 ? $" x{actor.ItemCount}" : string.Empty;
                return $"Item {actor.ItemId}{suffix}\n{actor.CellX},{actor.CellY} sub {actor.ItemSubX},{actor.ItemSubY}";
            }

            string name = string.IsNullOrWhiteSpace(actor.Name) ? actor.ServerId.ToString() : actor.Name;
            return $"{name}\n{actor.ObjectType} {actor.CellX},{actor.CellY}";
        }

        private static void DestroyMaterial(Material material) {
            if (material != null) {
                DestroyRuntimeObject(material);
            }
        }

        private static void DestroyRuntimeObject(Object obj) {
            if (obj == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(obj);
            } else {
                DestroyImmediate(obj);
            }
        }
    }
}
