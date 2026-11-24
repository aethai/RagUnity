using ROIO.Models.FileTypes;
using ROIO.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROIO.Loaders {
    public class GroundLoader {
        private const int GndTileByteSize = 40;
        private const int GndSurfaceByteSize = 28;
        private const int SupportedLightmapGridCellCount = 64;
        private const int LightmapBytesPerCell = 256;

        public static GND Load(MemoryStreamReader data) {
            string header = RoLoaderValidation.ReadBinaryString(data, 4, "GroundLoader.Load header");

            if (!string.Equals(header, GND.Header)) {
                throw new Exception("GroundLoader.Load: Header (" + header + ") is not \"GRGN\"");
            }

            byte majorVersion = RoLoaderValidation.ReadByte(data, "GroundLoader.Load version major");
            byte minorVersion = RoLoaderValidation.ReadByte(data, "GroundLoader.Load version minor");
            string version = majorVersion + "." + minorVersion;

            GND gnd = new GND(version);
            gnd.width = RoLoaderValidation.ReadUInt(data, $"GroundLoader.Load GND {version} width");
            gnd.height = RoLoaderValidation.ReadUInt(data, $"GroundLoader.Load GND {version} height");
            RoLoaderValidation.RequireMapElementCount(gnd.width, gnd.height, $"GroundLoader.Load GND {version}");
            gnd.zoom = RoLoaderValidation.ReadFloat(data, $"GroundLoader.Load GND {version} zoom");

            ParseTextures(gnd, data);
            ParseLightmaps(gnd, data);

            gnd.tiles = ParseTiles(gnd, data);
            gnd.surfaces = ParseSurfaces(gnd, data);
            ParseWaterSettings(gnd, data, majorVersion, minorVersion);

            return gnd;
        }

        public static bool TryLoad(MemoryStreamReader data, out GND gnd, out string error) {
            try {
                gnd = Load(data);
                error = string.Empty;
                return true;
            } catch (Exception ex) {
                gnd = null;
                error = ex.Message;
                return false;
            }
        }

        private static void ParseTextures(GND gnd, MemoryStreamReader data) {
            int textureCount = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUInt(data, "GroundLoader.ParseTextures texture count"), RoLoaderValidation.MaxCollectionCount, "texture", "GroundLoader.ParseTextures");
            int texturePathLength = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUInt(data, "GroundLoader.ParseTextures texture path length"), RoLoaderValidation.MaxTexturePathLength, "texture path length", "GroundLoader.ParseTextures");
            RoLoaderValidation.RequireRemaining(data, checked((long) textureCount * texturePathLength), "GroundLoader.ParseTextures texture name table");
            int[] lookupList = new int[textureCount];
            List<string> textures = new List<string>();

            for (int i = 0; i < textureCount; i++) {
                string texture = data.ReadBinaryString(texturePathLength);
                int pos = textures.IndexOf(texture);

                if (pos == -1) {
                    textures.Add(texture);
                    pos = textures.Count - 1;
                }

                lookupList[i] = pos;
            }

            gnd.textures = textures.ToArray();
            gnd.textureLookupList = lookupList;
        }

        private static void ParseLightmaps(GND gnd, MemoryStreamReader data) {
            int lightmapCount = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUInt(data, "GroundLoader.ParseLightmaps lightmap count"), RoLoaderValidation.MaxCollectionCount, "lightmap", "GroundLoader.ParseLightmaps");
            int lightmapWidth = RoLoaderValidation.ReadInt(data, "GroundLoader.ParseLightmaps lightmap width");
            int lightmapHeight = RoLoaderValidation.ReadInt(data, "GroundLoader.ParseLightmaps lightmap height");
            int gridSizeCell = RoLoaderValidation.ReadInt(data, "GroundLoader.ParseLightmaps grid cell size");
            if (lightmapWidth <= 0 || lightmapHeight <= 0 || gridSizeCell <= 0) {
                throw new Exception($"GroundLoader.ParseLightmaps: Invalid lightmap metadata {lightmapWidth}x{lightmapHeight}x{gridSizeCell}; values must be positive.");
            }

            int perCell = checked(lightmapWidth * lightmapHeight * gridSizeCell);

            if (perCell != SupportedLightmapGridCellCount) {
                throw new Exception($"GroundLoader.ParseLightmaps: Unsupported lightmap cell size {perCell}; expected {SupportedLightmapGridCellCount}.");
            }

            RoLoaderValidation.RequireRemaining(data, checked((long) lightmapCount * LightmapBytesPerCell), "GroundLoader.ParseLightmaps lightmap data");

            var lightmap = gnd.lightmap = new GND.Lightmap();
            lightmap.count = (uint) lightmapCount;
            lightmap.perCell = perCell;
            lightmap.data = new byte[lightmapCount][];

            for (int i = 0; i < lightmapCount; i++) {
                lightmap.data[i] = data.ReadBytes(LightmapBytesPerCell);
            }
        }

        private static GND.Tile[] ParseTiles(GND gnd, MemoryStreamReader data) {
            int count = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUInt(data, "GroundLoader.ParseTiles tile count"), RoLoaderValidation.MaxCollectionCount, "tile", "GroundLoader.ParseTiles");
            RoLoaderValidation.RequireRemaining(data, checked((long) count * GndTileByteSize), "GroundLoader.ParseTiles tile data");
            GND.Tile[] tiles = new GND.Tile[count];

            var ATLAS_COLS = Math.Round(Math.Sqrt(gnd.textures.Length));
            var ATLAS_ROWS = Math.Ceiling(Math.Sqrt(gnd.textures.Length));
            var ATLAS_WIDTH = Math.Pow(2, Math.Ceiling(Math.Log(ATLAS_COLS * 258) / Math.Log(2)));
            var ATLAS_HEIGHT = Math.Pow(2, Math.Ceiling(Math.Log(ATLAS_ROWS * 258) / Math.Log(2)));
            var ATLAS_FACTOR_U = ATLAS_COLS * 258 / ATLAS_WIDTH;
            var ATLAS_FACTOR_V = ATLAS_ROWS * 258 / ATLAS_HEIGHT;
            var ATLAS_PX_U = 1 / 258f;
            var ATLAS_PX_V = 1 / 258f;

            for (int i = 0; i < count; i++) {
                var tile = tiles[i] = new GND.Tile();
                tile.textureStart = new Vector4(data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
                tile.textureEnd = new Vector4(data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
                tile.texture = data.ReadUShort();
                tile.light = data.ReadUShort();
                var r = (byte) data.ReadByte();
                var g = (byte) data.ReadByte();
                var b = (byte) data.ReadByte();
                var a = (byte) data.ReadByte();
                tile.color = new byte[] { r, g, b, a };
                if (tile.texture >= gnd.textureLookupList.Length) {
                    throw new Exception($"GroundLoader.ParseTiles: Tile {i} references texture index {tile.texture}, but only {gnd.textureLookupList.Length} texture slot(s) exist.");
                }

                if (gnd.lightmap != null && gnd.lightmap.data != null && gnd.lightmap.data.Length > 0 && tile.light >= gnd.lightmap.data.Length) {
                    throw new Exception($"GroundLoader.ParseTiles: Tile {i} references lightmap index {tile.light}, but only {gnd.lightmap.data.Length} lightmap slot(s) exist.");
                }

                tile.texture = (ushort) gnd.textureLookupList[tile.texture];

                var start = tile.texture % ATLAS_COLS;
                var end = Math.Floor(tile.texture / ATLAS_COLS);

                for (int j = 0; j < 4; j++) {
                    tile.textureStart[j] = (float) ((start + tile.textureStart[j] * (1 - ATLAS_PX_U * 2) + ATLAS_PX_U) * ATLAS_FACTOR_U / ATLAS_COLS);
                    tile.textureEnd[j] = (float) ((end + tile.textureEnd[j] * (1 - ATLAS_PX_V * 2) + ATLAS_PX_V) * ATLAS_FACTOR_V / ATLAS_ROWS);
                }
            }

            return tiles;
        }

        private static GND.Surface[] ParseSurfaces(GND gnd, MemoryStreamReader data) {
            var count = RoLoaderValidation.RequireMapElementCount(gnd.width, gnd.height, "GroundLoader.ParseSurfaces");
            RoLoaderValidation.RequireRemaining(data, checked((long) count * GndSurfaceByteSize), "GroundLoader.ParseSurfaces surface data");
            GND.Surface[] surfaces = new GND.Surface[count];

            for (int i = 0; i < count; i++) {
                var surface = surfaces[i] = new GND.Surface();
                surface.height = new Vector4(data.ReadFloat() / 5, data.ReadFloat() / 5, data.ReadFloat() / 5, data.ReadFloat() / 5);
                surface.tileUp = data.ReadInt();
                surface.tileFront = data.ReadInt();
                surface.tileRight = data.ReadInt();
                ValidateSurfaceTileReference(surface.tileUp, "tileUp", i, gnd.tiles.Length);
                ValidateSurfaceTileReference(surface.tileFront, "tileFront", i, gnd.tiles.Length);
                ValidateSurfaceTileReference(surface.tileRight, "tileRight", i, gnd.tiles.Length);
            }

            return surfaces;
        }

        private static void ParseWaterSettings(GND gnd, MemoryStreamReader data, int majorVersion, int minorVersion) {
            if (!IsVersionAtLeast(majorVersion, minorVersion, 1, 8)) {
                return;
            }

            RoLoaderValidation.RequireRemaining(data, 32, $"GroundLoader.ParseWaterSettings GND {majorVersion}.{minorVersion}");

            float waterLevel = RoLoaderValidation.ReadFloat(data, "GroundLoader.ParseWaterSettings water level") / 5f;
            int waterType = RoLoaderValidation.ReadInt(data, "GroundLoader.ParseWaterSettings water type");
            float waveHeight = RoLoaderValidation.ReadFloat(data, "GroundLoader.ParseWaterSettings wave height") / 5f;
            float waveSpeed = RoLoaderValidation.ReadFloat(data, "GroundLoader.ParseWaterSettings wave speed");
            float wavePitch = RoLoaderValidation.ReadFloat(data, "GroundLoader.ParseWaterSettings wave pitch");
            int textureCyclingInterval = RoLoaderValidation.ReadInt(data, "GroundLoader.ParseWaterSettings animation speed");
            int splitWidth = RoLoaderValidation.ReadInt(data, "GroundLoader.ParseWaterSettings split width");
            int splitHeight = RoLoaderValidation.ReadInt(data, "GroundLoader.ParseWaterSettings split height");

            if (IsVersionAtLeast(majorVersion, minorVersion, 1, 9)) {
                if (splitWidth < 0 || splitHeight < 0) {
                    throw new Exception($"GroundLoader.ParseWaterSettings: Invalid water split dimensions {splitWidth}x{splitHeight}.");
                }

                long zoneCount = checked((long) splitWidth * splitHeight);
                long zoneBytes = checked(zoneCount * 24L);
                RoLoaderValidation.RequireRemaining(data, zoneBytes, "GroundLoader.ParseWaterSettings zone data");
                data.Position += zoneBytes;
            }

            gnd.water = new GND.WaterInfo(
                waterLevel,
                waterType,
                waveHeight,
                waveSpeed,
                wavePitch,
                textureCyclingInterval,
                splitWidth,
                splitHeight);
        }

        private static bool IsVersionAtLeast(int majorVersion, int minorVersion, int requiredMajor, int requiredMinor) {
            return majorVersion > requiredMajor ||
                   (majorVersion == requiredMajor && minorVersion >= requiredMinor);
        }

        private static void ValidateSurfaceTileReference(long tileIndex, string name, int surfaceIndex, int tileCount) {
            if (tileIndex < -1 || tileIndex >= tileCount) {
                throw new Exception($"GroundLoader.ParseSurfaces: Surface {surfaceIndex} {name} references tile index {tileIndex}, but valid values are -1 or 0..{tileCount - 1}.");
            }
        }

        private static Vector3[][] GetSmoothNormal(GND gnd) {
            Vector3 a = new Vector3();
            Vector3 b = new Vector3();
            Vector3 c = new Vector3();
            Vector3 d = new Vector3();
            var count = gnd.width * gnd.height;
            var tmp = new Vector3?[count];
            var normals = new Vector3[count][];
            var emptyVec = new Vector3();

            //calculate normal for each cell
            for (int y = 0; y < gnd.height; y++) {
                for (int x = 0; x < gnd.width; x++) {
                    var cell = gnd.surfaces[x + y * gnd.width];

                    if (cell.tileUp > -1) {
                        a[0] = (x + 0) * 2;
                        a[1] = cell.height[0];
                        a[2] = (y + 0) * 2;
                        b[0] = (x + 1) * 2;
                        b[1] = cell.height[1];
                        b[2] = (y + 0) * 2;
                        c[0] = (x + 1) * 2;
                        c[1] = cell.height[3];
                        c[2] = (y + 1) * 2;
                        d[0] = (x + 0) * 2;
                        d[1] = cell.height[2];
                        d[2] = (y + 1) * 2;

                        tmp[x + y * gnd.width] = Conversions.CalcNormal(a, b, c, d);
                    }
                }
            }

            //smooth normals
            for (int y = 0; y < gnd.height; y++) {
                for (int x = 0; x < gnd.width; x++) {
                    var n = normals[x + y * gnd.width] = new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };

                    for (byte i = 0; i < 4; i++) {
                        int b1 = i & 1;
                        int b2 = (i & 2) >> 1;
                        int xor = b1 ^ b2;
                        n[i] += Conversions.safeArrayAccess(tmp, x + y * gnd.width, emptyVec) ?? emptyVec;
                        n[i] += Conversions.safeArrayAccess(tmp, x + 1 * -xor + y * gnd.width, emptyVec) ?? emptyVec;
                        n[i] += Conversions.safeArrayAccess(tmp, x + 1 * -xor + (y + 1 * -b2) * gnd.width, emptyVec) ?? emptyVec;
                        n[i] += Conversions.safeArrayAccess(tmp, x + (y + 1 * -b2) * gnd.width, emptyVec) ?? emptyVec;
                        n[i].Normalize();
                    }
                }
            }

            return normals;
        }

        private static RoImage CreateLightmapImage(GND gnd) {
            int lightmapCount = Math.Max(1, (int) gnd.lightmap.count);
            int atlasColumns = Math.Max(1, (int) Math.Round(Math.Sqrt(lightmapCount)));
            int atlasRows = Math.Max(1, (int) Math.Ceiling(Math.Sqrt(lightmapCount)));
            int atlasWidth = NextPowerOfTwo(atlasColumns * 8);
            int atlasHeight = NextPowerOfTwo(atlasRows * 8);
            byte[] image = new byte[atlasWidth * atlasHeight * 4];

            for (int lightmapIndex = 0; lightmapIndex < (int) gnd.lightmap.count; lightmapIndex++) {
                int atlasX = lightmapIndex % atlasColumns * 8;
                int atlasY = lightmapIndex / atlasColumns * 8;
                byte[] lightmap = gnd.lightmap.data[lightmapIndex];

                for (int y = 0; y < 8; y++) {
                    for (int x = 0; x < 8; x++) {
                        int sourcePixel = x + y * 8;
                        int target = ((atlasX + x) + (atlasY + y) * atlasWidth) * 4;
                        image[target] = (byte) (lightmap[64 + sourcePixel * 3] >> 4 << 4);
                        image[target + 1] = (byte) (lightmap[64 + sourcePixel * 3 + 1] >> 4 << 4);
                        image[target + 2] = (byte) (lightmap[64 + sourcePixel * 3 + 2] >> 4 << 4);
                        image[target + 3] = lightmap[sourcePixel];
                    }
                }
            }

            return new RoImage {
                width = atlasWidth,
                height = atlasHeight,
                format = TextureFormat.RGBA32,
                mipChain = false,
                data = image
            };
        }

        private static RoImage CreateTilesColorImage(GND gnd) {
            byte[] data = new byte[gnd.width * gnd.height * 4];

            for (int y = 0; y < gnd.height; y++) {
                for (int x = 0; x < gnd.width; x++) {
                    var cell = gnd.surfaces[x + y * gnd.width];

                    // Check tile up
                    if (cell.tileUp > -1) {
                        var color = gnd.tiles[cell.tileUp].color;
                        color.CopyTo(data, (x + y * gnd.width) * 4);
                    }
                }
            }

            return new RoImage {
                width = (int) gnd.width,
                height = (int) gnd.height,
                format = TextureFormat.RGBA32,
                mipChain = false,
                data = data
            };
        }

        private static byte[] CreateShadowmapData(GND gnd) {
            int width = checked((int) gnd.width);
            int height = checked((int) gnd.height);
            int shadowWidth = checked(width * 8);
            byte[] output = new byte[checked(shadowWidth * height * 8)];

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    GND.Surface cell = gnd.surfaces[x + y * width];
                    byte[] lightmap = null;

                    if (cell.tileUp > -1 &&
                        cell.tileUp < gnd.tiles.Length) {
                        int tileIndex = (int) cell.tileUp;
                        ushort lightmapIndex = gnd.tiles[tileIndex].light;
                        if (gnd.lightmap?.data != null && lightmapIndex < gnd.lightmap.data.Length) {
                            lightmap = gnd.lightmap.data[lightmapIndex];
                        }
                    }

                    for (int pixelY = 0; pixelY < 8; pixelY++) {
                        for (int pixelX = 0; pixelX < 8; pixelX++) {
                            int target = x * 8 + pixelX + (y * 8 + pixelY) * shadowWidth;
                            output[target] = lightmap != null && lightmap.Length > pixelX + pixelY * 8
                                ? lightmap[pixelX + pixelY * 8]
                                : byte.MaxValue;
                        }
                    }
                }
            }

            return output;
        }

        public static GND.Mesh Compile(GND gnd, float WATER_LEVEL, float WATER_HEIGHT) {
            var normals = GetSmoothNormal(gnd);

            var meshData = new List<float>();
            var waterMeshData = new List<float>();

            for (int y = 0; y < gnd.height; y++) {
                for (int x = 0; x < gnd.width; x++) {

                    var cellA = gnd.surfaces[x + y * gnd.width];
                    var h_a = cellA.height;

                    // Check tile up
                    if (cellA.tileUp > -1) {
                        var tile = gnd.tiles[cellA.tileUp];

                        // Check if has texture
                        var n = normals[x + y * gnd.width];
                        var lightUv = GetLightmapAtlasUv(gnd, tile.light);

                        meshData.AddRange(new float[] {
                        (x + 0) * 2, h_a[0], (y + 0) * 2, n[0][0], n[0][1], n[0][1], tile.textureStart[0], tile.textureEnd[0], lightUv.U1, lightUv.V1, (x + 0.5f) / gnd.width, (y + 0.5f) / gnd.height,
                        (x + 1) * 2, h_a[1], (y + 0) * 2, n[1][0], n[1][1], n[1][1], tile.textureStart[1], tile.textureEnd[1], lightUv.U2, lightUv.V1, (x + 1.5f) / gnd.width, (y + 0.5f) / gnd.height,
                        (x + 1) * 2, h_a[3], (y + 1) * 2, n[2][0], n[2][1], n[2][1], tile.textureStart[3], tile.textureEnd[3], lightUv.U2, lightUv.V2, (x + 1.5f) / gnd.width, (y + 1.5f) / gnd.height,
                        (x + 0) * 2, h_a[2], (y + 1) * 2, n[3][0], n[3][1], n[3][1], tile.textureStart[2], tile.textureEnd[2], lightUv.U1, lightUv.V2, (x + 0.5f) / gnd.width, (y + 1.5f) / gnd.height,
                    });

                        // Add water only if it's upper than the ground.
                        if (h_a[0] > WATER_LEVEL - WATER_HEIGHT ||
                            h_a[1] > WATER_LEVEL - WATER_HEIGHT ||
                            h_a[2] > WATER_LEVEL - WATER_HEIGHT ||
                            h_a[3] > WATER_LEVEL - WATER_HEIGHT) {

                            float o = 5f;
                            var texx = (x + 1) % o / o;
                            if (texx == 0) {
                                texx = 1;
                            }
                            var texy = (y + 1) % o / o;
                            if (texy == 0) {
                                texy = 1;
                            }
                            waterMeshData.AddRange(new float[] {
                            //        vec3 pos            |            vec2 texcoords
                            (x + 0) * 2, WATER_LEVEL, (y + 0) * 2, (x + 0) % o / o, (y + 0) % o / o,
                            (x + 1) * 2, WATER_LEVEL, (y + 0) * 2, texx, (y + 0) % o / o,
                            (x + 1) * 2, WATER_LEVEL, (y + 1) * 2, texx, texy,
                            //(x + 1) * 2, WATER_LEVEL, (y + 1) * 2, texx, texy,
                            (x + 0) * 2, WATER_LEVEL, (y + 1) * 2, (x + 0) % o / o, texy,
                            //(x + 0) * 2, WATER_LEVEL, (y + 0) * 2, ((x + 0) % 5 / 5), ((y + 0) % 5 / 5)
                        });
                        }
                    }

                    // Check tile front
                    if (cellA.tileFront > -1 && y + 1 < gnd.height) {
                        var tile = gnd.tiles[cellA.tileFront];
                        var lightUv = GetLightmapAtlasUv(gnd, tile.light);

                        var cellB = gnd.surfaces[x + (y + 1) * gnd.width];
                        var h_b = cellB.height;

                        meshData.AddRange(new float[] {
                        //      vec3 pos           |  vec3 normals     |    vec2 texcoords      |   vec2 lightcoord  |   vec2 tileCoords
                        (x + 0) * 2, h_b[0], (y + 1) * 2, 0.0f, 0.0f, 1.0f, tile.textureStart[2], tile.textureEnd[2], lightUv.U1, lightUv.V2, 0, 0,
                        (x + 0) * 2, h_a[2], (y + 1) * 2, 0.0f, 0.0f, 1.0f, tile.textureStart[0], tile.textureEnd[0], lightUv.U1, lightUv.V1, 0, 0,
                        (x + 1) * 2, h_a[3], (y + 1) * 2, 0.0f, 0.0f, 1.0f, tile.textureStart[1], tile.textureEnd[1], lightUv.U2, lightUv.V1, 0, 0,
                        (x + 1) * 2, h_b[1], (y + 1) * 2, 0.0f, 0.0f, 1.0f, tile.textureStart[3], tile.textureEnd[3], lightUv.U2, lightUv.V2, 0, 0,

                    });
                    }

                    // Check tile right
                    if (cellA.tileRight > -1 && x + 1 < gnd.width) {
                        var tile = gnd.tiles[cellA.tileRight];
                        var lightUv = GetLightmapAtlasUv(gnd, tile.light);

                        var cellB = gnd.surfaces[x + 1 + y * gnd.width];
                        var h_b = cellB.height;

                        meshData.AddRange(new float[] {
                        //      vec3 pos           |  vec3 normals    |    vec2 texcoords      |   vec2 lightcoord   |    vec2 tileCoords
                        (x + 1) * 2, h_a[1], (y + 0) * 2, 1, 0, 0, tile.textureStart[1], tile.textureEnd[1], lightUv.U2, lightUv.V1, 0, 0,
                        (x + 1) * 2, h_a[3], (y + 1) * 2, 1, 0, 0, tile.textureStart[0], tile.textureEnd[0], lightUv.U1, lightUv.V1, 0, 0,
                        (x + 1) * 2, h_b[0], (y + 0) * 2, 1, 0, 0, tile.textureStart[3], tile.textureEnd[3], lightUv.U2, lightUv.V2, 0, 0,
                        //(x + 1) * 2, h_b[0], (y + 0) * 2, 1.0f, 0.0f, 0.0f, tile.textureStart[3], tile.textureEnd[3], lu2, lv2, 0, 0,
                        (x + 1) * 2, h_b[2], (y + 1) * 2, 1, 0, 0, tile.textureStart[2], tile.textureEnd[2], lightUv.U1, lightUv.V2, 0, 0,
                        //(x + 1) * 2, h_a[3], (y + 1) * 2, 1.0f, 0.0f, 0.0f, tile.textureStart[0], tile.textureEnd[0], lu1, lv1, 0, 0
                    });
                    }
                }
            }

            var mesh = new GND.Mesh();

            mesh.width = gnd.width;
            mesh.height = gnd.height;
            mesh.textures = gnd.textures;

            mesh.lightmap = CreateLightmapImage(gnd);
            mesh.tileColor = CreateTilesColorImage(gnd);
            mesh.shadowMap = CreateShadowmapData(gnd);

            mesh.mesh = meshData.ToArray();
            mesh.meshVertCount = meshData.Count / 12;

            mesh.waterMesh = waterMeshData.ToArray();
            mesh.waterVertCount = waterMeshData.Count / 5;

            return mesh;
        }

        private static LightmapAtlasUv GetLightmapAtlasUv(GND gnd, int lightmapIndex) {
            int lightmapCount = Math.Max(1, (int) gnd.lightmap.count);
            int atlasColumns = Math.Max(1, (int) Math.Round(Math.Sqrt(lightmapCount)));
            int atlasRows = Math.Max(1, (int) Math.Ceiling(Math.Sqrt(lightmapCount)));
            int atlasWidth = NextPowerOfTwo(atlasColumns * 8);
            int atlasHeight = NextPowerOfTwo(atlasRows * 8);
            int atlasX = lightmapIndex % atlasColumns * 8;
            int atlasY = lightmapIndex / atlasColumns * 8;

            return new LightmapAtlasUv(
                (atlasX + 1f) / atlasWidth,
                (atlasX + 7f) / atlasWidth,
                (atlasY + 1f) / atlasHeight,
                (atlasY + 7f) / atlasHeight
            );
        }

        private static int NextPowerOfTwo(int value) {
            int result = 1;
            while (result < value) {
                result <<= 1;
            }

            return result;
        }

        private readonly struct LightmapAtlasUv {
            public LightmapAtlasUv(float u1, float u2, float v1, float v2) {
                U1 = u1;
                U2 = u2;
                V1 = v1;
                V2 = v2;
            }

            public float U1 { get; }
            public float U2 { get; }
            public float V1 { get; }
            public float V2 { get; }
        }
    }
}
