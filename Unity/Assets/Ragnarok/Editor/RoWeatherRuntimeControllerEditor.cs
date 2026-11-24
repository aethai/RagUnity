using Ragnarok.Effects;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    [CustomEditor(typeof(RoWeatherRuntimeController))]
    internal sealed class RoWeatherRuntimeControllerEditor : UnityEditor.Editor {
        private static readonly Color DialBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color DialRingColor = new Color(0.3f, 0.75f, 1f, 1f);
        private static readonly Color DialArrowColor = new Color(0.15f, 0.85f, 1f, 1f);

        public override void OnInspectorGUI() {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.ObjectField(
                    "Script",
                    MonoScript.FromMonoBehaviour((RoWeatherRuntimeController) target),
                    typeof(MonoScript),
                    allowSceneObjects: false);
            }

            EditorGUI.BeginChangeCheck();
            DrawWindSettings(serializedObject.FindProperty("wind"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dayNight"), includeChildren: true);
            DrawDayNightControls();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rain"), includeChildren: true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cloudShadows"), includeChildren: true);
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();
            if (changed) {
                ApplyRuntimeSettings();
            }
        }

        private void DrawDayNightControls() {
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button("Start")) {
                    InvokeDayNightAction(weather => weather.StartDayNightCycle(reset: false));
                }

                if (GUILayout.Button("Reset")) {
                    InvokeDayNightAction(weather => weather.ResetDayNightCycle());
                }

                if (GUILayout.Button("Stop")) {
                    InvokeDayNightAction(weather => weather.StopDayNightCycle());
                }
            }
        }

        private static void DrawWindSettings(SerializedProperty windProperty) {
            if (windProperty == null) {
                return;
            }

            windProperty.isExpanded = EditorGUILayout.Foldout(windProperty.isExpanded, "Wind", toggleOnLabelClick: true);
            if (!windProperty.isExpanded) {
                return;
            }

            using (new EditorGUI.IndentLevelScope()) {
                SerializedProperty direction = windProperty.FindPropertyRelative("direction");
                SerializedProperty strength = windProperty.FindPropertyRelative("strength");
                DrawWindDirection(direction);
                EditorGUILayout.PropertyField(strength, new GUIContent("Strength"));
            }
        }

        private static void DrawWindDirection(SerializedProperty directionProperty) {
            if (directionProperty == null) {
                return;
            }

            EditorGUILayout.LabelField("Direction");
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                Rect dialRect = GUILayoutUtility.GetRect(74f, 74f, GUILayout.Width(74f), GUILayout.Height(74f));
                DrawDirectionDial(dialRect, directionProperty);

                using (new EditorGUILayout.VerticalScope()) {
                    EditorGUILayout.PropertyField(directionProperty, GUIContent.none);
                    if (GUILayout.Button("Normalize", GUILayout.Width(90f))) {
                        Vector2 direction = directionProperty.vector2Value;
                        directionProperty.vector2Value = direction.sqrMagnitude > 0.0001f
                            ? direction.normalized
                            : RoWeatherWindSettings.DefaultDirection;
                    }
                }
            }
        }

        private static void DrawDirectionDial(Rect rect, SerializedProperty directionProperty) {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlId)) {
                case EventType.MouseDown:
                    if (current.button == 0 && rect.Contains(current.mousePosition)) {
                        GUIUtility.hotControl = controlId;
                        SetDirectionFromMouse(rect, directionProperty, current.mousePosition);
                        current.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId) {
                        SetDirectionFromMouse(rect, directionProperty, current.mousePosition);
                        current.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && current.button == 0) {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }

                    break;
                case EventType.Repaint:
                    DrawDirectionDialRepaint(rect, directionProperty.vector2Value);
                    break;
            }
        }

        private static void SetDirectionFromMouse(Rect rect, SerializedProperty directionProperty, Vector2 mousePosition) {
            Vector2 center = rect.center;
            Vector2 delta = mousePosition - center;
            Vector2 direction = new Vector2(delta.x, -delta.y);
            if (direction.sqrMagnitude <= 0.0001f) {
                return;
            }

            directionProperty.vector2Value = direction.normalized;
            GUI.changed = true;
        }

        private static void DrawDirectionDialRepaint(Rect rect, Vector2 direction) {
            if (direction.sqrMagnitude <= 0.0001f) {
                direction = RoWeatherWindSettings.DefaultDirection;
            }

            direction.Normalize();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.44f;
            Vector2 screenDirection = new Vector2(direction.x, -direction.y);
            Vector2 tip = center + screenDirection * (radius * 0.78f);
            Vector2 basePoint = center - screenDirection * (radius * 0.28f);
            Vector2 right = new Vector2(-screenDirection.y, screenDirection.x);

            EditorGUI.DrawRect(rect, DialBackgroundColor);
            Handles.BeginGUI();
            Handles.color = DialRingColor;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.DrawLine(center + Vector2.up * radius, center - Vector2.up * radius);
            Handles.DrawLine(center + Vector2.right * radius, center - Vector2.right * radius);

            Handles.color = DialArrowColor;
            Handles.DrawAAPolyLine(4f, basePoint, tip);
            Handles.DrawAAConvexPolygon(
                tip,
                tip - screenDirection * 12f + right * 6f,
                tip - screenDirection * 12f - right * 6f);
            Handles.EndGUI();
        }

        private void InvokeDayNightAction(System.Action<RoWeatherRuntimeController> action) {
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targets.Length; i++) {
                if (targets[i] is RoWeatherRuntimeController weather) {
                    action(weather);
                    weather.SyncRuntime();
                    EditorUtility.SetDirty(weather);
                }
            }
        }

        private void ApplyRuntimeSettings() {
            for (int i = 0; i < targets.Length; i++) {
                if (targets[i] is RoWeatherRuntimeController weather) {
                    weather.WindSettings.ApplyGlobals();
                    weather.SyncRuntime();
                    EditorUtility.SetDirty(weather);
                }
            }
        }
    }
}
