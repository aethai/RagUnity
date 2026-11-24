using System;

namespace Ragnarok.Audio {
    /// <summary>Decoded PCM: interleaved samples in [-1, 1], ready for AudioClip.SetData.</summary>
    public sealed class RoPcm {
        public float[] Samples;
        public int Channels;
        public int SampleRate;
        /// <summary>Sample frames (samples per channel).</summary>
        public int Frames => Channels > 0 ? Samples.Length / Channels : 0;
    }

    /// <summary>
    /// RIFF/WAVE decoder for the client's sounds: walks the chunks (word-aligned), reads "fmt " and "data" by their sizes
    /// (a truncated data chunk ends at the file's end), and converts unsigned 8-bit, 16/24/32-bit PCM, 32-bit float and IMA
    /// ADPCM (the client decodes it with AIL_decompress_ADPCM) to interleaved floats as stored — no fades or any other
    /// change. Microsoft ADPCM is not decoded, like the client. Pure C#, safe off the main thread.
    /// </summary>
    public static class RoWavDecoder {
        const int FormatPcm = 1, FormatFloat = 3, FormatImaAdpcm = 0x11, FormatExtensible = 0xfffe;

        public static bool TryDecode(byte[] file, out RoPcm pcm, out string error) {
            pcm = null;
            error = null;
            if (file == null || file.Length < 12 || !Tag(file, 0, "RIFF") || !Tag(file, 8, "WAVE")) {
                error = "not a RIFF/WAVE file";
                return false;
            }

            int format = 0, channels = 0, sampleRate = 0, bits = 0, blockAlign = 0, dataOffset = -1, dataLength = 0;
            int pos = 12;
            while (pos + 8 <= file.Length) {
                int size = BitConverter.ToInt32(file, pos + 4);
                int body = pos + 8;
                if (size < 0) break;
                if (Tag(file, pos, "fmt ") && size >= 16 && body + 16 <= file.Length) {
                    format = BitConverter.ToUInt16(file, body);
                    channels = BitConverter.ToUInt16(file, body + 2);
                    sampleRate = BitConverter.ToInt32(file, body + 4);
                    blockAlign = BitConverter.ToUInt16(file, body + 12);
                    bits = BitConverter.ToUInt16(file, body + 14);
                    if (format == FormatExtensible && size >= 26 && body + 26 <= file.Length) {
                        format = BitConverter.ToUInt16(file, body + 24);   // the sub-format GUID's first two bytes
                    }
                } else if (Tag(file, pos, "data")) {
                    dataOffset = body;
                    dataLength = (int) Math.Min((long) size, file.Length - body);
                }

                pos = body + size + (size & 1);
            }

            if (channels <= 0 || sampleRate <= 0 || dataOffset < 0) {
                error = channels <= 0 ? "no fmt chunk" : "no data chunk";
                return false;
            }

            if (format == FormatImaAdpcm && bits == 4 && blockAlign >= 4 * channels && channels <= 2) {
                pcm = new RoPcm { Samples = DecodeImaAdpcm(file, dataOffset, dataLength, channels, blockAlign), Channels = channels, SampleRate = sampleRate };
                return true;
            }

            int bytes = bits / 8;
            bool supported = (format == FormatPcm && (bits == 8 || bits == 16 || bits == 24 || bits == 32)) ||
                (format == FormatFloat && bits == 32);
            if (!supported) {
                error = $"unsupported format {format} ({bits} bits)";
                return false;
            }

            int count = dataLength / bytes;
            count -= count % channels;
            var samples = new float[count];
            int p = dataOffset;
            switch (format == FormatFloat ? 0 : bits) {
                case 0:
                    for (int i = 0; i < count; i++, p += 4) samples[i] = BitConverter.ToSingle(file, p);
                    break;
                case 8:
                    for (int i = 0; i < count; i++, p++) samples[i] = (file[p] - 128) / 128f;
                    break;
                case 16:
                    for (int i = 0; i < count; i++, p += 2) samples[i] = (short) (file[p] | file[p + 1] << 8) / 32768f;
                    break;
                case 24:
                    for (int i = 0; i < count; i++, p += 3) samples[i] = ((file[p] << 8 | file[p + 1] << 16 | file[p + 2] << 24) >> 8) / 8388608f;
                    break;
                default:
                    for (int i = 0; i < count; i++, p += 4) samples[i] = BitConverter.ToInt32(file, p) / 2147483648f;
                    break;
            }

            pcm = new RoPcm { Samples = samples, Channels = channels, SampleRate = sampleRate };
            return true;
        }

        static readonly int[] ImaIndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

        static readonly int[] ImaStepTable = {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107,
            118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963,
            1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894,
            6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794,
            32767
        };

        /// <summary>
        /// IMA (DVI) ADPCM WAV blocks: per channel a 4-byte header (first sample, step index), then 4-bit codes, low nibble
        /// first; stereo interleaves 4 bytes (8 codes) per channel.
        /// </summary>
        static float[] DecodeImaAdpcm(byte[] file, int offset, int length, int channels, int blockAlign) {
            int framesPerBlock = (blockAlign - 4 * channels) * 8 / (4 * channels) + 1;
            int blocks = length / blockAlign;
            var samples = new float[blocks * framesPerBlock * channels];
            var predictor = new int[channels];
            var index = new int[channels];
            for (int block = 0; block < blocks; block++) {
                int p = offset + block * blockAlign;
                int frame0 = block * framesPerBlock;
                for (int c = 0; c < channels; c++, p += 4) {
                    predictor[c] = (short) (file[p] | file[p + 1] << 8);
                    index[c] = Math.Min(88, Math.Max(0, (int) file[p + 2]));
                    samples[frame0 * channels + c] = predictor[c] / 32768f;
                }

                int end = offset + (block + 1) * blockAlign;
                int[] frameOf = new int[channels];
                for (int c = 0; c < channels; c++) frameOf[c] = 1;
                while (p + 4 * channels <= end) {
                    for (int c = 0; c < channels; c++) {
                        for (int k = 0; k < 4; k++, p++) {
                            int b = file[p];
                            for (int nibble = 0; nibble < 2; nibble++) {
                                int code = nibble == 0 ? b & 0xf : b >> 4;
                                int step = ImaStepTable[index[c]];
                                int diff = step >> 3;
                                if ((code & 4) != 0) diff += step;
                                if ((code & 2) != 0) diff += step >> 1;
                                if ((code & 1) != 0) diff += step >> 2;
                                predictor[c] = Math.Max(-32768, Math.Min(32767, predictor[c] + ((code & 8) != 0 ? -diff : diff)));
                                index[c] = Math.Min(88, Math.Max(0, index[c] + ImaIndexTable[code]));
                                int frame = frameOf[c]++;
                                if (frame < framesPerBlock) samples[(frame0 + frame) * channels + c] = predictor[c] / 32768f;
                            }
                        }
                    }
                }
            }

            return samples;
        }

        static bool Tag(byte[] b, int at, string tag) =>
            at + 4 <= b.Length && b[at] == tag[0] && b[at + 1] == tag[1] && b[at + 2] == tag[2] && b[at + 3] == tag[3];
    }
}
