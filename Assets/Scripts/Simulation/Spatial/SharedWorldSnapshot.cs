using System;
using System.Collections.Generic;
using System.Threading;
using Simpiens.Cognition.Groups;
using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// A pooled, thread-safe snapshot of the entire world's spatial state.
    /// Shared by all cognitive agents during a tick to avoid per-agent allocations.
    /// Uses reference counting to return to the pool when all background tasks are done.
    /// </summary>
    public class SharedWorldSnapshot
    {
        private int _refCount;
        private Action<SharedWorldSnapshot> _returnToPoolAction;

        public readonly float CellSize;
        public readonly int Width;
        public readonly int Height;

        // Using a 1D array for the grid: index = y * Width + x
        // Each cell has a pre-allocated array of EntitySnapshots.
        public readonly EntitySnapshot[][] Grid;
        public readonly int[] CellCounts;

        // Sociopolitical Group Snapshot (Epic 8)
        public const int MaxGroups = 64;
        public readonly GroupData[] GroupBuffer = new GroupData[MaxGroups];
        public int ActiveGroupCount;

        // Outpost Economy & Trade Stalls (Epic 9 Slice 2)
        public const int MaxStalls = 8;
        public readonly TradeStallData[] StallBuffer = new TradeStallData[MaxStalls];
        public int ActiveStallCount;

        // Outpost Economy & Public Bounties (Epic 9 Slice 3)
        public const int MaxBounties = 16;
        public readonly BountyContract[] BountyBuffer = new BountyContract[MaxBounties];
        public int ActiveBountyCount;
        public ReadOnlySpan<BountyContract> Bounties => new ReadOnlySpan<BountyContract>(BountyBuffer, 0, ActiveBountyCount);

        // Static Navigation Grid (Section 2.6)
        public StaticNavGrid StaticGrid { get; private set; }
        public void SetStaticGrid(StaticNavGrid staticGrid) => StaticGrid = staticGrid;

        /// <summary>The loaded map (immutable; null in headless tests that build no map). Sectors, POIs, layout.</summary>
        public Simpiens.Simulation.World.MapDefinition Map { get; private set; }
        public void SetMap(Simpiens.Simulation.World.MapDefinition map) => Map = map;

        /// <summary>The session's economy tuning (immutable; null before a session applies one: read through Considerations.Economy).</summary>
        public Simpiens.Simulation.Economy.EconomyTuning Economy { get; private set; }
        public void SetEconomy(Simpiens.Simulation.Economy.EconomyTuning economy) => Economy = economy;

        /// <summary>
        /// Fog of war (Epic MAP Phase 3): bit i set when sector i is charted, meaning a scout has reported on it or it
        /// started explored. Nobody perceives what is inside an uncharted sector. Defaults to everything charted.
        /// </summary>
        public ulong ExploredSectorMask { get; private set; } = ulong.MaxValue;

        // Rest anchor (Epic MAP Phase 3): bunks with a sleeper this tick
        public const int MaxBunks = 32;
        private readonly UnityEngine.GUID[] _occupiedBunks = new UnityEngine.GUID[MaxBunks];
        public int OccupiedBunkCount { get; private set; }

        public void SetOccupiedBunks(IReadOnlyList<UnityEngine.GUID> occupied)
        {
            int count = occupied != null ? Mathf.Min(occupied.Count, MaxBunks) : 0;
            for (int i = 0; i < count; i++) _occupiedBunks[i] = occupied[i];
            OccupiedBunkCount = count;
        }

        public bool IsBunkOccupied(UnityEngine.GUID bunkId)
        {
            for (int i = 0; i < OccupiedBunkCount; i++)
            {
                if (_occupiedBunks[i] == bunkId) return true;
            }
            return false;
        }
        public void SetExploredSectors(ulong mask) => ExploredSectorMask = mask;

        public bool IsSectorCharted(int sectorId)
        {
            if (sectorId < 0 || sectorId >= 64) return true;
            return (ExploredSectorMask & (1UL << sectorId)) != 0;
        }

        /// <summary>True when the point lies in a charted sector, or when no map is loaded.</summary>
        public bool IsCharted(Vector2 position)
        {
            return Map == null || IsSectorCharted(Map.FindSector(position).Id);
        }

        public SharedWorldSnapshot(int width, int height, float cellSize, int maxEntitiesPerCell, Action<SharedWorldSnapshot> returnAction)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _returnToPoolAction = returnAction;

            int totalCells = width * height;
            Grid = new EntitySnapshot[totalCells][];
            CellCounts = new int[totalCells];

            for (int i = 0; i < totalCells; i++)
            {
                Grid[i] = new EntitySnapshot[maxEntitiesPerCell];
            }
        }

        public void Retain()
        {
            Interlocked.Increment(ref _refCount);
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                _returnToPoolAction(this);
            }
        }

        public void Clear()
        {
            Array.Clear(CellCounts, 0, CellCounts.Length);
            Array.Clear(GroupBuffer, 0, ActiveGroupCount);
            ActiveGroupCount = 0;
            Array.Clear(StallBuffer, 0, ActiveStallCount);
            ActiveStallCount = 0;
            Array.Clear(BountyBuffer, 0, ActiveBountyCount);
            ActiveBountyCount = 0;
        }

        /// <summary>
        /// Lock-free, zero-allocation lookup of a group's snapshot state by group ID (1..64).
        /// </summary>
        public bool TryGetGroup(byte groupId, out GroupData groupData)
        {
            if (groupId == 0 || groupId > MaxGroups)
            {
                groupData = GroupData.Empty;
                return false;
            }

            int count = ActiveGroupCount;
            for (int i = 0; i < count; i++)
            {
                if (GroupBuffer[i].GroupId == groupId)
                {
                    groupData = GroupBuffer[i];
                    return true;
                }
            }

            groupData = GroupData.Empty;
            return false;
        }

        /// <summary>
        /// Synchronizes group snapshot data into the pre-allocated buffer with zero heap allocations.
        /// </summary>
        public void SetGroups(GroupData[] source, int count)
        {
            if (source == null)
            {
                ActiveGroupCount = 0;
                return;
            }

            int toCopy = Mathf.Min(count, MaxGroups);
            Array.Copy(source, GroupBuffer, toCopy);
            ActiveGroupCount = toCopy;
        }

        /// <summary>
        /// Lock-free, zero-allocation lookup of a trade stall's snapshot state by entity ID.
        /// </summary>
        public bool TryGetStall(GUID stallId, out TradeStallData stallData)
        {
            if (stallId == default)
            {
                stallData = TradeStallData.Empty;
                return false;
            }

            int count = ActiveStallCount;
            for (int i = 0; i < count; i++)
            {
                if (StallBuffer[i].StallEntityId == stallId)
                {
                    stallData = StallBuffer[i];
                    return true;
                }
            }

            stallData = TradeStallData.Empty;
            return false;
        }

        /// <summary>
        /// Synchronizes trade stall snapshot data into the pre-allocated buffer with zero heap allocations.
        /// </summary>
        public void SetStalls(TradeStallData[] source, int count)
        {
            if (source == null)
            {
                ActiveStallCount = 0;
                return;
            }

            int toCopy = Mathf.Min(count, MaxStalls);
            Array.Copy(source, StallBuffer, toCopy);
            ActiveStallCount = toCopy;
        }

        /// <summary>
        /// Synchronizes trade stall snapshot data from a list with zero heap allocations.
        /// </summary>
        public void SetStalls(IReadOnlyList<TradeStallData> source)
        {
            if (source == null)
            {
                ActiveStallCount = 0;
                return;
            }

            int toCopy = Mathf.Min(source.Count, MaxStalls);
            for (int i = 0; i < toCopy; i++)
            {
                StallBuffer[i] = source[i];
            }
            ActiveStallCount = toCopy;
        }

        /// <summary>
        /// Lock-free, zero-allocation lookup of a public bounty contract by bounty ID.
        /// </summary>
        public bool TryGetBounty(ushort bountyId, out BountyContract bounty)
        {
            if (bountyId == 0)
            {
                bounty = BountyContract.Empty;
                return false;
            }

            int count = ActiveBountyCount;
            for (int i = 0; i < count; i++)
            {
                if (BountyBuffer[i].BountyId == bountyId)
                {
                    bounty = BountyBuffer[i];
                    return true;
                }
            }

            bounty = BountyContract.Empty;
            return false;
        }

        /// <summary>
        /// Synchronizes public bounty contracts into the pre-allocated buffer with zero heap allocations.
        /// </summary>
        public void SetBounties(BountyContract[] source, int count)
        {
            if (source == null)
            {
                ActiveBountyCount = 0;
                return;
            }

            int toCopy = Mathf.Min(count, MaxBounties);
            Array.Copy(source, BountyBuffer, toCopy);
            ActiveBountyCount = toCopy;
        }

        /// <summary>
        /// Synchronizes public bounty contracts from a list with zero heap allocations.
        /// </summary>
        public void SetBounties(IReadOnlyList<BountyContract> source)
        {
            if (source == null)
            {
                ActiveBountyCount = 0;
                return;
            }

            int toCopy = Mathf.Min(source.Count, MaxBounties);
            for (int i = 0; i < toCopy; i++)
            {
                BountyBuffer[i] = source[i];
            }
            ActiveBountyCount = toCopy;
        }

        public void AddEntity(int cellX, int cellY, in EntitySnapshot snapshot)
        {
            if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height) return;

            int index = cellY * Width + cellX;
            int count = CellCounts[index];

            if (count < Grid[index].Length)
            {
                Grid[index][count] = snapshot;
                CellCounts[index]++;
            }
            // If the cell is full, we drop it for now, or we could have dynamic resizing 
            // but dynamic resizing allocates. In a strict zero-alloc setup, we size appropriately.
        }

        /// <summary>
        /// Checks if a given cell is walkable: no static or dynamic wall, and no anchor (hearth) or stall centred in it.
        /// Anchors and stalls are small props listed in every cell their radius overlaps; they block only the cell
        /// holding their centre, so a hearth on a cell corner cannot seal off a narrow room. Movement keeps pawns off
        /// the prop itself (SimulationManager.IsPositionBlocked). Returns false if out of bounds.
        /// </summary>
        public bool IsWalkable(int cellX, int cellY)
        {
            if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height) return false;

            if (StaticGrid != null && !StaticGrid.IsCellWalkable(cellX, cellY))
            {
                return false;
            }

            int index = cellY * Width + cellX;
            int count = CellCounts[index];
            var cellArray = Grid[index];

            for (int i = 0; i < count; i++)
            {
                var type = cellArray[i].Type;
                if (type == EntityType.Wall)
                {
                    return false;
                }
                if ((type == EntityType.Anchor || type == EntityType.Stall) && IsCentredInCell(cellArray[i].Position, cellX, cellY))
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsCentredInCell(Vector2 position, int cellX, int cellY)
        {
            float offsetX = Width * CellSize * 0.5f;
            float offsetY = Height * CellSize * 0.5f;
            return Mathf.FloorToInt((position.x + offsetX) / CellSize) == cellX &&
                   Mathf.FloorToInt((position.y + offsetY) / CellSize) == cellY;
        }

        /// <summary>
        /// Checks whether a straight line between two world positions is unobstructed by walls and corner intersections.
        /// Zero allocations, thread-safe off-thread evaluation.
        /// </summary>
        public bool HasLineOfSight(Vector2 start, Vector2 end)
        {
            float distance = Vector2.Distance(start, end);
            if (distance < 0.1f) return true;

            float stepSize = 0.25f;
            int steps = Mathf.CeilToInt(distance / stepSize);
            Vector2 dir = (end - start) / distance;

            float offsetX = (Width * CellSize) * 0.5f;
            float offsetY = (Height * CellSize) * 0.5f;

            int prevCellX = Mathf.FloorToInt((start.x + offsetX) / CellSize);
            int prevCellY = Mathf.FloorToInt((start.y + offsetY) / CellSize);

            // Check intermediate steps along the ray
            for (int i = 1; i <= steps; i++)
            {
                Vector2 sample = (i == steps) ? end : start + dir * (i * stepSize);
                int cellX = Mathf.FloorToInt((sample.x + offsetX) / CellSize);
                int cellY = Mathf.FloorToInt((sample.y + offsetY) / CellSize);

                if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height)
                {
                    return false;
                }

                // Check diagonal transition: if moving diagonally across cells, both orthogonal cells must be clear
                if (cellX != prevCellX && cellY != prevCellY)
                {
                    if (!IsWalkable(cellX, prevCellY) || !IsWalkable(prevCellX, cellY))
                    {
                        return false;
                    }
                }

                // Check target cell walkability (excluding final destination point if it's an interactable object)
                if (i < steps)
                {
                    if (StaticGrid != null && !StaticGrid.IsCellWalkable(cellX, cellY))
                    {
                        return false;
                    }

                    int index = cellY * Width + cellX;
                    int count = CellCounts[index];
                    var cellArray = Grid[index];
                    for (int c = 0; c < count; c++)
                    {
                        if (cellArray[c].Type == EntityType.Wall)
                        {
                            return false;
                        }
                    }
                }

                prevCellX = cellX;
                prevCellY = cellY;
            }

            return true;
        }

        /// <summary>
        /// True when the world point lies in a statically walkable cell (static walls only). Out of bounds is not walkable.
        /// With no static grid every in-bounds point is walkable. Zero allocations, thread-safe.
        /// </summary>
        public bool IsStaticWalkable(Vector2 worldPos)
        {
            float offsetX = (Width * CellSize) * 0.5f;
            float offsetY = (Height * CellSize) * 0.5f;
            int cellX = Mathf.FloorToInt((worldPos.x + offsetX) / CellSize);
            int cellY = Mathf.FloorToInt((worldPos.y + offsetY) / CellSize);
            if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height) return false;
            return StaticGrid == null || StaticGrid.IsCellWalkable(cellX, cellY);
        }

        /// <summary>
        /// Exact static clearance of a segment: visits every cell the segment touches (grid traversal, not sampling)
        /// and fails on any statically blocked cell. Passing exactly through a cell corner requires both orthogonal
        /// cells to be walkable, matching A*'s no-corner-cutting rule. Zero allocations, thread-safe.
        /// </summary>
        public bool IsSegmentStaticClear(Vector2 start, Vector2 end)
        {
            float offsetX = (Width * CellSize) * 0.5f;
            float offsetY = (Height * CellSize) * 0.5f;
            float sx = (start.x + offsetX) / CellSize, sy = (start.y + offsetY) / CellSize;
            float ex = (end.x + offsetX) / CellSize, ey = (end.y + offsetY) / CellSize;

            int x = Mathf.FloorToInt(sx), y = Mathf.FloorToInt(sy);
            int endX = Mathf.FloorToInt(ex), endY = Mathf.FloorToInt(ey);
            if (!IsStaticCellWalkable(x, y) || !IsStaticCellWalkable(endX, endY)) return false;

            float dx = ex - sx, dy = ey - sy;
            int stepX = dx > 0f ? 1 : (dx < 0f ? -1 : 0);
            int stepY = dy > 0f ? 1 : (dy < 0f ? -1 : 0);
            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dx) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dy) : float.PositiveInfinity;
            float tMaxX = stepX > 0 ? (x + 1 - sx) * tDeltaX : (stepX < 0 ? (sx - x) * tDeltaX : float.PositiveInfinity);
            float tMaxY = stepY > 0 ? (y + 1 - sy) * tDeltaY : (stepY < 0 ? (sy - y) * tDeltaY : float.PositiveInfinity);

            int guard = Width + Height + 2;
            while ((x != endX || y != endY) && guard-- > 0)
            {
                if (Mathf.Abs(tMaxX - tMaxY) < 1e-5f)
                {
                    // Exact corner crossing: both orthogonal neighbours must be open
                    if (!IsStaticCellWalkable(x + stepX, y) || !IsStaticCellWalkable(x, y + stepY)) return false;
                    x += stepX; y += stepY;
                    tMaxX += tDeltaX; tMaxY += tDeltaY;
                }
                else if (tMaxX < tMaxY)
                {
                    x += stepX; tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY; tMaxY += tDeltaY;
                }

                if (!IsStaticCellWalkable(x, y)) return false;
            }
            return true;
        }

        /// <summary>
        /// True when a straight segment keeps at least <paramref name="clearance"/> from every hearth (anchor) and stall
        /// centre. Line of sight ignores these props (agents see over a campfire), so path smoothing checks this as
        /// well, or a smoothed segment could run straight through a fire. A prop within clearance of either endpoint
        /// is ignored: the path is leaving or approaching it. Clearance must not exceed one cell. Zero allocations.
        /// </summary>
        public bool IsSegmentPropClear(Vector2 start, Vector2 end, float clearance)
        {
            float distance = Vector2.Distance(start, end);
            float offsetX = (Width * CellSize) * 0.5f;
            float offsetY = (Height * CellSize) * 0.5f;
            float sqrClearance = clearance * clearance;

            // Samples at most half a cell apart; props within clearance of the segment lie within one cell of a sample
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (CellSize * 0.5f)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 sample = Vector2.Lerp(start, end, (float)i / steps);
                int sampleX = Mathf.FloorToInt((sample.x + offsetX) / CellSize);
                int sampleY = Mathf.FloorToInt((sample.y + offsetY) / CellSize);

                for (int cy = sampleY - 1; cy <= sampleY + 1; cy++)
                {
                    if (cy < 0 || cy >= Height) continue;
                    for (int cx = sampleX - 1; cx <= sampleX + 1; cx++)
                    {
                        if (cx < 0 || cx >= Width) continue;
                        int index = cy * Width + cx;
                        int count = CellCounts[index];
                        var cellArray = Grid[index];
                        for (int c = 0; c < count; c++)
                        {
                            var type = cellArray[c].Type;
                            if (type != EntityType.Anchor && type != EntityType.Stall) continue;

                            Vector2 prop = cellArray[c].Position;
                            if ((prop - start).sqrMagnitude < sqrClearance || (prop - end).sqrMagnitude < sqrClearance) continue;
                            if (SqrDistanceToSegment(prop, start, end) < sqrClearance) return false;
                        }
                    }
                }
            }
            return true;
        }

        private static float SqrDistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            float t = lengthSqr > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr) : 0f;
            return (point - (a + ab * t)).sqrMagnitude;
        }

        private bool IsStaticCellWalkable(int cellX, int cellY)
        {
            if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height) return false;
            return StaticGrid == null || StaticGrid.IsCellWalkable(cellX, cellY);
        }

        /// <summary>
        /// Thread-safe query method for cognitive agents to find entities in a radius.
        /// </summary>
        public void GetEntitiesInRange(Vector2 position, float radius, List<EntitySnapshot> results)
        {
            float offsetX = (Width * CellSize) * 0.5f;
            float offsetY = (Height * CellSize) * 0.5f;

            int minX = Mathf.FloorToInt((position.x - radius + offsetX) / CellSize);
            int maxX = Mathf.FloorToInt((position.x + radius + offsetX) / CellSize);
            int minY = Mathf.FloorToInt((position.y - radius + offsetY) / CellSize);
            int maxY = Mathf.FloorToInt((position.y + radius + offsetY) / CellSize);

            float sqrRadius = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                    
                    int cellIndex = y * Width + x;
                    
                    int count = CellCounts[cellIndex];
                    var cellArray = Grid[cellIndex];

                    for (int i = 0; i < count; i++)
                    {
                        var entity = cellArray[i];
                        
                        // Prevent duplicates if an entity spans multiple cells
                        // A quick check is to ensure we haven't already added it, 
                        // but List.Contains is O(N).
                        // Since this runs on background threads, small O(N) is okay, 
                        // or we could use a HashSet, but that allocates if not pooled.
                        
                        // For now, check distance:
                        float sqrDist = (entity.Position - position).sqrMagnitude;
                        
                        // We account for the entity's radius in the overlap
                        float totalRadius = radius + entity.Radius;

                        if (sqrDist <= totalRadius * totalRadius)
                        {
                            // Avoid duplicates (since entity could be in multiple cells)
                            bool exists = false;
                            for (int r = 0; r < results.Count; r++)
                            {
                                if (results[r].Id == entity.Id)
                                {
                                    exists = true;
                                    break;
                                }
                            }

                            if (!exists)
                            {
                                results.Add(entity);
                            }
                        }
                    }
                }
            }
        }
    }
}
