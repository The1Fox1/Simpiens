using System.Collections.Generic;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// Where and when the hazards' animations play (Epic 10 Part 2, the stretch, D69): presentation only, from the view's
    /// time, so the simulation never sees them. Each hazard sector gets spots on an even grid, two units clear of its walls
    /// and a unit clear of its nodes, each nudged and given its own phase by a hash of where it lies, so a map always
    /// looks the same at the same time. Foul air and the flood play without pause; a spot of falling rock drops a pebble
    /// once every <see cref="RockPeriodSeconds"/>.
    /// </summary>
    public sealed class HazardEffects
    {
        public const float PuffFramesPerSecond = 2f;
        public const float RockFramesPerSecond = 6f;
        public const float RippleFramesPerSecond = 2f;

        /// <summary>How often a spot of falling rock drops its pebble.</summary>
        public const float RockPeriodSeconds = 4f;

        /// <summary>The spots' spacing, and how far they keep from a sector's walls and from its nodes.</summary>
        public const float Spacing = 4f;
        public const float WallClearance = 2f;
        public const float NodeClearance = 1f;

        /// <summary>A place an animation plays.</summary>
        public readonly struct Spot
        {
            public readonly byte SectorId;
            public readonly HazardKind Kind;
            public readonly Vec2 Position;
            public readonly float Phase;

            public Spot(byte sectorId, HazardKind kind, Vec2 position, float phase)
            {
                SectorId = sectorId;
                Kind = kind;
                Position = position;
                Phase = phase;
            }
        }

        private readonly List<Spot> _spots = new List<Spot>(32);
        private MapDefinition _map;

        public IReadOnlyList<Spot> Spots => _spots;

        /// <summary>Lays out <paramref name="map"/>'s spots, unless it is the map already laid out.</summary>
        public void Load(MapDefinition map)
        {
            if (ReferenceEquals(map, _map)) return;
            _map = map;
            _spots.Clear();
            if (map == null) return;
            for (int s = 0; s < map.Sectors.Count; s++)
            {
                var sector = map.Sectors[s];
                if (!sector.Hazard.IsHazardous) continue;
                var bounds = sector.Bounds;
                float width = bounds.Width - 2f * WallClearance, height = bounds.Height - 2f * WallClearance;
                if (width <= 0f || height <= 0f) continue;
                int columns = (int)(width / Spacing) + 1, rows = (int)(height / Spacing) + 1;
                for (int row = 0; row < rows; row++)
                    for (int column = 0; column < columns; column++)
                    {
                        uint hash = Hash(sector.Id, column, row);
                        float nudgeX = ((hash & 0xFF) / 255f - 0.5f) * 0.8f, nudgeY = (((hash >> 8) & 0xFF) / 255f - 0.5f) * 0.8f;
                        var at = new Vec2(bounds.X + WallClearance + width * (column + 0.5f) / columns + nudgeX,
                                          bounds.Y + WallClearance + height * (row + 0.5f) / rows + nudgeY);
                        if (NearANode(map, at)) continue;
                        float phase = ((hash >> 16) & 0xFFFF) / 65535f * RockPeriodSeconds;
                        _spots.Add(new Spot(sector.Id, sector.Hazard.Kind, at, phase));
                    }
            }
        }

        /// <summary>
        /// How a hazard's animation is drawn: falling rock's pebbles stand among the props (a cell wide); foul air's puffs and
        /// the flood's ripples lie low over the floor, under whoever walks through them, and wider than a cell.
        /// </summary>
        public static DrawRole RoleOf(HazardKind kind) => kind == HazardKind.FallingRock ? DrawRole.Prop : DrawRole.FloorMark;

        /// <summary>
        /// A spot's time into its frames at <paramref name="seconds"/> of the view; negative while a spot of falling rock
        /// waits between pebbles (nothing is drawn then).
        /// </summary>
        public static float LocalSeconds(in Spot spot, float seconds)
        {
            float t = seconds + spot.Phase;
            if (spot.Kind != HazardKind.FallingRock) return t;
            float local = t % RockPeriodSeconds;
            if (local < 0f) local += RockPeriodSeconds;
            return local < Art.HazardSprites.RockFrames / RockFramesPerSecond ? local : -1f;
        }

        private static bool NearANode(MapDefinition map, Vec2 at)
        {
            for (int i = 0; i < map.ResourceNodes.Count; i++)
            {
                if ((map.ResourceNodes[i].Position - at).SqrMagnitude < NodeClearance * NodeClearance) return true;
            }
            return false;
        }

        private static uint Hash(byte sector, int column, int row)
        {
            uint h = 2166136261u;
            h = (h ^ sector) * 16777619u;
            h = (h ^ (uint)column) * 16777619u;
            h = (h ^ (uint)row) * 16777619u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            return h ^ (h >> 15);
        }
    }
}
