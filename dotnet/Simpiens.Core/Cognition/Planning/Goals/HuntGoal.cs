using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 10 Part 3, K6.2, Q7): a bold, armed resident joins a hunt the player posted and goes after the lair until it
    /// is empty, when the escrow pays the hunters by the damage each dealt (<see cref="Hunting"/>). DesiredState: LairHunted =
    /// true, through ClaimHunt and HuntLair. Weighted as a bounty is: its reward, the hunter's greed and diligence, less its
    /// hunger. Not while it is unfit: a hurt hunter heals, still in the party, and comes back.
    /// </summary>
    public class HuntGoal : GoapGoal
    {
        public HuntGoal() : base("Hunt", WorldState.Empty.With(StateFact.LairHunted, true), Motive.Income, ActivityFamily.Work)
        {
        }

        public override float BasePriority(AgentContext context)
        {
            float priority = 35f;
            ushort reward = 0;
            if (Hunting.TryGetJoined(context, out var joined) && context.Snapshot != null && context.Snapshot.TryGetBounty(joined.BountyId, out var own)) reward = own.RewardGold;
            else if (Hunting.TryChoose(context, out _, out var open)) reward = open.RewardGold;
            priority += SimMath.Clamp((reward / 50f) * 25f, 5f, 35f) * Considerations.TraitFactor(context.Personality.Greed, Considerations.GreedOnReward);
            priority *= Considerations.TraitFactor(context.Personality.Diligence, Considerations.DiligenceOnWork);
            return SimMath.Max(5f, priority - (context.Hunger * 0.3f));
        }

        public override bool IsValid(AgentContext context)
        {
            if (!Hunting.IsHunter(context)) return false;
            return (Hunting.TryGetJoined(context, out var joined) && Hunting.Braves(context, joined.LairIndex)) || Hunting.TryChoose(context, out _, out _);
        }
    }
}
