using System;
using UnityEngine;

namespace ROIO.Models.FileTypes {
    /// <summary>
    /// .gat file representation
    /// 
    /// @author Guilherme Hernandez
    /// Based on ROBrowser by Vincent Thibault (robrowser.com)
    /// </summary>
    
    [Serializable]
    public class GAT {
        public static string Header = "GRAT";
        public const uint WATER_TYPE_FLAG = 0x80000000u;

        [SerializeField] public long width;
        [SerializeField] public long height;
        [SerializeField] public Cell[] cells;
        [SerializeField] public string version;

        /// <summary>
        /// Cell known type
        /// </summary>
        [Serializable]
        public enum TYPE : byte {
            NONE = 1 << 0,
            WALKABLE = 1 << 1,
            WATER = 1 << 2,
            SNIPABLE = 1 << 3
        }

        [Serializable]
        public struct Cell {
            [SerializeField] public Vector4 Heights;
            [SerializeField] public byte type;
        }

        /// <summary>
        /// Taken from *athena at src/map/map.c
        /// </summary>
        [SerializeField]
        public static byte[] TYPE_TABLE = {
        (byte) TYPE.WALKABLE | (byte) TYPE.SNIPABLE,                     // walkable ground
		(byte) TYPE.NONE,                                                // non-walkable ground
		(byte) TYPE.WALKABLE | (byte) TYPE.SNIPABLE,                     // ???
		(byte) TYPE.WALKABLE | (byte) TYPE.SNIPABLE | (byte) TYPE.WATER, // walkable water
		(byte) TYPE.WALKABLE | (byte) TYPE.SNIPABLE,                     // ???
		(byte) TYPE.SNIPABLE,                                            // gat (snipable)
		(byte) TYPE.WALKABLE | (byte) TYPE.SNIPABLE                      // ???
    };

        public static bool TryResolveRawType(uint rawType, out byte type) {
            if (rawType < TYPE_TABLE.Length) {
                type = TYPE_TABLE[(int) rawType];
                return true;
            }

            switch (rawType) {
                case 0xffffffffu: // NoGat (-1)
                    type = (byte) TYPE.NONE;
                    return true;
                case 0x80000000u:
                    type = (byte) (TYPE.WALKABLE | TYPE.SNIPABLE);
                    return true;
                case 0x80000001u:
                    type = (byte) TYPE.NONE;
                    return true;
                case 0x80000002u:
                    type = (byte) (TYPE.WALKABLE | TYPE.SNIPABLE);
                    return true;
                case 0x80000003u:
                    type = (byte) (TYPE.WALKABLE | TYPE.SNIPABLE | TYPE.WATER);
                    return true;
                case 0x80000004u:
                    type = (byte) TYPE.NONE;
                    return true;
                case 0x80000005u:
                    type = (byte) (TYPE.WALKABLE | TYPE.SNIPABLE);
                    return true;
                case 0x80000006u:
                    type = (byte) (TYPE.WALKABLE | TYPE.SNIPABLE);
                    return true;
                case 0x80000007u:
                case 0x80000008u:
                case 0x80000009u:
                    type = (byte) TYPE.NONE;
                    return true;
                default:
                    type = 0;
                    return false;
            }
        }

        public GAT(uint width, uint height, Cell[] cells, string version) {
            this.width = width;
            this.height = height;
            this.cells = cells;
            this.version = version;
        }

        public override string ToString() {
            return "GAT v" + version + "(" + width + "x" + height + ")";
        }
    }
}
