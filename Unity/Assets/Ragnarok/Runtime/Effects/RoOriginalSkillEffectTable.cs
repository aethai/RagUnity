using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ragnarok.Effects {
    /// <summary>One effect launched before the hit (ZC_NOTIFY_SKILL receipt): on the caster (a projectile when
    /// <see cref="ToTarget"/>) or on the target. <see cref="Count"/> 0: the launch site sends no msg 0x2c (the effect
    /// keeps +0x174 = 0); otherwise it gets the packet's hit count.</summary>
    public readonly struct RoOriginalBeforeHitEffect {
        public RoOriginalBeforeHitEffect(int effectId, bool onCaster, bool toTarget, int count) {
            EffectId = effectId;
            OnCaster = onCaster;
            ToTarget = toTarget;
            Count = Math.Max(0, count);
        }

        public int EffectId { get; }
        public bool OnCaster { get; }
        public bool ToTarget { get; }
        public int Count { get; }
    }

    /// <summary>A ZC_USE_SKILL effect picked by the packet's amount field (Heal: &lt;200 → 7, &lt;2000 → 312, ...).</summary>
    public readonly struct RoOriginalAmountEffect {
        public RoOriginalAmountEffect(int min, int max, int effectId) {
            Min = min;
            Max = max;
            EffectId = effectId;
        }

        public int Min { get; }
        /// <summary>Inclusive; -1 = no upper bound.</summary>
        public int Max { get; }
        public int EffectId { get; }

        public bool Contains(int amount) => amount >= Min && (Max < 0 || amount <= Max);
    }

    /// <summary>
    /// A unit effect relaunched while its skill unit lives: every <c>DefaultDuration(id) - K</c> ticks; a persistent one
    /// (Ice Wall, Fog Wall, songs, ...) is launched once, kept at CSkill +0x418 and killed by the unit's destructor.
    /// </summary>
    public readonly struct RoOriginalPeriodicEffect {
        public RoOriginalPeriodicEffect(int effectId, int k, bool persistent = false) {
            EffectId = effectId;
            K = k;
            Persistent = persistent;
        }

        public int EffectId { get; }
        public int K { get; }
        public bool Persistent { get; }
    }

    /// <summary>
    /// What the client's CSkill does for one skill unit look (ZC_SKILL_ENTRY): effects at creation (kept until the unit
    /// disappears), effects relaunched periodically while it lives, and the effects when a trap fires (look → 0x8c).
    /// </summary>
    public sealed class RoOriginalSkillUnit {
        static readonly int[] None = Array.Empty<int>();

        public int UnitId { get; internal set; }
        public int[] OnCreate { get; internal set; } = None;
        public RoOriginalPeriodicEffect[] Periodic { get; internal set; } = Array.Empty<RoOriginalPeriodicEffect>();
        public int[] TrapTriggered { get; internal set; } = None;
    }

    /// <summary>
    /// What the original client (Custom game.exe) launches for one skill, per server packet. Extracted by running the
    /// client's own skill dispatch in an emulator (reference packages/skill-effects, UNITY_FORMAT.md): the effect ids,
    /// which actor or cell they go on and when. An empty slot means the original launches nothing there.
    /// </summary>
    public sealed class RoOriginalSkillEffects {
        static readonly int[] None = Array.Empty<int>();

        public ushort SkillId { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        /// <summary>ZC_USESKILL_ACK with a cast time: on the caster for the cast.</summary>
        public int[] BeginCast { get; internal set; } = None;
        /// <summary>The begin-cast id by the packet's property (element 0..9) when it depends on it.</summary>
        public int[] BeginCastByProperty { get; internal set; } = None;
        /// <summary>ZC_USESKILL_ACK on another actor: on the target for the cast (EF 60 lock-on).</summary>
        public int[] BeginCastOnTarget { get; internal set; } = None;
        /// <summary>ZC_NOTIFY_SKILL receipt: on the caster.</summary>
        public int[] OnCaster { get; internal set; } = None;
        public RoOriginalBeforeHitEffect[] BeforeHit { get; internal set; } = Array.Empty<RoOriginalBeforeHitEffect>();
        /// <summary>ZC_USE_SKILL: effects removed (actor msg 0x6d, the oldest instance) before the use launches —
        /// on the caster (<c>OnTarget</c> false) or the target.</summary>
        public (int EffectId, bool OnTarget)[] UseRemove { get; internal set; } = Array.Empty<(int, bool)>();
        /// <summary>ZC_NOTIFY_SKILL first hit, at the attack motion time, on the target, only with damage.</summary>
        public int[] Hit { get; internal set; } = None;
        /// <summary>ZC_NOTIFY_SKILL every further hit (+200 ms each), on the target.</summary>
        public int[] HitEachHit { get; internal set; } = None;
        /// <summary>ZC_USE_SKILL: on the packet's target actor.</summary>
        public int[] Use { get; internal set; } = None;
        /// <summary>ZC_USE_SKILL: on the packet's source actor.</summary>
        public int[] UseOnCaster { get; internal set; } = None;
        /// <summary>ZC_USE_SKILL: at the target's cell (position effects).</summary>
        public int[] UsePosition { get; internal set; } = None;
        public int[] UseOnlyIfCasterIsLocalPlayer { get; internal set; } = None;
        public RoOriginalAmountEffect[] UseByAmount { get; internal set; } = Array.Empty<RoOriginalAmountEffect>();
        /// <summary>When &gt; 0, ZC_USE_SKILL also launches <c>UseLevelBase + level</c> (Seven Wind).</summary>
        public int UseLevelBase { get; internal set; }
        /// <summary>ZC_NOTIFY_GROUNDSKILL: at the target cell.</summary>
        public int[] Ground { get; internal set; } = None;
        /// <summary>ZC_SKILL_ENTRY of one of <see cref="UnitIds"/>: on the unit's cell for the unit's life.</summary>
        public int[] Unit { get; internal set; } = None;
        public int[] UnitIds { get; internal set; } = None;
        public int[] UnitTrapTriggered { get; internal set; } = None;
        /// <summary>CGroundScope size in cells for a ground cast (area highlight, not an effect id); 0 = none.</summary>
        public int GroundScope { get; internal set; }
        public bool HideCastBar { get; internal set; }
        public bool HideCastAura { get; internal set; }
        /// <summary>Ids the client draws with its new C++ CEffect system instead of CRagEffect.</summary>
        public int[] NewSystem { get; internal set; } = None;
        /// <summary>Ids roBrowser lists for this skill that the original never launches for it.</summary>
        public int[] RoBrowserOnly { get; internal set; } = None;
        /// <summary>Its damage comes from a skill unit (Storm Gust, Meteor...): every hit launches <see cref="Hit"/> on the
        /// target at receipt, +200 ms per further hit, and nothing on the caster.</summary>
        public bool DamageFromUnit { get; internal set; }
        /// <summary>CDummyEffectLauncher lifetime per ground id, in 24 ms units, when a constant (0 = the effect's own).</summary>
        public Dictionary<int, int> GroundLifetimeUnits24 { get; internal set; } = new Dictionary<int, int>();

        /// <summary>
        /// What the client does for a skill missing from the table (FUN_00d13d20's default, 8128 of the 8960 skill ids in
        /// reference out/skillfx/tbl_begincast.tsv): the begin-cast EF 16 on the caster and the EF 60 lock-on on a target
        /// actor while casting (a cell target gets the ground scope); nothing on the other packets.
        /// </summary>
        public static RoOriginalSkillEffects Default { get; } = new RoOriginalSkillEffects {
            Name = "default",
            BeginCast = new[] { 16 },
            BeginCastOnTarget = new[] { 60 },
        };

        public int ResolveBeginCast(int property) {
            if (BeginCastByProperty.Length > 0 && property >= 0 && property < BeginCastByProperty.Length) {
                return BeginCastByProperty[property];
            }

            return BeginCast.Length > 0 ? BeginCast[0] : 0;
        }

        public bool TryResolveUseByAmount(int amount, out int effectId) {
            for (int i = 0; i < UseByAmount.Length; i++) {
                if (UseByAmount[i].Contains(amount)) {
                    effectId = UseByAmount[i].EffectId;
                    return true;
                }
            }

            effectId = 0;
            return false;
        }
    }

    /// <summary>
    /// The original client's skill → effect table (Resources/Ragnarok/Generated/OriginalClient/originalSkillEffects.json,
    /// exported by reference scripts/export_unity_skill_effects.js); <see cref="GetOrDefault"/> gives the client's
    /// default for a skill it does not list.
    /// </summary>
    public sealed class RoOriginalSkillEffectTable {
        public const string ResourcePath = "Ragnarok/Generated/OriginalClient/originalSkillEffects";

        static RoOriginalSkillEffectTable defaultTable;

        readonly Dictionary<ushort, RoOriginalSkillEffects> skills;
        readonly Dictionary<int, RoOriginalSkillEffects> skillsByUnitId;
        readonly Dictionary<int, RoOriginalSkillUnit> units;

        RoOriginalSkillEffectTable(Dictionary<ushort, RoOriginalSkillEffects> skills, Dictionary<int, RoOriginalSkillUnit> units = null) {
            this.skills = skills;
            this.units = units ?? new Dictionary<int, RoOriginalSkillUnit>();
            skillsByUnitId = new Dictionary<int, RoOriginalSkillEffects>();
            foreach (RoOriginalSkillEffects entry in skills.Values) {
                foreach (int unitId in entry.UnitIds) {
                    // several skills can share a unit look (e.g. traps); the first listed wins, like the client's table
                    if (!skillsByUnitId.ContainsKey(unitId)) {
                        skillsByUnitId[unitId] = entry;
                    }
                }
            }
        }

        public static RoOriginalSkillEffectTable Empty { get; } = new RoOriginalSkillEffectTable(new Dictionary<ushort, RoOriginalSkillEffects>());

        public static RoOriginalSkillEffectTable Default {
            get {
                if (defaultTable == null) {
                    defaultTable = TakePreloaded() ?? LoadDefaultOrEmpty();
                }

                return defaultTable;
            }
            set => defaultTable = value;
        }

        static System.Threading.Tasks.Task<RoOriginalSkillEffectTable> preloading;

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

        static RoOriginalSkillEffectTable TakePreloaded() {
            System.Threading.Tasks.Task<RoOriginalSkillEffectTable> task = preloading;
            preloading = null;
            try {
                return task?.Result;
            } catch (Exception ex) {
                Debug.LogWarning($"Could not load the original-client skill effect table '{ResourcePath}': {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }


        public int Count => skills.Count;

        public bool TryGet(ushort skillId, out RoOriginalSkillEffects entry) => skills.TryGetValue(skillId, out entry);

        /// <summary>The skill's entry, or <see cref="RoOriginalSkillEffects.Default"/> for one the table lacks.</summary>
        public RoOriginalSkillEffects GetOrDefault(ushort skillId) =>
            skills.TryGetValue(skillId, out RoOriginalSkillEffects entry) ? entry : RoOriginalSkillEffects.Default;

        public bool TryGetByUnitId(int unitId, out RoOriginalSkillEffects entry) => skillsByUnitId.TryGetValue(unitId, out entry);

        public bool TryGetUnit(int unitId, out RoOriginalSkillUnit unit) => units.TryGetValue(unitId, out unit);

        static RoOriginalSkillEffectTable LoadDefaultOrEmpty() {
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text)) {
                return Empty;
            }

            try {
                return LoadFromJson(asset.text);
            } catch (Exception ex) {
                Debug.LogWarning($"Could not load the original-client skill effect table '{ResourcePath}': {ex.Message}");
                return Empty;
            }
        }

        public static RoOriginalSkillEffectTable LoadFromJson(string json) {
            JObject root = JObject.Parse(json);
            var skills = new Dictionary<ushort, RoOriginalSkillEffects>();
            if (root["skills"] is JObject skillObjects) {
                foreach (JProperty property in skillObjects.Properties()) {
                    if (!ushort.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort skillId) ||
                        !(property.Value is JObject e)) {
                        continue;
                    }

                    skills[skillId] = Parse(skillId, e);
                }
            }

            var units = new Dictionary<int, RoOriginalSkillUnit>();
            if (root["units"] is JObject unitObjects) {
                foreach (JProperty property in unitObjects.Properties()) {
                    if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int unitId) ||
                        !(property.Value is JObject u)) {
                        continue;
                    }

                    var periodic = new List<RoOriginalPeriodicEffect>();
                    if (u["periodic"] is JArray list) {
                        foreach (JToken x in list) {
                            periodic.Add(new RoOriginalPeriodicEffect((int?) x["id"] ?? 0, (int?) x["k"] ?? 0, (bool?) x["persistent"] ?? false));
                        }
                    }

                    units[unitId] = new RoOriginalSkillUnit {
                        UnitId = unitId,
                        OnCreate = Ints(u["onCreate"]),
                        Periodic = periodic.ToArray(),
                        TrapTriggered = Ints(u["trap"]),
                    };
                }
            }

            return new RoOriginalSkillEffectTable(skills, units);
        }

        static RoOriginalSkillEffects Parse(ushort skillId, JObject e) {
            var entry = new RoOriginalSkillEffects {
                SkillId = skillId,
                Name = (string) e["name"] ?? string.Empty,
                BeginCast = Ints(e["beginCast"]),
                BeginCastByProperty = Ints(e["beginCastByProperty"]),
                BeginCastOnTarget = Ints(e["beginCastOnTarget"]),
                OnCaster = Ints(e["onCaster"]),
                Hit = Ints(e["hit"]),
                HitEachHit = Ints(e["hitEachHit"]),
                Use = Ints(e["use"]),
                UseOnCaster = Ints(e["useOnCaster"]),
                UsePosition = Ints(e["usePosition"]),
                UseOnlyIfCasterIsLocalPlayer = Ints(e["useOnlyIfCasterIsLocalPlayer"]),
                UseLevelBase = (int?) e["useLevelBase"] ?? 0,
                Ground = Ints(e["ground"]),
                Unit = Ints(e["unit"]),
                UnitIds = Ints(e["unitIds"]),
                UnitTrapTriggered = Ints(e["unitTrapTriggered"]),
                GroundScope = (int?) e["groundScope"] ?? 0,
                HideCastBar = (bool?) e["hideCastBar"] ?? false,
                HideCastAura = (bool?) e["hideCastAura"] ?? false,
                NewSystem = Ints(e["newSystem"]),
                RoBrowserOnly = Ints(e["roBrowserOnly"]),
                DamageFromUnit = (bool?) e["damageFromUnit"] ?? false,
            };

            if (e["groundLifetime"] is JArray lifetimes) {
                foreach (JToken l in lifetimes) {
                    int units24 = (int?) l["units24"] ?? 0;
                    // defaultDuration(ef) + k: the launcher outlives the effect's default duration, but an effect that
                    // lengthens its own (EF_ICEWALL sets 9999 at tick 0) still dies with the launcher (msg 0x18)
                    if (units24 <= 0 && l["plusDefault"] != null) {
                        int id = (int?) l["id"] ?? 0;
                        units24 = Ragnarok.Effects.Classic.ClassicEffectTables.DefaultDuration(id) + ((int?) l["plusDefault"] ?? 0);
                    }

                    if (units24 > 0) {
                        entry.GroundLifetimeUnits24[(int?) l["id"] ?? 0] = units24;
                    }
                }
            }

            if (e["useRemove"] is JArray removals && removals.Count > 0) {
                var list = new List<(int, bool)>(removals.Count);
                foreach (JToken r in removals) {
                    list.Add(((int?) r["id"] ?? 0, string.Equals((string) r["on"], "target", StringComparison.OrdinalIgnoreCase)));
                }

                entry.UseRemove = list.ToArray();
            }

            if (e["beforeHitDetail"] is JArray details && details.Count > 0) {
                var list = new List<RoOriginalBeforeHitEffect>(details.Count);
                foreach (JToken d in details) {
                    list.Add(new RoOriginalBeforeHitEffect(
                        (int?) d["id"] ?? 0,
                        string.Equals((string) d["on"], "caster", StringComparison.OrdinalIgnoreCase),
                        (bool?) d["toTarget"] ?? false,
                        (int?) d["count"] ?? 1));
                }

                entry.BeforeHit = list.ToArray();
            } else {
                // no detail: the ids go on the target, once
                int[] ids = Ints(e["beforeHit"]);
                var list = new RoOriginalBeforeHitEffect[ids.Length];
                for (int i = 0; i < ids.Length; i++) {
                    list[i] = new RoOriginalBeforeHitEffect(ids[i], onCaster: false, toTarget: false, count: 1);
                }

                entry.BeforeHit = list;
            }

            if (e["useByAmount"] is JArray amounts && amounts.Count > 0) {
                var list = new List<RoOriginalAmountEffect>(amounts.Count);
                foreach (JToken a in amounts) {
                    list.Add(new RoOriginalAmountEffect((int?) a["min"] ?? 0, (int?) a["max"] ?? -1, (int?) a["id"] ?? 0));
                }

                entry.UseByAmount = list.ToArray();
            }

            return entry;
        }

        static int[] Ints(JToken token) {
            if (!(token is JArray array) || array.Count == 0) {
                return Array.Empty<int>();
            }

            var result = new int[array.Count];
            for (int i = 0; i < array.Count; i++) {
                result[i] = (int?) array[i] ?? 0;
            }

            return result;
        }
    }
}
