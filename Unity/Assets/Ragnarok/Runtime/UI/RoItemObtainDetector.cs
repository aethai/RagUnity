using System;
using System.Collections.Generic;
using Ragnarok.Data;
using Ragnarok.Gameplay;

namespace Ragnarok.UI {
    public readonly struct RoItemObtainNotice {
        public RoItemObtainNotice(RoItemInstance item, int amount) {
            Item = item;
            Amount = amount;
        }

        public RoItemInstance Item { get; }
        public int Amount { get; }
    }

    /// <summary>
    /// Watches the inventory for gained items (a stack that appeared or grew between two inventory sequences) and shows
    /// the last gain in the classic pickup notice (RoClassicNotify.ShowItemObtained, UINotifyItemObtainWnd). A whole
    /// reload of the inventory (login, character change, map re-entry) only resets the baseline.
    /// </summary>
    public sealed class RoItemObtainDetector {
        private readonly Dictionary<int, ObservedInventoryItem> previousItems = new Dictionary<int, ObservedInventoryItem>();
        private readonly List<RoItemObtainNotice> gainBuffer = new List<RoItemObtainNotice>(4);
        private RoLoginShellSession session;
        private Func<RoItemMetadataTable> metadata;
        private int observedInventorySequence = int.MinValue;
        private bool hasBaseline;

        public static RoItemObtainNotice[] DetectInventoryGains(RoItemInstance[] previous, RoItemInstance[] current) {
            var observed = new Dictionary<int, ObservedInventoryItem>();
            CaptureSnapshot(previous, observed);
            var notices = new List<RoItemObtainNotice>();
            AppendGains(observed, current, notices);
            return notices.ToArray();
        }

        public void Bind(RoLoginShellSession loginSession, Func<RoItemMetadataTable> itemMetadata) {
            session = loginSession;
            metadata = itemMetadata;
            observedInventorySequence = session != null ? session.InventoryWindowChangeSequence : int.MinValue;
            hasBaseline = false;
            previousItems.Clear();
            if (session != null) {
                CaptureSnapshot(session.InventorySnapshot().InventoryItems, previousItems);
                hasBaseline = true;
            }
        }

        public void ObserveInventorySequence(int sequence) {
            if (session == null || sequence == observedInventorySequence) {
                return;
            }

            observedInventorySequence = sequence;
            RoItemInstance[] items = session.InventorySnapshot().InventoryItems ?? Array.Empty<RoItemInstance>();
            gainBuffer.Clear();
            if (hasBaseline) {
                AppendGains(previousItems, items, gainBuffer);
            }

            CaptureSnapshot(items, previousItems);
            hasBaseline = true;
            if (gainBuffer.Count > 0) {
                RoItemObtainNotice notice = gainBuffer[gainBuffer.Count - 1];
                RoClassicNotify.ShowItemObtained(notice.Item, notice.Amount, metadata?.Invoke() ?? RoItemMetadataTable.Empty);
            }
        }

        public void ObserveInventoryReload() {
            if (session == null) {
                return;
            }

            previousItems.Clear();
            CaptureSnapshot(session.InventorySnapshot().InventoryItems, previousItems);
            hasBaseline = true;
            observedInventorySequence = session.InventoryWindowChangeSequence;
        }

        private static void AppendGains(
            IReadOnlyDictionary<int, ObservedInventoryItem> previous,
            RoItemInstance[] current,
            List<RoItemObtainNotice> output) {
            if (current == null || output == null) {
                return;
            }

            for (int i = 0; i < current.Length; i++) {
                RoItemInstance item = current[i];
                if (!IsObservable(item)) {
                    continue;
                }

                int amount = item.Count;
                if (previous != null &&
                    previous.TryGetValue(item.BagIndex, out ObservedInventoryItem observed) &&
                    observed.ItemId == item.ItemId) {
                    amount = item.Count - observed.Count;
                }

                if (amount > 0) {
                    output.Add(new RoItemObtainNotice(item, amount));
                }
            }
        }

        private static void CaptureSnapshot(RoItemInstance[] items, Dictionary<int, ObservedInventoryItem> target) {
            if (target == null) {
                return;
            }

            target.Clear();
            if (items == null) {
                return;
            }

            for (int i = 0; i < items.Length; i++) {
                RoItemInstance item = items[i];
                if (IsObservable(item)) {
                    target[item.BagIndex] = new ObservedInventoryItem(item.ItemId, item.Count);
                }
            }
        }

        private static bool IsObservable(RoItemInstance item) {
            return item != null && item.BagIndex > 0 && item.ItemId > 0 && item.Count > 0;
        }

        private readonly struct ObservedInventoryItem {
            public ObservedInventoryItem(int itemId, int count) {
                ItemId = itemId;
                Count = Math.Max(0, count);
            }

            public int ItemId { get; }
            public int Count { get; }
        }
    }
}
