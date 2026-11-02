using System.Collections.Concurrent;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// The session's planner (ROADMAP §1A.2 #8): a pool of <see cref="GoapPlanner"/>s, as <c>AsyncPathfinder</c> pools its
    /// A* searchers. Each Plan rents one, so evaluations on several workers plan at once instead of queueing on one
    /// planner's lock. A plan depends only on its inputs (every search resets the planner's buffers), so which planner
    /// runs it changes nothing. The pool grows to the most plans ever in flight at once, then allocates nothing.
    /// </summary>
    public sealed class GoapPlannerPool : IGoapPlanner
    {
        private const int InitialPlanners = 8;

        private readonly ConcurrentQueue<GoapPlanner> _planners = new ConcurrentQueue<GoapPlanner>();
        private readonly int _maxNodes;

        public GoapPlannerPool(int maxNodes = 256)
        {
            _maxNodes = maxNodes;
            for (int i = 0; i < InitialPlanners; i++) _planners.Enqueue(new GoapPlanner(maxNodes));
        }

        /// <summary>Planners idle in the pool right now (diagnostics and tests).</summary>
        public int IdleCount => _planners.Count;

        /// <inheritdoc />
        public bool Plan(in WorldState currentState, GoapGoal goal, ActionGraph actionGraph, AgentContext context, GoapPlan outPlan)
        {
            if (!_planners.TryDequeue(out var planner))
            {
                planner = new GoapPlanner(_maxNodes);
            }

            try
            {
                return planner.Plan(in currentState, goal, actionGraph, context, outPlan);
            }
            finally
            {
                _planners.Enqueue(planner);
            }
        }
    }
}
