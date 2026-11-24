using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Gameplay;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    public static class RoQuestTrackingPreferences {
        private const string TrackerVisibleKey = "Ragnarok.UI.QuestTracker.Visible";
        private const string HiddenQuestIdsKey = "Ragnarok.UI.QuestTracker.HiddenQuestIds";

        public static bool IsTrackerVisible => PlayerPrefs.GetInt(TrackerVisibleKey, 1) != 0;

        public static void SetTrackerVisible(bool visible) {
            PlayerPrefs.SetInt(TrackerVisibleKey, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool IsQuestTracked(uint questId) {
            return questId != 0 && !ReadHiddenQuestIds().Contains(questId);
        }

        public static void SetQuestTracked(uint questId, bool tracked) {
            if (questId == 0) {
                return;
            }

            HashSet<uint> hidden = ReadHiddenQuestIds();
            if (tracked) {
                hidden.Remove(questId);
            } else {
                hidden.Add(questId);
            }

            WriteHiddenQuestIds(hidden);
        }

        public static bool ToggleQuestTracked(uint questId) {
            bool next = !IsQuestTracked(questId);
            SetQuestTracked(questId, next);
            return next;
        }

        public static void ClearForTests() {
            PlayerPrefs.DeleteKey(TrackerVisibleKey);
            PlayerPrefs.DeleteKey(HiddenQuestIdsKey);
            PlayerPrefs.Save();
        }

        private static HashSet<uint> ReadHiddenQuestIds() {
            var result = new HashSet<uint>();
            string raw = PlayerPrefs.GetString(HiddenQuestIdsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) {
                return result;
            }

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++) {
                if (uint.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint questId) && questId != 0) {
                    result.Add(questId);
                }
            }

            return result;
        }

        private static void WriteHiddenQuestIds(HashSet<uint> hidden) {
            if (hidden == null || hidden.Count == 0) {
                PlayerPrefs.DeleteKey(HiddenQuestIdsKey);
                PlayerPrefs.Save();
                return;
            }

            uint[] ids = new uint[hidden.Count];
            hidden.CopyTo(ids);
            Array.Sort(ids);
            var builder = new StringBuilder(ids.Length * 6);
            for (int i = 0; i < ids.Length; i++) {
                if (i > 0) {
                    builder.Append(',');
                }

                builder.Append(ids[i].ToString(CultureInfo.InvariantCulture));
            }

            PlayerPrefs.SetString(HiddenQuestIdsKey, builder.ToString());
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The quest table of the loaded map content (questid2display / the client's quest LUB), with a table loaded from
    /// the assets when the content's own is missing or smaller. Used by the classic quest windows.
    /// </summary>
    public static class RoQuestMetadataResolver {
        private const float AssetControllerLookupRetrySeconds = 0.5f;
        private static RoOnlineWorldController cachedAssetWorldController;
        private static RoRuntimeMapController cachedAssetRuntimeMapController;
        private static float nextAssetWorldLookupTime;
        private static float nextAssetRuntimeMapLookupTime;
        private static RoAssetDatabase fallbackQuestMetadataAssets;
        private static RoQuestMetadataTable fallbackQuestMetadataTable = RoQuestMetadataTable.Empty;

        public static RoQuestMetadataTable ResolveQuestMetadata(RoOnlineWorldController preferredWorld = null) {
            RoAssetDatabase assets = ResolveInitializedAssets(preferredWorld);
            if (assets == null) {
                return RoQuestMetadataTable.Empty;
            }

            RoQuestMetadataTable table = assets.GameData?.Quests;
            if (!ReferenceEquals(fallbackQuestMetadataAssets, assets)) {
                fallbackQuestMetadataAssets = assets;
                fallbackQuestMetadataTable = RoQuestMetadataTable.Load(assets);
            }

            if (fallbackQuestMetadataTable != null &&
                fallbackQuestMetadataTable.IsLoaded &&
                (table == null || !table.IsLoaded || fallbackQuestMetadataTable.Count > table.Count)) {
                return fallbackQuestMetadataTable;
            }

            if (table != null && table.IsLoaded) {
                return table;
            }

            return fallbackQuestMetadataTable != null && fallbackQuestMetadataTable.IsLoaded
                ? fallbackQuestMetadataTable
                : table ?? RoQuestMetadataTable.Empty;
        }

        public static RoAssetDatabase ResolveInitializedAssets(RoOnlineWorldController preferredWorld = null) {
            RoRuntimeMapController runtimeMap = ResolveRuntimeMapControllerForAssets(preferredWorld);

            return runtimeMap != null &&
                runtimeMap.Content != null &&
                runtimeMap.Content.Assets != null &&
                runtimeMap.Content.Assets.IsInitialized
                ? runtimeMap.Content.Assets
                : null;
        }

        private static RoRuntimeMapController ResolveRuntimeMapControllerForAssets(RoOnlineWorldController preferredWorld) {
            if (preferredWorld != null) {
                CacheAssetWorldController(preferredWorld);
                if (preferredWorld.RuntimeMapController != null) {
                    return preferredWorld.RuntimeMapController;
                }
            }

            if (cachedAssetWorldController != null) {
                RoRuntimeMapController runtimeMap = cachedAssetWorldController.RuntimeMapController;
                if (runtimeMap != null) {
                    cachedAssetRuntimeMapController = runtimeMap;
                    nextAssetRuntimeMapLookupTime = 0f;
                    return runtimeMap;
                }
            }

            float now = Time.unscaledTime;
            if (now >= nextAssetWorldLookupTime) {
                cachedAssetWorldController = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
                nextAssetWorldLookupTime = cachedAssetWorldController != null
                    ? 0f
                    : now + AssetControllerLookupRetrySeconds;
                if (cachedAssetWorldController != null && cachedAssetWorldController.RuntimeMapController != null) {
                    cachedAssetRuntimeMapController = cachedAssetWorldController.RuntimeMapController;
                    nextAssetRuntimeMapLookupTime = 0f;
                    return cachedAssetRuntimeMapController;
                }
            }

            if (cachedAssetRuntimeMapController != null) {
                return cachedAssetRuntimeMapController;
            }

            if (now < nextAssetRuntimeMapLookupTime) {
                return null;
            }

            cachedAssetRuntimeMapController = UnityEngine.Object.FindFirstObjectByType<RoRuntimeMapController>();
            nextAssetRuntimeMapLookupTime = cachedAssetRuntimeMapController != null
                ? 0f
                : now + AssetControllerLookupRetrySeconds;
            return cachedAssetRuntimeMapController;
        }

        private static void CacheAssetWorldController(RoOnlineWorldController world) {
            if (world == null) {
                return;
            }

            cachedAssetWorldController = world;
            nextAssetWorldLookupTime = 0f;
            if (world.RuntimeMapController != null) {
                cachedAssetRuntimeMapController = world.RuntimeMapController;
                nextAssetRuntimeMapLookupTime = 0f;
            }
        }
    }
}
