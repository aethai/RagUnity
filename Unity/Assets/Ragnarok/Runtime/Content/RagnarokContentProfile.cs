using System;
using System.Collections.Generic;

namespace Ragnarok.Content {
    [Serializable]
    public sealed class RagnarokContentProfile {
        public string name = "local";
        public string rootPath = "";
        public List<string> grfFiles = new List<string>();

        public string Name => name;
        public string RootPath => rootPath;
        public IReadOnlyList<string> GrfFiles => grfFiles;

        public RagnarokContentProfile() {
        }

        public RagnarokContentProfile(string name, string rootPath, IReadOnlyList<string> grfFiles) {
            this.name = string.IsNullOrWhiteSpace(name) ? "local" : name;
            this.rootPath = rootPath ?? "";
            this.grfFiles = grfFiles == null ? new List<string>() : new List<string>(grfFiles);
        }
    }
}
