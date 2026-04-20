using System.Collections.Concurrent;
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
        SharedWorldSnapshot GetActiveSnapshot();
    }

    public class SpatialHashGrid : ISpatialPartition
    {
        private float _cellSize;
        private int _width;
        private int _height;
        private int _maxEntitiesPerCell = 16;
        
        // Use a thread-safe pool because cognitive tasks (on background threads) 
        // will be releasing snapshots back to this pool concurrently.
        private ConcurrentQueue<SharedWorldSnapshot> _snapshotPool = new ConcurrentQueue<SharedWorldSnapshot>();
        
        private SharedWorldSnapshot _currentTickSnapshot;

        public void Initialize(float worldWidth, float worldHeight, float cellSize)
        {
            _cellSize = cellSize;
            _width = Mathf.CeilToInt(worldWidth / cellSize);
            _height = Mathf.CeilToInt(worldHeight / cellSize);

            // Pre-warm the pool with a few snapshots
            for (int i = 0; i < 3; i++)
            {
                _snapshotPool.Enqueue(CreateSnapshot());
            }
        }

        private SharedWorldSnapshot CreateSnapshot()
        {
            return new SharedWorldSnapshot(_width, _height, _cellSize, _maxEntitiesPerCell, ReturnSnapshotToPool);
        }

        private void ReturnSnapshotToPool(SharedWorldSnapshot snapshot)
        {
            _snapshotPool.Enqueue(snapshot);
        }

        public void UpdateFromRegistry(IWorldRegistry registry)
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

            // Populate the new snapshot
            var nodes = registry.ActiveNodes;
            int count = nodes.Count;

            // Assuming world origin (0,0) is bottom-left for grid calculations, 
            // or we shift positions if the world is centered at (0,0).
            // Let's assume the world is centered at (0,0) and spans from -half to +half.
            float offsetX = (_width * _cellSize) * 0.5f;
            float offsetY = (_height * _cellSize) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var node = nodes[i];
                var pos = node.Position;
                var radius = node.Radius;

                var snapshot = new EntitySnapshot(node.Id, pos, node.Velocity, radius);

                // Find cells overlapped by this entity
                int minX = Mathf.Max(0, Mathf.FloorToInt((pos.x - radius + offsetX) / _cellSize));
                int maxX = Mathf.Min(_width - 1, Mathf.FloorToInt((pos.x + radius + offsetX) / _cellSize));
                int minY = Mathf.Max(0, Mathf.FloorToInt((pos.y - radius + offsetY) / _cellSize));
                int maxY = Mathf.Min(_height - 1, Mathf.FloorToInt((pos.y + radius + offsetY) / _cellSize));

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        _currentTickSnapshot.AddEntity(x, y, in snapshot);
                    }
                }
            }
        }

        public SharedWorldSnapshot GetActiveSnapshot()
        {
            // Returns the current snapshot for any cognitive agents asking this frame.
            return _currentTickSnapshot;
        }
    }
}
