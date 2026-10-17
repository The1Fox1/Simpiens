using System;
using Simpiens.Simulation.World;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// Identifies the map a save was made on, so a load can refuse a save made on another map (Milestone 1, D4).
    /// FNV-1a over what the save's entity ids and positions rest on: the map's name and bounds, its walls, sectors,
    /// resource nodes and points of interest, in definition order. Labels and the camera frame are presentation and
    /// are left out.
    /// </summary>
    public static class SaveFingerprint
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Of(MapDefinition map)
        {
            if (map == null) return 0UL;

            ulong h = Offset;
            Mix(ref h, map.Name);
            Mix(ref h, map.PlayableBounds);

            Mix(ref h, map.Walls.Count);
            for (int i = 0; i < map.Walls.Count; i++) Mix(ref h, map.Walls[i].Solid);

            Mix(ref h, map.Sectors.Count);
            for (int i = 0; i < map.Sectors.Count; i++) Mix(ref h, map.Sectors[i]);
            Mix(ref h, map.FallbackSector);

            Mix(ref h, map.ResourceNodes.Count);
            for (int i = 0; i < map.ResourceNodes.Count; i++)
            {
                var node = map.ResourceNodes[i];
                Mix(ref h, node.Key);
                Mix(ref h, node.Position);
                Mix(ref h, (int)node.Item);
                Mix(ref h, node.Yield);
                Mix(ref h, (int)node.RegrowTicks);
            }

            Mix(ref h, map.Pois.Count);
            for (int i = 0; i < map.Pois.Count; i++)
            {
                var poi = map.Pois[i];
                Mix(ref h, poi.Key);
                Mix(ref h, (int)poi.Kind);
                Mix(ref h, poi.Position);
                Mix(ref h, poi.Radius);
            }
            return h;
        }

        private static void Mix(ref ulong h, in SectorDefinition sector)
        {
            Mix(ref h, sector.Id);
            Mix(ref h, sector.Name);
            Mix(ref h, sector.Bounds);
            Mix(ref h, sector.Anchor);
            Mix(ref h, sector.AnchorRadius);
            Mix(ref h, sector.StartsExplored ? 1 : 0);
        }

        private static void Mix(ref ulong h, RectF rect)
        {
            Mix(ref h, rect.X);
            Mix(ref h, rect.Y);
            Mix(ref h, rect.Width);
            Mix(ref h, rect.Height);
        }

        private static void Mix(ref ulong h, Vec2 v)
        {
            Mix(ref h, v.X);
            Mix(ref h, v.Y);
        }

        private static void Mix(ref ulong h, float value) => Mix(ref h, BitConverter.SingleToInt32Bits(value));

        private static void Mix(ref ulong h, string value)
        {
            if (value == null)
            {
                Mix(ref h, -1);
                return;
            }
            Mix(ref h, value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                h ^= value[i];
                h *= Prime;
            }
        }

        private static void Mix(ref ulong h, int value)
        {
            for (int i = 0; i < 4; i++)
            {
                h ^= (uint)(value >> (i * 8)) & 0xFFu;
                h *= Prime;
            }
        }
    }
}
