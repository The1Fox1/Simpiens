using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 10 Part 2, W1.4, D38): a resident who mines buys a pickaxe at the stall, to mine faster. DesiredState:
    /// HasTool = true, reached through TravelToStall → BuyTool. Only a resident (drifters pass through) wearing nothing that
    /// speeds its mining, who knows a mineral node it would mine, isn't hungry or worn out, and finds a piece in stock at or
    /// under its own price with a ration's price to spare (ResourceTargeting.TryChooseWorkGear, by stats since Epic 10 Part
    /// 3, K1.4), as Majesty's heroes spend their purses at the market.
    /// </summary>
    public class EquipForWorkGoal : GoapGoal
    {
        /// <summary>Above selling salvage (30) and earning (25 and up), under outfitting for danger (40): it equips before its next round of work.</summary>
        public const float Priority = 35f;

        public EquipForWorkGoal() : base("EquipForWork", WorldState.Empty.With(StateFact.HasTool, true), Motive.Gear, ActivityFamily.Work)
        {
        }

        public override float BasePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.IsDrifter || context.Inventory == null || ResourceTargeting.EquippedForWork(context.Inventory)) return false;
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            return ResourceTargeting.TryChooseWorkGear(context, out _) && ResourceTargeting.KnowsMineral(context); // the stock first: usually there is none
        }
    }
}
