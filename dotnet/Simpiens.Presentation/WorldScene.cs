using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// What the view shows this frame, as a sorted <see cref="DrawList"/> (Epic CLIENT): the map's tiles and room names,
    /// the props (resource nodes, the hearth, bunks) and the crawlers with their animation. What stands in an uncharted
    /// sector is hidden by its fog and fades in with it (<see cref="FogOfWar.Visibility"/>). It reads the view's
    /// frames (<see cref="WorldViewFrames"/>, review A3), never the live simulation. The map's tiles are built once per map. Sizes are Unity's: crawlers 0.7 of a cell,
    /// resource nodes 0.8, the hearth and bunks 0.9.
    /// </summary>
    public sealed class WorldScene
    {
        public const float CrawlerSize = 0.7f;
        public const float ResourceSize = 0.8f;
        public const float HearthSize = 0.9f;
        public const float BunkSize = 0.9f;

        private MapTileLayer _tiles;
        private uint _epoch; // the world the fog and the poses were loaded for (review A5)

        public DrawList Items { get; } = new DrawList();

        /// <summary>How the view projects the world: it gives each item its depth. Sizes here are world units, drawn at the projection's art scale.</summary>
        public IViewProjection Projection { get; set; } = TopDownProjection.Instance;

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
            for (int i = 0; i < frame.Count; i++)
            {
                ref readonly var entity = ref frame[i];
                if (entity.IsCrawler) Appearance.Advance(entity.Id, entity.VisualState, entity.Flags, frame.Tick); // its dwell runs under the fog too
            }
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
            _art = Projection.ArtPixelsPerUnit;
            for (int r = 0; r < _layers.Length; r++) _layers[r] = Projection.LayerOf((DrawRole)r);
            if (map != null)
            {
                if (_tiles == null || !ReferenceEquals(_tiles.Map, map)) _tiles = MapTileLayer.Build(map); // derived from the map alone
                AddTiles(_tiles);
                AddRoomNames(map);
                Fog.Draw(Items, viewSeconds, Projection);
                Fog.AddLabels(Labels, viewSeconds);
            }

            if (frame != null)
            {
                for (int i = 0; i < frame.Count; i++)
                {
                    ref readonly var entity = ref frame[i];
                    if (entity.IsCrawler) AddCrawler(entity, frames.DrawnPosition(i), viewSeconds);
                    else AddProp(entity, viewSeconds);
                }
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
            Add(DrawRole.Selection, 0f, ProceduralSprites.SelectionRing.Index, at.X, at.Y, SelectionRingSize * _art, SelectionRingSize * _art, Shown(White, shown));
            Labels.Add(new WorldLabel(selection.CrawlerName(), at + NameOffset, LabelKind.Name, Shown(NameColor, shown)));
        }

        /// <summary><paramref name="colour"/> with its alpha scaled by how much shows through the fog.</summary>
        private static Rgba32 Shown(Rgba32 colour, float shown)
            => shown >= 1f ? colour : new Rgba32(colour.R, colour.G, colour.B, (byte)(colour.A * shown));

        public const float SelectionRingSize = 0.9f;
        private static readonly Vec2 NameOffset = new Vec2(0f, 0.65f);
        private static readonly Rgba32 NameColor = Rgba32.From(new Rgba(1f, 0.92f, 0.55f));

        private void AddCrawler(in EntityView entity, Vec2 position, float viewSeconds)
        {
            var pose = Appearance.Pose(entity.Id, entity.VisualState, entity.Flags, viewSeconds);
            float shown = Fog.Visibility(position, viewSeconds);
            if (shown <= 0f) return;
            Add(DrawRole.Crawler, Projection.Depth(position), pose.Sprite.Index, position.X, position.Y,
                CrawlerSize * pose.ScaleX * _art, CrawlerSize * pose.ScaleY * _art, Shown(White, shown), pose.TiltDegrees * DegreesToRadians).Pick = Items.Pickable(entity.Id);
        }

        /// <summary>The props Unity drew (resource nodes, the hearth, bunks); other kinds have no picture.</summary>
        private void AddProp(in EntityView entity, float viewSeconds)
        {
            var position = entity.Position;
            float shown = Fog.Visibility(position, viewSeconds);
            if (shown <= 0f) return;
            var tint = Shown(White, shown);
            float depth = Projection.Depth(position);
            switch (entity.Type)
            {
                case EntityType.Resource:
                    Add(DrawRole.Prop, depth, ProceduralSprites.GetResourceSprite(entity.ResourceKind).Index, position.X, position.Y, ResourceSize * _art, ResourceSize * _art, tint);
                    break;
                case EntityType.Anchor:
                {
                    // Two-frame flame at 6 Hz and a subtle pulse (Unity's CampfireView)
                    var sprite = ProceduralSprites.GetCampfireSprite((int)(viewSeconds * 6f) & 1);
                    float size = HearthSize * (1f + MathF.Sin(viewSeconds * 4f) * 0.03f);
                    Add(DrawRole.Prop, depth, sprite.Index, position.X, position.Y, size * _art, size * _art, tint);
                    break;
                }
                case EntityType.Bunk:
                    // Under its sleeper: props are drawn before crawlers
                    Add(DrawRole.Prop, depth, ProceduralSprites.BunkSprite.Index, position.X, position.Y, BunkSize * _art, BunkSize * _art, tint);
                    break;
            }
        }

        private void AddTiles(MapTileLayer tiles)
        {
            int floor = ProceduralSprites.FloorSprite.Index;
            int rock = ProceduralSprites.RockSprite.Index;
            int wall = ProceduralSprites.WallSprite.Index;
            float cell = _art;
            var list = tiles.Tiles;
            for (int i = 0; i < list.Count; i++)
            {
                var tile = list[i];
                var centre = tiles.CellCentre(tile.X, tile.Y);
                switch (tile.Kind)
                {
                    case TileKind.Floor: Add(DrawRole.Floor, 0f, floor, centre.X, centre.Y, cell, cell, Rgba32.From(tile.Tint)); break;
                    case TileKind.Rock: Add(DrawRole.Rock, 0f, rock, centre.X, centre.Y, cell, cell, White); break;
                    // Walls stand: in isometric they interleave with crawlers by depth (in top-down they never overlap)
                    default: Add(DrawRole.Wall, Projection.Depth(centre), wall, centre.X, centre.Y, cell, cell, White); break;
                }
            }
        }

        /// <summary>The map's room names, centred on their positions in their colours (Unity's MapView labels).</summary>
        /// <summary>The map's room names, hung on their points; drawn in screen space (<see cref="MapLabelLayout"/>).</summary>
        private void AddRoomNames(MapDefinition map)
        {
            for (int i = 0; i < map.Labels.Count; i++)
            {
                var label = map.Labels[i];
                Labels.Add(new WorldLabel(label.Text, label.Position, LabelKind.Room, Rgba32.From(label.Color)));
            }
        }

        private float _art = TopDownProjection.BasePixelsPerUnit; // art pixels per world unit, this build
        private readonly int[] _layers = new int[(int)DrawRole.Crawler + 1]; // each role's layer, this build

        /// <summary>Adds an item of <paramref name="role"/> in the layer the projection gives that role.</summary>
        private ref DrawItem Add(DrawRole role, float depth, int sprite, float x, float y, float width, float height, Rgba32 tint, float rotation = 0f)
            => ref Items.Add(_layers[(int)role], role, depth, sprite, x, y, width, height, tint, rotation);

        private const float DegreesToRadians = MathF.PI / 180f;
        private static readonly Rgba32 White = new Rgba32(255, 255, 255, 255);
    }
}
