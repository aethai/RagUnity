using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ragnarok.Editor {
    /// <summary>
    /// Puts the actor sprites' depth prepass (RO Sprite Enhanced pass "SpriteDepthPrepass", LightMode RoSpriteDepthPrepass)
    /// into its own renderer step drawn for every sprite before the transparents, like the original client's sprite list
    /// 0x180 (pass 1, alpha-tested depth writes, for all sprites; then pass 2) — so the forward passes follow each other and
    /// the SRP batcher no longer breaks between the two passes of every layer renderer. Editor command
    /// <c>sprite-depth-feature</c>; idempotent.
    /// </summary>
    internal static class RoSpriteDepthPrepassSetup {
        internal const string FeatureName = "RO Sprite Depth Prepass";
        internal const string LightMode = "RoSpriteDepthPrepass";
        static readonly string[] RendererAssets = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

        internal static string Apply() {
            var report = new System.Text.StringBuilder();
            foreach (string path in RendererAssets) {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) {
                    report.Append(path).Append(": missing; ");
                    continue;
                }

                if (data.rendererFeatures.Exists(f => f != null && f.name == FeatureName)) {
                    report.Append(path).Append(": already there; ");
                    continue;
                }

                var feature = ScriptableObject.CreateInstance<RenderObjects>();
                feature.name = FeatureName;
                feature.settings.passTag = FeatureName;
                feature.settings.Event = RenderPassEvent.BeforeRenderingTransparents;
                feature.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
                feature.settings.filterSettings.LayerMask = ~0;
                feature.settings.filterSettings.PassNames = new[] { LightMode };
                AssetDatabase.AddObjectToAsset(feature, data);

                var serialized = new SerializedObject(data);
                SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
                SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                int index = features.arraySize;
                features.arraySize++;
                features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                report.Append(path).Append(": added; ");
            }

            AssetDatabase.SaveAssets();
            return report.ToString();
        }
    }
}
