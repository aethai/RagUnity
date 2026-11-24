using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Ragnarok.Editor {
    /// <summary>
    /// The player's icon: the original client's (game.exe, 32 x 32, scaled 8x without filtering in
    /// Assets/Ragnarok/Branding). Batchmode: -executeMethod Ragnarok.Editor.RoBrandingSettings.ApplyClientIcon.
    /// </summary>
    internal static class RoBrandingSettings {
        private const string IconPath = "Assets/Ragnarok/Branding/RoClientIcon.png";

        [MenuItem("Ragnarok/Branding/Apply Client Icon")]
        public static void ApplyClientIcon() {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) {
                Debug.LogError("[RO] branding: no icon at " + IconPath);
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("[RO] branding: default icon = " + IconPath);
        }
    }
}
