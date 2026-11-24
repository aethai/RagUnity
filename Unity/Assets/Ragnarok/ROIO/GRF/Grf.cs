using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using ICSharpCode.SharpZipLib.Zip.Compression;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace ROIO.GRF
{
    public class Grf : IDisposable
    {
        /* Headers */
        public static string GRF_HEADER = "Master of Magic";
        public static string GRF_HEADER_V3 = "Event Horizon";
        public static int GRF_HEADER_LEN = GRF_HEADER.Length;
        public static int GRF_HEADER_MID_LEN = GRF_HEADER_LEN + 0xF;    /* -1 + 0xF */
        public static int GRF_HEADER_FULL_LEN = GRF_HEADER_LEN + 0x1F;
        public static uint GRF_TYPE_GRF = 0x01; /** Value to distinguish a GRF file in Grf::type */
        private const uint GRF_VERSION_200 = 0x0200;
        private const uint GRF_VERSION_300 = 0x0300;
        private const uint GRF_VERSION_MASK = 0xFF00;
        private const int GRF_V3_TABLE_PREFIX_LEN = 4;
        private const int GRF_V2_ENTRY_TAIL_LEN = 0x11;
        private const int GRF_V3_ENTRY_TAIL_LEN = 0x15;

        string filename;
        protected ulong len;
        protected uint type;
        protected uint version;
        protected uint nfiles;

        public Hashtable files;
        private Dictionary<string, GrfFile> cachedFilesByPath;
        private List<GrfFile> cachedFiles;
        private bool cachedFilesReadOnly;

        protected byte allowCrypt;
        protected bool allowWrite;
        private FileStream archiveStream;
        private readonly object archiveStreamSync = new object();
        private bool disposed;
        private static int payloadReadCountForDiagnostics;
        private static int payloadInflateCountForDiagnostics;
        private static int tableInflateCountForDiagnostics;

        public static int PayloadReadCountForDiagnostics => payloadReadCountForDiagnostics;
        public static int PayloadInflateCountForDiagnostics => payloadInflateCountForDiagnostics;
        public static int TableInflateCountForDiagnostics => tableInflateCountForDiagnostics;

        public static void ResetPayloadReadCountForDiagnostics()
        {
            Interlocked.Exchange(ref payloadReadCountForDiagnostics, 0);
        }

        public static void ResetPayloadInflateCountForDiagnostics()
        {
            Interlocked.Exchange(ref payloadInflateCountForDiagnostics, 0);
        }

        public static void ResetTableInflateCountForDiagnostics()
        {
            Interlocked.Exchange(ref tableInflateCountForDiagnostics, 0);
        }

        public static Grf grf_callback_open(string fname, string mode, GrfOpenCallback callback)
        {
            byte[] buf = new byte[GRF_HEADER_FULL_LEN];
            uint i;//, zero_fcount = GrfSupport.ToLittleEndian32(7), create_ver = GrfSupport.ToLittleEndian32(0x0200);
            Grf grf;

            if (fname == null || mode == null)
            {
                throw new Exception("GE_BADARGS");
            }

            /* Allocate memory for grf */
            grf = new Grf();

            /* Copy the filename */
            grf.filename = fname;

            /* Open the file */
            grf.archiveStream = OpenArchiveStream(grf.filename);
            Stream fStream = grf.archiveStream;

            if (fStream == null)
            {
                throw new Exception("GE_ERRNO");
            }

            try
            {
                using (var br = new System.IO.BinaryReader(fStream, Encoding.ASCII, leaveOpen: true))
                {
                grf.allowWrite = !mode.Contains("+") && mode.Contains("w");

                //***skipped write***/

                /* Read the header */
                buf = br.ReadBytes(GRF_HEADER_FULL_LEN);

                if (buf.Length != GRF_HEADER_FULL_LEN)
                {
                    throw new Exception("GE_INVALID");
                }

                /* Check the header */
                string signature = ReadSignature(buf);
                if (!string.Equals(signature, GRF_HEADER, StringComparison.Ordinal) &&
                    !string.Equals(signature, GRF_HEADER_V3, StringComparison.Ordinal))
                {
                    throw new Exception("GE_INVALID");
                }

                /* Continued header check...
                 *
                 * GRF files that allow encryption of the files inside the archive
                 * have a hex header following "Master of Magic" (not including
                 * the nul-terminator) that looks like:
                 * 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E
                 *
                 * GRF files that do not allow it have a hex header that looks like:
                 * 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
                 *
                 * GRF files that do not allow it are generally found after a
                 * "Ragnarok.exe /repak" command has been issued
                 */
                if (buf[GRF_HEADER_LEN + 1] == 1)
                {
                    grf.allowCrypt = 1;
                    /* 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E */
                    for (i = 0; i < 0xF; i++)
                    {
                        if (buf[GRF_HEADER_LEN + i] != (int)i)
                        {
                            throw new Exception("GE_CORRUPTED");
                        }
                    }
                }
                else if (buf[GRF_HEADER_LEN] == 0)
                {
                    grf.allowCrypt = 0;
                    /* 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 */
                    for (i = 0; i < 0xF; i++)
                    {
                        if (buf[GRF_HEADER_LEN + i] != 0)
                        {
                            throw new Exception("GE_CORRUPTED");
                        }
                    }
                }
                else
                {
                    throw new Exception("GE_CORRUPTED");

                }

                /* Okay, so we finally are sure that its a valid GRF/GPF file.
                 * now its time to read info from it
                 */

                /* Set the type of archive this is */
                grf.type = GRF_TYPE_GRF;

                /* Read the version */
                grf.version = GrfSupport.LittleEndian32(buf, GRF_HEADER_MID_LEN + 0xC);
                uint majorVersion = grf.version & GRF_VERSION_MASK;
                bool eventHorizonLayout = string.Equals(signature, GRF_HEADER_V3, StringComparison.Ordinal);

                /* Read the number of files */
                uint rawNfiles = GrfSupport.LittleEndian32(buf, GRF_HEADER_MID_LEN + 8);
                uint rawNfiles2 = GrfSupport.LittleEndian32(buf, GRF_HEADER_MID_LEN + 4);
                grf.nfiles = majorVersion == GRF_VERSION_300
                    ? rawNfiles
                    : rawNfiles - rawNfiles2 - 7;

                /* Create the array of files */
                grf.files = new Hashtable(checked((int)Math.Min(grf.nfiles, int.MaxValue)), StringComparer.OrdinalIgnoreCase);

                /* Grab the filesize */
                grf.len = (ulong)fStream.Length;

                /* Seek to the offset of the file tables */
                ulong fileTableOffset = eventHorizonLayout
                    ? LittleEndian64(buf, GRF_HEADER_MID_LEN)
                    : GrfSupport.LittleEndian32(buf, GRF_HEADER_MID_LEN);

                ulong fileTablePosition = fileTableOffset + (ulong)GRF_HEADER_FULL_LEN;
                if (eventHorizonLayout)
                {
                    fileTablePosition += GRF_V3_TABLE_PREFIX_LEN;
                }

                Seek(br.BaseStream, fileTablePosition);

                /* Run a different function to read the file information based on
                 * the major version number
                 */
                switch (majorVersion)
                {
                    case GRF_VERSION_200:
                        i = GRF_readFileTable_info(br, grf, callback, false);
                        break;
                    case GRF_VERSION_300:
                        i = GRF_readFileTable_info(br, grf, callback, eventHorizonLayout);
                        break;
                    default:
                        throw new Exception("UNSUP_GRF_VERSION");
                }

                if (i > 0)
                {
                    grf.Dispose();
                    return null;
                }
            }
            }
            catch
            {
                grf.Dispose();
                throw;
            }

            return grf;
        }

        public static Grf CreateFromDescriptors(string fname, IEnumerable<GrfFile> descriptors)
        {
            return CreateFromDescriptors(fname, descriptors, cloneDescriptors: true);
        }

        public static Grf CreateFromOwnedDescriptors(string fname, IEnumerable<GrfFile> descriptors)
        {
            return CreateFromDescriptors(fname, descriptors, cloneDescriptors: false);
        }

        public static Grf CreateFromCachedDescriptors(string fname, IEnumerable<GrfFile> descriptors)
        {
            return CreateFromDescriptors(fname, descriptors, cloneDescriptors: false, normalizeDescriptors: false);
        }

        public static Grf CreateFromSharedCachedDescriptorIndex(string fname, CachedDescriptorIndex descriptorIndex)
        {
            if (fname == null)
            {
                throw new ArgumentNullException(nameof(fname));
            }

            if (descriptorIndex == null)
            {
                throw new ArgumentNullException(nameof(descriptorIndex));
            }

            if (!File.Exists(fname))
            {
                throw new Exception("GE_ERRNO");
            }

            return new Grf
            {
                filename = fname,
                type = GRF_TYPE_GRF,
                nfiles = checked((uint)descriptorIndex.Count),
                len = (ulong)new FileInfo(fname).Length,
                archiveStream = OpenArchiveStream(fname),
                cachedFilesByPath = descriptorIndex.Lookup,
                cachedFiles = descriptorIndex.Descriptors,
                cachedFilesReadOnly = true,
                files = new CachedGrfFileHashtable(descriptorIndex.Lookup, descriptorIndex.Descriptors, readOnly: true)
            };
        }

        public static Grf CreateForCachedIndex(string fname, int expectedFileCount)
        {
            if (fname == null)
            {
                throw new ArgumentNullException(nameof(fname));
            }

            if (!File.Exists(fname))
            {
                throw new Exception("GE_ERRNO");
            }

            int capacity = Math.Max(0, expectedFileCount);
            var cachedLookup = new Dictionary<string, GrfFile>(capacity, StringComparer.OrdinalIgnoreCase);
            var cachedList = new List<GrfFile>(capacity);
            return new Grf
            {
                filename = fname,
                type = GRF_TYPE_GRF,
                len = (ulong)new FileInfo(fname).Length,
                archiveStream = OpenArchiveStream(fname),
                cachedFilesByPath = cachedLookup,
                cachedFiles = cachedList,
                files = new CachedGrfFileHashtable(cachedLookup, cachedList)
            };
        }

        private static Grf CreateFromDescriptors(string fname, IEnumerable<GrfFile> descriptors, bool cloneDescriptors)
        {
            return CreateFromDescriptors(fname, descriptors, cloneDescriptors, normalizeDescriptors: true);
        }

        private static Grf CreateFromDescriptors(
            string fname,
            IEnumerable<GrfFile> descriptors,
            bool cloneDescriptors,
            bool normalizeDescriptors)
        {
            if (fname == null)
            {
                throw new ArgumentNullException(nameof(fname));
            }

            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            if (!File.Exists(fname))
            {
                throw new Exception("GE_ERRNO");
            }

            var grf = new Grf
            {
                filename = fname,
                type = GRF_TYPE_GRF,
                len = (ulong)new FileInfo(fname).Length,
                archiveStream = OpenArchiveStream(fname),
                files = new Hashtable(EstimateDescriptorCount(descriptors), StringComparer.OrdinalIgnoreCase)
            };

            try
            {
                uint count = 0;
                foreach (GrfFile descriptor in descriptors)
                {
                    if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.name))
                    {
                        continue;
                    }

                    GrfFile copy = cloneDescriptors ? CloneDescriptor(descriptor) : descriptor;
                    if (normalizeDescriptors)
                    {
                        copy.name = NormalizePath(copy.name);
                    }

                    if (copy.hash == 0)
                    {
                        copy.hash = GrfSupport.GRF_NameHash(copy.name);
                    }

                    grf.files[copy.name] = copy;
                    count++;
                }

                grf.nfiles = count;
            }
            catch
            {
                grf.Dispose();
                throw;
            }

            return grf;
        }

        /*! \brief Private function to read GRF file tables
         *
         * Reads the information about files within the archive...
         * for archive versions 0x02xx and 0x03xx.
         *
         * \param grf Pointer to the Grf struct to read to
         * \param error Pointer to a GrfErrorType struct/enum for error reporting
         * \param callback Function to call for each read file. It should return 0 if
         *		everything is fine, 1 if everything is fine (but further
         *		reading should stop), or -1 if there has been an error
         * \return 0 if everything went fine, 1 if something went wrong
         */
        static uint GRF_readFileTable_info(System.IO.BinaryReader br, Grf grf, GrfOpenCallback callback, bool use64BitEntryOffsets)
        {
            uint i, len, len2;
            int offset;
            byte[] buf, zbuf;

            /* Read the original and compressed sizes */
            buf = br.ReadBytes(8);

            /* Allocate memory and read the compressed file table */
            len = GrfSupport.LittleEndian32(buf, 0);
            zbuf = br.ReadBytes((int)len);

            if (0 == (len2 = GrfSupport.LittleEndian32(buf, 4)))
            {
                return 0;
            }

            /* Allocate memory and uncompress the compressed file table */
            Array.Resize(ref buf, (int)len2);
            InflateInto(zbuf, (int)len, buf);
            Interlocked.Increment(ref tableInflateCountForDiagnostics);

            /* Read information about each file */
            for (i = 0, offset = 0; i < grf.nfiles; i++)
            {
                /* Grab the filename length */
                len = GrfSupport.getCStringLength(buf, (uint)offset) + 1;

                /* Make sure its not too large */
                if (len >= GrfTypes.GRF_NAMELEN)
                {
                    throw new Exception("GE_CORRUPTED");
                }

                /* Grab filename */
                GrfFile file = new GrfFile();
                file.name = GrfSupport.getCString(buf, (uint)offset);

                offset += (int)len;

                /* Grab the rest of the information */
                file.compressed_len = GrfSupport.LittleEndian32(buf, offset);
                file.compressed_len_aligned = GrfSupport.LittleEndian32(buf, offset + 4);
                file.real_len = GrfSupport.LittleEndian32(buf, offset + 8);
                file.flags = buf[offset + 0xC];
                ulong entryOffset = use64BitEntryOffsets
                    ? LittleEndian64(buf, offset + 0xD)
                    : GrfSupport.LittleEndian32(buf, offset + 0xD);
                file.pos = entryOffset + (ulong)GRF_HEADER_FULL_LEN;
                file.hash = GrfSupport.GRF_NameHash(file.name);

                file.name = NormalizePath(file.name);

                grf.files.Add(file.name, file);

                /* Advance to the next file */
                offset += use64BitEntryOffsets ? GRF_V3_ENTRY_TAIL_LEN : GRF_V2_ENTRY_TAIL_LEN;

                /* Run the callback, if we have one */
                if (callback != null)
                {
                    callback.doCallback(file);

                    if (!callback.hasReturned())
                    {
                        throw new Exception("NO_RETURN");
                    }

                    if (callback.Response < 0)
                    {
                        /* Callback function had an error, so we
                         * have an error
                         */
                        return 1;
                    }
                    else if (callback.Response > 0)
                    {
                        /* Callback function found the file it needed,
                         * just exit now
                         */
                        return 0;
                    }
                }
            }

            /* Calling functions will set success...
            GRF_SETERR(error,GE_SUCCESS,GRF_readVer2_info);
            */

            return 0;
        }

        public static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ReadSignature(byte[] header)
        {
            int length = 0;
            while (length < GRF_HEADER_LEN && header[length] != 0)
            {
                length++;
            }

            return Encoding.ASCII.GetString(header, 0, length);
        }

        private static ulong LittleEndian64(byte[] buffer, int offset)
        {
            if (offset + 8 > buffer.Length)
            {
                throw new Exception("OUT_OF_BOUNDS");
            }

            return (ulong)buffer[offset] |
                ((ulong)buffer[offset + 1] << 8) |
                ((ulong)buffer[offset + 2] << 16) |
                ((ulong)buffer[offset + 3] << 24) |
                ((ulong)buffer[offset + 4] << 32) |
                ((ulong)buffer[offset + 5] << 40) |
                ((ulong)buffer[offset + 6] << 48) |
                ((ulong)buffer[offset + 7] << 56);
        }

        private static void Seek(Stream stream, ulong position)
        {
            if (position > long.MaxValue)
            {
                throw new Exception("GE_CORRUPTED");
            }

            stream.Seek((long)position, SeekOrigin.Begin);
        }

        private static FileStream OpenArchiveStream(string path)
        {
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch
            {
                throw new Exception("GE_ERRNO");
            }
        }

        private void EnsureNotDisposed()
        {
            if (disposed || archiveStream == null)
            {
                throw new ObjectDisposedException(nameof(Grf));
            }
        }

        /// <param name="pooled">The block was rented from <see cref="ArrayPool{T}.Shared"/> (plain blocks: they are only
        /// inflated, so the buffer goes back to the pool afterwards instead of becoming garbage).</param>
        private byte[] getFileZBlock(GrfFile file, out bool pooled)
        {
            pooled = false;
            EnsureNotDisposed();

            if (file.compressed_len > int.MaxValue || file.compressed_len_aligned > int.MaxValue)
            {
                throw new Exception("GE_CORRUPTED");
            }

            if (file.pos > len || file.pos + file.compressed_len_aligned > len)
            {
                throw new Exception("GE_CORRUPTED");
            }

            int length = (int)file.compressed_len_aligned;
            // plain zlib block (most entries of 0x200 archives): nothing to decrypt, read into a pooled buffer
            bool plain = (file.flags & (GrfTypes.GRFFILE_FLAG_MIXCRYPT | GrfTypes.GRFFILE_FLAG_0x14_DES)) == 0;
            byte[] data = plain ? ArrayPool<byte>.Shared.Rent(length) : new byte[length];
            try
            {
                lock (archiveStreamSync)
                {
                    Seek(archiveStream, file.pos);
                    ReadFully(archiveStream, data, length);
                }
            }
            catch
            {
                if (plain)
                {
                    ArrayPool<byte>.Shared.Return(data);
                }

                throw;
            }

            if (plain)
            {
                pooled = true;
                return data;
            }

            byte[] keyschedule = new byte[0x80];

            return GrfCrypt.GRFProcess(data, file.compressed_len_aligned, file.flags, file.compressed_len, keyschedule);
        }

        public byte[] GetData(GrfFile file)
        {
            return GetData(file, true);
        }

        public byte[] ReadData(GrfFile file)
        {
            return GetData(file, false);
        }

        private byte[] GetData(GrfFile file, bool cacheDecompressedData)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            //not a file
            if (file.IsDir())
            {
                return null;
            }

            //empty file
            if (file.real_len == 0)
            {
                return null;
            }

            //check if data was already extracted
            if (cacheDecompressedData && file.data != null)
            {
                return file.data;
            }

            /* Retrieve the unencrypted block */
            byte[] zbuf = getFileZBlock(file, out bool pooled);
            if (zbuf == null)
            {
                return null;
            }

            Interlocked.Increment(ref payloadReadCountForDiagnostics);

            byte[] data = new byte[file.real_len];
            try
            {
                InflateInto(zbuf, checked((int)file.compressed_len), data);
            }
            finally
            {
                if (pooled)
                {
                    ArrayPool<byte>.Shared.Return(zbuf);
                }
            }

            Interlocked.Increment(ref payloadInflateCountForDiagnostics);

            if (cacheDecompressedData)
            {
                file.data = data;
            }

            return data;
        }

        private static void ReadFully(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                {
                    throw new Exception("GE_CORRUPTED");
                }

                offset += read;
            }
        }

        private static void InflateInto(byte[] compressed, int compressedLength, byte[] output)
        {
            if (compressed == null || output == null || compressedLength < 0 || compressedLength > compressed.Length)
            {
                throw new Exception("GE_CORRUPTED");
            }

            if (output.Length == 0)
            {
                return;
            }

            if (TryInflateNative(compressed, compressedLength, output))
            {
                return;
            }

            var inflater = new Inflater();
            inflater.SetInput(compressed, 0, compressedLength);

            int offset = 0;
            try
            {
                while (offset < output.Length)
                {
                    int inflated = inflater.Inflate(output, offset, output.Length - offset);
                    if (inflated <= 0)
                    {
                        throw new InvalidDataException("Inflater stopped before the expected output length.");
                    }

                    offset += inflated;
                }

                if (!inflater.IsFinished)
                {
                    byte[] extra = new byte[1];
                    int extraInflated = inflater.Inflate(extra, 0, extra.Length);
                    if (extraInflated != 0 || !inflater.IsFinished)
                    {
                        throw new InvalidDataException("Inflater did not finish at the expected output length.");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("GE_CORRUPTED", ex);
            }
        }

        private static volatile bool nativeInflateUnavailable;
        private static readonly byte[] trailingProbe = new byte[1];

        /// <summary>
        /// Inflates a zlib (RFC 1950) block with the runtime's native zlib (DeflateStream: zlib in Mono and IL2CPP),
        /// several times faster than SharpZipLib's managed inflater and identical in output. False when the block is
        /// not a plain zlib stream, does not end exactly at the expected length, or native zlib is missing: the
        /// managed inflater then decodes it (and reports corruption) as before.
        /// </summary>
        private static bool TryInflateNative(byte[] compressed, int compressedLength, byte[] output)
        {
            if (nativeInflateUnavailable || compressedLength < 6)
            {
                return false;
            }

            int cmf = compressed[0], flg = compressed[1];
            if ((cmf & 0x0f) != 8 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0)
            {
                return false;   // not deflate, bad header check bits, or a preset dictionary
            }

            try
            {
                using (var stream = new DeflateStream(new MemoryStream(compressed, 2, compressedLength - 2, false), CompressionMode.Decompress))
                {
                    int offset = 0;
                    while (offset < output.Length)
                    {
                        int read = stream.Read(output, offset, output.Length - offset);
                        if (read <= 0)
                        {
                            return false;
                        }

                        offset += read;
                    }

                    // the stream must end at the expected length (the managed path checks the same)
                    return stream.Read(trailingProbe, 0, 1) == 0;
                }
            }
            catch (DllNotFoundException)
            {
                nativeInflateUnavailable = true;
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                nativeInflateUnavailable = true;
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public GrfFile GetDescriptor(string name)
        {
            if (cachedFilesByPath != null)
            {
                cachedFilesByPath.TryGetValue(NormalizePath(name), out GrfFile descriptor);
                return descriptor;
            }

            return files[NormalizePath(name)] as GrfFile;
        }

        public void AddCachedDescriptor(GrfFile descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.name))
            {
                throw new ArgumentException("Cached GRF descriptor is required.", nameof(descriptor));
            }

            if (cachedFilesByPath != null)
            {
                if (cachedFilesReadOnly)
                {
                    throw new InvalidOperationException("Shared cached GRF descriptor indexes are read-only.");
                }

                cachedFilesByPath[descriptor.name] = descriptor;
                cachedFiles?.Add(descriptor);
            }
            else
            {
                files[descriptor.name] = descriptor;
            }

            nfiles++;
        }

        public IEnumerable<string> EnumeratePaths()
        {
            if (cachedFiles != null)
            {
                for (int i = 0; i < cachedFiles.Count; i++)
                {
                    yield return cachedFiles[i].name;
                }

                yield break;
            }

            foreach (DictionaryEntry entry in files)
            {
                if (entry.Key is string path)
                {
                    yield return path;
                }
            }
        }

        public IEnumerable<GrfFile> EnumerateDescriptors()
        {
            if (cachedFiles != null)
            {
                for (int i = 0; i < cachedFiles.Count; i++)
                {
                    yield return cachedFiles[i];
                }

                yield break;
            }

            foreach (DictionaryEntry entry in files)
            {
                if (entry.Value is GrfFile file)
                {
                    yield return file;
                }
            }
        }

        public CachedDescriptorIndex ShareCachedDescriptorIndex()
        {
            if (cachedFilesByPath != null && cachedFiles != null)
            {
                return new CachedDescriptorIndex(cachedFilesByPath, cachedFiles);
            }

            var lookup = new Dictionary<string, GrfFile>(FileCount, StringComparer.OrdinalIgnoreCase);
            var descriptors = new List<GrfFile>(FileCount);
            foreach (GrfFile descriptor in EnumerateDescriptors())
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.name))
                {
                    continue;
                }

                lookup[descriptor.name] = descriptor;
                descriptors.Add(descriptor);
            }

            return new CachedDescriptorIndex(lookup, descriptors);
        }

        public int FileCount => cachedFiles?.Count ?? files?.Count ?? 0;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                archiveStream?.Dispose();
                archiveStream = null;
            }

            disposed = true;
        }

        private static GrfFile CloneDescriptor(GrfFile descriptor)
        {
            return new GrfFile
            {
                compressed_len_aligned = descriptor.compressed_len_aligned,
                compressed_len = descriptor.compressed_len,
                real_len = descriptor.real_len,
                pos = descriptor.pos,
                flags = descriptor.flags,
                hash = descriptor.hash,
                name = descriptor.name
            };
        }

        private static int EstimateDescriptorCount(IEnumerable<GrfFile> descriptors)
        {
            if (descriptors is ICollection<GrfFile> genericCollection)
            {
                return Math.Max(0, genericCollection.Count);
            }

            if (descriptors is ICollection collection)
            {
                return Math.Max(0, collection.Count);
            }

            return 0;
        }

        public sealed class CachedDescriptorIndex
        {
            internal CachedDescriptorIndex(Dictionary<string, GrfFile> lookup, List<GrfFile> descriptors)
            {
                Lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
                Descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
            }

            internal Dictionary<string, GrfFile> Lookup { get; }
            internal List<GrfFile> Descriptors { get; }
            public int Count => Descriptors.Count;
        }

        private sealed class CachedGrfFileHashtable : Hashtable
        {
            private readonly Dictionary<string, GrfFile> lookup;
            private readonly List<GrfFile> descriptors;
            private readonly bool readOnly;

            public CachedGrfFileHashtable(Dictionary<string, GrfFile> lookup, List<GrfFile> descriptors)
                : this(lookup, descriptors, readOnly: false)
            {
            }

            public CachedGrfFileHashtable(Dictionary<string, GrfFile> lookup, List<GrfFile> descriptors, bool readOnly)
                : base(StringComparer.OrdinalIgnoreCase)
            {
                this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
                this.descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
                this.readOnly = readOnly;
            }

            public override int Count => lookup.Count;

            public override object this[object key]
            {
                get
                {
                    return key is string path && lookup.TryGetValue(path, out GrfFile file)
                        ? file
                        : null;
                }
                set
                {
                    if (readOnly)
                    {
                        throw new InvalidOperationException("Shared cached GRF descriptor indexes are read-only.");
                    }

                    if (!(key is string path))
                    {
                        throw new ArgumentException("GRF file key must be a path string.", nameof(key));
                    }

                    if (!(value is GrfFile file))
                    {
                        throw new ArgumentException("GRF file value must be a descriptor.", nameof(value));
                    }

                    if (!lookup.ContainsKey(path))
                    {
                        descriptors.Add(file);
                    }

                    lookup[path] = file;
                }
            }

            public override void Add(object key, object value)
            {
                this[key] = value;
            }

            public override bool ContainsKey(object key)
            {
                return key is string path && lookup.ContainsKey(path);
            }

            public override void Clear()
            {
                if (readOnly)
                {
                    throw new InvalidOperationException("Shared cached GRF descriptor indexes are read-only.");
                }

                lookup.Clear();
                descriptors.Clear();
            }

            public override IDictionaryEnumerator GetEnumerator()
            {
                return new CachedGrfFileEnumerator(descriptors);
            }
        }

        private sealed class CachedGrfFileEnumerator : IDictionaryEnumerator
        {
            private readonly List<GrfFile> descriptors;
            private int index = -1;

            public CachedGrfFileEnumerator(List<GrfFile> descriptors)
            {
                this.descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
            }

            public DictionaryEntry Entry => new DictionaryEntry(Key, Value);
            public object Key => CurrentDescriptor.name;
            public object Value => CurrentDescriptor;
            public object Current => Entry;

            public bool MoveNext()
            {
                if (index + 1 >= descriptors.Count)
                {
                    return false;
                }

                index++;
                return true;
            }

            public void Reset()
            {
                index = -1;
            }

            private GrfFile CurrentDescriptor
            {
                get
                {
                    if (index < 0 || index >= descriptors.Count)
                    {
                        throw new InvalidOperationException("Enumerator is not positioned on a GRF descriptor.");
                    }

                    return descriptors[index];
                }
            }
        }
    }
}

