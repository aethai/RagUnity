using System;
using System.Collections.Generic;
using System.IO;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The emblem Edit button 0x112 of the info tab (guild-emblem-castle-spec A.7, vf37 0x8c3040): btn_edit 42 × 20 at
    /// (W − w − 8, 0x45) for the master. A press lists &lt;client&gt;\emblem\*.bmp then *.gif in the 0x1a popup (160 wide,
    /// 16-px rows, at most 160 tall, at the window's (238, 87)); none → MSI 0xbdd in a "Caution" box. A pick must load as
    /// a 24 × 24 image and be at most 51 200 bytes (else MSI 0xe03), then goes to the web API upload (busy → MSI 0xe02).
    /// </summary>
    public sealed partial class RoClassicGuild {
        private const int EmblemEditId = 0x112, EmblemRowH = 16, EmblemRows = 10;
        private RoClassicWindow emblemList;
        private readonly List<string> emblemFiles = new List<string>();
        private int emblemFirst, emblemHover = -1;

        private void AddEmblemEdit(RoClassicWindowLayout layout) {
            const string edit = Ui + "btn_edit";
            var button = new RoClassicChildLayout {
                Class = "UIBitmapButton", Id = EmblemEditId, X = -200, Y = -200, W = 42, H = 20, Kind = "control", Path = "UIGuildInfoManageWnd/UIBitmapButton#emblem"
            };
            button.States["normal"] = edit + ".bmp";
            button.States["hover"] = edit + "_a.bmp";
            button.States["pressed"] = edit + "_b.bmp";
            layout.Children.Add(button);
        }

        /// <summary>Msg 0x3c's placement: (W − w − 8, 0x45) for the master (DAT_014efb44), parked otherwise.</summary>
        private void SyncEmblemEdit() {
            RoClassicWindow w = windows[TabInfo];
            RoClassicChildLayout button = w?.Layout.Children.Find(c => c.Id == EmblemEditId);
            if (button == null) return;
            bool shown = Master;
            button.X = shown ? w.Width - button.W - 8 : -200;
            button.Y = shown ? 0x45 : -200;
            w.Invalidate();
        }

        private static string EmblemFolder() {
            string root = RoClassicUiHost.GameAssets()?.ClientRootPath;
            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "emblem");
        }

        private void PressEmblemEdit() {
            CloseEmblemList();
            emblemFiles.Clear();
            string folder = EmblemFolder();
            if (folder != null && Directory.Exists(folder)) {
                foreach (string pattern in new[] { "*.bmp", "*.gif" }) {
                    var names = new List<string>();
                    foreach (string file in Directory.GetFiles(folder, pattern)) names.Add(Path.GetFileName(file));
                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    emblemFiles.AddRange(names);
                }
            }

            if (emblemFiles.Count == 0) {
                RoClassicMessageBox.Show(Msg(0xbdd, "The selected emblem isn't available, try placing it in 'emblem' folder then try again"),
                    RoClassicMessageBox.TypeOk, true, null, true, "Caution");
                return;
            }

            if (emblemList == null) {
                emblemList = new RoClassicWindow(system, new RoClassicWindowLayout { Class = "UIComboBoxWnd", WindowId = 0x1a, Width = 160, Height = 160 }) {
                    Visible = false, Draggable = false, ClickableAt = _ => true,
                };
                emblemList.DrawDynamic += DrawEmblemList;
                emblemList.Pressed += (w, local) => {
                    int k = emblemFirst + local.y / EmblemRowH;
                    if (k < emblemFiles.Count) PickEmblem(emblemFiles[k]);
                };
                emblemList.Wheeled += (w, local, delta) => {
                    emblemFirst = Mathf.Clamp(emblemFirst + (delta > 0 ? -1 : 1), 0, Math.Max(0, emblemFiles.Count - EmblemRows));
                    w.Invalidate();
                };
                emblemList.EscClose = () => {
                    CloseEmblemList();
                    return true;
                };
            }

            RoClassicWindow owner = windows[TabInfo];
            int h = Math.Min(EmblemRowH * emblemFiles.Count, EmblemRowH * EmblemRows);
            emblemList.Layout.Height = h;
            emblemList.SetHeight(h);
            emblemFirst = 0;
            emblemHover = -1;
            if (owner != null) emblemList.MoveTo(owner.Position.x + owner.Width - 0xa3 + 1, owner.Position.y + 0x56 + 1);
            emblemList.Visible = true;
            system.Windows.BringToFront(emblemList);
            system.Windows.PushEsc(emblemList);
            emblemList.Invalidate();
        }

        private void DrawEmblemList(RoClassicWindow w) {
            int width = w.Width, height = w.Height;
            w.FillRect(0, 0, width, height, White);
            w.DrawBitmapRaw(0, 0, Basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(0, height - 7, Basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(width - 7, 0, Basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(width - 7, height - 7, Basic + "sysboxs_rd.bmp");
            Vector2Int mouse = system.Windows.Mouse;
            emblemHover = system.Windows.HoverWindow == w ? emblemFirst + (mouse.y - w.Position.y) / EmblemRowH : -1;
            for (int r = 0; r < EmblemRows && emblemFirst + r < emblemFiles.Count; r++) {
                int k = emblemFirst + r;
                bool on = k == emblemHover;
                if (on) w.FillRect(2, EmblemRowH * r + 1, width - 4, EmblemRowH - 2, Selected);
                w.TextOut(4, EmblemRowH * r + 2, emblemFiles[k], 0, 12, on ? White : Black);
            }

            w.DrawScrollBar(width - 13, 0, height, Math.Max(0, emblemFiles.Count - EmblemRows), emblemFiles.Count, EmblemRows, emblemFirst);
        }

        private void CloseEmblemList() {
            if (emblemList == null || !emblemList.Visible) return;
            emblemList.Visible = false;
            system.Windows.RemoveEsc(emblemList);
        }

        /// <summary>Msg 0x27: the file must load as 24 × 24 and be at most 0xC800 bytes, then CEmblemDataMgr::Upload.</summary>
        private void PickEmblem(string name) {
            CloseEmblemList();
            string message = Msg(0xe03, "This file cannot be registered.");
            byte[] data = null;
            try {
                data = File.ReadAllBytes(Path.Combine(EmblemFolder() ?? string.Empty, name));
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            }

            if (data == null || !RoClassicGuildEmblems.TryDecodeImage(data, out int w, out int h, out _) || w != 24 || h != 24 || data.Length > 0xC800) {
                RoClassicMessageBox.Show(message, RoClassicMessageBox.TypeOk, true, null, true, "Message");
                return;
            }

            if (!RoClassicGuildEmblems.Upload(guild.GuildId, name, data)) {
                RoClassicMessageBox.Show(Msg(0xe02, "Please try again in a few minutes."), RoClassicMessageBox.TypeOk, true, null, true, "Message");
            }
        }

        /// <summary>Sweep: emblem edit | emblem pick name.</summary>
        private string EmblemSweep(string[] parts) {
            if (parts.Length > 1 && parts[1] == "edit") PressEmblemEdit();
            else if (parts.Length > 2 && parts[1] == "pick") PickEmblem(parts[2]);
            return "ver=" + guild.EmblemVersion + "/" + (guild.Info?.EmblemVersion ?? -1) + " files=[" + string.Join(",", emblemFiles) + "] list=" + (emblemList != null && emblemList.Visible) + " folder=" + EmblemFolder() +
                " webapi=" + RoClassicWebApi.BaseAddress(session) + " token=" + (session != null && !string.IsNullOrEmpty(session.WebAuthToken));
        }
    }
}
