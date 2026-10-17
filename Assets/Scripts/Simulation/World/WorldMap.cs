using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using ZLogger;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Entity ids the session assigned to a loaded map's keyed entries (resource nodes, POIs), so session scripts
    /// can refer to "hub.stall" instead of capturing ids at spawn time.
    /// </summary>
    public sealed class LoadedWorld
    {
        private readonly Dictionary<string, EntityId> _ids = new Dictionary<string, EntityId>();

        public LoadedWorld(MapDefinition map)
        {
            Map = map;
        }

        public MapDefinition Map { get; }

        public void RegisterEntity(string key, EntityId id)
        {
            _ids[key] = id;
        }

        public bool TryGetEntityId(string key, out EntityId id)
        {
            return _ids.TryGetValue(key, out id);
        }

        public EntityId GetEntityId(string key)
        {
            if (_ids.TryGetValue(key, out var id)) return id;
            throw new KeyNotFoundException($"Map '{Map?.Name}' has no spawned entity with key '{key}'.");
        }

        /// <summary>Every keyed entity, in ordinal key order (a save).</summary>
        public void CaptureEntities(out string[] keys, out EntityId[] ids)
        {
            keys = new string[_ids.Count];
            _ids.Keys.CopyTo(keys, 0);
            System.Array.Sort(keys, System.StringComparer.Ordinal);
            ids = new EntityId[keys.Length];
            for (int i = 0; i < keys.Length; i++) ids[i] = _ids[keys[i]];
        }
    }

    /// <summary>The single entry point that builds a world from a map definition.</summary>
    public interface IWorldMap
    {
        /// <summary>The loaded map, or null before the first load.</summary>
        MapDefinition Current { get; }

        /// <summary>Incremented on every load (views rebuild when it changes).</summary>
        int Version { get; }

        /// <summary>
        /// Sizes the spatial partition from the map's bounds, writes its walls into the static grid, rebuilds regions
        /// and loads its sectors. Main thread only; call before spawning the map's entities.
        /// </summary>
        LoadedWorld Load(MapDefinition map);

        /// <summary>The sector registry loaded with each map (null in headless setups without one).</summary>
        ISectorRegistry Sectors { get; }
    }

    public sealed class WorldMap : IWorldMap
    {
        public const float CellSize = 1f;

        private readonly ISpatialPartition _partition;
        private readonly ISectorRegistry _sectors;

        public MapDefinition Current { get; private set; }
        public int Version { get; private set; }

        public ISectorRegistry Sectors => _sectors;

        [Inject]
        public WorldMap(ISpatialPartition partition = null, ISectorRegistry sectors = null)
        {
            _partition = partition;
            _sectors = sectors;
        }

        public LoadedWorld Load(MapDefinition map)
        {
            if (map == null) throw new System.ArgumentNullException(nameof(map));

            if (_partition != null)
            {
                var cells = map.PartitionCells(CellSize);
                bool resized = _partition.Configure(cells.x * CellSize, cells.y * CellSize, CellSize);
                var grid = _partition.StaticGrid;
                if (grid != null)
                {
                    WallRasterizer.Rasterize(map, grid);
                    grid.EnsureRegions();
                }
                _partition.SetMap(map);
                SimLog.Bootstrap.ZLogInformation($"Map '{map.Name}' loaded: {map.Walls.Count} wall rects, {map.Sectors.Count} sectors, partition {cells.x}x{cells.y}{(resized ? " (resized)" : "")}");
            }

            _sectors?.Load(map);
            Current = map;
            Version++;
            return new LoadedWorld(map);
        }
    }
}
