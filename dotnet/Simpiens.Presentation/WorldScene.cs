using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// What the view shows this frame, as a sorted <see cref="DrawList"/> (Epic CLIENT): the map's tiles and room names,
    /// the props (resource nodes, the hearth, bunks) and the crawlers with their animation. What stands in an uncharted
    /// sector is hidden by its fog and fades in with it (<see cref="FogOfWar.Visibility"/>). It reads the view's
    /// frames (<see cref="WorldViewFrames"/>, review A3), never the live simulation. The map's tiles are built once per
    /// map. What each thing looks like (sprite, size, pivot, frames) comes from the view mode's art catalogue
    /// (<see cref="Catalog"/>, graphics V2), and which art a thing has, from its visuals (<see cref="EntityVisuals"/>);
    /// where it goes and in what order, from the projection.
    /// </summary>
    /// <summary>How the walls in front of rooms are drawn (M3 design D7, producer P2: cutaway).</summary>
    public enum WallStyle : byte
    {
        /// <summary>A wall stands full height only where no floor lies behind it: the far walls stand, every nearer one is a stub (The Sims' cutaway).</summary>
        Cutaway = 0,
        /// <summary>Every wall full height: what is behind a wall is hidden by it.</summary>
        Full = 1,
        /// <summary>Every wall a stub.</summary>
        Low = 2,
    }

    public sealed class WorldScene
    {
        private MapTileLayer _tiles;
        private uint _epoch; // the world the fog and the poses were loaded for (review A5)

        public DrawList Items { get; } = new DrawList();

        /// <summary>How the view projects the world: it gives each item its depth and layer.</summary>
        public IViewProjection Projection { get; set; } = TopDownProjection.Instance;

        /// <summary>What things look like in this view, as data: their sprites, frames, sizes and pivots. Setting it resolves the scene's art ids.</summary>
        public ArtCatalog Catalog
        {
            get => _catalog;
            set
            {
                _catalog = value ?? throw new ArgumentNullException(nameof(value));
                _floor = value.Id(ArtKeys.TileFloor);
                _rock = value.Id(ArtKeys.TileRock);
                _wall = value.Id(ArtKeys.TileWall);
                _lowWall = value.Id(ArtKeys.TileWallLow);
                _ring = value.Id(ArtKeys.SelectionRing);
                _shadow = value.TryId(ArtKeys.CrawlerShadow, out var shadow) ? shadow : ArtId.None;
                Visuals = new EntityVisuals(value);
            }
        }

        /// <summary>Which art each entity of the frames is drawn with, in <see cref="Catalog"/>.</summary>
        public EntityVisuals Visuals { get; private set; }

        private ArtCatalog _catalog;
        private ArtId _floor, _rock, _wall, _lowWall, _ring, _shadow;

        public WorldScene() => Catalog = TopDownArtTable.Catalog;

        /// <summary>How walls in front of rooms are drawn; only a catalogue with a stub (isometric) shows a difference.</summary>
        public WallStyle WallStyle { get; set; } = WallStyle.Cutaway;

        /// <summary>The projection and the art catalogue together (<see cref="ViewMode"/>).</summary>
        public ViewMode Mode
        {
            set
            {
                Projection = value.Projection;
                Catalog = value.Catalog;
            }
        }

        /// <summary>The map's text this frame: room names, "UNCHARTED", the selected crawler's name (laid out on screen by <see cref="MapLabelLayout"/>).</summary>
        public List<WorldLabel> Labels { get; } = new List<WorldLabel>(16);

        /// <summary>The tiles of the map last built, or null before the first build.</summary>
        public MapTileLayer Tiles => _tiles;

        /// <summary>Each crawler's displayed state across frames.</summary>
        public CrawlerAppearance Appearance { get; } = new CrawlerAppearance();

        /// <summary>The fog over uncharted sectors, and its fades.</summary>
        public FogOfWar Fog { get; } = new FogOfWar();

        /// <summary>
        /// The view's state step, once per update (review A6): a new world reloads the fog and forgets the crawlers'
        /// poses; a newly charted sector starts its fade, a finished one is removed; each crawler's shown state moves on
        /// to the frame's tick. <paramref name="viewSeconds"/> drives the fades: real time in the game, the tick's time in tests.
        /// </summary>
        public void Advance(MapDefinition map, WorldViewFrames frames, float viewSeconds)
        {
            var frame = frames?.Current;
            // A new world (a respawn, a switch or a load: frames.Epoch): the fog and the crawlers' poses start afresh.
            // Without frames there is no world to key on, so the fog is loaded for the map every step.
            uint epoch = frames != null ? frames.Epoch : 0;
            if (epoch == 0 || epoch != _epoch)
            {
                Fog.Load(map, frame);
                Appearance.Reset();
                _epoch = epoch;
            }
            Fog.Update(frame, viewSeconds);
            if (frame == null) return;
            int crawlers = 0;
            for (int i = 0; i < frame.Count; i++)
            {
                ref readonly var entity = ref frame[i];
                if (!entity.IsCrawler) continue;
                Appearance.Advance(entity.Id, entity.VisualState, entity.Flags, frame.Tick); // its dwell runs under the fog too
                crawlers++;
            }
            Appearance.ForgetAbsent(frame.Tick, crawlers);
        }

        /// <summary>
        /// Fills <see cref="Items"/> and <see cref="Labels"/> for <paramref name="map"/> and what <paramref name="frames"/>
        /// captured of the world (its entities and which sectors are charted), from the state <see cref="Advance"/> left.
        /// Changes no view state, so a draw, a capture or a second view at any size give the same picture (review A6).
        /// <paramref name="viewSeconds"/> drives the animations and fades. With <paramref name="cullTo"/>, what can't land in
        /// that view's viewport is dropped before sorting (<see cref="DrawList.Cull"/>).
        /// </summary>
        public void Present(MapDefinition map, WorldViewFrames frames, float viewSeconds, WorldSelection selection = null, in View cullTo = default)
        {
            Items.Clear();
            Labels.Clear();
            var frame = frames?.Current;
            for (int r = 0; r < _layers.Length; r++) _layers[r] = Projection.LayerOf((DrawRole)r);
            if (map != null)
            {
                if (_tiles == null || !ReferenceEquals(_tiles.Map, map)) _tiles = MapTileLayer.Build(map); // derived from the map alone
                AddTiles(_tiles);
                AddRoomNames(map);
                Fog.Draw(Items, viewSeconds, Projection);
                Fog.AddLabels(Labels, viewSeconds);
                AddHazardNotes(map);
            }

            if (frame != null)
            {
                for (int i = 0; i < frame.Count; i++)
                {
                    ref readonly var entity = ref frame[i];
                    if (entity.IsCrawler) AddCrawler(entity, frames.DrawnPosition(i), viewSeconds);
                    else AddProp(entity, viewSeconds);
                }
                for (int i = 0; i < frame.StallCount; i++) AddStall(frame.Stall(i), viewSeconds);
            }

            if (selection != null) AddSelection(selection, viewSeconds);

            if (cullTo.Projection != null) Items.Cull(cullTo);
            Items.Sort();
        }

        /// <summary>A one-off picture (tests, tools): <see cref="Advance"/>, then <see cref="Present"/>.</summary>
        public void Build(MapDefinition map, WorldViewFrames frames, float viewSeconds, WorldSelection selection = null)
        {
            Advance(map, frames, viewSeconds);
            Present(map, frames, viewSeconds, selection);
        }

        /// <summary>A gold ring under the selected crawler and its name above it (Unity's CameraController), hidden with the crawler in the fog.</summary>
        private void AddSelection(WorldSelection selection, float viewSeconds)
        {
            if (!selection.TryGetCrawlerPosition(out var at)) return;
            float shown = Fog.Visibility(at, viewSeconds);
            if (shown <= 0f) return;
            Add(DrawRole.Selection, 0f, _catalog.Piece(_ring, viewSeconds), at, Shown(White, shown));
            Labels.Add(new WorldLabel(selection.CrawlerName(), at + _catalog.NameOffset, LabelKind.Name, Shown(NameColor, shown), _catalog.NameHeight));
        }

        /// <summary><paramref name="colour"/> with its alpha scaled by how much shows through the fog.</summary>
        private static Rgba32 Shown(Rgba32 colour, float shown)
            => shown >= 1f ? colour : new Rgba32(colour.R, colour.G, colour.B, (byte)(colour.A * shown));

        private static readonly Rgba32 NameColor = Rgba32.From(new Rgba(1f, 0.92f, 0.55f));

        private void AddCrawler(in EntityView entity, Vec2 position, float viewSeconds)
        {
            var pose = Appearance.Pose(entity.Id, entity.VisualState, entity.Flags, viewSeconds);
            float shown = Fog.Visibility(position, viewSeconds);
            if (shown <= 0f) return;
            if (!_shadow.IsNone) Add(DrawRole.Shadow, 0f, _catalog.Piece(_shadow, viewSeconds), position, Shown(White, shown));
            // Wearing its gear (graphics V5): a helmet is the crawler's own variant
            var art = _catalog.Piece(Visuals.Crawler(pose.State, entity.Worn), pose.Frame, pose.ScaleX, pose.ScaleY);
            float depth = Projection.Depth(position);
            int pick = Items.Pickable(entity.Id);
            Add(DrawRole.Crawler, depth, art, position, Shown(White, shown), pose.TiltDegrees * DegreesToRadians).Pick = pick;
            if (entity.Carried == ItemType.None) return;
            // What it carries hangs over it, drawn after it at its depth; a click on it picks the carrier
            var carried = _catalog.Piece(_catalog.Picture(ItemCatalog.Get(entity.Carried)?.ArtKey, ItemPicture.Carried), viewSeconds);
            ref var bundle = ref Add(DrawRole.Crawler, depth, carried, position + _catalog.CarryOffset, Shown(White, shown));
            bundle.Z = _catalog.CarryHeight;
            bundle.Pick = pick;
        }

        /// <summary>
        /// The trade stall (graphics V5): its counter at its place, and on it, in its display slots, the goods it holds in
        /// stock, in its table's order, as the catalogue's display picture. Drawn at the stall's depth, after it, so they
        /// stand on its counter; faded with the fog like any prop.
        /// </summary>
        private void AddStall(in TradeStallData stall, float viewSeconds)
        {
            if (Visuals.Stall.IsNone || !stall.IsActive) return;
            var position = stall.Position;
            float shown = Fog.Visibility(position, viewSeconds);
            if (shown <= 0f) return;
            var tint = Shown(White, shown);
            float depth = Projection.Depth(position);
            Add(DrawRole.Prop, depth, _catalog.Piece(Visuals.Stall, viewSeconds), position, tint);
            var slots = _catalog.DisplaySlots;
            int slot = 0;
            for (int i = 0; i < TradeStallData.GoodsCapacity && slot < slots.Count; i++)
            {
                var good = stall.GoodAt(i);
                if (good.Item == ItemType.None || good.Stock == 0) continue;
                var picture = _catalog.Piece(_catalog.Picture(ItemCatalog.Get(good.Item)?.ArtKey, _catalog.DisplayPicture), viewSeconds);
                ref var shownGood = ref Add(DrawRole.Prop, depth, picture, position + slots[slot].Offset, tint);
                shownGood.Z = slots[slot].Height;
                slot++;
            }
        }

        /// <summary>The props (resource nodes, the hearth, bunks), each as its visual says; other kinds have no picture.</summary>
        private void AddProp(in EntityView entity, float viewSeconds)
        {
            var position = entity.Position;
            float shown = Fog.Visibility(position, viewSeconds);
            if (shown <= 0f || !Visuals.TryProp(entity, out var art)) return;
            Add(DrawRole.Prop, Projection.Depth(position), _catalog.Piece(art, viewSeconds), position, Shown(White, shown));
        }

        private void AddTiles(MapTileLayer tiles)
        {
            var floor = _catalog.Piece(_floor, 0f);
            var rock = _catalog.Piece(_rock, 0f);
            var wall = _catalog.Piece(_wall, 0f);
            var low = _catalog.Piece(_lowWall, 0f);
            var stands = WallsStanding(tiles);
            var list = tiles.Tiles;
            for (int i = 0; i < list.Count; i++)
            {
                var tile = list[i];
                var centre = tiles.CellCentre(tile.X, tile.Y);
                switch (tile.Kind)
                {
                    case TileKind.Floor: Add(DrawRole.Floor, 0f, floor, centre, Rgba32.From(tile.Tint)); break;
                    case TileKind.Rock: Add(DrawRole.Rock, 0f, rock, centre, White); break;
                    // Walls stand: in isometric they interleave with crawlers by depth (in top-down they never overlap)
                    default: Add(DrawRole.Wall, Projection.Depth(centre), Stands(stands, i) ? wall : low, centre, White); break;
                }
            }
        }

        /// <summary>
        /// The map's room names, hung on their points; drawn in screen space (<see cref="MapLabelLayout"/>). Each carries
        /// the sector it stands in (the first that contains it, in map order), which pairs it with that sector's
        /// "UNCHARTED".
        /// </summary>
        /// <summary>The red a hazard sector's danger is written in.</summary>
        public static readonly Rgba HazardColor = new Rgba(0.98f, 0.45f, 0.40f, 1f);

        /// <summary>What a hazard sector's note says (Epic 10 G2).</summary>
        public static string HazardText(HazardKind kind) => kind switch
        {
            HazardKind.FoulAir => "FOUL AIR",
            HazardKind.FallingRock => "FALLING ROCK",
            _ => "DANGER",
        };

        /// <summary>
        /// Each hazard sector's danger under its room's name; none while the fog covers the sector, whose "UNCHARTED"
        /// takes that line (the fog hides what an uncharted sector holds).
        /// </summary>
        private void AddHazardNotes(MapDefinition map)
        {
            for (int s = 0; s < map.Sectors.Count; s++)
            {
                var sector = map.Sectors[s];
                if (!sector.Hazard.IsHazardous || Fog.Covers(sector.Id)) continue;
                var at = sector.Bounds.Center;
                for (int i = 0; i < map.Labels.Count; i++)
                {
                    if (sector.Contains(map.Labels[i].Position)) { at = map.Labels[i].Position; break; }
                }
                Labels.Add(new WorldLabel(HazardText(sector.Hazard.Kind), at, LabelKind.Hazard, Rgba32.From(HazardColor), sectorId: sector.Id));
            }
        }

        private void AddRoomNames(MapDefinition map)
        {
            if (!ReferenceEquals(_labelSectorsMap, map))
            {
                _labelSectors = new int[map.Labels.Count];
                for (int i = 0; i < map.Labels.Count; i++)
                {
                    _labelSectors[i] = -1;
                    for (int s = 0; s < map.Sectors.Count; s++)
                    {
                        if (!map.Sectors[s].Contains(map.Labels[i].Position)) continue;
                        _labelSectors[i] = map.Sectors[s].Id;
                        break;
                    }
                }
                _labelSectorsMap = map; // derived from the map alone
            }
            for (int i = 0; i < map.Labels.Count; i++)
            {
                var label = map.Labels[i];
                Labels.Add(new WorldLabel(label.Text, label.Position, LabelKind.Room, Rgba32.From(label.Color), sectorId: _labelSectors[i]));
            }
        }

        private int[] _labelSectors;
        private MapDefinition _labelSectorsMap;

        private bool Stands(bool[] cutaway, int tile) => WallStyle == WallStyle.Full || (WallStyle == WallStyle.Cutaway && cutaway[tile]);

        private bool[] _standing; // per tile: a wall with no floor behind it, for the cutaway
        private MapTileLayer _standingTiles;
        private IViewProjection _standingProjection;

        /// <summary>
        /// For each tile, whether a wall there has no floor behind it: none of its eight neighbours further from the viewer
        /// (by the projection's depth) is floor. The diagonals count, or a wall meeting the front wall would stand as a lone
        /// post. Derived from the map and the projection alone, so it is kept until either changes.
        /// </summary>
        private bool[] WallsStanding(MapTileLayer tiles)
        {
            if (ReferenceEquals(tiles, _standingTiles) && ReferenceEquals(Projection, _standingProjection)) return _standing;
            var list = tiles.Tiles;
            var standing = new bool[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var tile = list[i];
                if (tile.Kind != TileKind.Wall) continue;
                float depth = Projection.Depth(tiles.CellCentre(tile.X, tile.Y));
                bool behind = false;
                for (int dy = -1; dy <= 1 && !behind; dy++)
                    for (int dx = -1; dx <= 1 && !behind; dx++)
                        behind = (dx != 0 || dy != 0) && FloorBehind(tiles, tile.X + dx, tile.Y + dy, depth);
                standing[i] = !behind;
            }
            _standing = standing;
            _standingTiles = tiles;
            _standingProjection = Projection;
            return standing;
        }

        private bool FloorBehind(MapTileLayer tiles, int x, int y, float depth) => tiles.IsFloor(x, y) && Projection.Depth(tiles.CellCentre(x, y)) < depth;

        private readonly int[] _layers = new int[(int)DrawRole.Shadow + 1]; // each role's layer, this build

        /// <summary>Adds <paramref name="piece"/> standing at <paramref name="at"/> as an item of <paramref name="role"/>, in the layer the projection gives that role.</summary>
        private ref DrawItem Add(DrawRole role, float depth, in ArtPiece piece, Vec2 at, Rgba32 tint, float rotation = 0f)
        {
            ref var item = ref Items.Add(_layers[(int)role], role, depth, piece.Sprite, at.X, at.Y, piece.Width, piece.Height, tint, rotation, piece.Sheet);
            item.PivotX = piece.PivotX;
            item.PivotY = piece.PivotY;
            item.SnapToPixel = role is DrawRole.Prop or DrawRole.Crawler or DrawRole.Shadow or DrawRole.Selection; // standing pieces (F19)
            return ref item;
        }

        private const float DegreesToRadians = MathF.PI / 180f;
        private static readonly Rgba32 White = new Rgba32(255, 255, 255, 255);
    }
}
