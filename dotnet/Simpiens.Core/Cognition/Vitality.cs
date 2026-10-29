using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.World;

namespace Simpiens.Cognition
{
    /// <summary>
    /// A crawler's health and the danger of the floor (Epic 10 G2; producer decisions Q2, no combat, and Q3, no death).
    /// Hazard sectors (map data, <see cref="SectorHazard"/>) take health while a crawler stands in them; at 0 it is downed
    /// and limps to a bunk, where it heals; a medicinal herb heals at once. Who goes into danger is a matter of
    /// personality and condition. Times are per second of simulation (ticks are a tenth of one).
    /// </summary>
    public static class Vitality
    {
        public const float MaxHealth = 100f;

        /// <summary>Healing anywhere, per second: slow (a full recovery takes about half an hour).</summary>
        public const float RestingHealPerSecond = 0.05f;

        /// <summary>Healing asleep in a bunk, per second.</summary>
        public const float BunkHealPerSecond = 4f;

        /// <summary>A crawler under this eats a herb it carries.</summary>
        public const float TreatWoundsBelow = 60f;

        /// <summary>A crawler in a hazard sector under this gives up and heads for a bunk.</summary>
        public const float RetreatBelow = 40f;

        /// <summary>A downed crawler gets up again at this health.</summary>
        public const float RecoveredAt = 60f;

        /// <summary>A downed crawler limps at this share of its speed.</summary>
        public const float DownedSpeedFactor = 0.4f;

        /// <summary>Only a crawler at least this bold (personality) and this healthy goes into a hazard sector for its salvage.</summary>
        public const float VentureRiskTolerance = 0.3f;
        public const int VentureRiskTolerancePercent = 30;
        public const float VentureHealth = 80f;

        /// <summary>True when this crawler would go into danger for salvage now.</summary>
        public static bool WouldVenture(in AgentContext context) =>
            context.Personality.RiskTolerancePercent >= VentureRiskTolerancePercent && context.Health >= VentureHealth && !context.IsDowned;

        /// <summary>
        /// Where a hurt crawler heads: the floor plan's nearest bunk, free or not (the plan is common knowledge; a crawler
        /// that knew no free bunk stayed downed in a hazard sector all day). False on a map without bunks.
        /// </summary>
        public static bool TryGetRefuge(in AgentContext context, out Vec2 refuge)
        {
            refuge = default;
            var map = context.Snapshot?.Map;
            if (map == null) return false;
            float bestSqr = float.MaxValue;
            bool found = false;
            for (int i = 0; i < map.Pois.Count; i++)
            {
                var poi = map.Pois[i];
                if (poi.Kind != PoiKind.Bunk) continue;
                float sqr = (poi.Position - context.Position).SqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                refuge = poi.Position;
                found = true;
            }
            return found;
        }

        /// <summary>The danger where <paramref name="position"/> lies, or none.</summary>
        public static SectorHazard HazardAt(MapDefinition map, Vec2 position) => map != null ? map.FindSector(position).Hazard : default;
    }
}
