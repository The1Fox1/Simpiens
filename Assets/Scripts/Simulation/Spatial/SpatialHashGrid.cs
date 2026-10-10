using System.Collections.Concurrent;
using System.Collections.Generic;
using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// Maintains the spatial hash grid on the main thread and manages the pool of snapshots.
    /// </summary>
    public interface ISpatialPartition
    {
        void Initialize(float worldWidth, float worldHeight, float cellSize);
        void UpdateFromRegistry(IWorldRegistry registry);
        void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry);
        void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry, IReadOnlyList<TradeStallData> stalls);
        void UpdateFromRegistry(IWorldRegistry registry, IGroupRegistry groupRegistry, IReadOnlyList<TradeStallData> stalls, IReadOnlyList<BountyContract> bounties);
        SharedWorldSnapshot GetActiveSnapshot();
        StaticNavGrid StaticGrid { get; }
    }

    public class SpatialHashGrid : ISpatialPartition
    {
        private float _cellSize;
        private int _width;
        private int _height;
        private int _maxEntitiesPerCell = 16;
        private StaticNavGrid _staticGrid;

        public StaticNavGrid StaticGrid => _staticGrid;
        
        // Use a thread-safe pool because cognitive tasks (on background threads) 
        // will be releasing snapshots back to this pool concurrently.
        private ConcurrentQueue<SharedWorldSnapshot> _snapshotPool = new ConcurrentQueue<SharedWorldSnapshot>();
        
        private SharedWorldSnapshot _currentTickSnapshot;
        private readonly Simpiens.Cognition.Groups.GroupData[] _groupScratchBuffer = new Simpiens.Cognition.Groups.GroupData[SharedWorldSnapshot.MaxGroups];

        public void Initialize(float worldWidth, float worldHeight, float cellSize)
        {
            _cellSize = cellSize;
            _width = Mathf.CeilToInt(worldWidth / cellSize);
            _height = Mathf.CeilToInt(worldHeight / cellSize);
            _staticGrid = new StaticNavGrid(_width, _height, _cellSize);

            // Pre-warm the pool with a few snapshots
            for (int i = 0; i < 3; i++)
            {
                _snapshotPool.Enqueue(CreateSnapshot());
            }
        }

        private SharedWorldSnapshot CreateSnapshot()
        {
            var snapshot = new SharedWorldSnapshot(_width, _height, _cellSize, _maxEntitiesPerCell, ReturnSnapshotToPool);
            snapshot.SetStaticGrid(_staticGrid);
            return snapshot;
        }

        private void ReturnSnapshotToPool(SharedWorldSnapshot snapshot)
        {
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

            // Get a new snapshot from the pool
            if (!_snapshotPool.TryDequeue(out _currentTickSnapshot))
            {
                // Pool empty, create a new one. This is a fallback allocation.
                _currentTickSnapshot = CreateSnapshot();
            }

            // The main thread now "owns" one reference to it.
            _currentTickSnapshot.Retain();
            _currentTickSnapshot.Clear();

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
            var nodes = registry != null ? registry.ActiveNodes : null;
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
                float hunger = node.Agent != null ? node.Agent.Hunger : 0f;
                var snapshot = new EntitySnapshot(node.Id, pos, node.Velocity, radius, type, isCollapsed, hunger, node.ResourceKind);

                // Find cells overlapped by this entity
                int minX = Mathf.Max(0, Mathf.FloorToInt((pos.x - radius + offsetX) / _cellSize));
                int maxX = Mathf.Min(_width - 1, Mathf.FloorToInt((pos.x + radius + offsetX) / _cellSize));
                int minY = Mathf.Max(0, Mathf.FloorToInt((pos.y - radius + offsetY) / _cellSize));
                int maxY = Mathf.Min(_height - 1, Mathf.FloorToInt((pos.y + radius + offsetY) / _cellSize));

                if (type == EntityType.Wall)
                {
                    if (_staticGrid != null)
                    {
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
