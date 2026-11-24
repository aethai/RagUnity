using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ragnarok.Editor {
    /// <summary>
    /// RO materials are created from GRF data at runtime, so their Shader.Find calls do not
    /// create scene dependencies. Keep the project's runtime shaders in every player build.
    /// </summary>
    internal sealed class RoRuntimeShaderBuildProcessor : IPreprocessBuildWithReport {
        public int callbackOrder => int.MinValue + 50;

        public void OnPreprocessBuild(BuildReport report) {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (settings.Length == 0) {
                throw new BuildFailedException("Cannot include RO runtime shaders: GraphicsSettings could not be loaded.");
            }

            var graphics = new SerializedObject(settings[0]);
            SerializedProperty included = graphics.FindProperty("m_AlwaysIncludedShaders");
            if (included == null) {
                throw new BuildFailedException("Cannot include RO runtime shaders: Always Included Shaders was not found.");
            }

            var shaders = new HashSet<Shader>();
            for (int i = 0; i < included.arraySize; i++) {
                if (included.GetArrayElementAtIndex(i).objectReferenceValue is Shader shader) {
                    shaders.Add(shader);
                }
            }

            string[] paths = Array.ConvertAll(
                AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Ragnarok/Runtime" }),
                AssetDatabase.GUIDToAssetPath);
            Array.Sort(paths, StringComparer.Ordinal);
            if (paths.Length == 0) {
                throw new BuildFailedException("No RO runtime shaders were found in Assets/Ragnarok/Runtime.");
            }

            int added = 0;
            foreach (string path in paths) {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null || ShaderUtil.ShaderHasError(shader)) {
                    throw new BuildFailedException($"RO runtime shader could not be compiled or loaded: {path}");
                }

                if (!shaders.Add(shader)) continue;
                int index = included.arraySize;
                included.InsertArrayElementAtIndex(index);
                included.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                added++;
            }

            if (added > 0) {
                graphics.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(settings[0]);
            }

            Debug.Log($"[RO] {paths.Length} runtime shaders included in player build ({added} added to Graphics Settings).");
        }
    }
}
