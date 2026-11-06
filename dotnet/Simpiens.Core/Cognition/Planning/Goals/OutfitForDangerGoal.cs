using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 10 G3): before going into danger, buy protection at the stall. DesiredState: HasHazardGear = true.
    /// Reached through TravelToStall → BuyHazardGear. Only a crawler that would venture (Vitality.WouldVenture: bold and
    /// healthy) wants it, and only while it wears none, isn't hungry or worn out, and the stall has a piece it can pay for
    /// that lets it work hazardous salvage it knows, preferring one that opens salvage it can't reach as it is (a respirator
    /// for the Gallery, Epic 10 Part 2, D51; ResourceTargeting.TryChooseProtection).
    /// </summary>
    public class OutfitForDangerGoal : GoapGoal
    {
        /// <summary>Above selling salvage (30) and earning (25 and up): a crawler gears up before it goes back in; real hunger still comes first.</summary>
        public const float Priority = 40f;

        public OutfitForDangerGoal() : base("OutfitForDanger", WorldState.Empty.With(StateFact.HasHazardGear, true))
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.Inventory == null || context.Inventory.IsWearing(Simpiens.Simulation.Items.ItemCategory.Protection)) return false;
            if (!Vitality.WouldVenture(context)) return false;
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            return ResourceTargeting.TryChooseProtection(context, out _);
        }
    }
}
