namespace Ragnarok.Assets {
    public sealed class RoAssetManifestEntry {
        public RoAssetManifestEntry(string path, string sourceName, int sourceIndex) {
            Path = path;
            SourceName = sourceName;
            SourceIndex = sourceIndex;
        }

        public string Path { get; }
        public string SourceName { get; }
        public int SourceIndex { get; }
    }
}
