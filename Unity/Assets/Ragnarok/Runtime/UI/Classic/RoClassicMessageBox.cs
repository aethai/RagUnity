using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// UIMessageBox (ui/RUNTIME-RULES-STORE-SHOP.md §1), drawn from the oracle's 280×120 layouts: a text viewer at
    /// (10, 22) wrapping at 37 bytes and the type's buttons. A modal box (0x9c07e0) takes the mouse (no other window
    /// nor the world gets it), Enter clicks its default button, Esc answers <see cref="IdNo"/> whatever the type; the
    /// game keeps running, so the caller gets the answer through a callback instead of the client's blocking loop.
    /// </summary>
    public sealed class RoClassicMessageBox {
        /// <summary>Type 0 OK.</summary>
        public const int IdOk = 0xb4;
        /// <summary>Type 4 Cancel; also what a modal call answers at once while another modal box is open.</summary>
        public const int IdCancel = 0xb5;
        /// <summary>Type 2 OK (and the timeout).</summary>
        public const int IdYes = 0xb7;
        /// <summary>Type 2 Cancel and Esc.</summary>
        public const int IdNo = 0xb8;

        public const int TypeOk = 0;
        public const int TypeOkCancel = 2;
        public const int TypeCancel = 4;

        private const string LayoutRoot = "Ragnarok/Generated/OriginalClient/ui/UIMessageBox.";
        private const int VisibleLines = 3; // 65 px viewer, 18 px lines

        private static readonly List<RoClassicMessageBox> modal = new List<RoClassicMessageBox>();
        private static readonly List<(int Start, int End)> wrapScratch = new List<(int, int)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => modal.Clear();

        private readonly RoClassicWindow window;
        private readonly RoClassicChildLayout viewer;
        private readonly Action<int> onResult;
        private readonly int enterId;
        private readonly bool isModal;
        private int lineCount;
        private int openedFrame;

        /// <summary>True while a modal box is open: gameplay keys and the mouse belong to it.</summary>
        public static bool ModalOpen => modal.Count > 0;

        /// <summary>The topmost modal box's window (the only one taking the mouse), or null.</summary>
        public static RoClassicWindow ModalWindow => modal.Count > 0 ? modal[modal.Count - 1].window : null;

        public RoClassicWindow Window => window;

        public string Text { get; private set; }

        private RoClassicMessageBox(RoClassicUiSystem system, RoClassicWindowLayout layout, int type, bool defaultYes, bool isModal, Action<int> onResult) {
            this.onResult = onResult;
            this.isModal = isModal;
            enterId = type == TypeOkCancel ? (defaultYes ? IdYes : IdNo) : type == TypeOk ? IdOk : IdCancel;
            foreach (RoClassicChildLayout child in layout.Children) {
                if (child.Class == "UITextViewer") viewer = child;
            }

            window = new RoClassicWindow(system, layout);
            window.Clicked += child => Answer(child.Id);
            window.Wheeled += (w, local, delta) => Scroll(delta > 0 ? -1 : 1);
        }

        /// <summary>
        /// Opens a box (MessageBoxModal 0x9c07e0 / MessageBox 0x9d2b20) at ((W − 640)/2 + 185, (H − 480)/2 + 180) and
        /// returns it; <paramref name="onResult"/> gets the button id. A modal call while a modal box is open answers
        /// <see cref="IdCancel"/> at once and opens nothing.
        /// </summary>
        public static RoClassicMessageBox Show(string text, int type, bool defaultYes, Action<int> onResult, bool isModal = true, string title = null) {
            if (isModal && ModalOpen) {
                onResult?.Invoke(IdCancel);
                return null;
            }

            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            string state = type == TypeOkCancel ? "type2_okcancel" : type == TypeCancel ? "type4_cancel" : "type0_ok";
            var asset = system != null ? Resources.Load<TextAsset>(LayoutRoot + state) : null;
            if (asset == null) {
                onResult?.Invoke(IdCancel);
                return null;
            }

            RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
            if (title != null) {
                foreach (RoClassicOp op in layout.Ops) {
                    if (op.Kind == RoClassicOpKind.Text) op.Text = title; // the title bar's only text
                }
            }

            // the client drops the mouse capture and ends editing in the focused field first
            RoClassicTextInput.Focus(null);
            var box = new RoClassicMessageBox(system, layout, type, defaultYes, isModal, onResult);
            box.SetText(text);
            box.openedFrame = Time.frameCount;
            Vector2Int client = system.ClientSize;
            box.window.MoveTo((client.x - 640) / 2 + 185, (client.y - 480) / 2 + 180);
            system.Windows.BringToFront(box.window);
            if (isModal) modal.Add(box);
            return box;
        }

        /// <summary>Replaces the text (msg 0x39); the view goes back to the top.</summary>
        public void SetText(string text) {
            text = (text ?? string.Empty).Replace("\r", string.Empty);
            Text = text;
            window.Set("Message", text);
            int wrap = (int?) viewer?.Props?["wrapBytes"] ?? 37;
            RoClassicWindow.WrapBytes(text, wrap, wrapScratch);
            lineCount = wrapScratch.Count;
            SetScroll(0);
        }

        private void Scroll(int delta) => SetScroll(((int?) viewer?.Props?["scroll"] ?? 0) + delta);

        private void SetScroll(int scroll) {
            if (viewer?.Props == null) return;
            scroll = Mathf.Clamp(scroll, 0, Math.Max(0, lineCount - VisibleLines));
            if ((int?) viewer.Props["scroll"] == scroll) return;
            viewer.Props["scroll"] = scroll;
            window.Invalidate();
        }

        /// <summary>
        /// A button: a modal box always closes with the id; a non-modal one closes on OK / Yes / No and ignores its
        /// lone Cancel (0xb5), which never closes it by itself.
        /// </summary>
        private void Answer(int id) {
            if (!isModal && id == IdCancel) return;
            Close();
            onResult?.Invoke(id);
        }

        public void Close() {
            modal.Remove(this);
            window.Destroy();
        }

        /// <summary>
        /// Keys while a modal box is open (0x9bcff0 / 0x9d3830): Enter = the default button, Esc = <see cref="IdNo"/>.
        /// Called once per frame by the classic UI system, before the text fields.
        /// </summary>
        public static void Process() {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (modal.Count == 0 || keyboard == null) return;
            RoClassicMessageBox top = modal[modal.Count - 1];
            if (top.openedFrame == Time.frameCount) return; // the key that opened it is not its answer
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) top.Answer(top.enterId);
            else if (keyboard.escapeKey.wasPressedThisFrame) top.Answer(IdNo);
#endif
        }

        /// <summary>Diagnostics: answers the top modal box as if <paramref name="id"/> was clicked (−1 = Enter, −2 = Esc).</summary>
        public static bool AnswerTop(int id) {
            if (modal.Count == 0) return false;
            RoClassicMessageBox top = modal[modal.Count - 1];
            top.Answer(id == -1 ? top.enterId : id == -2 ? IdNo : id);
            return true;
        }

        /// <summary>Diagnostics: the open modal boxes, topmost last.</summary>
        public static string Describe() {
            if (modal.Count == 0) return "no modal box";
            var s = new System.Text.StringBuilder();
            foreach (RoClassicMessageBox box in modal) {
                s.Append('[').Append(box.window.Position.x).Append(',').Append(box.window.Position.y).Append(" enter=0x")
                    .Append(box.enterId.ToString("x")).Append(" lines=").Append(box.lineCount).Append("] ").Append(box.Text).Append(' ');
            }

            return s.ToString().TrimEnd();
        }

        /// <summary>Closes every box without answering (leaving the map, classic UI switched off).</summary>
        public static void CloseAll() {
            for (int i = modal.Count - 1; i >= 0; i--) modal[i].Close();
        }
    }
}
