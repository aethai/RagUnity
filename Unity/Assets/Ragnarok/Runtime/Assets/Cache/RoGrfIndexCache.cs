using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ROIO.GRF;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Ragnarok.Assets {
    public sealed class RoGrfIndexCacheReadTiming {
        public bool CacheFileFound { get; private set; }
        public bool ParseSucceeded { get; private set; }
        public bool MemoryCacheHit { get; private set; }
        public long CacheBytes { get; private set; }
        public int DescriptorCount { get; private set; }
        public long DiskReadMilliseconds { get; private set; }
        public long BinaryParseMilliseconds { get; private set; }

        internal void RecordDiskRead(long milliseconds, long cacheBytes, bool found) {
            DiskReadMilliseconds = milliseconds;
            CacheBytes = Math.Max(0, cacheBytes);
            CacheFileFound = found;
            MemoryCacheHit = false;
        }

        internal void RecordBinaryParse(long milliseconds, int descriptorCount, bool succeeded) {
            BinaryParseMilliseconds = milliseconds;
            DescriptorCount = Math.Max(0, descriptorCount);
            ParseSucceeded = succeeded;
        }

        internal void RecordMemoryHit(int descriptorCount) {
            CacheFileFound = true;
            ParseSucceeded = true;
            MemoryCacheHit = true;
            CacheBytes = 0;
            DescriptorCount = Math.Max(0, descriptorCount);
            DiskReadMilliseconds = 0;
            BinaryParseMilliseconds = 0;
        }
    }

    public sealed class RoGrfIndexEntry {
        public string Path { get; set; }
        public uint CompressedLength { get; set; }
        public uint CompressedLengthAligned { get; set; }
        public uint RealLength { get; set; }
        public byte Flags { get; set; }
        public ulong Position { get; set; }
        public uint Hash { get; set; }

        public static RoGrfIndexEntry FromGrfFile(GrfFile file) {
            if (file == null) {
                throw new ArgumentNullException(nameof(file));
            }

            string path = RoAssetPath.Normalize(file.Path);
            return new RoGrfIndexEntry {
                Path = path,
                CompressedLength = file.compressed_len,
                CompressedLengthAligned = file.compressed_len_aligned,
                RealLength = file.real_len,
                Flags = file.flags,
                Position = file.pos,
                Hash = file.hash == 0 ? GrfSupport.GRF_NameHash(path) : file.hash
            };
        }

        public GrfFile ToGrfFile() {
            string path = RoAssetPath.Normalize(Path);
            return new GrfFile {
                name = path,
                compressed_len = CompressedLength,
                compressed_len_aligned = CompressedLengthAligned,
                real_len = RealLength,
                flags = Flags,
                pos = Position,
                hash = Hash == 0 ? GrfSupport.GRF_NameHash(path) : Hash
            };
        }
    }

    public sealed class RoGrfIndexRecord {
        public const int IndexFormatVersion = 3;
        public const int LegacyIndexFormatVersion = 2;

        private RoGrfIndexRecord(
            string archivePath,
            int sourceOrder,
            long archiveLength,
            long archiveLastWriteTimeUtcTicks) {
            ArchivePath = Path.GetFullPath(archivePath);
            SourceOrder = sourceOrder;
            ArchiveLength = archiveLength;
            ArchiveLastWriteTimeUtcTicks = archiveLastWriteTimeUtcTicks;
        }

        public string ArchivePath { get; }
        public int SourceOrder { get; }
        public long ArchiveLength { get; }
        public long ArchiveLastWriteTimeUtcTicks { get; }

        public string CacheKey => GetCacheKey(IndexFormatVersion);

        public string LegacyCacheKey => GetCacheKey(LegacyIndexFormatVersion);

        public string GetCacheKey(int formatVersion) =>
            $"{SourceOrder}|{ArchivePath}|{ArchiveLength}|{ArchiveLastWriteTimeUtcTicks}|v{formatVersion}";

        public static RoGrfIndexRecord ForArchive(string archivePath, int sourceOrder) {
            if (string.IsNullOrWhiteSpace(archivePath)) {
                throw new ArgumentException("GRF archive path is required.", nameof(archivePath));
            }

            string fullPath = Path.GetFullPath(archivePath);
            var info = new FileInfo(fullPath);
            if (!info.Exists) {
                throw new FileNotFoundException($"GRF archive does not exist: {fullPath}", fullPath);
            }

            return new RoGrfIndexRecord(
                fullPath,
                sourceOrder,
                info.Length,
                info.LastWriteTimeUtc.Ticks);
        }

        public bool Matches(string archivePath, int sourceOrder, long archiveLength, long archiveLastWriteTimeUtcTicks) {
            return SourceOrder == sourceOrder &&
                ArchiveLength == archiveLength &&
                ArchiveLastWriteTimeUtcTicks == archiveLastWriteTimeUtcTicks &&
                string.Equals(ArchivePath, Path.GetFullPath(archivePath), StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class RoGrfIndexCache {
        private const int Magic = 0x58494752; // RGIX
        private const int MaxIndexEntries = 5_000_000;
        private const int MaxMemorySnapshots = 8;
        private static readonly object MemoryCacheSync = new object();
        private static readonly Dictionary<string, GrfIndexMemorySnapshot> MemorySnapshots =
            new Dictionary<string, GrfIndexMemorySnapshot>(StringComparer.Ordinal);

        public static bool HasIndexFile(RoAssetDiskCache cache, RoGrfIndexRecord record) {
            if (cache == null || record == null) {
                return false;
            }

            return File.Exists(cache.GetPath(RoAssetCacheCategory.GrfIndex, record.CacheKey, ".bin"));
        }

        public static bool TryReadGrf(
            RoAssetDiskCache cache,
            RoGrfIndexRecord record,
            string archivePath,
            out Grf grf,
            RoGrfIndexCacheReadTiming timing = null) {
            grf = null;
            if (cache == null || record == null || string.IsNullOrWhiteSpace(archivePath)) {
                return false;
            }

            if (TryReadGrfFromMemory(cache, record, archivePath, out grf, timing)) {
                return true;
            }

            if (TryReadGrfFromCache(cache, record, archivePath, record.CacheKey, out grf, timing)) {
                TryStoreMemorySnapshot(cache, record, grf);
                return true;
            }

            if (TryReadGrfFromCache(cache, record, archivePath, record.LegacyCacheKey, out grf, timing)) {
                try {
                    Write(cache, record, grf);
                } catch {
                    // Legacy cache migration is best-effort; the parsed GRF index remains valid.
                }

                TryStoreMemorySnapshot(cache, record, grf);
                return true;
            }

            return false;
        }

        private static bool TryReadGrfFromMemory(
            RoAssetDiskCache cache,
            RoGrfIndexRecord record,
            string archivePath,
            out Grf grf,
            RoGrfIndexCacheReadTiming timing
        ) {
            grf = null;
            if (!TryGetIndexCacheFileState(cache, record, out string cachePath, out long cacheLength, out long cacheWriteTicks)) {
                return false;
            }

            GrfIndexMemorySnapshot snapshot;
            lock (MemoryCacheSync) {
                MemorySnapshots.TryGetValue(record.CacheKey, out snapshot);
            }

            if (snapshot == null ||
                !snapshot.Matches(record, cachePath, cacheLength, cacheWriteTicks)) {
                return false;
            }

            try {
                grf = Grf.CreateFromSharedCachedDescriptorIndex(archivePath, snapshot.DescriptorIndex);
                timing?.RecordMemoryHit(snapshot.DescriptorIndex.Count);
                return true;
            } catch {
                grf?.Dispose();
                grf = null;
                return false;
            }
        }

        private static bool TryReadGrfFromCache(
            RoAssetDiskCache cache,
            RoGrfIndexRecord record,
            string archivePath,
            string cacheKey,
            out Grf grf,
            RoGrfIndexCacheReadTiming timing
        ) {
            grf = null;
            long parseStart = 0;
            int parsedDescriptorCount = 0;
            try {
                long readStart = Stopwatch.GetTimestamp();
                if (!cache.TryReadBytes(RoAssetCacheCategory.GrfIndex, cacheKey, ".bin", out byte[] data)) {
                    timing?.RecordDiskRead(ElapsedMilliseconds(readStart), 0, found: false);
                    return false;
                }

                timing?.RecordDiskRead(ElapsedMilliseconds(readStart), data.LongLength, found: true);
                parseStart = Stopwatch.GetTimestamp();
                if (data == null || data.Length < 8 || ReadInt32LittleEndian(data, 0) != Magic) {
                    timing?.RecordBinaryParse(ElapsedMilliseconds(parseStart), parsedDescriptorCount, succeeded: false);
                    return false;
                }

                int version = ReadInt32LittleEndian(data, 4);
                switch (version) {
                    case RoGrfIndexRecord.IndexFormatVersion:
                        grf = ReadGrfV3(data, record, archivePath, out parsedDescriptorCount);
                        break;
                    case RoGrfIndexRecord.LegacyIndexFormatVersion:
                        grf = ReadGrfV2(data, record, archivePath, out parsedDescriptorCount);
                        break;
                    default:
                        timing?.RecordBinaryParse(ElapsedMilliseconds(parseStart), parsedDescriptorCount, succeeded: false);
                        return false;
                }

                timing?.RecordBinaryParse(ElapsedMilliseconds(parseStart), parsedDescriptorCount, succeeded: true);
                return true;
            } catch {
                grf?.Dispose();
                grf = null;
                if (parseStart != 0) {
                    timing?.RecordBinaryParse(ElapsedMilliseconds(parseStart), parsedDescriptorCount, succeeded: false);
                }

                return false;
            }
        }

        private static Grf ReadGrfV2(
            byte[] data,
            RoGrfIndexRecord record,
            string archivePath,
            out int parsedDescriptorCount
        ) {
            parsedDescriptorCount = 0;
            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadInt32() != Magic ||
                reader.ReadInt32() != RoGrfIndexRecord.LegacyIndexFormatVersion) {
                throw new InvalidDataException("Cached GRF index header is invalid.");
            }

            string cachedArchivePath = reader.ReadString();
            int sourceOrder = reader.ReadInt32();
            long archiveLength = reader.ReadInt64();
            long archiveLastWriteTicks = reader.ReadInt64();
            if (!record.Matches(cachedArchivePath, sourceOrder, archiveLength, archiveLastWriteTicks)) {
                throw new InvalidDataException("Cached GRF index record does not match the archive.");
            }

            int count = reader.ReadInt32();
            if (count < 0 || count > MaxIndexEntries) {
                throw new InvalidDataException("Cached GRF index entry count is invalid.");
            }

            Grf grf = Grf.CreateForCachedIndex(archivePath, count);
            try {
                ulong archiveLengthUlong = checked((ulong) archiveLength);
                for (int i = 0; i < count; i++) {
                    var descriptor = new GrfFile {
                        name = reader.ReadString(),
                        compressed_len = reader.ReadUInt32(),
                        compressed_len_aligned = reader.ReadUInt32(),
                        real_len = reader.ReadUInt32(),
                        flags = reader.ReadByte(),
                        pos = reader.ReadUInt64(),
                        hash = reader.ReadUInt32()
                    };

                    AddValidatedDescriptor(grf, descriptor, archiveLengthUlong);
                    parsedDescriptorCount++;
                }

                return grf;
            } catch {
                grf.Dispose();
                throw;
            }
        }

        private static Grf ReadGrfV3(
            byte[] data,
            RoGrfIndexRecord record,
            string archivePath,
            out int parsedDescriptorCount
        ) {
            parsedDescriptorCount = 0;
            var reader = new IndexByteReader(data);
            if (reader.ReadInt32() != Magic ||
                reader.ReadInt32() != RoGrfIndexRecord.IndexFormatVersion) {
                throw new InvalidDataException("Cached GRF index header is invalid.");
            }

            string cachedArchivePath = reader.ReadString();
            int sourceOrder = reader.ReadInt32();
            long archiveLength = reader.ReadInt64();
            long archiveLastWriteTicks = reader.ReadInt64();
            if (!record.Matches(cachedArchivePath, sourceOrder, archiveLength, archiveLastWriteTicks)) {
                throw new InvalidDataException("Cached GRF index record does not match the archive.");
            }

            int count = reader.ReadInt32();
            if (count < 0 || count > MaxIndexEntries) {
                throw new InvalidDataException("Cached GRF index entry count is invalid.");
            }

            Grf grf = Grf.CreateForCachedIndex(archivePath, count);
            try {
                ulong archiveLengthUlong = checked((ulong) archiveLength);
                for (int i = 0; i < count; i++) {
                    var descriptor = new GrfFile {
                        name = reader.ReadString(),
                        compressed_len = reader.ReadUInt32(),
                        compressed_len_aligned = reader.ReadUInt32(),
                        real_len = reader.ReadUInt32(),
                        flags = reader.ReadByte(),
                        pos = reader.ReadUInt64(),
                        hash = reader.ReadUInt32()
                    };

                    AddValidatedDescriptor(grf, descriptor, archiveLengthUlong);
                    parsedDescriptorCount++;
                }

                return grf;
            } catch {
                grf.Dispose();
                throw;
            }
        }

        private static void AddValidatedDescriptor(Grf grf, GrfFile descriptor, ulong archiveLength) {
            if (descriptor.hash == 0) {
                descriptor.hash = GrfSupport.GRF_NameHash(descriptor.name);
            }

            if (!IsValidDescriptor(descriptor, archiveLength)) {
                throw new InvalidDataException("Cached GRF descriptor is invalid.");
            }

            grf.AddCachedDescriptor(descriptor);
        }

        public static bool TryRead(
            RoAssetDiskCache cache,
            RoGrfIndexRecord record,
            out List<GrfFile> descriptors,
            RoGrfIndexCacheReadTiming timing = null) {
            descriptors = null;
            if (cache == null || record == null) {
                return false;
            }

            if (!TryReadGrf(cache, record, record.ArchivePath, out Grf grf, timing)) {
                return false;
            }

            try {
                var result = new List<GrfFile>(grf.FileCount);
                foreach (GrfFile descriptor in grf.EnumerateDescriptors()) {
                    result.Add(descriptor);
                }

                descriptors = result;
                return true;
            } finally {
                grf.Dispose();
            }
        }

        public static string Write(RoAssetDiskCache cache, RoGrfIndexRecord record, Grf grf) {
            if (cache == null) {
                throw new ArgumentNullException(nameof(cache));
            }

            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }

            if (grf == null) {
                throw new ArgumentNullException(nameof(grf));
            }

            var entries = new List<RoGrfIndexEntry>(grf.FileCount);
            foreach (GrfFile file in grf.EnumerateDescriptors()) {
                entries.Add(RoGrfIndexEntry.FromGrfFile(file));
            }

            entries.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
                writer.Write(Magic);
                writer.Write(RoGrfIndexRecord.IndexFormatVersion);
                WriteString(writer, record.ArchivePath);
                writer.Write(record.SourceOrder);
                writer.Write(record.ArchiveLength);
                writer.Write(record.ArchiveLastWriteTimeUtcTicks);
                writer.Write(entries.Count);
                for (int i = 0; i < entries.Count; i++) {
                    RoGrfIndexEntry entry = entries[i];
                    WriteString(writer, entry.Path);
                    writer.Write(entry.CompressedLength);
                    writer.Write(entry.CompressedLengthAligned);
                    writer.Write(entry.RealLength);
                    writer.Write(entry.Flags);
                    writer.Write(entry.Position);
                    writer.Write(entry.Hash);
                }
            }

            string path = cache.WriteBytes(RoAssetCacheCategory.GrfIndex, record.CacheKey, ".bin", stream.ToArray());
            TryStoreMemorySnapshot(cache, record, grf);
            return path;
        }

        private static void TryStoreMemorySnapshot(RoAssetDiskCache cache, RoGrfIndexRecord record, Grf grf) {
            if (cache == null ||
                record == null ||
                grf == null ||
                !TryGetIndexCacheFileState(cache, record, out string cachePath, out long cacheLength, out long cacheWriteTicks)) {
                return;
            }

            Grf.CachedDescriptorIndex descriptorIndex;
            try {
                descriptorIndex = grf.ShareCachedDescriptorIndex();
            } catch {
                return;
            }

            var snapshot = new GrfIndexMemorySnapshot(record, cachePath, cacheLength, cacheWriteTicks, descriptorIndex);
            lock (MemoryCacheSync) {
                if (MemorySnapshots.Count >= MaxMemorySnapshots && !MemorySnapshots.ContainsKey(record.CacheKey)) {
                    MemorySnapshots.Clear();
                }

                MemorySnapshots[record.CacheKey] = snapshot;
            }
        }

        private static bool TryGetIndexCacheFileState(
            RoAssetDiskCache cache,
            RoGrfIndexRecord record,
            out string cachePath,
            out long cacheLength,
            out long cacheLastWriteUtcTicks
        ) {
            cachePath = null;
            cacheLength = 0;
            cacheLastWriteUtcTicks = 0;
            if (cache == null || record == null) {
                return false;
            }

            try {
                cachePath = cache.GetPath(RoAssetCacheCategory.GrfIndex, record.CacheKey, ".bin");
                var info = new FileInfo(cachePath);
                if (!info.Exists) {
                    return false;
                }

                cacheLength = info.Length;
                cacheLastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
                return true;
            } catch {
                cachePath = null;
                cacheLength = 0;
                cacheLastWriteUtcTicks = 0;
                return false;
            }
        }

        private sealed class GrfIndexMemorySnapshot {
            public GrfIndexMemorySnapshot(
                RoGrfIndexRecord record,
                string cachePath,
                long cacheLength,
                long cacheLastWriteUtcTicks,
                Grf.CachedDescriptorIndex descriptorIndex
            ) {
                ArchivePath = record.ArchivePath;
                SourceOrder = record.SourceOrder;
                ArchiveLength = record.ArchiveLength;
                ArchiveLastWriteTimeUtcTicks = record.ArchiveLastWriteTimeUtcTicks;
                CachePath = cachePath ?? string.Empty;
                CacheLength = cacheLength;
                CacheLastWriteUtcTicks = cacheLastWriteUtcTicks;
                DescriptorIndex = descriptorIndex ?? throw new ArgumentNullException(nameof(descriptorIndex));
            }

            public string ArchivePath { get; }
            public int SourceOrder { get; }
            public long ArchiveLength { get; }
            public long ArchiveLastWriteTimeUtcTicks { get; }
            public string CachePath { get; }
            public long CacheLength { get; }
            public long CacheLastWriteUtcTicks { get; }
            public Grf.CachedDescriptorIndex DescriptorIndex { get; }

            public bool Matches(RoGrfIndexRecord record, string cachePath, long cacheLength, long cacheLastWriteUtcTicks) {
                return record != null &&
                    SourceOrder == record.SourceOrder &&
                    ArchiveLength == record.ArchiveLength &&
                    ArchiveLastWriteTimeUtcTicks == record.ArchiveLastWriteTimeUtcTicks &&
                    CacheLength == cacheLength &&
                    CacheLastWriteUtcTicks == cacheLastWriteUtcTicks &&
                    string.Equals(ArchivePath, record.ArchivePath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(CachePath, cachePath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void WriteString(BinaryWriter writer, string value) {
            if (string.IsNullOrEmpty(value)) {
                writer.Write(0);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static int ReadInt32LittleEndian(byte[] data, int offset) {
            if (data == null || offset < 0 || offset + 4 > data.Length) {
                throw new EndOfStreamException();
            }

            return data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24);
        }

        private struct IndexByteReader {
            private readonly byte[] data;
            private int offset;

            public IndexByteReader(byte[] data) {
                this.data = data ?? throw new ArgumentNullException(nameof(data));
                offset = 0;
            }

            public byte ReadByte() {
                EnsureAvailable(1);
                return data[offset++];
            }

            public int ReadInt32() {
                EnsureAvailable(4);
                int value = data[offset] |
                    (data[offset + 1] << 8) |
                    (data[offset + 2] << 16) |
                    (data[offset + 3] << 24);
                offset += 4;
                return value;
            }

            public uint ReadUInt32() {
                return unchecked((uint) ReadInt32());
            }

            public long ReadInt64() {
                return unchecked((long) ReadUInt64());
            }

            public ulong ReadUInt64() {
                EnsureAvailable(8);
                uint low = (uint) (
                    data[offset] |
                    (data[offset + 1] << 8) |
                    (data[offset + 2] << 16) |
                    (data[offset + 3] << 24));
                uint high = (uint) (
                    data[offset + 4] |
                    (data[offset + 5] << 8) |
                    (data[offset + 6] << 16) |
                    (data[offset + 7] << 24));
                offset += 8;
                return low | ((ulong) high << 32);
            }

            public string ReadString() {
                int byteCount = ReadInt32();
                if (byteCount < 0) {
                    throw new InvalidDataException("Cached GRF index string length is invalid.");
                }

                EnsureAvailable(byteCount);
                string value = byteCount == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(data, offset, byteCount);
                offset += byteCount;
                return value;
            }

            private void EnsureAvailable(int byteCount) {
                if (byteCount < 0 || offset > data.Length - byteCount) {
                    throw new EndOfStreamException();
                }
            }
        }

        private static bool IsValidDescriptor(GrfFile descriptor, ulong archiveLength) {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.name)) {
                return false;
            }

            if (descriptor.IsDir() || descriptor.real_len == 0) {
                return true;
            }

            ulong alignedLength = descriptor.compressed_len_aligned;
            return descriptor.pos <= archiveLength && alignedLength <= archiveLength - descriptor.pos;
        }

        private static long ElapsedMilliseconds(long startTimestamp) {
            return (long)((Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency);
        }
    }
}
