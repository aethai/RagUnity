using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>One skill as the skill window shows it: the session's skill info with its metadata, the use level and the plan.</summary>
    public readonly struct RoSkillDisplayEntry {
        public RoSkillDisplayEntry(
            int skillId,
            int tab,
            int slot,
            string displayName,
            RoSkillMetadata metadata,
            RoGameplaySkillInfo skill,
            bool learned,
            int level,
            int selectedLevel,
            int spCost,
            bool passive,
            bool draggable,
            bool canSelectLevel,
            RoSkillTargetFlags targetFlags,
            int plannedDelta) {
            SkillId = skillId;
            Tab = tab;
            Slot = slot;
            DisplayName = displayName ?? string.Empty;
            Metadata = metadata;
            Skill = skill;
            Learned = learned;
            Level = Math.Max(0, level);
            SelectedLevel = Math.Max(1, selectedLevel);
            SpCost = Math.Max(0, spCost);
            Passive = passive;
            Draggable = draggable;
            CanSelectLevel = canSelectLevel && Learned && Level > 0;
            TargetFlags = targetFlags;
            PlannedDelta = Math.Max(0, plannedDelta);
        }

        public int SkillId { get; }
        public int Tab { get; }
        public int Slot { get; }
        public string DisplayName { get; }
        public RoSkillMetadata Metadata { get; }
        public RoGameplaySkillInfo Skill { get; }
        public bool Learned { get; }
        public int Level { get; }
        public int SelectedLevel { get; }
        public int SpCost { get; }
        public bool Passive { get; }
        public bool Draggable { get; }
        public bool CanSelectLevel { get; }
        public RoSkillTargetFlags TargetFlags { get; }
        public int PlannedDelta { get; }
        public int EffectiveLevel => Level + PlannedDelta;
        public bool CanUse => Learned && Level > 0 && !Passive;
    }

    /// <summary>
    /// The skill window's data, without any drawing: the display entries built from the session's skills and the skill
    /// metadata, the job's tree layout, the skill-up plan (the levels planned in the window, sent as CZ_UPGRADE_SKILLLEVEL
    /// one per level on Apply), the chosen use level of level-use skills, and the skills the server starts by itself
    /// (ZC_AUTORUN_SKILL), which go through the targeting like a skill used from the list.
    /// </summary>
    public sealed class RoSkillTreeModel {
        private const float LookupRetrySeconds = 0.5f;
        private const int MerchantIdentifySkillId = 40;
        private const int MerchantChangeCartSkillId = 154;
        private const int MerchantCartDecorateSkillId = 2544;
        private const int ThiefFindStoneSkillId = 151;
        private static readonly Dictionary<string, string> ClassicSkillDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["NV_BASIC"] = "Basic Skill",
            ["NV_FIRSTAID"] = "First Aid",
            ["NV_TRICKDEAD"] = "Play Dead",
            ["MG_SRECOVERY"] = "Increase SP Recovery",
            ["MG_SIGHT"] = "Sight",
            ["MG_NAPALMBEAT"] = "Napalm Beat",
            ["MG_SAFETYWALL"] = "Safety Wall",
            ["MG_SOULSTRIKE"] = "Soul Strike",
            ["MG_COLDBOLT"] = "Cold Bolt",
            ["MG_FROSTDIVER"] = "Frost Diver",
            ["MG_STONECURSE"] = "Stone Curse",
            ["MG_FIREBALL"] = "Fire Ball",
            ["MG_FIREWALL"] = "Fire Wall",
            ["MG_FIREBOLT"] = "Fire Bolt",
            ["MG_LIGHTNINGBOLT"] = "Lightning Bolt",
            ["MG_THUNDERSTORM"] = "Thunder Storm",
            ["MC_INCCARRY"] = "Enlarge Weight Limit",
            ["MC_DISCOUNT"] = "Discount",
            ["MC_OVERCHARGE"] = "Overcharge",
            ["MC_PUSHCART"] = "Pushcart",
            ["MC_IDENTIFY"] = "Item Appraisal",
            ["MC_MAMMONITE"] = "Mammonite",
            ["MC_VENDING"] = "Vending",
            ["MC_LOUD"] = "Loud Exclamation",
            ["MC_CHANGECART"] = "Change Cart",
            ["MC_CARTREVOLUTION"] = "Cart Revolution",
            ["MC_CARTDECORATE"] = "Cart Decoration",
        };

        private readonly Dictionary<int, RoSkillDisplayEntry> entries = new Dictionary<int, RoSkillDisplayEntry>();
        private readonly Dictionary<int, int> selectedUseLevels = new Dictionary<int, int>();
        private readonly Dictionary<int, int> plannedDeltas = new Dictionary<int, int>();
        private readonly List<int> plannedOrder = new List<int>(16);
        private readonly List<int> plannedOrderApplyScratch = new List<int>(16);
        private readonly Dictionary<int, int> planIncrementScratch = new Dictionary<int, int>();
        private readonly HashSet<int> planVisitScratch = new HashSet<int>();
        private RoOnlineWorldController worldController;
        private RoRuntimeMapController runtimeMapController;
        private float nextWorldControllerLookupTime;
        private float nextRuntimeMapLookupTime;
        private RoSkillMetadataTable skillMetadata = RoSkillMetadataTable.Empty;
        private RoAssetDatabase activeAssets;
        private int cachedLayoutJobId = int.MinValue;
        private RoSkillTreeCell[] cachedLayout = Array.Empty<RoSkillTreeCell>();
        private bool applyingPlannedUpgrades;

        /// <summary>The plan or a use level changed: the window rebuilds its lists from a fresh snapshot.</summary>
        public Action Changed;

        public RoLoginShellSession Session { get; set; }
        public RoSkillMetadataTable Metadata => skillMetadata;
        public RoAssetDatabase Assets => activeAssets;

        /// <summary>Moves whenever the asset database (the map) the entries are built from changed.</summary>
        public int AssetsVersion { get; private set; }

        /// <summary>The character's skill points at the last snapshot.</summary>
        public int SkillPoints { get; private set; }

        /// <summary>The planned levels in the order they were planned (one skill id per level).</summary>
        public IReadOnlyList<int> PlannedOrder => plannedOrder;
        public bool PlanPending => plannedOrder.Count > 0;
        public int Remaining => Math.Max(0, SkillPoints - plannedOrder.Count);

        /// <summary>
        /// The session's skills and status for a rebuild; the asset database (and with it the skill metadata) is looked up
        /// again, and the entries of the previous build are forgotten.
        /// </summary>
        public RoGameplaySkillSnapshot TakeSnapshot() {
            RefreshAssets();
            RoGameplaySkillSnapshot gameplay = Session != null ? Session.SkillSnapshot() : RoGameplaySkillSnapshot.Empty;
            RoPlayerStatusSnapshot status = gameplay.Status ?? RoPlayerStatusSnapshot.Empty;
            SkillPoints = status.SkillPoints;
            entries.Clear();
            return gameplay;
        }

        /// <summary>Takes the initialized asset database (and its skill metadata) when it moved; <see cref="AssetsVersion"/> counts the moves.</summary>
        public void RefreshAssets() {
            RoAssetDatabase assets = ResolveAssetDatabase();
            if (ReferenceEquals(activeAssets, assets)) return;
            activeAssets = assets;
            skillMetadata = activeAssets != null ? activeAssets.GameData?.Skills ?? RoSkillMetadataTable.Empty : RoSkillMetadataTable.Empty;
            AssetsVersion++;
        }

        public RoSkillTreeCell[] GetLayoutForJob(int jobId) {
            if (cachedLayoutJobId == jobId && cachedLayout != null) return cachedLayout;
            cachedLayoutJobId = jobId;
            cachedLayout = RoSkillTreeLayout.BuildLayoutForJob(jobId) ?? Array.Empty<RoSkillTreeCell>();
            return cachedLayout;
        }

        /// <summary>The entry of a skill (learned or a tree cell), remembered for the plan and the use levels until the next build.</summary>
        public RoSkillDisplayEntry BuildDisplayEntry(int skillId, int tab, int slot, RoGameplaySkillInfo skill, bool hasLearned) {
            RoSkillDisplayEntry entry = CreateEntry(skillId, tab, slot, skill, hasLearned);
            entries[skillId] = entry;
            return entry;
        }

        private RoSkillDisplayEntry CreateEntry(int skillId, int tab, int slot, RoGameplaySkillInfo skill, bool hasLearned) {
            RoSkillMetadata metadata = skillMetadata != null ? skillMetadata.GetSkillOrFallback(skillId) : null;
            string name = ResolveSkillName(skill, skillId, metadata);
            int level = hasLearned ? ResolveLevel(skill) : 0;
            RoSkillTargetFlags targetFlags = ResolveTargetFlags(skill, metadata);
            bool passive = hasLearned && targetFlags == RoSkillTargetFlags.None;
            bool canSelectUseLevel = CanSelectUseLevel(hasLearned, level, passive, targetFlags);
            int selectedLevel = ResolveSelectedUseLevel(skillId, level, canSelectUseLevel);
            bool draggable = hasLearned && IsSkillDraggable(skill, metadata);
            int sp = ResolveDisplaySpCost(skill, metadata, hasLearned, selectedLevel);
            int plannedDelta = plannedDeltas.TryGetValue(skillId, out int delta) ? delta : 0;
            return new RoSkillDisplayEntry(skillId, tab, slot, name, metadata, skill, hasLearned, level, selectedLevel, sp, passive, draggable,
                canSelectUseLevel, targetFlags, plannedDelta);
        }

        // ---- the skill-up plan ----

        /// <summary>One more level for the skill, the missing prerequisite levels planned with it, when the points cover all of them.</summary>
        public void PlanSkillUpgrade(RoSkillDisplayEntry entry) {
            if (entry.SkillId <= 0 || SkillPoints <= 0) return;
            planIncrementScratch.Clear();
            planVisitScratch.Clear();
            BuildMissingPrerequisitePlan(entry.SkillId, planIncrementScratch, planVisitScratch);
            int effectiveLevel = entry.EffectiveLevel + GetPendingIncrement(planIncrementScratch, entry.SkillId);
            int maxLevel = entry.Metadata != null && entry.Metadata.MaxLevel > 0 ? entry.Metadata.MaxLevel : Math.Max(entry.Level + 1, 1);
            if (effectiveLevel >= maxLevel) return;
            AddIncrement(planIncrementScratch, entry.SkillId, 1);
            int requested = 0;
            foreach (KeyValuePair<int, int> pair in planIncrementScratch) requested += pair.Value;
            if (requested <= 0 || requested > Remaining) return;
            foreach (KeyValuePair<int, int> pair in planIncrementScratch) {
                plannedDeltas.TryGetValue(pair.Key, out int current);
                plannedDeltas[pair.Key] = current + pair.Value;
                for (int i = 0; i < pair.Value; i++) plannedOrder.Add(pair.Key);
            }

            Changed?.Invoke();
        }

        /// <summary>Apply: CZ_UPGRADE_SKILLLEVEL per planned level in the planned order, then the plan is cleared.</summary>
        public async void ApplyPlannedUpgrades() {
            RoLoginShellSession session = Session;
            if (session == null || plannedOrder.Count == 0 || applyingPlannedUpgrades) return;
            applyingPlannedUpgrades = true;
            plannedOrderApplyScratch.Clear();
            plannedOrderApplyScratch.AddRange(plannedOrder);
            try {
                for (int i = 0; i < plannedOrderApplyScratch.Count; i++) await session.BeginPlannedSkillUpgrade(plannedOrderApplyScratch[i]);
                ResetUpgradePlan();
            } catch (Exception ex) {
                Debug.LogWarning($"Planned skill-up request was not fully sent: {ex.Message}");
            } finally {
                plannedOrderApplyScratch.Clear();
                applyingPlannedUpgrades = false;
            }
        }

        public void ResetUpgradePlan() {
            ClearPlan();
            Changed?.Invoke();
        }

        /// <summary>Leaving the map: the plan and the chosen use levels are forgotten (nothing is sent).</summary>
        public void Reset() {
            ClearPlan();
            selectedUseLevels.Clear();
            entries.Clear();
        }

        private void ClearPlan() {
            plannedDeltas.Clear();
            plannedOrder.Clear();
            planIncrementScratch.Clear();
            planVisitScratch.Clear();
        }

        private void BuildMissingPrerequisitePlan(int skillId, Dictionary<int, int> increments, HashSet<int> visiting) {
            if (skillId <= 0 || !visiting.Add(skillId)) return;
            RoSkillMetadata metadata = null;
            skillMetadata?.TryGetSkill(skillId, out metadata);
            IReadOnlyList<RoSkillRequirement> prerequisites = metadata != null ? metadata.Prerequisites : Array.Empty<RoSkillRequirement>();
            for (int i = 0; i < prerequisites.Count; i++) {
                RoSkillRequirement requirement = prerequisites[i];
                int current = GetEffectiveSkillLevel(requirement.SkillId) + GetPendingIncrement(increments, requirement.SkillId);
                if (current < requirement.Level) AddIncrement(increments, requirement.SkillId, requirement.Level - current);
                BuildMissingPrerequisitePlan(requirement.SkillId, increments, visiting);
            }

            visiting.Remove(skillId);
        }

        private int GetEffectiveSkillLevel(int skillId) {
            if (entries.TryGetValue(skillId, out RoSkillDisplayEntry entry)) return entry.EffectiveLevel;
            return plannedDeltas.TryGetValue(skillId, out int delta) ? delta : 0;
        }

        private static int GetPendingIncrement(Dictionary<int, int> increments, int skillId) => increments.TryGetValue(skillId, out int value) ? value : 0;

        private static void AddIncrement(Dictionary<int, int> increments, int skillId, int amount) {
            if (skillId <= 0 || amount <= 0) return;
            increments.TryGetValue(skillId, out int current);
            increments[skillId] = current + amount;
        }

        // ---- the use level of level-use skills ----

        /// <summary>The level arrows: the use level within 1..level (the learned level is the default and is not stored).</summary>
        public void AdjustSelectedUseLevel(int skillId, int delta) {
            if (skillId <= 0 || !entries.TryGetValue(skillId, out RoSkillDisplayEntry entry) || !entry.CanSelectLevel) return;
            int max = Math.Max(1, entry.Level);
            int next = Mathf.Clamp(entry.SelectedLevel + delta, 1, max);
            if (next >= max) selectedUseLevels.Remove(skillId);
            else selectedUseLevels[skillId] = next;
            Changed?.Invoke();
        }

        private int ResolveSelectedUseLevel(int skillId, int learnedLevel, bool canSelectUseLevel) {
            int max = Math.Max(1, learnedLevel);
            if (!canSelectUseLevel) {
                selectedUseLevels.Remove(skillId);
                return max;
            }

            if (!selectedUseLevels.TryGetValue(skillId, out int selected)) return max;
            selected = Mathf.Clamp(selected, 1, max);
            if (selected >= max) {
                selectedUseLevels.Remove(skillId);
                return max;
            }

            selectedUseLevels[skillId] = selected;
            return selected;
        }

        // ---- ZC_AUTORUN_SKILL ----

        /// <summary>The skills the server started (ZC_AUTORUN_SKILL), at the packet's level: self skills at once, others through the targeting.</summary>
        public void DrainPendingAutoRunSkills() {
            RoLoginShellSession session = Session;
            if (session == null) return;
            int guard = 8;
            while (guard-- > 0 && session.TryDequeueAutoRunSkill(out RoZcSkillInfo skillInfo)) {
                if (skillInfo.SkillId <= 0) continue;
                RefreshAssets();
                RequestUseSkill(CreateEntry(skillInfo.SkillId, RoSkillTreeLayout.TabCount, 0, new RoGameplaySkillInfo(skillInfo), true));
            }
        }

        /// <summary>A self-only skill is sent on the own actor (CZ_USE_SKILL); any other starts RoSkillTargetSelection.</summary>
        public void RequestUseSkill(RoSkillDisplayEntry entry) {
            RoLoginShellSession session = Session;
            if (session == null || !entry.CanUse) return;
            int level = Mathf.Max(1, entry.SelectedLevel);
            RoSkillTargetFlags targetFlags = entry.TargetFlags;
            if (targetFlags == RoSkillTargetFlags.None) return;
            RoSkillTargetFlags explicitTargetFlags = RoSkillTargetFlags.Enemy | RoSkillTargetFlags.Place | RoSkillTargetFlags.Friend |
                RoSkillTargetFlags.Trap | RoSkillTargetFlags.Pet | RoSkillTargetFlags.Homun;
            if ((targetFlags & RoSkillTargetFlags.Self) != 0 && (targetFlags & explicitTargetFlags) == 0 && TryResolveOwnActorAid(out int ownAid)) {
                _ = session.BeginLegacyUseSkillOnTarget(entry.SkillId, level, ownAid);
                return;
            }

            RoSkillTargetSelection.EnsureInScene().Begin(session, entry.SkillId, level, entry.DisplayName, targetFlags, entry.CanSelectLevel,
                Math.Max(1, entry.Level));
        }

        private bool TryResolveOwnActorAid(out int ownAid) {
            ownAid = 0;
            RoOnlineWorldController controller = ResolveWorldController(forceLookup: true);
            if (controller == null || controller.OwnActorId == 0 || controller.OwnActorId > int.MaxValue) return false;
            ownAid = unchecked((int) controller.OwnActorId);
            return ownAid > 0;
        }

        // ---- scene lookups (cached, retried twice a second while missing) ----

        public RoOnlineWorldController ResolveWorldController(bool forceLookup = false) {
            if (worldController != null) return worldController;
            float now = Time.unscaledTime;
            if (!forceLookup && now < nextWorldControllerLookupTime) return null;
            nextWorldControllerLookupTime = now + LookupRetrySeconds;
            worldController = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            return worldController;
        }

        public RoAssetDatabase ResolveAssetDatabase(bool forceLookup = false) {
            RoRuntimeMapController map = runtimeMapController;
            if (map == null) {
                float now = Time.unscaledTime;
                if (!forceLookup && now < nextRuntimeMapLookupTime) return null;
                nextRuntimeMapLookupTime = now + LookupRetrySeconds;
                map = UnityEngine.Object.FindFirstObjectByType<RoRuntimeMapController>();
                if (map != null) {
                    runtimeMapController = map;
                    nextRuntimeMapLookupTime = 0f;
                }
            }

            RoAssetDatabase assets = map != null && map.Content != null ? map.Content.Assets : null;
            return assets != null && assets.IsInitialized ? assets : null;
        }

        // ---- skill rules ----

        /// <summary>
        /// A learned skill with a level and a target can go to the shortcut bar; the server sends type 0 for some active
        /// skills (Find Stone, Item Appraisal, Change Cart), which <see cref="ResolveTargetFlags"/> makes self skills.
        /// </summary>
        public static bool IsSkillDraggable(RoGameplaySkillInfo skill, RoSkillMetadata metadata) {
            int level = ResolveLevel(skill);
            if (skill.SkillId == 0 || level <= 0) return false;
            return ResolveTargetFlags(skill, metadata) != RoSkillTargetFlags.None;
        }

        private static int ResolveLevel(RoGameplaySkillInfo skill) => skill.Level > 0 ? skill.Level : skill.Level2;

        private static bool CanSelectUseLevel(bool learned, int level, bool passive, RoSkillTargetFlags targetFlags) =>
            learned && level > 0 && !passive && targetFlags != RoSkillTargetFlags.None;

        private static int ResolveDisplaySpCost(RoGameplaySkillInfo skill, RoSkillMetadata metadata, bool hasLearned, int selectedLevel) {
            if (!hasLearned) return metadata != null ? Math.Max(0, metadata.GetSpCost(1)) : 0;
            int packetCost = Math.Max(0, (int) skill.SpCost);
            if (metadata == null || selectedLevel <= 0) return packetCost;
            int metadataCost = Math.Max(0, metadata.GetSpCost(selectedLevel));
            if (metadataCost > 0 || packetCost == 0) return metadataCost;
            return packetCost;
        }

        private static RoSkillTargetFlags ResolveTargetFlags(RoGameplaySkillInfo skill, RoSkillMetadata metadata) {
            RoSkillTargetFlags flags = metadata != null ? metadata.TargetFlags : RoSkillTargetFlags.None;
            RoSkillTargetFlags serverFlags = RoSkillMetadata.FromServerSkillType(skill.Type);
            if (serverFlags != RoSkillTargetFlags.None) flags = serverFlags;
            if (serverFlags == RoSkillTargetFlags.None &&
                (skill.SkillId == MerchantIdentifySkillId || skill.SkillId == MerchantChangeCartSkillId ||
                 skill.SkillId == MerchantCartDecorateSkillId || skill.SkillId == ThiefFindStoneSkillId)) {
                return RoSkillTargetFlags.Self;
            }

            return flags;
        }

        private static string ResolveSkillName(RoGameplaySkillInfo skill, int skillId, RoSkillMetadata metadata) {
            if (metadata != null) {
                string displayName = metadata.GetDisplayName();
                if (!IsRawAegisSkillName(displayName) && !string.Equals(displayName, "Unknown Skill", StringComparison.OrdinalIgnoreCase)) return displayName;
            }

            if (!string.IsNullOrWhiteSpace(skill.Name) && !IsRawAegisSkillName(skill.Name)) return skill.Name;
            string aegisName = !string.IsNullOrWhiteSpace(skill.Name) ? skill.Name : metadata != null ? metadata.AegisName : null;
            string friendly = ResolveClassicSkillDisplayName(aegisName);
            if (!string.IsNullOrEmpty(friendly)) return friendly;
            return metadata != null ? metadata.GetDisplayName() : "Skill " + skillId.ToString();
        }

        /// <summary>A readable name for a raw aegis name (MG_FIREBOLT → "Fire Bolt"), or null.</summary>
        internal static string ResolveClassicSkillDisplayName(string aegisName) {
            if (string.IsNullOrWhiteSpace(aegisName)) return null;
            string key = aegisName.Trim();
            if (ClassicSkillDisplayNames.TryGetValue(key, out string knownName)) return knownName;
            int underscore = key.IndexOf('_');
            string body = underscore >= 0 && underscore + 1 < key.Length ? key.Substring(underscore + 1) : key;
            if (string.IsNullOrWhiteSpace(body) || !IsRawAegisSkillName(key)) return null;
            body = body.Replace('_', ' ');
            body = Regex.Replace(body, "(BOLT|BALL|WALL|STORM|CURSE|DIVER|STRIKE|BEAT|RECOVERY|SIGHT)", " $1", RegexOptions.IgnoreCase);
            body = Regex.Replace(body, "\\s+", " ").Trim();
            if (body.Length == 0) return null;
            string[] parts = body.Split(' ');
            for (int i = 0; i < parts.Length; i++) {
                if (string.Equals(parts[i], "SP", StringComparison.OrdinalIgnoreCase) || string.Equals(parts[i], "HP", StringComparison.OrdinalIgnoreCase)) {
                    parts[i] = parts[i].ToUpperInvariant();
                    continue;
                }

                parts[i] = InvariantTitle(parts[i]);
            }

            return string.Join(" ", parts);
        }

        private static string InvariantTitle(string value) {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string lower = value.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + (lower.Length > 1 ? lower.Substring(1) : string.Empty);
        }

        /// <summary>An upper-case name with an underscore (an aegis name such as MG_FIREBOLT).</summary>
        internal static bool IsRawAegisSkillName(string value) {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string trimmed = value.Trim();
            return trimmed.IndexOf('_') >= 0 && string.Equals(trimmed, trimmed.ToUpperInvariant(), StringComparison.Ordinal);
        }
    }
}
