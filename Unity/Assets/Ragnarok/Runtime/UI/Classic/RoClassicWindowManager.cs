using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI.Classic {
    /// <summary>Something carried by a classic drag (a hotkey slot, an item...): the bitmap that follows the cursor.</summary>
    public interface IRoClassicDragPayload {
        /// <summary>Texture path (relative to data\texture\) drawn under the cursor while dragging.</summary>
        string IconPath { get; }
    }

    /// <summary>
    /// Mouse handling of classic windows, after <c>UIWindowMgr</c>: hit test from the top of the z-order (0x9c2400),
    /// press brings the window to the front, bitmap buttons go normal → hover → pressed and fire on release over the
    /// same button, frame windows drag from their title bar (or the area a window declares) at alpha 0x80 (restored
    /// on release) and snap to the screen edges and, with /window on, to other windows within ±10 px (FUN_009167a0). Windows
    /// also get double clicks, right clicks, content drags with the payload's icon under the cursor, and drops coming from
    /// the legacy UGUI.
    /// </summary>
    public sealed class RoClassicWindowManager {
        public const int TitleBarHeight = 17;
        public const int SnapDistance = 10;

        /// <summary>
        /// /window ([0x01552d48], "window docking"): only then does 0x9dba40 put the windows into the dock map (mgr+0x194)
        /// that RefreshSnap 0x9c1c10 walks after the screen edges, so with it off a window snaps to the screen only. Set by
        /// the game.
        /// </summary>
        public static bool SnapToWindows { get; set; }
        private const byte DragAlpha = 0x80;
        private const float DoubleClickSeconds = 0.5f; // GetDoubleClickTime() default
        private const int DoubleClickSlop = 4;
        private const int ContentDragThreshold = 3;

        private readonly List<RoClassicWindow> windows = new List<RoClassicWindow>();
        private readonly RoClassicUiSystem system;
        private RoClassicWindow hoverWindow;
        private int hoverChild = -1;
        private RoClassicWindow pressWindow;
        private int pressChild = -1;
        private RoClassicWindow dragWindow;
        private Vector2Int dragOffset;

        private RoClassicWindow lastClickWindow;
        private Vector2Int lastClickPoint;
        private float lastClickTime = -10f;

        private RoClassicWindow contentSource;
        private IRoClassicDragPayload contentPayload;
        private Vector2Int contentStart;
        private bool contentDragging;
        private RoClassicUiLayer dragLayer;

        private RoClassicWindow resizeWindow;
        private int resizeMode;
        private Vector2Int resizeStart;
        private Vector2Int resizeLastMouse;
        private Vector2Int resizeStartSize;

        // the Esc list (mgr+0x18): [0] is the front
        private readonly List<RoClassicWindow> escList = new List<RoClassicWindow>();

        public RoClassicWindowManager(RoClassicUiSystem system) => this.system = system;

        public IReadOnlyList<RoClassicWindow> Windows => windows;

        /// <summary>The mouse in client pixels (top-left origin), as of the last <see cref="Process"/>.</summary>
        public Vector2Int Mouse { get; private set; } = new Vector2Int(-1, -1);

        /// <summary>The topmost window under the mouse, as of the last <see cref="Process"/>.</summary>
        public RoClassicWindow HoverWindow => hoverWindow;

        /// <summary>The payload of the classic drag in progress, if any.</summary>
        public IRoClassicDragPayload Dragging => contentDragging ? contentPayload : null;

        /// <summary>
        /// mgr+0x4efe8: the window a left press last landed on (0x9d5240, null for a press on no window; the client's
        /// MakeWindow 0x9c8020 sets it too, which has no single counterpart here); PageUp / PageDown go to it as msg 3 / 4
        /// (0x9d3830).
        /// </summary>
        public RoClassicWindow ActiveWindow { get; private set; }

        /// <summary>The window being dragged by its frame, if any.</summary>
        public RoClassicWindow DragWindow => dragWindow;

        /// <summary>
        /// A frame drag started by code while the left button is held (another window's LBtnDown handing the capture over,
        /// e.g. the chat's msg 0x75 → the new window's UIFrameWnd::LBtnDown): the window follows the mouse at
        /// <paramref name="offset"/> (the grab point inside it) at alpha 0x80 until the button goes up.
        /// </summary>
        public void BeginDrag(RoClassicWindow window, Vector2Int offset) {
            if (window == null) return;
            if (dragWindow != null && dragWindow != window) dragWindow.Alpha = 0xff;
            dragWindow = window;
            dragOffset = offset;
            window.Alpha = DragAlpha;
            pressWindow = null;
            pressChild = -1;
            ActiveWindow = window;
        }

        /// <summary>
        /// The mouse is over something that takes a click (a button, a check box, a link...): the client shows cursor 2,
        /// the "click" hand, there (UIButton / UITextButton OnMouseHover → SetCursor 0xa04cc0); plain window areas keep 0.
        /// </summary>
        public bool HoverClickable =>
            resizeWindow != null || (hoverWindow != null && (hoverChild >= 0 ||
                (hoverWindow.ClickableAt != null && hoverWindow.ClickableAt(Mouse - hoverWindow.Position)) ||
                (hoverWindow.ResizerModeAt != null && hoverWindow.ResizerModeAt(Mouse - hoverWindow.Position) >= 0)));

        public void Add(RoClassicWindow window) {
            if (!windows.Contains(window)) windows.Add(window);
        }

        public void Remove(RoClassicWindow window) {
            windows.Remove(window);
            if (hoverWindow == window) hoverWindow = null;
            if (pressWindow == window) pressWindow = null;
            if (dragWindow == window) dragWindow = null;
            if (dockDragWindow == window || dockGroup.Contains(window)) EndDockDrag();
            if (lastClickWindow == window) lastClickWindow = null;
            if (ActiveWindow == window) ActiveWindow = null;
            if (contentSource == window) EndContentDrag();
            if (resizeWindow == window) resizeWindow = null;
            escList.Remove(window);
        }

        /// <summary>
        /// MakeWindow and a click on a frame window put it at the front of the Esc list (0x9c5e20), unless it is
        /// Esc-excluded (no <see cref="RoClassicWindow.EscClose"/>).
        /// </summary>
        public void PushEsc(RoClassicWindow window) {
            if (window == null || window.EscClose == null) return;
            escList.Remove(window);
            escList.Insert(0, window);
        }

        /// <summary>DeleteWindow's removal from the Esc list (0x9bfd50).</summary>
        public void RemoveEsc(RoClassicWindow window) => escList.Remove(window);

        /// <summary>
        /// Esc (0x9d3830 case 0x1b, after the message box and the text fields): the front window of the Esc list takes it;
        /// false when nothing did (the caller then toggles the Esc menu). Windows hidden since they were pushed drop out
        /// unless they hold the list while hidden.
        /// </summary>
        public bool HandleEscape() {
            if (RoClassicMessageBox.ModalOpen) return true; // a modal box answers Esc itself (0xb8)
            while (escList.Count > 0) {
                RoClassicWindow front = escList[0];
                if (front.EscClose == null || (!front.Visible && !front.EscHoldsWhileHidden)) {
                    escList.RemoveAt(0);
                    continue;
                }

                bool used = front.EscClose();
                if (!front.Visible && !front.EscHoldsWhileHidden) escList.Remove(front);
                return used;
            }

            return false;
        }

        public void BringToFront(RoClassicWindow window) {
            if (window != null && window.HiddenByCloseTabs) window.CloseTabsSetShow(true);   // MakeWindow shows it again
            int i = windows.IndexOf(window);
            if (i < 0 || i == windows.Count - 1) return;
            windows.RemoveAt(i);
            windows.Add(window);
            foreach (RoClassicUiLayer under in window.UnderLayers) system.Renderer.BringToFront(under);
            system.Renderer.BringToFront(window.Layer);
        }

        private static readonly Color32 TooltipWhite = new Color32(255, 255, 255, 255);

        /// <summary>
        /// UIButton::vf28 0x7c0720 on every hover move: a close button (0xc5) of any window but the navigation (0xcb), and the
        /// minimize button (0xc6) of Basic Info (0), take the parent window's hotkey (GetBeHaviorOfWindow) as their text; another
        /// minimize button has none. A non-empty text shows at the cursor (0xa03bf0).
        /// </summary>
        private static void ShowButtonTooltip(RoClassicWindow window, int childIndex, Vector2Int mouse) {
            List<RoClassicChildLayout> children = window.Layout.Children;
            if (childIndex >= children.Count) return;
            RoClassicChildLayout child = children[childIndex];
            string text = window.ChildTooltip?.Invoke(child) ?? child.Tooltip;
            int windowId = window.Layout.WindowId;
            if ((child.Id == 0xc5 || child.Id == 0xc6) && windowId != 0xcb) {
                text = child.Id == 0xc6 && windowId != 0 ? string.Empty : RoClassicUiSystem.WindowHotkeyText?.Invoke(windowId) ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(text)) RoClassicTooltip.Shared.Show(text, TooltipWhite, mouse.x, mouse.y - 20);
        }

        /// <summary>The topmost visible window under a top-left-origin client pixel.</summary>
        public RoClassicWindow WindowAt(int x, int y) {
            for (int i = windows.Count - 1; i >= 0; i--) {
                RoClassicWindow w = windows[i];
                if (w.Visible && w.Contains(x, y)) return w;
            }

            return null;
        }

        /// <summary>Diagnostics (!ptr): the parts of <see cref="HitTest"/> at a Unity screen point.</summary>
        public string DescribeHit(Vector2 unityScreenPoint) {
            Vector2Int p = system.ScreenToClient(unityScreenPoint);
            RoClassicWindow w = WindowAt(p.x, p.y);
            return "client=" + p + " modal=" + RoClassicMessageBox.ModalOpen + " drag=" + (dragWindow != null) + " resize=" + (resizeWindow != null) +
                " content=" + contentDragging + " window=" + (w != null ? w.Layout.Class + " " + w.Layout.WindowId : "-");
        }

        /// <summary>True when a Unity screen point (bottom-left origin) is over a classic window.</summary>
        public bool HitTest(Vector2 unityScreenPoint) {
            Vector2Int p = system.ScreenToClient(unityScreenPoint);
            // a modal message box takes the whole screen's mouse (0x9d5240)
            return RoClassicMessageBox.ModalOpen || dragWindow != null || resizeWindow != null || contentDragging || WindowAt(p.x, p.y) != null;
        }

        /// <summary>Re-emits every window whose data, child state, alpha or position changed.</summary>
        public void RefreshAll() {
            // windows stay on screen when it shrinks or the UI scale grows (client size = screen / scale)
            Vector2Int client = system.ClientSize;
            for (int i = 0; i < windows.Count; i++) {
                RoClassicWindow w = windows[i];
                int x = Mathf.Clamp(w.Position.x, 0, Mathf.Max(0, client.x - w.Width));
                int y = Mathf.Clamp(w.Position.y, 0, Mathf.Max(0, client.y - w.Height));
                if (x != w.Position.x || y != w.Position.y) w.MoveTo(x, y);
                w.Refresh();
            }
        }

        public void Process() {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            // everything here is in client pixels (screen / UI scale), like the window layouts
            Vector2Int client = system.ClientSize;
            int screenW = client.x, screenH = client.y;
            Vector2Int p = system.ScreenToClient(mouse.position.ReadValue());
            int x = p.x, y = p.y;
            Mouse = p;

            if (dragWindow != null) {
                if (mouse.leftButton.isPressed) {
                    // LBtnDown 0x819510 -> 0x73f6b0: Basic Info takes the windows docked to it along (positions as at the press)
                    if (dockDragWindow != dragWindow) CaptureDockGroup(dragWindow);
                    if (dockGroup.Count > 0) {
                        MoveDockGroup(x - dragOffset.x, y - dragOffset.y, screenW, screenH);
                        return;
                    }

                    Vector2Int target = Snap(dragWindow, x - dragOffset.x, y - dragOffset.y, screenW, screenH);
                    dragWindow.MoveTo(target.x, target.y);
                    return;
                }

                dragWindow.Alpha = 0xff;
                dragWindow = null;
                EndDockDrag();
            }

            if (resizeWindow != null) {
                if (mouse.leftButton.isPressed) {
                    // OnMouseMove 0x7c0c80: only when the mouse moved, the start size plus the delta per mode
                    if (p != resizeLastMouse) {
                        resizeLastMouse = p;
                        int dx = x - resizeStart.x, dy = y - resizeStart.y, w = resizeStartSize.x, h = resizeStartSize.y;
                        switch (resizeMode) {
                            case 0: w -= dx; break;
                            case 1: w += dx; break;
                            case 2: h -= dy; break;
                            case 3: h += dy; break;
                            case 4: w -= dx; h -= dy; break;
                            case 5: w += dx; h -= dy; break;
                            case 6: w -= dx; h += dy; break;
                            case 7: w += dx; h += dy; break;
                        }

                        resizeWindow.RaiseResizeRequested(resizeMode, w, h);
                    }

                    return;
                }

                // LBtnUp 0x7c0570: the parent's RefreshSnap (±10 px to the edges and the other windows), capture released
                Vector2Int snapped = Snap(resizeWindow, resizeWindow.Position.x, resizeWindow.Position.y, screenW, screenH);
                resizeWindow.MoveTo(snapped.x, snapped.y);
                resizeWindow = null;
            }

            bool releasedFromDrag = false;
            if (contentSource != null) {
                if (mouse.leftButton.isPressed) {
                    if (!contentDragging && (Mathf.Abs(x - contentStart.x) > ContentDragThreshold || Mathf.Abs(y - contentStart.y) > ContentDragThreshold)) {
                        contentDragging = true;
                    }

                    if (contentDragging) DrawDragIcon(p);
                } else {
                    releasedFromDrag = contentDragging;
                    FinishContentDrag(p);
                }
            }

            RoClassicWindow over = WindowAt(x, y);
            RoClassicWindow modalBox = RoClassicMessageBox.ModalWindow;
            if (modalBox != null) {
                // 0x9d5240: while a modal box is on top only it (and its children) gets the mouse
                if (over != modalBox) over = null;
                if (windows.Count > 0 && windows[windows.Count - 1] != modalBox) BringToFront(modalBox);
            }

            int child = over != null ? over.ChildAt(x, y) : -1;
            if (over != hoverWindow || child != hoverChild) {
                if (hoverWindow != null && hoverChild >= 0) hoverWindow.SetChildState(hoverChild, "normal");
                hoverWindow = over;
                hoverChild = child;
                if (over != null && child >= 0) over.SetChildState(child, pressWindow == over && pressChild == child ? "pressed" : "hover");
            }

            if (hoverWindow != null && hoverChild >= 0 && dragWindow == null && !contentDragging) ShowButtonTooltip(hoverWindow, hoverChild, p);

            float wheel = mouse.scroll.ReadValue().y;
            if (wheel != 0f && over != null) over.RaiseWheeled(p - over.Position, wheel > 0f ? 1 : -1);

            if (mouse.leftButton.wasPressedThisFrame) ActiveWindow = over;
            if (mouse.leftButton.wasPressedThisFrame && over != null) {
                BringToFront(over);
                Vector2Int local = p - over.Position;
                int resizer = over.ResizerModeAt != null ? over.ResizerModeAt(local) : -1;
                if (resizer >= 0) {
                    // UIResizer LBtnDown 0x7c0280: capture, the mouse and the parent's size at the press
                    resizeWindow = over;
                    resizeMode = resizer;
                    resizeStart = resizeLastMouse = p;
                    resizeStartSize = new Vector2Int(over.Width, over.Height);
                    return;
                }

                bool doubleClick = over == lastClickWindow && Time.unscaledTime - lastClickTime <= DoubleClickSeconds &&
                    Mathf.Abs(p.x - lastClickPoint.x) <= DoubleClickSlop && Mathf.Abs(p.y - lastClickPoint.y) <= DoubleClickSlop;
                lastClickWindow = doubleClick ? null : over;
                lastClickPoint = p;
                lastClickTime = Time.unscaledTime;
                pressWindow = over;
                pressChild = child;
                PushEsc(over); // UIFrameWnd::LBtnDown → the Esc list front
                RoClassicEditField edit = child < 0 ? over.EditFieldAt(x, y) : null;
                if (child < 0 && edit == null) over.RaisePressed(local);
                if (edit != null) {
                    edit.Focus(); // LBtnDown 0x7c01c0: focus, which selects all (no caret placement by mouse)
                    if (edit.ForwardsPressToParent && over.FrameDragArea != null && over.FrameDragArea(local)) {
                        dragWindow = over;
                        dragOffset = new Vector2Int(x - over.Position.x, y - over.Position.y);
                        over.Alpha = DragAlpha;
                    }
                } else if (child >= 0) {
                    over.SetChildState(child, "pressed");
                } else if (doubleClick) {
                    over.RaiseDoubleClicked(local);
                } else {
                    IRoClassicDragPayload payload = over.DragSourceAt?.Invoke(local);
                    if (payload != null) {
                        contentSource = over;
                        contentPayload = payload;
                        contentStart = p;
                        contentDragging = false;
                    } else if (over.FrameDragArea != null ? over.FrameDragArea(local) : over.Draggable && local.y < TitleBarHeight) {
                        dragWindow = over;
                        dragOffset = new Vector2Int(x - over.Position.x, y - over.Position.y);
                        over.Alpha = DragAlpha;
                    }
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && over != null) over.RaiseRightClicked(p - over.Position);

            if (mouse.leftButton.wasReleasedThisFrame && pressWindow != null) {
                RoClassicWindow released = pressWindow;
                int releasedChild = pressChild;
                pressWindow = null;
                pressChild = -1;
                if (releasedChild >= 0) {
                    released.SetChildState(releasedChild, over == released && child == releasedChild ? "hover" : "normal");
                    if (over == released && child == releasedChild) released.RaiseClicked(releasedChild);
                } else if (over == released && !releasedFromDrag && dragWindow == null) {
                    released.RaiseReleased(p - released.Position);
                }
            }
#endif
        }

        /// <summary>Drop on the window under the cursor; released outside every window, the source hears about it.</summary>
        private void FinishContentDrag(Vector2Int p) {
            if (contentDragging) {
                RoClassicWindow target = WindowAt(p.x, p.y);
                if (target != null) target.RaiseDropped(p - target.Position, contentPayload);
                else contentSource.RaiseDraggedOutside(contentPayload);
            }

            EndContentDrag();
        }

        private void EndContentDrag() {
            contentSource = null;
            contentPayload = null;
            contentDragging = false;
            if (dragLayer != null) dragLayer.Visible = false;
        }

        /// <summary>The payload's bitmap centred on the cursor, in a layer above every window.</summary>
        private void DrawDragIcon(Vector2Int p) {
            if (dragLayer == null) {
                dragLayer = system.Renderer.CreateLayer("drag");
                dragLayer.AboveOverlayUi = true;
            }

            if (!dragLayer.Visible) {
                system.Renderer.BringToFront(dragLayer);
                dragLayer.Visible = true;
            }

            RoClassicUiQuads q = dragLayer.Quads;
            q.Clear();
            if (contentPayload.IconPath != null && system.Images.TryGet(contentPayload.IconPath, out RoClassicUiImage icon)) {
                q.Image(p.x - icon.Width / 2, p.y - icon.Height / 2, icon, new Color32(255, 255, 255, 255));
            }

            dragLayer.MarkDirty();
        }

        /// <summary>Snaps a dragged window to the screen edges and (/window on) to other windows' edges within ±10 px.</summary>
        private Vector2Int Snap(RoClassicWindow w, int x, int y, int screenW, int screenH) {
            int ww = w.Width, wh = w.Height;
            x = SnapAxis(x, ww, 0, screenW);
            y = SnapAxis(y, wh, 0, screenH);
            if (!SnapToWindows) return new Vector2Int(x, y);
            foreach (RoClassicWindow other in windows) {
                // 0x9c1c10: the windows of the group dragged with Basic Info are never snap targets of the group
                if (other == w || !other.Visible || IsInDockDrag(other)) continue;
                int ox = other.Position.x, oy = other.Position.y, ow = other.Width, oh = other.Height;
                bool overlapY = y < oy + oh + SnapDistance && y + wh > oy - SnapDistance;
                bool overlapX = x < ox + ow + SnapDistance && x + ww > ox - SnapDistance;
                if (overlapY) {
                    if (Mathf.Abs(x - (ox + ow)) <= SnapDistance) x = ox + ow;
                    else if (Mathf.Abs(x + ww - ox) <= SnapDistance) x = ox - ww;
                }

                if (overlapX) {
                    if (Mathf.Abs(y - (oy + oh)) <= SnapDistance) y = oy + oh;
                    else if (Mathf.Abs(y + wh - oy) <= SnapDistance) y = oy - wh;
                }
            }

            return new Vector2Int(x, y);
        }

        // ---- the window group of Basic Info (the dock graph at 0x011572b4) ----

        /// <summary>The root of the group (MakeWindow(0) registers it with 0x73f550).</summary>
        public const string DockRootClass = "UIBasicInfoWnd";

        /// <summary>The windows MakeWindow registers as group members (0x73f600): item, equipment, status, shortcut, ...</summary>
        private static readonly int[] DockMemberIds = { 0x08, 0x0a, 0x0b, 0x24, 0x8b, 0xb8, 0x141 };

        /// <summary>The members whose height changes move the windows docked under them (0x73ff60's callers).</summary>
        private static readonly int[] DockPushIds = { 0x08, 0x0a, 0x0b };

        private RoClassicWindow dockDragWindow;
        private readonly List<RoClassicWindow> dockGroup = new List<RoClassicWindow>();
        private readonly List<Vector2Int> dockSaved = new List<Vector2Int>();
        private readonly List<RoClassicWindow> dockScratch = new List<RoClassicWindow>();
        private readonly List<RectInt> dockScratchRects = new List<RectInt>();
        private readonly List<int> dockMoving = new List<int>();
        private Vector2Int dockRootSaved;

        public static bool IsDockRoot(RoClassicWindow w) => w != null && w.Layout.Class == DockRootClass;

        public static bool IsDockMember(RoClassicWindow w) =>
            w != null && !IsDockRoot(w) && IsDockMemberId(w.Layout.WindowId);

        /// <summary>The window ids MakeWindow adds to the group with 0x73f600.</summary>
        public static bool IsDockMemberId(int windowId) => windowId != 0 && System.Array.IndexOf(DockMemberIds, windowId) >= 0;

        private static bool IsDockNode(RoClassicWindow w) => IsDockRoot(w) || IsDockMember(w);

        private static RectInt RectOf(RoClassicWindow w) => new RectInt(w.Position.x, w.Position.y, w.Width, w.Height);

        private bool IsInDockDrag(RoClassicWindow w) => dockGroup.Count > 0 && (w == dockDragWindow || dockGroup.Contains(w));

        private void EndDockDrag() {
            dockDragWindow = null;
            dockGroup.Clear();
            dockSaved.Clear();
        }

        /// <summary>
        /// 0x73f700: where <paramref name="b"/> touches <paramref name="a"/> - 0 its right edge on a's left one, 2 its left
        /// edge on a's right one, 1 its bottom on a's top, 3 its top on a's bottom (the other axis' ranges overlapping, ends
        /// included); -1 when they do not touch. Touching windows are linked in the graph (0x740450).
        /// </summary>
        public static int DockSide(RectInt a, RectInt b) {
            if (a.x == b.x + b.width && Touch(a.y, a.y + a.height, b.y, b.y + b.height)) return 0;
            if (b.x == a.x + a.width && Touch(a.y, a.y + a.height, b.y, b.y + b.height)) return 2;
            if (a.y == b.y + b.height && Touch(a.x, a.x + a.width, b.x, b.x + b.width)) return 1;
            if (b.y == a.y + a.height && Touch(a.x, a.x + a.width, b.x, b.x + b.width)) return 3;
            return -1;
        }

        private static bool Touch(int a0, int a1, int b0, int b1) => a0 <= b1 && b0 <= a1;

        /// <summary>
        /// 0x73f6b0 at the press on Basic Info: every group window connected to it through touching windows (0x73fef0,
        /// the depth-first 0x73fc20) keeps its position (SaveOriginalPos); a member dragged on its own moves alone.
        /// </summary>
        private void CaptureDockGroup(RoClassicWindow window) {
            EndDockDrag();
            dockDragWindow = window;
            if (!IsDockRoot(window)) return;
            dockRootSaved = window.Position;
            dockScratch.Clear();
            dockScratch.Add(window);
            for (int i = 0; i < dockScratch.Count; i++) {
                RectInt at = RectOf(dockScratch[i]);
                foreach (RoClassicWindow other in windows) {
                    if (!other.Visible || !IsDockMember(other) || dockScratch.Contains(other)) continue;
                    if (DockSide(at, RectOf(other)) < 0) continue;
                    dockScratch.Add(other);
                    dockGroup.Add(other);
                    dockSaved.Add(other.Position);
                }
            }
        }

        /// <summary>
        /// OnMouseMove 0x819840 for Basic Info with its group: every window moves by the mouse's delta, each one's snap
        /// (screen edges and, with /window, the windows outside the group) is taken (0x73f9a0 keeps the largest per axis,
        /// Basic Info's own wins only when larger) and the whole group moves by it (0x740320).
        /// </summary>
        private void MoveDockGroup(int x, int y, int screenW, int screenH) {
            RoClassicWindow root = dockDragWindow;
            int dx = x - dockRootSaved.x, dy = y - dockRootSaved.y;
            Vector2Int rootTarget = Snap(root, x, y, screenW, screenH);
            Vector2Int snap = Vector2Int.zero;
            for (int i = 0; i < dockGroup.Count; i++) {
                int mx = dockSaved[i].x + dx, my = dockSaved[i].y + dy;
                Vector2Int target = Snap(dockGroup[i], mx, my, screenW, screenH);
                snap = DockLargerSnap(snap, new Vector2Int(target.x - mx, target.y - my));
            }

            snap = DockLargerSnap(snap, new Vector2Int(rootTarget.x - x, rootTarget.y - y));
            root.MoveTo(x + snap.x, y + snap.y);
            for (int i = 0; i < dockGroup.Count; i++) dockGroup[i].MoveTo(dockSaved[i].x + dx + snap.x, dockSaved[i].y + dy + snap.y);
        }

        /// <summary>0x73f9a0 / 0x819840: per axis the snap of larger size wins, the earlier one on a tie.</summary>
        public static Vector2Int DockLargerSnap(Vector2Int kept, Vector2Int candidate) =>
            new Vector2Int(Mathf.Abs(kept.x) < Mathf.Abs(candidate.x) ? candidate.x : kept.x,
                Mathf.Abs(kept.y) < Mathf.Abs(candidate.y) ? candidate.y : kept.y);

        /// <summary>
        /// 0x73ff60(window, 0, height change, 3), which the item, equipment and status windows call when their height
        /// changes (minimize / restore, the bag's rows): the group windows docked under it follow, or — when it grows
        /// upward — the window keeps its bottom edge and the ones docked over it move (<see cref="PlanDockHeightPush"/>).
        /// </summary>
        internal void PushDockedOnHeightChange(RoClassicWindow window, int oldHeight) {
            if (window == null || !window.Visible || System.Array.IndexOf(DockPushIds, window.Layout.WindowId) < 0) return;
            if (window.Height == oldHeight) return;
            dockScratch.Clear();
            dockScratchRects.Clear();
            int node = -1, root = -1;
            foreach (RoClassicWindow other in windows) {
                if (!other.Visible || !IsDockNode(other)) continue;
                if (other == window) node = dockScratch.Count;
                else if (IsDockRoot(other)) root = dockScratch.Count;
                dockScratch.Add(other);
                dockScratchRects.Add(other == window ? new RectInt(window.Position.x, window.Position.y, window.Width, oldHeight) : RectOf(other));
            }

            if (node < 0) return;
            int dy = PlanDockHeightPush(dockScratchRects, node, root, window.Height, system.ClientSize.y, dockMoving);
            for (int i = 0; i < dockMoving.Count; i++) {
                RoClassicWindow moved = dockScratch[dockMoving[i]];
                moved.MoveTo(moved.Position.x, moved.Position.y + dy);
            }
        }

        /// <summary>
        /// 0x73ff60 on the group's rectangles (<paramref name="node"/> the window whose height changed, still at its old
        /// height; <paramref name="root"/> Basic Info's index or −1): fills <paramref name="moving"/> with the windows that
        /// move and returns how far (y). Normally the windows linked under it (side 3), and theirs, move by the height
        /// change (0x740150). It grows upward instead — the window itself moves by −Δh so its bottom edge stays, and the
        /// windows over it (side 1), and theirs, move by −Δh — when the stack over it does not reach the screen's top
        /// (y − the heights of the chain over it ≠ 0) and either the stack under it ends on the screen's bottom (y + old
        /// height + the heights of the chain under it = screen height) or it is over Basic Info (in its chain of windows
        /// above, 0x73fd40). A chain sums one window per step: the last one linked on that side with a height (0x73faa0).
        /// </summary>
        public static int PlanDockHeightPush(List<RectInt> rects, int node, int root, int newHeight, int screenHeight, List<int> moving) {
            moving.Clear();
            RectInt at = rects[node];
            int dy = newHeight - at.height;
            if (dy == 0) return 0;
            int side = 3;
            bool belowTop = at.y - DockChainHeight(rects, node, 1) != 0;
            bool onBottom = at.y + at.height + DockChainHeight(rects, node, 3) == screenHeight;
            if (belowTop && (onBottom || (root >= 0 && root != node && IsDockAbove(rects, root, node)))) {
                side = 1;
                dy = -dy;
                moving.Add(node);
            }

            // 0x740150: every window linked on that side, then theirs, each once (the node itself is already visited)
            int start = moving.Count;
            for (int q = -1; q < moving.Count - start; q++) {
                int from = q < 0 ? node : moving[start + q];
                for (int j = 0; j < rects.Count; j++) {
                    if (j == node || moving.Contains(j) || DockSide(rects[from], rects[j]) != side) continue;
                    moving.Add(j);
                }
            }

            return dy;
        }

        /// <summary>0x73faa0 without the start: the heights of the chain on <paramref name="side"/> (1 over, 3 under).</summary>
        private static int DockChainHeight(List<RectInt> rects, int start, int side) {
            int sum = 0, at = start;
            for (int steps = 0; steps < rects.Count; steps++) {
                int next = -1;
                for (int j = 0; j < rects.Count; j++) {
                    if (j != at && rects[j].height >= 1 && DockSide(rects[at], rects[j]) == side) next = j;
                }

                if (next < 0) break;
                sum += rects[next].height;
                at = next;
            }

            return sum;
        }

        /// <summary>0x73fd40 from Basic Info's links over it: <paramref name="node"/> is in its chain of windows above.</summary>
        private static bool IsDockAbove(List<RectInt> rects, int at, int node, int depth = 0) {
            if (depth >= rects.Count) return false;
            for (int j = 0; j < rects.Count; j++) {
                if (j == at || DockSide(rects[at], rects[j]) != 1) continue;
                if (j == node || IsDockAbove(rects, j, node, depth + 1)) return true;
            }

            return false;
        }

        private static int SnapAxis(int pos, int size, int min, int max) {
            if (Mathf.Abs(pos - min) <= SnapDistance) return min;
            if (Mathf.Abs(pos + size - max) <= SnapDistance) return max - size;
            return pos;
        }
    }
}
