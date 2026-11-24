using System;
using Ragnarok.Assets;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The classic pre-game screens (codex-ui-research RUNTIME-RULES-PRE-GAME.md), driven straight by the login shell:
    /// the login background, UILoginWnd 0x3 (301 × 132 at (trunc((W − 640) / 2) + 185, trunc(H · 300 / 480)), its oracle
    /// layout over the bg_login.tga panel of the 0x145 image window), UIWaitWnd 0x6 at the same place while the characters
    /// load, and the refusal messages. Start 0xb6 (or Enter) plays the button sound, rejects an account shorter than 4
    /// (CSV 0xbd5) or a password shorter than 6 (0xbd6), then logs in; Save ID 0xd1 keeps the account; Sign Up 0x107 opens
    /// the sign-up page; the close X 0xd9 asks 0x11 and quits. Opening 0x119 (mode 0x2722, the intro) has no route.
    /// It redraws only when the shell's UI sequence moves.
    /// </summary>
    public sealed partial class RoClassicPreGame : IRoClassicUiProducer {
        private const int StartId = 0xb6, CloseId = 0xd9, SignUpId = 0x107, SaveId = 0xd1;
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Background = "유저인터페이스\\t_login.jpg";
        private const string SaveIdKey = "Ragnarok.RoBrowserLogin.SaveId";
        private const string SavedAccountKey = "Ragnarok.RoBrowserLogin.Account";

        private static RoClassicPreGame instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoMinimalLoginShell shell;
        private RoClassicWindow login, wait;
        private RoClassicEditField account, password;
        private RoClassicMessageBox message;
        private RoClassicUiLayer backgroundLayer;
        private bool backgroundReady, saveId;
        private int seenSequence = int.MinValue;
        private string dismissedFailure = string.Empty, shownFailure = string.Empty;
        private Vector2Int laidOutFor;
        private float nextBackgroundTry;

        /// <summary>The shell builds the classic screens instead of the RoBrowser ones.</summary>
        public static bool Use => Application.isPlaying;

        /// <summary>The pre-game background is up: the world behind it is neither seen nor heard.</summary>
        public static bool CoversWorld => instance?.backgroundLayer != null && instance.backgroundLayer.Visible;

        public static bool LoginVisible => instance?.login != null && instance.login.Visible;

        public static RoClassicPreGame Ensure(RoMinimalLoginShell shell) {
            RoClassicUiSystem system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicPreGame { system = system };
                system.Register(instance);
            }

            instance.shell = shell;
            return instance;
        }

        /// <summary>
        /// The UI scale capped so the shown selection (997 × 626) or creation window (794 × 422, 70 px above centre) fits the
        /// screen: the original assumes at least 1024 × 768; smaller views scale the pre-game screens down (not below ½).
        /// </summary>
        internal static float FitScale(float scale) {
            RoClassicPreGame p = instance;
            if (p == null) return scale;
            int needW, needH;
            if (p.select != null && p.select.Visible) {
                needW = SelW;
                needH = SelH;
            } else if (p.make != null && p.make.Visible) {
                needW = MakeW;
                needH = MakeH + 2 * 70;
            } else {
                return scale;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            float screenW = camera != null ? camera.pixelWidth : Screen.width, screenH = camera != null ? camera.pixelHeight : Screen.height;
            float fit = Mathf.Min(screenW / needW, screenH / needH);
            return fit < scale ? Mathf.Max(0.5f, fit) : scale;
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (shell == null) return;
            // MsgStr and the GRF images before any map runs
            if (RoClassicUiHost.PreGameAssets == null && shell.TryGetBrowserContentAssets(out RoAssetDatabase assets)) RoClassicUiHost.PreGameAssets = assets;
            SelectTick();
            CreateTick();
            ServerTick();
            Vector2Int client = system.ClientSize;
            // redraws only on a shell change or a resize (the background JPEG is tried once per change, never per frame)
            bool retryBackground = !backgroundReady && backgroundLayer != null && backgroundLayer.Visible && Time.unscaledTime >= nextBackgroundTry;
            if (shell.Phase11UiChangeSequence == seenSequence && client == laidOutFor && !retryBackground) return;
            nextBackgroundTry = Time.unscaledTime + 1f;
            seenSequence = shell.Phase11UiChangeSequence;
            laidOutFor = client;
            Refresh();
        }

        private void Refresh() {
            RoLoginShellSnapshot snapshot = shell.GetSnapshotForPhase11Ui();
            RoLoginShellStage stage = snapshot != null ? snapshot.Stage : RoLoginShellStage.Login;
            if (stage == RoLoginShellStage.Login) createOpen = false;
            // a box of the pre-game screens does not outlive them
            if ((stage == RoLoginShellStage.EnteringMap || stage == RoLoginShellStage.MapEntered) && message != null) {
                message.Close();
                message = null;
            }

            // a sent creation ends when the list comes back
            if (createSent && stage == RoLoginShellStage.CharacterSelect) {
                createSent = false;
                createOpen = false;
                selSlot = createSlot;
            }

            bool deleteRefused = TrackDeleteAnswer(snapshot, stage);
            bool create = createOpen && (stage == RoLoginShellStage.CharacterSelect || stage == RoLoginShellStage.LoadingCharacters || stage == RoLoginShellStage.Failed);
            bool select = !create && stage == RoLoginShellStage.CharacterSelect && !deleteRefused;
            bool front = !create && (stage == RoLoginShellStage.Login || stage == RoLoginShellStage.LoadingCharacters || stage == RoLoginShellStage.Failed);
            bool failure = TryFailure(snapshot, out string text, out string key);
            if (!failure && stage != RoLoginShellStage.Failed) dismissedFailure = shownFailure = string.Empty;
            bool showFailure = failure && key != dismissedFailure;
            bool serverList = !create && stage == RoLoginShellStage.ServerSelect;
            SetBackground(front || select || create || serverList || deleteRefused);
            if (showFailure && key != shownFailure) ShowMessage(text, key, create);
            bool loading = stage == RoLoginShellStage.LoadingCharacters;
            SetWait(((front || create) && loading) || deleteRefused);
            SetLogin(front && !loading && !showFailure);
            SetSelect(select, snapshot);
            SetCreate(create);
            SetServer(serverList);
        }

        // ------------------------------------------------------------------ background

        /// <summary>t_login.jpg over the whole screen, under every window, while the login front is up.</summary>
        private void SetBackground(bool visible) {
            if (backgroundLayer == null) {
                if (!visible) return;
                backgroundLayer = system.Renderer.CreateLayer("pre-game background");
                backgroundLayer.AboveOverlayUi = true;
                system.Renderer.SendToBack(backgroundLayer);
            }

            backgroundLayer.Visible = visible;
            if (!visible) return;
            if (!backgroundReady) backgroundReady = LoadBackground();
            RoClassicUiQuads q = backgroundLayer.Quads;
            q.Clear();
            Vector2Int client = system.ClientSize;
            if (backgroundReady && system.Images.TryGet("@pregame-background", out RoClassicUiImage image)) {
                q.Image(0, 0, client.x, client.y, image, new Color32(255, 255, 255, 255));
            }

            backgroundLayer.MarkDirty();
        }

        /// <summary>The JPEG once, decoded to a top-down dynamic image.</summary>
        private bool LoadBackground() {
            Func<string, byte[]> source = RoClassicUiImages.DefaultSourceProvider?.Invoke();
            byte[] data = source?.Invoke("data\\texture\\" + Background);
            if (data == null) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try {
                if (!texture.LoadImage(data)) return false;
                int w = texture.width, h = texture.height;
                Color32[] bottomUp = texture.GetPixels32();
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++) Array.Copy(bottomUp, (h - 1 - y) * w, pixels, y * w, w);
                return system.Images.SetDynamic("@pregame-background", w, h, pixels);
            } finally {
                UnityEngine.Object.Destroy(texture);
            }
        }

        // ------------------------------------------------------------------ 0x3

        private void SetLogin(bool visible) {
            if (!visible) {
                if (login != null) login.Visible = false;
                return;
            }

            if (login == null) {
                TextAsset asset = Resources.Load<TextAsset>(Layouts + "UILoginWnd");
                if (asset == null) return;
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
                // the 0x145 image window's panel under the transparent login window
                layout.Ops.Insert(Math.Min(1, layout.Ops.Count), new RoClassicOp { Kind = RoClassicOpKind.Bitmap, Path = "유저인터페이스\\login_interface\\bg_login.tga", X = 0, Y = 0 });
                layout.Children.RemoveAll(child => child.Id == SignUpId);
                foreach (RoClassicChildLayout child in layout.Children) {
                    // the edits are native; the capture's caret sample goes
                    if (child.Class == "UIEditCtrl_Custom") child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                    if (child.Class != "UIComboBoxDef2Wnd") continue;
                    // the server name's static text is centred on x 80 of the combo (the capture recorded it left-aligned at its box)
                    foreach (RoClassicChildLayout part in child.Children) {
                        if (part.Class != "UIStaticText") continue;
                        part.X = 80 - part.W / 2;
                        foreach (RoClassicOp op in part.Ops) {
                            if (op.Kind != RoClassicOpKind.Text) continue;
                            op.Align = RoClassicTextAlign.Center;
                            op.BoxW = part.W;
                        }
                    }
                }

                login = new RoClassicWindow(system, layout) { Visible = false, Draggable = false };
                login.Clicked += OnClicked;
                account = new RoClassicEditField { Rect = new RectInt(15, 40, 153, 16), MaxBytes = 23, Centered = true };
                password = new RoClassicEditField { Rect = new RectInt(15, 64, 153, 16), MaxBytes = 23, PasswordMask = '*', Centered = true };
                account.Submitted += _ => Start();
                password.Submitted += _ => Start();
                account.TabPressed += _ => password.Focus();
                password.TabPressed += _ => account.Focus();
                login.AddEditField(account);
                login.AddEditField(password);
                saveId = PlayerPrefs.GetInt(SaveIdKey, 1) != 0;
                // the shell's serialized account is a lab default; a release build starts with an empty box
                string fallback = Application.isEditor || Debug.isDebugBuild ? shell.Account ?? string.Empty : string.Empty;
                account.SetText(saveId ? PlayerPrefs.GetString(SavedAccountKey, fallback) : fallback);
            }

            Vector2Int client = system.ClientSize;
            login.MoveTo((client.x - 640) / 2 + 185, client.y * 300 / 480);
            SetCheck(login, SaveId, saveId);
            if (!login.Visible) {
                login.Visible = true;
                system.Windows.BringToFront(login);
                (saveId && account.Text.Length > 0 ? password : account).Focus();
            }

            login.Invalidate();
        }

        /// <summary>The Save ID UICheckBox child: every state shows checkbox_on / checkbox_off.</summary>
        private static void SetCheck(RoClassicWindow w, int id, bool on) {
            string art = "유저인터페이스\\login_interface\\" + (on ? "checkbox_on.bmp" : "checkbox_off.bmp");
            foreach (RoClassicChildLayout c in w.Layout.Children) {
                if (c.Id != id) continue;
                c.States["normal"] = c.States["hover"] = c.States["pressed"] = art;
                // its recorded op is the off bitmap
                c.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Bitmap);
            }

            w.Invalidate();
        }

        private void OnClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case StartId:
                    Start();
                    break;
                case SaveId:
                    saveId = !saveId;
                    PlayerPrefs.SetInt(SaveIdKey, saveId ? 1 : 0);
                    SetCheck(login, SaveId, saveId);
                    break;
                case CloseId:
                    // 0x81d390: CSV 0x11, then mode 2 on Yes
                    RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x11, "Are you sure that you want to quit?"), RoClassicMessageBox.TypeOkCancel, true, r => {
                        if (r == RoClassicMessageBox.IdYes) Quit();
                    });
                    break;
            }
        }

        /// <summary>Start 0xb6: the button sound, the length checks, the saved ID, then the shell's login once its content is ready.</summary>
        private void Start() {
            Ragnarok.Audio.RoAudioEvents.PlayButtonSound();
            string id = account.Text.Trim(), pass = password.Text;
            if (id.Length < 4) {
                ShowValidation(RoClassicUiHost.MsgStr(0xbd5, "Please enter at least %d characters.").Replace("%d", "4"));
                return;
            }

            if (pass.Length < 6) {
                ShowValidation(RoClassicUiHost.MsgStr(0xbd6, "Please enter at least %d characters.").Replace("%d", "6"));
                return;
            }

            shell.Account = id;
            shell.Password = pass;
            PlayerPrefs.SetString(SavedAccountKey, saveId ? id : string.Empty);
            if (shell.IsBrowserContentReady || (shell.BeginBrowserContentPreload(out _) && shell.IsBrowserContentReady)) {
                // the login alone: the character server list (UISelectServerWnd 0x2) comes next
                shell.LoginFromPhase11Ui();
            } else if (shell.TryGetBrowserContentLoadFailure(out string failure)) {
                ShowValidation(failure);
            }

            seenSequence = int.MinValue;
        }

        private static void Quit() {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ 0x6 and messages

        private void SetWait(bool visible) {
            if (!visible) {
                if (wait != null) wait.Visible = false;
                return;
            }

            if (wait == null) {
                TextAsset asset = Resources.Load<TextAsset>(Layouts + "UIWaitWnd");
                if (asset == null) return;
                wait = new RoClassicWindow(system, RoClassicWindowLayout.Parse(asset.text)) { Visible = false, Draggable = false };
            }

            Vector2Int client = system.ClientSize;
            wait.MoveTo((client.x - 640) / 2 + 185, client.y * 300 / 480);
            if (!wait.Visible) {
                wait.Visible = true;
                system.Windows.BringToFront(wait);
            }
        }

        private void ShowValidation(string text) {
            message?.Close();
            message = RoClassicMessageBox.Show(text, RoClassicMessageBox.TypeOk, true, _ => {
                message = null;
                password.Focus();
            });
        }

        /// <summary>A refused login: the box until OK, then the login again with an empty password.</summary>
        private void ShowMessage(string text, string key, bool creating) {
            shownFailure = key;
            message?.Close();
            message = RoClassicMessageBox.Show(text, RoClassicMessageBox.TypeOk, true, _ => {
                message = null;
                dismissedFailure = key;
                seenSequence = int.MinValue;
                if (creating) {
                    // a refused creation goes back to the selection list, the creation window stays open
                    createSent = false;
                    shell.ReturnToCharacterSelectFromPhase11Ui();
                    return;
                }

                password?.SetText(string.Empty);
            });
        }

        private static bool TryFailure(RoLoginShellSnapshot snapshot, out string text, out string key) {
            text = key = string.Empty;
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ErrorText) || snapshot.Stage != RoLoginShellStage.Failed) return false;
            string error = snapshot.ErrorText.Trim();
            key = snapshot.Stage + "|" + error;
            text = FailureText(error);
            return true;
        }

        /// <summary>The refusal code 1 is the wrong password; a broken connection reads as one; anything else as reported.</summary>
        private static string FailureText(string error) {
            const string prefix = "Login refused with error code ";
            int at = error.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (at >= 0) {
                int start = at + prefix.Length, end = start;
                while (end < error.Length && char.IsDigit(error[end])) end++;
                if (end > start && error.Substring(start, end - start) == "1") return "Incorrect User ID or Password. Please try again.";
                return error;
            }

            string[] connection = {
                "disconnected", "connection was closed", "transport is not connected", "connection refused", "actively refused",
                "forcibly closed", "connection reset", "network is unreachable", "host is unreachable", "timed out while waiting", "timeout",
            };
            foreach (string fragment in connection) {
                if (error.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) return RoClassicUiHost.MsgStr(0x1, "Failed to Connect to Server.");
            }

            return error;
        }

        /// <summary>The sweep console: state | account t | password t | start | save.</summary>
        public static string Sweep(string command) {
            RoClassicPreGame p = instance;
            if (p == null) return "no classic pre-game";
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            switch (a[0]) {
                case "account":
                    p.account?.SetText(a.Length > 1 ? a[1] : string.Empty);
                    break;
                case "password":
                    p.password?.SetText(a.Length > 1 ? a[1] : string.Empty);
                    break;
                case "start":
                    p.Start();
                    break;
                case "save":
                    p.OnClicked(new RoClassicChildLayout { Id = SaveId });
                    break;
                case "slot":
                    if (p.select != null && int.TryParse(a.Length > 1 ? a[1] : string.Empty, out int slot)) p.PickSlot(slot);
                    break;
                case "server":
                    return p.SweepServer(a.Length > 1 ? ("server " + a[1]).Split(' ') : new[] { "server" });
                case "gamestart":
                    p.GameStart();
                    break;
                case "reserve": {
                    RoLoginShellCharacter? c = p.CharacterAt(p.selSlot);
                    if (c.HasValue) p.shell.ReserveDeleteCharacterFromPhase11Ui(c.Value.CharacterId);
                    break;
                }
                case "delete": {
                    // the Delete button past the date, OK on CSV 0x13, the deletion password, OK
                    RoLoginShellCharacter? c = p.CharacterAt(p.selSlot);
                    if (!c.HasValue) return "delete: no character";
                    p.ShowDeletionPassword(c.Value.CharacterId);
                    p.deletePasswordEdit.SetText(a.Length > 1 ? a[1] : string.Empty);
                    p.ConfirmDeletionPassword();
                    break;
                }
                case "page":
                    if (p.select != null) p.OnSelectClicked(new RoClassicChildLayout { Id = a.Length > 1 && a[1] == "left" ? PageLeftId : PageRightId });
                    break;
                case "makename":
                    p.makeName?.SetText(a.Length > 1 ? a[1] : string.Empty);
                    break;
                case "makestyle":
                    if (p.make != null && int.TryParse(a.Length > 1 ? a[1] : string.Empty, out int style)) { p.makeStyle = style; p.previewVersion++; p.make.Invalidate(); }
                    break;
                case "makecolor":
                    if (p.make != null && int.TryParse(a.Length > 1 ? a[1] : string.Empty, out int color)) { p.makeColor = color; p.previewVersion++; p.make.Invalidate(); }
                    break;
                case "race":
                    if (p.make != null && int.TryParse(a.Length > 1 ? a[1] : string.Empty, out int race)) { p.SelectRace(race); p.previewVersion++; p.make.Invalidate(); }
                    break;
                case "makesex":
                    if (p.make != null) { p.makeMale = !p.makeMale; p.previewVersion++; p.make.Invalidate(); }
                    break;
                case "rotate":
                    if (p.make != null) p.OnCreateClicked(new RoClassicChildLayout { Id = RotateRightId });
                    break;
                case "create":
                    if (p.make != null) p.Create();
                    break;
                case "makeback":
                    p.CloseCreate();
                    break;
            }

            RoLoginShellSnapshot s = p.shell?.GetSnapshotForPhase11Ui();
            return "stage=" + (s != null ? s.Stage.ToString() : "-") + " select=" + SelectVisible + " slot=" + p.selSlot + " page=" + p.selPage + " create=" + CreateVisible + " login=" + LoginVisible + " wait=" + (p.wait != null && p.wait.Visible) +
                   " bg=" + p.backgroundReady + " box=" + (p.message != null ? p.message.Text : "-") + " account=" + p.account?.Text + " save=" + p.saveId;
        }
    }
}
