using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.World
{
    public enum PoiKind : byte
    {
        Stall,
        Hearth,
        Bunk,
        SpawnPoint
    }

    /// <summary>What makes a sector dangerous (Epic 10 G2): the environment, never creatures (producer decision Q2).</summary>
    public enum HazardKind : byte
    {
        None = 0,
        FoulAir = 1,
        FallingRock = 2
    }

    /// <summary>A sector's danger: every crawler standing in it loses health at this rate (G3's gear will soften it).</summary>
    public readonly struct SectorHazard
    {
        public readonly HazardKind Kind;
        public readonly float DamagePerSecond;

        public SectorHazard(HazardKind kind, float damagePerSecond)
        {
            Kind = kind;
            DamagePerSecond = damagePerSecond;
        }

        public bool IsHazardous => Kind != HazardKind.None && DamagePerSecond > 0f;
    }

    /// <summary>Axis-aligned solid wall geometry in world units.</summary>
    public readonly struct WallRect
    {
        public readonly RectF Solid;

        public WallRect(RectF solid)
        {
            Solid = solid;
        }
    }

    /// <summary>
    /// A named region of the map. Containment checks the anchor circle first, then the bounds
    /// (same rule the facility zones always used). Id 0 is reserved for the map's fallback sector.
    /// </summary>
    public readonly struct SectorDefinition
    {
        public readonly byte Id;
        public readonly string Name;
        public readonly string Description;
        public readonly RectF Bounds;
        public readonly Vec2 Anchor;
        public readonly float AnchorRadius;
        public readonly bool StartsExplored;
        public readonly SectorHazard Hazard;

        public SectorDefinition(byte id, string name, string description, RectF bounds, Vec2 anchor = default, float anchorRadius = 0f, bool startsExplored = true, SectorHazard hazard = default)
        {
            Hazard = hazard;
            Id = id;
            Name = name;
            Description = description;
            Bounds = bounds;
            Anchor = anchor;
            AnchorRadius = anchorRadius;
            StartsExplored = startsExplored;
        }

        public bool Contains(Vec2 pos)
        {
            if (AnchorRadius > 0f && Vec2.Distance(pos, Anchor) <= AnchorRadius)
            {
                return true;
            }
            return Bounds.Contains(pos);
        }

        /// <summary>Walkable target for travel into the sector: its anchor when defined, otherwise its bounds centre.</summary>
        public Vec2 TargetPoint => AnchorRadius > 0f ? Anchor : Bounds.Center;
    }

    public readonly struct ResourceNodeDefinition
    {
        public readonly string Key;
        public readonly Vec2 Position;
        public readonly ItemType Item;
        public readonly int Yield;
        public readonly uint RegrowTicks;

        public ResourceNodeDefinition(string key, Vec2 position, ItemType item, int yield, uint regrowTicks)
        {
            Key = key;
            Position = position;
            Item = item;
            Yield = yield;
            RegrowTicks = regrowTicks;
        }
    }

    /// <summary>Point of interest: stall, hearth, bunk or spawn point.</summary>
    public readonly struct PoiDefinition
    {
        public readonly string Key;
        public readonly PoiKind Kind;
        public readonly Vec2 Position;
        public readonly float Radius;

        public PoiDefinition(string key, PoiKind kind, Vec2 position, float radius)
        {
            Key = key;
            Kind = kind;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>Presentation hint: a text label drawn on the map.</summary>
    public readonly struct MapLabel
    {
        public readonly string Text;
        public readonly Vec2 Position;
        public readonly Rgba Color;

        public MapLabel(string text, Vec2 position, Rgba color)
        {
            Text = text;
            Position = position;
            Color = color;
        }
    }

    /// <summary>
    /// Plain-data description of a map's static geography: the single source of truth for the nav grid,
    /// sectors, resource nodes, points of interest and labels. Immutable once built (<see cref="MapDefinitionBuilder"/>).
    /// Coordinates are world units on an origin-centred grid.
    /// </summary>
    public sealed class MapDefinition
    {
        public readonly string Name;

        /// <summary>Cells lying entirely outside this rect are blocked.</summary>
        public readonly RectF PlayableBounds;

        /// <summary>View hint: the world rect the camera frames on load.</summary>
        public readonly RectF CameraFrame;

        public readonly IReadOnlyList<WallRect> Walls;
        public readonly IReadOnlyList<SectorDefinition> Sectors;
        public readonly SectorDefinition FallbackSector;
        public readonly IReadOnlyList<ResourceNodeDefinition> ResourceNodes;
        public readonly IReadOnlyList<PoiDefinition> Pois;
        public readonly IReadOnlyList<MapLabel> Labels;

        internal MapDefinition(
            string name,
            RectF playableBounds,
            RectF cameraFrame,
            WallRect[] walls,
            SectorDefinition[] sectors,
            SectorDefinition fallbackSector,
            ResourceNodeDefinition[] resourceNodes,
            PoiDefinition[] pois,
            MapLabel[] labels)
        {
            Name = name;
            PlayableBounds = playableBounds;
            CameraFrame = cameraFrame;
            Walls = walls;
            Sectors = sectors;
            FallbackSector = fallbackSector;
            ResourceNodes = resourceNodes;
            Pois = pois;
            Labels = labels;
        }

        /// <summary>
        /// Spatial partition size covering the playable bounds symmetrically about the origin plus a margin.
        /// Always an even number of cells per axis so cell edges stay on whole world units.
        /// </summary>
        public GridPoint PartitionCells(float cellSize, int marginCells = 2)
        {
            float halfX = SimMath.Max(SimMath.Abs(PlayableBounds.XMin), SimMath.Abs(PlayableBounds.XMax));
            float halfY = SimMath.Max(SimMath.Abs(PlayableBounds.YMin), SimMath.Abs(PlayableBounds.YMax));
            int cellsX = 2 * (SimMath.CeilToInt(halfX / cellSize) + marginCells);
            int cellsY = 2 * (SimMath.CeilToInt(halfY / cellSize) + marginCells);
            return new GridPoint(cellsX, cellsY);
        }

        /// <summary>First sector (in definition order) containing the point, otherwise the fallback sector.</summary>
        public SectorDefinition FindSector(Vec2 pos)
        {
            for (int i = 0; i < Sectors.Count; i++)
            {
                if (Sectors[i].Contains(pos)) return Sectors[i];
            }
            return FallbackSector;
        }

        public bool TryGetSector(byte id, out SectorDefinition sector)
        {
            if (id == FallbackSector.Id)
            {
                sector = FallbackSector;
                return true;
            }
            for (int i = 0; i < Sectors.Count; i++)
            {
                if (Sectors[i].Id == id)
                {
                    sector = Sectors[i];
                    return true;
                }
            }
            sector = default;
            return false;
        }

        public bool TryGetPoi(string key, out PoiDefinition poi)
        {
            for (int i = 0; i < Pois.Count; i++)
            {
                if (Pois[i].Key == key)
                {
                    poi = Pois[i];
                    return true;
                }
            }
            poi = default;
            return false;
        }

        public bool TryGetFirstPoi(PoiKind kind, out PoiDefinition poi)
        {
            for (int i = 0; i < Pois.Count; i++)
            {
                if (Pois[i].Kind == kind)
                {
                    poi = Pois[i];
                    return true;
                }
            }
            poi = default;
            return false;
        }

        public bool TryGetResourceNode(string key, out ResourceNodeDefinition node)
        {
            for (int i = 0; i < ResourceNodes.Count; i++)
            {
                if (ResourceNodes[i].Key == key)
                {
                    node = ResourceNodes[i];
                    return true;
                }
            }
            node = default;
            return false;
        }
    }
}
