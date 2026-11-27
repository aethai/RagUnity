using System;

namespace Ragnarok.Combat {
    /// <summary>
    /// Items due at a time, taken out in time order (equal times in the order they were added): a binary min-heap over
    /// a flat array, O(log n) per add and take, no allocation once the array has grown. The combat presentation queues
    /// its timed work here (hits, numbers, effect launches) instead of scanning lists every frame, so its cost stays
    /// flat with hundreds of actors.
    /// </summary>
    public sealed class RoTimeline<T> {
        struct Entry {
            public float Due;
            public long Sequence;
            public T Item;
        }

        Entry[] heap;
        int count;
        long nextSequence;

        public RoTimeline(int capacity = 32) {
            heap = new Entry[Math.Max(4, capacity)];
        }

        public int Count => count;

        /// <summary>The earliest due time, or +∞ when empty.</summary>
        public float NextDue => count > 0 ? heap[0].Due : float.PositiveInfinity;

        public void Add(float due, in T item) {
            if (count == heap.Length) {
                Array.Resize(ref heap, heap.Length * 2);
            }

            int i = count++;
            var entry = new Entry { Due = due, Sequence = nextSequence++, Item = item };
            while (i > 0) {
                int parent = (i - 1) >> 1;
                if (!Before(in entry, in heap[parent])) {
                    break;
                }

                heap[i] = heap[parent];
                i = parent;
            }

            heap[i] = entry;
        }

        /// <summary>Takes the earliest item if it is due at <paramref name="now"/>.</summary>
        public bool TryTakeDue(float now, out T item) {
            if (count == 0 || heap[0].Due > now) {
                item = default;
                return false;
            }

            item = heap[0].Item;
            Entry last = heap[--count];
            heap[count] = default;
            if (count > 0) {
                SiftDown(in last);
            }

            return true;
        }

        /// <summary>Removes every item <paramref name="match"/> accepts (e.g. the cues of an actor that left), keeping the order.</summary>
        public int RemoveAll(Predicate<T> match) {
            if (match == null || count == 0) {
                return 0;
            }

            int kept = 0;
            for (int i = 0; i < count; i++) {
                if (!match(heap[i].Item)) {
                    heap[kept++] = heap[i];
                }
            }

            int removed = count - kept;
            Array.Clear(heap, kept, removed);
            count = kept;
            for (int i = (count >> 1) - 1; i >= 0; i--) {
                Entry e = heap[i];
                SiftDownFrom(i, in e);
            }

            return removed;
        }

        /// <summary>The latest-due item <paramref name="match"/> accepts and its due time; false when none matches.</summary>
        public bool TryGetLatest(Predicate<T> match, out float due, out T item) {
            due = float.NegativeInfinity;
            item = default;
            bool found = false;
            for (int i = 0; i < count; i++) {
                if (heap[i].Due >= due && match(heap[i].Item)) {
                    due = heap[i].Due;
                    item = heap[i].Item;
                    found = true;
                }
            }

            return found;
        }

        public void Clear() {
            Array.Clear(heap, 0, count);
            count = 0;
        }

        void SiftDown(in Entry entry) => SiftDownFrom(0, in entry);

        void SiftDownFrom(int i, in Entry entry) {
            int half = count >> 1;
            while (i < half) {
                int child = 2 * i + 1;
                int right = child + 1;
                if (right < count && Before(in heap[right], in heap[child])) {
                    child = right;
                }

                if (!Before(in heap[child], in entry)) {
                    break;
                }

                heap[i] = heap[child];
                i = child;
            }

            heap[i] = entry;
        }

        static bool Before(in Entry a, in Entry b) => a.Due < b.Due || (a.Due == b.Due && a.Sequence < b.Sequence);
    }
}
