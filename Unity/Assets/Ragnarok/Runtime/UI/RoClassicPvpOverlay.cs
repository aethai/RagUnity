using System;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The PvP and battleground numbers drawn straight on the screen (pvp-bg-spec A2, A3, C3), from RoPvpState:
    /// <list type="bullet">
    /// <item>0xbf4f80: rank over total in RankFont digits at the bottom right — the rank's ones digit at (W − 105, H − 50)
    /// and 25 px left per digit, the separator (action 10) at (W − 80, H − 40), the total (≤ 9999) from W − 80 + 25·n
    /// leftwards at H − 30;</item>
    /// <item>0xbf5210: rank 1 of at least 2 — the time since in TimeFont at y 40, centred, 40 px a glyph (ss, mm:ss,
    /// hh:mm:ss, dd:hh:mm:ss; action 10 the colon), and the TimeAttack splash (action 0, a frame per 3 rendered frames, 51)
    /// at y 120 during the first 3 s — once per client run, as its counter is never reset;</item>
    /// <item>0x538780: the lion / eagle emblems (effect\사자(a).bmp, 독수리(b).bmp stretched to 48² at alpha 0xCD,
    /// (W − 280, 26) / (W − 280, 84)) with the scores in RankFont, 32 px a digit leftwards from W − 207 (both below 10)
    /// or W − 175, at y 50 / 108.</item>
    /// </list>
    /// One layer under every window, rebuilt only when what it shows changes.
    /// </summary>
    public sealed class RoClassicPvpOverlay : IRoClassicUiProducer {
        private const string RankFont = "data/sprite/이팩트/RankFont";
        private const string TimeFont = "data/sprite/이팩트/TimeFont";
        private const string TimeAttack = "data/sprite/이팩트/TimeAttack";
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Emblem = new Color32(255, 255, 255, 0xcd);

        private static RoClassicPvpOverlay instance;
        private static readonly long[] Parts = new long[4];
        private static int splashCounter;   // [0x153bab8]: counts up once per rendered frame while the splash shows, never reset

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            splashCounter = 0;
        }

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private long shownKey = long.MinValue;

        /// <summary>Per frame from the chat window's classic ticks: the overlay exists once the classic UI does.</summary>
        public static void Tick() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || (instance != null && instance.system == system)) return;
            instance = new RoClassicPvpOverlay { system = system };
            instance.layer = system.Renderer.CreateLayer("pvp overlay");
            instance.layer.AboveOverlayUi = true;
            system.Renderer.SendToBack(instance.layer);
            system.Register(instance);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            bool rank = RoPvpState.ShowRankOverlay, timer = RoPvpState.ShowRank1Timer, score = RoPvpState.ShowScoreOverlay;
            long now = RoMonotonicTime.NowMilliseconds;
            long since = RoPvpState.Rank1SinceMs;
            bool splash = timer && since != 0 && now - since < 3000;
            if (splash) splashCounter++;
            Vector2Int client = ui.ClientSize;
            long elapsedSeconds = timer && since != 0 ? Math.Max(0, (now - since) / 1000) : 0;
            // what is on the screen: rebuilt only when this changes (the splash every frame while it plays)
            long key = 17;
            unchecked {
                key = key * 31 + (rank ? RoPvpState.Rank * 10007L + RoPvpState.Total : -1);
                key = key * 31 + (timer ? elapsedSeconds : -1);
                key = key * 31 + (score ? RoPvpState.Lion * 10007L + RoPvpState.Eagle : -1);
                key = key * 31 + (splash ? splashCounter / 3 : -1);
                key = key * 31 + client.x * 4099L + client.y;
            }

            if (key == shownKey) return;
            shownKey = key;
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            int w = client.x, h = client.y;
            if (rank) DrawRank(q, w, h);
            if (timer) DrawTimer(q, w, elapsedSeconds, splash);
            if (score) DrawScore(q, w);
            layer.MarkDirty();
        }

        /// <summary>0xbf4f80: the rank's digits leftwards from (W − 105, H − 50), the separator, the total's from W − 80 + 25·n.</summary>
        private static void DrawRank(RoClassicUiQuads q, int w, int h) {
            RoClassicActSprite font = RoClassicActSprite.Get(RankFont);
            if (font == null || !font.Ready) return;
            int total = Math.Min(9999, RoPvpState.Total), rank = RoPvpState.Rank % 10000;
            int n = Digits(total);
            for (int i = 0, v = total; i < n; i++, v /= 10) font.Emit(q, v % 10, 0, w - 80 + (n - i) * 25, h - 30, 1f, White);
            font.Emit(q, 10, 0, w - 80, h - 40, 1f, White);
            int m = Digits(rank);
            for (int i = 0, v = rank; i < m; i++, v /= 10) font.Emit(q, v % 10, 0, w - 105 - i * 25, h - 50, 1f, White);
        }

        /// <summary>0xbf5210: ss / mm:ss / hh:mm:ss / dd:hh:mm:ss centred at y 40 (a colon every third glyph), and the splash.</summary>
        private static void DrawTimer(RoClassicUiQuads q, int w, long seconds, bool splash) {
            RoClassicActSprite font = RoClassicActSprite.Get(TimeFont);
            if (font != null && font.Ready) {
                long s = seconds % 60, m = seconds / 60 % 60, hh = seconds / 3600 % 24, d = seconds / 86400;
                int count = d > 0 ? 4 : hh > 0 ? 3 : m > 0 ? 2 : 1;
                Parts[0] = d;
                Parts[1] = hh;
                Parts[2] = m;
                Parts[3] = s;
                int n = count * 3 - 1;
                int k = 1;
                for (int p = 4 - count; p < 4; p++) {
                    long part = Parts[p];
                    if (k > 1) {
                        font.Emit(q, 10, 0, w / 2 - 20 * n + 40 * (k - 1), 40, 1f, White);
                        k++;
                    }

                    font.Emit(q, (int) (part / 10 % 10), 0, w / 2 - 20 * n + 40 * (k - 1), 40, 1f, White);
                    k++;
                    font.Emit(q, (int) (part % 10), 0, w / 2 - 20 * n + 40 * (k - 1), 40, 1f, White);
                    k++;
                }
            }

            if (!splash) return;
            RoClassicActSprite attack = RoClassicActSprite.Get(TimeAttack);
            if (attack == null || !attack.Ready) return;
            int frame = splashCounter / 3;
            if (frame >= attack.FrameCount(0)) return;
            attack.Emit(q, 0, frame, w / 2, 120, 1f, White);
        }

        /// <summary>0x538780: the emblems and the two scores.</summary>
        private void DrawScore(RoClassicUiQuads q, int w) {
            RoClassicUiImages images = system.Images;
            if (images.TryGet("effect\\사자(a).bmp", out RoClassicUiImage lion)) q.Image(w - 280, 26, 48, 48, lion, Emblem);
            if (images.TryGet("effect\\독수리(b).bmp", out RoClassicUiImage eagle)) q.Image(w - 280, 84, 48, 48, eagle, Emblem);
            RoClassicActSprite font = RoClassicActSprite.Get(RankFont);
            if (font == null || !font.Ready) return;
            int ones = RoPvpState.Lion < 10 && RoPvpState.Eagle < 10 ? w - 207 : w - 175;
            int a = RoPvpState.Lion, b = RoPvpState.Eagle;
            for (int i = 0, n = Digits(a); i < n; i++, a /= 10) font.Emit(q, a % 10, 0, ones - 32 * i, 50, 1f, White);
            for (int i = 0, n = Digits(b); i < n; i++, b /= 10) font.Emit(q, b % 10, 0, ones - 32 * i, 108, 1f, White);
        }

        private static int Digits(int v) {
            int n = 1;
            for (v = Math.Abs(v); v >= 10; v /= 10) n++;
            return n;
        }
    }
}
