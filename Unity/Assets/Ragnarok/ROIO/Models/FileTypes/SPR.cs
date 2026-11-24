using UnityEngine;

namespace ROIO.Models.FileTypes {
    public class SPR {

        public const int PIXELS_PER_UNIT = 32;
        public static string Header = "SP";

        public static int TYPE_PAL = 0;
        public static int TYPE_RGBA = 1;

        public string filename;
        public string version;
        public ushort indexedCount;
        public ushort _indexedCount;
        public int rgbaCount;
        public Frame[] frames;
        public ushort rgbaIndex;
        public ushort oldRgbaIndex;
        public byte[] palette;
        public bool compiled = false;

        public bool TryGetFrame(int frameIndex, out Frame frame) {
            if (frames == null || frameIndex < 0 || frameIndex >= frames.Length) {
                frame = null;
                return false;
            }

            frame = frames[frameIndex];
            return frame != null;
        }

        public class Frame {
            public bool compiled = false;

            public int type { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public byte[] data { get; set; }

            public int originalWidth { get; set; }
            public int originalHeight { get; set; }

            public void Compile(byte[] palette) {
                if (compiled) {
                    return;
                }

                // no power-of-two padding (a WebGL 1 need of roBrowser): every reader takes the image through
                // originalWidth / originalHeight (and a centring offset of 0 now), and the padding made big arrays
                // (large-object heap, GC) and bigger sprite atlases to upload
                var gl_width = width;
                var gl_height = height;
                var start_x = 0;
                var start_y = 0;
                byte[] _out;
                if (type == TYPE_PAL) {
                    _out = new byte[gl_width * gl_height];

                    for (int y = 0; y < height; y++) {
                        for (int x = 0; x < width; x++) {
                            _out[(y + start_y) * gl_width + x + start_x] = data[y * width + x];

                            if (palette[data[y * width + x] * 4] == 255
                                && palette[data[y * width + x] * 4 + 2] == 255
                                && palette[data[y * width + x] * 4 + 1] == 0) {
                                _out[(y + start_y) * gl_width + x + start_x] = 0;
                            }
                        }
                    }
                } else {
                    _out = new byte[gl_width * gl_height * 4];
                    byte[] src = data;

                    // rows flipped, ABGR -> RGBA; row offsets computed once per row
                    for (int y = 0; y < height; ++y) {
                        int s4 = (height - y - 1) * width * 4;
                        int d4 = ((y + start_y) * gl_width + start_x) * 4;
                        int end = d4 + width * 4;
                        for (; d4 < end; d4 += 4, s4 += 4) {
                            _out[d4] = src[s4 + 3];
                            _out[d4 + 1] = src[s4 + 2];
                            _out[d4 + 2] = src[s4 + 1];
                            _out[d4 + 3] = src[s4];
                        }
                    }
                }

                originalWidth = width;
                originalHeight = height;
                width = gl_width;
                height = gl_height;
                data = _out;
                compiled = true;
            }

            /// <summary>
            /// A palette frame straight to its compiled RGBA (the bytes <see cref="SPR.SwitchToRGBA(byte[])"/> followed
            /// by <see cref="Compile(byte[])"/> produce: rows flipped, R G B from the palette, alpha 0 for index 0).
            /// </summary>
            public void CompilePalette(byte[] palette) {
                if (compiled) {
                    return;
                }

                int w = width;
                int h = height;
                var _out = new byte[w * h * 4];
                byte[] indices = data;
                for (int y = 0; y < h; y++) {
                    int s = (h - y - 1) * w;
                    int d = y * w * 4;
                    for (int x = 0; x < w; x++, d += 4) {
                        int idx1 = indices[s + x] * 4;
                        _out[d] = palette[idx1];
                        _out[d + 1] = palette[idx1 + 1];
                        _out[d + 2] = palette[idx1 + 2];
                        _out[d + 3] = idx1 != 0 ? (byte) 255 : (byte) 0;
                    }
                }

                originalWidth = w;
                originalHeight = h;
                data = _out;
                type = TYPE_RGBA;
                compiled = true;
            }
        }

        public void SwitchToRGBA() {
            SwitchToRGBA(new byte[] { });
        }

        public void SwitchToRGBA(byte[] currentPalette) {
            if (currentPalette.Length > 0) {
                palette = currentPalette;
            }

            for (int i = 0; i < indexedCount; i++) {
                var frame = frames[i];

                if (frame.type == TYPE_PAL) {
                    int pixels = frame.width * frame.height;
                    var _out = new byte[pixels * 4];
                    byte[] indices = frame.data;
                    byte[] pal = palette;

                    // A B G R per pixel (alpha 0 for index 0), one index read per pixel
                    for (int p = 0, o = 0; p < pixels; p++, o += 4) {
                        int idx1 = indices[p] * 4;
                        _out[o + 3] = pal[idx1];
                        _out[o + 2] = pal[idx1 + 1];
                        _out[o + 1] = pal[idx1 + 2];
                        _out[o] = idx1 != 0 ? (byte) 255 : (byte) 0;
                    }

                    frame.data = _out;
                    frame.type = TYPE_RGBA;
                }
            }

            indexedCount = 0;
            rgbaCount = frames.Length;
            rgbaIndex = 0;
        }

        /// <summary>
        /// <see cref="SwitchToRGBA(byte[])"/> then <see cref="Compile()"/>, with one array per palette frame instead of two.
        /// </summary>
        public void CompileRgba(byte[] currentPalette) {
            if (compiled) {
                return;
            }

            if (currentPalette != null && currentPalette.Length > 0) {
                palette = currentPalette;
            }

            for (int i = 0; i < frames.Length; i++) {
                Frame frame = frames[i];
                if (frame.type == TYPE_PAL) {
                    frame.CompilePalette(palette);
                } else {
                    frame.Compile(palette);
                }
            }

            if (indexedCount > 0) {
                indexedCount = 0;
                rgbaCount = frames.Length;
                rgbaIndex = 0;
            }

            oldRgbaIndex = _indexedCount;
            compiled = true;
        }

        public void Compile() {
            if (!compiled) {
                for (int i = 0; i < frames.Length; i++) {
                    frames[i].Compile(palette);
                }

                oldRgbaIndex = _indexedCount;
                compiled = true;
            }
        }

        public Sprite[] GetSprites() {
            if (!compiled) {
                throw new System.Exception("SPR.GetAtlas: SPR is not compiled");
            }

            Sprite[] sprites = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++) {
                Frame frame = frames[i];
                Texture2D texture = new Texture2D(frame.width, frame.height, TextureFormat.RGBA32, false);
                texture.LoadRawTextureData(frame.data);
                texture.filterMode = FilterMode.Bilinear;
                texture.Apply();
                /**
                 * This anchor offset is a hack
                 */
                sprites[i] = Sprite.Create(texture, new Rect(0, 0, frame.width, frame.height), new Vector2(0.5f, 0.5f), PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect);
            }

            return sprites;
        }
    }
}
