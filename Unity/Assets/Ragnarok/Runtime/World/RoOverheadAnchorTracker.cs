using System.Collections.Generic;
using Ragnarok.Entities;
using UnityEngine;

namespace Ragnarok.World {
    /// <summary>
    /// The screen point above an actor for overhead UI (NPC sign boards, chat bubbles): the top centre of the screen
    /// rectangle of the actor's sprite bounds, united over the current action. The current frame's bounds alone made
    /// the anchor bob with the animation; the union holds still while the action plays, yet follows the actor's size (a
    /// tall NPC, a player's headgear). It starts over when the action changes or the camera turns (the billboards' bounds
    /// change with the camera).
    /// </summary>
    public sealed class RoOverheadAnchorTracker {
        private const int MaxEntries = 512;
        private const float CameraTurnResetDegrees = 0.5f;

        private struct Entry {
            public int ActionKey;
            public Quaternion CameraRotation;
            public Vector3 Min;
            public Vector3 Max;
        }

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly List<RoActorSpriteVisual> visuals = new List<RoActorSpriteVisual>();

        public bool TryResolve(UnityEngine.Camera camera, Transform actorRoot, out Vector3 screenAnchor) {
            screenAnchor = default;
            if (camera == null || actorRoot == null) {
                return false;
            }

            Vector3 position = actorRoot.position;
            actorRoot.GetComponentsInChildren(false, visuals);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;
            int actionKey = 0;
            for (int i = 0; i < visuals.Count; i++) {
                RoActorSpriteVisual visual = visuals[i];
                if (!visual.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                min = Vector3.Min(min, bounds.min - position);
                max = Vector3.Max(max, bounds.max - position);
                if (!any) {
                    actionKey = visual.ActionIndex;
                    any = true;
                }
            }

            if (!any) {
                return false;
            }

            Quaternion cameraRotation = camera.transform.rotation;
            int id = actorRoot.GetInstanceID();
            if (entries.TryGetValue(id, out Entry entry)
                && entry.ActionKey == actionKey
                && Quaternion.Angle(entry.CameraRotation, cameraRotation) <= CameraTurnResetDegrees) {
                min = Vector3.Min(min, entry.Min);
                max = Vector3.Max(max, entry.Max);
                cameraRotation = entry.CameraRotation;
            } else if (entries.Count >= MaxEntries) {
                entries.Clear();
            }

            entries[id] = new Entry { ActionKey = actionKey, CameraRotation = cameraRotation, Min = min, Max = max };

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float z = 0f;
            bool projected = false;
            for (int corner = 0; corner < 8; corner++) {
                Vector3 point = position + new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 screen = camera.WorldToScreenPoint(point);
                if (screen.z <= 0f) {
                    continue;
                }

                projected = true;
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                maxY = Mathf.Max(maxY, screen.y);
                z = screen.z;
            }

            if (!projected) {
                return false;
            }

            screenAnchor = new Vector3((minX + maxX) * 0.5f, maxY, z);
            return true;
        }
    }
}
