using System.Collections.Generic;
using System.Threading;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Service responsible for cognitive evaluation and intent formulation for an AgentState.
    /// Bridges reflexive evaluators, deliberative GOAP planning, and action dispatch off the main thread.
    /// </summary>
    public interface IAgentCognitionService
    {
        /// <summary>
        /// Main thread. Prepares the agent and starts its evaluation off the main thread. The decision is applied by the
        /// simulation's <see cref="CognitionScheduler"/> at the start of the next tick (or at once, without one).
        /// </summary>
        void BeginEvaluation(
            AgentState agent,
            ISpatialPartition spatialPartition,
            ISimulationManager simulationManager,
            ISimulationClock clock,
            IGoapPlanner planner,
            ActionGraph actionGraph,
            IReadOnlyList<GoapGoal> goals,
            ICognitiveEvaluator[] evaluators,
            CancellationToken ct = default);
    }
}
