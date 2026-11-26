using System;
using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Fluent authoring helper for <see cref="MapDefinition"/>. Build() checks the cheap structural invariants;
    /// connectivity and walkability are checked by the map validators against a rasterized grid.
    /// </summary>
    public sealed class MapDefinitionBuilder
    {
        /// <summary>
        /// Half thickness of a unit wall cell. Matches the legacy wall-node collider radius (0.55), so a wall run
        /// rasterizes to exactly the cells the old per-cell wall entities blocked.
        /// </summary>
        public const float WallHalfExtent = 0.55f;

        private readonly string _name;
        private RectF _playableBounds;
        private RectF _cameraFrame;
        private SectorDefinition _fallback = new SectorDefinition(0, "Transit / Open Yard", "Exterior grounds, open corridors, and transit routes.", new RectF(-50f, -50f, 100f, 100f));
        private readonly List<WallRect> _walls = new List<WallRect>();
        private readonly List<SectorDefinition> _sectors = new List<SectorDefinition>();
        private readonly List<ResourceNodeDefinition> _nodes = new List<ResourceNodeDefinition>();
        private readonly List<PoiDefinition> _pois = new List<PoiDefinition>();
        private readonly List<StationDefinition> _stations = new List<StationDefinition>();
        private readonly List<LairDefinition> _lairs = new List<LairDefinition>();
        private readonly List<(string Key, Vec2 Position, float Radius, Vec2[] Seats, byte Hours, uint Activities, VenueAccess Access, string Station)> _venues
            = new List<(string, Vec2, float, Vec2[], byte, uint, VenueAccess, string)>();
        private readonly List<MapLabel> _labels = new List<MapLabel>();
        private readonly List<(string Key, Simpiens.Simulation.Places.BuildingKind Kind, RectF Footprint, Vec2[] Doors)> _buildings
            = new List<(string, Simpiens.Simulation.Places.BuildingKind, RectF, Vec2[])>();
        private Vec2[] _caravanRoute;

        public MapDefinitionBuilder(string name, RectF playableBounds)
        {
            _name = name;
            _playableBounds = playableBounds;
            _cameraFrame = playableBounds;
        }

        public MapDefinitionBuilder CameraFrame(RectF frame)
        {
            _cameraFrame = frame;
            return this;
        }

        public MapDefinitionBuilder Fallback(string name, string description, bool startsExplored = true)
        {
            _fallback = new SectorDefinition(0, name, description, _fallback.Bounds, default, 0f, startsExplored);
            return this;
        }

        /// <summary>Solid wall rect in world units.</summary>
        public MapDefinitionBuilder Wall(RectF solid)
        {
            _walls.Add(new WallRect(solid));
            return this;
        }

        /// <summary>
        /// An axis-aligned run of unit wall cells centred from <paramref name="from"/> to <paramref name="to"/> (inclusive).
        /// </summary>
        public MapDefinitionBuilder WallRun(Vec2 from, Vec2 to)
        {
            if (!SimMath.Approximately(from.X, to.X) && !SimMath.Approximately(from.Y, to.Y))
            {
                throw new ArgumentException($"Wall run {from} -> {to} in map '{_name}' is not axis-aligned.");
            }
            float minX = SimMath.Min(from.X, to.X) - WallHalfExtent;
            float maxX = SimMath.Max(from.X, to.X) + WallHalfExtent;
            float minY = SimMath.Min(from.Y, to.Y) - WallHalfExtent;
            float maxY = SimMath.Max(from.Y, to.Y) + WallHalfExtent;
            _walls.Add(new WallRect(RectF.MinMaxRect(minX, minY, maxX, maxY)));
            return this;
        }

        public MapDefinitionBuilder Sector(byte id, string name, string description, RectF bounds, Vec2 anchor = default, float anchorRadius = 0f, bool startsExplored = true, SectorHazard hazard = default, bool facility = false)
        {
            _sectors.Add(new SectorDefinition(id, name, description, bounds, anchor, anchorRadius, startsExplored, hazard, facility));
            return this;
        }

        public MapDefinitionBuilder Node(string key, Vec2 position, ItemType item, int yield, uint regrowTicks)
        {
            _nodes.Add(new ResourceNodeDefinition(key, position, item, yield, regrowTicks));
            return this;
        }

        public MapDefinitionBuilder Poi(string key, PoiKind kind, Vec2 position, float radius = 0f)
        {
            _pois.Add(new PoiDefinition(key, kind, position, radius));
            return this;
        }

        /// <summary>A place where the player may build a station (Epic 10 Part 2).</summary>
        public MapDefinitionBuilder Station(string key, Simpiens.Simulation.Items.StationKind kind, Vec2 position, float radius)
        {
            _stations.Add(new StationDefinition(key, kind, position, radius));
            return this;
        }

        /// <summary>
        /// A lair of <paramref name="kind"/> (Epic 10 Part 3, K2.1): its pack is drawn from the session's seed. With a
        /// <paramref name="boss"/> at its head (K10), or guarding the descent gate.
        /// </summary>
        public MapDefinitionBuilder Lair(string key, Simpiens.Simulation.Creatures.CreatureKind kind, Vec2 position,
            Simpiens.Simulation.Creatures.CreatureKind boss = Simpiens.Simulation.Creatures.CreatureKind.None, bool guardsGate = false)
        {
            _lairs.Add(new LairDefinition(key, kind, position, boss, guardsGate));
            return this;
        }

        public MapDefinitionBuilder Label(string text, Vec2 position, Rgba color)
        {
            _labels.Add(new MapLabel(text, position, color));
            return this;
        }

        /// <summary>The trade caravan's way in, from where the road meets the map to the stall (Epic 10 Part 2, D70).</summary>
        public MapDefinitionBuilder CaravanRoute(params Vec2[] waypoints)
        {
            if (waypoints == null || waypoints.Length < 2) throw new InvalidOperationException($"Map '{_name}': a caravan route needs two waypoints at least.");
            _caravanRoute = (Vec2[])waypoints.Clone();
            return this;
        }

        /// <summary>
        /// A place to spend free time (Epic LIFE-1 plan 2, L2.3, D13): its centre and radius, its seats, the activities it
        /// offers, its hours (every block unless given), who may come, and the key of a station it needs built. Its sector is
        /// the one its centre lies in.
        /// </summary>
        public MapDefinitionBuilder Venue(string key, Vec2 position, float radius, Vec2[] seats, Simpiens.Simulation.Life.Activity[] offers,
            byte hours = 0b111111, VenueAccess access = VenueAccess.Anyone, string station = null)
        {
            uint activities = 0;
            if (offers != null) foreach (var activity in offers) activities |= 1u << (byte)activity;
            _venues.Add((key, position, radius, seats ?? System.Array.Empty<Vec2>(), hours, activities, access, station));
            return this;
        }

        /// <summary>
        /// A building standing from the start (Epic PLACES plan 1, B.1, D2–D3): its kind, its footprint (its walls' centre lines, on
        /// the map's wall grid (<see cref="MapDefinition.WallAlignment"/>), wholly inside one zone) and its doors (centres on its edges; the first is its main door). Its walls are
        /// written in <see cref="Build"/> after every other wall, unit cell by cell, less its doors and any cell a wall already covers.
        /// </summary>
        public MapDefinitionBuilder Building(string key, Simpiens.Simulation.Places.BuildingKind kind, RectF footprint, params Vec2[] doors)
        {
            _buildings.Add((key, kind, footprint, doors ?? System.Array.Empty<Vec2>()));
            return this;
        }

        /// <summary>An empty lot (D5): a building of kind None, its walls drawn as foundations.</summary>
        public MapDefinitionBuilder Lot(string key, RectF footprint, params Vec2[] doors) => Building(key, Simpiens.Simulation.Places.BuildingKind.None, footprint, doors);

        public MapDefinition Build()
        {
            if (string.IsNullOrEmpty(_name)) throw new InvalidOperationException("Map definition needs a name.");
            if (_playableBounds.Width <= 0f || _playableBounds.Height <= 0f)
            {
                throw new InvalidOperationException($"Map '{_name}' has empty playable bounds {_playableBounds}.");
            }

            var ids = new HashSet<byte>();
            for (int i = 0; i < _sectors.Count; i++)
            {
                byte id = _sectors[i].Id;
                if (id == _fallback.Id) throw new InvalidOperationException($"Map '{_name}': sector id {id} is reserved for the fallback sector.");
                if (!ids.Add(id)) throw new InvalidOperationException($"Map '{_name}': duplicate sector id {id}.");
            }

            var keys = new HashSet<string>();
            for (int i = 0; i < _nodes.Count; i++)
            {
                RequireKey(keys, _nodes[i].Key);
                RequireInside(_nodes[i].Position, _nodes[i].Key);
            }
            for (int i = 0; i < _pois.Count; i++)
            {
                RequireKey(keys, _pois[i].Key);
                RequireInside(_pois[i].Position, _pois[i].Key);
            }
            for (int i = 0; i < _stations.Count; i++)
            {
                RequireKey(keys, _stations[i].Key);
                RequireInside(_stations[i].Position, _stations[i].Key);
                if (_stations[i].Kind == Simpiens.Simulation.Items.StationKind.None) throw new InvalidOperationException($"Map '{_name}': station '{_stations[i].Key}' has no kind.");
            }
            for (int i = 0; i < _lairs.Count; i++)
            {
                RequireKey(keys, _lairs[i].Key);
                RequireInside(_lairs[i].Position, _lairs[i].Key);
                if (_lairs[i].Kind == Simpiens.Simulation.Creatures.CreatureKind.None) throw new InvalidOperationException($"Map '{_name}': lair '{_lairs[i].Key}' has no kind.");
            }

            if (_venues.Count > MapDefinition.MaxVenues) throw new InvalidOperationException($"Map '{_name}': {_venues.Count} venues; a map holds at most {MapDefinition.MaxVenues}.");
            var venues = new VenueDefinition[_venues.Count];
            for (int i = 0; i < _venues.Count; i++)
            {
                var v = _venues[i];
                RequireKey(keys, v.Key);
                RequireInside(v.Position, v.Key);
                foreach (var seat in v.Seats) RequireInside(seat, v.Key + " seat");
                if (v.Activities == 0) throw new InvalidOperationException($"Map '{_name}': venue '{v.Key}' offers nothing.");
                if (v.Hours == 0) throw new InvalidOperationException($"Map '{_name}': venue '{v.Key}' is never open.");
                if (v.Station != null && !_stations.Exists(s => s.Key == v.Station)) throw new InvalidOperationException($"Map '{_name}': venue '{v.Key}' needs station '{v.Station}', which the map hasn't.");
                byte sector = 0;
                foreach (var s in _sectors) if (s.Bounds.Contains(v.Position)) { sector = s.Id; break; }
                if (sector == 0) throw new InvalidOperationException($"Map '{_name}': venue '{v.Key}' lies in no sector.");
                venues[i] = new VenueDefinition(v.Key, sector, v.Position, v.Radius, v.Seats, v.Hours, v.Activities, v.Access, v.Station);
            }

            var walls = new List<WallRect>(_walls);
            var buildings = BuildBuildings(keys, walls);

            return new MapDefinition(
                _name,
                _playableBounds,
                _cameraFrame,
                walls.ToArray(),
                _sectors.ToArray(),
                _fallback,
                _nodes.ToArray(),
                _pois.ToArray(),
                _stations.ToArray(),
                _labels.ToArray(),
                _caravanRoute,
                _lairs.ToArray(),
                venues,
                buildings);
        }

        // --- Buildings (Epic PLACES plan 1, B.1) ---

        private BuildingDefinition[] BuildBuildings(HashSet<string> keys, List<WallRect> walls)
        {
            if (_buildings.Count > MapDefinition.MaxBuildings) throw new InvalidOperationException($"Map '{_name}': {_buildings.Count} buildings; a map holds at most {MapDefinition.MaxBuildings}.");
            var buildings = new BuildingDefinition[_buildings.Count];
            var grid = _walls.Count > 0 ? MapDefinition.AlignmentOf(_walls) : _buildings.Count > 0 ? new Vec2(Fraction(_buildings[0].Footprint.XMin), Fraction(_buildings[0].Footprint.YMin)) : Vec2.Zero;
            for (int i = 0; i < _buildings.Count; i++)
            {
                var (key, kind, footprint, doors) = _buildings[i];
                RequireKey(keys, key);
                RequireInside(footprint.Center, key);
                if (!IsWhole(footprint.XMin - grid.X) || !IsWhole(footprint.YMin - grid.Y) || !IsWhole(footprint.Width) || !IsWhole(footprint.Height))
                    throw new InvalidOperationException($"Map '{_name}': building '{key}' has a footprint {footprint} off the map's wall grid.");
                if (footprint.Width < 4f || footprint.Height < 4f) throw new InvalidOperationException($"Map '{_name}': building '{key}' is too small ({footprint}).");
                var row = Simpiens.Simulation.Places.BuildingTable.Get(kind);
                if (row.Kind != kind) throw new InvalidOperationException($"Map '{_name}': building '{key}' is of kind {kind}, which has no row.");
                if (Simpiens.Simulation.Places.BuildingTable.SizeOf(footprint) < row.Size)
                    throw new InvalidOperationException($"Map '{_name}': building '{key}', a {row.Word}, needs a {row.Size} lot; its footprint makes a {Simpiens.Simulation.Places.BuildingTable.SizeOf(footprint)} one.");

                byte sector = 0;
                foreach (var s in _sectors)
                {
                    if (footprint.XMin >= s.Bounds.XMin && footprint.XMax <= s.Bounds.XMax && footprint.YMin >= s.Bounds.YMin && footprint.YMax <= s.Bounds.YMax)
                    {
                        sector = s.Id;
                        break;
                    }
                }
                if (sector == 0) throw new InvalidOperationException($"Map '{_name}': building '{key}' doesn't lie wholly inside one zone.");

                for (int j = 0; j < i; j++)
                {
                    var other = _buildings[j].Footprint;
                    if (footprint.XMin < other.XMax && other.XMin < footprint.XMax && footprint.YMin < other.YMax && other.YMin < footprint.YMax)
                        throw new InvalidOperationException($"Map '{_name}': building '{key}' overlaps '{_buildings[j].Key}'.");
                }

                if (doors.Length == 0) throw new InvalidOperationException($"Map '{_name}': building '{key}' has no door.");
                for (int d = 0; d < doors.Length; d++)
                {
                    if (!OnEdge(footprint, doors[d], grid)) throw new InvalidOperationException($"Map '{_name}': building '{key}'s door at {doors[d]} isn't on its edges with room for its posts.");
                    for (int e = 0; e < d; e++)
                    {
                        if (SameEdge(footprint, doors[d], doors[e]) && SimMath.Abs(Along(footprint, doors[d]) - Along(footprint, doors[e])) < BuildingDefinition.DoorSpan)
                            throw new InvalidOperationException($"Map '{_name}': building '{key}'s doors at {doors[e]} and {doors[d]} overlap.");
                    }
                }

                int added = WriteBuildingWalls(footprint, doors, walls);
                buildings[i] = new BuildingDefinition(key, kind, sector, footprint, (Vec2[])doors.Clone(), SignBeside(footprint, doors[0]), added);
            }
            return buildings;
        }

        /// <summary>Writes a building's walls along its four edges, a unit cell at a time, less its doors' gaps and any cell a wall already covers; merges each edge's cells into runs.</summary>
        private static int WriteBuildingWalls(RectF footprint, Vec2[] doors, List<WallRect> walls)
        {
            int added = 0;
            added += WriteEdge(new Vec2(footprint.XMin, footprint.YMin), new Vec2(1f, 0f), (int)footprint.Width, footprint, doors, walls);
            added += WriteEdge(new Vec2(footprint.XMin, footprint.YMax), new Vec2(1f, 0f), (int)footprint.Width, footprint, doors, walls);
            added += WriteEdge(new Vec2(footprint.XMin, footprint.YMin), new Vec2(0f, 1f), (int)footprint.Height, footprint, doors, walls);
            added += WriteEdge(new Vec2(footprint.XMax, footprint.YMin), new Vec2(0f, 1f), (int)footprint.Height, footprint, doors, walls);
            return added;
        }

        private static int WriteEdge(Vec2 start, Vec2 step, int length, RectF footprint, Vec2[] doors, List<WallRect> walls)
        {
            int added = 0, runFrom = -1;
            for (int t = 0; t <= length + 1; t++)
            {
                bool wall = false;
                if (t <= length)
                {
                    var cell = new Vec2(start.X + step.X * t, start.Y + step.Y * t);
                    wall = !InDoorway(footprint, doors, cell) && !Covered(walls, cell);
                }
                if (wall && runFrom < 0) runFrom = t;
                if (!wall && runFrom >= 0)
                {
                    var from = new Vec2(start.X + step.X * runFrom, start.Y + step.Y * runFrom);
                    var to = new Vec2(start.X + step.X * (t - 1), start.Y + step.Y * (t - 1));
                    walls.Add(new WallRect(RectF.MinMaxRect(SimMath.Min(from.X, to.X) - WallHalfExtent, SimMath.Min(from.Y, to.Y) - WallHalfExtent,
                        SimMath.Max(from.X, to.X) + WallHalfExtent, SimMath.Max(from.Y, to.Y) + WallHalfExtent)));
                    added += t - runFrom;
                    runFrom = -1;
                }
            }
            return added;
        }

        /// <summary>Whether a wall cell centred at <paramref name="cell"/> lies open in one of the doors' gaps (between its posts).</summary>
        private static bool InDoorway(RectF footprint, Vec2[] doors, Vec2 cell)
        {
            for (int d = 0; d < doors.Length; d++)
            {
                if (SameEdge(footprint, doors[d], cell) && SimMath.Abs(Along(footprint, cell) - Along(footprint, doors[d])) < BuildingDefinition.DoorSpan * 0.5f - 0.01f) return true;
            }
            return false;
        }

        private static bool Covered(List<WallRect> walls, Vec2 cell)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                var solid = walls[i].Solid;
                if (cell.X >= solid.XMin && cell.X <= solid.XMax && cell.Y >= solid.YMin && cell.Y <= solid.YMax) return true;
            }
            return false;
        }

        /// <summary>Whether a door centred at <paramref name="door"/> lies on an edge of the footprint, on the map's wall grid, with its posts within the edge.</summary>
        private static bool OnEdge(RectF footprint, Vec2 door, Vec2 grid)
        {
            float half = BuildingDefinition.DoorSpan * 0.5f;
            if (!IsWhole(door.X - grid.X) || !IsWhole(door.Y - grid.Y)) return false;
            bool horizontal = SimMath.Approximately(door.Y, footprint.YMin) || SimMath.Approximately(door.Y, footprint.YMax);
            bool vertical = SimMath.Approximately(door.X, footprint.XMin) || SimMath.Approximately(door.X, footprint.XMax);
            if (horizontal == vertical) return false; // on no edge, or a corner
            return horizontal
                ? door.X - half >= footprint.XMin && door.X + half <= footprint.XMax
                : door.Y - half >= footprint.YMin && door.Y + half <= footprint.YMax;
        }

        /// <summary>Which edge a point on the footprint's lines lies on: 0 and 1 the low and high horizontal edges, 2 and 3 the vertical; -1 for none.</summary>
        private static int EdgeOf(RectF footprint, Vec2 point)
        {
            if (SimMath.Approximately(point.Y, footprint.YMin)) return 0;
            if (SimMath.Approximately(point.Y, footprint.YMax)) return 1;
            if (SimMath.Approximately(point.X, footprint.XMin)) return 2;
            if (SimMath.Approximately(point.X, footprint.XMax)) return 3;
            return -1;
        }

        private static bool SameEdge(RectF footprint, Vec2 a, Vec2 b)
        {
            int edge = EdgeOf(footprint, a);
            return edge >= 0 && edge == EdgeOf(footprint, b);
        }

        /// <summary>A point's place along its edge: x on a horizontal edge, y on a vertical one.</summary>
        private static float Along(RectF footprint, Vec2 point) => EdgeOf(footprint, point) <= 1 ? point.X : point.Y;

        /// <summary>
        /// Where a building's sign stands (D10): outside its main door's wall, a cell out, a cell past the door's post toward the
        /// edge's middle. A post outside, not a plaque on the wall: the isometric cutaway lowers front walls.
        /// </summary>
        private static Vec2 SignBeside(RectF footprint, Vec2 door)
        {
            float offset = BuildingDefinition.DoorSpan * 0.5f + 1f;
            int edge = EdgeOf(footprint, door);
            if (edge <= 1)
            {
                float x = door.X <= footprint.Center.X ? door.X + offset : door.X - offset;
                return new Vec2(SimMath.Clamp(x, footprint.XMin + 1f, footprint.XMax - 1f), door.Y + (edge == 0 ? -1f : 1f));
            }
            float y = door.Y <= footprint.Center.Y ? door.Y + offset : door.Y - offset;
            return new Vec2(door.X + (edge == 2 ? -1f : 1f), SimMath.Clamp(y, footprint.YMin + 1f, footprint.YMax - 1f));
        }

        private static bool IsWhole(float value) => SimMath.Approximately(value, SimMath.Round(value));

        private static float Fraction(float v)
        {
            float f = v - SimMath.Floor(v);
            return SimMath.Abs(f - 0.5f) < 0.01f ? 0.5f : 0f;
        }

        private void RequireKey(HashSet<string> keys, string key)
        {
            if (string.IsNullOrEmpty(key)) throw new InvalidOperationException($"Map '{_name}': entry without a key.");
            if (!keys.Add(key)) throw new InvalidOperationException($"Map '{_name}': duplicate key '{key}'.");
        }

        private void RequireInside(Vec2 pos, string key)
        {
            if (!_playableBounds.Contains(pos)) throw new InvalidOperationException($"Map '{_name}': '{key}' at {pos} lies outside the playable bounds {_playableBounds}.");
        }
    }
}
