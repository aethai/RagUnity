using ROIO.Loaders;
using ROIO.Models;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROIO {
    public static class Tables {

        public static Dictionary<string, MapTableStruct> MapTable = new Dictionary<string, MapTableStruct>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> ResNameTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> MsgStringTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // C-2: Clear stale static state on every domain reload so Play Mode
        // always starts from a known empty state (Init() fills them when a map loads).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnDomainReload() {
            MapTable.Clear();
            ResNameTable.Clear();
            MsgStringTable.Clear();
        }

        public static void Init() {
            MapTable.Clear();
            ResNameTable.Clear();
            MsgStringTable.Clear();

            InitMsgStringTable();
            InitMp3NameTable();
            InitMapTable();
            InitResNameTable();

            // Phase 7/12 — card display tables (not needed until inventory/item UI):
            //LoadTable("data/num2cardillustnametable.txt", 2);
            //LoadTable("data/cardprefixnametable.txt", 2);
            // fogparametertable is handled independently by RoMapFog — do not load here.
        }

        private static void InitResNameTable() {
            var data = TryLoadTextTable("data/resnametable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 2)) {
                ResNameTable[Convert.ToString(args[1])] = Convert.ToString(args[2]);
            }
        }

        private static void InitMapTable() {
            var data = TryLoadTextTable("data/mapnametable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 2)) {
                var key = Convert.ToString(args[1]);
                if (!MapTable.ContainsKey(key)) {
                    MapTable.Add(key, new MapTableStruct());
                }

                MapTableStruct mts = MapTable[key];
                mts.name = Convert.ToString(args[2]);
                MapTable[key] = mts;
            }
        }

        private static void InitMp3NameTable() {
            var data = TryLoadTextTable("data/mp3nametable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 2)) {
                var key = Convert.ToString(args[1]);
                if (!MapTable.ContainsKey(key)) {
                    MapTable.Add(key, new MapTableStruct());
                }

                MapTableStruct mts = MapTable[key];
                mts.mp3 = System.IO.Path.GetFileName(args[2].ToString());
                MapTable[key] = mts;
            }
        }

        private static void InitMsgStringTable() {
            var data = TryLoadTextTable("data/msgstringtable.txt");
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            foreach (object[] args in TableLoader.LoadTable(data, 1)) {
                MsgStringTable[Convert.ToString(args[0])] = Convert.ToString(args[1]);
            }
        }

        private static string TryLoadTextTable(string path) {
            try {
                return FileManager.Load(path) as string;
            } catch (Exception ex) {
                Debug.LogWarning($"Could not load RO text table '{path}': {ex.Message}");
                return null;
            }
        }
    }
}
