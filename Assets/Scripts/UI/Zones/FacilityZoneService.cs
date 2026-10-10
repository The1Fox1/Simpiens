using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.UI.Zones
{
    /// <summary>
    /// Pure C# implementation of IFacilityZoneService.
    /// Maps 2D coordinates to facility zones with zero GC allocations in hot paths.
    /// </summary>
    public class FacilityZoneService : IFacilityZoneService
    {
        private readonly List<FacilityZone> _zones = new List<FacilityZone>(16);
        private readonly HashSet<int> _exploredZoneIds = new HashSet<int>();
        private FacilityZone _transitZone;

        // Outpost Commons (open yard north of the demo pens, §3.1 live supply loop)
        public const int OutpostMarketZoneId = 11;
        public const int OreRidgeZoneId = 12;
        public const int HerbHollowZoneId = 13;

        public FacilityZoneService()
        {
            // Core outpost sectors start explored; outer hazard/expedition sectors start shrouded
            _exploredZoneIds.Add(0); // Transit
            _exploredZoneIds.Add(1); // Perimeter West
            _exploredZoneIds.Add(2); // Berry Orchard
            _exploredZoneIds.Add(3); // Central Corridor / Market
            _exploredZoneIds.Add(4); // Common Hearth
            _exploredZoneIds.Add(5); // East Quarters
            _exploredZoneIds.Add(OutpostMarketZoneId); // Outpost Commons market; Ore Ridge & Herb Hollow start shrouded

            _transitZone = new FacilityZone(
                zoneId: 0,
                name: "Transit / Open Yard",
                description: "Exterior grounds, open corridors, and transit routes.",
                bounds: new Rect(-50f, -50f, 100f, 100f),
                isExplored: true
            );

            // Register standard sectors matching the demonstration playpens and anchors (Row 1: Epics 7 & Beta)
            _zones.Add(new FacilityZone(
                zoneId: 1,
                name: "Perimeter West",
                description: "Security perimeter, outer observation post, and quarantine gate.",
                bounds: new Rect(-10f, -3.5f, 4f, 7f),
                anchorPosition: new Vector2(-8f, 0f),
                anchorRadius: 2.5f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 2,
                name: "Berry Orchard",
                description: "Hydroponics foraging rows and biological resource cultivation.",
                bounds: new Rect(-6f, -3.5f, 4f, 7f),
                anchorPosition: new Vector2(-4f, 0f),
                anchorRadius: 2.5f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 3,
                name: "Central Corridor",
                description: "High-traffic central nexus connecting east and west wings.",
                bounds: new Rect(-2f, -3.5f, 4f, 7f),
                anchorPosition: new Vector2(0f, 0f),
                anchorRadius: 2.5f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 4,
                name: "Common Hearth",
                description: "Campfire circle, relaxation lounge, and communal gossip site.",
                bounds: new Rect(2f, -3.5f, 4f, 7f),
                anchorPosition: new Vector2(4f, 0f),
                anchorRadius: 3.0f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 5,
                name: "East Quarters",
                description: "Living bunks, private retreat chambers, and resting quarters.",
                bounds: new Rect(6f, -3.5f, 4f, 7f),
                anchorPosition: new Vector2(8f, 0f),
                anchorRadius: 3.0f
            ));

            // Register new sectors matching the demonstration playpens and anchors (Row 2: Epic 8)
            _zones.Add(new FacilityZone(
                zoneId: 6,
                name: "Directive Sector",
                description: "Collective rallying point, military standard, and discipline drills.",
                bounds: new Rect(-10f, -12.5f, 4f, 7f),
                anchorPosition: new Vector2(-8f, -9f),
                anchorRadius: 2.0f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 7,
                name: "Defiance Sector",
                description: "Starvation defiance, passive desertion, and grievance post.",
                bounds: new Rect(-6f, -12.5f, 4f, 7f),
                anchorPosition: new Vector2(-4f, -9f),
                anchorRadius: 2.0f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 8,
                name: "Emergence Hearth",
                description: "Organic hearth emergence, campfire consensus, and settlement founding.",
                bounds: new Rect(-2f, -12.5f, 4f, 7f),
                anchorPosition: new Vector2(0f, -9f),
                anchorRadius: 2.0f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 9,
                name: "Succession Arena",
                description: "Leadership succession contest, dominance trial, and power transition.",
                bounds: new Rect(2f, -12.5f, 4f, 7f),
                anchorPosition: new Vector2(4f, -9f),
                anchorRadius: 2.0f
            ));

            _zones.Add(new FacilityZone(
                zoneId: 10,
                name: "Schism Ward",
                description: "Crisis of legitimacy, factional fracture, and splinter bands.",
                bounds: new Rect(6f, -12.5f, 4f, 7f),
                anchorPosition: new Vector2(8f, -9f),
                anchorRadius: 2.0f
            ));

            // Row 3: Outpost Commons — the live supply loop arena
            _zones.Add(new FacilityZone(
                zoneId: OutpostMarketZoneId,
                name: "Outpost Market",
                description: "Trade stall, bounty board, and the commons where crawlers gather between runs.",
                bounds: new Rect(-3f, 5f, 6f, 10f),
                anchorPosition: new Vector2(0f, 9f),
                anchorRadius: 3.0f
            ));

            _zones.Add(new FacilityZone(
                zoneId: OreRidgeZoneId,
                name: "Ore Ridge",
                description: "Exposed iron veins along the western ridge; regrow slowly after mining.",
                bounds: new Rect(-12f, 5f, 7f, 10f),
                anchorPosition: new Vector2(-8.5f, 10f),
                anchorRadius: 2.5f
            ));

            _zones.Add(new FacilityZone(
                zoneId: HerbHollowZoneId,
                name: "Herb Hollow",
                description: "Sheltered eastern hollow where medicinal herbs grow back between harvests.",
                bounds: new Rect(5f, 5f, 7f, 10f),
                anchorPosition: new Vector2(8.5f, 10f),
                anchorRadius: 2.5f
            ));

            for (int i = 0; i < _zones.Count; i++)
            {
                bool exp = _exploredZoneIds.Contains(_zones[i].ZoneId);
                _zones[i] = _zones[i].WithExplored(exp);
            }
        }

        public IReadOnlyList<FacilityZone> GetAllZones() => _zones;

        public FacilityZone GetZoneById(int zoneId)
        {
            int count = _zones.Count;
            for (int i = 0; i < count; i++)
            {
                if (_zones[i].ZoneId == zoneId) return _zones[i];
            }
            return _transitZone;
        }

        public bool TryGetZone(int zoneId, out FacilityZone zone)
        {
            if (zoneId == _transitZone.ZoneId)
            {
                zone = _transitZone;
                return true;
            }

            int count = _zones.Count;
            for (int i = 0; i < count; i++)
            {
                if (_zones[i].ZoneId == zoneId)
                {
                    zone = _zones[i];
                    return true;
                }
            }

            zone = default;
            return false;
        }

        public FacilityZone GetZoneForPosition(Vector2 worldPos)
        {
            int count = _zones.Count;
            for (int i = 0; i < count; i++)
            {
                if (_zones[i].Contains(worldPos))
                {
                    return _zones[i];
                }
            }
            return _transitZone;
        }

        public int GetOccupancyCount(int zoneId, IWorldRegistry registry)
        {
            if (registry == null) return 0;

            var zone = GetZoneById(zoneId);
            var nodes = registry.ActiveNodes;
            int total = nodes.Count;
            int occupancy = 0;

            for (int i = 0; i < total; i++)
            {
                var node = nodes[i];
                if (node != null && node.Type == EntityType.Pawn)
                {
                    if (zone.Contains(node.Position))
                    {
                        occupancy++;
                    }
                }
            }

            return occupancy;
        }

        public void GetAgentsInZone(int zoneId, IWorldRegistry registry, List<NodeController> resultsBuffer)
        {
            if (resultsBuffer == null) return;
            resultsBuffer.Clear();

            if (registry == null) return;

            var zone = GetZoneById(zoneId);
            var nodes = registry.ActiveNodes;
            int total = nodes.Count;

            for (int i = 0; i < total; i++)
            {
                var node = nodes[i];
                if (node != null && node.Type == EntityType.Pawn)
                {
                    if (zone.Contains(node.Position))
                    {
                        resultsBuffer.Add(node);
                    }
                }
            }
        }

        public bool IsSectorExplored(int zoneId)
        {
            return _exploredZoneIds.Contains(zoneId);
        }

        public void SetSectorExplored(int zoneId, bool isExplored)
        {
            if (isExplored)
            {
                _exploredZoneIds.Add(zoneId);
            }
            else
            {
                _exploredZoneIds.Remove(zoneId);
            }

            if (_transitZone.ZoneId == zoneId)
            {
                _transitZone = _transitZone.WithExplored(isExplored);
            }

            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].ZoneId == zoneId)
                {
                    _zones[i] = _zones[i].WithExplored(isExplored);
                    break;
                }
            }
        }

        public void SetAllSectorsExplored(bool isExplored)
        {
            if (isExplored)
            {
                _exploredZoneIds.Add(_transitZone.ZoneId);
                _transitZone = _transitZone.WithExplored(true);

                for (int i = 0; i < _zones.Count; i++)
                {
                    _exploredZoneIds.Add(_zones[i].ZoneId);
                    _zones[i] = _zones[i].WithExplored(true);
                }
            }
            else
            {
                _exploredZoneIds.Clear();
                _transitZone = _transitZone.WithExplored(false);

                for (int i = 0; i < _zones.Count; i++)
                {
                    _zones[i] = _zones[i].WithExplored(false);
                }
            }
        }
    }
}
