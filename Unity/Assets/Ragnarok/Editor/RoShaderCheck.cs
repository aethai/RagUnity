using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    /// <summary>
    /// Batch check of the RO shaders without a player build (the build only says "could not be compiled"):
    /// -executeMethod Ragnarok.Editor.RoShaderCheck.Run [-roShaderCheckOut file] writes every shader's errors and warnings.
    /// </summary>
    public static class RoShaderCheck {
        public static void Run() {
            var text = new StringBuilder();
            int errors = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Ragnarok" })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;
                // compile every pass for the active platform so the messages are filled
                var material = new Material(shader);
                for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                bool hasError = ShaderUtil.ShaderHasError(shader);
                if (hasError) errors++;
                text.Append(hasError ? "ERROR " : "ok    ").Append(path).Append('\n');
                foreach (ShaderMessage m in messages) {
                    text.Append("    ").Append(m.severity).Append(' ').Append(m.file).Append(':').Append(m.line).Append(' ').Append(m.message)
                        .Append(" (").Append(m.platform).Append(")\n");
                }
            }

            string outPath = ArgumentAfter("-roShaderCheckOut") ?? "Temp/ro-shader-check.txt";
            File.WriteAllText(outPath, text.ToString());
            Debug.Log("[RoShaderCheck] " + errors + " shader(s) with errors -> " + outPath + "\n" + text);
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        static string ArgumentAfter(string name) {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) {
                if (args[i] == name) return args[i + 1];
            }

            return null;
        }
    }
}
