using System.Collections.Generic;
using Ragnarok.Entities;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Effects.Classic {
    /// <summary>
    /// Draws the effect looks the original client's actor renderers apply (reference packages/actorfx/ACTOR_LOOK.md):
    /// effects write a flag word into their owner (+0xe8) and the renderer draws extra copies of every sprite layer —
    /// additive layers, glows, aura rings, flashes — or bends the layer (scale, lift, squash, shake). The flag word and
    /// its counters come from the ported engine (<see cref="FxWorld.GetActorLook"/>, which also marks the actor rendered
    /// so the word resets on the next tick like the client's); the renderer-owned phase counters (+0xee aura, +0xef
    /// ring, +0xed weapon flicker) advance here once per layer per rendered frame, as in the client.
    /// Pixel sizes S(x) are sprite pixels at the default zoom (x · 400 / zoom in the client).
    /// </summary>
    public sealed class RoClassicActorLookRenderer {
        const float PixelsPerUnit = SPR.PIXELS_PER_UNIT;
        const string SpriteShaderName = "Ragnarok/RO Sprite Unlit";
        const int BehindOffset = -1;
        const int FrontOffset = 1;

        // bits of the owner's flag word (+0xe8)
        const uint FlagAdditive = 0x1, FlagAuraBlade = 0x2, FlagWeaponFlicker = 0x4, FlagAura = 0x8, FlagShrink = 0x10,
            FlagSquash = 0x20, FlagSpin = 0x40, FlagLift = 0x80, FlagFlash = 0x100, FlagReflect = 0x200, FlagGrow = 0x400,
            FlagAsura = 0x800, FlagFourWay = 0x1000, FlagShake = 0x2000, FlagBlueGlow = 0x4000, FlagJump = 0x10000,
            FlagLand = 0x20000, FlagRedGlow = 0x40000, FlagGreenAura = 0x80000;

        sealed class ActorState {
            public Transform Anchor;
            public RoActorLayeredVisual Layered;
            public RoActorSpriteVisual Single;
            public Transform Root;
            /// <summary>A renderer of the actor, resolved once, for the sprite height (lift / spin pivots).</summary>
            public Renderer HeightRenderer;
            public Vector3 BaseScale;
            public Vector3 BaseLocalPosition;
            public Quaternion BaseLocalRotation;
            public bool Bent;
            public int AuraPhase;     // +0xee, 0..180
            public int RingPhase;     // +0xef, 0..200
            public bool WeaponToggle; // +0xed
            public int LastSeenFrame;
            public uint LastFlags;
            public int Copies;
            public bool Reported;
            public uint AppliedTint = 0xffffffff;
        }

        struct Layer {
            public Mesh Mesh;
            public Matrix4x4 LocalToWorld;
            public Texture Texture;
            public float VerticalOffset;
            public int Queue;
            public int Index;        // the client's layer slot: 0 body, 1 head, 2..4 accessories, 5 weapon, 6+ others
            public Bounds Bounds;
        }

        readonly Dictionary<uint, ActorState> actors = new Dictionary<uint, ActorState>();
        readonly List<uint> stale = new List<uint>();
        readonly List<Layer> layers = new List<Layer>();
        readonly Dictionary<(bool additive, int queue), Material> materials = new Dictionary<(bool, int), Material>();
        /// <summary>Created on first use: the renderer lives in a MonoBehaviour field, where Unity objects cannot be made.</summary>
        MaterialPropertyBlock block;
        readonly System.Random shakeRandom = new System.Random(0x5eed);
        Shader shader;
        ActorState currentState;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int VPosId = Shader.PropertyToID("_VPos");

        /// <summary>
        /// One rendered frame: for every actor the engine knows, decode its look (marks it rendered) and draw it.
        /// Call after <see cref="FxWorld.Render"/>.
        /// </summary>
        public void Render(FxWorld world, Dictionary<uint, Transform> anchors) {
            int frame = Time.frameCount;
            bool shown = false;
            foreach (KeyValuePair<uint, Transform> kv in anchors) {
                if (kv.Value == null) continue;
                FxActorLook look = world.GetActorLook(kv.Key);
                if (!actors.TryGetValue(kv.Key, out ActorState state)) {
                    if (look.Flags == 0 && (look.Tint == 0xffffffff || look.Tint == 0)) continue;
                    state = new ActorState { Anchor = kv.Value };
                    actors[kv.Key] = state;
                }

                state.LastSeenFrame = frame;
                if (!Resolve(state)) continue;
                if (look.Flags != state.LastFlags) {
                    state.LastFlags = look.Flags;
                    state.Reported = false;
                }

                state.Copies = 0;
                ApplyTint(state, look.Tint);
                shown |= look.Flags != 0 || (look.Tint != 0 && look.Tint != 0xffffffff);
                if (look.Flags == 0) {
                    Unbend(state);
                    continue;
                }

                currentState = state;
                Draw(state, look);
                if (!state.Reported && look.Flags != 0 && Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) {
                    // diagnostics: what the first frame of a new look drew
                    state.Reported = true;
                    Ragnarok.Diagnostics.RoSkillSweepProbe.Record(
                        $"look aid={kv.Key} flags=0x{look.Flags:x} copies={state.Copies} layers={layers.Count} scale={look.Scale:F2}");
                }
            }

            if (shown && Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.CountLookFrame();
            stale.Clear();
            foreach (KeyValuePair<uint, ActorState> kv in actors) {
                if (kv.Value.LastSeenFrame != frame || kv.Value.Anchor == null) stale.Add(kv.Key);
            }

            foreach (uint aid in stale) {
                Unbend(actors[aid]);
                ApplyTint(actors[aid], 0xffffffff);
                actors.Remove(aid);
            }
        }

        public void Clear() {
            foreach (ActorState state in actors.Values) {
                Unbend(state);
                ApplyTint(state, 0xffffffff);
            }

            actors.Clear();
        }

        bool Resolve(ActorState state) {
            if (state.Anchor == null) return false;
            if (state.Layered == null && state.Single == null) {
                state.Layered = state.Anchor.GetComponentInChildren<RoActorLayeredVisual>();
                if (state.Layered == null) state.Single = state.Anchor.GetComponentInChildren<RoActorSpriteVisual>();
                state.Root = state.Layered != null ? state.Layered.transform : state.Single != null ? state.Single.transform : null;
                if (state.Root != null) {
                    state.BaseScale = state.Root.localScale;
                    state.BaseLocalPosition = state.Root.localPosition;
                }
            }

            return state.Root != null;
        }

        void Draw(ActorState state, FxActorLook look) {
            bool player = state.Layered != null && state.Layered.HasLayer(RoActorSpriteLayer.Body) &&
                          state.Layered.HasLayer(RoActorSpriteLayer.Head);
            Bend(state, look, player);
            CollectLayers(state);
            if (layers.Count == 0) return;

            uint f = look.Flags;
            byte f2 = look.AuraAlpha;
            int f4 = look.Counter;
            foreach (Layer layer in layers) {
                bool bodyLayers = layer.Index <= 4;          // 0 body, 1 head, 2..4 accessories
                bool weapon = layer.Index == 5;
                state.AuraPhase = (state.AuraPhase + 1) % 181;
                state.RingPhase = (state.RingPhase + 1) % 200;
                float sinAura = Mathf.Sin(state.AuraPhase * Mathf.Deg2Rad);

                // 1: layers drawn additively (players: layers 0..4, not the weapon / shield)
                if ((f & FlagAdditive) != 0 && (!player || bodyLayers)) {
                    Copy(layer, Color.white, 0f, Vector2.zero, additive: true, behind: false);
                }

                // 2 Aura Blade: players — the weapon additive; others — blue layer tint + two expanded additive copies
                if ((f & FlagAuraBlade) != 0) {
                    if (player) {
                        if (weapon) Copy(layer, Color.white, 0f, Vector2.zero, true, false);
                    } else {
                        Color c = Rgba(f2, 55, 55, 255);
                        Copy(layer, c, 1f, Vector2.zero, true, true);
                        Copy(layer, c, 2f, Vector2.zero, true, true);
                    }
                }

                // 4 Magic Power: the weapon once more, additively, every other rendered frame
                if ((f & FlagWeaponFlicker) != 0 && player && weapon) {
                    state.WeaponToggle = !state.WeaponToggle;
                    if (state.WeaponToggle) Copy(layer, Color.white, 0f, Vector2.zero, true, false);
                }

                // 8 aura (Assumptio ...): behind the layer
                if ((f & FlagAura) != 0 && (f & (FlagJump | FlagLand)) == 0) {
                    if (player) {
                        if (bodyLayers) {
                            Copy(layer, new Color(1f, 1f, 1f, 100f / 255f), sinAura + 3f, Vector2.zero, true, true);
                            Copy(layer, new Color(1f, 1f, 1f, 120f / 255f), 2f * sinAura + 5f, Vector2.zero, true, true);
                        }
                    } else {
                        Copy(layer, Color.white, 1.5f * sinAura + 5f, Vector2.zero, true, true);
                    }
                }

                // 0x200 Kaite: four white rings behind, α 100 − 5k
                if ((f & FlagReflect) != 0 && (f & (FlagJump | FlagLand)) == 0 && (f & FlagAdditive) == 0) {
                    for (int n = 1; n <= 4; n++) {
                        float k = (state.RingPhase * 0.1f + 5f * n) % 20f;
                        Copy(layer, new Color(1f, 1f, 1f, Mathf.Clamp01((100f - 5f * k) / 255f)), k, Vector2.zero, false, true);
                    }
                }

                // 0x800 Spirit (Asura): ten growing white rings (others) / two scaled copies (players)
                if ((f & FlagAsura) != 0 && (f & FlagLand) == 0) {
                    if (player) {
                        if (bodyLayers) {
                            Color c = Rgba(f2, 55, 55, 255);
                            Copy(layer, c, ScaleExpansion(layer, 0.01f), Vector2.zero, true, true);
                            Copy(layer, c, ScaleExpansion(layer, 0.02f), Vector2.zero, true, true);
                        }
                    } else {
                        for (int i = 1; i <= 10; i++) {
                            float a = f4 <= 130 ? Mathf.Clamp(f4 - 5 * i - 50, 0, 200) : Mathf.Clamp(245 - 10 * i - f4, 0, 200);
                            if (a > 0) Copy(layer, new Color(1f, 1f, 1f, a / 255f), 3f * i, Vector2.zero, true, true);
                        }
                    }
                }

                // 0x80000 green aura (EF 655 / 656)
                if ((f & FlagGreenAura) != 0 && (f & FlagLand) == 0) {
                    if (player) {
                        if (bodyLayers) {
                            Color c = Rgba(f2, 5, 100, 5);
                            Copy(layer, c, ScaleExpansion(layer, 0.01f), Vector2.zero, true, true);
                            Copy(layer, c, ScaleExpansion(layer, 0.02f), Vector2.zero, true, true);
                        }
                    } else {
                        Color c = Rgba(f2, 5, 155, 5);
                        Copy(layer, c, 1f, Vector2.zero, true, true);
                        Copy(layer, c, 2f, Vector2.zero, true, true);
                    }
                }

                // 0x1000 four-way: four fading white copies shifted right / left / down / up, in front
                if ((f & FlagFourWay) != 0 && (f & (FlagJump | FlagLand)) == 0) {
                    float shift = player ? 4f * f4 : f4 * 0.05f * layer.Bounds.size.y * PixelsPerUnit;
                    var c = new Color(1f, 1f, 1f, Mathf.Clamp01(5f * (30 - f4) / 255f));
                    Copy(layer, c, 0f, new Vector2(shift, 0f), false, false);
                    Copy(layer, c, 0f, new Vector2(-shift, 0f), false, false);
                    Copy(layer, c, 0f, new Vector2(0f, -shift), false, false);
                    Copy(layer, c, 0f, new Vector2(0f, shift), false, false);
                }

                // 0x100 hit flash, 0x4000 / 0x40000 glows: additive copies of the final rect
                if ((f & FlagFlash) != 0 && look.Flash != 0) Copy(layer, Argb(look.Flash), 0f, Vector2.zero, true, false);
                if ((f & FlagBlueGlow) != 0 && look.BlueGlow != 0) {
                    Copy(layer, Argb(look.BlueGlow), 0f, Vector2.zero, true, false);
                    Copy(layer, Argb(look.BlueGlow), 0f, Vector2.zero, true, false);
                }

                if ((f & FlagRedGlow) != 0 && look.RedGlow != 0) {
                    Copy(layer, Argb(look.RedGlow), 0f, Vector2.zero, true, false);
                    Copy(layer, Argb(look.RedGlow), 0f, Vector2.zero, true, false);
                }
            }
        }

        /// <summary>Scale / lift / squash / shake move the actor's own sprites: applied to the visual root, undone when the look ends.</summary>
        void Bend(ActorState state, FxActorLook look, bool player) {
            uint f = look.Flags;
            bool bends = (f & (FlagShrink | FlagGrow | FlagLift | FlagSquash | FlagShake | FlagSpin | FlagJump | FlagLand)) != 0;
            if (!bends) {
                Unbend(state);
                return;
            }

            if (!state.Bent) {
                state.BaseScale = state.Root.localScale;
                state.BaseLocalPosition = state.Root.localPosition;
                state.BaseLocalRotation = state.Root.localRotation;
                state.Bent = true;
            }

            float scale = look.Scale > 0f ? look.Scale : 1f;
            float squash = 1f;
            if ((f & FlagSquash) != 0) {
                int f4 = look.Counter;
                squash = f4 <= 3 ? 1f - 0.2f * f4 : 0.35f + 0.01f * shakeRandom.Next(6);
            }

            state.Root.localScale = new Vector3(state.BaseScale.x * scale, state.BaseScale.y * scale * squash, state.BaseScale.z * scale);
            Vector3 offset = Vector3.zero;
            if ((f & FlagLift) != 0) {
                offset.y += look.Lift * SpriteHeightUnits(state);
            }

            if ((f & FlagShake) != 0) {
                float range = (player ? 5f : 10f) / PixelsPerUnit;
                offset.x += (float) (shakeRandom.NextDouble() * 2.0 - 1.0) * range;
                offset.y += (float) (shakeRandom.NextDouble() * 2.0 - 1.0) * range;
            }

            // 0x10000 jump (TK): the layers rise S(20·f4); 0x20000 land: they come down from S((25 − f6)·20)
            if ((f & FlagJump) != 0) offset.y += 20f * Mathf.Max(0, (int) look.Counter) / PixelsPerUnit;
            if ((f & FlagLand) != 0) offset.y += Mathf.Max(0f, (25 - look.GlowPhase) * 20f) / PixelsPerUnit;

            // 0x40 spin (Roundhouse, Seven Wind): the layer angle is replaced by 180 − 180·cos(5·f4), turning the sprite
            // in the screen plane about its middle
            Quaternion rotation = state.BaseLocalRotation;
            if ((f & FlagSpin) != 0 && look.Spin.HasValue) {
                float height = SpriteHeightUnits(state) / Mathf.Max(1e-4f, Mathf.Abs(state.Root.lossyScale.y));
                Vector3 centre = new Vector3(0f, height * 0.5f, 0f);
                Quaternion spin = Quaternion.AngleAxis(-look.Spin.Value, Vector3.forward);
                rotation = state.BaseLocalRotation * spin;
                offset += state.BaseLocalRotation * (centre - spin * centre);
            }

            state.Root.localRotation = rotation;
            state.Root.localPosition = state.BaseLocalPosition + offset;
        }

        /// <summary>The engine's actor colour (+0x98..+0x9b, ARGB) on the actor's layers, only when it changes.</summary>
        static void ApplyTint(ActorState state, uint argb) {
            if (argb == 0) argb = 0xffffffff;
            if (state.AppliedTint == argb) return;
            state.AppliedTint = argb;
            Color c = Argb(argb);
            if (state.Layered != null) state.Layered.SetEffectTint(c);
            else if (state.Single != null) state.Single.SetEffectTint(c);
        }

        static void Unbend(ActorState state) {
            if (!state.Bent || state.Root == null) {
                state.Bent = false;
                return;
            }

            state.Root.localScale = state.BaseScale;
            state.Root.localPosition = state.BaseLocalPosition;
            state.Root.localRotation = state.BaseLocalRotation;
            state.Bent = false;
        }

        static float SpriteHeightUnits(ActorState state) {
            if (state.HeightRenderer == null) state.HeightRenderer = state.Root.GetComponentInChildren<Renderer>();
            return state.HeightRenderer != null ? state.HeightRenderer.bounds.size.y : 1f;
        }

        void CollectLayers(ActorState state) {
            layers.Clear();
            if (state.Layered != null) {
                for (int i = 0; i < state.Layered.LayerCount; i++) {
                    if (!state.Layered.TryGetLayer(i, out RoActorSpriteLayer kind, out RoActorSpriteVisual visual) || visual == null) continue;
                    AddLayer(visual, ClientSlot(kind));
                }
            } else if (state.Single != null) {
                AddLayer(state.Single, 0);
            }
        }

        void AddLayer(RoActorSpriteVisual visual, int slot) {
            RoActSpriteAnimator animator = visual.Animator;
            if (animator == null || slot < 0 ||
                !animator.TryGetLookDrawState(out Mesh mesh, out Matrix4x4 matrix, out Texture texture, out float vpos, out int queue)) {
                return;
            }

            layers.Add(new Layer {
                Mesh = mesh, LocalToWorld = matrix, Texture = texture, VerticalOffset = vpos, Queue = queue, Index = slot,
                Bounds = mesh.bounds,
            });
        }

        /// <summary>The client's CPc layer slot of a Unity layer (-1: not drawn with effect looks).</summary>
        static int ClientSlot(RoActorSpriteLayer kind) {
            switch (kind) {
                case RoActorSpriteLayer.Body: return 0;
                case RoActorSpriteLayer.Head:
                case RoActorSpriteLayer.Hair: return 1;
                case RoActorSpriteLayer.HeadgearTop: return 2;
                case RoActorSpriteLayer.HeadgearMid: return 3;
                case RoActorSpriteLayer.HeadgearLower: return 4;
                case RoActorSpriteLayer.Weapon: return 5;
                case RoActorSpriteLayer.Shield: return 6;
                case RoActorSpriteLayer.Garment: return 7;
                default: return -1;   // cart, cart shadow, weapon trail
            }
        }

        /// <summary>The expansion in sprite pixels that scales the layer by 1 + <paramref name="factor"/>.</summary>
        static float ScaleExpansion(Layer layer, float factor) =>
            Mathf.Max(layer.Bounds.size.x, layer.Bounds.size.y) * factor * 0.5f * PixelsPerUnit;

        /// <summary>
        /// One extra draw of <paramref name="layer"/>: tinted by <paramref name="color"/>, its rect grown by
        /// <paramref name="expandPixels"/> on every side and moved by <paramref name="shiftPixels"/> (x right, y down),
        /// additive (SRCALPHA / ONE) or alpha-blended, just behind or just in front of the layer.
        /// </summary>
        void Copy(Layer layer, Color color, float expandPixels, Vector2 shiftPixels, bool additive, bool behind) {
            if (color.a <= 0f || layer.Mesh == null) return;
            if (currentState != null) currentState.Copies++;
            Material material = MaterialFor(additive, layer.Queue + (behind ? BehindOffset : FrontOffset));
            if (material == null) return;

            Bounds b = layer.Bounds;
            float e = expandPixels / PixelsPerUnit;
            float sx = b.size.x > 1e-5f ? (b.size.x + 2f * e) / b.size.x : 1f;
            float sy = b.size.y > 1e-5f ? (b.size.y + 2f * e) / b.size.y : 1f;
            Vector3 shift = new Vector3(shiftPixels.x, -shiftPixels.y, 0f) / PixelsPerUnit;
            Matrix4x4 local = Matrix4x4.Translate(b.center + shift) * Matrix4x4.Scale(new Vector3(sx, sy, 1f)) * Matrix4x4.Translate(-b.center);

            block ??= new MaterialPropertyBlock();
            block.Clear();
            if (layer.Texture != null) {
                block.SetTexture(BaseMapId, layer.Texture);
                block.SetTexture(MainTexId, layer.Texture);
            }

            block.SetColor(BaseColorId, color);
            block.SetFloat(VPosId, layer.VerticalOffset);
            var rp = new RenderParams(material) {
                matProps = block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMesh(rp, layer.Mesh, 0, layer.LocalToWorld * local);
        }

        Material MaterialFor(bool additive, int queue) {
            if (materials.TryGetValue((additive, queue), out Material m)) return m;
            shader ??= Shader.Find(SpriteShaderName);
            if (shader == null) return null;
            m = new Material(shader) { hideFlags = HideFlags.DontSave, name = additive ? "RO actor look (add)" : "RO actor look", renderQueue = queue };
            m.SetFloat("_SrcBlend", (float) BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float) (additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            materials[(additive, queue)] = m;
            return m;
        }

        static Color Rgba(int a, int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, a / 255f);

        static Color Argb(uint argb) =>
            new Color(((argb >> 16) & 0xff) / 255f, ((argb >> 8) & 0xff) / 255f, (argb & 0xff) / 255f, ((argb >> 24) & 0xff) / 255f);
    }
}
