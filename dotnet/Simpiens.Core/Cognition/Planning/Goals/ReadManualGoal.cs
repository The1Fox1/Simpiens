using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Items;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 11, S3.3, Q10, D11): a crawler carrying a manual reads it at rest by the hearth, two game hours, and it is
    /// used up. DesiredState: HasManual = false, reached through TravelToAnchor → ReadManual. Only a crawler not hungry or worn
    /// out, who knows a hearth.
    /// </summary>
    public class ReadManualGoal : GoapGoal
    {
        /// <summary>Just above earning's floor (25): a bought book is read before the next round of work, once the purse allows.</summary>
        public const float Priority = 27f;

        public ReadManualGoal() : base("ReadManual", WorldState.Empty.With(StateFact.HasManual, false), Motive.Growth, ActivityFamily.Leisure)
        {
        }

        public override float BasePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.Inventory == null || !context.Inventory.HasAny(ItemCategory.Manual)) return false;
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            // A hearth it remembers (as gathering at the anchor asks)
            var memory = context.Memory?.SpatialMemoryMap;
            if (memory == null) return false;
            foreach (var pair in memory) if (pair.Value.Type == Simpiens.Simulation.Spatial.EntityType.Anchor) return true;
            return false;
        }
    }
}
