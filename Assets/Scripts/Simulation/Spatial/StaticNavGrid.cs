using System;
using UnityEngine;

namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// Pure C# static navigation grid representing permanent world obstacles (walls, fixed boundaries).
    /// Thread-safe for concurrent read-only queries during off-thread pathfinding and sensory checks.
    /// </summary>
    public class StaticNavGrid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        public readonly float OffsetX;
        public readonly float OffsetY;

        private readonly bool[] _walkable;
        private float _playableHalfExtent = float.PositiveInfinity;

        // Connected-region labels (4-connected walkable cells; matches A*'s no-corner-cutting rule).
        // Rebuilt on the main thread into a fresh array and published by reference, so background readers
        // always see a complete, immutable labelling. Allocation happens only when walkability changes.
        private volatile int[] _regionLabels;
        private int _walkabilityVersion;
        private int _regionsVersion = -1;
        private int[] _floodQueue;

        /// <summary>Incremented whenever a cell's walkability actually changes.</summary>
        public int WalkabilityVersion => _walkabilityVersion;

        /// <summary>Number of times region labels have been rebuilt (diagnostics/tests).</summary>
        public int RegionRebuildCount { get; private set; }

        /// <summary>Snap radius (cells) used to find a walkable cell when a query point sits on a wall edge.</summary>
        public const int RegionSnapRadius = 2;

        public StaticNavGrid(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            OffsetX = (width * cellSize) * 0.5f;
            OffsetY = (height * cellSize) * 0.5f;

            int totalCells = width * height;
            _walkable = new bool[totalCells];

            // Default all cells inside bounds to walkable
            for (int i = 0; i < totalCells; i++)
            {
                _walkable[i] = true;
            }
        }

        public bool InBounds(int cellX, int cellY)
        {
            return cellX >= 0 && cellX < Width && cellY >= 0 && cellY < Height;
        }

        public bool IsCellWalkable(int cellX, int cellY)
        {
            if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height)
            {
                return false;
            }
            return _walkable[cellY * Width + cellX];
        }

        public void SetCellWalkable(int cellX, int cellY, bool walkable)
        {
            if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height)
            {
                return;
            }
            int index = cellY * Width + cellX;
            if (_walkable[index] != walkable)
            {
                _walkable[index] = walkable;
                _walkabilityVersion++;
            }
        }

        public void SetWall(int cellX, int cellY)
        {
            SetCellWalkable(cellX, cellY, false);
        }

        public void Clear()
        {
            for (int i = 0; i < _walkable.Length; i++)
            {
                _walkable[i] = true;
            }
            ApplyPlayableBounds();
            _walkabilityVersion++;
        }

        /// <summary>
        /// Permanently blocks every cell lying entirely outside [-halfExtent, halfExtent] on either axis.
        /// Survives <see cref="Clear"/>, since the play area is not a wall that can be removed.
        /// </summary>
        public void SetPlayableHalfExtent(float halfExtent)
        {
            _playableHalfExtent = halfExtent;
            ApplyPlayableBounds();
            _walkabilityVersion++;
        }

        private void ApplyPlayableBounds()
        {
            if (float.IsPositiveInfinity(_playableHalfExtent)) return;
            for (int y = 0; y < Height; y++)
            {
                float minY = y * CellSize - OffsetY;
                bool rowOutside = minY >= _playableHalfExtent || minY + CellSize <= -_playableHalfExtent;
                for (int x = 0; x < Width; x++)
                {
                    float minX = x * CellSize - OffsetX;
                    if (rowOutside || minX >= _playableHalfExtent || minX + CellSize <= -_playableHalfExtent)
                    {
                        _walkable[y * Width + x] = false;
                    }
                }
            }
        }

        /// <summary>
        /// Rebuilds region labels if walkability changed since the last build. Main thread only.
        /// </summary>
        public void EnsureRegions()
        {
            if (_regionsVersion == _walkabilityVersion && _regionLabels != null) return;

            int total = Width * Height;
            var labels = new int[total];
            if (_floodQueue == null || _floodQueue.Length < total) _floodQueue = new int[total];
            var queue = _floodQueue;

            for (int i = 0; i < total; i++) labels[i] = -1;

            int nextLabel = 0;
            for (int start = 0; start < total; start++)
            {
                if (!_walkable[start] || labels[start] >= 0) continue;

                int head = 0, tail = 0;
                queue[tail++] = start;
                labels[start] = nextLabel;

                while (head < tail)
                {
                    int cell = queue[head++];
                    int cx = cell % Width;
                    int cy = cell / Width;

                    if (cx > 0) Visit(cell - 1);
                    if (cx < Width - 1) Visit(cell + 1);
                    if (cy > 0) Visit(cell - Width);
                    if (cy < Height - 1) Visit(cell + Width);
                }

                nextLabel++;

                void Visit(int n)
                {
                    if (_walkable[n] && labels[n] < 0)
                    {
                        labels[n] = nextLabel;
                        queue[tail++] = n;
                    }
                }
            }

            _regionLabels = labels;
            _regionsVersion = _walkabilityVersion;
            RegionRebuildCount++;
        }

        /// <summary>
        /// Region label at a world position, snapping to the nearest walkable cell within RegionSnapRadius.
        /// Returns false when regions are not built, the point is out of bounds, or no walkable cell is near.
        /// Thread-safe for concurrent readers.
        /// </summary>
        public bool TryGetRegion(Vector2 worldPos, out int region)
        {
            region = -1;
            var labels = _regionLabels;
            if (labels == null) return false;
            if (!WorldToCell(worldPos, out int cx, out int cy)) return false;

            for (int r = 0; r <= RegionSnapRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring only
                        int x = cx + dx, y = cy + dy;
                        if (!InBounds(x, y)) continue;
                        int label = labels[y * Width + x];
                        if (label >= 0)
                        {
                            region = label;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// True when both points lie in the same walkable region. Unknown (no labels, out of bounds,
        /// buried in walls) is treated as connected so callers never block on missing data.
        /// </summary>
        public bool AreConnected(Vector2 a, Vector2 b)
        {
            if (!TryGetRegion(a, out int ra) || !TryGetRegion(b, out int rb)) return true;
            return ra == rb;
        }

        public bool WorldToCell(Vector2 worldPos, out int cellX, out int cellY)
        {
            cellX = Mathf.FloorToInt((worldPos.x + OffsetX) / CellSize);
            cellY = Mathf.FloorToInt((worldPos.y + OffsetY) / CellSize);
            return InBounds(cellX, cellY);
        }

        public Vector2 CellToWorld(int cellX, int cellY)
        {
            float wx = (cellX + 0.5f) * CellSize - OffsetX;
            float wy = (cellY + 0.5f) * CellSize - OffsetY;
            return new Vector2(wx, wy);
        }
    }
}
