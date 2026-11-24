using System;
using System.Globalization;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>The homunculus as the client keeps it (DAT_01552eb0..01552f44).</summary>
    public readonly struct RoClassicHomunState {
        public static readonly RoClassicHomunState None = default;

        private RoClassicHomunState(bool hasProperty, uint gid, string name, int flags, int level, int hunger, int intimacy, short[] stats, int hp, int maxHp,
            int sp, int maxSp, long exp, long expNext, int skillPoints) {
            HasProperty = hasProperty;
            Gid = gid;
            Name = name ?? string.Empty;
            Flags = flags;
            Level = level;
            Hunger = hunger;
            Intimacy = intimacy;
            Stats = stats ?? new short[8];
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            Exp = exp;
            ExpNext = expNext;
            SkillPoints = skillPoints;
        }

        public bool HasProperty { get; }
        public uint Gid { get; }
        public string Name { get; }

        /// <summary>ZC_PROPERTY_HOMUN flags: bit 0 renamed, 1 vaporized, 2 dead.</summary>
        public int Flags { get; }

        public int Level { get; }
        public int Hunger { get; }
        public int Intimacy { get; }

        /// <summary>atk, matk, hit, crit, def, mdef, flee, amotion (the stat column of 0x8171f0).</summary>
        public short[] Stats { get; }

        public int Hp { get; }
        public int MaxHp { get; }
        public int Sp { get; }
        public int MaxSp { get; }
        public long Exp { get; }
        public long ExpNext { get; }
        public int SkillPoints { get; }

        /// <summary>The keyboard dispatcher's DAT_01552f44: a summoned homunculus (state 0 seen, not vaporized).</summary>
        public bool Active => Gid != 0 && HasProperty && (Flags & 2) == 0;

        public bool Renamed => (Flags & 1) != 0;

        /// <summary>From ZC_PROPERTY_HOMUN with its flags byte (renamed / vaporized / alive).</summary>
        public static RoClassicHomunState From(RoZcHomunculusProperty p, uint gid, int flags) =>
            new RoClassicHomunState(true, gid, p.Name, flags, p.Level, p.Hunger, p.Intimacy,
                new[] { p.Attack, p.MagicAttack, p.Hit, p.Critical, p.Defense, p.MagicDefense, p.Flee, p.AttackSpeed },
                p.Hp, p.MaxHp, (ushort) p.Sp, (ushort) p.MaxSp, (uint) p.Exp, (uint) p.MaxExp, p.SkillPoints);

        private RoClassicHomunState With(uint? gid = null, string name = null, int? flags = null, int? hunger = null, int? intimacy = null,
            int? hp = null, int? maxHp = null, int? sp = null, int? maxSp = null, long? exp = null, long? next = null) =>
            new RoClassicHomunState(HasProperty, gid ?? Gid, name ?? Name, flags ?? Flags, Level, hunger ?? Hunger, intimacy ?? Intimacy, Stats,
                hp ?? Hp, maxHp ?? MaxHp, sp ?? Sp, maxSp ?? MaxSp, exp ?? Exp, next ?? ExpNext, SkillPoints);

        public RoClassicHomunState WithName(string name) => With(name: name, flags: Flags | 1);

        /// <summary>ZC_CHANGESTATE_MER (0xc47c70): state 0 the actor, 1 intimacy, 2 hunger.</summary>
        public RoClassicHomunState Apply(RoZcCompanionChangeState change) {
            switch (change.State) {
                case 0: return With(gid: change.ActorId);
                case 1: return With(intimacy: change.Data);
                case 2: return With(hunger: change.Data);
                default: return this;
            }
        }

        /// <summary>ZC_HO_PAR_CHANGE: SP_BASEEXP 1, SP_HP 5, SP_MAXHP 6, SP_SP 7, SP_MAXSP 8, SP_NEXTBASEEXP 22.</summary>
        public RoClassicHomunState Apply(RoZcCompanionParameterChange change) {
            switch (change.Parameter) {
                case 1: return With(exp: change.Value);
                case 5: return With(hp: change.Value);
                case 6: return With(maxHp: change.Value);
                case 7: return With(sp: change.Value);
                case 8: return With(maxSp: change.Value);
                case 22: return With(next: change.Value);
                default: return this;
            }
        }
    }

    /// <summary>The mercenary as the client keeps it (DAT_01552f60..01552fe8).</summary>
    public readonly struct RoClassicMercState {
        public static readonly RoClassicMercState None = default;

        private RoClassicMercState(uint gid, string name, int level, short[] stats, int hp, int maxHp, int sp, int maxSp, int expire, int faith, int calls,
            int kills) {
            Gid = gid;
            Name = name ?? string.Empty;
            Level = level;
            Stats = stats ?? new short[8];
            Hp = hp;
            MaxHp = maxHp;
            Sp = sp;
            MaxSp = maxSp;
            Expire = expire;
            Faith = faith;
            Calls = calls;
            Kills = kills;
        }

        public uint Gid { get; }
        public string Name { get; }
        public int Level { get; }
        public short[] Stats { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Sp { get; }
        public int MaxSp { get; }

        /// <summary>The contract's end, seconds since 1970 (shown through _localtime32 with MsgStr 0x4ec).</summary>
        public int Expire { get; }

        public int Faith { get; }
        public int Calls { get; }
        public int Kills { get; }
        public bool Active => Gid != 0;

        public static RoClassicMercState From(RoZcMercenaryInfo m) =>
            new RoClassicMercState(m.ActorId, m.Name, m.Level,
                new[] { m.Attack, m.MagicAttack, m.Hit, m.Critical, m.Defense, m.MagicDefense, m.Flee, m.AttackSpeed },
                m.Hp, m.MaxHp, m.Sp, m.MaxSp, m.ExpireDate, m.Faith, m.CallCount, m.KillCount);

        /// <summary>ZC_MER_PAR_CHANGE (clif_mercenary_updatestatus): the SP_* parameter ids the server sends.</summary>
        public RoClassicMercState Apply(RoZcCompanionParameterChange change) {
            // a parameter can come before ZC_MER_INIT (map entry): nothing to update yet
            if (!Active || Stats == null) return this;
            short[] s = (short[]) Stats.Clone();
            int hp = Hp, maxHp = MaxHp, sp = Sp, maxSp = MaxSp, faith = Faith, kills = Kills;
            short v = (short) Math.Max(short.MinValue, Math.Min(short.MaxValue, change.Value));
            switch (change.Parameter) {
                case 41: s[0] = v; break; // SP_ATK1
                case 43: s[1] = v; break; // SP_MATK1
                case 49: s[2] = v; break; // SP_HIT
                case 52: s[3] = v; break; // SP_CRITICAL
                case 45: s[4] = v; break; // SP_DEF1
                case 47: s[5] = v; break; // SP_MDEF1
                case 165: s[6] = v; break; // SP_MERCFLEE
                case 53: s[7] = v; break; // SP_ASPD (amotion)
                case 5: hp = change.Value; break;
                case 6: maxHp = change.Value; break;
                case 7: sp = change.Value; break;
                case 8: maxSp = change.Value; break;
                case 189: kills = change.Value; break; // SP_MERCKILLS
                case 190: faith = change.Value; break; // SP_MERCFAITH
                default: return this;
            }

            return new RoClassicMercState(Gid, Name, Level, s, hp, maxHp, sp, maxSp, Expire, faith, Calls, kills);
        }
    }

    /// <summary>
    /// The companion information windows (codex-ui-research RUNTIME-RULES-COMPANIONS-MAIL.md "homunculus-mercenary"):
    /// UIHomunInfoWnd 0x71 (Alt+R) and UIMerInfoWnd 0x7d (Ctrl+R), both on homuninfo_bg.bmp at their saved place (first
    /// (150, 100), HOMUNINFOWNDINFO / MERINFOWNDINFO). The stat column (atk, matk, hit, crit, def, mdef, flee and
    /// (2000 − amotion) / 10) is right-aligned on x 79 from y 24 in 18 px steps (Arial 14), the level at (135, 47).
    /// Homunculus (OnDraw 0x8171f0): title 0x3a8, EXP / Hunger / Intimacy labels, HP / SP UIBarGraphHomun (117, 73 / 88,
    /// 157, 9), EXP UIINT64BarGraph (101, 119, 102, 6) with "%lld" right on 205, hunger UIBarGraph4 (101, 145, 102, 6) with
    /// "%d / 100" right on 200, the intimacy band at (150, 159) (0xc47c70's thresholds &lt;4, ..10, ..100, ..250, ..750, ..910,
    /// ..1000), the auto feeding box (210, 142) with 0xccf at (225, 136) and the red 0xcd4 footer at (8, 180). Buttons
    /// (vf37 0x81cd10): Feed 0x259 → CZ_COMMAND_MER 1, Delete 0x3bb → 2, Rename 0xba1 → CZ_RENAME_MER, the box → CZ_CONFIG 3.
    /// Mercenary (OnDraw 0x817c20): the class title 0x49d / 0x49e / 0x49f / 0x557, the name at (135, 24) over a #646464
    /// shadow, HP / SP UIBarGraphPlayer with "%d / %d" at (218, 70 / 85), the expiration (0x4ec) in red at (155, 111),
    /// loyalty (170, 133), summons (170, 157), kills (245, 157); Fire 0x55a → CZ_MER_COMMAND 2.
    /// </summary>
    public sealed class RoClassicCompanion : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const int CloseId = 0xc5;
        private const int RenameId = 0xb4;
        private const int HomunSkillId = 0x11d, HomunFeedId = 0x11e, HomunDeleteId = 0x11f, AutoFeedId = 0xd1;
        private const int MercSkillId = 0x159, MercFireId = 0x15a;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 Gray = new Color32(0x64, 0x64, 0x64, 255);
        private static readonly Color32 EditFill = new Color32(0xe8, 0xe8, 0xe8, 255);

        private static RoClassicCompanion instance;

        /// <summary>The session the windows were last ticked with (the companion skill lists open through it).</summary>
        internal static RoLoginShellSession CurrentSession => instance?.session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoClassicWindow homunWindow, mercWindow;
        private RoClassicEditField homunName;
        private RoClassicHomunState homun;
        private RoClassicMercState merc;
        private int seenHomun = -1, seenMerc = -1;
        private bool autoFeed;

        public static void Tick(RoLoginShellSession session, RoClassicChat chat) {
            if (session == null) return;
            RoClassicCompanion c = Ensure();
            if (c == null) return;
            c.session = session;
            c.chat = chat;
            (RoClassicHomunState h, int hs) = session.HomunSnapshot();
            if (hs != c.seenHomun) {
                c.seenHomun = hs;
                c.homun = h;
                if (!h.Active && c.homunWindow != null) c.homunWindow.Visible = false;
                if (c.homunName != null && !ReferenceEquals(RoClassicTextInput.Focused, c.homunName) && c.homunName.Text != h.Name) c.homunName.SetText(h.Name);
                c.homunWindow?.Invalidate();
            }

            (RoClassicMercState m, int ms) = session.MercSnapshot();
            if (ms != c.seenMerc) {
                c.seenMerc = ms;
                c.merc = m;
                if (!m.Active && c.mercWindow != null) c.mercWindow.Visible = false;
                c.SyncMercBars();
                c.mercWindow?.Invalidate();
            }
        }

        private static RoClassicCompanion Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicCompanion { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Alt+R: FUN_009d4010 toggles 0x71 only while a homunculus is out.</summary>
        public static bool ToggleHomun() {
            RoClassicCompanion c = Ensure();
            if (c == null || c.session == null) return false;
            if (c.homunWindow != null && c.homunWindow.Visible) c.homunWindow.Visible = false;
            else if (c.homun.Active) c.Open(ref c.homunWindow, "UIHomunInfoWnd", "homun");
            return true;
        }

        /// <summary>Ctrl+R: toggles 0x7d only while a mercenary is out.</summary>
        public static bool ToggleMerc() {
            RoClassicCompanion c = Ensure();
            if (c == null || c.session == null) return false;
            if (c.mercWindow != null && c.mercWindow.Visible) c.mercWindow.Visible = false;
            else if (c.merc.Active) {
                c.Open(ref c.mercWindow, "UIMerInfoWnd", "merc");
                c.SyncMercBars();
            }

            return true;
        }

        private void Open(ref RoClassicWindow window, string layoutName, string key) {
            if (window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + layoutName).text);
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    // the homunculus gauges, edit and box are drawn here; the mercenary keeps its native UIBarGraphPlayer
                    if (child.Class == "UIEditCtrl2" || child.Class == "UIBarGraphHomun" || child.Class == "UIINT64BarGraph" ||
                        child.Class == "UIBarGraph4" || child.Class == "UICheckBox") child.Visible = false;
                }

                RoClassicWindow w = new RoClassicWindow(system, layout) { Visible = false };
                string prefs = "ro.ui.classic." + key + "info.";
                w.Moved += moved => {
                    PlayerPrefs.SetInt(prefs + "x", moved.Position.x);
                    PlayerPrefs.SetInt(prefs + "y", moved.Position.y);
                };
                if (key == "homun") {
                    w.DrawDynamic += DrawHomun;
                    w.Clicked += HomunClicked;
                    w.Pressed += (pw, local) => {
                        if (local.x >= 210 && local.x < 220 && local.y >= 142 && local.y < 152) ToggleAutoFeed();
                    };
                    homunName = new RoClassicEditField { Rect = new RectInt(135, 22, 80, 16), Background = EditFill, MaxBytes = 23 };
                    homunName.Submitted += _ => RenameHomun();
                    homunName.SetText(homun.Name);
                    w.AddEditField(homunName);
                } else {
                    w.DrawDynamic += DrawMerc;
                    w.Clicked += MercClicked;
                }

                window = w;
            }

            Vector2Int client = system.ClientSize;
            string p = "ro.ui.classic." + key + "info.";
            int x = PlayerPrefs.GetInt(p + "x", 150), y = PlayerPrefs.GetInt(p + "y", 100);
            // the validators (0x907030 / 0x907460) want the whole rectangle on screen, else (150, 100)
            if (x < 0 || y < 0 || x + window.Width > client.x || y + window.Height > client.y) (x, y) = (150, 100);
            window.MoveTo(x, y);
            window.Visible = true;
            system.Windows.BringToFront(window);
            window.Invalidate();
        }

        // ------------------------------------------------------------------ 0x71

        private static string Aspd(short amotion) => ((2000 - amotion) / 10).ToString(CultureInfo.InvariantCulture);

        private static void DrawStats(RoClassicWindow w, short[] stats) {
            if (stats == null) return;
            for (int i = 0; i < 7; i++) w.TextOutRight(79, 24 + 18 * i, stats[i].ToString(CultureInfo.InvariantCulture), 1, 14, Black);
            w.TextOutRight(79, 150, Aspd(stats[7]), 1, 14, Black);
        }

        /// <summary>TextOut 0x9b4890: "\n" in the message breaks the line, 12 px apart.</summary>
        private static void TextLines(RoClassicWindow w, int x, int y, string text, Color32 color) {
            string[] lines = text.Replace("\\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++) w.TextOut(x, y + 12 * i, lines[i], 0, 12, color);
        }

        public static (int Message, string Fallback) HomunIntimacy(int intimacy) {
            if (intimacy >= 0 && intimacy < 4) return (0x3fe, "Hate with a Passion");
            if (intimacy >= 0 && intimacy < 11) return (0x3fd, "Hate");
            if (intimacy >= 0 && intimacy < 101) return (0x2a0, " Awkward");
            if (intimacy >= 0 && intimacy < 251) return (0x2a1, " Shy");
            if (intimacy >= 0 && intimacy < 751) return (0x29d, " Neutral");
            if (intimacy >= 0 && intimacy < 911) return (0x2a2, " Cordial");
            if (intimacy >= 0 && intimacy < 1001) return (0x2a3, " Loyal");
            return (0x2a4, "Unknown");
        }

        private void DrawHomun(RoClassicWindow w) {
            w.TextOut(5, 2, RoClassicUiHost.MsgStr(0x3a8, "Homunculus Info"), 0, 12, Black);
            w.TextOut(100, 24, RoClassicUiHost.MsgStr(0x197, "Name"), 0, 12, Black);
            w.TextOut(100, 47, RoClassicUiHost.MsgStr(0x198, "Level"), 0, 12, Black);
            w.TextOut(100, 105, RoClassicUiHost.MsgStr(0x3fb, "EXP"), 0, 12, Black);
            w.TextOut(100, 131, RoClassicUiHost.MsgStr(0x249, "Hunger"), 0, 12, Black);
            w.TextOut(100, 159, RoClassicUiHost.MsgStr(0x24a, "Intimacy"), 0, 12, Black);
            w.TextOut(140, 159, ":", 0, 12, Black);
            DrawStats(w, homun.Stats);
            w.TextOut(135, 47, homun.Level.ToString(CultureInfo.InvariantCulture), 0, 12, Black);
            DrawHomunBar(w, 117, 73, 157, homun.Hp, homun.MaxHp);
            DrawHomunBar(w, 117, 88, 157, homun.Sp, homun.MaxSp);
            DrawLineBar(w, 101, 119, 102, 6, homun.Exp, homun.ExpNext);
            DrawLineBar(w, 101, 145, 102, 6, homun.Hunger, 100);
            w.DrawBitmap(210, 142, Ui + (autoFeed ? "checkbox_1.bmp" : "checkbox_0.bmp"));
            if (!homun.HasProperty) return;
            w.TextOutRight(205, 104, homun.Exp.ToString(CultureInfo.InvariantCulture), 1, 14, Black);
            w.TextOutRight(200, 130, homun.Hunger.ToString(CultureInfo.InvariantCulture) + " / 100", 1, 14, Black);
            (int message, string fallback) = HomunIntimacy(homun.Intimacy);
            w.TextOut(150, 159, RoClassicUiHost.MsgStr(message, fallback), 0, 12, Black);
            TextLines(w, 225, 136, RoClassicUiHost.MsgStr(0xccf, "Auto \\nFeeding"), Black);
            TextLines(w, 8, 180, RoClassicUiHost.MsgStr(0xcd4, "Homunculus receives \\n10% of master's experience."), Red);
        }

        /// <summary>
        /// UIBarGraphHomun 0x7e8ee0: the gze_bg left / mid / right bed, then while value &lt;= max the gze{blue|red} fill
        /// over (w − 8) × value / max (red below 25%) and "%3I64d  /  %3I64d" centred at y −2 (Arial 13).
        /// </summary>
        private void DrawHomunBar(RoClassicWindow w, int x, int y, int width, long value, long max) {
            if (value > max) return;
            w.DrawBitmap(x, y, Basic + "gze_bg_left.bmp");
            for (int i = 0; i < width - 2 - 4; i++) w.DrawBitmap(x + 2 + i, y, Basic + "gze_bg_mid.bmp");
            w.DrawBitmap(x + width - 4, y, Basic + "gze_bg_right.bmp");
            if (max == 0) return;
            double ratio = (double) value / max;
            if (ratio == 0) return;
            string prefix = Basic + "gze" + (ratio < 0.25 ? "red" : "blue");
            int fill = (int) ((width - 8) * ratio);
            w.DrawBitmap(x, y, prefix + "_left.bmp");
            for (int i = 0; i < fill; i++) w.DrawBitmap(x + 4 + i, y, prefix + "_mid.bmp");
            w.DrawBitmap(x + 4 + fill, y, prefix + "_right.bmp");
            string text = Pad3(value) + "  /  " + Pad3(max);
            RoClassicGdiFont font = w.FontFor(1, 13);
            w.TextOut(x + (width - font.Measure(text)) / 2, y - 2, text, 1, 13, Black);
        }

        private static string Pad3(long v) => v.ToString(CultureInfo.InvariantCulture).PadLeft(3);

        /// <summary>UIBarGraph4 0x7e8830 / UIINT64BarGraph: #AFAFAF border, white inside, #567FCF fill (red below 25%).</summary>
        private static void DrawLineBar(RoClassicWindow w, int x, int y, int width, int height, long value, long max) {
            if (value > max) return;
            w.FillRect(x, y, width, height, new Color32(0xaf, 0xaf, 0xaf, 255));
            w.FillRect(x + 1, y + 1, width - 2, height - 2, new Color32(255, 255, 255, 255));
            if (max == 0) return;
            double ratio = (double) value / max;
            if (ratio == 0) return;
            int fill = (int) ((width - 2) * ratio);
            if (fill > 0) w.FillRect(x + 1, y + 1, fill, height - 2, ratio * 100 < 25 ? Red : new Color32(0x56, 0x7f, 0xcf, 255));
        }

        private void HomunClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case CloseId:
                    homunWindow.Visible = false;
                    break;
                case RenameId:
                    RenameHomun();
                    break;
                case HomunFeedId:
                    Confirm(0x259, "Are you sure that you want to feed your pet?", () => _ = session?.BeginLegacyHomunculusCommand(1));
                    break;
                case HomunDeleteId:
                    Confirm(0x3bb, "You have decided to delete this Homunculus. Would you like to continue?", () => {
                        _ = session?.BeginLegacyHomunculusCommand(2);
                        homunWindow.Visible = false;
                    });
                    break;
                case HomunSkillId:
                    // Msg 6 0x11d: deletes / toggles 0x72
                    RoClassicCompanionSkills.Toggle(session, 0);
                    break;
            }
        }

        private void ToggleAutoFeed() {
            autoFeed = !autoFeed;
            _ = session?.BeginLegacyCompanionConfig(3, autoFeed ? 1 : 0);
            homunWindow?.Invalidate();
        }

        /// <summary>Msg 6 / 0xb4: under 24 bytes, 0xba1's confirmation, then CZ_RENAME_MER (0x3aa when too long).</summary>
        private void RenameHomun() {
            string name = homunName?.Text?.Trim() ?? string.Empty;
            if (name.Length == 0 || name == homun.Name) return;
            if (System.Text.Encoding.UTF8.GetByteCount(name) >= 24) {
                Chat(RoClassicUiHost.MsgStr(0x3aa, "Please give your Homunculus a name no longer than 23 letters."), "#FF0000");
                homunName.SetText(homun.Name);
                return;
            }

            string question = RoClassicUiHost.MsgStr(0xba1, "The Homunculus's name will be changed to^0000ff^0000ff %s^000000^000000, Are you sure?").Replace("%s", name);
            RoClassicMessageBox.Show(question, RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result == RoClassicMessageBox.IdYes) _ = session?.BeginLegacyHomunculusRename(name);
                else homunName.SetText(homun.Name);
            });
        }

        // ------------------------------------------------------------------ 0x7d

        private void SyncMercBars() {
            if (mercWindow == null) return;
            mercWindow.Set("Hp", merc.Hp);
            mercWindow.Set("MaxHp", merc.MaxHp);
            mercWindow.Set("Sp", merc.Sp);
            mercWindow.Set("MaxSp", merc.MaxSp);
        }

        private int MercClass() {
            var world = UnityEngine.Object.FindFirstObjectByType<Ragnarok.World.RoOnlineWorldController>();
            if (world == null || merc.Gid == 0) return -1;
            foreach (Ragnarok.World.RoOnlineActorSnapshot actor in world.SnapshotActors()) {
                if (actor.ServerId == merc.Gid) return actor.Job;
            }

            return -1;
        }

        /// <summary>0x817c20's title: archers 0x1781.., swordsmen 0x1795.., spearmen 0x178b.. (ten each), else 0x557.</summary>
        public static (int Message, string Fallback) MercTitle(int job) {
            if (unchecked((uint) (job - 0x1781)) < 10) return (0x49d, "Mercenary: Archer");
            if (unchecked((uint) (job - 0x1795)) < 10) return (0x49e, "Mercenary: Swordman");
            if (unchecked((uint) (job - 0x178b)) < 10) return (0x49f, "Mercenary: Spearman");
            return (0x557, "Mercenary Info - Monster Type");
        }

        private void DrawMerc(RoClassicWindow w) {
            int job = MercClass();
            (int title, string fallback) = MercTitle(job);
            w.TextOut(5, 2, RoClassicUiHost.MsgStr(title, fallback), 0, 12, Black);
            w.TextOut(100, 24, RoClassicUiHost.MsgStr(0x197, "Name"), 0, 12, Black);
            w.TextOut(100, 47, RoClassicUiHost.MsgStr(0x198, "Level"), 0, 12, Black);
            w.TextOut(100, 111, RoClassicUiHost.MsgStr(0x4a0, "Expiration"), 0, 12, Black);
            w.TextOut(100, 135, RoClassicUiHost.MsgStr(0x4a1, "Loyalty"), 0, 12, Black);
            w.TextOut(100, 159, RoClassicUiHost.MsgStr(0x4a2, "Summons"), 0, 12, Black);
            w.TextOut(230, 133, RoClassicUiHost.MsgStr(0x4a3, "Kill"), 0, 12, Black);
            DrawStats(w, merc.Stats);
            w.TextOut(136, 24, merc.Name, 0, 12, Gray);
            w.TextOut(135, 24, merc.Name, 0, 12, Black);
            w.TextOut(135, 47, merc.Level.ToString(CultureInfo.InvariantCulture), 0, 12, Black);
            if (!merc.Active) return;
            w.TextOut(218, 70, merc.Hp.ToString(CultureInfo.InvariantCulture) + " / " + merc.MaxHp.ToString(CultureInfo.InvariantCulture), 1, 14, Black);
            w.TextOut(218, 85, merc.Sp.ToString(CultureInfo.InvariantCulture) + " / " + merc.MaxSp.ToString(CultureInfo.InvariantCulture), 1, 14, Black);
            w.TextOut(155, 111, ExpireText(merc.Expire, RoClassicUiHost.MsgStr(0x4ec, "%m/%d %H:%M")), 0, 12, Red);
            w.TextOut(170, 133, merc.Faith.ToString(CultureInfo.InvariantCulture), 1, 14, Black);
            w.TextOut(170, 157, merc.Calls.ToString(CultureInfo.InvariantCulture), 1, 14, Black);
            w.TextOut(245, 157, merc.Kills.ToString(CultureInfo.InvariantCulture), 1, 14, Black);
        }

        /// <summary>strftime of _localtime32: the client's %m %d %H %M (the only ones its message uses).</summary>
        public static string ExpireText(int unixSeconds, string format) {
            DateTime t = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
            return format.Replace("%m", t.Month.ToString("00", CultureInfo.InvariantCulture)).Replace("%d", t.Day.ToString("00", CultureInfo.InvariantCulture))
                .Replace("%H", t.Hour.ToString("00", CultureInfo.InvariantCulture)).Replace("%M", t.Minute.ToString("00", CultureInfo.InvariantCulture));
        }

        private void MercClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case CloseId:
                    mercWindow.Visible = false;
                    break;
                case MercFireId:
                    Confirm(0x55a, "^ff0000Deleting a Mercenary Soldier^000000 will also delete his growth history. Do you really want to proceed with the deletion?", () => {
                        _ = session?.BeginLegacyMercenaryCommand(2);
                        mercWindow.Visible = false;
                    });
                    break;
                case MercSkillId:
                    RoClassicCompanionSkills.Toggle(session, 1);
                    break;
            }
        }

        // ------------------------------------------------------------------ shared

        private static void Confirm(int message, string fallback, Action yes) {
            RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(message, fallback), RoClassicMessageBox.TypeOkCancel, true, result => {
                if (result == RoClassicMessageBox.IdYes) yes();
            });
        }

        private void Chat(string text, string color) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, color);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: info | merc | feed | delete | fire | rename name | autofeed | state.</summary>
        public static string Sweep(string command) {
            RoClassicCompanion c = instance;
            if (c == null) return "no companion producer";
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            switch (a[0]) {
                case "info": ToggleHomun(); break;
                case "merc": ToggleMerc(); break;
                case "feed": c.HomunClicked(new RoClassicChildLayout { Id = HomunFeedId }); break;
                case "delete": c.HomunClicked(new RoClassicChildLayout { Id = HomunDeleteId }); break;
                case "fire": c.MercClicked(new RoClassicChildLayout { Id = MercFireId }); break;
                case "autofeed": c.ToggleAutoFeed(); break;
                case "rename":
                    if (a.Length > 1 && c.homunName != null) {
                        c.homunName.SetText(a[1]);
                        c.RenameHomun();
                    }

                    break;
            }

            RoClassicHomunState h = c.homun;
            RoClassicMercState m = c.merc;
            return "homun visible=" + (c.homunWindow != null && c.homunWindow.Visible) + " active=" + h.Active + " gid=" + h.Gid + " name=" + h.Name +
                   " flags=" + h.Flags + " lv=" + h.Level + " hp=" + h.Hp + "/" + h.MaxHp + " sp=" + h.Sp + "/" + h.MaxSp + " exp=" + h.Exp + "/" + h.ExpNext +
                   " hunger=" + h.Hunger + " intimacy=" + h.Intimacy + " | merc visible=" + (c.mercWindow != null && c.mercWindow.Visible) + " active=" + m.Active +
                   " gid=" + m.Gid + " name=" + m.Name + " lv=" + m.Level + " hp=" + m.Hp + "/" + m.MaxHp + " faith=" + m.Faith + " kills=" + m.Kills +
                   " expire=" + m.Expire + " job=" + c.MercClass();
        }
    }
}
