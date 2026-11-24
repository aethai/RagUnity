using Ragnarok.Assets;
using Ragnarok.Content;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    [CustomEditor(typeof(RagnarokContentSettings))]
    public sealed class RagnarokContentSettingsEditor : UnityEditor.Editor {
        private static readonly string[] BaselineAssets = {
            "data/prontera.rsw",
            "data/geffen.rsw",
            "data/payon.rsw",
            "data/izlude.rsw",
            "data/prt_in.rsw",
            "data/pay_dun00.rsw"
        };

        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate RO Content")) {
                ValidateContent((RagnarokContentSettings) target);
            }

            if (GUILayout.Button("Select RO Folder / Auto-detect GRF")) {
                if (RagnarokContentSettingsEditorUtility.SelectAndApplyContentFolder((RagnarokContentSettings) target, out string report)) {
                    Debug.Log(report, target);
                } else {
                    Debug.LogWarning(report, target);
                }
            }

            if (GUILayout.Button("Open Content Diagnostics")) {
                RagnarokContentDiagnosticsWindow.Open((RagnarokContentSettings) target);
            }
        }

        private static void ValidateContent(RagnarokContentSettings settings) {
            if (settings == null) {
                return;
            }

            using var assets = new RoAssetDatabase();
            try {
                assets.Initialize(settings.RootPath, settings.GrfFiles);
                assets.LogSummary(settings);

                int missing = 0;
                foreach (string path in BaselineAssets) {
                    if (!assets.Exists(path)) {
                        missing++;
                        Debug.LogWarning($"Missing baseline RO asset: {path}", settings);
                    }
                }

                if (missing == 0) {
                    Debug.Log("RO content validation passed for baseline map assets.", settings);
                } else {
                    Debug.LogWarning($"RO content validation found {missing} missing baseline asset(s).", settings);
                }

                assets.LogMissingSummary(settings);
            } catch (System.Exception ex) {
                Debug.LogException(ex, settings);
            }
        }
    }
}
