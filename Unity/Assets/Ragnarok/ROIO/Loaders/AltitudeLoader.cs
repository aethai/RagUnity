using ROIO.Models.FileTypes;
using ROIO.Utils;
using System;
using UnityEngine;

namespace ROIO.Loaders
{
    /// <summary>
    /// Loader for Gravity .gat file (Ground Altitude)
    /// 
    /// @author Guilherme Hernandez
    /// Based on ROBrowser by Vincent Thibault (robrowser.com)
    /// </summary>
    public class AltitudeLoader
    {

        /// <summary>
        /// Load a GAT file
        /// </summary>
        /// <param name="data">GAT file data</param>
        public static GAT Load(MemoryStreamReader data)
        {
            string header = data.ReadBinaryString(4);

            //check for valid gat file
            if (!string.Equals(header, GAT.Header))
            {
                throw new Exception("AltitudeLoader.Load: Header (" + header + ") is not \"GRAT\"");
            }

            //load parameters
            RoFileVersion fileVersion = RoFileVersion.ReadMajorMinor(data);
            string version = fileVersion.Text;
            uint width = data.ReadUInt();
            uint height = data.ReadUInt();
            int cellCount = RoLoaderValidation.RequireMapElementCount(width, height, $"AltitudeLoader.Load GAT {version}");
            RoLoaderValidation.RequireRemaining(data, (long) cellCount * 20, $"AltitudeLoader.Load GAT {version} cells");
            GAT.Cell[] cells = new GAT.Cell[cellCount];

            //load the cells
            for (int i = 0; i < cellCount; i++)
            {
                Vector4 heights = new Vector4();
                heights[0] = data.ReadFloat() * 0.2f;         // height 1
                heights[1] = data.ReadFloat() * 0.2f;         // height 2
                heights[2] = data.ReadFloat() * 0.2f;         // height 3
                heights[3] = data.ReadFloat() * 0.2f;         // height 4
                cells[i].Heights = heights;
                uint rawType = data.ReadUInt();
                if (!GAT.TryResolveRawType(rawType, out byte cellType)) {
                    throw new Exception($"AltitudeLoader.Load: Unsupported terrain type raw {rawType} at cell {i}/{cellCount} in GAT {version}");
                }

                cells[i].type = cellType;
            }

            //exports
            return new GAT(width, height, cells, version);
        }

        public static bool TryLoad(MemoryStreamReader data, out GAT gat, out string error) {
            try {
                gat = Load(data);
                error = string.Empty;
                return true;
            } catch (Exception ex) {
                gat = null;
                error = ex.Message;
                return false;
            }
        }
    }
}
