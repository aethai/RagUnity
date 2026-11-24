using System;
using System.Globalization;
using System.Text;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The classic Command List, UICommandListWnd 0x70 (ui/RUNTIME-RULES-HOTKEYS.md §2): the slash commands of
    /// CmdOnOffOderList in two columns with their on/off icon, the help text as the tooltip, a press runs the command (its
    /// flag toggles). Opened by Game Settings' "Additional settings" (and the client's Alt+Y), re-created at
    /// (scaledX(400), scaledY(200)) on every open, closed by its X or Esc. The flags this client has a setting for drive
    /// it; the others are kept as the client keeps them.
    /// </summary>
    public sealed class RoClassicCommandList : IRoClassicUiProducer {
        private const string Layout = "Ragnarok/Generated/OriginalClient/ui/UICommandListWnd";
        private const string Basic = "유저인터페이스\\basic_interface\\";
        private const int CloseId = 0xc5;
        private const int Columns = 2;
        private const int CellW = 105;   // (W − 40) / 2
        private const int Rows = 8;      // (H − 38) / 20
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Highlight = new Color32(0x70, 0x9f, 0xed, 255);

        /// <summary>
        /// A command: its name and the help line (CmdHelpList → msgstring_kr.lub); its flag is the slash command's
        /// (RoChatSlashCommands' options: the Game Settings value or the flag kept under "ro.ui.classic.cmd./name").
        /// </summary>
        private sealed class Command {
            public string Name;
            public string Help;
            public string HelpOff;   // /showname: the text for the off state
        }

        // cmdinfo.lub's order without /btg (unknown to this client); defaults from optioninfo.lub
        private static readonly Command[] Commands = {
            Cmd("/notrade", "/notrade : Declines trade offers automatically. On | Off"),
            Cmd("/noshift", "/noshift : You may use your ''force heal'' ability without the Shift key. On | Off"),
            Cmd("/noctrl", "/noctrl : Auto attack without pressing ctrl key. On | Off"),
            Cmd("/skillfail", "/skillfail : Display red font message when skill fails. On | Off"),
            Cmd("/notalkmsg", "/notalkmsg : Chat will not be displayed in chat window. On | Off"),
            Cmd("/notalkmsg2", "/notalkmsg2 : Hides chat messages(including guild chat). On | Off"),
            new Command {
                Name = "/showname", Help = "Normal Font Displayed. [showname type 1]",
                HelpOff = "Font will be thin and party name will be shown [showname type 2]",
            },
            Cmd("/fog", "/fog : Fog effect. On | Off"),
            // Game Settings: /aura on = "Original" (not the simple aura), /aura2 on = the aura hidden
            Cmd("/aura", "/aura : Simplify Aura effect On | Off"),
            Cmd("/aura2", "/aura2 : Turn Aura completly On Off"),
            Cmd("/window", "/window : Display windows will snap/dock together. On | Off"),
            Cmd("/miss", "/miss : Toggle ''Miss'' display"),
            Cmd("/q1", "/quickspell  : Right-click menu enables you to use skills assigned to the F9 hotkey. On | Off"),
            Cmd("/q2", "/quickspell2 : By rolling the mouse wheel up and down, you are able to use skills registered on F7 and F8 hotkeys. On | Off"),
            Cmd("/effect", "/effect : Effects On | Off"),
            Cmd("/bgm", "/music : Turns BGM On | Off"),
            Cmd("/sound", "/sound: Turns Sound Effects On | Off"),
            Cmd("/loginout", "/loginout : Shows guildsmen and friends online status. On | Off"),
            Cmd("/shopping", "/shopping : Enables you to open a shop with a single left-click and close your shop with a single right-click. On | Off"),
            Cmd("/stateinfo", "/stateinfo : Shows a description of the status icons. On | Off"),
            Cmd("/snap", "/snap : Turns the snap On | Off for monsters."),
            Cmd("/itemsnap", "/itemsnap : Turns snap On | Off for items on the grounds."),
            Cmd("/skillsnap", "/skillsnap : Turns snap On | Off for skills."),
            Cmd("/hoai", "/hoai : Toggles custom homunculus AI. On | Off"),
            Cmd("/merai", "/merai : Toggles custom mercenary AI. On | Off"),
            Cmd("/camera", "/camera : Toggles between smooth/locked camera. On | Off"),
            Cmd("/lightmap", "/lightmap : Turns object shadows. On | Off"),
            Cmd("/monsterhp", "/monsterhp : Toggles monster HP information when attacking. On/Off"),
            Cmd("/cashshop", "/cashshop : Display Cash Shop Button. Show / Hide"),
            Cmd("/goldpc", "/goldpc : Display Gold PC Cafe Button. Show / Hide"),
            Cmd("/roulette", "/roulette : Display Roulette button. Show / Hide"),
            Cmd("/quake", "/quake : Flicker effect On/Off"),
            Cmd("/minimap", "/minimap : Shows minimap On | Off."),
            Cmd("/mineffect", "/mineffect : Turn minimum effect On/off"),
            Cmd("/mable", "/mable : Display Spirit Marble On/Off"),
            Cmd("/zoom", "/zoom : Turns Zoom Out option On/Off"),
        };

        private static Command Cmd(string name, string help) => new Command { Name = name, Help = help };

        private static RoClassicCommandList instance;

        private readonly RoClassicWindow window;
        private readonly RoClassicChildLayout tabControl;
        private RoClientSettingsModel settings;
        private RoClientSettingsSnapshot snap;   // the settings as of the last open / run (the cells draw from it)
        private int tab;
        private int first;     // +0xac, the first visible row
        private int hover = -1;
        private Vector2Int hoverAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicCommandList(RoClassicUiSystem system, RoClassicWindowLayout layout) {
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UITab") tabControl = child;
                if (child.Class == "UIScrollBar") child.Visible = false; // drawn by DrawDynamic with the live range
            }

            window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 17 };
            window.DrawDynamic += Draw;
            window.Clicked += child => {
                if (child.Id == CloseId) Close();
            };
            window.Pressed += (w, local) => Press(local);
            window.Wheeled += (w, local, delta) => Scroll(delta > 0 ? -3 : 3);
            window.EscClose = () => {
                Close();
                return true;
            };
            system.Register(this);
        }

        public static bool IsOpen => instance != null && instance.window.Visible;

        /// <summary>MakeWindow(0x70) / DeleteWindow(0x70): the Game Settings button and Alt+Y toggle it.</summary>
        public static void Toggle(RoClientSettingsModel settings) {
            if (IsOpen) {
                Close();
                return;
            }

            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || settings == null) return;
            if (instance == null || instance.window == null) {
                var asset = Resources.Load<TextAsset>(Layout);
                if (asset == null) return;
                instance = new RoClassicCommandList(system, RoClassicWindowLayout.Parse(asset.text));
            }

            instance.Open(system, settings);
        }

        public static void Close() {
            if (instance == null) return;
            instance.window.Visible = false;
            RoClassicUiSystem.Instance?.Windows.RemoveEsc(instance.window);
        }

        private void Open(RoClassicUiSystem system, RoClientSettingsModel model) {
            settings = model;
            snap = settings.Snapshot();
            tab = 0;
            first = 0;
            hover = -1;
            SetTabBitmap();
            Vector2Int client = system.ClientSize;
            window.MoveTo(Mathf.Clamp((client.x - 640) / 2 + 400, 0, Mathf.Max(0, client.x - window.Width)),
                Mathf.Clamp(200 * client.y / 480, 0, Mathf.Max(0, client.y - window.Height)));
            window.Visible = true;
            window.Invalidate();
            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
        }

        private bool Flag(Command c) =>
            RoChatSlashCommands.TryGetOption(c.Name, out RoChatSlashCommands.Option option)
                ? RoChatSlashCommands.GetOptionFrom(option, snap)
                : RoChatSlashCommands.ReadFlag(RoChatSlashCommands.PrefsPrefix + c.Name, true);   // /stateinfo (RoClassicStatusIcons)

        private int Count => tab == 0 ? Commands.Length : 0; // nothing in this client writes the EXE list

        private int TotalRows => (Count + Columns - 1) / Columns;

        // ---- drawing (OnDraw 0x84c840 after the oracle's frame) ----

        private void Draw(RoClassicWindow w) {
            int count = Count;
            for (int n = 0; n < Rows * Columns; n++) {
                int index = first * Columns + n;
                if (index >= count) break;
                int c = n % Columns, r = n / Columns, cellX = 25 + CellW * c;
                if (index == hover) {
                    w.FillRect(cellX, 19 + 20 * r, CellW, 20, Highlight);
                    w.FillRect(cellX + 1, 20 + 20 * r, CellW - 2, 18, White);
                }

                Command command = Commands[index];
                w.TextOut(cellX + 25, 22 + 20 * r, command.Name, 0, 12, Black);
                w.DrawBitmap(cellX + 2, 22 + 20 * r, Basic + (Flag(command) ? "grp_online.bmp" : "grp_offline.bmp"));
            }

            int range = Math.Max(0, TotalRows - Rows);
            w.DrawScrollBar(w.Width - 15, 14, w.Height - 38, range, TotalRows, Rows, first);
        }

        private void SetTabBitmap() {
            if (tabControl == null) return;
            foreach (RoClassicOp op in tabControl.Ops) {
                if (op.Kind == RoClassicOpKind.Bitmap) op.Path = Basic + (tab == 0 ? "tab_cmd_01.bmp" : "tab_cmd_02.bmp");
            }
        }

        // ---- input (0x8538f0, 0x854130, 0x856130) ----

        private void Press(Vector2Int local) {
            if (local.y < 17) return; // the frame drags
            if (local.x < 20 && local.y < 17 + 89) {
                // the vertical tab: y / (89 / 2), 0 or 1
                int t = Mathf.Clamp((local.y - 17) / (89 / 2), 0, 1);
                if (t != tab) {
                    tab = t;
                    first = 0;
                    hover = -1;
                    SetTabBitmap();
                    window.Invalidate();
                }

                return;
            }

            if (local.x >= window.Width - 15 && local.y >= 14 && local.y < 14 + window.Height - 38) {
                PressScrollBar(local.y - 14, window.Height - 38);
                return;
            }

            int index = CellAt(local);
            if (index < 0) return;
            hover = index;
            Run(Commands[index]);
            window.Invalidate();
        }

        /// <summary>The UIScrollBar: arrows ±1 row, the track ±7 (page) above / below the thumb.</summary>
        private void PressScrollBar(int y, int h) {
            int range = Math.Max(0, TotalRows - Rows);
            if (range == 0) return;
            const int b = 13;
            int len = Math.Max(1, (h - 2 * b) * Rows / TotalRows);
            int top = b + first * (h - 2 * b - len) / range;
            Scroll(y < b ? -1 : y >= h - b ? 1 : y < top ? -7 : y >= top + len ? 7 : 0);
        }

        /// <summary>Hit test 0x834490: rows from y 17, columns split at W / 2; the index counts from the first row.</summary>
        private int CellAt(Vector2Int local) {
            if (local.y < 17 || local.y >= 17 + 20 * Rows || local.x < 0 || local.x >= window.Width) return -1;
            int cell = Columns * ((local.y - 17) / 20) + local.x / (window.Width / 2);
            int index = Columns * first + cell;
            return index < Count ? index : -1;
        }

        /// <summary>
        /// mode->Msg(0x2a, id): the command as if typed — its flag toggles and its line prints in the chat; an open Game
        /// Settings follows.
        /// </summary>
        private void Run(Command command) {
            if (!RoClassicChat.RunCommand(command.Name) && RoChatSlashCommands.TryGetOption(command.Name, out RoChatSlashCommands.Option option)) {
                RoChatSlashCommands.ToggleOption(option, settings, null, out _);
            }

            if (settings != null) snap = settings.Snapshot();

            RoClassicGameSettings.RefreshValues();
        }

        private void Scroll(int rows) {
            int range = Math.Max(0, TotalRows - Rows);
            int next = Mathf.Clamp(first + rows, 0, range);
            if (next == first) return;
            first = next;
            hover = -1;
            window.Invalidate();
        }

        /// <summary>Per frame: the highlighted cell under the pointer and its help text as the tooltip.</summary>
        public void ClassicUiTick(RoClassicUiSystem system) {
            if (window == null || !window.Visible) return;
            if (system.Windows.HoverWindow != window) return;
            Vector2Int local = system.Windows.Mouse - window.Position;
            int index = CellAt(local);
            if (index != hover && index >= 0) {
                hover = index;
                window.Invalidate();
            }

            if (index < 0) return;
            Command command = Commands[index];
            string help = command.HelpOff != null && !Flag(command) ? command.HelpOff : command.Help;
            // near the cell: ((x − 10) / 20 + 2)·20, ((y − 17) / 20)·20 + 1
            RoClassicTooltip.Shared.Show(help, White, window.Position.x + ((local.x - 10) / 20 + 2) * 20,
                window.Position.y + ((local.y - 17) / 20) * 20 + 1);
        }

        // ---- diagnostics console (RoSkillSweepRunner "!cl ...") ----

        internal static string Sweep(string command, RoClientSettingsModel settings) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length ? int.Parse(a[i], CultureInfo.InvariantCulture) : 0;
            switch (a[0]) {
                case "toggle":
                    Toggle(settings);
                    break;
                case "press":
                    instance?.Press(new Vector2Int(Arg(1), Arg(2)));
                    break;
                case "wheel":
                    instance?.Scroll(Arg(1));
                    break;
            }

            if (instance == null) return "no command list";
            var s = new StringBuilder();
            s.Append("open=").Append(IsOpen).Append(" tab=").Append(instance.tab).Append(" first=").Append(instance.first).Append(" pos=").Append(instance.window.Position).Append(" flags=");
            if (instance.settings != null) {
                instance.snap = instance.settings.Snapshot();
                foreach (Command c in Commands) s.Append(c.Name).Append(instance.Flag(c) ? "+" : "-").Append(' ');
            }

            return s.ToString();
        }
    }
}
