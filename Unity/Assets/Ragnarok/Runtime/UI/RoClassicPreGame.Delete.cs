using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The last step of a character delete in this client (langtype 1, checked live in the original 10-05): the Delete
    /// button 0xcf (shown once the deletion date has passed) asks CSV 0x13, then FUN_009c03b0 runs UIEmailAddressWnd
    /// (ctor 0x824c00, OnCreate 0x83a980, OnDraw 0x84ce80, SendMsg 0x85a1d0): 280 × 48 at (scaledX(185), scaledY(180)), the
    /// sysbox frame (0x809d50), CSV 0x12b "Please enter the deletion password." at (15, 6), an edit at (15, 22) W − 77 wide
    /// (+0x84 '*' mask, +0x88 49 bytes, #E8E8E8) and OK (btn_ok, id 0xb4, the default button) at (W − 55, H − 28). OK sends
    /// mode 0x271c with the text and CLoginMode state 0xb sends CH_DELETE_CHAR2 0x01fb (0xcb4a61), which Hercules checks
    /// against the account's e-mail. While it runs the selection gives way to "Please wait..."; HC_REFUSE_DELETECHAR shows
    /// CSV 0x12d over the wait box and the selection comes back on OK; HC_ACCEPT_DELETECHAR just brings the list back, the
    /// first slot selected either way.
    /// </summary>
    public sealed partial class RoClassicPreGame {
        private const int DeletePasswordW = 280, DeletePasswordH = 48, DeletePasswordOkId = 0xb4;
        private RoClassicWindow deletePassword;
        private RoClassicEditField deletePasswordEdit;
        private int deletePasswordCharacter;
        private bool deleteSent, deleteRefusalOpen;

        private void ShowDeletionPassword(int characterId) {
            deletePasswordCharacter = characterId;
            if (deletePassword == null) {
                var layout = new RoClassicWindowLayout { Class = "UIEmailAddressWnd", Width = DeletePasswordW, Height = DeletePasswordH };
                var ok = new RoClassicChildLayout { Class = "UIBitmapButton", Id = DeletePasswordOkId, X = DeletePasswordW - 55, Y = DeletePasswordH - 28, W = 42, H = 20 };
                ok.States["normal"] = "유저인터페이스\\btn_ok.bmp";
                ok.States["hover"] = "유저인터페이스\\btn_ok_a.bmp";
                ok.States["pressed"] = "유저인터페이스\\btn_ok_b.bmp";
                layout.Children.Add(ok);
                deletePassword = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 17 };
                deletePasswordEdit = new RoClassicEditField {
                    Rect = new RectInt(15, 22, DeletePasswordW - 77, 16),
                    MaxBytes = 49,
                    PasswordMask = '*',
                    Background = new Color32(0xe8, 0xe8, 0xe8, 255)
                };
                deletePassword.AddEditField(deletePasswordEdit);
                deletePasswordEdit.Submitted += _ => ConfirmDeletionPassword();
                deletePasswordEdit.Cancelled += _ => HideDeletionPassword();
                deletePassword.Clicked += child => {
                    if (child.Id == DeletePasswordOkId) ConfirmDeletionPassword();
                };
                deletePassword.DrawDynamic += w => {
                    w.DrawSysBox("유저인터페이스\\sysbox_", 0, 0, w.Width, w.Height, false);
                    w.TextOut(15, 6, RoClassicUiHost.MsgStr(0x12b, "Please enter the deletion password."), 0, 12, new Color32(0, 0, 0, 255));
                };
            }

            Vector2Int client = system.ClientSize;
            deletePassword.MoveTo((client.x - 640) / 2 + 185, (client.y - 480) / 2 + 180);
            deletePasswordEdit.SetText(string.Empty);
            deletePassword.Visible = true;
            system.Windows.BringToFront(deletePassword);
            deletePasswordEdit.Focus();
            deletePassword.Invalidate();
        }

        /// <summary>SendMsg 0x85a1d0 msg 6 / 0xb4: the text to the mode (0x271c), the modal ends with 0xb4 and the delete goes out.</summary>
        private void ConfirmDeletionPassword() {
            if (deletePassword == null || !deletePassword.Visible) return;
            string key = deletePasswordEdit.Text;
            HideDeletionPassword();
            deleteSent = true;
            shell.DeleteCharacterFromPhase11Ui(deletePasswordCharacter, key);
        }

        private void HideDeletionPassword() {
            if (deletePassword == null || !deletePassword.Visible) return;
            if (deletePasswordEdit.HasFocus) RoClassicTextInput.Focus(null);
            deletePassword.Visible = false;
        }

        /// <summary>
        /// Refresh: a delete answered brings the first slot back; a refusal keeps the wait box with CSV 0x12d until OK. True
        /// while the selection has to stay away for it.
        /// </summary>
        private bool TrackDeleteAnswer(RoLoginShellSnapshot snapshot, RoLoginShellStage stage) {
            if (deleteSent && stage == RoLoginShellStage.CharacterSelect) {
                deleteSent = false;
                selSlot = 0;
                selPage = 0;
                selButtonState = -1;
                if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.ErrorText)) {
                    deleteRefusalOpen = true;
                    message?.Close();
                    message = RoClassicMessageBox.Show(
                        RoClassicUiHost.MsgStr(0x12d, "Character Deletion has failed because you have entered an incorrect e-mail address."),
                        RoClassicMessageBox.TypeOk,
                        true,
                        _ => {
                            message = null;
                            deleteRefusalOpen = false;
                            seenSequence = int.MinValue;
                        });
                }
            } else if (stage != RoLoginShellStage.CharacterSelect && stage != RoLoginShellStage.LoadingCharacters) {
                deleteSent = deleteRefusalOpen = false;
                HideDeletionPassword();
            }

            return deleteRefusalOpen;
        }
    }
}
