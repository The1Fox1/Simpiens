using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic LIFE-1 plan 2, W.3, D20): spend free time on a want the crawler holds, at the venue and activity it asks for.
    /// DesiredState: WantMet = true. Base: the want's strength times the leisure score of the best spell that answers it
    /// (<see cref="Leisure.TryChooseForWants"/>). Never hungry, tired or hurt, as Recreate; a want pulls one crawler to one place
    /// where Recreate takes whatever scores best.
    /// </summary>
    public class FulfilWantGoal : GoapGoal
    {
        public FulfilWantGoal() : base("FulfilWant", WorldState.Empty.With(StateFact.WantMet, true), Motive.Want, ActivityFamily.Leisure)
        {
        }

        public override float BasePriority(AgentContext context) => Leisure.TryChooseForWants(context, out _, out _, out _, out float pull) ? pull : 0f;

        public override bool IsValid(AgentContext context)
        {
            if (context.Hunger >= RecreateGoal.HungerLine || context.Energy < Considerations.TiredEnergy || context.Health < Vitality.TreatWoundsBelow) return false;
            return Leisure.TryChooseForWants(context, out _, out _, out _, out _);
        }
    }
}
