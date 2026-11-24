using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>Screen anchor of an overhead element: integer pixels, origin top-left.</summary>
    public interface IRoClassicAnchorSource {
        bool TryGetClassicAnchor(out int x, out int y);
    }

    /// <summary>
    /// HP/SP gauges over actors drawn exactly like the original client's <c>UIPcGage::OnDraw</c> (0x7ed970) and
    /// <c>UIMonsterGage::OnDraw</c> (0x7ed850): a background fill, "empty" fills, then HP/SP fills whose widths use
    /// the original integer/float arithmetic. Colours come from the constructors (<c>UIGage</c> 0x7ce800,
    /// <c>UIMonsterGage</c> 0x7cf360; COLORREF, converted to RGB by <c>CDCBitmap::FillRect</c> 0x5253a0).
    /// All gauges share one layer at the bottom of the z-order; each follows its own GPU anchor, so moving actors
    /// never re-uploads geometry — only value or visibility changes rebuild the layer.
    /// </summary>
    public sealed class RoClassicActorGauges : IRoClassicUiProducer {
        public const int Width = 60;
        public const int HpOnlyHeight = 5;
        public const int HpSpHeight = 9;
        public const int RechargeHeight = 6;

        public enum GaugeKind {
            Pc,
            Monster,
            /// <summary>UIRechargeGage (cast progress): OnDraw 0x7ee8e0, UIGage colours, cur/max from 0 / 100.</summary>
            Recharge,
        }

        // UIGage::UIGage (0x7ce800): +0x7c background, +0x80 empty, +0x84 HP, +0x88 SP, +0x8c low HP
        private static readonly Color32 PcBackground = new Color32(0x14, 0x19, 0x9b, 0xff);
        private static readonly Color32 PcEmpty = new Color32(0x44, 0x44, 0x44, 0xff);
        private static readonly Color32 PcHp = new Color32(0x16, 0xea, 0x25, 0xff);
        private static readonly Color32 PcSp = new Color32(0x1d, 0x65, 0xdb, 0xff);
        private static readonly Color32 PcLowHp = new Color32(0xea, 0x16, 0x16, 0xff);

        // UIMonsterGage::UIMonsterGage (0x7cf360): overrides +0x7c/+0x80/+0x84/+0x8c, low-HP threshold +0xb0 = 25
        private static readonly Color32 MonsterBackground = new Color32(0x10, 0x18, 0x9c, 0xff);
        private static readonly Color32 MonsterEmpty = new Color32(0x42, 0x41, 0x42, 0xff);
        private static readonly Color32 MonsterHp = new Color32(0xff, 0x00, 0xe7, 0xff);
        private static readonly Color32 MonsterLowHp = new Color32(0xff, 0xff, 0x00, 0xff);
        private const float MonsterLowHpPercent = 25f;

        public sealed class Gauge {
            internal IRoClassicAnchorSource Source;
            internal int Anchor;
            internal GaugeKind Kind;
            internal int Hp;
            internal int MaxHp;
            internal int Sp = -1;
            internal int MaxSp;
            internal bool Shown;
            /// <summary>What the gauge draws (kind, fill widths, colours): only its change rebuilds the layer.</summary>
            internal long VisualKey = long.MinValue;
            internal bool Released;
            internal RoClassicActorGauges Owner;

            public bool HasSp => Sp >= 0 && MaxSp > 0;
            public int Height => Kind == GaugeKind.Recharge ? RechargeHeight : HasSp ? HpSpHeight : HpOnlyHeight;

            public void Set(bool monster, int hp, int maxHp, int sp, int maxSp) {
                GaugeKind kind = monster ? GaugeKind.Monster : GaugeKind.Pc;
                if (Shown && Kind == kind && Hp == hp && MaxHp == maxHp && Sp == sp && MaxSp == maxSp) return;
                Kind = kind;
                Hp = hp;
                MaxHp = maxHp;
                Sp = sp;
                MaxSp = maxSp;
                Shown = hp >= 0 && maxHp > 0;
                MarkIfVisualChanged();
            }

            /// <summary>Cast progress in the gauge's integer units (the client feeds elapsed / total).</summary>
            public void SetRecharge(int current, int maximum) {
                if (Shown && Kind == GaugeKind.Recharge && Hp == current && MaxHp == maximum) return;
                Kind = GaugeKind.Recharge;
                Hp = current;
                MaxHp = maximum;
                Sp = -1;
                MaxSp = 0;
                Shown = maximum > 0;
                // the cast bar is fed the elapsed milliseconds every frame: the layer is rebuilt when the fill moves a pixel
                MarkIfVisualChanged();
            }

            public void Hide() {
                if (!Shown) return;
                Shown = false;
                MarkIfVisualChanged();
            }

            private void MarkIfVisualChanged() {
                long key = ResolveVisualKey(this);
                if (key == VisualKey) return;
                VisualKey = key;
                Owner.geometryDirty = true;
            }

            public void Release() {
                if (Released) return;
                Released = true;
                Owner.Remove(this);
            }
        }

        private static RoClassicActorGauges instance;
        private readonly List<Gauge> gauges = new List<Gauge>();
        private RoClassicUiLayer layer;
        private RoClassicUiSystem system;
        private bool geometryDirty = true;

        public static RoClassicActorGauges Instance {
            get {
                if (instance == null || instance.system == null) {
                    instance = new RoClassicActorGauges();
                    instance.system = RoClassicUiSystem.Ensure(UnityEngine.Camera.main);
                    instance.layer = instance.system.Renderer.CreateLayer("actor gauges");
                    instance.system.Renderer.SendToBack(instance.layer);
                    instance.system.Register(instance);
                }

                return instance;
            }
        }

        /// <summary>
        /// 0xbd43d0 asks at every draw: true hides every monster gauge (/monsterhp off, or the interface hidden by F11 —
        /// mgr +0x50c == 2). Read once a tick, the gauges only collapse their anchors.
        /// </summary>
        public static Func<bool> MonsterGaugesHidden { get; set; }

        /// <summary>Called once a tick before the anchors are read (the host drops its per-frame camera data there).</summary>
        public static Action BeforeAnchors { get; set; }

        public Gauge Create(IRoClassicAnchorSource source) {
            var gauge = new Gauge { Source = source, Owner = this, Anchor = system.Renderer.AllocateAnchor() };
            gauges.Add(gauge);
            return gauge;
        }

        private void Remove(Gauge gauge) {
            if (gauges.Remove(gauge)) {
                system.Renderer.FreeAnchor(gauge.Anchor);
                geometryDirty = true;
            }
        }

        public void ClassicUiTick(RoClassicUiSystem ui) {
            RoClassicUiRenderer renderer = ui.Renderer;
            bool monstersHidden = MonsterGaugesHidden != null && MonsterGaugesHidden();
            BeforeAnchors?.Invoke();
            for (int i = 0; i < gauges.Count; i++) {
                Gauge g = gauges[i];
                int x = 0, y = 0;
                bool visible = g.Shown && !(monstersHidden && g.Kind == GaugeKind.Monster) &&
                    g.Source != null && g.Source.TryGetClassicAnchor(out x, out y);
                // UIGage placement (CGameActor::vf02 0xbd86c0): centred on the actor, integer pixels
                renderer.SetAnchor(g.Anchor, x - Mathf.RoundToInt(Width * renderer.Scale) / 2, y, visible); // centred at any UI scale
            }

            if (!geometryDirty) return;
            geometryDirty = false;
            RoClassicUiQuads q = layer.Quads;
            q.Clear();
            RoClassicUiImage white = ui.White;
            for (int i = 0; i < gauges.Count; i++) {
                Gauge g = gauges[i];
                if (!g.Shown) continue;
                q.Anchor = g.Anchor;
                switch (g.Kind) {
                    case GaugeKind.Monster: EmitMonster(q, white, g); break;
                    case GaugeKind.Recharge: EmitRecharge(q, white, g); break;
                    default: EmitPc(q, white, g); break;
                }
            }

            q.Anchor = 0;
            layer.MarkDirty();
        }

        /// <summary>The fill widths and colours the Emit* functions draw for <paramref name="g"/> (their own arithmetic).</summary>
        private static long ResolveVisualKey(Gauge g) {
            if (!g.Shown) return -1;
            long fill1 = -1, fill2 = -1, colour = 0;
            switch (g.Kind) {
                case GaugeKind.Monster:
                    if (g.MaxHp > 0) {
                        float pct = g.Hp * 100f / g.MaxHp;
                        if (pct > 0f) {
                            fill1 = (int) ((Width - 2) * pct / 100f);
                            colour = MonsterLowHpPercent <= pct ? 1 : 2;
                        }
                    }

                    break;
                case GaugeKind.Recharge:
                    if (g.MaxHp != 0) {
                        float pct = g.Hp * 100 / g.MaxHp;
                        if (pct != 0f) fill1 = (int) (Width * pct / 100f - 2f);
                    }

                    break;
                default:
                    if (!g.HasSp) {
                        if (g.MaxHp != 0) {
                            float pct = g.Hp * 100 / g.MaxHp;
                            if (pct != 0f) {
                                fill1 = (int) (Width * pct / 100f - 2f);
                                colour = pct >= 25f ? 1 : 2;
                            }
                        }

                        break;
                    }

                    if (g.MaxHp == 0 || g.MaxSp == 0) {
                        colour = 3;
                        break;
                    }

                    float hpPct = g.Hp * 100 / g.MaxHp;
                    float spPct = g.Sp * 100 / g.MaxSp;
                    if (hpPct != 0f) {
                        fill1 = (int) (Width * hpPct / 100f - 2f);
                        colour = hpPct >= 25f ? 1 : 2;
                    }

                    if (spPct != 0f) fill2 = (int) (Width * spPct / 100f - 2f);
                    colour |= 4;
                    break;
            }

            unchecked {
                return ((long) g.Kind << 56) ^ (colour << 48) ^ ((fill1 & 0xffffff) << 24) ^ (fill2 & 0xffffff);
            }
        }

        /// <summary>UIWindow fill (0x9ac310): clamps to the window rectangle, draws nothing for empty rectangles.</summary>
        private static void Fill(RoClassicUiQuads q, in RoClassicUiImage white, int w, int h, int x, int y, int fw, int fh, Color32 c) {
            int x0 = Mathf.Max(x, 0), y0 = Mathf.Max(y, 0);
            int x1 = Mathf.Min(x + fw, w), y1 = Mathf.Min(y + fh, h);
            if (x1 <= x0 || y1 <= y0) return;
            q.Image(x0, y0, x1 - x0, y1 - y0, white, c);
        }

        private static void EmitPc(RoClassicUiQuads q, in RoClassicUiImage white, Gauge g) {
            int w = Width, h = g.Height;
            Fill(q, white, w, h, 0, 0, w, h, PcBackground);
            if (!g.HasSp) {
                Fill(q, white, w, h, 1, 1, w - 2, h - 2, PcEmpty);
                if (g.MaxHp != 0) {
                    float pct = g.Hp * 100 / g.MaxHp; // integer division, then float (as the original)
                    if (pct != 0f) Fill(q, white, w, h, 1, 1, (int) (w * pct / 100f - 2f), h - 2, pct >= 25f ? PcHp : PcLowHp);
                }

                return;
            }

            int bar = (h - 3) / 2;
            Fill(q, white, w, h, 1, 1, w - 2, bar, PcEmpty);
            Fill(q, white, w, h, 1, h / 2 + 1, w - 2, bar, PcEmpty);
            if (g.MaxHp == 0 || g.MaxSp == 0) return;
            float hpPct = g.Hp * 100 / g.MaxHp;
            float spPct = g.Sp * 100 / g.MaxSp;
            if (hpPct != 0f) Fill(q, white, w, h, 1, 1, (int) (w * hpPct / 100f - 2f), bar, hpPct >= 25f ? PcHp : PcLowHp);
            if (spPct != 0f) Fill(q, white, w, h, 1, h / 2 + 1, (int) (w * spPct / 100f - 2f), bar, PcSp);
        }

        private static void EmitRecharge(RoClassicUiQuads q, in RoClassicUiImage white, Gauge g) {
            int w = Width, h = RechargeHeight;
            Fill(q, white, w, h, 0, 0, w, h, PcBackground);
            Fill(q, white, w, h, 1, 1, w - 2, h - 2, PcEmpty);
            if (g.MaxHp == 0) return;
            float pct = g.Hp * 100 / g.MaxHp;
            if (pct != 0f) Fill(q, white, w, h, 1, 1, (int) (w * pct / 100f - 2f), h - 2, PcHp);
        }

        private static void EmitMonster(RoClassicUiQuads q, in RoClassicUiImage white, Gauge g) {
            int w = Width, h = HpOnlyHeight;
            Fill(q, white, w, h, 0, 0, w, h, MonsterBackground);
            Fill(q, white, w, h, 1, 1, w - 2, h - 2, MonsterEmpty);
            if (g.MaxHp <= 0) return;
            float pct = g.Hp * 100f / g.MaxHp;
            if (pct > 0f) {
                Fill(q, white, w, h, 1, 1, (int) ((w - 2) * pct / 100f), h - 2, MonsterLowHpPercent <= pct ? MonsterHp : MonsterLowHp);
            }
        }
    }
}
