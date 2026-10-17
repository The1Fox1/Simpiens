using System;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Pathfinding
{
    public struct PathNode
    {
        public short X;
        public short Y;
        public int ParentIndex;
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;
    }

    /// <summary>
    /// High-performance, zero-allocation 2D Grid A* pathfinder.
    /// Utilizes a pre-allocated binary min-heap, struct node pool, $O(1)$ epoch visited tracking,
    /// 8-directional octile heuristics, diagonal corner-cutting prevention, and raycast string-pulling path smoothing.
    /// Thread-safe when accessed by one thread at a time (pooled per worker).
    /// </summary>
    public class GridAStarSearcher
    {
        private const int MaxNodes = 8192;
        private const int MaxCells = 16384;
        private const int MaxPathLength = 256;
        private const float Sqrt2 = 1.41421356f;

        // 8 directions: 4 cardinal (0..3), 4 diagonal (4..7)
        private static readonly short[] Dx = { 0, 0, 1, -1, 1, 1, -1, -1 };
        private static readonly short[] Dy = { 1, -1, 0, 0, 1, -1, 1, -1 };
        private static readonly float[] StepCosts = { 1.0f, 1.0f, 1.0f, 1.0f, Sqrt2, Sqrt2, Sqrt2, Sqrt2 };

        // Node pool & open min-heap
        private readonly PathNode[] _nodePool = new PathNode[MaxNodes];
        private int _nodeCount;

        private readonly int[] _openHeap = new int[MaxNodes];
        private int _heapCount;

        // Visited lookup using epoch counter (O(1) search initialization without Array.Clear)
        private readonly uint[] _cellIteration = new uint[MaxCells];
        private readonly int[] _cellNodeIndex = new int[MaxCells];
        private uint _currentIteration;

        // Scratch buffers for raw path reconstruction and string-pulling smoothing
        private readonly Vec2[] _rawWaypoints = new Vec2[MaxPathLength];
        private readonly Vec2[] _smoothWaypoints = new Vec2[MaxPathLength];

        public bool Search(in PathRequest request, SharedWorldSnapshot snapshot, PathResponse response)
        {
            if (snapshot == null || response == null)
            {
                if (response != null)
                {
                    response.Length = 0;
                    response.IsValid = false;
                    response.IsComplete = true;
                }
                return false;
            }

            // World to Cell coordinate conversion
            float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
            float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;
            float cellSize = snapshot.CellSize;

            int startX = SimMath.FloorToInt((request.StartPosition.X + offsetX) / cellSize);
            int startY = SimMath.FloorToInt((request.StartPosition.Y + offsetY) / cellSize);
            int targetX = SimMath.FloorToInt((request.TargetPosition.X + offsetX) / cellSize);
            int targetY = SimMath.FloorToInt((request.TargetPosition.Y + offsetY) / cellSize);

            // Clamp / In-Bounds Validation
            if (startX < 0 || startX >= snapshot.Width || startY < 0 || startY >= snapshot.Height)
            {
                response.Length = 0;
                response.IsValid = false;
                response.IsComplete = true;
                return false;
            }

            // If start equals target, return trivial 1-waypoint path
            if (startX == targetX && startY == targetY)
            {
                response.Waypoints[0] = request.TargetPosition;
                response.Length = 1;
                response.IsValid = true;
                response.IsComplete = true;
                return true;
            }

            // Target walkability fallback: if exact target cell is blocked, find closest walkable adjacent neighbor
            bool targetSnapped = false;
            if (!snapshot.IsWalkable(targetX, targetY))
            {
                targetSnapped = true;
                if (!TryFindNearestWalkableNeighbor(snapshot, targetX, targetY, startX, startY, out targetX, out targetY))
                {
                    response.Length = 0;
                    response.IsValid = false;
                    response.IsComplete = true;
                    return false;
                }
            }

            // Advance search iteration epoch for O(1) state reset
            _currentIteration++;
            if (_currentIteration == 0)
            {
                Array.Clear(_cellIteration, 0, _cellIteration.Length);
                _currentIteration = 1;
            }

            _nodeCount = 0;
            _heapCount = 0;

            int gridWidth = snapshot.Width;
            int totalGridCells = gridWidth * snapshot.Height;
            if (totalGridCells > MaxCells)
            {
                totalGridCells = MaxCells;
            }

            // Initialize Root Node
            int startIndex = startY * gridWidth + startX;
            float initialH = CalculateOctileDistance(startX, startY, targetX, targetY, cellSize);
            int rootNodeIdx = AllocateNode((short)startX, (short)startY, -1, 0f, initialH);

            _cellIteration[startIndex] = _currentIteration;
            _cellNodeIndex[startIndex] = rootNodeIdx;
            HeapPush(rootNodeIdx);

            int goalNodeIdx = -1;

            // A* Search Loop
            while (_heapCount > 0)
            {
                int currentIdx = HeapPop();
                ref readonly var current = ref _nodePool[currentIdx];

                if (current.X == targetX && current.Y == targetY)
                {
                    goalNodeIdx = currentIdx;
                    break;
                }

                // Check node budget limit to safeguard frame time
                if (_nodeCount >= MaxNodes - 8)
                {
                    break;
                }

                // Explore 8 neighbors
                for (int d = 0; d < 8; d++)
                {
                    short nx = (short)(current.X + Dx[d]);
                    short ny = (short)(current.Y + Dy[d]);

                    // Bounds check
                    if (nx < 0 || nx >= snapshot.Width || ny < 0 || ny >= snapshot.Height)
                    {
                        continue;
                    }

                    int nCellIdx = ny * gridWidth + nx;
                    if (nCellIdx >= MaxCells)
                    {
                        continue;
                    }

                    // Walkability check
                    if (!snapshot.IsWalkable(nx, ny))
                    {
                        continue;
                    }

                    // Diagonal corner cutting prevention:
                    // For diagonal moves (d >= 4), both adjacent orthogonal cells MUST be walkable!
                    if (d >= 4)
                    {
                        if (!snapshot.IsWalkable(current.X, ny) || !snapshot.IsWalkable(nx, current.Y))
                        {
                            continue;
                        }
                    }

                    float stepCost = StepCosts[d] * cellSize;
                    float tentativeG = current.GCost + stepCost;

                    bool alreadyVisited = (_cellIteration[nCellIdx] == _currentIteration);
                    if (alreadyVisited)
                    {
                        int existingIdx = _cellNodeIndex[nCellIdx];
                        if (tentativeG >= _nodePool[existingIdx].GCost)
                        {
                            continue;
                        }

                        // Found a shorter path to existing node
                        _nodePool[existingIdx].GCost = tentativeG;
                        _nodePool[existingIdx].ParentIndex = currentIdx;
                        HeapUpdate(existingIdx);
                    }
                    else
                    {
                        // New node discovery
                        float h = CalculateOctileDistance(nx, ny, targetX, targetY, cellSize);
                        int newNodeIdx = AllocateNode(nx, ny, currentIdx, tentativeG, h);

                        _cellIteration[nCellIdx] = _currentIteration;
                        _cellNodeIndex[nCellIdx] = newNodeIdx;
                        HeapPush(newNodeIdx);
                    }
                }
            }

            if (goalNodeIdx == -1)
            {
                response.Length = 0;
                response.IsValid = false;
                response.IsComplete = true;
                return false;
            }

            // Reconstruct raw waypoints (from goal backward to start)
            int rawLength = 0;
            int curr = goalNodeIdx;
            while (curr != -1 && rawLength < MaxPathLength)
            {
                float wx = (_nodePool[curr].X + 0.5f) * cellSize - offsetX;
                float wy = (_nodePool[curr].Y + 0.5f) * cellSize - offsetY;
                _rawWaypoints[rawLength++] = new Vec2(wx, wy);
                curr = _nodePool[curr].ParentIndex;
            }

            // Reverse raw path so it points from Start to Goal
            for (int i = 0; i < rawLength / 2; i++)
            {
                var tmp = _rawWaypoints[i];
                _rawWaypoints[i] = _rawWaypoints[rawLength - 1 - i];
                _rawWaypoints[rawLength - 1 - i] = tmp;
            }

            // Anchor endpoints to exact request positions. A target inside a blocked cell (wall, stall, anchor) is
            // replaced by the closest point of the walkable cell A* actually reached, so pawns never path into geometry.
            _rawWaypoints[0] = request.StartPosition;
            _rawWaypoints[rawLength - 1] = targetSnapped
                ? ClosestPointInCell(request.TargetPosition, targetX, targetY, cellSize, offsetX, offsetY)
                : request.TargetPosition;

            // Apply Raycast String-Pulling Path Smoothing
            int smoothLength = ApplyPathSmoothing(snapshot, rawLength);

            // Copy to response
            int copyCount = SimMath.Min(smoothLength, response.Waypoints.Length);
            for (int i = 0; i < copyCount; i++)
            {
                response.Waypoints[i] = _smoothWaypoints[i];
            }

            response.Length = copyCount;
            response.IsValid = copyCount > 0;
            response.IsComplete = true;
            return true;
        }

        private const float CellInset = 0.05f;

        /// <summary>Smoothed segments keep this far from hearth and stall centres (movement stops pawns at 0.45).</summary>
        public const float PropClearance = 0.75f;

        private static Vec2 ClosestPointInCell(Vec2 point, int cellX, int cellY, float cellSize, float offsetX, float offsetY)
        {
            float minX = cellX * cellSize - offsetX + CellInset;
            float minY = cellY * cellSize - offsetY + CellInset;
            float maxX = (cellX + 1) * cellSize - offsetX - CellInset;
            float maxY = (cellY + 1) * cellSize - offsetY - CellInset;
            return new Vec2(SimMath.Clamp(point.X, minX, maxX), SimMath.Clamp(point.Y, minY, maxY));
        }

        private int ApplyPathSmoothing(SharedWorldSnapshot snapshot, int rawCount)
        {
            if (rawCount <= 2)
            {
                for (int i = 0; i < rawCount; i++)
                {
                    _smoothWaypoints[i] = _rawWaypoints[i];
                }
                return rawCount;
            }

            int smoothCount = 0;
            _smoothWaypoints[smoothCount++] = _rawWaypoints[0];

            int anchor = 0;
            while (anchor < rawCount - 1)
            {
                // Greedily look as far ahead as possible with unobstructed line of sight
                int furthest = anchor + 1;
                for (int test = rawCount - 1; test > anchor + 1; test--)
                {
                    if (snapshot.IsSegmentStaticClear(_rawWaypoints[anchor], _rawWaypoints[test]) &&
                        snapshot.HasLineOfSight(_rawWaypoints[anchor], _rawWaypoints[test]) &&
                        snapshot.IsSegmentPropClear(_rawWaypoints[anchor], _rawWaypoints[test], PropClearance))
                    {
                        furthest = test;
                        break;
                    }
                }

                _smoothWaypoints[smoothCount++] = _rawWaypoints[furthest];
                anchor = furthest;

                if (smoothCount >= MaxPathLength)
                {
                    break;
                }
            }

            return smoothCount;
        }

        private bool TryFindNearestWalkableNeighbor(
            SharedWorldSnapshot snapshot,
            int targetX,
            int targetY,
            int startX,
            int startY,
            out int bestX,
            out int bestY)
        {
            bestX = targetX;
            bestY = targetY;
            float bestDistSq = float.MaxValue;
            bool found = false;

            for (int d = 0; d < 8; d++)
            {
                int nx = targetX + Dx[d];
                int ny = targetY + Dy[d];

                if (nx >= 0 && nx < snapshot.Width && ny >= 0 && ny < snapshot.Height)
                {
                    if (snapshot.IsWalkable(nx, ny))
                    {
                        float distSq = (nx - startX) * (nx - startX) + (ny - startY) * (ny - startY);
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestX = nx;
                            bestY = ny;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        private int AllocateNode(short x, short y, int parentIndex, float gCost, float hCost)
        {
            int idx = _nodeCount++;
            _nodePool[idx] = new PathNode
            {
                X = x,
                Y = y,
                ParentIndex = parentIndex,
                GCost = gCost,
                HCost = hCost
            };
            return idx;
        }

        private static float CalculateOctileDistance(int x1, int y1, int x2, int y2, float cellSize)
        {
            int dx = Math.Abs(x1 - x2);
            int dy = Math.Abs(y1 - y2);
            int min = dx < dy ? dx : dy;
            int max = dx > dy ? dx : dy;
            return ((max - min) + Sqrt2 * min) * cellSize;
        }

        private void HeapPush(int nodeIndex)
        {
            int i = _heapCount++;
            _openHeap[i] = nodeIndex;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (CompareNodes(_openHeap[i], _openHeap[parent]) < 0)
                {
                    int tmp = _openHeap[i];
                    _openHeap[i] = _openHeap[parent];
                    _openHeap[parent] = tmp;
                    i = parent;
                }
                else break;
            }
        }

        private int HeapPop()
        {
            int top = _openHeap[0];
            _heapCount--;
            if (_heapCount > 0)
            {
                _openHeap[0] = _openHeap[_heapCount];
                int i = 0;
                while (true)
                {
                    int left = (i << 1) + 1;
                    int right = left + 1;
                    if (left >= _heapCount) break;

                    int best = left;
                    if (right < _heapCount && CompareNodes(_openHeap[right], _openHeap[left]) < 0)
                    {
                        best = right;
                    }

                    if (CompareNodes(_openHeap[best], _openHeap[i]) < 0)
                    {
                        int tmp = _openHeap[i];
                        _openHeap[i] = _openHeap[best];
                        _openHeap[best] = tmp;
                        i = best;
                    }
                    else break;
                }
            }
            return top;
        }

        private void HeapUpdate(int nodeIndex)
        {
            // Simple reheapify up from position in heap
            for (int i = 0; i < _heapCount; i++)
            {
                if (_openHeap[i] == nodeIndex)
                {
                    while (i > 0)
                    {
                        int parent = (i - 1) >> 1;
                        if (CompareNodes(_openHeap[i], _openHeap[parent]) < 0)
                        {
                            int tmp = _openHeap[i];
                            _openHeap[i] = _openHeap[parent];
                            _openHeap[parent] = tmp;
                            i = parent;
                        }
                        else break;
                    }
                    break;
                }
            }
        }

        private int CompareNodes(int a, int b)
        {
            float fA = _nodePool[a].FCost;
            float fB = _nodePool[b].FCost;
            if (fA < fB) return -1;
            if (fA > fB) return 1;

            float hA = _nodePool[a].HCost;
            float hB = _nodePool[b].HCost;
            if (hA < hB) return -1;
            if (hA > hB) return 1;

            return 0;
        }
    }
}
