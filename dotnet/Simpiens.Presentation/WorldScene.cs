using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// What the top-down view shows this frame, as a sorted <see cref="DrawList"/> (Epic CLIENT): the map's tiles and
    /// room names, the props (resource nodes, the hearth, bunks) and the crawlers with their animation. It reads the simulation's state
    /// and never writes it. The map's tiles are built once per map. Sizes are Unity's: crawlers 0.7 of a cell,
    /// resource nodes 0.8, the hearth and bunks 0.9.
    /// </summary>
    public sealed class WorldScene
    {
        public const float CrawlerSize = 0.7f;
        public const float ResourceSize = 0.8f;
        public const float HearthSize = 0.9f;
        public const float BunkSize = 0.9f;

        private MapTileLayer _tiles;
        private long _lastTick = long.MinValue;

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

        /// <summary>A new session or a load (the clock may have moved either way): the fog and the crawlers' poses start afresh.</summary>
        public void Reset()
        {
            _lastTick = long.MaxValue; // the next Build reloads the fog
            Appearance.Reset();
        }

        /// <summary>
        /// Fills <see cref="Items"/> and <see cref="Labels"/> for <paramref name="map"/>, its <paramref name="sectors"/>
        /// (charted or not) and <paramref name="entities"/> at <paramref name="tick"/>. <paramref name="viewSeconds"/> drives
        /// the animations and fades: real time in the game, the tick's time in tests.
        /// </summary>
        public void Build(MapDefinition map, ISectorRegistry sectors, IReadOnlyList<WorldEntity> entities, long tick, float viewSeconds, WorldSelection selection = null)
        {
            Items.Clear();
            Labels.Clear();
            if (tick < _lastTick) Fog.Load(map, sectors); // the clock went back: a respawn or a load, same map or not
            _lastTick = tick;
            Fog.Update(map, sectors, viewSeconds);
            _art = Projection.ArtPixelsPerUnit;
            for (int r = 0; r < _layers.Length; r++) _layers[r] = Projection.LayerOf((DrawRole)r);
            if (map != null)
            {
                if (_tiles == null || !ReferenceEquals(_tiles.Map, map)) _tiles = MapTileLayer.Build(map);
                AddTiles(_tiles);
                AddRoomNames(map);
                Fog.Draw(Items, viewSeconds, Projection);
                Fog.AddLabels(Labels, viewSeconds);
            }

            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    if (entity.Agent != null) AddCrawler(entity, tick, viewSeconds);
                    else AddProp(entity, viewSeconds);
                }
            }

            if (selection != null) AddSelection(selection);

            Items.Sort();
        }

        /// <summary>A gold ring under the selected crawler and its name above it (Unity's CameraController).</summary>
        private void AddSelection(WorldSelection selection)
        {
            if (!selection.TryGetCrawlerPosition(out var at)) return;
            Add(DrawRole.Selection, 0f, ProceduralSprites.SelectionRing.Index, at.X, at.Y, SelectionRingSize * _art, SelectionRingSize * _art, White);
            Labels.Add(new WorldLabel(selection.CrawlerName(), at + NameOffset, LabelKind.Name, NameColor));
        }

        public const float SelectionRingSize = 0.9f;
        private static readonly Vec2 NameOffset = new Vec2(0f, 0.65f);
        private static readonly Rgba32 NameColor = Rgba32.From(new Rgba(1f, 0.92f, 0.55f));

        private void AddCrawler(WorldEntity entity, long tick, float viewSeconds)
        {
            var pose = Appearance.Pose(entity.Id, entity.Agent, tick, viewSeconds);
            var position = entity.Agent.Position;
            Add(DrawRole.Crawler, Projection.Depth(position), pose.Sprite.Index, position.X, position.Y,
                CrawlerSize * pose.ScaleX * _art, CrawlerSize * pose.ScaleY * _art, White, pose.TiltDegrees * DegreesToRadians).Pick = Items.Pickable(entity.Id);
        }

        /// <summary>The props Unity drew (resource nodes, the hearth, bunks); other kinds have no picture.</summary>
        private void AddProp(WorldEntity entity, float viewSeconds)
        {
            var position = entity.Position;
            float depth = Projection.Depth(position);
            switch (entity.Type)
            {
                case EntityType.Resource:
                    Add(DrawRole.Prop, depth, ProceduralSprites.GetResourceSprite(entity.ResourceKind).Index, position.X, position.Y, ResourceSize * _art, ResourceSize * _art, White);
                    break;
                case EntityType.Anchor:
                {
                    // Two-frame flame at 6 Hz and a subtle pulse (Unity's CampfireView)
                    var sprite = ProceduralSprites.GetCampfireSprite((int)(viewSeconds * 6f) & 1);
                    float size = HearthSize * (1f + MathF.Sin(viewSeconds * 4f) * 0.03f);
                    Add(DrawRole.Prop, depth, sprite.Index, position.X, position.Y, size * _art, size * _art, White);
                    break;
                }
                case EntityType.Bunk:
                    // Under its sleeper: props are drawn before crawlers
                    Add(DrawRole.Prop, depth, ProceduralSprites.BunkSprite.Index, position.X, position.Y, BunkSize * _art, BunkSize * _art, White);
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
