using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Content {
    [CreateAssetMenu(fileName = "RagnarokContentSettings", menuName = "Ragnarok/Content Settings")]
    public sealed class RagnarokContentSettings : ScriptableObject {
        [SerializeField] private string rootPath = "";
        [SerializeField] private List<string> grfFiles = new List<string>();

        public string RootPath => rootPath;
        public IReadOnlyList<string> GrfFiles => grfFiles;
    }
}
