using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 10 Part 2, W2.3, D45): before going into danger, a crawler who would venture buys a draught to carry, as
    /// Majesty's heroes buy potions at the market. DesiredState: HasMedicine = true, through TravelToStall → BuyDraught. Only
    /// one who would venture (Vitality.WouldVenture: bold and healthy) and knows where hazardous salvage is, carries no
    /// medicine, isn't hungry or worn out, and finds a draught in stock at or under its own price with a ration's price to
    /// spare (ResourceTargeting.CanBuyMedicine). The herb reflex drinks it when hurt.
    /// </summary>
    public class CarryMedicineGoal : GoapGoal
    {
        /// <summary>Under outfitting for danger (40), so gear comes first; above equipping for work (35) and selling salvage (30).</summary>
        public const float Priority = 38f;

        public CarryMedicineGoal() : base("CarryMedicine", WorldState.Empty.With(StateFact.HasMedicine, true))
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.Inventory == null || context.Inventory.HasAny(Simpiens.Simulation.Items.ItemCategory.Medicine)) return false;
            if (!Vitality.WouldVenture(context)) return false;
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            return ResourceTargeting.CanBuyMedicine(context) && ResourceTargeting.KnowsHazardousSalvage(context); // the stock first: usually there is none
        }
    }
}
