using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Defines the contract for Goal-Oriented Action Planning search algorithms.
    /// Evaluates current world state and goal to find an optimal sequence of actions.
    /// </summary>
    public interface IGoapPlanner
    {
        /// <summary>
        /// Finds the lowest-cost sequence of actions from currentState that satisfies goal.DesiredState.
        /// Populates outPlan with the discovered actions.
        /// </summary>
        /// <param name="currentState">The starting state of the agent/world.</param>
        /// <param name="goal">The target goal containing DesiredState.</param>
        /// <param name="actionGraph">The graph containing available actions.</param>
        /// <param name="context">The agent context for procedural validity and dynamic costs.</param>
        /// <param name="outPlan">Pre-allocated/reusable plan object to receive the action sequence.</param>
        /// <returns>True if a valid plan was found; false if the goal is unreachable.</returns>
        bool Plan(
            in WorldState currentState,
            GoapGoal goal,
            ActionGraph actionGraph,
            AgentContext context,
            GoapPlan outPlan);
    }
}
