using System;
using System.Collections.Generic;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The two 280 × 150 equipment pickers (codex-ui-research RUNTIME-RULES-ITEM-SERVICES.md "Repair 0x6b" and "0x6f —
    /// Whitesmith weapon refine selection"): UIItemRepairWnd 0x6b (title MsgStr 0x32c "Repairable items") and
    /// UIWeaponRefineWnd 0x6f (0x38e "Upgradeable weapons"), both at ((W − 640) / 2 + 185, H·300/480 − 60) with the
    /// UIListBox (12, 22, 256, 95; rows 16 px, selection #CADFFB) of each listed item's name and OK / Cancel.
    /// Repair: OK with a selection sends CZ_REQ_ITEMREPAIR2 with that record, OK on an empty list and Cancel send index −1,
    /// ZC_ACK_ITEMREPAIR prints 0x32d (0x00FFFF00) or 0x32e (red). Weapon refine: OK sends CZ_REQ_WEAPONREFINE with the
    /// listed index, Cancel 0xffffffff; ZC_ACK_WEAPONREFINE (0x00c6c480) prints 0x38f / 0x390 / 0x391 / 0x392 with the item's
    /// name in COLORREF 0xffff00 / 0xcdcd00 / 0xc8c8ff / 0xc8c8ff. The same frame is UIMakeTargetListWnd 0x4f
    /// ("Manufacturing List", MsgStr 0x1a9) for ZC_MAKABLEITEMLIST: OK hands the chosen item to the materials window 0x50
    /// at this window's place (vf37 0x905d80), Cancel sends CZ_REQMAKINGITEM with item 0. OK without a selection does
    /// nothing. An empty list shows MsgStr 0x1a8 instead of the window. Ticked by the chat window while the classic UI is on.
    /// </summary>
    public sealed class RoClassicRepair : IRoClassicUiProducer {
        public const int Repair = 0x6b;
        public const int WeaponRefine = 0x6f;
        public const int MakeList = 0x4f;
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const int OkId = 0xb4;
        private const int CancelId = 0xb5;
        private const int RowH = 16;
        private static readonly RectInt List = new RectInt(12, 22, 256, 95);
        private static readonly Color32 ListBg = new Color32(0xf0, 0xf0, 0xf0, 255);
        private static readonly Color32 SelectedBg = new Color32(0xca, 0xdf, 0xfb, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private sealed class Picker {
            public int Id;
            public RoClassicWindow Window;
            public readonly List<RoRepairItem> Items = new List<RoRepairItem>();
            public readonly List<string> Names = new List<string>();
            public int Selected = -1;
            public int Top;
            public int SeenList = -1;
            public int SeenResult = -1;
        }

        private static RoClassicRepair instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoItemMetadataTable metadata = RoItemMetadataTable.Empty;
        private readonly Picker repair = new Picker { Id = Repair };
        private readonly Picker refine = new Picker { Id = WeaponRefine };
        private readonly Picker make = new Picker { Id = MakeList };

        public static void Tick(RoLoginShellSession session, RoClassicChat chat, RoItemMetadataTable metadata) {
            if (session == null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) {
                instance = new RoClassicRepair { system = system };
                system.Register(instance);
            }

            RoClassicRepair r = instance;
            (RoZcRepairItemList repairList, int repairSerial, int repairResult, int repairResultSerial) = session.RepairSnapshot();
            (RoZcRepairItemList refineList, int refineSerial, RoZcAckWeaponRefine refineResult, int refineResultSerial) = session.WeaponRefineSnapshot();
            (RoZcMakableItemList makeList, int makeSerial, _, _) = session.MakingSnapshot();
            if (!ReferenceEquals(r.session, session)) {
                r.session = session;
                r.repair.SeenList = repairSerial;
                r.repair.SeenResult = repairResultSerial;
                r.refine.SeenList = refineSerial;
                r.refine.SeenResult = refineResultSerial;
                r.make.SeenList = makeSerial;
            }

            r.chat = chat;
            r.metadata = metadata ?? RoItemMetadataTable.Empty;
            if (repairSerial != r.repair.SeenList) {
                r.repair.SeenList = repairSerial;
                r.Open(r.repair, repairList);
            }

            if (repairResultSerial != r.repair.SeenResult) {
                r.repair.SeenResult = repairResultSerial;
                // 0x01fe (0xc32d0d): 0 → MsgStr 0x32d in 0x00FFFF00, 1 → 0x32e in red
                if (repairResult == 0) r.Chat(RoClassicUiHost.MsgStr(0x32d, "Item has been successfully repaired."), "#00FFFF");
                else if (repairResult == 1) r.Chat(RoClassicUiHost.MsgStr(0x32e, "You have failed to repair this item. Please check the distance between you and opponent."), "#FF0000");
            }

            if (refineSerial != r.refine.SeenList) {
                r.refine.SeenList = refineSerial;
                r.Open(r.refine, refineList);
            }

            if (refineResultSerial != r.refine.SeenResult) {
                r.refine.SeenResult = refineResultSerial;
                r.RefineResult(refineResult);
            }

            if (makeSerial != r.make.SeenList) {
                r.make.SeenList = makeSerial;
                // 0x00c6f890: each 16-byte record contributes its item id
                var made = new RoRepairItem[makeList.ItemIds?.Length ?? 0];
                for (int i = 0; i < made.Length; i++) made[i] = new RoRepairItem(0, makeList.ItemIds[i], 0, null);
                r.Open(r.make, new RoZcRepairItemList(made));
            }
        }

        /// <summary>0x00c6c480: the item's name in the result's message; results past 3 print nothing.</summary>
        private void RefineResult(RoZcAckWeaponRefine result) {
            int message;
            string fallback, color;
            switch (result.Result) {
                case 0:
                    (message, fallback, color) = (0x38f, "Refined weapon: %s", "#00FFFF");
                    break;
                case 1:
                    (message, fallback, color) = (0x390, "Refined weapon: %s", "#00CDCD");
                    break;
                case 2:
                    (message, fallback, color) = (0x391, "You cannot upgrade %s until you level up your Upgrade Weapon skill.", "#FFC8C8");
                    break;
                case 3:
                    (message, fallback, color) = (0x392, "%s is required to upgrade this weapon.", "#FFC8C8");
                    break;
                default:
                    return;
            }

            string name = RoItemInstance.CreateRegular(0, unchecked((int) result.ItemId), 1).GetFullDisplayName(metadata);
            Chat(RoClassicUiHost.MsgStr(message, fallback).Replace("%s", name), color);
        }

        private void Chat(string text, string color) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, color);
        }

        private void Open(Picker p, RoZcRepairItemList list) {
            p.Items.Clear();
            p.Names.Clear();
            foreach (RoRepairItem item in list.Items ?? Array.Empty<RoRepairItem>()) {
                p.Items.Add(item);
                p.Names.Add(p.Id == MakeList ? RoItemInstance.CreateRegular(0, unchecked((int) item.ItemId), 1).GetFullDisplayName(metadata) : Name(item));
            }

            if (p.Items.Count == 0) {
                // 0xc32b9b / FUN_00c71710: an empty list shows a message instead of the window
                if (p.Window != null) p.Window.Visible = false;
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x1a8, "You can't create items yet."), RoClassicMessageBox.TypeOk, true, _ => { });
                return;
            }

            if (p.Window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(
                    Resources.Load<TextAsset>(Layouts + (p.Id == WeaponRefine ? "UIWeaponRefineWnd" : p.Id == MakeList ? "UIMakeTargetListWnd" : "UIItemRepairWnd")).text);
                layout.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIListBox") child.Visible = false; // drawn per state
                }

                RoClassicWindow window = new RoClassicWindow(system, layout) { Visible = false };
                window.DrawDynamic += w => Draw(p, w);
                window.Clicked += child => {
                    // the list shares id 0xb4 with OK: only the bitmap button is a click here
                    if (child.Class != "UIBitmapButton") return;
                    if (child.Id == OkId) Ok(p);
                    else if (child.Id == CancelId) Send(p, -1);
                };
                window.Pressed += (w, local) => {
                    if (!List.Contains(local)) return;
                    int i = (local.y - List.y) / RowH + p.Top;
                    p.Selected = i < p.Names.Count ? i : -1;
                    w.Invalidate();
                };
                window.Wheeled += (w, local, delta) => {
                    p.Top = Mathf.Clamp(p.Top + (delta > 0 ? -1 : 1), 0, Math.Max(0, p.Names.Count - List.height / RowH));
                    w.Invalidate();
                };
                p.Window = window;
            }

            p.Selected = -1;
            p.Top = 0;
            Vector2Int client = system.ClientSize;
            p.Window.MoveTo(Mathf.Clamp((client.x - 640) / 2 + 185, 0, Math.Max(0, client.x - p.Window.Width)),
                Mathf.Clamp(client.y * 300 / 480 - 60, 0, Math.Max(0, client.y - p.Window.Height)));
            p.Window.Visible = true;
            system.Windows.BringToFront(p.Window);
            p.Window.Invalidate();
        }

        /// <summary>Msg 0x1f: the label as FUN_0065d630 builds it (refine and name).</summary>
        private string Name(RoRepairItem item) {
            RoItemInstance instance = RoItemInstance.CreateEquipment(item.Index, unchecked((int) item.ItemId), 1, true, refineLevel: item.Refine,
                cardIds: Array.ConvertAll(item.Cards, c => unchecked((int) c)));
            return instance.GetFullDisplayName(metadata);
        }

        private static void Draw(Picker p, RoClassicWindow w) {
            string title = p.Id == WeaponRefine ? RoClassicUiHost.MsgStr(0x38e, "Upgradeable weapons")
                : p.Id == MakeList ? RoClassicUiHost.MsgStr(0x1a9, "Manufacturing List")
                : RoClassicUiHost.MsgStr(0x32c, "Repairable items");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            w.FillRect(List.x, List.y, List.width, List.height, ListBg);
            w.ClipTo(List.x, List.y, List.width, List.height);
            int rows = List.height / RowH;
            for (int i = p.Top; i < p.Names.Count && i < p.Top + rows; i++) {
                int y = List.y + RowH * (i - p.Top);
                if (i == p.Selected) w.FillRect(List.x, y, List.width, 15, SelectedBg);
                w.TextOut(List.x + 3, y, p.Names[i], 0, 12, Black);
            }

            if (p.Names.Count > rows) w.DrawScrollBar(List.x + List.width - 13, List.y, List.height, p.Names.Count - rows, p.Names.Count, rows, p.Top);
            w.ResetClip();
        }

        /// <summary>
        /// Msg 6 / 0xb4 (repair vf37 0x905880, refine vf37 0x906740): a selection sends its record, no selection nothing,
        /// the empty repair list −1.
        /// </summary>
        private void Ok(Picker p) {
            if (p.Items.Count == 0) {
                Send(p, -1);
                return;
            }

            if (p.Selected < 0 || p.Selected >= p.Items.Count) return;
            Send(p, p.Selected);
        }

        private void Send(Picker p, int row) {
            if (p.Window != null) p.Window.Visible = false;
            if (session == null) return;
            if (p.Id == MakeList) {
                if (row >= 0) RoClassicProduce.Open(session, metadata, p.Items[row].ItemId, p.Window.Position);
                else _ = session.BeginLegacyMakingItem(0, null);
            } else if (p.Id == WeaponRefine) {
                _ = session.BeginLegacyWeaponRefine(row >= 0 ? p.Items[row].Index : -1);
            } else {
                _ = session.BeginLegacyItemRepair(row >= 0 ? p.Items[row] : (RoRepairItem?) null);
            }
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
        }

        /// <summary>The sweep console: [refine|make] pick i | ok | cancel | state (the repair list unless "refine" / "make" leads).</summary>
        public static string Sweep(string command) {
            RoClassicRepair r = instance;
            if (r == null) return "no repair list";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            Picker p = r.repair;
            int at = 0;
            if (a.Length > 0 && (a[0] == "refine" || a[0] == "make")) {
                p = a[0] == "make" ? r.make : r.refine;
                at = 1;
            }

            switch (a.Length > at ? a[at] : string.Empty) {
                case "pick":
                    if (int.TryParse(a.Length > at + 1 ? a[at + 1] : string.Empty, out int i) && i < p.Names.Count) {
                        p.Selected = i;
                        p.Window?.Invalidate();
                    }

                    break;
                case "ok":
                    r.Ok(p);
                    break;
                case "cancel":
                    r.Send(p, -1);
                    break;
            }

            return "0x" + p.Id.ToString("x") + " visible=" + (p.Window != null && p.Window.Visible) + " selected=" + p.Selected + " items=" + string.Join(" | ", p.Names);
        }
    }
}
