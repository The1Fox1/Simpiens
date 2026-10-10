using Simpiens.Cognition.Contracts;
using UnityEngine;

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
        public const int ComfortableGold = 40;

        public EarnIncomeGoal() : base("EarnIncome", WorldState.Empty.With(StateFact.HasSoldSalvage, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            int gold = context.Inventory != null ? context.Inventory.Gold : 0;
            float need = Mathf.Clamp(ComfortableGold - gold, 0, ComfortableGold) * 0.5f;
            return Mathf.Max(5f, 25f + need - (context.Hunger * 0.2f));
        }

        public override bool IsValid(AgentContext context)
        {
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            if (context.Inventory == null || context.Inventory.Gold >= ComfortableGold) return false;
            if (context.Snapshot == null || context.Snapshot.ActiveStallCount == 0) return false;
            if (!ResourceTargeting.NearestStallCanBuySalvage(context)) return false;

            // A claimed contract takes precedence; the bounty goal drives that agent
            return !BountyQueries.TryGetClaimedBounty(context, out _);
        }
    }
}
