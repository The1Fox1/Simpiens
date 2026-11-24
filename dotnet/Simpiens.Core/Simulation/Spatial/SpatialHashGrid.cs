using System.Collections.Concurrent;
using System.Collections.Generic;
using Simpiens.Simulation.Economy;
namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// Maintains the spatial hash grid on the main thread and manages the pool of snapshots.
    /// </summary>
    public interface ISpatialPartition
    {
        void Initialize(float worldWidth, float worldHeight, float cellSize);

        /// <summary>
        /// Sizes the partition for a (new) world. No-op returning false when the dimensions are unchanged; otherwise
        /// builds a fresh static grid and snapshot pool. Snapshots still held by background readers keep their old,
        /// consistent grid and are dropped when released. Main thread only.
        /// </summary>
        bool Configure(float worldWidth, float worldHeight, float cellSize);
        void UpdateFromRegistry(IWorldRegistry registry);
        void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry);
        void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry, IReadOnlyList<TradeStallData> stalls);
        void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry, IReadOnlyList<TradeStallData> stalls, IReadOnlyList<BountyContract> bounties);
        SharedWorldSnapshot GetActiveSnapshot();
        StaticNavGrid StaticGrid { get; }

        /// <summary>
        /// The loaded map, published by reference in every snapshot so background cognition can reason about sectors.
        /// Map definitions are immutable, so sharing them across threads is safe. Main thread only.
        /// </summary>
        void SetMap(Simpiens.Simulation.World.MapDefinition map);

        /// <summary>Which sectors are charted (bit i = sector i), published in every following snapshot. Main thread only.</summary>
        void SetExploredSectors(ulong mask);

        /// <summary>Which of the map's lairs are charted (bit i = lair i, Epic 10 Part 3, K2.3), published in every following snapshot. Main thread only.</summary>
        void SetChartedLairs(ulong mask);

        /// <summary>Which of the map's lairs have their boss alive (bit i = lair i, Epic 10 Part 3, K10.2a), published in every following snapshot. Main thread only.</summary>
        void SetBossLairs(ulong mask) { }

        /// <summary>The highest rank of a named creature in each lair, two bits a lair (Epic 11, S7.2), published in every following snapshot. Main thread only.</summary>
        void SetNamedLairs(ulong low, ulong high) { }

        /// <summary>Which of the map's stations stand built (bit i = station i, Epic 11, S3.4), published in every following snapshot. Main thread only.</summary>
        void SetBuiltStations(ulong mask) { }

        /// <summary>Which of the map's venues are open (bit i = venue i, Epic LIFE-1 plan 2, L2.3), published in every following snapshot. Main thread only.</summary>
        void SetOpenVenues(uint mask) { }

        /// <summary>The items on the ground (Epic 10 Part 3, K4.3b), copied into every following snapshot as they are then. Main thread only.</summary>
        void SetPiles(IReadOnlyList<Simpiens.Simulation.Loot.LootPile> piles);

        /// <summary>The hunts' parties (Epic 10 Part 3, K6.2), copied into every following snapshot. Main thread only.</summary>
        void SetHunts(IReadOnlyList<Simpiens.Simulation.Economy.HuntRecord> hunts);

        /// <summary>The session's economy tuning (immutable), published by reference in every snapshot. Main thread only.</summary>
        void SetEconomy(Simpiens.Simulation.Economy.EconomyTuning economy);

        /// <summary>Bunks with a sleeper, copied into every following snapshot. Main thread only.</summary>
        void SetOccupiedBunks(IReadOnlyList<EntityId> occupied);

        /// <summary>
        /// Clears all rasterized walls; registered walls are re-rasterized on the next UpdateFromRegistry.
        /// Call when the world is torn down (respawn) so stale walls don't outlive their entities.
        /// </summary>
        void ResetStatic();
    }

    public class SpatialHashGrid : ISpatialPartition
    {
        private float _cellSize;
        private int _width;
        private int _height;
        private int _maxEntitiesPerCell = 16;
        private StaticNavGrid _staticGrid;
        private int _lastWallCount;
        private bool _staticRebuildPending;
        private Simpiens.Simulation.World.MapDefinition _map;

        public StaticNavGrid StaticGrid => _staticGrid;

        public void SetMap(Simpiens.Simulation.World.MapDefinition map) => _map = map;

        private ulong _exploredSectors = ulong.MaxValue;
        public void SetExploredSectors(ulong mask) => _exploredSectors = mask;

        private ulong _chartedLairs;
        public void SetChartedLairs(ulong mask) => _chartedLairs = mask;

        private ulong _bossLairs;
        public void SetBossLairs(ulong mask) => _bossLairs = mask;

        private ulong _namedLairLow, _namedLairHigh;
        public void SetNamedLairs(ulong low, ulong high)
        {
            _namedLairLow = low;
            _namedLairHigh = high;
        }

        private ulong _builtStations;
        public void SetBuiltStations(ulong mask) => _builtStations = mask;

        private uint _openVenues;
        public void SetOpenVenues(uint mask) => _openVenues = mask;

        private IReadOnlyList<Simpiens.Simulation.Loot.LootPile> _piles;
        public void SetPiles(IReadOnlyList<Simpiens.Simulation.Loot.LootPile> piles) => _piles = piles;

        private IReadOnlyList<Simpiens.Simulation.Economy.HuntRecord> _hunts;
        public void SetHunts(IReadOnlyList<Simpiens.Simulation.Economy.HuntRecord> hunts) => _hunts = hunts;

        private Simpiens.Simulation.Economy.EconomyTuning _economy;
        public void SetEconomy(Simpiens.Simulation.Economy.EconomyTuning economy) => _economy = economy;

        private IReadOnlyList<EntityId> _occupiedBunks;
        public void SetOccupiedBunks(IReadOnlyList<EntityId> occupied) => _occupiedBunks = occupied;

        public void ResetStatic()
        {
            _staticGrid?.Clear();
            _lastWallCount = 0;
            _staticRebuildPending = false;
        }
        
        // Use a thread-safe pool because cognitive tasks (on background threads) 
        // will be releasing snapshots back to this pool concurrently.
        private ConcurrentQueue<SharedWorldSnapshot> _snapshotPool = new ConcurrentQueue<SharedWorldSnapshot>();
        
        private SharedWorldSnapshot _currentTickSnapshot;
        private readonly Simpiens.Cognition.Groups.GroupData[] _groupScratchBuffer = new Simpiens.Cognition.Groups.GroupData[SharedWorldSnapshot.MaxGroups];

        public void Initialize(float worldWidth, float worldHeight, float cellSize)
        {
            Build(worldWidth, worldHeight, cellSize);
        }

        public bool Configure(float worldWidth, float worldHeight, float cellSize)
        {
            int width = SimMath.CeilToInt(worldWidth / cellSize);
            int height = SimMath.CeilToInt(worldHeight / cellSize);
            if (_staticGrid != null && width == _width && height == _height && SimMath.Approximately(cellSize, _cellSize))
            {
                return false;
            }
            Build(worldWidth, worldHeight, cellSize);
            return true;
        }

        private void Build(float worldWidth, float worldHeight, float cellSize)
        {
            _cellSize = cellSize;
            _width = SimMath.CeilToInt(worldWidth / cellSize);
            _height = SimMath.CeilToInt(worldHeight / cellSize);
            _staticGrid = new StaticNavGrid(_width, _height, _cellSize);
            _staticGrid.SetPlayableHalfExtent(NavConstants.PlayAreaHalfExtent);
            _lastWallCount = 0;
            _staticRebuildPending = false;

            // Drop snapshots sized for a previous world; the main thread's current one is released on the next update
            while (_snapshotPool.TryDequeue(out _)) { }

            // Pre-warm the pool with a few snapshots
            for (int i = 0; i < 3; i++)
            {
                _snapshotPool.Enqueue(CreateSnapshot());
            }
        }

        private bool IsCurrent(SharedWorldSnapshot snapshot)
        {
            return ReferenceEquals(snapshot.StaticGrid, _staticGrid);
        }

        private SharedWorldSnapshot CreateSnapshot()
        {
            var snapshot = new SharedWorldSnapshot(_width, _height, _cellSize, _maxEntitiesPerCell, ReturnSnapshotToPool);
            snapshot.SetStaticGrid(_staticGrid);
            return snapshot;
        }

        private void ReturnSnapshotToPool(SharedWorldSnapshot snapshot)
        {
            // A snapshot from before the last Configure() belongs to a world that no longer exists
            if (!IsCurrent(snapshot)) return;
            _snapshotPool.Enqueue(snapshot);
        }

        public void UpdateFromRegistry(IWorldRegistry registry)
        {
            UpdateFromRegistry(registry, null, null, null);
        }

        public void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry)
        {
            UpdateFromRegistry(registry, groupRegistry, null, null);
        }

        public void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry, IReadOnlyList<TradeStallData> stalls)
        {
            UpdateFromRegistry(registry, groupRegistry, stalls, null);
        }

        public void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry, IReadOnlyList<TradeStallData> stalls, IReadOnlyList<BountyContract> bounties)
        {
            // Release the previous tick's snapshot if it was created but nobody retained it
            if (_currentTickSnapshot != null)
            {
                _currentTickSnapshot.Release();
            }

            // Get a new snapshot from the pool (skipping any stale one that raced a Configure())
            _currentTickSnapshot = null;
            while (_snapshotPool.TryDequeue(out var pooled))
            {
                if (IsCurrent(pooled))
                {
                    _currentTickSnapshot = pooled;
                    break;
                }
            }
            if (_currentTickSnapshot == null)
            {
                // Pool empty, create a new one. This is a fallback allocation.
                _currentTickSnapshot = CreateSnapshot();
            }

            // The main thread now "owns" one reference to it.
            _currentTickSnapshot.Retain();
            _currentTickSnapshot.Clear();
            _currentTickSnapshot.SetMap(_map);
            _currentTickSnapshot.SetEconomy(_economy);
            _currentTickSnapshot.SetExploredSectors(_exploredSectors);
            _currentTickSnapshot.SetChartedLairs(_chartedLairs);
            _currentTickSnapshot.SetBossLairs(_bossLairs); // K10.2a
            _currentTickSnapshot.SetNamedLairs(_namedLairLow, _namedLairHigh); // Epic 11, S7.2
            _currentTickSnapshot.SetBuiltStations(_builtStations); // Epic 11, S3.4
            _currentTickSnapshot.SetOpenVenues(_openVenues); // Epic LIFE-1 plan 2, L2.3
            _currentTickSnapshot.SetPiles(_piles); // copied now, on the main thread (K4.3b)
            _currentTickSnapshot.SetHunts(_hunts); // K6.2
            _currentTickSnapshot.SetOccupiedBunks(_occupiedBunks);

            // A wall disappeared last tick: walls are only ever added by rasterization, so rebuild from scratch
            if (_staticRebuildPending)
            {
                _staticGrid?.Clear();
                _staticRebuildPending = false;
            }
            int wallCount = 0;

            // Populate sociopolitical groups if registry is present
            if (groupRegistry != null)
            {
                groupRegistry.CopySnapshotData(_groupScratchBuffer, out int groupCount);
                _currentTickSnapshot.SetGroups(_groupScratchBuffer, groupCount);
            }

            // Populate outpost trade stalls if present
            if (stalls != null)
            {
                _currentTickSnapshot.SetStalls(stalls);
            }

            // Populate public bounty contracts if present
            if (bounties != null)
            {
                _currentTickSnapshot.SetBounties(bounties);
            }

            // Populate the new snapshot
            var nodes = registry != null ? registry.ActiveEntities : null;
            if (nodes == null) return;
            int count = nodes.Count;

            // Assuming world origin (0,0) is bottom-left for grid calculations, 
            // or we shift positions if the world is centered at (0,0).
            // Let's assume the world is centered at (0,0) and spans from -half to +half.
            float offsetX = (_width * _cellSize) * 0.5f;
            float offsetY = (_height * _cellSize) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var node = nodes[i];
                if (node == null) continue;

                var pos = node.Position;
                var radius = node.Radius;
                var type = node.Type;

                bool isCollapsed = node.Agent != null && node.Agent.IsExhaustionCollapsed;
                if (node.Agent != null && node.Agent.IsCollapsed && !node.Agent.HasLeft)
                {
                    // Collapsed in danger (Epic 10 Part 3, K5.3): its cry reaches would-be rescuers through the snapshot
                    byte party = node.Agent.Memberships.TryGet(Simpiens.Cognition.Groups.GroupType.TemporaryParty, out var membership) ? membership.GroupId : (byte)0;
                    _currentTickSnapshot.AddFallen(new FallenView(node.Id, pos, node.Agent.BleedsOutAt, node.Agent.CarriedBy, party));
                }
                float hunger = node.Agent != null ? node.Agent.Hunger : 0f;
                var snapshot = new EntitySnapshot(node.Id, pos, node.Velocity, radius, type, isCollapsed, hunger, node.ResourceKind);

                // Find cells overlapped by this entity
                int minX = SimMath.Max(0, SimMath.FloorToInt((pos.X - radius + offsetX) / _cellSize));
                int maxX = SimMath.Min(_width - 1, SimMath.FloorToInt((pos.X + radius + offsetX) / _cellSize));
                int minY = SimMath.Max(0, SimMath.FloorToInt((pos.Y - radius + offsetY) / _cellSize));
                int maxY = SimMath.Min(_height - 1, SimMath.FloorToInt((pos.Y + radius + offsetY) / _cellSize));

                if (type == EntityType.Wall)
                {
                    wallCount++;
                    if (_staticGrid != null)
                    {
                        // Inflate by the pawn body radius: a pawn centred in any walkable cell then clears the wall
                        float inflated = radius + NavConstants.PawnBodyRadius;
                        minX = SimMath.Max(0, SimMath.FloorToInt((pos.X - inflated + offsetX) / _cellSize));
                        maxX = SimMath.Min(_width - 1, SimMath.FloorToInt((pos.X + inflated + offsetX) / _cellSize));
                        minY = SimMath.Max(0, SimMath.FloorToInt((pos.Y - inflated + offsetY) / _cellSize));
                        maxY = SimMath.Min(_height - 1, SimMath.FloorToInt((pos.Y + inflated + offsetY) / _cellSize));
                        for (int y = minY; y <= maxY; y++)
                        {
                            for (int x = minX; x <= maxX; x++)
                            {
                                _staticGrid.SetWall(x, y);
                            }
                        }
                    }
                }
                else
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            _currentTickSnapshot.AddEntity(x, y, in snapshot);
                        }
                    }
                }
            }

            if (wallCount < _lastWallCount) _staticRebuildPending = true;
            _lastWallCount = wallCount;

            // Walls are rasterized above; rebuild connected regions only if walkability changed
            _staticGrid?.EnsureRegions();
        }

        // (Region labels are refreshed at the end of UpdateFromRegistry once walls are rasterized.)
        public SharedWorldSnapshot GetActiveSnapshot()
        {
            // Returns the current snapshot for any cognitive agents asking this frame.
            return _currentTickSnapshot;
        }
    }
}
