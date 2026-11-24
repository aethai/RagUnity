using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    /// <summary>
    /// The level of the skill waiting for a target, right of the cursor (hud-efst-banner-spec §4, 0xbf5a40): no skill name,
    /// only 이팩트\숫자 digits (action 0, frame = digit) scaled ×2 in 0xFAFFFFFF, the ones digit at (mouse x + 24 + 16·(n − 1),
    /// mouse y) and 16 px left per digit; nothing for the taming capture (level 0). Drawn after the windows and the cursor.
    /// </summary>
    public sealed class RoClassicSkillLevel : IRoClassicUiProducer {
        private const string Digits = "data/sprite/이팩트/숫자";
        private static readonly Color32 Tint = new Color32(255, 255, 255, 0xfa);
        private static RoClassicSkillLevel instance;
        internal static Vector2Int ForcedPointer = new Vector2Int(-1, -1);   // the sweep's mouse

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            ForcedPointer = new Vector2Int(-1, -1);
        }

        private RoClassicUiSystem system;
        private RoClassicUiLayer layer;
        private long shownKey = long.MinValue;

        /// <summary>Per frame from the chat window's classic ticks: the layer exists once the classic UI does.</summary>
        public static void Tick() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || (instance != null && instance.system == system)) return;
            instance = new RoClassicSkillLevel { system = system };
            instance.layer = system.Renderer.CreateLayer("skill level digits");
            instance.layer.AboveOverlayUi = true;
            instance.layer.Visible = false;
            system.Renderer.BringToFront(instance.layer);
            system.Register(instance);
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            RoSkillTargetSelection target = RoSkillTargetSelection.Current;
            Mouse mouse = Mouse.current;
            int level = target != null && target.HasActiveSelection && !target.CapturesTarget ? Mathf.Clamp(target.SkillLevel, 0, 9999) : 0;
            if (level == 0 || (mouse == null && ForcedPointer.x < 0)) {
                if (layer.Visible) layer.Visible = false;
                shownKey = long.MinValue;
                return;
            }

            Vector2Int p = ForcedPointer.x >= 0 ? ForcedPointer : ui.ScreenToClient(mouse.position.ReadValue());
            long key = ((long) level << 40) ^ ((long) (p.x & 0xfffff) << 20) ^ (p.y & 0xfffff);
            if (!layer.Visible) {
                layer.Visible = true;
                system.Renderer.BringToFront(layer);
            }

            if (key == shownKey) return;
            RoClassicActSprite digits = RoClassicActSprite.Get(Digits);
            if (digits == null || !digits.Ready) return;
            shownKey = key;
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            int n = level >= 1000 ? 4 : level >= 100 ? 3 : level >= 10 ? 2 : 1;
            for (int i = 0, v = level; i < n; i++, v /= 10) digits.Emit(q, 0, v % 10, p.x + 24 + 16 * (n - 1) - 16 * i, p.y, 2f, Tint);
            layer.MarkDirty();
        }
    }
}
