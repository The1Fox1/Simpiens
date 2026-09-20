using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;

namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Deliberative cognitive evaluator implementing multi-step Goal-Oriented Action Planning.
    /// Evaluates candidate goals based on agent context, queries the IGoapPlanner,
    /// and dispatches the appropriate action intents.
    /// </summary>
    public class GoapCognitiveEvaluator : ICognitiveEvaluator
    {
        private readonly IGoapPlanner _planner;
        private readonly ActionGraph _actionGraph;
        private readonly List<GoapGoal> _goals;
        private readonly GoapPlan _scratchPlan;

        public IReadOnlyList<GoapGoal> Goals => _goals;
        public ActionGraph ActionGraph => _actionGraph;
        public IGoapPlanner Planner => _planner;

        public GoapCognitiveEvaluator(IGoapPlanner planner, ActionGraph actionGraph, List<GoapGoal> goals = null)
        {
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _actionGraph = actionGraph ?? throw new ArgumentNullException(nameof(actionGraph));
            _goals = goals ?? new List<GoapGoal>(8);
            _scratchPlan = new GoapPlan(16);
        }

        public void RegisterGoal(GoapGoal goal)
        {
            if (goal == null) throw new ArgumentNullException(nameof(goal));
            if (!_goals.Contains(goal))
            {
                _goals.Add(goal);
            }
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            if (_goals.Count == 0) return null;

            // 1. Build current world state bitmask from context and memory
            var currentState = WorldStateBuilder.BuildWorldState(context);

            // 2. Select the highest priority valid, unsatisfied goal
            GoapGoal bestGoal = null;
            float highestPriority = -1f;

            int goalCount = _goals.Count;
            for (int i = 0; i < goalCount; i++)
            {
                var goal = _goals[i];
                if (!goal.IsValid(context)) continue;
                if (goal.IsSatisfied(currentState)) continue;

                float priority = goal.CalculatePriority(context);
                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    bestGoal = goal;
                }
            }

            if (bestGoal == null)
            {
                return null;
            }

            // 3. Plan using the A* search engine
            bool success = _planner.Plan(currentState, bestGoal, _actionGraph, context, _scratchPlan);
            if (!success || _scratchPlan.IsEmpty)
            {
                return null;
            }

            // 4. Generate the intent for the first action step in the plan
            var firstAction = _scratchPlan.CurrentAction;
            if (firstAction == null || !firstAction.IsValid(context))
            {
                return null;
            }

            return await firstAction.CreateIntentAsync(context, ct);
        }
    }
}
