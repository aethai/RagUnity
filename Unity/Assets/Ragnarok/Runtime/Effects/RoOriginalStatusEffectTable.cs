using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ragnarok.Effects {
    public enum RoStatusActionKind {
        Unknown = 0,
        /// <summary>Classic CRagEffect launched on the actor (LaunchEffect 0x00bd1f40); it follows the actor.</summary>
        Effect,
        /// <summary>New C++ CEffect (FUN_00a4d090) owned by the actor.</summary>
        NewEffect,
        /// <summary>Actor msg 0x6d: kill the classic effects with that id on the actor.</summary>
        RemoveEffect,
        /// <summary>FUN_00a4d760: remove the actor's new-system effect with that id.</summary>
        RemoveNewEffect,
        /// <summary>Hat / aura effect (actor set +0x3fc) on or off, by new-system id.</summary>
        HatEffect,
        /// <summary>Hat effect by the client Lua index (GetEffectType).</summary>
        HatLua,
        /// <summary>CMsgEffect sprite overlay attached to the actor.</summary>
        MsgEffect,
        /// <summary>Actor render state after the step (colour, visible, shadow, freeze ...).</summary>
        Actor,
        /// <summary>FUN_00be3d40 on the actor (ARGB, -1 keeps a channel).</summary>
        SetArgb,
        /// <summary>Actor SetAction (vtable slot 15).</summary>
        Motion,
        /// <summary>Raw ACT action (CGameActor vf07).</summary>
        ActionRaw,
        /// <summary>The client reloads the body sprite (job / monster id).</summary>
        SetJobSprite,
        ReloadSprites,
        Sound,
        Text,
        Number,
        /// <summary>Ground effect at the actor's position (CDummyEffectLauncher).</summary>
        GroundEffect,
        /// <summary>A client call the table only documents (no host action defined).</summary>
        Call,
    }

    /// <summary>The actor render state a status step leaves behind (only the fields the step changed are set).</summary>
    public sealed class RoStatusActorState {
        public Color32? Color;
        public bool? UseColor;
        public bool? DrawBody;
        public bool? Visible;
        public bool? Shadow;
        public bool? FreezeFrame;
        public bool? LockAction;
        /// <summary>CMsgEffect slots (falcon, cartOrWug, freeze, curse, sleep, stun, silence, angelus) set / cleared.</summary>
        public Dictionary<string, bool> MsgEffectSlots;
    }

    /// <summary>One item of a status action list (packages/status-effects/UNITY_FORMAT.md §1), run in order.</summary>
    public sealed class RoStatusAction {
        public RoStatusActionKind Kind;
        public int EffectId;
        public string NewSystemClass;
        /// <summary>Effect msg 0x2c(0xffff, sel) colour right after the launch, or null.</summary>
        public int[] Color;
        /// <summary>Effect msg 0x2c(n) count, or -1.</summary>
        public int Count = -1;
        /// <summary>Effect msg 0xe vector, or null.</summary>
        public float[] EffectMsg0E;
        public bool On;
        /// <summary>Launched on the world (owner AID 0) instead of the actor ("on": "world(owner aid 0)").</summary>
        public bool OnWorld;
        public int Value;
        public string Sprite;
        public string Act;
        public int Action;
        public int[] Argb;
        public float MotionSpeed;
        public RoStatusActorState Actor;
        public string Text;
        public int Lifetime;
        public float Rotation;
    }

    /// <summary>on / off / entry / reapply lists of one status for one actor kind.</summary>
    public sealed class RoStatusActionSet {
        public static readonly RoStatusActionSet Empty = new RoStatusActionSet();

        public RoStatusAction[] On = Array.Empty<RoStatusAction>();
        public RoStatusAction[] Off = Array.Empty<RoStatusAction>();
        /// <summary>Actor entering the view (entry packets; ZC_EFST_SET_ENTER), or null when the client uses On.</summary>
        public RoStatusAction[] Entry;
        /// <summary>Map change / relog of the local player for a still-active EFST, or null.</summary>
        public RoStatusAction[] Reapply;
    }

    public sealed class RoStatusVal1Group {
        public int[] Val1 = Array.Empty<int>();
        public RoStatusActionSet Actions = RoStatusActionSet.Empty;
    }

    /// <summary>One EFST / option bit / opt1 value / opt2 bit: the remote-player lists plus the per-kind variants.</summary>
    public sealed class RoStatusEntry {
        public string Name;
        public RoStatusActionSet Remote = RoStatusActionSet.Empty;
        public RoStatusActionSet LocalPlayer;
        public RoStatusActionSet Monster;
        public RoStatusVal1Group[] ByVal1Remote;
        public RoStatusVal1Group[] ByVal1LocalPlayer;

        public RoStatusActionSet Resolve(bool localPlayer, bool monster, int val1) {
            RoStatusVal1Group[] groups = localPlayer ? ByVal1LocalPlayer : ByVal1Remote;
            if (groups != null) {
                foreach (RoStatusVal1Group group in groups) {
                    if (Array.IndexOf(group.Val1, val1) >= 0) {
                        return group.Actions;
                    }
                }
            }

            if (localPlayer && LocalPlayer != null) return LocalPlayer;
            if (monster && Monster != null) return Monster;
            return Remote;
        }
    }

    /// <summary>opt3 bit: player jobs launch on set (cleared bits remove nothing), other actors on / off.</summary>
    public sealed class RoStatusOpt3Entry {
        public string Name;
        public RoStatusActionSet PlayerJob = RoStatusActionSet.Empty;
        public RoStatusActionSet NonPlayerJob = RoStatusActionSet.Empty;
    }

    public sealed class RoStatusPerTickEntry {
        public int EffectId;
        public int EveryTicks;
    }

    /// <summary>
    /// The original client's status visuals (game.exe status / option / EFST handlers executed per id; exported by
    /// reference scripts/export_unity_status_effects.js, format: packages/status-effects/UNITY_FORMAT.md).
    /// </summary>
    public sealed class RoOriginalStatusEffectTable {
        public const string ResourcePath = "Ragnarok/Generated/OriginalClient/originalStatusEffects";

        static RoOriginalStatusEffectTable defaultTable;

        public readonly Dictionary<int, RoStatusEntry> Efst = new Dictionary<int, RoStatusEntry>();
        /// <summary>opt1 value (ZC_STATE_CHANGE body state).</summary>
        public readonly Dictionary<int, RoStatusEntry> BodyState = new Dictionary<int, RoStatusEntry>();
        /// <summary>opt2 bit (health state).</summary>
        public readonly Dictionary<int, RoStatusEntry> HealthState = new Dictionary<int, RoStatusEntry>();
        /// <summary>option bit (effect state).</summary>
        public readonly Dictionary<int, RoStatusEntry> Option = new Dictionary<int, RoStatusEntry>();
        public readonly Dictionary<int, RoStatusOpt3Entry> Opt3 = new Dictionary<int, RoStatusOpt3Entry>();
        /// <summary>option bit → effect relaunched every N logic ticks while the bit is set (Sight 0x1, Ruwach 0x2000).</summary>
        public readonly Dictionary<int, RoStatusPerTickEntry> OptionPerTick = new Dictionary<int, RoStatusPerTickEntry>();
        /// <summary>ZC_NOTIFY_EFFECT 0x19b type → actions.</summary>
        public readonly Dictionary<int, RoStatusAction[]> NotifyEffectType = new Dictionary<int, RoStatusAction[]>();
        /// <summary>ZC_NOTIFY_EFFECT2 0x1f3 / 3 0x284 effect id → actions (normal / with /mineffect).</summary>
        public readonly Dictionary<int, RoStatusAction[]> NotifyEffectId = new Dictionary<int, RoStatusAction[]>();
        public readonly Dictionary<int, RoStatusAction[]> NotifyEffectIdMinEffect = new Dictionary<int, RoStatusAction[]>();

        /// <summary>Chase Walk footprints: ground effects alternating every <see cref="FootprintPeriodFrames"/> frames.</summary>
        public int[] FootprintEffects = Array.Empty<int>();
        public int FootprintPeriodFrames;
        public int FootprintLifetime;

        public static RoOriginalStatusEffectTable Empty { get; } = new RoOriginalStatusEffectTable();

        public static RoOriginalStatusEffectTable Default {
            get {
                if (defaultTable == null) {
                    defaultTable = TakePreloaded() ?? LoadDefaultOrEmpty();
                }

                return defaultTable;
            }
            set => defaultTable = value;
        }

        static System.Threading.Tasks.Task<RoOriginalStatusEffectTable> preloading;

        /// <summary>
        /// Starts parsing the default table on a worker thread (the asset is read here, on the main thread): call it early,
        /// so the first lookup during play does not parse the JSON on the main thread. <see cref="Default"/> waits for it.
        /// </summary>
        public static void Preload() {
            if (defaultTable != null || preloading != null) {
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            string json = asset != null ? asset.text : null;
            if (string.IsNullOrWhiteSpace(json)) {
                return;
            }

            preloading = System.Threading.Tasks.Task.Run(() => LoadFromJson(json));
        }

        static RoOriginalStatusEffectTable TakePreloaded() {
            System.Threading.Tasks.Task<RoOriginalStatusEffectTable> task = preloading;
            preloading = null;
            try {
                return task?.Result;
            } catch (Exception ex) {
                Debug.LogWarning($"Could not load the original-client status effect table '{ResourcePath}': {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }


        public int Count => Efst.Count + BodyState.Count + HealthState.Count + Option.Count + Opt3.Count;

        static RoOriginalStatusEffectTable LoadDefaultOrEmpty() {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text)) {
                return Empty;
            }

            try {
                return LoadFromJson(asset.text);
            } catch (Exception ex) {
                Debug.LogWarning($"Could not load the original-client status effect table '{ResourcePath}': {ex.Message}");
                return Empty;
            }
        }

        public static RoOriginalStatusEffectTable LoadFromJson(string json) {
            JObject root = JObject.Parse(json);
            var table = new RoOriginalStatusEffectTable();
            ReadEntries(root["efst"], table.Efst);
            ReadEntries(root["bodyState"], table.BodyState);
            ReadEntries(root["healthState"], table.HealthState);
            ReadEntries(root["option"], table.Option);

            if (root["opt3"] is JObject opt3) {
                foreach (JProperty p in opt3.Properties()) {
                    if (!TryKey(p.Name, out int bit) || !(p.Value is JObject e)) continue;
                    table.Opt3[bit] = new RoStatusOpt3Entry {
                        Name = (string) e["name"],
                        PlayerJob = ReadSet(e["playerJob"]) ?? RoStatusActionSet.Empty,
                        NonPlayerJob = ReadSet(e["nonPlayerJob"]) ?? RoStatusActionSet.Empty,
                    };
                }
            }

            if (root["optionPerTick"] is JObject perTick) {
                foreach (JProperty p in perTick.Properties()) {
                    if (!TryKey(p.Name, out int bit) || !(p.Value is JObject e)) continue;
                    table.OptionPerTick[bit] = new RoStatusPerTickEntry {
                        EffectId = (int?) e["ef"] ?? 0,
                        EveryTicks = (int?) e["everyTicks"] ?? 0,
                    };
                }
            }

            if (root["footprints"] is JObject footprints) {
                foreach (JProperty p in footprints.Properties()) {
                    if (!(p.Value is JObject e)) continue;
                    table.FootprintEffects = Ints(e["groundEf"]);
                    table.FootprintPeriodFrames = (int?) e["periodFrames"] ?? 12;
                    table.FootprintLifetime = (int?) e["lifetime"] ?? 0;
                }
            }

            if (root["notifyEffect"] is JObject notify) {
                if (notify["0x19b"] is JObject byType) {
                    foreach (JProperty p in byType.Properties()) {
                        if (TryKey(p.Name, out int type) && p.Value is JObject e) {
                            table.NotifyEffectType[type] = ReadList(e["normal"]);
                        }
                    }
                }

                foreach (string packet in new[] { "0x1f3", "0x284" }) {
                    if (!(notify[packet] is JObject byId)) continue;
                    foreach (JProperty p in byId.Properties()) {
                        if (!TryKey(p.Name, out int id) || !(p.Value is JObject e)) continue;
                        if (!table.NotifyEffectId.ContainsKey(id)) table.NotifyEffectId[id] = ReadList(e["normal"]);
                        if (e["minEffect"] != null && !table.NotifyEffectIdMinEffect.ContainsKey(id)) {
                            table.NotifyEffectIdMinEffect[id] = ReadList(e["minEffect"]);
                        }
                    }
                }
            }

            return table;
        }

        static void ReadEntries(JToken token, Dictionary<int, RoStatusEntry> into) {
            if (!(token is JObject entries)) return;
            foreach (JProperty p in entries.Properties()) {
                if (!TryKey(p.Name, out int key) || !(p.Value is JObject e)) continue;
                var entry = new RoStatusEntry {
                    Name = (string) e["name"],
                    Remote = ReadSet(e) ?? RoStatusActionSet.Empty,
                    LocalPlayer = e["localPlayer"] is JObject lp ? ReadSet(lp) : null,
                    Monster = e["monster"] is JObject mo ? ReadSet(mo) : null,
                };
                if (e["byVal1"] is JObject byVal1) {
                    entry.ByVal1Remote = ReadGroups(byVal1["remote"]);
                    entry.ByVal1LocalPlayer = ReadGroups(byVal1["localPlayer"]);
                }

                into[key] = entry;
            }
        }

        static RoStatusVal1Group[] ReadGroups(JToken token) {
            if (!(token is JArray array) || array.Count == 0) return null;
            var groups = new List<RoStatusVal1Group>(array.Count);
            foreach (JToken g in array) {
                if (!(g is JObject o)) continue;
                groups.Add(new RoStatusVal1Group { Val1 = Ints(o["val1"]), Actions = ReadSet(o) ?? RoStatusActionSet.Empty });
            }

            return groups.ToArray();
        }

        static RoStatusActionSet ReadSet(JToken token) {
            if (!(token is JObject o)) return null;
            return new RoStatusActionSet {
                On = ReadList(o["on"]),
                Off = ReadList(o["off"]),
                Entry = o["entry"] is JArray ? ReadList(o["entry"]) : null,
                Reapply = o["reapply"] is JArray ? ReadList(o["reapply"]) : null,
            };
        }

        static RoStatusAction[] ReadList(JToken token) {
            if (!(token is JArray array) || array.Count == 0) return Array.Empty<RoStatusAction>();
            var list = new List<RoStatusAction>(array.Count);
            foreach (JToken item in array) {
                if (item is JObject o) {
                    list.Add(ReadAction(o));
                }
            }

            return list.ToArray();
        }

        static RoStatusAction ReadAction(JObject o) {
            var a = new RoStatusAction();
            if (o["ef"] != null) {
                a.EffectId = (int?) o["ef"] ?? 0;
                a.NewSystemClass = (string) o["newSystem"];
                a.Kind = a.NewSystemClass != null ? RoStatusActionKind.NewEffect : RoStatusActionKind.Effect;
                a.Color = o["color"] is JArray ? Ints(o["color"]) : null;
                a.Count = (int?) o["count"] ?? -1;
                a.EffectMsg0E = Floats(o["effectMsg0xe"]);
                a.On = OnFlag(o["on"], out a.OnWorld);
            } else if (o["removeEf"] != null) {
                a.Kind = RoStatusActionKind.RemoveEffect;
                a.EffectId = (int?) o["removeEf"] ?? 0;
            } else if (o["removeNewEf"] != null) {
                a.Kind = RoStatusActionKind.RemoveNewEffect;
                a.EffectId = (int?) o["removeNewEf"] ?? 0;
            } else if (o["hatEf"] != null) {
                a.Kind = RoStatusActionKind.HatEffect;
                a.EffectId = (int?) o["hatEf"] ?? 0;
                a.On = OnFlag(o["on"], out a.OnWorld);
            } else if (o["hatLua"] != null) {
                a.Kind = RoStatusActionKind.HatLua;
                a.Value = (int?) o["hatLua"] ?? 0;
                a.On = OnFlag(o["on"], out a.OnWorld);
            } else if (o["msgEffect"] is JObject m) {
                a.Kind = RoStatusActionKind.MsgEffect;
                a.Sprite = (string) m["spr"];
                a.Act = (string) m["act"];
                a.Action = (int?) m["action"] ?? 0;
                a.Argb = m["argb"] is JArray ? Ints(m["argb"]) : null;
                a.MotionSpeed = (float?) m["motionSpeed"] ?? 0f;
            } else if (o["actor"] is JObject actor) {
                a.Kind = RoStatusActionKind.Actor;
                a.Actor = ReadActorState(actor);
            } else if (o["setArgb"] != null) {
                a.Kind = RoStatusActionKind.SetArgb;
                a.Argb = Ints(o["setArgb"]);
            } else if (o["motion"] != null) {
                a.Kind = RoStatusActionKind.Motion;
                a.Value = (int?) o["motion"] ?? 0;
            } else if (o["actionRaw"] != null) {
                a.Kind = RoStatusActionKind.ActionRaw;
                int[] raw = Ints(o["actionRaw"]);
                a.Value = raw.Length > 0 ? raw[0] : 0;
                a.Argb = raw;
            } else if (o["setJobSprite"] != null) {
                a.Kind = RoStatusActionKind.SetJobSprite;
                a.Value = (int?) o["setJobSprite"] ?? 0;
            } else if (o["reloadSprites"] != null) {
                a.Kind = RoStatusActionKind.ReloadSprites;
            } else if (o["sound"] != null) {
                a.Kind = RoStatusActionKind.Sound;
                a.Text = (string) o["sound"];
            } else if (o["text"] != null) {
                a.Kind = RoStatusActionKind.Text;
                a.Text = (string) o["text"];
            } else if (o["number"] != null) {
                a.Kind = RoStatusActionKind.Number;
                a.Value = (int?) o["number"] ?? 0;
                a.Argb = o["argb"] is JArray ? Ints(o["argb"]) : new[] { (int?) o["argb"] ?? 0 };
            } else if (o["groundEf"] != null) {
                a.Kind = RoStatusActionKind.GroundEffect;
                a.EffectId = (int?) o["groundEf"] ?? 0;
                a.Lifetime = (int?) o["lifetime"] ?? 0;
                a.Rotation = (float?) o["rotation"] ?? 0f;
            } else if (o["call"] != null) {
                a.Kind = RoStatusActionKind.Call;
                a.Text = (string) o["call"];
            }

            return a;
        }

        static RoStatusActorState ReadActorState(JObject o) {
            var s = new RoStatusActorState();
            foreach (JProperty p in o.Properties()) {
                string key = p.Name;
                int paren = key.IndexOf('(');
                string name = paren >= 0 ? key.Substring(0, paren) : key;
                switch (name) {
                    case "color":
                        if (p.Value is JObject c) {
                            s.Color = new Color32((byte) ((int?) c["r"] ?? 255), (byte) ((int?) c["g"] ?? 255),
                                (byte) ((int?) c["b"] ?? 255), (byte) ((int?) c["a"] ?? 255));
                        }

                        break;
                    case "useColor": s.UseColor = Flag(p.Value); break;
                    case "drawBody": s.DrawBody = Flag(p.Value); break;
                    case "visible": s.Visible = Flag(p.Value); break;
                    case "shadow": s.Shadow = Flag(p.Value); break;
                    case "freezeFrame": s.FreezeFrame = Flag(p.Value); break;
                    case "lockAction": s.LockAction = Flag(p.Value); break;
                    default:
                        if (name.StartsWith("msgEffect.", StringComparison.Ordinal)) {
                            s.MsgEffectSlots ??= new Dictionary<string, bool>();
                            s.MsgEffectSlots[name.Substring("msgEffect.".Length)] = string.Equals((string) p.Value, "set", StringComparison.Ordinal);
                        }

                        break;
                }
            }

            return s;
        }

        /// <summary>"on": true / false, or a string naming another owner ("world(owner aid 0)"): launched, on the world.</summary>
        static bool OnFlag(JToken token, out bool onWorld) {
            onWorld = token != null && token.Type == JTokenType.String;
            return token == null || token.Type != JTokenType.Boolean || (bool) token;
        }

        static bool Flag(JToken token) => token.Type == JTokenType.Boolean ? (bool) token : ((int?) token ?? 0) != 0;

        /// <summary>Keys are decimal ids or hex bits ("0x4000", "0x4000 CHASEWALK").</summary>
        static bool TryKey(string text, out int key) {
            string head = text.Trim();
            int space = head.IndexOf(' ');
            if (space > 0) head = head.Substring(0, space);
            if (head.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                return int.TryParse(head.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out key);
            }

            return int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out key);
        }

        static int[] Ints(JToken token) {
            if (!(token is JArray array) || array.Count == 0) return Array.Empty<int>();
            var result = new int[array.Count];
            for (int i = 0; i < array.Count; i++) result[i] = (int?) array[i] ?? 0;
            return result;
        }

        static float[] Floats(JToken token) {
            if (!(token is JArray array) || array.Count == 0) return null;
            var result = new float[array.Count];
            for (int i = 0; i < array.Count; i++) result[i] = (float?) array[i] ?? 0f;
            return result;
        }
    }
}
