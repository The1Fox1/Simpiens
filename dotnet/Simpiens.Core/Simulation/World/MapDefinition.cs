using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.World
{
    public enum PoiKind : byte
    {
        Stall,
        Hearth,
        Bunk,
        SpawnPoint,
        /// <summary>A bunk in the guild's barracks, kept for a member (Epic 10 Part 2, W5.2, D62).</summary>
        GuildBunk,
        /// <summary>The sealed descent gate (Epic 10 Part 3, K10.1, Q13): drawn by the view, sealed or breached by the world's flag.</summary>
        DescentGate
    }

    /// <summary>What makes a sector dangerous (Epic 10 G2): the environment, never creatures (producer decision Q2).</summary>
    public enum HazardKind : byte
    {
        None = 0,
        FoulAir = 1,
        FallingRock = 2,
        /// <summary>The Drowned Gallery's flooded air (Epic 10 G5, D11).</summary>
        Flooding = 3
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

        /// <summary>
        /// The player's facility (Epic 10 Part 2, D7): the workshop row, the guildhall. Nothing in it is found by looking,
        /// so no crawler explores it; artisans and members go there with work to do.
        /// </summary>
        public readonly bool Facility;

        public SectorDefinition(byte id, string name, string description, RectF bounds, Vec2 anchor = default, float anchorRadius = 0f, bool startsExplored = true, SectorHazard hazard = default, bool facility = false)
        {
            Hazard = hazard;
            Facility = facility;
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

        /// <summary>
        /// The loot table a harvest will roll (item system I7, a foundation): 0, the node yields <see cref="Item"/> as
        /// today. No node sets one yet.
        /// </summary>
        public readonly ushort LootTableId;

        public ResourceNodeDefinition(string key, Vec2 position, ItemType item, int yield, uint regrowTicks, ushort lootTableId = 0)
        {
            Key = key;
            Position = position;
            Item = item;
            Yield = yield;
            RegrowTicks = regrowTicks;
            LootTableId = lootTableId;
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

    /// <summary>
    /// A station's place in the outpost (Epic 10 Part 2, W0.4): a forge or a still the player may build there. The map
    /// fixes where stations stand (Q2, Q10); whether one is built, who works it and what it makes is the session's state.
    /// A station holds no stock of its own (D3): it works from the stall's.
    /// </summary>
    public readonly struct StationDefinition
    {
        public readonly string Key;
        public readonly Simpiens.Simulation.Items.StationKind Kind;
        public readonly Vec2 Position;

        /// <summary>How near an artisan stands to work it.</summary>
        public readonly float Radius;

        public StationDefinition(string key, Simpiens.Simulation.Items.StationKind kind, Vec2 position, float radius)
        {
            Key = key;
            Kind = kind;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>Who may spend free time at a venue (Epic LIFE-1 plan 2, L2.3, D13).</summary>
    public enum VenueAccess : byte
    {
        Anyone = 0,
        /// <summary>The guild's members only: the guildhall's yard.</summary>
        GuildMembers = 1,
    }

    /// <summary>
    /// A place to spend free time (Epic LIFE-1 plan 2, L2.3, D13; the epic's A6, "venues are map data", after The Sims' smart
    /// objects: what a place offers lives in the place): its key, sector, centre and radius, its seats (where a crawler stands
    /// or sits at an activity; free when no one is within <see cref="SeatRadius"/> of it), its hours (a <c>ShiftBlocks</c>
    /// mask), the activities it offers (a bit per <c>Activity</c> id), who may come, and the station it needs built, if any.
    /// No venue stands in a hazard sector or a lair's reach (<c>VenueValidator</c>).
    /// </summary>
    public readonly struct VenueDefinition
    {
        /// <summary>How near another crawler makes a seat taken.</summary>
        public const float SeatRadius = 0.4f;

        public readonly string Key;
        public readonly byte SectorId;
        public readonly Vec2 Position;
        public readonly float Radius;
        public readonly IReadOnlyList<Vec2> Seats;

        /// <summary>The blocks of the day it is open (<c>ShiftBlocks</c>: bit 0 is 06:00–10:00; all six for a place always open).</summary>
        public readonly byte Hours;

        /// <summary>The activities it offers: bit i for activity id i.</summary>
        public readonly uint Activities;

        public readonly VenueAccess Access;

        /// <summary>The key of the station it needs built (the drill yard for the guildhall's yard; the tavern's counter); null for none.</summary>
        public readonly string Station;

        public VenueDefinition(string key, byte sectorId, Vec2 position, float radius, IReadOnlyList<Vec2> seats, byte hours, uint activities, VenueAccess access, string station)
        {
            Key = key;
            SectorId = sectorId;
            Position = position;
            Radius = radius;
            Seats = seats ?? System.Array.Empty<Vec2>();
            Hours = hours;
            Activities = activities;
            Access = access;
            Station = station;
        }

        /// <summary>Whether it offers <paramref name="activity"/>.</summary>
        public bool Offers(Simpiens.Simulation.Life.Activity activity) => (byte)activity < 32 && (Activities & (1u << (byte)activity)) != 0;
    }

    /// <summary>
    /// A creatures' lair (Epic 10 Part 3, K2.1; Q5: "lairs are map data"): where a pack of one kind lives, rests and heals.
    /// Its pack is drawn from the session's seed and a slain member returns there (<see cref="Simpiens.Simulation.Creatures.CreatureService"/>).
    /// </summary>
    public readonly struct LairDefinition
    {
        public readonly string Key;

        /// <summary>The kind its pack is of (its brood, under a boss).</summary>
        public readonly Simpiens.Simulation.Creatures.CreatureKind Kind;

        /// <summary>The lair's heart: its pack rests around it.</summary>
        public readonly Vec2 Position;

        /// <summary>
        /// A boss that heads the pack (Epic 10 Part 3, K10, Q13: the Matriarch heads the warren), holding the lair's first
        /// place at its heart, the brood in the places after; None for a lair without one. A boss alone (the Gatekeeper) is a
        /// lair of its own kind instead.
        /// </summary>
        public readonly Simpiens.Simulation.Creatures.CreatureKind Boss;

        /// <summary>
        /// The lair guards the descent gate (K10, Q13): it is charted with the sector it stands in (the gate is the room's
        /// landmark), and emptied for good, it breaches the gate.
        /// </summary>
        public readonly bool GuardsGate;

        public LairDefinition(string key, Simpiens.Simulation.Creatures.CreatureKind kind, Vec2 position,
            Simpiens.Simulation.Creatures.CreatureKind boss = Simpiens.Simulation.Creatures.CreatureKind.None, bool guardsGate = false)
        {
            Key = key;
            Kind = kind;
            Position = position;
            Boss = boss;
            GuardsGate = guardsGate;
        }

        /// <summary>True for a lair headed by a boss.</summary>
        public bool HasBoss => Boss != Simpiens.Simulation.Creatures.CreatureKind.None;

        /// <summary>The kind that holds <paramref name="slot"/>: the boss in the first place of a lair with one, else its pack's kind.</summary>
        public Simpiens.Simulation.Creatures.CreatureKind KindIn(int slot) => slot == 0 && HasBoss ? Boss : Kind;
    }

    /// <summary>
    /// A building (Epic PLACES, A1, plan 1's D2): a walled structure inside one zone, standing from the start or, of kind None, an
    /// empty lot. Its footprint is its walls' centre lines, on the map's wall grid; its doors are gaps in its edges, each
    /// <see cref="DoorSpan"/> between the wall cells either side, as the map's doors are. Its walls are written with the zone's
    /// (<see cref="MapDefinitionBuilder.Building"/>), once: a wall the map already has isn't added again.
    /// </summary>
    public readonly struct BuildingDefinition
    {
        /// <summary>A door's span: from the wall cell on one side to the one on the other, centre to centre. The cells between are open.</summary>
        public const float DoorSpan = 6f;

        public readonly string Key;
        public readonly Simpiens.Simulation.Places.BuildingKind Kind;

        /// <summary>The zone it stands in, wholly.</summary>
        public readonly byte SectorId;

        /// <summary>Its walls' centre lines.</summary>
        public readonly RectF Footprint;

        /// <summary>Its doors' centres, each on an edge of its footprint; the first is its main door, its sign beside it.</summary>
        public readonly IReadOnlyList<Vec2> Doors;

        /// <summary>Where its sign hangs: on the wall line beside its main door.</summary>
        public readonly Vec2 Sign;

        /// <summary>The wall cells it added to the map's: none for a building standing on a zone's walls (the bunkhouse).</summary>
        public readonly int WallCellsAdded;

        public BuildingDefinition(string key, Simpiens.Simulation.Places.BuildingKind kind, byte sectorId, RectF footprint, IReadOnlyList<Vec2> doors, Vec2 sign, int wallCellsAdded)
        {
            Key = key;
            Kind = kind;
            SectorId = sectorId;
            Footprint = footprint;
            Doors = doors ?? System.Array.Empty<Vec2>();
            Sign = sign;
            WallCellsAdded = wallCellsAdded;
        }

        /// <summary>An empty lot: foundations, no building yet (D5).</summary>
        public bool IsLot => Kind == Simpiens.Simulation.Places.BuildingKind.None;

        /// <summary>Its row in the building table.</summary>
        public Simpiens.Simulation.Places.BuildingKindDefinition Row => Simpiens.Simulation.Places.BuildingTable.Get(Kind);

        /// <summary>The lot its footprint makes.</summary>
        public Simpiens.Simulation.Places.LotSize Size => Simpiens.Simulation.Places.BuildingTable.SizeOf(Footprint);

        /// <summary>Whether <paramref name="point"/> is inside it: within its walls, not on them nor in a doorway.</summary>
        public bool Contains(Vec2 point)
        {
            const float wall = MapDefinitionBuilder.WallHalfExtent;
            return point.X > Footprint.XMin + wall && point.X < Footprint.XMax - wall && point.Y > Footprint.YMin + wall && point.Y < Footprint.YMax - wall;
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

        /// <summary>Where stations may be built, in definition order (Epic 10 Part 2).</summary>
        public readonly IReadOnlyList<StationDefinition> Stations;

        public readonly IReadOnlyList<MapLabel> Labels;

        /// <summary>The creatures' lairs, in definition order (Epic 10 Part 3, K2.1). Empty on a map with none.</summary>
        public readonly IReadOnlyList<LairDefinition> Lairs;

        /// <summary>The places to spend free time, in definition order (Epic LIFE-1 plan 2, L2.3, D13); at most 32. Empty on a map with none.</summary>
        public readonly IReadOnlyList<VenueDefinition> Venues;

        /// <summary>The most venues a map holds: the snapshot's open mask is 32 bits.</summary>
        public const int MaxVenues = 32;

        /// <summary>The buildings and lots, in definition order (Epic PLACES plan 1, B.1); at most <see cref="MaxBuildings"/>. Empty on a map with none.</summary>
        public readonly IReadOnlyList<BuildingDefinition> Buildings;

        /// <summary>The most buildings a map holds, as venues: a mask of them fits 32 bits.</summary>
        public const int MaxBuildings = 32;

        /// <summary>
        /// The trade caravan's way in (Epic 10 Part 2, the stretch, D70): from where the road meets the map to the stall, in
        /// order; it walks back out the same way. Empty on a map the caravan doesn't walk.
        /// </summary>
        public readonly IReadOnlyList<Vec2> CaravanRoute;

        internal MapDefinition(
            string name,
            RectF playableBounds,
            RectF cameraFrame,
            WallRect[] walls,
            SectorDefinition[] sectors,
            SectorDefinition fallbackSector,
            ResourceNodeDefinition[] resourceNodes,
            PoiDefinition[] pois,
            StationDefinition[] stations,
            MapLabel[] labels,
            Vec2[] caravanRoute = null,
            LairDefinition[] lairs = null,
            VenueDefinition[] venues = null,
            BuildingDefinition[] buildings = null)
        {
            Buildings = buildings ?? System.Array.Empty<BuildingDefinition>();
            Venues = venues ?? System.Array.Empty<VenueDefinition>();
            CaravanRoute = caravanRoute ?? System.Array.Empty<Vec2>();
            Lairs = lairs ?? System.Array.Empty<LairDefinition>();
            Name = name;
            PlayableBounds = playableBounds;
            CameraFrame = cameraFrame;
            Walls = walls;
            Sectors = sectors;
            FallbackSector = fallbackSector;
            ResourceNodes = resourceNodes;
            Pois = pois;
            Stations = stations;
            Labels = labels;
        }

        /// <summary>
        /// The map's wall grid (Epic PLACES plan 1, B.2): the fractional part, 0 or 0.5 on each axis, of its first wall cell's
        /// centre. A map's walls share one alignment (Floor 1's on whole units; the Dev Map's on half units in y); its tiles line
        /// up with it, and its buildings' footprints and doors lie on it.
        /// </summary>
        public Vec2 WallAlignment => AlignmentOf(Walls);

        /// <summary>The wall grid of <paramref name="walls"/>: (0, 0) with none.</summary>
        public static Vec2 AlignmentOf(IReadOnlyList<WallRect> walls)
        {
            if (walls == null || walls.Count == 0) return Vec2.Zero;
            var solid = walls[0].Solid;
            float unit = 2f * MapDefinitionBuilder.WallHalfExtent + 0.01f;
            float inset = solid.Width > unit && solid.Height > unit ? 0.5f : MapDefinitionBuilder.WallHalfExtent; // rock, or a wall run
            return new Vec2(Fraction(solid.XMin + inset), Fraction(solid.YMin + inset));
        }

        private static float Fraction(float v)
        {
            float f = v - SimMath.Floor(v);
            return SimMath.Abs(f - 0.5f) < 0.01f ? 0.5f : 0f;
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

        /// <summary>The index of the building <paramref name="position"/> is inside (within its walls, not on them), or -1 (Epic PLACES, A2, plan 1's D8).</summary>
        public int BuildingAt(Vec2 position)
        {
            for (int i = 0; i < Buildings.Count; i++)
            {
                if (Buildings[i].Contains(position)) return i;
            }
            return -1;
        }

        public bool TryGetBuilding(string key, out BuildingDefinition building)
        {
            for (int i = 0; i < Buildings.Count; i++)
            {
                if (Buildings[i].Key == key)
                {
                    building = Buildings[i];
                    return true;
                }
            }
            building = default;
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

        public bool TryGetLair(string key, out LairDefinition lair)
        {
            for (int i = 0; i < Lairs.Count; i++)
            {
                if (Lairs[i].Key == key)
                {
                    lair = Lairs[i];
                    return true;
                }
            }
            lair = default;
            return false;
        }

        public bool TryGetStation(string key, out StationDefinition station)
        {
            for (int i = 0; i < Stations.Count; i++)
            {
                if (Stations[i].Key == key)
                {
                    station = Stations[i];
                    return true;
                }
            }
            station = default;
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
