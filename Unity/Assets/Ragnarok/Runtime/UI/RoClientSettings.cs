using System;
using System.Text;
using System.Collections.Generic;
using Ragnarok.Maps;
using UnityEngine;

namespace Ragnarok.UI {
    public enum RoClientSettingsStatus {
        Applied = 0,
        InvalidValue = 1,
        UnknownAction = 2,
        Conflict = 3
    }

    public enum RoUiScaleMode {
        Auto = 0,
        Manual = 1
    }

    public readonly struct RoClientSettingsResult {
        public RoClientSettingsResult(RoClientSettingsStatus status, string message) {
            Status = status;
            Message = message ?? string.Empty;
        }

        public RoClientSettingsStatus Status { get; }
        public string Message { get; }
        public bool Applied => Status == RoClientSettingsStatus.Applied;

        public static RoClientSettingsResult Success() {
            return new RoClientSettingsResult(RoClientSettingsStatus.Applied, string.Empty);
        }
    }

    public readonly struct RoKeyChord : IEquatable<RoKeyChord> {
        public static readonly RoKeyChord None = new RoKeyChord(KeyCode.None, EventModifiers.None);

        public RoKeyChord(KeyCode key, EventModifiers modifiers = EventModifiers.None) {
            Key = key;
            Modifiers = key == KeyCode.None ? EventModifiers.None : NormalizeModifiers(modifiers);
        }

        public KeyCode Key { get; }
        public EventModifiers Modifiers { get; }
        public bool IsAssigned => Key != KeyCode.None;

        public string ToDisplayString() {
            if (!IsAssigned) {
                return "Not Assigned";
            }

            string key = FormatKey(Key);
            string modifiers = FormatModifiers(Modifiers);
            return string.IsNullOrEmpty(modifiers) ? key : modifiers + " + " + key;
        }

        public bool Equals(RoKeyChord other) {
            return Key == other.Key && Modifiers == other.Modifiers;
        }

        public override bool Equals(object obj) {
            return obj is RoKeyChord other && Equals(other);
        }

        public override int GetHashCode() {
            return ((int) Key * 397) ^ (int) Modifiers;
        }

        public override string ToString() {
            return ToDisplayString();
        }

        public static bool operator ==(RoKeyChord left, RoKeyChord right) {
            return left.Equals(right);
        }

        public static bool operator !=(RoKeyChord left, RoKeyChord right) {
            return !left.Equals(right);
        }

        public static bool TryParse(string value, out RoKeyChord chord) {
            chord = None;
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            string trimmed = value.Trim();
            if (string.Equals(trimmed, "Not Assigned", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase)) {
                chord = None;
                return true;
            }

            string[] parts = trimmed.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            EventModifiers modifiers = EventModifiers.None;
            KeyCode key = KeyCode.None;
            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i].Trim();
                if (string.Equals(part, "Alt", StringComparison.OrdinalIgnoreCase)) {
                    modifiers |= EventModifiers.Alt;
                } else if (string.Equals(part, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(part, "Control", StringComparison.OrdinalIgnoreCase)) {
                    modifiers |= EventModifiers.Control;
                } else if (string.Equals(part, "Shift", StringComparison.OrdinalIgnoreCase)) {
                    modifiers |= EventModifiers.Shift;
                } else if (TryParseKey(part, out KeyCode parsedKey)) {
                    key = parsedKey;
                } else {
                    return false;
                }
            }

            if (key == KeyCode.None) {
                return false;
            }

            chord = new RoKeyChord(key, modifiers);
            return true;
        }

        public static string FormatKey(KeyCode key) {
            switch (key) {
                case KeyCode.None:
                    return "Not Assigned";
                case KeyCode.Escape:
                    return "Esc";
                case KeyCode.Return:
                    return "Return";
                case KeyCode.Insert:
                    return "Ins";
                case KeyCode.Print:
                    return "Snapshot";
                case KeyCode.BackQuote:
                    return "`";
                case KeyCode.Quote:
                    return "'";
                case KeyCode.Alpha0:
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                case KeyCode.Alpha5:
                case KeyCode.Alpha6:
                case KeyCode.Alpha7:
                case KeyCode.Alpha8:
                case KeyCode.Alpha9:
                    return ((int) key - (int) KeyCode.Alpha0).ToString();
                default:
                    return key.ToString();
            }
        }

        public static string FormatModifiers(EventModifiers modifiers) {
            modifiers = NormalizeModifiers(modifiers);
            string result = string.Empty;
            AppendModifier(ref result, modifiers, EventModifiers.Alt, "Alt");
            AppendModifier(ref result, modifiers, EventModifiers.Control, "Ctrl");
            AppendModifier(ref result, modifiers, EventModifiers.Shift, "Shift");
            return result;
        }

        private static bool TryParseKey(string value, out KeyCode key) {
            key = KeyCode.None;
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            string normalized = value.Trim();
            if (normalized.Length == 1) {
                char c = char.ToUpperInvariant(normalized[0]);
                if (c >= 'A' && c <= 'Z') {
                    key = (KeyCode) ((int) KeyCode.A + (c - 'A'));
                    return true;
                }

                if (c >= '0' && c <= '9') {
                    key = (KeyCode) ((int) KeyCode.Alpha0 + (c - '0'));
                    return true;
                }

                if (c == '`') {
                    key = KeyCode.BackQuote;
                    return true;
                }

                if (c == '\'') {
                    key = KeyCode.Quote;
                    return true;
                }
            }

            if (string.Equals(normalized, "Esc", StringComparison.OrdinalIgnoreCase)) {
                key = KeyCode.Escape;
                return true;
            }

            if (string.Equals(normalized, "Ins", StringComparison.OrdinalIgnoreCase)) {
                key = KeyCode.Insert;
                return true;
            }

            if (string.Equals(normalized, "Snapshot", StringComparison.OrdinalIgnoreCase)) {
                key = KeyCode.Print;
                return true;
            }

            if (Enum.TryParse(normalized, true, out KeyCode parsed)) {
                key = parsed;
                return true;
            }

            return false;
        }

        private static EventModifiers NormalizeModifiers(EventModifiers modifiers) {
            return modifiers & (EventModifiers.Alt | EventModifiers.Control | EventModifiers.Shift);
        }

        private static void AppendModifier(ref string result, EventModifiers modifiers, EventModifiers flag, string label) {
            if ((modifiers & flag) == 0) {
                return;
            }

            result = string.IsNullOrEmpty(result) ? label : result + " + " + label;
        }
    }

    public readonly struct RoClientKeybindSnapshot {
        public RoClientKeybindSnapshot(string actionId, string displayName, string key) {
            ActionId = actionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Key = key ?? string.Empty;
        }

        public string ActionId { get; }
        public string DisplayName { get; }
        public string Key { get; }
    }

    public sealed class RoShortcutBindingSnapshot {
        public RoShortcutBindingSnapshot(
            string actionId,
            string displayName,
            string category,
            int page,
            RoKeyChord binding,
            bool runtimeSupported) {
            ActionId = actionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
            Page = Math.Max(1, page);
            Binding = binding;
            RuntimeSupported = runtimeSupported;
        }

        public string ActionId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Page { get; }
        public RoKeyChord Binding { get; }
        public bool RuntimeSupported { get; }
    }

    public sealed class RoClientSettingsSnapshot {
        public RoClientSettingsSnapshot(
            float uiScale,
            RoUiScaleMode uiScaleMode,
            float manualUiScale,
            float effectiveUiScale,
            float masterVolume,
            float bgmVolume,
            float effectsVolume,
            bool muted,
            RoClientKeybindSnapshot[] keybinds,
            RoShortcutBindingSnapshot[] shortcuts,
            bool battleModeEnabled,
            bool bgmMuted,
            bool effectsMuted,
            bool showAura,
            bool simpleAura,
            bool showSkillEffects,
            bool showShadow,
            bool expandDamageDisplay,
            int damageMotion,
            int damageSkin,
            bool showMiss,
            bool showMapName,
            bool fogEffect,
            bool showAllFootprints,
            bool mouseSnapMonster,
            bool mouseSnapSkill,
            bool mouseSnapPickupItem,
            bool noCtrl,
            bool noShift,
            bool zoomOut,
            bool windowCursorLock,
            int spriteResolution,
            int textureResolution,
            bool windowed,
            bool trilinearFiltering,
            RoCameraAntialiasingMode cameraAntialiasing) {
            UiScale = uiScale;
            UiScaleMode = uiScaleMode;
            ManualUiScale = manualUiScale;
            EffectiveUiScale = effectiveUiScale;
            MasterVolume = masterVolume;
            BgmVolume = bgmVolume;
            EffectsVolume = effectsVolume;
            Muted = muted;
            Keybinds = Copy(keybinds);
            Shortcuts = Copy(shortcuts);
            BattleModeEnabled = battleModeEnabled;
            BgmMuted = bgmMuted;
            EffectsMuted = effectsMuted;
            ShowAura = showAura;
            SimpleAura = simpleAura;
            ShowSkillEffects = showSkillEffects;
            ShowShadow = showShadow;
            ExpandDamageDisplay = expandDamageDisplay;
            DamageMotion = damageMotion;
            DamageSkin = damageSkin;
            ShowMiss = showMiss;
            ShowMapName = showMapName;
            FogEffect = fogEffect;
            ShowAllFootprints = showAllFootprints;
            MouseSnapMonster = mouseSnapMonster;
            MouseSnapSkill = mouseSnapSkill;
            MouseSnapPickupItem = mouseSnapPickupItem;
            NoCtrl = noCtrl;
            NoShift = noShift;
            ZoomOut = zoomOut;
            WindowCursorLock = windowCursorLock;
            SpriteResolution = spriteResolution;
            TextureResolution = textureResolution;
            Windowed = windowed;
            TrilinearFiltering = trilinearFiltering;
            CameraAntialiasing = cameraAntialiasing;
        }

        public float UiScale { get; }
        public RoUiScaleMode UiScaleMode { get; }
        public bool AutoUiScale => UiScaleMode == RoUiScaleMode.Auto;
        public float ManualUiScale { get; }
        public float EffectiveUiScale { get; }
        public float MasterVolume { get; }
        public float BgmVolume { get; }
        public float EffectsVolume { get; }
        public bool Muted { get; }
        public RoClientKeybindSnapshot[] Keybinds { get; }
        public RoShortcutBindingSnapshot[] Shortcuts { get; }
        public bool BattleModeEnabled { get; }
        public bool BgmMuted { get; }
        public bool EffectsMuted { get; }
        public bool ShowAura { get; }
        public bool SimpleAura { get; }
        public bool ShowSkillEffects { get; }
        public bool ShowShadow { get; }
        public bool ExpandDamageDisplay { get; }
        public int DamageMotion { get; }
        public int DamageSkin { get; }
        public bool ShowMiss { get; }
        public bool ShowMapName { get; }
        public bool FogEffect { get; }
        public bool ShowAllFootprints { get; }
        public bool MouseSnapMonster { get; }
        public bool MouseSnapSkill { get; }
        public bool MouseSnapPickupItem { get; }
        public bool NoCtrl { get; }
        public bool NoShift { get; }
        public bool ZoomOut { get; }
        public bool WindowCursorLock { get; }
        public int SpriteResolution { get; }
        public int TextureResolution { get; }
        public bool Windowed { get; }
        public bool TrilinearFiltering { get; }
        public RoCameraAntialiasingMode CameraAntialiasing { get; }

        private static RoClientKeybindSnapshot[] Copy(RoClientKeybindSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoClientKeybindSnapshot>();
            }

            var copy = new RoClientKeybindSnapshot[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RoShortcutBindingSnapshot[] Copy(RoShortcutBindingSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoShortcutBindingSnapshot>();
            }

            var copy = new RoShortcutBindingSnapshot[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    public sealed class RoClientSettingsModel {
        public const float MinUiScale = 0.75f;
        public const float MaxUiScale = 2.25f;
        public const string CategorySkillBar = "Skill Bar";
        public const string CategoryHotkeyBar2 = "Hotkey Bar 2";
        public const string CategoryInterface = "Interface";
        public const string CategoryMacros = "Macros";

        private readonly Dictionary<string, RoClientKeybindSnapshot> keybinds =
            new Dictionary<string, RoClientKeybindSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RoClientKeybindSnapshot> defaults =
            new Dictionary<string, RoClientKeybindSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RoShortcutBindingSnapshot> shortcuts =
            new Dictionary<string, RoShortcutBindingSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RoShortcutBindingSnapshot> shortcutDefaults =
            new Dictionary<string, RoShortcutBindingSnapshot>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> shortcutOrder =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private RoUiScaleMode uiScaleMode = RoUiScaleMode.Auto;
        private float uiScale = 1f;
        private float manualUiScale = 1f;
        private float masterVolume = 1f;
        private float bgmVolume = 0.8f;
        private float effectsVolume = 1f;
        private bool muted;
        private bool battleModeEnabled = true;
        private bool bgmMuted;
        private bool effectsMuted;
        private bool showAura = true;
        private bool simpleAura;
        private bool showSkillEffects = true;
        private bool showShadow = true;
        private bool expandDamageDisplay = true;
        private int damageMotion;
        private int damageSkin;
        private bool showMiss = true;
        private bool showMapName = true;
        private bool fogEffect = true;
        private bool showAllFootprints = true;
        private bool mouseSnapMonster;
        private bool mouseSnapSkill;
        private bool mouseSnapPickupItem;
        private bool noCtrl = true;
        private bool noShift = true;
        private bool zoomOut = true;
        private bool windowCursorLock;
        private int spriteResolution = 2;
        private int textureResolution = 2;
        private bool windowed = true;
        private bool trilinearFiltering = true;
        private RoCameraAntialiasingMode cameraAntialiasing = RoCameraAntialiasingMode.SmaaHigh;

        public RoClientSettingsModel() {
            AddDefaultKeybind("move", "Move / interact", "Mouse0");
            AddDefaultKeybind("inventory", "Inventory", "I");
            AddDefaultKeybind("equipment", "Equipment", "E");
            AddDefaultKeybind("skills", "Skill tree", "K");
            AddDefaultKeybind("status", "Status", "Alt+A");
            AddDefaultKeybind("quest", "Quest log", "Alt+U");
            AddDefaultKeybind("party", "Party", "Alt+Z");
            AddDefaultKeybind("friends", "Friends", "Alt+H");
            AddDefaultKeybind("settings", "Settings", "Esc");
            AddDefaultShortcuts();
            // PlayerPrefs throws inside MonoBehaviour constructors / field initialisers: then the first use loads them
            try {
                LoadPersisted();
            } catch (UnityException) {
                pendingLoad = true;
            }
        }

        private bool pendingLoad;

        /// <summary>Reads the saved options now if the constructor could not (call from Awake / Start).</summary>
        public void EnsurePersistedLoaded() {
            if (!pendingLoad) return;
            pendingLoad = false;
            LoadPersisted();
        }

        private const string Prefs = "ro.settings.";
        private bool loadingPersisted;

        /// <summary>
        /// The options as the client keeps them between runs (the original in the registry; PlayerPrefs is the registry on
        /// Windows): every setter writes them, a new model reads them back.
        /// </summary>
        private void Persist() {
            if (loadingPersisted) return;
            EnsurePersistedLoaded();
            PlayerPrefs.SetFloat(Prefs + "masterVolume", masterVolume);
            PlayerPrefs.SetFloat(Prefs + "bgmVolume", bgmVolume);
            PlayerPrefs.SetFloat(Prefs + "effectsVolume", effectsVolume);
            Bool("muted", muted);
            Bool("bgmMuted", bgmMuted);
            Bool("effectsMuted", effectsMuted);
            Bool("battleMode", battleModeEnabled);
            Bool("showAura", showAura);
            Bool("simpleAura", simpleAura);
            Bool("showSkillEffects", showSkillEffects);
            Bool("showShadow", showShadow);
            Bool("expandDamageDisplay", expandDamageDisplay);
            PlayerPrefs.SetInt(Prefs + "damageMotion", damageMotion);
            PlayerPrefs.SetInt(Prefs + "damageSkin", damageSkin);
            Bool("showMiss", showMiss);
            Bool("showMapName", showMapName);
            Bool("fogEffect", fogEffect);
            Bool("showAllFootprints", showAllFootprints);
            Bool("snapMonster", mouseSnapMonster);
            Bool("snapSkill", mouseSnapSkill);
            Bool("snapPickupItem", mouseSnapPickupItem);
            Bool("noCtrl", noCtrl);
            Bool("noShift", noShift);
            Bool("zoomOut", zoomOut);
            Bool("windowCursorLock", windowCursorLock);
            PlayerPrefs.SetInt(Prefs + "spriteResolution", spriteResolution);
            PlayerPrefs.SetInt(Prefs + "textureResolution", textureResolution);
            Bool("windowed", windowed);
            Bool("trilinear", trilinearFiltering);
            PlayerPrefs.SetInt(Prefs + "antialiasing", (int) cameraAntialiasing);
            var changed = new StringBuilder();
            foreach (RoShortcutBindingSnapshot binding in shortcuts.Values) {
                if (shortcutDefaults.TryGetValue(binding.ActionId, out RoShortcutBindingSnapshot def) && def.Binding == binding.Binding) continue;
                changed.Append(binding.ActionId).Append('=').Append((int) binding.Binding.Key).Append(',').Append((int) binding.Binding.Modifiers).Append(';');
            }

            PlayerPrefs.SetString(Prefs + "shortcuts", changed.ToString());
            PlayerPrefs.SetInt(Prefs + "saved", 1);
            PlayerPrefs.Save();

            void Bool(string key, bool value) => PlayerPrefs.SetInt(Prefs + key, value ? 1 : 0);
        }

        private void LoadPersisted() {
            if (PlayerPrefs.GetInt(Prefs + "saved", 0) == 0) return;
            loadingPersisted = true;
            try {
                masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefs + "masterVolume", masterVolume));
                bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefs + "bgmVolume", bgmVolume));
                effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefs + "effectsVolume", effectsVolume));
                muted = Bool("muted", muted);
                bgmMuted = Bool("bgmMuted", bgmMuted);
                effectsMuted = Bool("effectsMuted", effectsMuted);
                battleModeEnabled = Bool("battleMode", battleModeEnabled);
                showAura = Bool("showAura", showAura);
                simpleAura = Bool("simpleAura", simpleAura);
                showSkillEffects = Bool("showSkillEffects", showSkillEffects);
                showShadow = Bool("showShadow", showShadow);
                expandDamageDisplay = Bool("expandDamageDisplay", expandDamageDisplay);
                damageMotion = Mathf.Clamp(PlayerPrefs.GetInt(Prefs + "damageMotion", damageMotion), 0, 3);
                damageSkin = Mathf.Clamp(PlayerPrefs.GetInt(Prefs + "damageSkin", damageSkin), 0, 3);
                showMiss = Bool("showMiss", showMiss);
                showMapName = Bool("showMapName", showMapName);
                fogEffect = Bool("fogEffect", fogEffect);
                showAllFootprints = Bool("showAllFootprints", showAllFootprints);
                mouseSnapMonster = Bool("snapMonster", mouseSnapMonster);
                mouseSnapSkill = Bool("snapSkill", mouseSnapSkill);
                mouseSnapPickupItem = Bool("snapPickupItem", mouseSnapPickupItem);
                noCtrl = Bool("noCtrl", noCtrl);
                noShift = Bool("noShift", noShift);
                Ragnarok.World.RoPickHostility.NoShift = noShift;
                zoomOut = Bool("zoomOut", zoomOut);
                windowCursorLock = Bool("windowCursorLock", windowCursorLock);
                spriteResolution = PlayerPrefs.GetInt(Prefs + "spriteResolution", spriteResolution);
                textureResolution = PlayerPrefs.GetInt(Prefs + "textureResolution", textureResolution);
                windowed = Bool("windowed", windowed);
                trilinearFiltering = Bool("trilinear", trilinearFiltering);
                var aa = (RoCameraAntialiasingMode) PlayerPrefs.GetInt(Prefs + "antialiasing", (int) cameraAntialiasing);
                if (Enum.IsDefined(typeof(RoCameraAntialiasingMode), aa)) cameraAntialiasing = aa;
                foreach (string entry in PlayerPrefs.GetString(Prefs + "shortcuts", string.Empty).Split(';')) {
                    int eq = entry.IndexOf('='), comma = entry.LastIndexOf(',');
                    if (eq <= 0 || comma <= eq) continue;
                    string id = entry.Substring(0, eq);
                    if (!shortcuts.TryGetValue(id, out RoShortcutBindingSnapshot existing) ||
                        !int.TryParse(entry.Substring(eq + 1, comma - eq - 1), out int key) || !int.TryParse(entry.Substring(comma + 1), out int mods)) continue;
                    shortcuts[id] = new RoShortcutBindingSnapshot(existing.ActionId, existing.DisplayName, existing.Category, existing.Page,
                        new RoKeyChord((KeyCode) key, (EventModifiers) mods), existing.RuntimeSupported);
                }

                PushAudioSettings();
                SyncDamageGlobals();
                RoClassicMapBanner.SetShowMapNamesEnabled(showMapName);
            } finally {
                loadingPersisted = false;
            }

            bool Bool(string key, bool fallback) => PlayerPrefs.GetInt(Prefs + key, fallback ? 1 : 0) != 0;
        }

        public RoClientSettingsSnapshot Snapshot() {
            EnsurePersistedLoaded();
            SyncDamageGlobals();
            var values = new RoClientKeybindSnapshot[keybinds.Count];
            keybinds.Values.CopyTo(values, 0);
            Array.Sort(values, (left, right) => string.Compare(left.ActionId, right.ActionId, StringComparison.OrdinalIgnoreCase));
            RoShortcutBindingSnapshot[] shortcutValues = ShortcutBindings();
            return new RoClientSettingsSnapshot(
                uiScale,
                uiScaleMode,
                manualUiScale,
                uiScale,
                masterVolume,
                bgmVolume,
                effectsVolume,
                muted,
                values,
                shortcutValues,
                battleModeEnabled,
                bgmMuted,
                effectsMuted,
                showAura,
                simpleAura,
                showSkillEffects,
                showShadow,
                expandDamageDisplay,
                damageMotion,
                damageSkin,
                showMiss,
                showMapName,
                fogEffect,
                showAllFootprints,
                mouseSnapMonster,
                mouseSnapSkill,
                mouseSnapPickupItem,
                noCtrl,
                noShift,
                zoomOut,
                windowCursorLock,
                spriteResolution,
                textureResolution,
                windowed,
                trilinearFiltering,
                cameraAntialiasing);
        }

        public RoUiScaleMode UiScaleMode => uiScaleMode;
        public float ManualUiScale => manualUiScale;
        public float EffectiveUiScale => uiScale;

        /// <summary>The damage display options into the original's CMsgEffect globals (the damage skin through the
        /// bridge's damageskininfo table), and /aura, /aura2 into the bridge's level aura dispatch.</summary>
        private void SyncDamageGlobals() {
            Ragnarok.Effects.Classic.RoClassicEffectBridge.ShowLevelAura = showAura;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.SimpleLevelAura = simpleAura;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.DamageDriftMode = damageMotion;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.ShowMissText = showMiss;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.DamageNumberAbbreviate = expandDamageDisplay;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.DamageSkin = damageSkin;
        }

        public bool TryGetKeybind(string actionId, out RoClientKeybindSnapshot keybind) {
            return keybinds.TryGetValue(actionId ?? string.Empty, out keybind);
        }

        public bool TryGetShortcutBinding(string actionId, out RoShortcutBindingSnapshot binding) {
            return shortcuts.TryGetValue(actionId ?? string.Empty, out binding);
        }

        public bool TryGetDefaultShortcutBinding(string actionId, out RoShortcutBindingSnapshot binding) {
            return shortcutDefaults.TryGetValue(actionId ?? string.Empty, out binding);
        }

        /// <summary>A category's action ids in the order of the client's hotkey table.</summary>
        public IReadOnlyList<string> ShortcutOrder(string category) {
            return shortcutOrder.TryGetValue(category ?? string.Empty, out List<string> order) ? order : (IReadOnlyList<string>) Array.Empty<string>();
        }

        public RoShortcutBindingSnapshot[] ShortcutBindings() {
            var values = new RoShortcutBindingSnapshot[shortcuts.Count];
            shortcuts.Values.CopyTo(values, 0);
            Array.Sort(values, CompareShortcutBindings);
            return values;
        }

        public RoClientSettingsResult RebindShortcut(string actionId, RoKeyChord chord) {
            if (string.IsNullOrWhiteSpace(actionId) || !shortcuts.TryGetValue(actionId, out RoShortcutBindingSnapshot existing)) {
                return new RoClientSettingsResult(RoClientSettingsStatus.UnknownAction, "Unknown shortcut action.");
            }

            if (chord.IsAssigned) {
                foreach (RoShortcutBindingSnapshot candidate in shortcuts.Values) {
                    if (string.Equals(candidate.ActionId, actionId, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    if (string.Equals(candidate.Category, existing.Category, StringComparison.OrdinalIgnoreCase) &&
                        candidate.Binding == chord) {
                        return new RoClientSettingsResult(
                            RoClientSettingsStatus.Conflict,
                            $"Key '{chord.ToDisplayString()}' is already assigned to '{candidate.DisplayName}'.");
                    }
                }
            }

            shortcuts[actionId] = new RoShortcutBindingSnapshot(
                existing.ActionId,
                existing.DisplayName,
                existing.Category,
                existing.Page,
                chord,
                existing.RuntimeSupported);
            Persist();
            return RoClientSettingsResult.Success();
        }

        public RoClientSettingsResult ClearShortcut(string actionId) {
            return RebindShortcut(actionId, RoKeyChord.None);
        }

        public RoClientSettingsResult ResetShortcut(string actionId) {
            if (string.IsNullOrWhiteSpace(actionId) || !shortcutDefaults.TryGetValue(actionId, out RoShortcutBindingSnapshot value)) {
                return new RoClientSettingsResult(RoClientSettingsStatus.UnknownAction, "Unknown shortcut action.");
            }

            shortcuts[actionId] = value;
            Persist();
            return RoClientSettingsResult.Success();
        }

        public void ResetShortcutCategory(string category) {
            string resolved = category ?? string.Empty;
            foreach (RoShortcutBindingSnapshot value in shortcutDefaults.Values) {
                if (string.Equals(value.Category, resolved, StringComparison.OrdinalIgnoreCase)) {
                    shortcuts[value.ActionId] = value;
                }
            }

            Persist();
        }

        public RoClientSettingsResult SetUiScale(float value) {
            if (!IsValidFiniteScale(value) || value < MinUiScale || value > MaxUiScale) {
                return new RoClientSettingsResult(RoClientSettingsStatus.InvalidValue, $"UI scale must be between {MinUiScale:0.##} and {MaxUiScale:0.##}.");
            }

            return SetManualUiScale(value);
        }

        public RoClientSettingsResult SetManualUiScale(float value) {
            if (!IsValidFiniteScale(value)) {
                return new RoClientSettingsResult(RoClientSettingsStatus.InvalidValue, $"UI scale must be between {MinUiScale:0.##} and {MaxUiScale:0.##}.");
            }

            manualUiScale = ClampUiScale(value);
            uiScale = manualUiScale;
            uiScaleMode = RoUiScaleMode.Manual;
            return RoClientSettingsResult.Success();
        }

        public RoClientSettingsResult SetAutoUiScale(float value) {
            if (!IsValidFiniteScale(value)) {
                return new RoClientSettingsResult(RoClientSettingsStatus.InvalidValue, $"UI scale must be between {MinUiScale:0.##} and {MaxUiScale:0.##}.");
            }

            uiScale = ClampUiScale(value);
            uiScaleMode = RoUiScaleMode.Auto;
            return RoClientSettingsResult.Success();
        }

        public void ResetUiScaleToAuto() {
            uiScaleMode = RoUiScaleMode.Auto;
            manualUiScale = 1f;
            uiScale = 1f;
        }

        public RoClientSettingsResult SetMasterVolume(float value) {
            return PushAudioSettings(SetVolume(ref masterVolume, value, "Master volume"));
        }

        /// <summary>The volumes and mutes are what the client's sound plays with (<see cref="Ragnarok.Audio.RoAudioSystem"/>).</summary>
        private RoClientSettingsResult PushAudioSettings(RoClientSettingsResult result = default) {
            Ragnarok.Audio.RoAudioSettings audio = Ragnarok.Audio.RoAudioSystem.Settings;
            audio.masterVolume = masterVolume;
            audio.bgmVolume = bgmVolume;
            audio.effectsVolume = effectsVolume;
            audio.ambientVolume = effectsVolume;
            audio.uiVolume = effectsVolume;
            audio.mute = muted;
            audio.bgmMuted = bgmMuted;
            audio.effectsMuted = effectsMuted;
            Ragnarok.Audio.RoAudioSystem.ApplySettings();
            Persist();
            return result;
        }

        public RoClientSettingsResult SetBgmVolume(float value) {
            return PushAudioSettings(SetVolume(ref bgmVolume, value, "BGM volume"));
        }

        public RoClientSettingsResult SetEffectsVolume(float value) {
            return PushAudioSettings(SetVolume(ref effectsVolume, value, "Effects volume"));
        }

        public void SetMuted(bool value) {
            muted = value;
            PushAudioSettings();
            Persist();
        }

        public void SetBattleModeEnabled(bool value) {
            battleModeEnabled = value;
            Persist();
        }

        /// <summary>The battle (chat) mode without a snapshot: the classic chat reads it every frame.</summary>
        public bool IsBattleModeEnabled => battleModeEnabled;
        public void SetBgmMuted(bool value) {
            bgmMuted = value;
            PushAudioSettings();
            Persist();
        }

        public void SetEffectsMuted(bool value) {
            effectsMuted = value;
            PushAudioSettings();
            Persist();
        }
        public void SetShowAura(bool value) {
            showAura = value;
            SyncDamageGlobals();
            Persist();
        }
        public void SetSimpleAura(bool value) {
            simpleAura = value;
            SyncDamageGlobals();
            Persist();
        }
        public void SetShowSkillEffects(bool value) {
            showSkillEffects = value;
            Persist();
        }
        public void SetShowShadow(bool value) {
            showShadow = value;
            Persist();
        }
        public void SetExpandDamageDisplay(bool value) {
            expandDamageDisplay = value;
            SyncDamageGlobals();
            Persist();
        }
        public void SetDamageMotion(int value) {
            damageMotion = Mathf.Clamp(value, 0, 3);
            SyncDamageGlobals();
            Persist();
        }
        public void SetDamageSkin(int value) {
            damageSkin = Mathf.Clamp(value, 0, 3);
            SyncDamageGlobals();
            Persist();
        }
        public void SetShowMiss(bool value) {
            showMiss = value;
            SyncDamageGlobals();
            Persist();
        }
        public void SetShowMapName(bool value) {
            showMapName = value;
            RoClassicMapBanner.SetShowMapNamesEnabled(value);
            Persist();
        }
        public void SetFogEffect(bool value) {
            fogEffect = value;
            Persist();
        }
        public void SetShowAllFootprints(bool value) {
            showAllFootprints = value;
            Persist();
        }
        public void SetMouseSnapMonster(bool value) {
            mouseSnapMonster = value;
            Persist();
        }
        public void SetMouseSnapSkill(bool value) {
            mouseSnapSkill = value;
            Persist();
        }
        public void SetMouseSnapPickupItem(bool value) {
            mouseSnapPickupItem = value;
            Persist();
        }
        public void SetNoCtrl(bool value) {
            noCtrl = value;
            Persist();
        }
        public void SetNoShift(bool value) {
            noShift = value;
            Ragnarok.World.RoPickHostility.NoShift = value;
            Persist();
        }
        public void SetZoomOut(bool value) {
            zoomOut = value;
            Persist();
        }
        public void SetWindowCursorLock(bool value) {
            windowCursorLock = value;
            Persist();
        }
        public void SetWindowed(bool value) {
            windowed = value;
            Persist();
        }
        public void SetTrilinearFiltering(bool value) {
            trilinearFiltering = value;
            Persist();
        }
        public void SetCameraAntialiasing(RoCameraAntialiasingMode value) {
            cameraAntialiasing = Enum.IsDefined(typeof(RoCameraAntialiasingMode), value)
                ? value
                : RoCameraAntialiasingMode.SmaaHigh;
            Persist();
        }

        public RoClientSettingsResult SetSpriteResolution(int value) {
            RoClientSettingsResult result = SetResolutionValue(ref spriteResolution, value, "Sprite resolution");
            Persist();
            return result;
        }

        public RoClientSettingsResult SetTextureResolution(int value) {
            RoClientSettingsResult result = SetResolutionValue(ref textureResolution, value, "Texture resolution");
            Persist();
            return result;
        }

        public void ResetBasicSettings() {
            bgmVolume = 0.8f;
            effectsVolume = 1f;
            bgmMuted = false;
            effectsMuted = false;
            PushAudioSettings();
            Persist();
        }

        public void ResetEffectSettings() {
            showAura = true;
            simpleAura = true;
            showSkillEffects = true;
            showShadow = true;
            expandDamageDisplay = true;
            showMapName = true;
            fogEffect = true;
            showAllFootprints = true;
            Persist();
        }

        public void ResetControlSettings() {
            mouseSnapMonster = false;
            mouseSnapSkill = false;
            mouseSnapPickupItem = false;
            noCtrl = true;
            noShift = true;
            Ragnarok.World.RoPickHostility.NoShift = true;
            zoomOut = true;
            windowCursorLock = false;
            Persist();
        }

        public void ResetGraphicsSettings() {
            spriteResolution = 2;
            textureResolution = 2;
            windowed = true;
            trilinearFiltering = true;
            cameraAntialiasing = RoCameraAntialiasingMode.SmaaHigh;
            Persist();
        }

        public RoClientSettingsResult Rebind(string actionId, string key) {
            if (string.IsNullOrWhiteSpace(actionId) || !keybinds.TryGetValue(actionId, out RoClientKeybindSnapshot existing)) {
                return new RoClientSettingsResult(RoClientSettingsStatus.UnknownAction, "Unknown keybind action.");
            }

            if (string.IsNullOrWhiteSpace(key)) {
                return new RoClientSettingsResult(RoClientSettingsStatus.InvalidValue, "Keybind key is required.");
            }

            string normalizedKey = key.Trim();
            foreach (RoClientKeybindSnapshot candidate in keybinds.Values) {
                if (string.Equals(candidate.ActionId, actionId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (string.Equals(candidate.Key, normalizedKey, StringComparison.OrdinalIgnoreCase)) {
                    return new RoClientSettingsResult(
                        RoClientSettingsStatus.Conflict,
                        $"Key '{normalizedKey}' is already assigned to '{candidate.DisplayName}'.");
                }
            }

            keybinds[actionId] = new RoClientKeybindSnapshot(existing.ActionId, existing.DisplayName, normalizedKey);
            return RoClientSettingsResult.Success();
        }

        public RoClientSettingsResult ResetKeybind(string actionId) {
            if (string.IsNullOrWhiteSpace(actionId) || !defaults.TryGetValue(actionId, out RoClientKeybindSnapshot value)) {
                return new RoClientSettingsResult(RoClientSettingsStatus.UnknownAction, "Unknown keybind action.");
            }

            keybinds[actionId] = value;
            return RoClientSettingsResult.Success();
        }

        public void ResetDefaults() {
            ResetUiScaleToAuto();
            masterVolume = 1f;
            bgmVolume = 0.8f;
            effectsVolume = 1f;
            muted = false;
            PushAudioSettings();
            keybinds.Clear();
            foreach (KeyValuePair<string, RoClientKeybindSnapshot> pair in defaults) {
                keybinds[pair.Key] = pair.Value;
            }

            shortcuts.Clear();
            foreach (KeyValuePair<string, RoShortcutBindingSnapshot> pair in shortcutDefaults) {
                shortcuts[pair.Key] = pair.Value;
            }

            battleModeEnabled = true;
            ResetBasicSettings();
            ResetEffectSettings();
            ResetControlSettings();
            ResetGraphicsSettings();
            Persist();
        }

        /// <summary>The hotkey bar asks for every slot's id each frame: the first 36 are built once.</summary>
        private static readonly string[] HotkeyActionIds = BuildHotkeyActionIds(36);

        private static string[] BuildHotkeyActionIds(int count) {
            var ids = new string[count];
            for (int i = 0; i < count; i++) {
                ids[i] = "hotkey." + (i / 9 + 1).ToString() + "." + (i % 9 + 1).ToString();
            }

            return ids;
        }

        public static string HotkeyActionId(int zeroBasedIndex) {
            if (zeroBasedIndex >= 0 && zeroBasedIndex < HotkeyActionIds.Length) {
                return HotkeyActionIds[zeroBasedIndex];
            }

            int row = Math.Max(0, zeroBasedIndex) / 9 + 1;
            int column = Math.Max(0, zeroBasedIndex) % 9 + 1;
            return "hotkey." + row.ToString() + "." + column.ToString();
        }

        public static string HotkeyBar2ActionId(int zeroBasedIndex) {
            int row = Math.Max(0, zeroBasedIndex) / 9 + 1;
            int column = Math.Max(0, zeroBasedIndex) % 9 + 1;
            return "hotkey2." + row.ToString() + "." + column.ToString();
        }

        // the hotkey executors ask for every icon's action id every frame
        private static readonly Dictionary<string, string> InterfaceActionIds = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string InterfaceActionId(string basicInfoIconId) {
            string key = basicInfoIconId ?? string.Empty;
            if (!InterfaceActionIds.TryGetValue(key, out string actionId)) {
                actionId = "interface." + key.Trim().ToLowerInvariant();
                InterfaceActionIds[key] = actionId;
            }

            return actionId;
        }

        private void AddDefaultKeybind(string actionId, string displayName, string key) {
            var snapshot = new RoClientKeybindSnapshot(actionId, displayName, key);
            defaults[actionId] = snapshot;
            keybinds[actionId] = snapshot;
        }

        private void AddDefaultShortcuts() {
            for (int i = 0; i < 36; i++) {
                int row = i / 9 + 1;
                int column = i % 9 + 1;
                RoKeyChord skillBarKey = row == 1
                    ? new RoKeyChord((KeyCode) ((int) KeyCode.F1 + column - 1))
                    : RoKeyChord.None;
                AddDefaultShortcut(HotkeyActionId(i), "Hotkey " + row.ToString() + "-" + column.ToString(), CategorySkillBar, 1, skillBarKey, runtimeSupported: true);

                RoKeyChord hotkeyBar2Key = row == 1
                    ? new RoKeyChord((KeyCode) ((int) KeyCode.F1 + column - 1))
                    : RoKeyChord.None;
                AddDefaultShortcut(HotkeyBar2ActionId(i), "Hotkey 2 " + row.ToString() + "-" + column.ToString(), CategoryHotkeyBar2, 1, hotkeyBar2Key, runtimeSupported: false);
            }

            // Interface: hotkey.lub HOTKEY_2 in its order, 36 rows a page (index 61 has no text in msgstring_kr.lub)
            AddDefaultShortcut("interface.basicinfo", "Basic Information", CategoryInterface, 1, Chord("Alt + V"), false);
            AddDefaultShortcut(InterfaceActionId("equip"), "Equipment/Status", CategoryInterface, 1, Chord("Alt + Q"), true);
            AddDefaultShortcut(InterfaceActionId("skill"), "Skills", CategoryInterface, 1, Chord("Alt + S"), true);
            AddDefaultShortcut(InterfaceActionId("quest"), "Quest Log", CategoryInterface, 1, Chord("Alt + U"), true);
            AddDefaultShortcut("interface.friends", "Friends List", CategoryInterface, 1, Chord("Alt + H"), true);
            AddDefaultShortcut(InterfaceActionId("party"), "Party List", CategoryInterface, 1, Chord("Alt + Z"), true);
            AddDefaultShortcut("interface.chatroom", "Chatroom", CategoryInterface, 1, Chord("Alt + C"), false);
            AddDefaultShortcut("interface.cart", "View Cart", CategoryInterface, 1, Chord("Alt + W"), false);
            AddDefaultShortcut(InterfaceActionId("map"), "World Map", CategoryInterface, 1, Chord("Ctrl + `"), true);
            AddDefaultShortcut(InterfaceActionId("item"), "Inventory", CategoryInterface, 1, Chord("Alt + E"), true);
            AddDefaultShortcut(InterfaceActionId("guild"), "Guild Options", CategoryInterface, 1, Chord("Alt + G"), true);
            AddDefaultShortcut("interface.emotions", "Emoticon Icon List", CategoryInterface, 1, Chord("Alt + L"), false);
            AddDefaultShortcut("interface.sitstand", "Sit/Stand", CategoryInterface, 1, Chord("Insert"), false);
            AddDefaultShortcut("interface.minimap", "Mini-Map", CategoryInterface, 1, Chord("Ctrl + Tab"), false);
            AddDefaultShortcut("interface.shortcuts", "Shortcuts List", CategoryInterface, 1, Chord("Alt + M"), false);
            AddDefaultShortcut("interface.commands", "Commands List", CategoryInterface, 1, Chord("Alt + Y"), false);
            AddDefaultShortcut("interface.closeTabs", "Close Tabs", CategoryInterface, 1, Chord("F11"), false);
            AddDefaultShortcut("interface.hotkeyBar", "Hotkey Bar", CategoryInterface, 1, Chord("F12"), false);
            AddDefaultShortcut("interface.chatTab", "Chatting Tab", CategoryInterface, 1, Chord("Alt + F10"), false);
            AddDefaultShortcut("interface.resizeChat", "Re-size Chatting Tab", CategoryInterface, 1, Chord("F10"), false);
            AddDefaultShortcut("interface.hpspGauge", "HP/SP Gauge On/Off", CategoryInterface, 1, Chord("Alt + End"), false);
            AddDefaultShortcut("interface.screenshot", "Screenshot", CategoryInterface, 1, Chord("Snapshot"), false);
            AddDefaultShortcut("interface.memorialDungeon", "Memorial Dungeon", CategoryInterface, 1, Chord("Alt + B"), false);
            AddDefaultShortcut("interface.pet", "Pet Information", CategoryInterface, 1, Chord("Alt + J"), false);
            AddDefaultShortcut("interface.homunculus", "Homunculus Info", CategoryInterface, 1, Chord("Alt + R"), false);
            AddDefaultShortcut("interface.mercenary", "Mercenary Info", CategoryInterface, 1, Chord("Ctrl + R"), false);
            AddDefaultShortcut("interface.homunCommand", "Homun. Command", CategoryInterface, 1, Chord("Alt + T"), false);
            AddDefaultShortcut("interface.mercCommand", "Merc. Command", CategoryInterface, 1, Chord("Ctrl + T"), false);
            AddDefaultShortcut("interface.friendSetup", "Friend Setup", CategoryInterface, 1, RoKeyChord.None, true);
            AddDefaultShortcut("interface.partySetup", "Party Setup", CategoryInterface, 1, RoKeyChord.None, true);
            AddDefaultShortcut("interface.battlefieldPosition", "Battlefield Position", CategoryInterface, 1, RoKeyChord.None, false);
            AddDefaultShortcut("interface.chatMode", "Chatmode On/Off", CategoryInterface, 1, Chord("Return"), false);
            AddDefaultShortcut("interface.infoDisplay", "Info Display Button", CategoryInterface, 1, Chord("Alt + `"), false);
            AddDefaultShortcut("interface.partyRecruit", "Party Recruit", CategoryInterface, 1, RoKeyChord.None, false);
            AddDefaultShortcut("interface.partyBooking", "Party Booking On/Off", CategoryInterface, 1, RoKeyChord.None, false);
            AddDefaultShortcut("interface.groundCursor", "Ground Cursor On/Off", CategoryInterface, 1, Chord("Alt + Home"), false);
            AddDefaultShortcut("interface.macroSetting", "Macro Setting", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.cashshop", "Simple Cashshop On/Off", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.recording", "Recording On/Off", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.startStopRecording", "Start/Stop recording", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.replayIncrease", "Increase replay speed", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.replayReduce", "Reduce replay speed", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.navigation", "Navigation On/Off", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.relocateButton", "Relocate Button", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.clan", "Clan On/Off", CategoryInterface, 2, Chord("Ctrl + G"), false);
            AddDefaultShortcut("interface.menuIcons", "Menu Icons Hide/Show", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut(InterfaceActionId("bank"), "Bank", CategoryInterface, 2, Chord("Ctrl + B"), true);
            AddDefaultShortcut(InterfaceActionId("info"), "Status On/Off", CategoryInterface, 2, Chord("Alt + A"), true);
            AddDefaultShortcut("interface.rotateRows", "Rotate Hotkey Rows", CategoryInterface, 2, Chord("Ctrl + F12"), false);
            AddDefaultShortcut("interface.achievements", "Achievements On/Off", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.extendedMinimap", "Extended minimap", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.mail", "Mail On/Off", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.replaceEquipSet", "Replace Equipment Set", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.twitter", "Twitter On/Off", CategoryInterface, 2, Chord("Ctrl + S"), false);
            AddDefaultShortcut("interface.tipbox", "Tipbox On/Off", CategoryInterface, 2, Chord("Alt + D"), false);
            AddDefaultShortcut("interface.switchHotkeyBar", "Switch Hotkey Bar", CategoryInterface, 2, RoKeyChord.None, false);
            AddDefaultShortcut("interface.costume", "Costume Equipment On/Off", CategoryInterface, 2, Chord("Alt + X"), false);
            AddDefaultShortcut("interface.adventurerAgency", "Adventurer Agency On/Off", CategoryInterface, 2, Chord("Ctrl + Z"), false);
            // HOTKEY_2 row 61 (MSI_HK_EQUIPMENT_PROPERTIES): no msgstring, so the client's shortcut window hides the row
            AddDefaultShortcut("interface.equipmentProperties", "Equipment Properties On/Off", CategoryInterface, 2, Chord("Ctrl + Q"), true);
            AddDefaultShortcut("interface.huntingQuest", "Hunting Quest Info On/Off", CategoryInterface, 2, Chord("Ctrl + H"), false);
            AddDefaultShortcut("interface.hidePlayers", "Hide Players on Screen", CategoryInterface, 2, Chord("Ctrl + End"), false);
            AddDefaultShortcut("interface.reputation", "Reputation On/Off", CategoryInterface, 2, RoKeyChord.None, false);

            // Macros: HOTKEY_3, "Macro 1" … "Macro 10" on Alt + 1 … 0, "Flag 1" … "Flag 10" on Ctrl + 1 … 0
            for (int i = 0; i < 10; i++) {
                string digit = ((i + 1) % 10).ToString();
                AddDefaultShortcut("macro." + (i + 1).ToString(), "Macro " + (i + 1).ToString(), CategoryMacros, 1, Chord("Alt + " + digit), false);
            }

            for (int i = 0; i < 10; i++) {
                string digit = ((i + 1) % 10).ToString();
                AddDefaultShortcut("flag." + (i + 1).ToString(), "Flag " + (i + 1).ToString(), CategoryMacros, 1, Chord("Ctrl + " + digit), false);
            }
        }

        private void AddDefaultShortcut(
            string actionId,
            string displayName,
            string category,
            int page,
            RoKeyChord binding,
            bool runtimeSupported) {
            var snapshot = new RoShortcutBindingSnapshot(actionId, displayName, category, page, binding, runtimeSupported);
            shortcutDefaults[actionId] = snapshot;
            shortcuts[actionId] = snapshot;
            if (!shortcutOrder.TryGetValue(category, out List<string> order)) {
                order = new List<string>();
                shortcutOrder[category] = order;
            }

            if (!order.Contains(actionId)) {
                order.Add(actionId);
            }
        }

        private static RoKeyChord Chord(string text) {
            return RoKeyChord.TryParse(text, out RoKeyChord chord) ? chord : RoKeyChord.None;
        }

        private static RoClientSettingsResult SetVolume(ref float target, float value, string label) {
            if (float.IsNaN(value) || value < 0f || value > 1f) {
                return new RoClientSettingsResult(RoClientSettingsStatus.InvalidValue, label + " must be between 0 and 1.");
            }

            target = value;
            return RoClientSettingsResult.Success();
        }

        private static bool IsValidFiniteScale(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float ClampUiScale(float value) {
            return Mathf.Clamp(value, MinUiScale, MaxUiScale);
        }

        private static RoClientSettingsResult SetResolutionValue(ref int target, int value, string label) {
            if (value < 0 || value > 3) {
                return new RoClientSettingsResult(RoClientSettingsStatus.InvalidValue, label + " must be between 0 and 3.");
            }

            target = value;
            return RoClientSettingsResult.Success();
        }

        private static int CompareShortcutBindings(RoShortcutBindingSnapshot left, RoShortcutBindingSnapshot right) {
            int category = CategorySort(left.Category).CompareTo(CategorySort(right.Category));
            if (category != 0) {
                return category;
            }

            int page = left.Page.CompareTo(right.Page);
            if (page != 0) {
                return page;
            }

            return string.Compare(left.ActionId, right.ActionId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CategorySort(string category) {
            if (string.Equals(category, CategorySkillBar, StringComparison.OrdinalIgnoreCase)) {
                return 0;
            }

            if (string.Equals(category, CategoryHotkeyBar2, StringComparison.OrdinalIgnoreCase)) {
                return 1;
            }

            if (string.Equals(category, CategoryInterface, StringComparison.OrdinalIgnoreCase)) {
                return 2;
            }

            if (string.Equals(category, CategoryMacros, StringComparison.OrdinalIgnoreCase)) {
                return 3;
            }

            return 9;
        }
    }
}
