// Synced from reference/port/src/RoClassicFx/Shared/render-lists/FxMsvcSort.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>Strict weak ordering used by <see cref="FxMsvcSort"/> (a struct so the sort is not virtual).</summary>
    public interface IFxLess<T>
    {
        bool Less(in T a, in T b);
    }

    /// <summary>
    /// MSVC's <c>std::sort</c> (introsort: median-of-3 / Tukey ninther guess, fat partition, 1.5·log2(n)
    /// division budget, insertion sort at ≤ 32 elements, heap sort when the budget runs out) exactly as the
    /// client's instantiations run it: 0x537120 (pairs {float key, RP*}: partition 0x536200, insertion 0x5355f0,
    /// make_heap 0x535710, pop-heap hole 0x5368c0) and 0x536a10 (RP* by texture: partition 0x535a90).
    /// The result equals the client's for any comparator, including ones with equivalent elements
    /// (the texture sorts), because the element moves are the same.
    /// </summary>
    public static class FxMsvcSort
    {
        const int InsertionMax = 32;   // _ISORT_MAX

        public static void Sort<T, TLess>(T[] a, int first, int last, TLess less) where TLess : struct, IFxLess<T>
            => SortUnchecked(a, first, last, last - first, ref less);

        static void SortUnchecked<T, TLess>(T[] a, int first, int last, int ideal, ref TLess less) where TLess : struct, IFxLess<T>
        {
            for (;;)
            {
                if (last - first <= InsertionMax) { InsertionSort(a, first, last, ref less); return; }
                if (ideal <= 0)
                {
                    MakeHeap(a, first, last, ref less);
                    for (; last - first >= 2; --last) PopHeap(a, first, last, ref less);
                    return;
                }
                PartitionByMedianGuess(a, first, last, ref less, out int midFirst, out int midLast);
                ideal = (ideal >> 1) + (ideal >> 2);
                if (midFirst - first < last - midLast)
                {
                    SortUnchecked(a, first, midFirst, ideal, ref less);
                    first = midLast;
                }
                else
                {
                    SortUnchecked(a, midLast, last, ideal, ref less);
                    last = midFirst;
                }
            }
        }

        static void InsertionSort<T, TLess>(T[] a, int first, int last, ref TLess less) where TLess : struct, IFxLess<T>
        {
            if (first == last) return;
            for (int mid = first + 1; mid != last; ++mid)
            {
                int hole = mid;
                T val = a[mid];
                if (less.Less(val, a[first]))
                {
                    Array.Copy(a, first, a, first + 1, mid - first);
                    a[first] = val;
                }
                else
                {
                    for (int prev = hole - 1; less.Less(val, a[prev]); hole = prev, --prev) a[hole] = a[prev];
                    a[hole] = val;
                }
            }
        }

        static void Swap<T>(T[] a, int i, int j) { T t = a[i]; a[i] = a[j]; a[j] = t; }

        static void Med3<T, TLess>(T[] a, int first, int mid, int last, ref TLess less) where TLess : struct, IFxLess<T>
        {
            if (less.Less(a[mid], a[first])) Swap(a, mid, first);
            if (less.Less(a[last], a[mid]))
            {
                Swap(a, last, mid);
                if (less.Less(a[mid], a[first])) Swap(a, mid, first);
            }
        }

        static void GuessMedian<T, TLess>(T[] a, int first, int mid, int last, ref TLess less) where TLess : struct, IFxLess<T>
        {
            int count = last - first;
            if (40 < count)
            {
                int step = (count + 1) >> 3, twoStep = step << 1;
                Med3(a, first, first + step, first + twoStep, ref less);
                Med3(a, mid - step, mid, mid + step, ref less);
                Med3(a, last - twoStep, last - step, last, ref less);
                Med3(a, first + step, mid, last - step, ref less);
            }
            else Med3(a, first, mid, last, ref less);
        }

        static void PartitionByMedianGuess<T, TLess>(T[] a, int first, int last, ref TLess less, out int pFirstOut, out int pLastOut)
            where TLess : struct, IFxLess<T>
        {
            int mid = first + ((last - first) >> 1);
            GuessMedian(a, first, mid, last - 1, ref less);
            int pFirst = mid, pLast = pFirst + 1;
            while (first < pFirst && !less.Less(a[pFirst - 1], a[pFirst]) && !less.Less(a[pFirst], a[pFirst - 1])) --pFirst;
            while (pLast < last && !less.Less(a[pLast], a[pFirst]) && !less.Less(a[pFirst], a[pLast])) ++pLast;
            int gFirst = pLast, gLast = pFirst;
            for (;;)
            {
                for (; gFirst < last; ++gFirst)
                {
                    if (less.Less(a[pFirst], a[gFirst])) continue;
                    if (less.Less(a[gFirst], a[pFirst])) break;
                    if (pLast != gFirst) Swap(a, pLast, gFirst);
                    ++pLast;
                }
                for (; first < gLast; --gLast)
                {
                    if (less.Less(a[gLast - 1], a[pFirst])) continue;
                    if (less.Less(a[pFirst], a[gLast - 1])) break;
                    if (--pFirst != gLast - 1) Swap(a, pFirst, gLast - 1);
                }
                if (gLast == first && gFirst == last) { pFirstOut = pFirst; pLastOut = pLast; return; }
                if (gLast == first)
                {
                    if (pLast != gFirst) Swap(a, pFirst, pLast);
                    ++pLast;
                    Swap(a, pFirst, gFirst);
                    ++pFirst;
                    ++gFirst;
                }
                else if (gFirst == last)
                {
                    if (--gLast != --pFirst) Swap(a, gLast, pFirst);
                    Swap(a, pFirst, --pLast);
                }
                else
                {
                    Swap(a, gFirst, --gLast);
                    ++gFirst;
                }
            }
        }

        static void PushHeapByIndex<T, TLess>(T[] a, int first, int hole, int top, T val, ref TLess less) where TLess : struct, IFxLess<T>
        {
            for (int idx = (hole - 1) >> 1; top < hole && less.Less(a[first + idx], val); idx = (hole - 1) >> 1)
            {
                a[first + hole] = a[first + idx];
                hole = idx;
            }
            a[first + hole] = val;
        }

        static void PopHeapHoleByIndex<T, TLess>(T[] a, int first, int hole, int bottom, T val, ref TLess less) where TLess : struct, IFxLess<T>
        {
            int top = hole, idx = hole;
            int maxNonLeaf = (bottom - 1) >> 1;
            while (idx < maxNonLeaf)
            {
                idx = 2 * idx + 2;
                if (less.Less(a[first + idx], a[first + idx - 1])) --idx;
                a[first + hole] = a[first + idx];
                hole = idx;
            }
            if (idx == maxNonLeaf && bottom % 2 == 0)
            {
                a[first + hole] = a[first + bottom - 1];
                hole = bottom - 1;
            }
            PushHeapByIndex(a, first, hole, top, val, ref less);
        }

        static void MakeHeap<T, TLess>(T[] a, int first, int last, ref TLess less) where TLess : struct, IFxLess<T>
        {
            int bottom = last - first;
            for (int hole = bottom >> 1; hole > 0;)
            {
                --hole;
                PopHeapHoleByIndex(a, first, hole, bottom, a[first + hole], ref less);
            }
        }

        static void PopHeap<T, TLess>(T[] a, int first, int last, ref TLess less) where TLess : struct, IFxLess<T>
        {
            if (2 <= last - first)
            {
                --last;
                T val = a[last];
                a[last] = a[first];
                PopHeapHoleByIndex(a, first, 0, last - first, val, ref less);
            }
        }
    }
}
