using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 11, S3.3, Q10, D11): a resident buys a manual for a skill it loves at the stall, to read at rest.
    /// DesiredState: HasManual = true, reached through TravelToStall → BuyManual. Only a resident carrying no manual, not
    /// hungry or worn out, with a manual in stock it would buy (<see cref="ResourceTargeting.TryChooseManual"/>: passions only,
    /// below the manual's ceiling, from a spare purse).
    /// </summary>
    public class StudyGoal : GoapGoal
    {
        /// <summary>Beside selling salvage (30), under equipping for work (35): bought on the way between rounds of work.</summary>
        public const float Priority = 30f;

        public StudyGoal() : base("Study", WorldState.Empty.With(StateFact.HasManual, true))
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            return ResourceTargeting.TryChooseManual(context, out _);
        }
    }
}
