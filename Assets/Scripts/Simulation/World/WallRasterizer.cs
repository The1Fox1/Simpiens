using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Writes a map definition's static walls into a <see cref="StaticNavGrid"/> base layer. Pure, headless, allocation-free.
    /// Walls are inflated by the pawn body radius (as dynamic walls are in SpatialHashGrid), so a pawn whose centre
    /// stays in a walkable cell never overlaps wall geometry.
    /// </summary>
    public static class WallRasterizer
    {
        public static float Inflation => NavConstants.PawnBodyRadius;

        /// <summary>
        /// Replaces the grid's base layer with the definition's walls and applies its playable bounds.
        /// Region labels are not rebuilt here; call <see cref="StaticNavGrid.EnsureRegions"/> afterwards.
        /// </summary>
        public static void Rasterize(MapDefinition definition, StaticNavGrid grid)
        {
            if (definition == null || grid == null) return;

            grid.ClearBaseLayer();

            var walls = definition.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                MarkRect(walls[i].Solid, grid);
            }

            // Bounds first, then Clear(): walkability is recomputed from the base layer and the new bounds only,
            // dropping dynamic walls and any cells the previous bounds had blocked
            grid.SetPlayableBounds(definition.PlayableBounds);
            grid.Clear();
        }

        private static void MarkRect(RectF solid, StaticNavGrid grid)
        {
            float inflate = Inflation;
            float cs = grid.CellSize;
            int minX = SimMath.Max(0, SimMath.FloorToInt((solid.XMin - inflate + grid.OffsetX) / cs));
            int maxX = SimMath.Min(grid.Width - 1, SimMath.FloorToInt((solid.XMax + inflate + grid.OffsetX) / cs));
            int minY = SimMath.Max(0, SimMath.FloorToInt((solid.YMin - inflate + grid.OffsetY) / cs));
            int maxY = SimMath.Min(grid.Height - 1, SimMath.FloorToInt((solid.YMax + inflate + grid.OffsetY) / cs));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    grid.SetBaseWall(x, y);
                }
            }
        }

        /// <summary>
        /// True when a circle overlaps any static wall of the definition (exact geometry, no inflation).
        /// Used by diagnostics; allocation-free.
        /// </summary>
        public static bool CircleOverlapsWall(MapDefinition definition, Vec2 center, float radius)
        {
            if (definition == null) return false;
            var walls = definition.Walls;
            float r2 = radius * radius;
            for (int i = 0; i < walls.Count; i++)
            {
                var rect = walls[i].Solid;
                float cx = SimMath.Clamp(center.X, rect.XMin, rect.XMax);
                float cy = SimMath.Clamp(center.Y, rect.YMin, rect.YMax);
                float dx = center.X - cx;
                float dy = center.Y - cy;
                if (dx * dx + dy * dy < r2) return true;
            }
            return false;
        }
    }
}
