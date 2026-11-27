using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    using Lua51Exception = Ragnarok.Data.Lua51.LuaException;
    using Lua51State = Ragnarok.Data.Lua51.LuaState;
    using Lua51Table = Ragnarok.Data.Lua51.LuaTable;
    using Lua51Value = Ragnarok.Data.Lua51.LuaValue;

    /// <summary>
    /// Turns a precompiled Lua 5.1 table file (the official client ships its lub tables as bytecode) into Lua source the
    /// text table readers already parse: the chunk runs in the Lua 5.1 VM and every global table it defines is written
    /// back as a table constructor. Strings keep their raw bytes (the VM maps each byte to one char), so the result
    /// decodes exactly like a plain-text lub of the same data.
    ///
    /// Table files refer to each other's id tables (jobname indexes JTtbl from jobidentity, accname indexes
    /// ACCESSORY_IDs...), so all files of one asset database run in one shared state that first runs the id tables.
    /// </summary>
    public static class RoLuaBytecodeSource {
        private const long InstructionLimit = 50_000_000;
        private const int MaxDepth = 24;

        /// <summary>Id tables other table files index into, run before any other file (missing ones are skipped).</summary>
        private static readonly string[] DependencyNames = {
            "datainfo/jobidentity",
            "datainfo/npcidentity",
            "datainfo/accessoryid",
            "datainfo/spriterobeid",
            "datainfo/weapontable",
            "skillinfoz/jobinheritlist",
            "skillinfoz/skillid",
            "stateicon/efstids",
        };

        private static readonly string[] LuaRoots = {
            "data/luafiles514/lua files/",
            "data/lua files/",
        };

        private sealed class SharedState {
            public readonly object Gate = new object();
            public Lua51State State;
        }

        private static readonly ConditionalWeakTable<RoAssetDatabase, SharedState> States =
            new ConditionalWeakTable<RoAssetDatabase, SharedState>();

        public static bool IsBytecode(byte[] bytes) {
            return bytes != null && bytes.Length >= 4 && bytes[0] == 0x1B && bytes[1] == (byte) 'L' && bytes[2] == (byte) 'u' && bytes[3] == (byte) 'a';
        }

        /// <summary>
        /// The Lua source equivalent of a precompiled table file, or null (with the reason in <paramref name="error"/>)
        /// when the chunk cannot run or defines no table.
        /// </summary>
        public static byte[] ToSource(RoAssetDatabase assets, string path, byte[] bytecode, out string error) {
            error = null;
            if (!IsBytecode(bytecode)) {
                error = "not Lua 5.1 bytecode";
                return null;
            }

            SharedState shared = assets != null ? States.GetValue(assets, _ => new SharedState()) : new SharedState();
            lock (shared.Gate) {
                try {
                    Lua51State state = shared.State;
                    if (state == null) {
                        state = new Lua51State { InstructionLimit = InstructionLimit };
                        RunDependencies(assets, state);
                        shared.State = state;
                    }

                    var before = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (KeyValuePair<Lua51Value, Lua51Value> global in state.Globals) {
                        if (global.Key.IsString) {
                            before[global.Key.AsString] = global.Value.IsTable ? (object) global.Value.AsTable : null;
                        }
                    }

                    state.DoChunk(bytecode, path);
                    return WriteDefinedTables(state, before, out error);
                } catch (Exception ex) {
                    error = "compiled Lua could not run: " + ex.Message;
                    return null;
                }
            }
        }

        private static void RunDependencies(RoAssetDatabase assets, Lua51State state) {
            if (assets == null) {
                return;
            }

            foreach (string name in DependencyNames) {
                foreach (string root in LuaRoots) {
                    string candidate = root + name + ".lub";
                    if (!assets.Exists(candidate)) {
                        continue;
                    }

                    byte[] bytes = assets.ReadBytesQuiet(candidate);
                    if (!IsBytecode(bytes)) {
                        continue;
                    }

                    try {
                        state.DoChunk(bytes, candidate);
                    } catch (Lua51Exception) {
                        // a broken id table only costs the names that index into it
                    }

                    break;
                }
            }
        }

        private static byte[] WriteDefinedTables(Lua51State state, Dictionary<string, object> before, out string error) {
            error = null;
            var names = new List<string>();
            foreach (KeyValuePair<Lua51Value, Lua51Value> global in state.Globals) {
                if (!global.Key.IsString || !global.Value.IsTable || global.Key.AsString == "_G") {
                    continue;
                }

                string name = global.Key.AsString;
                if (before.TryGetValue(name, out object previous) && ReferenceEquals(previous, global.Value.AsTable)) {
                    continue;
                }

                names.Add(name);
            }

            if (names.Count == 0) {
                error = "compiled Lua defined no table";
                return null;
            }

            names.Sort(StringComparer.Ordinal);
            var text = new StringBuilder(4096);
            var visiting = new HashSet<Lua51Table>();
            foreach (string name in names) {
                text.Append(name).Append(" = ");
                WriteTable(text, state.GetGlobal(name).AsTable, 0, visiting);
                text.Append('\n');
            }

            var bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++) {
                bytes[i] = unchecked((byte) text[i]);
            }

            return bytes;
        }

        private static void WriteTable(StringBuilder text, Lua51Table table, int depth, HashSet<Lua51Table> visiting) {
            if (depth >= MaxDepth || !visiting.Add(table)) {
                text.Append("{}");
                return;
            }

            var entries = new List<KeyValuePair<Lua51Value, Lua51Value>>();
            foreach (KeyValuePair<Lua51Value, Lua51Value> entry in table) {
                if (IsWritable(entry.Value) && (entry.Key.IsNumber || entry.Key.IsString)) {
                    entries.Add(entry);
                }
            }

            entries.Sort(CompareKeys);
            string indent = new string('\t', depth + 1);
            text.Append("{\n");
            foreach (KeyValuePair<Lua51Value, Lua51Value> entry in entries) {
                text.Append(indent);
                WriteKey(text, entry.Key);
                text.Append(" = ");
                if (entry.Value.IsTable) {
                    WriteTable(text, entry.Value.AsTable, depth + 1, visiting);
                } else {
                    WriteScalar(text, entry.Value);
                }

                text.Append(",\n");
            }

            text.Append(new string('\t', depth)).Append('}');
            visiting.Remove(table);
        }

        private static bool IsWritable(Lua51Value value) {
            return value.IsNumber || value.IsString || value.IsBoolean || value.IsTable;
        }

        private static int CompareKeys(KeyValuePair<Lua51Value, Lua51Value> a, KeyValuePair<Lua51Value, Lua51Value> b) {
            if (a.Key.IsNumber != b.Key.IsNumber) {
                return a.Key.IsNumber ? -1 : 1;
            }

            return a.Key.IsNumber
                ? a.Key.Number.CompareTo(b.Key.Number)
                : string.CompareOrdinal(a.Key.AsString, b.Key.AsString);
        }

        private static void WriteKey(StringBuilder text, Lua51Value key) {
            if (key.IsNumber) {
                text.Append('[').Append(FormatNumber(key.Number)).Append(']');
            } else if (IsIdentifier(key.AsString)) {
                text.Append(key.AsString);
            } else {
                text.Append('[');
                WriteString(text, key.AsString);
                text.Append(']');
            }
        }

        private static void WriteScalar(StringBuilder text, Lua51Value value) {
            if (value.IsNumber) {
                text.Append(FormatNumber(value.Number));
            } else if (value.IsBoolean) {
                text.Append(value.IsTruthy ? "true" : "false");
            } else {
                WriteString(text, value.AsString);
            }
        }

        private static string FormatNumber(double number) {
            if (Math.Abs(number) < 1e15 && Math.Floor(number) == number) {
                return ((long) number).ToString(CultureInfo.InvariantCulture);
            }

            return number.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder text, string value) {
            text.Append('"');
            foreach (char c in value) {
                switch (c) {
                    case '\\': text.Append("\\\\"); break;
                    case '"': text.Append("\\\""); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    case '\0': text.Append("\\0"); break;
                    default: text.Append(c); break;
                }
            }

            text.Append('"');
        }

        private static bool IsIdentifier(string value) {
            if (string.IsNullOrEmpty(value) || !(char.IsLetter(value[0]) || value[0] == '_') || value[0] > 0x7f) {
                return false;
            }

            for (int i = 1; i < value.Length; i++) {
                char c = value[i];
                if (c > 0x7f || !(char.IsLetterOrDigit(c) || c == '_')) {
                    return false;
                }
            }

            return !LuaKeywords.Contains(value);
        }

        private static readonly HashSet<string> LuaKeywords = new HashSet<string>(StringComparer.Ordinal) {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if", "in", "local", "nil", "not", "or",
            "repeat", "return", "then", "true", "until", "while",
        };
    }
}
