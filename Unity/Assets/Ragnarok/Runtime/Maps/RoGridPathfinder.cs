using System;
using System.Collections.Generic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Maps {
    public static class RoGridPathfinder {
        private const int StraightCost = 10;
        private const int DiagonalCost = 14;
        private const int UnvisitedCost = int.MaxValue;

        [ThreadStatic] private static PathSearchWorkspace searchWorkspace;

        private static readonly Vector2Int[] Directions = {
            new Vector2Int(1, -1),
            new Vector2Int(1, 0),
            new Vector2Int(1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, -1),
            new Vector2Int(0, -1)
        };

        public static bool TryFindPath(GAT altitude, Vector2Int start, Vector2Int target, List<Vector2Int> path, int maxSteps = int.MaxValue) {
            return TryFindPath(altitude, start, target, null, path, maxSteps);
        }

        public static void PrepareSearchWorkspace(GAT altitude) {
            if (altitude == null || altitude.cells == null) {
                return;
            }

            int width = (int) altitude.width;
            int height = (int) altitude.height;
            if (width <= 0 || height <= 0) {
                return;
            }

            SearchWorkspace.EnsureCapacity(width * height);
        }

        public static bool TryFindPath(
            GAT altitude,
            Vector2Int start,
            Vector2Int target,
            ISet<Vector2Int> blockedCells,
            List<Vector2Int> path,
            int maxSteps = int.MaxValue
        ) {
            path.Clear();
            if (!IsTraversable(altitude, target.x, target.y, blockedCells)) {
                return false;
            }

            if (!IsWalkable(altitude, start.x, start.y) &&
                !TryFindNearestTraversable(altitude, start, 8, blockedCells, out start)) {
                return false;
            }

            if (start == target) {
                path.Add(start);
                return true;
            }

            if (TryFindClientDirectPath(altitude, start, target, blockedCells, path, maxSteps)) {
                return true;
            }

            int width = (int) altitude.width;
            int height = (int) altitude.height;
            int size = width * height;
            int targetIndex = ToIndex(target.x, target.y, width);
            int startIndex = ToIndex(start.x, start.y, width);

            PathSearchWorkspace workspace = SearchWorkspace;
            workspace.Reset(size);

            workspace.SetCost(startIndex, 0, -1);
            workspace.PushOpen(startIndex, target, width);

            while (workspace.TryPopOpen(out int currentIndex)) {
                if (currentIndex == targetIndex) {
                    BuildPath(workspace, currentIndex, width, path);
                    if (path.Count - 1 > maxSteps) {
                        path.Clear();
                        return false;
                    }

                    return true;
                }

                workspace.SetClosed(currentIndex);
                int currentX = currentIndex % width;
                int currentY = currentIndex / width;
                int currentCost = workspace.GetCost(currentIndex);

                for (int i = 0; i < Directions.Length; i++) {
                    Vector2Int direction = Directions[i];
                    int nextX = currentX + direction.x;
                    int nextY = currentY + direction.y;
                    if (!CanStep(altitude, currentX, currentY, nextX, nextY, blockedCells)) {
                        continue;
                    }

                    int nextIndex = ToIndex(nextX, nextY, width);
                    if (workspace.IsClosed(nextIndex)) {
                        continue;
                    }

                    int moveCost = direction.x != 0 && direction.y != 0 ? DiagonalCost : StraightCost;
                    int candidateCost = currentCost + moveCost;
                    if (candidateCost >= workspace.GetCost(nextIndex)) {
                        continue;
                    }

                    workspace.SetCost(nextIndex, candidateCost, currentIndex);
                    workspace.PushOpen(nextIndex, target, width);
                }
            }

            return false;
        }

        public static bool TryFindNearestWalkable(GAT altitude, Vector2Int origin, int radius, out Vector2Int cell) {
            if (IsWalkable(altitude, origin.x, origin.y)) {
                cell = origin;
                return true;
            }

            for (int distance = 1; distance <= radius; distance++) {
                int minX = origin.x - distance;
                int maxX = origin.x + distance;
                int minY = origin.y - distance;
                int maxY = origin.y + distance;

                for (int y = minY; y <= maxY; y++) {
                    for (int x = minX; x <= maxX; x++) {
                        bool edge = x == minX || x == maxX || y == minY || y == maxY;
                        if (edge && IsWalkable(altitude, x, y)) {
                            cell = new Vector2Int(x, y);
                            return true;
                        }
                    }
                }
            }

            cell = default;
            return false;
        }

        private static bool TryFindNearestTraversable(
            GAT altitude,
            Vector2Int origin,
            int radius,
            ISet<Vector2Int> blockedCells,
            out Vector2Int cell
        ) {
            if (IsTraversable(altitude, origin.x, origin.y, blockedCells)) {
                cell = origin;
                return true;
            }

            for (int distance = 1; distance <= radius; distance++) {
                int minX = origin.x - distance;
                int maxX = origin.x + distance;
                int minY = origin.y - distance;
                int maxY = origin.y + distance;

                for (int y = minY; y <= maxY; y++) {
                    for (int x = minX; x <= maxX; x++) {
                        bool edge = x == minX || x == maxX || y == minY || y == maxY;
                        if (edge && IsTraversable(altitude, x, y, blockedCells)) {
                            cell = new Vector2Int(x, y);
                            return true;
                        }
                    }
                }
            }

            cell = default;
            return false;
        }

        public static bool IsWalkable(GAT altitude, int x, int y) {
            if (altitude == null || altitude.cells == null) {
                return false;
            }

            int width = (int) altitude.width;
            int height = (int) altitude.height;
            if (x < 0 || y < 0 || x >= width || y >= height) {
                return false;
            }

            GAT.Cell cell = altitude.cells[ToIndex(x, y, width)];
            return (cell.type & (byte) GAT.TYPE.WALKABLE) != 0;
        }

        public static Vector3 CellToLocalCenter(GAT altitude, Vector2Int cell, float yOffset) {
            return RoMapCoordinateSystem.GatCellCenter(altitude, cell, yOffset);
        }

        public static bool TryFindClientDirectPath(GAT altitude, Vector2Int start, Vector2Int target, List<Vector2Int> path) {
            return TryFindClientDirectPath(altitude, start, target, null, path, int.MaxValue);
        }

        private static bool TryFindClientDirectPath(
            GAT altitude,
            Vector2Int start,
            Vector2Int target,
            ISet<Vector2Int> blockedCells,
            List<Vector2Int> path,
            int maxSteps
        ) {
            path.Clear();
            if (altitude == null || !IsTraversable(altitude, target.x, target.y, blockedCells)) {
                return false;
            }

            int x = start.x;
            int y = start.y;
            int dx = Sign(target.x - start.x);
            int dy = Sign(target.y - start.y);
            int guard = (int) altitude.width + (int) altitude.height + 1;
            path.Add(start);

            while ((x != target.x || y != target.y) && guard-- > 0) {
                int nextX = x + dx;
                int nextY = y + dy;
                if (!CanStep(altitude, x, y, nextX, nextY, blockedCells)) {
                    path.Clear();
                    return false;
                }

                x = nextX;
                y = nextY;
                path.Add(new Vector2Int(x, y));

                if (x == target.x) {
                    dx = 0;
                }

                if (y == target.y) {
                    dy = 0;
                }
            }

            bool found = x == target.x && y == target.y;
            if (!found || path.Count - 1 > maxSteps) {
                path.Clear();
                return false;
            }

            return true;
        }

        private static bool CanStep(GAT altitude, int fromX, int fromY, int toX, int toY) {
            return CanStep(altitude, fromX, fromY, toX, toY, null);
        }

        private static bool CanStep(
            GAT altitude,
            int fromX,
            int fromY,
            int toX,
            int toY,
            ISet<Vector2Int> blockedCells
        ) {
            if (!IsTraversable(altitude, toX, toY, blockedCells)) {
                return false;
            }

            int deltaX = toX - fromX;
            int deltaY = toY - fromY;
            bool diagonal = deltaX != 0 && deltaY != 0;
            if (!diagonal) {
                return true;
            }

            return IsTraversable(altitude, fromX + deltaX, fromY, blockedCells) &&
                   IsTraversable(altitude, fromX, fromY + deltaY, blockedCells);
        }

        private static bool IsTraversable(GAT altitude, int x, int y, ISet<Vector2Int> blockedCells) {
            return IsWalkable(altitude, x, y) && !IsBlocked(blockedCells, x, y);
        }

        private static bool IsBlocked(ISet<Vector2Int> blockedCells, int x, int y) {
            return blockedCells != null && blockedCells.Contains(new Vector2Int(x, y));
        }

        private static int Heuristic(int x, int y, int targetX, int targetY) {
            return StraightCost * (Mathf.Abs(x - targetX) + Mathf.Abs(y - targetY));
        }

        private static void BuildPath(PathSearchWorkspace workspace, int currentIndex, int width, List<Vector2Int> path) {
            path.Clear();
            while (currentIndex >= 0) {
                path.Add(new Vector2Int(currentIndex % width, currentIndex / width));
                currentIndex = workspace.GetCameFrom(currentIndex);
            }

            path.Reverse();
        }

        private static int ToIndex(int x, int y, int width) {
            return x + y * width;
        }

        private static int Sign(int value) {
            return value == 0 ? 0 : value < 0 ? -1 : 1;
        }

        private static PathSearchWorkspace SearchWorkspace {
            get {
                searchWorkspace ??= new PathSearchWorkspace();
                return searchWorkspace;
            }
        }

        private sealed class PathSearchWorkspace {
            private const int InitialHeapCapacity = 256;

            public int[] VisitStamp = Array.Empty<int>();
            public bool[] Closed = Array.Empty<bool>();
            public int[] CostFromStart = Array.Empty<int>();
            public int[] CameFrom = Array.Empty<int>();
            public int[] Heap = Array.Empty<int>();
            public int[] HeapCost = Array.Empty<int>();
            public int[] HeapScore = Array.Empty<int>();
            public int CurrentStamp;
            public int HeapCount;

            public void EnsureCapacity(int size) {
                if (size <= 0) {
                    return;
                }

                if (Closed.Length < size) {
                    VisitStamp = new int[size];
                    Closed = new bool[size];
                    CostFromStart = new int[size];
                    CameFrom = new int[size];
                }

                if (Heap.Length < InitialHeapCapacity) {
                    Heap = new int[InitialHeapCapacity];
                    HeapCost = new int[InitialHeapCapacity];
                    HeapScore = new int[InitialHeapCapacity];
                }
            }

            public void Reset(int size) {
                EnsureCapacity(size);
                HeapCount = 0;
                if (CurrentStamp == int.MaxValue) {
                    Array.Clear(VisitStamp, 0, VisitStamp.Length);
                    CurrentStamp = 1;
                } else {
                    CurrentStamp++;
                }
            }

            public int GetCost(int index) {
                return VisitStamp[index] == CurrentStamp ? CostFromStart[index] : UnvisitedCost;
            }

            public int GetCameFrom(int index) {
                return VisitStamp[index] == CurrentStamp ? CameFrom[index] : -1;
            }

            public bool IsClosed(int index) {
                return VisitStamp[index] == CurrentStamp && Closed[index];
            }

            public void SetClosed(int index) {
                EnsureVisited(index);
                Closed[index] = true;
            }

            public void SetCost(int index, int cost, int cameFrom) {
                EnsureVisited(index);
                CostFromStart[index] = cost;
                CameFrom[index] = cameFrom;
            }

            public void PushOpen(int index, Vector2Int target, int width) {
                EnsureHeapCapacity(HeapCount + 1);
                int costAtPush = GetCost(index);
                int heapIndex = HeapCount++;
                Heap[heapIndex] = index;
                HeapCost[heapIndex] = costAtPush;
                HeapScore[heapIndex] = costAtPush + Heuristic(index % width, index / width, target.x, target.y);
                SiftUp(heapIndex);
            }

            public bool TryPopOpen(out int index) {
                while (HeapCount > 0) {
                    index = PopOpen(out int costAtPush);
                    if (!IsClosed(index) && costAtPush == GetCost(index)) {
                        return true;
                    }
                }

                index = -1;
                return false;
            }

            private void EnsureVisited(int index) {
                if (VisitStamp[index] == CurrentStamp) {
                    return;
                }

                VisitStamp[index] = CurrentStamp;
                Closed[index] = false;
                CostFromStart[index] = UnvisitedCost;
                CameFrom[index] = -1;
            }

            private int PopOpen(out int costAtPush) {
                int result = Heap[0];
                costAtPush = HeapCost[0];
                HeapCount--;
                if (HeapCount > 0) {
                    Heap[0] = Heap[HeapCount];
                    HeapCost[0] = HeapCost[HeapCount];
                    HeapScore[0] = HeapScore[HeapCount];
                    SiftDown(0);
                }

                return result;
            }

            private void EnsureHeapCapacity(int size) {
                if (Heap.Length >= size) {
                    return;
                }

                int nextSize = Mathf.Max(InitialHeapCapacity, Heap.Length * 2);
                while (nextSize < size) {
                    nextSize *= 2;
                }

                Array.Resize(ref Heap, nextSize);
                Array.Resize(ref HeapCost, nextSize);
                Array.Resize(ref HeapScore, nextSize);
            }

            private void SiftUp(int index) {
                while (index > 0) {
                    int parent = (index - 1) / 2;
                    if (!IsHigherPriority(index, parent)) {
                        return;
                    }

                    Swap(index, parent);
                    index = parent;
                }
            }

            private void SiftDown(int index) {
                while (true) {
                    int left = index * 2 + 1;
                    if (left >= HeapCount) {
                        return;
                    }

                    int right = left + 1;
                    int best = right < HeapCount && IsHigherPriority(right, left) ? right : left;
                    if (!IsHigherPriority(best, index)) {
                        return;
                    }

                    Swap(index, best);
                    index = best;
                }
            }

            private bool IsHigherPriority(int left, int right) {
                int leftScore = HeapScore[left];
                int rightScore = HeapScore[right];
                if (leftScore != rightScore) {
                    return leftScore < rightScore;
                }

                int leftCost = HeapCost[left];
                int rightCost = HeapCost[right];
                if (leftCost != rightCost) {
                    return leftCost < rightCost;
                }

                return Heap[left] < Heap[right];
            }

            private void Swap(int left, int right) {
                int temp = Heap[left];
                Heap[left] = Heap[right];
                Heap[right] = temp;

                temp = HeapCost[left];
                HeapCost[left] = HeapCost[right];
                HeapCost[right] = temp;

                temp = HeapScore[left];
                HeapScore[left] = HeapScore[right];
                HeapScore[right] = temp;
            }
        }
    }
}
