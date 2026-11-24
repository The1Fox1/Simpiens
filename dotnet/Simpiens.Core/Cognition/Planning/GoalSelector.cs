using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// The best three goals of one choice (Epic LIFE-1, L0.4, D3), highest first: the best is pursued, the others are the
    /// "why" (<see cref="DecisionSummary"/>) and the fallback's candidates (L0.6). A value type with three slots, so a
    /// choice allocates nothing.
    /// </summary>
    public struct RankedGoals
    {
        public GoapGoal Goal1, Goal2, Goal3;
        public float Priority1, Priority2, Priority3;

        /// <summary>How many slots hold a goal (0 to 3).</summary>
        public readonly int Count => Goal1 == null ? 0 : Goal2 == null ? 1 : Goal3 == null ? 2 : 3;

        /// <summary>The goal in place <paramref name="index"/> (0 the best), or null.</summary>
        public readonly GoapGoal GoalAt(int index) => index switch { 0 => Goal1, 1 => Goal2, 2 => Goal3, _ => null };

        /// <summary>The priority in place <paramref name="index"/>, or -1.</summary>
        public readonly float PriorityAt(int index) => index switch { 0 => Priority1, 1 => Priority2, 2 => Priority3, _ => -1f };

        public static RankedGoals Empty => new RankedGoals { Priority1 = -1f, Priority2 = -1f, Priority3 = -1f };
    }

    /// <summary>
    /// How a crawler chooses its goal (Epic LIFE-1, L0.4, D3; the epic's F1): the one argmax over the valid, unsatisfied goals,
    /// which <see cref="AgentCognitionService"/> and <see cref="Evaluators.GoapCognitiveEvaluator"/> both call. Ties go to
    /// the earlier goal in the list (a later one must be strictly higher), and a priority of -1 or less is never ranked.
    /// Stateless; allocates nothing; reads only the context it is given, so it runs off the main thread.
    /// </summary>
    public static class GoalSelector
    {
        /// <summary>
        /// Ranks <paramref name="goals"/> for <paramref name="context"/> in <paramref name="state"/>: the best three, highest
        /// first, by each goal's priority. <paramref name="trace"/> logs each goal's verdict (the planning channel, at trace).
        /// </summary>
        public static RankedGoals Rank(IReadOnlyList<GoapGoal> goals, AgentContext context, in WorldState state, bool trace = false)
        {
            var ranked = RankedGoals.Empty;
            if (goals == null) return ranked;
            int count = goals.Count;
            for (int i = 0; i < count; i++)
            {
                var goal = goals[i];
                if (!goal.IsValid(context))
                {
                    if (trace) SimLog.Planning.ZLogTrace($"{SimLog.Agent(context.AgentId)} goal {goal.Name}: invalid");
                    continue;
                }
                if (goal.IsSatisfied(state))
                {
                    if (trace) SimLog.Planning.ZLogTrace($"{SimLog.Agent(context.AgentId)} goal {goal.Name}: already satisfied");
                    continue;
                }

                float priority = goal.CalculatePriority(context);
                if (trace) SimLog.Planning.ZLogTrace($"{SimLog.Agent(context.AgentId)} goal {goal.Name}: priority {priority:F1}");
                if (priority > ranked.Priority1)
                {
                    ranked.Goal3 = ranked.Goal2; ranked.Priority3 = ranked.Priority2;
                    ranked.Goal2 = ranked.Goal1; ranked.Priority2 = ranked.Priority1;
                    ranked.Goal1 = goal; ranked.Priority1 = priority;
                }
                else if (priority > ranked.Priority2)
                {
                    ranked.Goal3 = ranked.Goal2; ranked.Priority3 = ranked.Priority2;
                    ranked.Goal2 = goal; ranked.Priority2 = priority;
                }
                else if (priority > ranked.Priority3)
                {
                    ranked.Goal3 = goal; ranked.Priority3 = priority;
                }
            }
            return ranked;
        }
    }
}
