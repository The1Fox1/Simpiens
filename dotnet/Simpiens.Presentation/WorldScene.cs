using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// What the top-down view shows this frame, as a sorted <see cref="DrawList"/> (Epic CLIENT M0): the map's tiles and
    /// the crawlers. It reads the simulation's state and never writes it. The map's tiles are built once per map.
    /// </summary>
    public sealed class WorldScene
    {
        /// <summary>A crawler's sprite size in world units (Unity drew crawlers at 0.7 scale).</summary>
        public const float CrawlerSize = 0.7f;

        private MapTileLayer _tiles;

        public DrawList Items { get; } = new DrawList();

        /// <summary>The tiles of the map last built, or null before the first build.</summary>
        public MapTileLayer Tiles => _tiles;

        /// <summary>Fills <see cref="Items"/> for <paramref name="map"/> and <paramref name="entities"/> at <paramref name="tick"/>.</summary>
        public void Build(MapDefinition map, IReadOnlyList<WorldEntity> entities, long tick)
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
                    var agent = entities[i].Agent;
                    if (agent == null) continue;
                    var sprite = ProceduralSprites.GetAgentSprite(agent.VisualState, AnimationFrame(agent.VisualState, tick));
                    var position = agent.Position;
                    Items.Add(RenderLayer.Crawlers, TopDownCamera.Depth(position), sprite.Index, position.X, position.Y, CrawlerSize, CrawlerSize, White);
                }
            }

            Items.Sort();
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

        /// <summary>
        /// The sprite frame (0 or 1) from the simulation's tick, at Unity's animation speeds (frames per second: 8
        /// panicking, 1.5 asleep, 4 otherwise), so a frame drawn at a given tick is always the same.
        /// </summary>
        public static int AnimationFrame(AgentVisualState state, long tick)
        {
            float framesPerSecond = state == AgentVisualState.Panicking ? 8f : state == AgentVisualState.Sleeping ? 1.5f : 4f;
            return (int)(tick * SimulationClock.FixedTickDeltaTime * framesPerSecond) & 1;
        }

        private static readonly Rgba32 White = new Rgba32(255, 255, 255, 255);
    }
}
