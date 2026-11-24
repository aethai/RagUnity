using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using MoonSharp.Interpreter;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Data.Lua51;
using Ragnarok.Gameplay;
using LuaValue = Ragnarok.Data.Lua51.LuaValue;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// EquipmentProperties::CEquipmentPropertiesMgr (equip-properties-mgr-spec): the client sums the "bonus scripts" of what
    /// the character wears from EquipmentProperties.lub (compiled Lua 5.1, run by <see cref="LuaState"/>) and formats them
    /// with EquipmentPropertiesOrder.lub (text Lua, read with MoonSharp together with EnumVar.lub). Every worn item
    /// (Wearable: Stat, refine bonus, OnStartEquip, random options, its cards), the active combos (Combiitem) and the pet
    /// egg (Expansion "(P)") keep their own container; the total merges them for the window 0x2711 (GetLines by category),
    /// and equipping / removing prints the item's lines in the chat (#00FFC8 / #FF0000, chat type 23). The data is kRO's,
    /// as in the original client.
    /// </summary>
    public sealed class RoEquipProperties {
        // ---------------------------------------------------------------- Order entries (parser 0x640ca0)

        private const int OpAdd = 0, OpOne = 1, OpMax = 2, OpDrainAdd = 3;

        private sealed class Entry {
            public int Category, Order, ValIdx, Op, SepIdx, RndOpt = -1;
            public string Name = string.Empty, SepGetter = string.Empty, Uniq1 = string.Empty, Uniq2 = string.Empty;
            public string[] Funcs = Array.Empty<string>(), Sym = Array.Empty<string>();
            public int? Min, Max, Div, Float;
        }

        // ---------------------------------------------------------------- properties (1Dim / 2Dim / Flag, §3)

        private abstract class Prop {
            public Entry E;
            public abstract Prop Clone();
            public abstract void Merge(Prop other);
            public abstract string Format(RoEquipProperties m, int mode, bool invert);
        }

        private sealed class Dim1 : Prop {
            public int V;
            public override Prop Clone() => new Dim1 { E = E, V = V };

            public override void Merge(Prop other) {
                int o = ((Dim1) other).V;
                switch (E.Op) {
                    case OpAdd: V += o; break;
                    case OpOne: V += 1; break;
                    case OpMax: V = Math.Max(V, o); break;
                }
            }

            public override string Format(RoEquipProperties m, int mode, bool invert) => m.Line(E, V, mode, invert, null);
        }

        private sealed class Dim2 : Prop {
            public readonly SortedDictionary<int, int> M = new SortedDictionary<int, int>();

            public override Prop Clone() {
                var c = new Dim2 { E = E };
                foreach (KeyValuePair<int, int> p in M) c.M[p.Key] = p.Value;
                return c;
            }

            public override void Merge(Prop other) {
                var o = (Dim2) other;
                if (E.Op == OpDrainAdd) {
                    if (M.Count != 1 || o.M.Count == 0) return;
                    int k0 = 0, v0 = 0;
                    foreach (KeyValuePair<int, int> p in M) (k0, v0) = (p.Key, p.Value);
                    foreach (KeyValuePair<int, int> p in o.M) {
                        M.Clear();
                        M[k0 + p.Key] = v0 + p.Value;
                        break;
                    }

                    return;
                }

                foreach (KeyValuePair<int, int> p in o.M) {
                    M.TryGetValue(p.Key, out int v);
                    M[p.Key] = E.Op == OpAdd ? v + p.Value : E.Op == OpOne ? v + 1 : Math.Max(v, p.Value);
                }
            }

            public override string Format(RoEquipProperties m, int mode, bool invert) {
                var sb = new StringBuilder();
                foreach (KeyValuePair<int, int> p in M) {
                    if (!m.SepText(E, p.Key, out string sep, out bool error)) {
                        if (error) return string.Empty;
                        continue;
                    }

                    string line = m.Line(E, p.Value, mode, invert, sep);
                    if (line.Length == 0) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }

                return sb.ToString();
            }
        }

        private sealed class Flag : Prop {
            public override Prop Clone() => new Flag { E = E };
            public override void Merge(Prop other) { }
            public override string Format(RoEquipProperties m, int mode, bool invert) =>
                E.Sym.Length == 0 ? Fmt(E.Name) : Fmt(E.Name, E.Sym[invert ? 1 : 0]);
        }

        private sealed class Container {
            public readonly SortedDictionary<int, SortedDictionary<int, Prop>> Cats = new SortedDictionary<int, SortedDictionary<int, Prop>>();
            public readonly Dictionary<string, string> Uniq2 = new Dictionary<string, string>();

            public void Clear() {
                Cats.Clear();
                Uniq2.Clear();
            }

            public void Add(Prop p) {
                if (!Cats.TryGetValue(p.E.Category, out SortedDictionary<int, Prop> orders)) Cats[p.E.Category] = orders = new SortedDictionary<int, Prop>();
                if (orders.TryGetValue(p.E.Order, out Prop existing)) existing.Merge(p);
                else orders[p.E.Order] = p.Clone();
                if (p.E.Uniq2.Length > 0) Uniq2[p.E.Uniq2] = p.E.Name;
            }

            /// <summary>0x62a770: every property of <paramref name="from"/> added here.</summary>
            public void Merge(Container from) {
                foreach (SortedDictionary<int, Prop> orders in from.Cats.Values) {
                    foreach (Prop p in orders.Values) Add(p);
                }
            }

            /// <summary>0x62c490: the first of each uniq1 stays; uniq2 (flag 1) keeps only the remembered name.</summary>
            public void Finalize(bool flag) {
                var seen = new HashSet<string>();
                var drop = new List<(int, int)>();
                foreach (KeyValuePair<int, SortedDictionary<int, Prop>> cat in Cats) {
                    foreach (KeyValuePair<int, Prop> o in cat.Value) {
                        Entry e = o.Value.E;
                        if (e.Uniq1.Length > 0) {
                            if (!seen.Add(e.Uniq1)) drop.Add((cat.Key, o.Key));
                        } else if (e.Uniq2.Length > 0 && flag && (!Uniq2.TryGetValue(e.Uniq2, out string keep) || keep != e.Name)) {
                            drop.Add((cat.Key, o.Key));
                        }
                    }
                }

                foreach ((int c, int o) in drop) Cats[c].Remove(o);
            }
        }

        // ---------------------------------------------------------------- equipment (§0, §2b)

        private abstract class Equipment {
            public int Id;
            public LuaTable Info;
            public readonly Container Box = new Container();
            public bool Pretend;
            public abstract int Mask { get; }
        }

        private sealed class Wearable : Equipment {
            public RoItemInstance Item;
            public int SlotMask;
            public bool Active = true;
            public readonly List<Card> Cards = new List<Card>();
            public override int Mask => SlotMask;
        }

        private sealed class Card : Equipment {
            public Wearable Owner;
            public override int Mask => Owner?.SlotMask ?? 0;
        }

        private sealed class Combo : Equipment {
            public override int Mask => 0;
        }

        private sealed class Expansion : Equipment {
            public override int Mask => 0;
        }

        // ---------------------------------------------------------------- state

        private static RoEquipProperties instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private LuaState lua;
        private Task loading;
        private bool ready, failed;
        private readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<int, Entry> byRndOpt = new Dictionary<int, Entry>();
        private readonly Dictionary<int, int> adSkill = new Dictionary<int, int>();
        private readonly SortedDictionary<int, Wearable> wearables = new SortedDictionary<int, Wearable>();
        private readonly SortedDictionary<long, Combo> combos = new SortedDictionary<long, Combo>();
        private Expansion egg;
        private readonly Dictionary<int, int> worn = new Dictionary<int, int>();   // the worn multiset (item and card ids)
        private readonly Container total = new Container();
        private readonly Dictionary<long, int> temp = new Dictionary<long, int>();
        private Equipment current;
        private bool dirty = true;
        private RoLoginShellSession session;
        private uint seenChar;
        private int seenGameplaySequence = int.MinValue;
        private bool populated;
        private readonly Dictionary<int, string> wornKeys = new Dictionary<int, string>();
        private string statKey = string.Empty;
        private int seenEgg;

        /// <summary>Bumped when the lines may have changed (msg 0x3c to the window).</summary>
        public static int Serial { get; private set; }

        public static RoEquipProperties Instance => instance;

        // ---------------------------------------------------------------- loading (Init 0x63d6e0)

        private static readonly (string Name, int Args, int Val, int Sep)[] Accumulators = BuildAccumulators();

        private static (string, int, int, int)[] BuildAccumulators() {
            var list = new List<(string, int, int, int)>();
            void Pair(string add, string sub, int n, int v, int s) {
                list.Add((add, n, v, s));
                if (sub != null) list.Add((sub, n, v, s));
            }

            foreach (string f in new[] { "ExtParam", "Damage_Property", "Damage_Size", "MDamage_Property", "MDamage_Size" }) Pair("Add" + f, "Sub" + f, 3, 3, -1);
            Pair("ClassAddDamage", "ClassSubDamage", 3, 3, -1);
            foreach (string f in new[] {
                         "AttrTolerace", "BowAttackDamage", "CRIPercent_Race", "Damage_CRI", "EXPPercent_KillRace", "Ignore_MRES_RacePercent",
                         "Ignore_RES_RacePercent", "Mdamage_Class", "Mdamage_Race", "MeleeAttackDamage", "RangeAttackDamage", "RaceTolerace", "SkillMDamage"
                     }) {
                Pair("Add" + f, "Sub" + f, 2, 2, -1);
            }

            foreach (string f in new[] { "RaceAddDamage", "RaceAddDamageSelf", "RaceSubDamage", "RaceSubDamageSelf", "SetIgnoreDefClass_Percent", "SetIgnoreDefRace_Percent", "SetIgnoreMdefClass", "SetIgnoreMdefRace" }) {
                Pair(f, null, 2, 2, -1);
            }

            Pair("addattrtolerace", "subattrtolerace", 3, 2, -1);
            Pair("AddDamage_SKID", "SubDamage_SKID", 3, 3, 2);
            foreach (string f in new[] {
                         "GuideAttack", "HealModifyPercent", "HealValue", "MeleeAttackReflect", "ReceiveItem_Equip", "ReflectMagic", "ReflectTolerace",
                         "SPconsumption", "SpellCastTime", "SpellDelay"
                     }) {
                Pair("Add" + f, "Sub" + f, 1, 1, -1);
            }

            Pair("AddNeverknockback", null, 1, 1, -1);
            Pair("SubNeverknockback", null, 0, -1, -1);
            foreach (string f in new[] { "Clairvoyance", "IndestructibleArmor", "IndestructibleWeapon", "Magicimmune", "NoDispell", "NoJamstone", "NoMadogearfuel", "PerfectDamage", "Reincarnation", "SplashAttack" }) {
                Pair(f, null, 1, 1, -1);
            }

            foreach (string f in new[] { "HPdrain", "SPdrain", "SFCTEquipAmount", "SFCTEquipPermill" }) Pair("Add" + f, "Sub" + f, 2, 1, 2);
            Pair("addspconsumption", "subspconsumption", 2, 1, 2);
            foreach (string f in new[] { "SkillDelay", "SkillSP", "SpecificSpellCastTime" }) Pair("Add" + f, "Sub" + f, 2, 2, 1);
            Pair("EnableSkill", null, 2, 2, 1);
            foreach (string f in new[] { "BodyAttribute", "SetIgnoreDEFClass", "SetIgnoreDEFRace" }) Pair(f, null, 1, -1, -1);
            Pair("Condition", null, 3, -1, -1);
            Pair("SetInvestigate", null, 0, -1, -1);
            return list.ToArray();
        }

        /// <summary>The manager, loading its data on a worker thread the first time (the client does it at start-up).</summary>
        public static RoEquipProperties Ensure() {
            if (instance != null) return instance;
            RoAssetDatabase assets = RoClassicUiHost.GameAssets();
            if (assets == null) return null;
            instance = new RoEquipProperties();
            byte[] ep = assets.ReadBytesQuiet("data/luafiles514/lua files/equipmentproperties/equipmentproperties.lub");
            string enumVar = assets.ReadText("data/luafiles514/lua files/datainfo/enumvar.lub");
            string order = assets.ReadText("data/luafiles514/lua files/equipmentproperties/equipmentpropertiesorder.lub");
            instance.loading = Task.Run(() => instance.Load(ep, enumVar, order));
            return instance;
        }

        private void Load(byte[] ep, string enumVar, string order) {
            try {
                if (ep == null || enumVar == null || order == null) throw new InvalidOperationException("the EquipmentProperties lubs are missing");
                ParseOrder(enumVar, order);
                var state = new LuaState { InstructionLimit = 20_000_000 };
                foreach ((string name, int args, int val, int sep) in Accumulators) {
                    string n = name;
                    state.Register(n, a => Accumulate(n, a, args, val, sep));
                }

                RegisterGetters(state);
                state.DoChunk(ep, "EquipmentProperties.lub");
                lua = state;
                ready = true;
            } catch (Exception e) {
                failed = true;
                Debug.LogWarning("[equipprops] load failed: " + e.Message);
            }
        }

        /// <summary>EnumVar + EquipmentPropertiesOrder (one state in the client) read with MoonSharp, then 0x640ca0's entries.</summary>
        private void ParseOrder(string enumVar, string order) {
            var script = new Script(CoreModules.Preset_SoftSandbox);
            script.DoString(enumVar);
            script.DoString(order);
            Table root = script.Globals.Get("EquipmentPropertiesOrder").Table;
            foreach (TablePair cat in root.Pairs) {
                int category = (int) cat.Key.Number;
                Table orders = cat.Value.Table?.Get("order").Table;
                if (orders == null) continue;
                foreach (TablePair o in orders.Pairs) {
                    Table t = o.Value.Table;
                    if (t == null) continue;
                    var e = new Entry { Category = category, Order = (int) o.Key.Number };
                    e.Name = ConvertName(t.Get("name").CastToString() ?? string.Empty);
                    var funcs = new List<string>();
                    Table func = t.Get("func").Table;
                    if (func != null) {
                        for (int i = 1; i <= func.Length; i++) funcs.Add(func.Get(i).CastToString());
                    }

                    e.Funcs = funcs.ToArray();
                    Table val = t.Get("val").Table;
                    if (val != null) {
                        foreach (TablePair v in val.Pairs) {
                            e.ValIdx = (int) v.Key.Number;
                            e.Op = (int) v.Value.Number;
                        }
                    } else {
                        e.Op = OpOne;
                    }

                    Table sym = t.Get("sym").Table;
                    if (sym != null) {
                        var s = new List<string>();
                        for (int i = 1; i <= sym.Length; i++) s.Add(sym.Get(i).CastToString() ?? string.Empty);
                        e.Sym = s.ToArray();
                    }

                    Table sep = t.Get("sep").Table;
                    if (sep != null) {
                        foreach (TablePair p in sep.Pairs) {
                            e.SepIdx = (int) p.Key.Number;
                            e.SepGetter = p.Value.CastToString() ?? string.Empty;
                        }
                    }

                    Table opt = t.Get("valOpt").Table;
                    if (opt != null) {
                        foreach (TablePair p in opt.Pairs) {
                            int k = (int) p.Key.Number, v = (int) p.Value.Number;
                            if (k == 0) e.Min = v;
                            else if (k == 1) e.Max = v;
                            else if (k == 2) e.Div = v;
                            else if (k == 3) e.Float = v;
                        }
                    }

                    e.Uniq1 = t.Get("uniq1").CastToString() ?? string.Empty;
                    e.Uniq2 = t.Get("uniq2").CastToString() ?? string.Empty;
                    DynValue rnd = t.Get("rndOpt");
                    if (rnd.Type == DataType.Number) {
                        e.RndOpt = (int) rnd.Number;
                        byRndOpt[e.RndOpt] = e;
                    }

                    // the keys: each func + " arg{i}:{cond[i]}" over cond ascending
                    var cond = new SortedDictionary<int, long>();
                    Table c = t.Get("cond").Table;
                    if (c != null) {
                        foreach (TablePair p in c.Pairs) cond[(int) p.Key.Number] = (long) p.Value.Number;
                    }

                    foreach (string f in e.Funcs) {
                        var key = new StringBuilder(f);
                        foreach (KeyValuePair<int, long> p in cond) key.Append(" arg").Append(p.Key).Append(':').Append(p.Value);
                        byKey[key.ToString()] = e;
                    }
                }
            }

            Table ad = script.Globals.Get("ADSkill").Table;
            if (ad != null) {
                foreach (TablePair p in ad.Pairs) adSkill[(int) p.Key.Number] = (int) p.Value.Number;
            }
        }

        /// <summary>{val}→{0}, {sep}→{1}, {sym}→{2} with a sep else {1}; without {val} only {sym}→{0}.</summary>
        private static string ConvertName(string name) {
            if (name.Contains("{val}")) {
                bool hasSep = name.Contains("{sep}");
                return name.Replace("{val}", "{0}").Replace("{sep}", "{1}").Replace("{sym}", hasSep ? "{2}" : "{1}");
            }

            return name.Replace("{sym}", "{0}");
        }

        private static string Fmt(string template, params string[] args) {
            var sb = new StringBuilder(template.Length + 16);
            for (int i = 0; i < template.Length; i++) {
                if (template[i] == '{' && i + 2 < template.Length && char.IsDigit(template[i + 1]) && template[i + 2] == '}') {
                    int k = template[i + 1] - '0';
                    if (k < args.Length) sb.Append(args[k]);
                    i += 2;
                    continue;
                }

                sb.Append(template[i]);
            }

            return sb.ToString();
        }

        // ---------------------------------------------------------------- Lua host functions (§1a)

        /// <summary>AccumulateCall 0x63c100: the last nargs args, the value / separator by index, the rest in the key.</summary>
        private LuaReturn Accumulate(string name, LuaArgs a, int nargs, int valIdx, int sepIdx) {
            var key = new StringBuilder(name);
            int sep = -1, val = int.MaxValue, first = a.Count - nargs;
            for (int i = 1; i <= nargs; i++) {
                LuaValue v = first + i - 1 >= 0 && first + i - 1 < a.Count ? a[first + i - 1] : LuaValue.Nil;
                if (!v.TryGetNumber(out double d)) return LuaReturn.Of(false, "arg" + i + " is must number");
                int n = (int) d;
                if (i == valIdx) val = n;
                else if (i == sepIdx) sep = n;
                else key.Append(" arg").Append(i).Append(':').Append(n);
            }

            if (current != null) AddProperty(current.Box, key.ToString(), name, sep, val);
            return LuaReturn.None;
        }

        /// <summary>AddProperty 0x627800.</summary>
        private void AddProperty(Container box, string key, string func, int sep, int val) {
            if (!byKey.TryGetValue(key, out Entry e)) return;
            int sign = e.Funcs.Length > 0 && e.Funcs[0] == func ? 1 : -1;
            if (val == int.MaxValue) {
                if (sep == -1 && e.Op == OpOne && e.ValIdx == 0) box.Add(new Flag { E = e });
                return;
            }

            if (sep != -1) {
                var p = new Dim2 { E = e };
                p.M[sep] = val * sign;
                box.Add(p);
            } else if (e.Op != OpOne) {
                box.Add(new Dim1 { E = e, V = val * sign });
            } else {
                box.Add(new Flag { E = e });
            }
        }

        private static readonly int[] EqiMasks = new int[36];

        static RoEquipProperties() {
            (int Eqi, int Mask)[] map = {
                (1, 0x1), (2, 0x10), (3, 0x20), (4, 0x2), (5, 0x4), (6, 0x40), (7, 0x8), (8, 0x80), (10, 0x100), (11, 0x200), (20, 0x400),
                (21, 0x800), (22, 0x1000), (23, 0x2000), (30, 0x10000), (31, 0x20000), (32, 0x40000), (33, 0x80000), (34, 0x100000), (35, 0x200000)
            };
            foreach ((int eqi, int mask) in map) EqiMasks[eqi] = mask;
        }

        private static int EqiToMask(int eqi) => eqi >= 0 && eqi < EqiMasks.Length ? EqiMasks[eqi] : 0;

        /// <summary>MaskToEqi 0x63d460 (combined masks too).</summary>
        private static int MaskToEqi(int mask) {
            switch (mask) {
                case 0x22: return 4;
                case 0x101: case 0x201: case 0x301: return 1;
                case 0x300: return 10;
                case 0xc00: case 0x1400: case 0x1c00: return 20;
                case 0x1800: return 21;
                case 0x8000: return 0;
            }

            for (int i = 0; i < EqiMasks.Length; i++) {
                if (EqiMasks[i] != 0 && EqiMasks[i] == mask) return i;
            }

            return 0;
        }

        private RoItemInstance WornAt(int mask) {
            if (mask == 0 || session == null) return null;
            foreach (RoItemInstance item in session.EquipmentSnapshot().InventoryItems ?? Array.Empty<RoItemInstance>()) {
                if (item != null && ((int) item.WearState & mask) != 0) return item;
            }

            return null;
        }

        private void RegisterGetters(LuaState state) {
            state.Register("get", a => {
                if (!a[0].TryGetNumber(out double d)) return LuaReturn.Of(false, "arg1 is must number");
                RoPlayerStatusSnapshot s = session?.BasicInfoSnapshot().Status;
                if (s == null) return 0;
                switch ((int) d) {
                    case 7: return s.Sp;
                    case 8: return s.MaxSp;
                    case 11: return s.BaseLevel;
                    case 19: return s.JobId;
                    case 32: return s.Str;
                    case 33: return s.Agi;
                    case 34: return s.Vit;
                    case 35: return s.Int;
                    case 36: return s.Dex;
                    case 37: return s.Luk;
                    case 55: return s.JobLevel;
                    default: return 0;
                }
            });
            state.Register("GetRefineLevel", a => a[0].TryGetNumber(out double d) ? WornAt(EqiToMask((int) d))?.RefineLevel ?? 0 : LuaReturn.Of(false, "arg1 is must number"));
            state.Register("GetEquipGradeLevel", a => a[0].TryGetNumber(out double d) ? (int) d == 0 ? 0 : (WornAt(EqiToMask((int) d))?.EnchantGrade ?? 0) : LuaReturn.Of(false, "arg1 is must number"));
            state.Register("GetItemIDLocation", a => a[0].TryGetNumber(out double d) ? WornAt(EqiToMask((int) d))?.ItemId ?? 0 : LuaReturn.Of(false, "arg1 is must number"));
            state.Register("GetLocation", a => MaskToEqi(current?.Mask ?? 0));
            state.Register("GetWeaponClass", a => a[0].TryGetNumber(out double d) ? Math.Max(0, Field(Wearer((int) d), 1)) : LuaReturn.Of(false, "arg1 is must number"));
            state.Register("GetEquipArmorLv", a => a[0].TryGetNumber(out double d) ? Field(Wearer((int) d), 2) : LuaReturn.Of(false, "arg1 is must number"));
            state.Register("GetEquipWeaponLv", a => a[0].TryGetNumber(out double d) ? Field(Wearer((int) d), 2) : LuaReturn.Of(false, "arg1 is must number"));
            state.Register("GetSkillLevel", a => {
                if (!a[0].TryGetNumber(out double d)) return LuaReturn.Of(false, "arg1 is must number");
                foreach (RoGameplaySkillInfo skill in session?.SkillSnapshot().Skills ?? Array.Empty<RoGameplaySkillInfo>()) {
                    if (skill.SkillId == (int) d) return skill.Level;
                }

                return 0;
            });
            state.Register("GetPureJob", a => PureJob(session?.BasicInfoSnapshot().Status?.JobId ?? 0));
            state.Register("GetPetRelationship", a => {
                int intimacy = session != null ? session.PetSnapshot().Pet.Intimacy : 0;
                return intimacy < 101 ? 0 : intimacy < 251 ? 1 : intimacy < 751 ? 2 : intimacy <= 900 ? 3 : 4;
            });
            state.Register("IsPremiumPcCafe", a => 0);
            state.Register("GetMapName", a => UnityEngine.Object.FindFirstObjectByType<Ragnarok.Maps.RoRuntimeMapController>()?.MapName ?? string.Empty);
            state.Register("SetEquipTempValue", a => {
                if (a[0].TryGetNumber(out double k) && a[1].TryGetNumber(out double v)) temp[(long) k] = (int) v;
                return LuaReturn.None;
            });
            state.Register("GetEquipTempValue", a => a[0].TryGetNumber(out double k) && temp.TryGetValue((long) k, out int v) ? v : 0);
            state.Register("C_Logger", a => {
                Chat(a.Count > 0 ? a[0].AsString ?? string.Empty : string.Empty, "#FFFF00");
                return LuaReturn.None;
            });
        }

        private Wearable Wearer(int eqi) => wearables.TryGetValue(eqi, out Wearable w) ? w : null;

        /// <summary>ItemInfo fields (vf04 0x627b90): 0 element, 1 weapon class, 2 level; 0 for an empty slot, −1 without an Item entry.</summary>
        private static int Field(Wearable w, int field) {
            if (w == null) return 0;
            if (w.Info == null) return -1;
            ItemShape shape = Shape(w.Info);
            return field == 0 ? shape.Element : field == 1 ? shape.Class : shape.Level;
        }

        private static int PureJob(int job) {
            int[][] groups = {
                new[] { 1, 1, 7, 14, 4002, 4008, 4015, 4024, 4030, 4037, 4054, 4060, 4066, 4073, 4096, 4102, 4252, 4258 },
                new[] { 2, 2, 9, 16, 4003, 4010, 4017, 4025, 4032, 4039, 4055, 4061, 4067, 4074, 4097, 4103, 4255, 4261 },
                new[] { 3, 3, 11, 19, 20, 4004, 4012, 4020, 4021, 4026, 4034, 4042, 4043, 4056, 4062, 4068, 4069, 4075, 4076, 4098, 4104, 4105, 4257, 4263, 4264 },
                new[] { 4, 4, 8, 15, 4005, 4009, 4016, 4027, 4031, 4038, 4057, 4063, 4070, 4077, 4099, 4106, 4256, 4262 },
                new[] { 5, 5, 10, 18, 4006, 4011, 4019, 4028, 4033, 4041, 4058, 4064, 4071, 4078, 4100, 4107, 4253, 4259 },
                new[] { 6, 6, 12, 17, 4007, 4013, 4018, 4029, 4035, 4040, 4059, 4065, 4072, 4079, 4101, 4108, 4254, 4260 },
                new[] { 24, 24, 4215, 4228, 4229, 4306 },
                new[] { 25, 25, 4211, 4212, 4222, 4223, 4224, 4304, 4305 },
                new[] { 4046, 4046, 4047, 4048, 4049, 4225, 4226, 4227, 4239, 4240, 4241, 4242, 4302, 4303 },
                new[] { 4218, 4218, 4220, 4308 },
            };
            foreach (int[] g in groups) {
                for (int i = 1; i < g.Length; i++) {
                    if (g[i] == job) return g[0];
                }
            }

            return 0;
        }

        // ---------------------------------------------------------------- the Item table (parser 0x644330)

        private struct ItemShape {
            public int Kind, Element, Class, Level;
            public (int Param, int Value)[] Stats;
        }

        private static readonly Dictionary<LuaTable, ItemShape> Shapes = new Dictionary<LuaTable, ItemShape>();

        /// <summary>Type → kind and the Stat layout; only non-zero stats are kept, translated to Order params.</summary>
        private static ItemShape Shape(LuaTable info) {
            if (Shapes.TryGetValue(info, out ItemShape shape)) return shape;
            string type = info["Type"].AsString ?? string.Empty;
            LuaTable stat = info["Stat"].AsTable;
            const int Str = 103, Agi = 104, Vit = 105, Int = 106, Dex = 107, Luk = 108, Pow = 234, Sta = 235, Wis = 236, Spl = 237, Con = 238, Crt = 239;
            int[] layout;   // the Order param per position: 0 = skip, −1 element, −2 class, −3 level
            switch (type) {
                case "armor":
                    shape.Kind = 1;
                    layout = new[] { 45, Str, Int, Vit, Dex, Agi, Luk, 109, 110, 47, -3, Pow, Spl, Sta, Wis, Con, Crt };
                    break;
                case "Mweapon":
                    shape.Kind = 2;
                    layout = new[] { -1, -2, 41, 200, Str, Int, Vit, Dex, Agi, Luk, -3, Pow, Spl, Sta, Wis, Con, Crt };
                    break;
                case "Rweapon":
                    shape.Kind = 3;
                    layout = new[] { -2, 41, Str, Int, Vit, Dex, Agi, Luk, -3, Pow, Spl, Sta, Wis, Con, Crt };
                    break;
                case "ammo":
                    shape.Kind = 4;
                    layout = new[] { -1, 41 };
                    break;
                default:
                    shape.Kind = type == "special" ? 5 : 0;
                    layout = Array.Empty<int>();
                    break;
            }

            var stats = new List<(int, int)>();
            if (stat != null && stat.Length == layout.Length) {
                for (int i = 0; i < layout.Length; i++) {
                    int v = stat[i + 1].TryGetNumber(out double d) ? (int) d : 0;
                    switch (layout[i]) {
                        case -1: shape.Element = v; break;
                        case -2: shape.Class = v; break;
                        case -3: shape.Level = v; break;
                        default:
                            if (v != 0) stats.Add((layout[i], v));
                            break;
                    }
                }
            }

            shape.Stats = stats.ToArray();
            Shapes[info] = shape;
            return shape;
        }

        private LuaTable ItemInfo(int id) => lua?.GetGlobal("Item").AsTable?[id].AsTable;

        /// <summary>RB 0x63d350: trunc(RefiningBonus[kind][level][refine]); 0 when missing or refine 0.</summary>
        private int Rb(string kind, int level, int refine) {
            if (refine <= 0) return 0;
            LuaTable rows = lua.GetGlobal("RefiningBonus").AsTable?[kind].AsTable?[level].AsTable;
            return rows != null && rows[refine].TryGetNumber(out double d) ? (int) d : 0;
        }

        /// <summary>GB 0x63cbc0: GradeBonus.NORMAL_WEAPON[grade][field], 0 without a grade.</summary>
        private int Gb(int grade, string field) {
            string[] grades = { null, "GRADE_MAGIC", "GRADE_RARE", "GRADE_EPIC", "GRADE_UNIQUE", "GRADE_LEGEND", "GRADE_ANCIENT", "GRADE_AWAKEN" };
            if (grade <= 0 || grade >= grades.Length) return 0;
            LuaTable row = lua.GetGlobal("GradeBonus").AsTable?["NORMAL_WEAPON"].AsTable?[grades[grade]].AsTable;
            return row != null && row[field].TryGetNumber(out double d) ? (int) d : 0;
        }

        // ---------------------------------------------------------------- runs (§2b)

        private void Begin(Equipment e) {
            e.Box.Clear();
            current = e;
            temp.Clear();
        }

        private void CallOnStart(LuaTable info) {
            if (info == null) return;
            LuaValue fn = info["OnStartEquip"];
            if (fn.IsNil) return;
            try {
                lua.Call(fn);
            } catch (LuaException) {
                // lua_pcall: the error is ignored
            }
        }

        /// <summary>Wearable::Run 0x628140.</summary>
        private void Run(Wearable w, bool print) {
            Begin(w);
            w.Info = ItemInfo(w.Id);
            if (w.Info != null) {
                ItemShape shape = Shape(w.Info);
                foreach ((int param, int v) in shape.Stats) AddProperty(w.Box, "AddExtParam arg1:0 arg2:" + param, "AddExtParam", -1, v);
                int refine = w.Item.RefineLevel, grade = w.Item.EnchantGrade;
                bool costume = (w.SlotMask & 0x3ffc00) != 0 && (w.SlotMask & 0x3ff) == 0;
                if (!costume && refine > 0) {
                    if (shape.Kind == 1) {
                        int v = Rb("ARMOR_DEF", shape.Level, refine);
                        if (v != 0) AddProperty(w.Box, "AddExtParam arg1:0 arg2:45", "AddExtParam", -1, v);
                        v = Rb("ARMOR_RES", shape.Level, refine);
                        if (v != 0) {
                            AddProperty(w.Box, "AddExtParam arg1:0 arg2:244", "AddExtParam", -1, v);
                            AddProperty(w.Box, "AddExtParam arg1:0 arg2:245", "AddExtParam", -1, v);
                        }
                    } else if (shape.Kind == 2 || shape.Kind == 3) {
                        int v = Rb("WEAPON_ATK", shape.Level, refine);
                        if (v != 0) {
                            AddProperty(w.Box, "AddExtParam arg1:0 arg2:41", "AddExtParam", -1, (int) ((float) (Gb(grade, "REFINING_WEAPON_ATK_MULTIPLE") * v) / 10f + v));
                            if (shape.Kind == 2) {
                                AddProperty(w.Box, "AddExtParam arg1:0 arg2:200", "AddExtParam", -1, (int) ((float) (Gb(grade, "REFINING_WEAPON_MATK_MULTIPLE") * v) / 10f + v));
                            }
                        }

                        v = Rb("WEAPON_ADV_ATK", shape.Level, refine);
                        if (v != 0) {
                            AddProperty(w.Box, "AddExtParam arg1:0 arg2:242", "AddExtParam", -1, v);
                            AddProperty(w.Box, "AddExtParam arg1:0 arg2:243", "AddExtParam", -1, v);
                        }
                    }
                }

                CallOnStart(w.Info);
                w.Pretend = w.Info["OnStartPretendEquip"].Type == LuaType.Boolean || (w.Id >= 9001 && w.Id < 9500);
            }

            // the random options (built in the ctor from item +0x90)
            for (int i = 0; i < RoItemInstance.MaxRandomOptions; i++) {
                RoItemRandomOption o = w.Item.GetRandomOption(i);
                if (o.IsEmpty || o.Index == 0) continue;
                AddRandomOption(w.Box, o.Index, o.Value, o.Param);
            }

            foreach (Card c in w.Cards) {
                RunCard(c);
                w.Box.Merge(c.Box);
            }

            w.Box.Finalize(true);
            current = null;
            if (print) Echo(w.Box, false, string.Empty);
        }

        private void AddRandomOption(Container box, int id, int value, int param) {
            if (!byRndOpt.TryGetValue(id, out Entry e) || (e.Funcs.Length > 0 && e.Funcs[0] == "SetWeaponAttr")) return;
            int mul = e.Div ?? 1;
            if (e.SepIdx != 0) {
                var p = new Dim2 { E = e };
                p.M[param] = value * mul;
                box.Add(p);
            } else if (e.Op != OpOne) {
                box.Add(new Dim1 { E = e, V = value * mul });
                if (id == 35 && byRndOpt.TryGetValue(25, out Entry other)) box.Add(new Dim1 { E = other, V = -value });
            } else {
                box.Add(new Flag { E = e });
            }
        }

        /// <summary>Attachable::Run 0x627cc0: only OnStartEquip; the owner turns pretend too.</summary>
        private void RunCard(Card c) {
            Begin(c);
            c.Info = ItemInfo(c.Id);
            CallOnStart(c.Info);
            if (c.Info != null && c.Info["OnStartPretendEquip"].Type == LuaType.Boolean && c.Owner != null) c.Owner.Pretend = true;
        }

        /// <summary>Combiitem::Run 0x627e30 ("(C)").</summary>
        private void Run(Combo c, bool print) {
            Begin(c);
            c.Info = lua.GetGlobal("Combiitem").AsTable?[(double) c.Id].AsTable;
            CallOnStart(c.Info);
            c.Pretend = c.Info != null && c.Info["OnStartPretendEquip"].Type == LuaType.Boolean;
            c.Box.Finalize(true);
            current = null;
            if (print) Echo(c.Box, false, "(C)");
        }

        /// <summary>Expansion::Run 0x627fd0 (the pet egg, "(P)").</summary>
        private void Run(Expansion x, bool print) {
            Begin(x);
            x.Info = ItemInfo(x.Id);
            CallOnStart(x.Info);
            x.Pretend = true;
            x.Box.Finalize(true);
            current = null;
            if (print) Echo(x.Box, false, "(P)");
        }

        /// <summary>0x628ab0: the container's lines, "#" → " ", "~" removed, + the suffix; equip #00FFC8, removal #FF0000 (type 23).</summary>
        private void Echo(Container box, bool removal, string suffix) {
            foreach (SortedDictionary<int, Prop> orders in box.Cats.Values) {
                foreach (Prop p in orders.Values) {
                    foreach (string piece in p.Format(this, 1, removal).Split('\n')) {
                        string line = piece.Replace("#", " ").Replace("~", string.Empty);
                        if (line.Length > 0) Chat(line + suffix, removal ? "#FF0000" : "#00FFC8", 23);
                    }
                }
            }
        }

        private static void Chat(string text, string colour, int extraFlag = -1) {
            RoClassicChat chat = RoClassicChat.Instance;
            if (chat == null) return;
            if (extraFlag >= 0) chat.AddFlaggedLine(text, colour, extraFlag);
            else chat.AddLine(text, colour);
        }

        // ---------------------------------------------------------------- the manager's entry points (§5)

        private void ClearAll() {
            wearables.Clear();
            combos.Clear();
            egg = null;
            worn.Clear();
            current = null;
            dirty = true;
        }

        /// <summary>AddWearable 0x646a00: the slot replaced silently, Run, then the combos of the item and its cards.</summary>
        private void AddWearable(RoItemInstance item, bool print) {
            int mask = (int) item.WearState;
            int eqi = MaskToEqi(mask);
            var w = new Wearable { Id = item.ItemId, Item = item, SlotMask = mask };
            bool forged = item.GetCardId(0) == 0x00ff || item.GetCardId(0) == 0x00fe || item.GetCardId(0) == 0xff00;
            for (int i = 0; i < RoItemInstance.MaxCardSlots; i++) {
                int card = item.GetCardId(i);
                if (card != 0 && !forged) w.Cards.Add(new Card { Id = card, Owner = w });
            }

            wearables[eqi] = w;
            dirty = true;
            Run(w, print);
            ComboAdd(w.Id, print);
            foreach (Card c in w.Cards) ComboAdd(c.Id, print);
        }

        /// <summary>RemoveWearable 0x646cf0: the combos of its cards and the item, the red echo, then gone.</summary>
        private void RemoveWearable(int eqi, bool print) {
            if (!wearables.TryGetValue(eqi, out Wearable w)) return;
            dirty = true;
            foreach (Card c in w.Cards) ComboRemove(c.Id, print);
            ComboRemove(w.Id, print);
            if (print && w.Active) Echo(w.Box, true, string.Empty);
            wearables.Remove(eqi);
        }

        /// <summary>0x63c3d0: the id counted; each of its combos activates when every listed id is worn.</summary>
        private void ComboAdd(int id, bool print) {
            worn.TryGetValue(id, out int n);
            worn[id] = n + 1;
            LuaTable list = ItemInfo(id)?["Combiitem"].AsTable;
            if (list == null) return;
            for (int i = 1; i <= list.Length; i++) {
                if (!list[i].TryGetNumber(out double comboId) || combos.ContainsKey((long) comboId)) continue;
                LuaTable needed = lua.GetGlobal("Combiitem").AsTable?[comboId].AsTable?["Item"].AsTable;
                if (needed == null) continue;
                bool all = true;
                for (int k = 1; k <= needed.Length && all; k++) all = needed[k].TryGetNumber(out double need) && worn.TryGetValue((int) need, out int c) && c > 0;
                if (!all) continue;
                var combo = new Combo { Id = (int) (long) comboId };
                combos[(long) comboId] = combo;
                Run(combo, print);
            }
        }

        /// <summary>0x63c930: every copy of the id gone; its combos no longer met are removed (red echo with print).</summary>
        private void ComboRemove(int id, bool print) {
            worn.Remove(id);
            LuaTable list = ItemInfo(id)?["Combiitem"].AsTable;
            if (list == null) return;
            for (int i = 1; i <= list.Length; i++) {
                if (!list[i].TryGetNumber(out double comboId) || !combos.TryGetValue((long) comboId, out Combo combo)) continue;
                if (print) Echo(combo.Box, true, "(C)");
                combos.Remove((long) comboId);
            }
        }

        /// <summary>OnStatChange 0x647470: the pretend equipment runs again.</summary>
        private void StatChanged() {
            foreach (Wearable w in wearables.Values) {
                if (!w.Pretend) continue;
                Run(w, false);
                dirty = true;
            }

            foreach (Combo c in combos.Values) {
                if (!c.Pretend) continue;
                Run(c, false);
                dirty = true;
            }

            if (egg != null) {
                Run(egg, false);
                dirty = true;
            }
        }

        // ---------------------------------------------------------------- per frame: the equip packets' effect

        /// <summary>
        /// The equipment from the session: the first full list (a new character) loads silently (ClearAll + adds); later a
        /// slot that empties prints red (take-off ACK), a slot that fills prints its lines (equip ACK). Stat changes re-run
        /// the pretend items; the called pet's egg is the Expansion.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession) {
            if (loginSession == null) return;
            RoEquipProperties m = Ensure();
            if (m == null || !m.ready) return;
            m.session = loginSession;
            m.Step();
        }

        private void Step() {
            (uint charId, _) = session.LocalCharacterIds();
            if (charId != seenChar) {
                seenChar = charId;
                populated = false;
                wornKeys.Clear();
                ClearAll();
            }

            // the equipment, the inventory and the stats change only with the session's gameplay state: unchanged, the
            // snapshots, the dictionary and the stat key below need not be built again (they were, every frame)
            int sequence = session.GameplayChangeSequence;
            if (populated && sequence == seenGameplaySequence) {
                return;
            }

            seenGameplaySequence = sequence;

            RoItemInstance[] items = session.EquipmentSnapshot().InventoryItems ?? Array.Empty<RoItemInstance>();
            var now = new Dictionary<int, (string Key, RoItemInstance Item)>();
            foreach (RoItemInstance item in items) {
                if (item == null || item.WearState == RoItemEquipLocation.None || item.WearState == RoItemEquipLocation.Ammo) continue;
                int eqi = MaskToEqi((int) item.WearState);
                if (eqi == 0) continue;
                now[eqi] = (ItemKey(item), item);
            }

            if (!populated) {
                if (now.Count == 0) return;
                populated = true;
                foreach (KeyValuePair<int, (string Key, RoItemInstance Item)> p in now) {
                    wornKeys[p.Key] = p.Value.Key;
                    AddWearable(p.Value.Item, false);
                }

                Serial++;
            } else {
                bool changed = false;
                var gone = new List<int>();
                foreach (KeyValuePair<int, string> p in wornKeys) {
                    if (!now.TryGetValue(p.Key, out var n) || n.Key != p.Value) gone.Add(p.Key);
                }

                foreach (int eqi in gone) {
                    RemoveWearable(eqi, true);
                    wornKeys.Remove(eqi);
                    changed = true;
                }

                foreach (KeyValuePair<int, (string Key, RoItemInstance Item)> p in now) {
                    if (wornKeys.ContainsKey(p.Key)) continue;
                    wornKeys[p.Key] = p.Value.Key;
                    AddWearable(p.Value.Item, true);
                    changed = true;
                }

                if (changed) Serial++;
            }

            // ZC_PAR_CHANGE / skill lists: the pretend items again
            RoPlayerStatusSnapshot s = session.BasicInfoSnapshot().Status;
            string stats = s == null ? string.Empty : s.BaseLevel + "/" + s.JobLevel + "/" + s.JobId + "/" + s.Str + "/" + s.Agi + "/" + s.Vit + "/" + s.Int + "/" + s.Dex + "/" + s.Luk;
            if (stats != statKey) {
                statKey = stats;
                StatChanged();
                Serial++;
            }

            // the called pet's egg (the inventory marks it damaged, DAT_0154ecfc)
            int eggId = CalledEgg(items);
            if (eggId != seenEgg) {
                if (egg != null) {
                    Echo(egg.Box, true, "(P)");
                    egg = null;
                }

                seenEgg = eggId;
                if (eggId != 0) {
                    egg = new Expansion { Id = eggId };
                    Run(egg, false);
                }

                dirty = true;
                Serial++;
            }
        }

        private int CalledEgg(RoItemInstance[] items) {
            foreach (RoItemInstance item in items) {
                if (item != null && item.IsDamaged && item.ItemTypeRaw == 7) return item.ItemId;
            }

            return 0;
        }

        private static string ItemKey(RoItemInstance item) {
            var sb = new StringBuilder();
            sb.Append(item.BagIndex).Append('/').Append(item.ItemId).Append('/').Append(item.RefineLevel).Append('/').Append(item.EnchantGrade);
            for (int i = 0; i < RoItemInstance.MaxCardSlots; i++) sb.Append('/').Append(item.GetCardId(i));
            for (int i = 0; i < RoItemInstance.MaxRandomOptions; i++) {
                RoItemRandomOption o = item.GetRandomOption(i);
                sb.Append('/').Append(o.Index).Append(':').Append(o.Value);
            }

            return sb.ToString();
        }

        // ---------------------------------------------------------------- GetLines (§4)

        /// <summary>Rebuild 0x6471f0 (when dirty) and GetLines 0x63ce90: category 0 = all, "value#label" strings.</summary>
        public List<string> Lines(int category) {
            var lines = new List<string>();
            if (!ready) return lines;
            if (dirty) {
                total.Clear();
                foreach (Wearable w in wearables.Values) {
                    if (w.Active) total.Merge(w.Box);
                }

                if (egg != null) total.Merge(egg.Box);
                foreach (Combo c in combos.Values) total.Merge(c.Box);
                total.Finalize(false);
                dirty = false;
            }

            foreach (KeyValuePair<int, SortedDictionary<int, Prop>> cat in total.Cats) {
                if (category != 0 && cat.Key != category) continue;
                foreach (Prop p in cat.Value.Values) {
                    string s = p.Format(this, 0, false);
                    if (s.Length == 0) continue;
                    lines.AddRange(s.Split('\n'));
                }
            }

            return lines;
        }

        public bool Ready => ready;
        public bool Failed => failed;

        /// <summary>1Dim vf03 0x62b260 (per sep key for 2Dim).</summary>
        private string Line(Entry e, int value, int mode, bool invert, string sep) {
            int v = value;
            if (mode == 0) {
                if (e.Min.HasValue) v = Math.Max(v, e.Min.Value);
                if (e.Max.HasValue) v = Math.Min(v, e.Max.Value);
            }

            double d = v;
            if (e.Div.HasValue && e.Div.Value != 0) d /= e.Div.Value;
            int prec = e.Float ?? 0;
            int sign = d < 0 && e.Sym.Length == 2 ? -1 : 1;
            double p = Math.Pow(10.0, prec), q = Math.Floor(d * p) / p;
            if (q == 0) return string.Empty;
            string num = (sign * q).ToString("F" + prec, CultureInfo.InvariantCulture);
            bool neg = q <= 0, idx = invert ? !neg : neg;
            if (e.Sym.Length == 0) return sep != null ? Fmt(e.Name, num, sep) : Fmt(e.Name, num);
            if (e.Sym.Length != 2) return string.Empty;
            string symbol = e.Sym[idx ? 1 : 0];
            return (idx ? "~" : string.Empty) + (sep != null ? Fmt(e.Name, num, sep, symbol) : Fmt(e.Name, num, symbol));
        }

        /// <summary>The sep getters (2Dim vf03 0x62b790): false = skip the pair, error = the whole property empty.</summary>
        private bool SepText(Entry e, int k, out string text, out bool error) {
            text = string.Empty;
            error = false;
            switch (e.SepGetter) {
                case "GetSkillName":
                    text = SkillName(k);
                    return text.Length > 0;
                case "GetNumber":
                    text = k.ToString(CultureInfo.InvariantCulture);
                    return true;
                case "GetSkillGroup": {
                    if (k == 0) return true;
                    LuaTable group = lua.GetGlobal("SkillGroup").AsTable?[k].AsTable;
                    if (group == null) {
                        error = true;
                        return false;
                    }

                    var names = new List<string>();
                    for (int i = 1; i <= group.Length; i++) {
                        if (group[i].TryGetNumber(out double skid)) names.Add(SkillName((int) skid));
                    }

                    text = string.Join(", ", names) + " ";
                    return true;
                }
                case "GetAddDamageSkillName": {
                    int m = adSkill.TryGetValue(k, out int mapped) ? mapped : k;
                    if (m == 0) return false;
                    text = SkillName(m);
                    return text.Length > 0;
                }
                default:
                    error = true;
                    return false;
            }
        }

        private static string SkillName(int skid) {
            RoSkillMetadataTable table = RoClassicUiHost.GameAssets()?.GameData?.Skills;
            return table != null && table.TryGetSkill(skid, out RoSkillMetadata meta) ? meta.DisplayName ?? string.Empty : string.Empty;
        }

        internal static string Describe() {
            RoEquipProperties m = instance;
            if (m == null) return "not loaded";
            return "ready=" + m.ready + " failed=" + m.failed + " entries=" + m.byKey.Count + " worn=" + m.wearables.Count + " combos=" + m.combos.Count +
                " egg=" + (m.egg?.Id ?? 0) + " lines=" + string.Join(" | ", m.Lines(0));
        }
    }
}
