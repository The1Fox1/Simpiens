using Simpiens.Cognition.Contracts;
namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Earn gold by gathering salvage and selling it at an outpost trade stall.
    /// DesiredState: HasSoldSalvage = true.
    /// Reached through SearchSalvage → GatherSalvage → TravelToStall → SellSalvage. This is the no-bounty
    /// supply chain that keeps stall ore/herb stock moving. Priority is a flat need curve on low gold;
    /// per-agent variation is owned by §3.2 (personality).
    /// </summary>
    public class EarnIncomeGoal : GoapGoal
    {
        public EarnIncomeGoal() : base("EarnIncome", WorldState.Empty.With(StateFact.HasSoldSalvage, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            int gold = context.Inventory != null ? context.Inventory.Gold : 0;
            int comfortable = Considerations.Economy(context).ComfortablePurse;
            // The need is a share of the purse, 20 when empty (Epic 10 Part 2, T5.1b): a bigger purse doesn't make a crawler
            // more desperate, nor every bounty look smaller beside earning (before the day, 0.5 a gold short of 40g, as here)
            float need = comfortable > 0 ? SimMath.Clamp(comfortable - gold, 0, comfortable) * (20f / comfortable) : 0f;
            float priority = SimMath.Max(5f, 25f + need - (context.Hunger * 0.2f));
            return priority * Considerations.TraitFactor(context.Personality.Greed, Considerations.GreedOnIncome)
                            * Considerations.TraitFactor(context.Personality.Diligence, Considerations.DiligenceOnWork);
        }

        public override bool IsValid(AgentContext context)
        {
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            if (context.Inventory == null || context.Inventory.Gold >= Considerations.Economy(context).ComfortablePurse) return false;
            if (context.Snapshot == null || context.Snapshot.ActiveStallCount == 0) return false;
            if (!ResourceTargeting.NearestStallCanBuySalvage(context)) return false;

            // A claimed contract takes precedence; the bounty goal drives that agent
            return !BountyQueries.TryGetClaimedBounty(context, out _);
        }
    }
}
