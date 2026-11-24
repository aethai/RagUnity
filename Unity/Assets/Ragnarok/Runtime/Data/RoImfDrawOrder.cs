using System;
using System.Collections.Generic;
using Ragnarok.Assets;

namespace Ragnarok.Data {
    /// <summary>
    /// A job's <c>data\imf\&lt;body sprite&gt;.imf</c>: per ACT action and frame, which of the body (layer 0) and the head
    /// (layer 1) the client inserts first (reference packages/draw-order §1.3). 0x5e5bd0 asks FUN_006ca4c0 for the layer
    /// whose IMF priority is 1, then for the one with priority 0, so a frame with head = 1 / body = 0 draws the head under
    /// the body: a back bent over the head when picking an item up facing away, an arm swung across the hair. Nearly every
    /// file has such frames (pickup, attacks, hurt, casting); idle, walk and sit never do.
    /// Main thread only.
    /// </summary>
    public sealed class RoImfDrawOrder {
        private const int BodyLayer = 0;
        private const int HeadLayer = 1;

        /// <summary>Diagnostics A/B (<c>!toggle imf off</c>): off draws the body under the head in every frame.</summary>
        public static bool Enabled = true;

        private static readonly Dictionary<string, RoImfDrawOrder> Cache = new Dictionary<string, RoImfDrawOrder>(StringComparer.OrdinalIgnoreCase);

        // per action, the frames that draw the head first; null rows (and a null table) draw the body first
        private readonly bool[][] headFirst;

        private RoImfDrawOrder(bool[][] headFirst) {
            this.headFirst = headFirst;
        }

        /// <summary>Whether the client draws the head before the body at ACT action <paramref name="actionIndex"/> (action·8 + direction) frame <paramref name="frameIndex"/>.</summary>
        public bool IsHeadFirst(int actionIndex, int frameIndex) {
            if (!Enabled || (uint) actionIndex >= (uint) headFirst.Length) {
                return false;
            }

            bool[] frames = headFirst[actionIndex];
            return frames != null && (uint) frameIndex < (uint) frames.Length && frames[frameIndex];
        }

        /// <summary>The number of (action, frame) pairs that draw the head first (diagnostics, tests).</summary>
        public int HeadFirstFrameCount {
            get {
                int count = 0;
                foreach (bool[] frames in headFirst) {
                    if (frames == null) continue;
                    foreach (bool first in frames) {
                        if (first) count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// The table for a player body sprite (<c>data/sprite/인간족/몸통/여/위저드_여</c> reads <c>data/imf/위저드_여.imf</c>),
        /// cached per file; null when the job has no IMF or it has no head-first frame (the body is drawn first).
        /// </summary>
        public static RoImfDrawOrder ForBody(RoAssetDatabase assets, string bodyBasePath) {
            if (assets == null || string.IsNullOrEmpty(bodyBasePath)) {
                return null;
            }

            int slash = Math.Max(bodyBasePath.LastIndexOf('/'), bodyBasePath.LastIndexOf('\\'));
            string name = bodyBasePath.Substring(slash + 1);
            if (name.EndsWith(".spr", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".act", StringComparison.OrdinalIgnoreCase)) {
                name = name.Substring(0, name.Length - 4);
            }

            if (name.Length == 0) {
                return null;
            }

            if (Cache.TryGetValue(name, out RoImfDrawOrder cached)) {
                return cached;
            }

            RoImfDrawOrder table = Parse(assets.ReadBytesQuiet("data/imf/" + name + ".imf"));
            Cache[name] = table;
            return table;
        }

        /// <summary>
        /// Parses an IMF file: float version, int checksum, int max layer, then per layer 0..max an action count and per
        /// action a frame count and per frame (int priority, int cx, int cy). Null for missing or malformed data and for a
        /// file without a head-first frame.
        /// </summary>
        public static RoImfDrawOrder Parse(byte[] data) {
            if (data == null || data.Length < 12) {
                return null;
            }

            try {
                int position = 4 + 4; // version, checksum
                int maxLayer = ReadInt(data, ref position);
                if (maxLayer < HeadLayer || maxLayer > 64) {
                    return null;
                }

                int[][] bodyPriorities = null;
                int[][] headPriorities = null;
                for (int layer = 0; layer <= maxLayer && layer <= HeadLayer; layer++) {
                    int actionCount = ReadInt(data, ref position);
                    if (actionCount < 0 || actionCount > 4096) return null;
                    var actions = new int[actionCount][];
                    for (int action = 0; action < actionCount; action++) {
                        int frameCount = ReadInt(data, ref position);
                        if (frameCount < 0 || frameCount > 4096 || position + frameCount * 12 > data.Length) return null;
                        var priorities = new int[frameCount];
                        for (int frame = 0; frame < frameCount; frame++) {
                            priorities[frame] = ReadInt(data, ref position);
                            position += 8; // cx, cy
                        }

                        actions[action] = priorities;
                    }

                    if (layer == BodyLayer) bodyPriorities = actions; else headPriorities = actions;
                }

                if (bodyPriorities == null || headPriorities == null) {
                    return null;
                }

                bool any = false;
                var headFirst = new bool[bodyPriorities.Length][];
                for (int action = 0; action < bodyPriorities.Length && action < headPriorities.Length; action++) {
                    int[] body = bodyPriorities[action];
                    int[] head = headPriorities[action];
                    for (int frame = 0; frame < body.Length && frame < head.Length; frame++) {
                        // the client inserts the priority-1 layer first: head 1 / body 0 puts the head under the body
                        if (head[frame] > body[frame]) {
                            headFirst[action] ??= new bool[body.Length];
                            headFirst[action][frame] = true;
                            any = true;
                        }
                    }
                }

                return any ? new RoImfDrawOrder(headFirst) : null;
            } catch (ArgumentOutOfRangeException) {
                return null;
            }
        }

        private static int ReadInt(byte[] data, ref int position) {
            if (position + 4 > data.Length) {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            int value = BitConverter.ToInt32(data, position);
            position += 4;
            return value;
        }
    }
}
