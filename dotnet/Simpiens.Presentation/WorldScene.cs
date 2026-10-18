using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// What the top-down view shows this frame, as a sorted <see cref="DrawList"/> (Epic CLIENT): the map's tiles, the
    /// props (resource nodes, the hearth, bunks) and the crawlers with their animation. It reads the simulation's state
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

        public DrawList Items { get; } = new DrawList();

        /// <summary>The tiles of the map last built, or null before the first build.</summary>
        public MapTileLayer Tiles => _tiles;

        /// <summary>Each crawler's displayed state across frames.</summary>
        public CrawlerAppearance Appearance { get; } = new CrawlerAppearance();

        /// <summary>
        /// Fills <see cref="Items"/> for <paramref name="map"/> and <paramref name="entities"/> at <paramref name="tick"/>.
        /// <paramref name="viewSeconds"/> drives the animations: real time in the game, the tick's time in tests.
        /// </summary>
        public void Build(MapDefinition map, IReadOnlyList<WorldEntity> entities, long tick, float viewSeconds)
        {
            Items.Clear();
            if (map != null)
            {
                if (_tiles == null || !ReferenceEquals(_tiles.Map, map)) _tiles = MapTileLayer.Build(map);
                AddTiles(_tiles);
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

            Items.Sort();
        }

        private void AddCrawler(WorldEntity entity, long tick, float viewSeconds)
        {
            var pose = Appearance.Pose(entity.Id, entity.Agent, tick, viewSeconds);
            var position = entity.Agent.Position;
            Items.Add(RenderLayer.Crawlers, TopDownCamera.Depth(position), pose.Sprite.Index, position.X, position.Y,
                CrawlerSize * pose.ScaleX, CrawlerSize * pose.ScaleY, White, pose.TiltDegrees * DegreesToRadians);
        }

        /// <summary>The props Unity drew (resource nodes, the hearth, bunks); other kinds have no picture.</summary>
        private void AddProp(WorldEntity entity, float viewSeconds)
        {
            var position = entity.Position;
            float depth = TopDownCamera.Depth(position);
            switch (entity.Type)
            {
                case EntityType.Resource:
                    Items.Add(RenderLayer.Props, depth, ProceduralSprites.GetResourceSprite(entity.ResourceKind).Index, position.X, position.Y, ResourceSize, ResourceSize, White);
                    break;
                case EntityType.Anchor:
                {
                    // Two-frame flame at 6 Hz and a subtle pulse (Unity's CampfireView)
                    var sprite = ProceduralSprites.GetCampfireSprite((int)(viewSeconds * 6f) & 1);
                    float size = HearthSize * (1f + MathF.Sin(viewSeconds * 4f) * 0.03f);
                    Items.Add(RenderLayer.Props, depth, sprite.Index, position.X, position.Y, size, size, White);
                    break;
                }
                case EntityType.Bunk:
                    // Under its sleeper: props are drawn before crawlers
                    Items.Add(RenderLayer.Props, depth, ProceduralSprites.BunkSprite.Index, position.X, position.Y, BunkSize, BunkSize, White);
                    break;
            }
        }

        private void AddTiles(MapTileLayer tiles)
        {
            int floor = ProceduralSprites.FloorSprite.Index;
            int rock = ProceduralSprites.RockSprite.Index;
            int wall = ProceduralSprites.WallSprite.Index;
            var list = tiles.Tiles;
            for (int i = 0; i < list.Count; i++)
            {
                var tile = list[i];
                var centre = tiles.CellCentre(tile.X, tile.Y);
                switch (tile.Kind)
                {
                    case TileKind.Floor: Items.Add(RenderLayer.Floor, 0f, floor, centre.X, centre.Y, 1f, 1f, Rgba32.From(tile.Tint)); break;
                    case TileKind.Rock: Items.Add(RenderLayer.Rock, 0f, rock, centre.X, centre.Y, 1f, 1f, White); break;
                    default: Items.Add(RenderLayer.Walls, 0f, wall, centre.X, centre.Y, 1f, 1f, White); break;
                }
            }
        }

        private const float DegreesToRadians = MathF.PI / 180f;
        private static readonly Rgba32 White = new Rgba32(255, 255, 255, 255);
    }
}
