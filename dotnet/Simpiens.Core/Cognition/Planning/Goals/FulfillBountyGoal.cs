using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;
namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Take on and fulfill a public bounty contract to earn currency and establish renown.
    /// DesiredState: BountyDelivered = true.
    /// </summary>
    public class FulfillBountyGoal : GoapGoal
    {
        public FulfillBountyGoal() : base("FulfillBounty", WorldState.Empty.With(StateFact.BountyDelivered, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            float priority = 35f;

            // Own claim first, otherwise the bounty this agent would claim (reachable, lowest id)
            if (BountyQueries.TryGetClaimedBounty(context, out var bounty) || BountyQueries.TryGetOpenBounty(context, BountyType.None, out bounty))
            {
                priority += SimMath.Clamp((bounty.RewardGold / 50f) * 25f, 5f, 35f)
                            * Considerations.TraitFactor(context.Personality.Greed, Considerations.GreedOnReward);
            }

            // Reduced priority if biological hunger is urgent
            priority *= Considerations.TraitFactor(context.Personality.Diligence, Considerations.DiligenceOnWork);
            return SimMath.Max(5f, priority - (context.Hunger * 0.3f));
        }

        public override bool IsValid(AgentContext context)
        {
            // Requires stable biological drives
            if (context.Hunger >= 75f || context.Energy <= 25f)
            {
                return false;
            }

            // A claimed contract, or an open one this agent can actually reach and redeem
            return BountyQueries.TryGetClaimedBounty(context, out _) || BountyQueries.TryGetOpenBounty(context, BountyType.None, out _);
        }
    }
}
