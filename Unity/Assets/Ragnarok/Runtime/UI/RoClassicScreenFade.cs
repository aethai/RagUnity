using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// A black full-screen fade over the whole classic UI (the same-map transition's fade out / in): one layer on top,
    /// redrawn only when its alpha or the screen size changes.
    /// </summary>
    public sealed class RoClassicScreenFade {
        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private float alpha;
        private int drawnAlpha = -1;
        private Vector2Int drawnFor;

        public float Alpha {
            get => alpha;
            set {
                alpha = Mathf.Clamp01(value);
                Redraw();
            }
        }

        public bool Visible => layer != null && layer.Visible;

        public void Show() {
            RoClassicUiSystem current = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
            if (current == null) return;
            if (layer == null || system != current) {
                system = current;
                layer = current.Renderer.CreateLayer("screen fade");
                layer.AboveOverlayUi = true;
                drawnAlpha = -1;
            }

            current.Renderer.BringToFront(layer);
            layer.Visible = true;
            Redraw();
        }

        public void Hide() {
            if (layer != null) layer.Visible = false;
        }

        private void Redraw() {
            if (layer == null || !layer.Visible || system == null) return;
            int a = Mathf.RoundToInt(alpha * 255f);
            Vector2Int client = system.ClientSize;
            if (a == drawnAlpha && client == drawnFor) return;
            drawnAlpha = a;
            drawnFor = client;
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            q.Image(0, 0, client.x, client.y, system.White, new Color32(0, 0, 0, (byte) a));
            layer.MarkDirty();
        }
    }
}
