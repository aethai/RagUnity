using System;
using System.Collections.Generic;

namespace Ragnarok.Assets {
    public interface IRoAssetSource : IDisposable {
        string Name { get; }
        IReadOnlyCollection<string> Files { get; }

        bool Exists(string path);
        byte[] ReadBytes(string path);
        bool TryGetFingerprint(string path, out string fingerprint);
        /// <summary>
        /// Where the file's stored bytes live, when the source can tell. Entries that point at the same block have
        /// the same content; see <see cref="RoAssetContentKey"/>.
        /// </summary>
        bool TryGetContentBlock(string path, out ulong offset, out uint length);
    }
}
