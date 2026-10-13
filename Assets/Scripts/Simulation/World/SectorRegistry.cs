using System.Collections.Generic;
using UnityEngine;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Authoritative sector lookup and exploration state for the loaded map. Explored state is simulation state:
    /// written on the main thread only (scout fulfilment, player commands) and reset whenever a map is loaded.
    /// </summary>
    public interface ISectorRegistry
    {
        /// <summary>The loaded map, or null before the first load.</summary>
        MapDefinition Map { get; }

        IReadOnlyList<SectorDefinition> All { get; }
        SectorDefinition Fallback { get; }

        /// <summary>Sector containing the point (first match in definition order), otherwise the fallback.</summary>
        SectorDefinition GetSectorAt(Vector2 position);

        /// <summary>Looks up a sector by id; id 0 is the fallback sector.</summary>
        bool TryGetSector(int sectorId, out SectorDefinition sector);

        bool IsExplored(int sectorId);
        void SetExplored(int sectorId, bool explored);

        /// <summary>Bit i set when sector i (ids below 64) is explored: what cognition sees as charted.</summary>
        ulong ExploredMask { get; }
        void SetAllExplored(bool explored);

        /// <summary>Replaces the sector table with the map's and resets exploration to each sector's default.</summary>
        void Load(MapDefinition map);
    }

    public sealed class SectorRegistry : ISectorRegistry
    {
        private static readonly SectorDefinition[] NoSectors = new SectorDefinition[0];

        private readonly bool[] _explored = new bool[256];
        private MapDefinition _map;
        private SectorDefinition _fallback = new SectorDefinition(0, "Transit / Open Yard", "Exterior grounds, open corridors, and transit routes.", new Rect(-50f, -50f, 100f, 100f));

        public SectorRegistry()
        {
            _explored[0] = true;
        }

        public MapDefinition Map => _map;

        public ulong ExploredMask
        {
            get
            {
                ulong mask = 0;
                for (int i = 0; i < 64; i++)
                {
                    if (_explored[i]) mask |= 1UL << i;
                }
                return mask;
            }
        }
        public IReadOnlyList<SectorDefinition> All => _map != null ? _map.Sectors : NoSectors;
        public SectorDefinition Fallback => _fallback;

        public void Load(MapDefinition map)
        {
            _map = map;
            System.Array.Clear(_explored, 0, _explored.Length);
            if (map == null)
            {
                _explored[0] = true;
                return;
            }

            _fallback = map.FallbackSector;
            _explored[_fallback.Id] = _fallback.StartsExplored;
            var sectors = map.Sectors;
            for (int i = 0; i < sectors.Count; i++)
            {
                _explored[sectors[i].Id] = sectors[i].StartsExplored;
            }
        }

        public SectorDefinition GetSectorAt(Vector2 position)
        {
            return _map != null ? _map.FindSector(position) : _fallback;
        }

        public bool TryGetSector(int sectorId, out SectorDefinition sector)
        {
            if (sectorId == _fallback.Id)
            {
                sector = _fallback;
                return true;
            }
            if (_map != null && sectorId >= 0 && sectorId <= byte.MaxValue)
            {
                return _map.TryGetSector((byte)sectorId, out sector);
            }
            sector = default;
            return false;
        }

        public bool IsExplored(int sectorId)
        {
            return sectorId >= 0 && sectorId < _explored.Length && _explored[sectorId];
        }

        public void SetExplored(int sectorId, bool explored)
        {
            if (sectorId < 0 || sectorId >= _explored.Length) return;
            _explored[sectorId] = explored;
        }

        public void SetAllExplored(bool explored)
        {
            if (!explored)
            {
                System.Array.Clear(_explored, 0, _explored.Length);
                return;
            }
            _explored[_fallback.Id] = true;
            var sectors = All;
            for (int i = 0; i < sectors.Count; i++)
            {
                _explored[sectors[i].Id] = true;
            }
        }
    }
}
