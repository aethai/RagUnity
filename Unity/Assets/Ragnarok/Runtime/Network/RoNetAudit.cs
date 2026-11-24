using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ragnarok.Network {
    /// <summary>
    /// Diagnostics for a packet-version switch: every map packet the drain accumulator sees, the ids its switch does not
    /// handle and the parse failures (the sweep's !netaudit). Written from the receive path, read from the console.
    /// </summary>
    public static class RoNetAudit {
        private static readonly object Sync = new object();
        private static readonly Dictionary<ushort, (string Name, int Count, int Length)> received = new Dictionary<ushort, (string, int, int)>();
        private static readonly HashSet<ushort> unhandled = new HashSet<ushort>();
        private static readonly List<string> failures = new List<string>();

        /// <summary>Off until the console's !netaudit: the receive path then costs a branch per packet.</summary>
        public static volatile bool Enabled;

        public static void Received(RoPacketFrame frame) {
            if (!Enabled) return;
            lock (Sync) {
                received.TryGetValue(frame.PacketId, out (string Name, int Count, int Length) e);
                received[frame.PacketId] = (frame.Definition.Name ?? string.Empty, e.Count + 1, frame.Length);
            }
        }

        public static void Unhandled(ushort packetId) {
            if (!Enabled) return;
            lock (Sync) unhandled.Add(packetId);
        }

        public static void Failed(string message) {
            if (!Enabled) return;
            lock (Sync) {
                if (failures.Count < 64) failures.Add(message);
            }
        }

        public static void Reset() {
            lock (Sync) {
                received.Clear();
                unhandled.Clear();
                failures.Clear();
            }
        }

        public static string Describe() {
            var sb = new StringBuilder();
            lock (Sync) {
                sb.Append("received ids=").Append(received.Count).Append(" unhandled:");
                foreach (ushort id in unhandled) {
                    received.TryGetValue(id, out (string Name, int Count, int Length) e);
                    sb.Append(" 0x").Append(id.ToString("x4", CultureInfo.InvariantCulture)).Append(' ').Append(e.Name).Append(" x").Append(e.Count).Append(" (").Append(e.Length).Append("B)");
                }

                sb.Append(" | failures=").Append(failures.Count);
                foreach (string f in failures) sb.Append(" || ").Append(f);
            }

            return sb.ToString();
        }
    }
}
