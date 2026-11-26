using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic LIFE-1 plan 2, L2.6, D14; the epic's A6): spend free time at a venue, at the best activity a crawler knows of.
    /// DesiredState: Recreated = true. Base: fun's urgency (its boredom) times the best leisure score (<see cref="Leisure"/>),
    /// with no constants of its own (AGENTS.md, "Adding a goal").
    /// Never while hungry (60), tired (under the tired line) or hurt (under the treat line), so it never outbids eating, sleep
    /// or healing (the epic's F12). Under orders it stays valid and competes with duty through the motive model (plan 2's D25).
    /// </summary>
    public class RecreateGoal : GoapGoal
    {
        /// <summary>The hunger from which a crawler eats before it plays (EarnIncome's line).</summary>
        public const float HungerLine = 60f;

        public RecreateGoal() : base("Recreate", WorldState.Empty.With(StateFact.Recreated, true), Motive.Fun, ActivityFamily.Leisure)
        {
        }

        public override float BasePriority(AgentContext context)
        {
            if (!Leisure.TryChoose(context, out _, out _, out _, out float score)) return 0f;
            return Urgency.Of(Motive.Fun, context) * score;
        }

        public override bool IsValid(AgentContext context)
        {
            if (context.Hunger >= HungerLine || context.Energy < Considerations.TiredEnergy || context.Health < Vitality.TreatWoundsBelow) return false;
            return Leisure.TryChoose(context, out _, out _, out _, out _);
        }
    }
}
