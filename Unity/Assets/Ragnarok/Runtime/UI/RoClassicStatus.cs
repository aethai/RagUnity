using System;
using System.Globalization;
using System.Threading.Tasks;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The status window, the original client's UIStatusWnd (MakeWindow 0xb, the menu icon / Alt+A). The frame, labels
    /// and controls come from the oracle's layout (Resources/Ragnarok/Generated/OriginalClient/ui/UIStatusWnd.json); the
    /// numbers are drawn by a port of the data part of its OnDraw (0x850410). The data is the session's status
    /// (ZC_PAR_CHANGE / ZC_STATUS ...), refreshed on its change sequence while the window is open; the "+" buttons send
    /// CZ_STATUS_CHANGE.
    /// </summary>
    public sealed class RoClassicStatus {
        private const string LayoutPath = "Ragnarok/Generated/OriginalClient/ui/UIStatusWnd";
        private const int CloseButtonId = 197;
        private const int MinimizeButtonId = 198;
        private const int TitleBarHeight = 17;
        private const int FirstStatButtonId = 201;
        private const int BaseStatCap = 99;

        /// <summary>
        /// Ragnarok has no 4th jobs: the window ends under the guild row (the dark line at y 135 of w_ex_statwin_bg.bmp is its
        /// bottom edge), without the trait stats (Pow ... Crt, P.Atk ... T.Status Point) of the 2024 window (user, 10-06).
        /// </summary>
        private const int BaseSectionHeight = 136;

        /// <summary>The six arw_right "+" buttons (ids 201–206), UIStatusWnd members +0x94..+0xa8.</summary>
        private static readonly string[] StatButtonOwners = { "+0x94", "+0x98", "+0x9c", "+0xa0", "+0xa4", "+0xa8" };

        private static readonly RoStatusStat[] StatOrder = {
            RoStatusStat.Str, RoStatusStat.Agi, RoStatusStat.Vit, RoStatusStat.Int, RoStatusStat.Dex, RoStatusStat.Luk,
        };

        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private static RoClassicStatus instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicWindow window;
        private bool layoutMissing;
        private int seenSequence = int.MinValue;
        private readonly int[] stat = new int[6];
        private readonly int[] bonus = new int[6];
        private readonly int[] need = new int[6];
        private int atk, atk2, matkMin, matkMax, hit, crit, def, def2, mdef, mdef2, flee, flee2, aspd, points;
        private bool minimized;
        private string guild = string.Empty;

        public static bool IsOpen => instance?.window != null && instance.window.Visible;

        /// <summary>Per frame (RoClassicGameUi): the open window follows the session's status.</summary>
        public static void Tick(RoLoginShellSession loginSession) {
            RoClassicStatus s = instance;
            if (s == null || loginSession == null) return;
            s.session = loginSession;
            if (s.window == null || !s.window.Visible) return;
            int sequence = loginSession.StatusWindowChangeSequence;
            if (sequence == s.seenSequence) return;
            s.seenSequence = sequence;
            s.Apply();
        }

        /// <summary>The menu icon / Alt+A: opens or closes the window.</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            if (IsOpen) {
                instance.window.Visible = false;
                return;
            }

            Open(loginSession);
        }

        public static void Open(RoLoginShellSession loginSession) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginSession == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicStatus { system = system };
            RoClassicStatus s = instance;
            s.session = loginSession;
            if (!s.EnsureWindow()) return;
            s.seenSequence = loginSession.StatusWindowChangeSequence;
            s.Apply();
            s.window.Visible = true;
            system.Windows.BringToFront(s.window);
        }

        /// <summary>The blinking "new" status icon: a base stat can be raised with the points in hand.</summary>
        public static bool HasAffordableBaseStatIncrease(RoPlayerStatusSnapshot status) {
            if (status == null || !status.HasKnownStatusPoints || status.StatusPoints <= 0) return false;
            int p = status.StatusPoints;
            return CanAfford(status.Str, status.NeedStr, p) || CanAfford(status.Agi, status.NeedAgi, p) ||
                CanAfford(status.Vit, status.NeedVit, p) || CanAfford(status.Int, status.NeedInt, p) ||
                CanAfford(status.Dex, status.NeedDex, p) || CanAfford(status.Luk, status.NeedLuk, p);
        }

        /// <summary>The raise cost before ZC_STATUS arrives: (stat − 1) / 10 + 2.</summary>
        public static int CalculateStatPointCost(int statValue) {
            if (statValue <= 0) return 0;
            return ((Mathf.Max(1, statValue) - 1) / 10) + 2;
        }

        private static bool CanAfford(int value, int serverRequirement, int available) {
            int statValue = value > 0 ? value : 1;
            if (statValue >= BaseStatCap) return false;
            int requirement = serverRequirement > 0 ? serverRequirement : CalculateStatPointCost(statValue);
            return requirement > 0 && requirement <= available;
        }

        private static int ResolveStat(int statusValue, bool hasCharacter, byte characterValue) {
            if (statusValue > 0) return statusValue;
            return hasCharacter && characterValue > 0 ? characterValue : 0;
        }

        private void Apply() {
            RoShellBasicInfoSnapshot basicInfo = session.BasicInfoSnapshot();
            RoPlayerStatusSnapshot status = basicInfo.Status ?? RoPlayerStatusSnapshot.Empty;
            bool hasCharacter = basicInfo.HasCharacter;
            RoLoginShellCharacter character = basicInfo.Character;
            SetStat(0, ResolveStat(status.Str, hasCharacter, character.Strength), status.StrBonus, status.NeedStr);
            SetStat(1, ResolveStat(status.Agi, hasCharacter, character.Agility), status.AgiBonus, status.NeedAgi);
            SetStat(2, ResolveStat(status.Vit, hasCharacter, character.Vitality), status.VitBonus, status.NeedVit);
            SetStat(3, ResolveStat(status.Int, hasCharacter, character.Intelligence), status.IntBonus, status.NeedInt);
            SetStat(4, ResolveStat(status.Dex, hasCharacter, character.Dexterity), status.DexBonus, status.NeedDex);
            SetStat(5, ResolveStat(status.Luk, hasCharacter, character.Luck), status.LukBonus, status.NeedLuk);
            atk = status.Attack;
            atk2 = status.RefiningPower;
            matkMin = status.MagicAttackMin;
            matkMax = status.MagicAttackMax;
            hit = status.Hit;
            crit = status.Critical;
            def = status.Defense;
            def2 = status.PlusDefense;
            mdef = status.MagicDefense;
            mdef2 = status.PlusMagicDefense;
            flee = status.Flee;
            flee2 = status.PlusFlee;
            aspd = status.AttackSpeed;
            points = status.StatusPoints;

            // SendMsg(0x23) 0x865ef0: a stat's "+" is in place when its cost is non-zero and affordable, parked at
            // (-100, -100) otherwise; the client has no max-stat check (the server sends cost 0 at the cap)
            for (int i = 0; i < 6; i++) {
                bool canRaise = status.HasKnownStatusPoints && need[i] != 0 && need[i] <= status.StatusPoints;
                window.SetChildPosition(StatButtonOwners[i], canRaise ? 78 : -100, canRaise ? 24 + 16 * i : -100);
            }

            window.Invalidate();
        }

        private void SetStat(int index, int value, int statBonus, int serverNeed) {
            stat[index] = value;
            bonus[index] = statBonus;
            // the server's cost; the formula only stands in until ZC_STATUS has arrived
            need[index] = serverNeed > 0 ? serverNeed : CalculateStatPointCost(value);
        }

        /// <summary>
        /// UIStatusWnd::OnDraw 0x850410, data part: the six stats at (36, 23 + 16·i), their bonus when non-zero (+n, or
        /// −n as is) at x 49 — or at x 54, 2 px lower and in height 12 once the stat reaches 100 — the raise cost
        /// right-aligned at 101, then the right-aligned derived values (ASPD shown as (2000 − aspd) / 10) and the
        /// guild name in fontType 0.
        /// </summary>
        private void Draw(RoClassicWindow w) {
            for (int i = 0; i < 6; i++) {
                int y = 0x17 + 16 * i;
                w.TextOut(0x24, y, Format(stat[i]), 1, 0xe, Black);
                int b = bonus[i];
                if (b != 0) {
                    string text = b > 0 ? "+" + Format(b) : Format(b);
                    if (stat[i] < 100) w.TextOut(0x31, y, text, 1, 0xe, Black);
                    else w.TextOut(0x36, y + 2, text, 1, 0xc, Black);
                }

                w.TextOutRight(0x65, y, Format(need[i]), 1, 0xe, Black);
            }

            w.TextOutRight(0xbd, 0x16, Format(atk) + " + " + Format(atk2), 1, 0xe, Black);
            w.TextOutRight(0xbd, 0x26, Format(matkMin) + " ~ " + Format(matkMax), 1, 0xe, Black);
            w.TextOutRight(0xbd, 0x36, Format(hit), 1, 0xe, Black);
            w.TextOutRight(0xbd, 0x46, Format(crit), 1, 0xe, Black);
            w.TextOutRight(0x113, 0x16, Format(def) + " + " + Format(def2), 1, 0xe, Black);
            w.TextOutRight(0x113, 0x26, Format(mdef) + " + " + Format(mdef2), 1, 0xe, Black);
            w.TextOutRight(0x113, 0x36, Format(flee) + " + " + Format(flee2), 1, 0xe, Black);
            w.TextOutRight(0x113, 0x46, Format((2000 - aspd) / 10), 1, 0xe, Black);
            w.TextOutRight(0x113, 0x56, Format(points), 1, 0xe, Black);
            if (!string.IsNullOrEmpty(guild)) w.TextOutRight(0x113, 0x66, guild, 0, 0xc, Black);
        }

        private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

        private void OnClicked(RoClassicChildLayout child) {
            if (child.Id == CloseButtonId) {
                window.Visible = false;
                return;
            }

            if (child.Id == MinimizeButtonId) {
                // minimized: only the 17 px title bar is drawn; restoring brings back the base section
                minimized = !minimized;
                window.SetHeight(minimized ? TitleBarHeight : BaseSectionHeight);
                return;
            }

            // ids 201-206: CZ_STATUS_CHANGE only while the cost is affordable (0x865ef0)
            int index = child.Id - FirstStatButtonId;
            if (index >= 0 && index < StatOrder.Length && need[index] != 0 && need[index] <= points) {
                _ = AllocateStatAsync(StatOrder[index]);
            }
        }

        private async Task AllocateStatAsync(RoStatusStat value) {
            try {
                await session.BeginLegacyAllocateStat(value, 1);
            } catch (Exception e) {
                Debug.LogWarning("Status-up request was not sent: " + e.Message);
            }
        }

        private bool EnsureWindow() {
            if (window != null) return true;
            if (layoutMissing) return false;
            var asset = Resources.Load<TextAsset>(LayoutPath);
            if (asset == null) {
                layoutMissing = true;
                Debug.LogWarning("[ClassicUi] UIStatusWnd layout not found at Resources/" + LayoutPath);
                return false;
            }

            window = new RoClassicWindow(system, RoClassicWindowLayout.Parse(asset.text)) { SuppressBoundOps = true };
            window.SetHeight(BaseSectionHeight);
            window.DrawDynamic += Draw;
            window.Clicked += OnClicked;
            return true;
        }

        /// <summary>Leaving the map (character select): the window goes.</summary>
        public static void Close() {
            if (instance?.window != null) instance.window.Visible = false;
        }
    }
}
