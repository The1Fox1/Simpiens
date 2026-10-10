using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Service responsible for asynchronous cognitive evaluation and intent formulation for an AgentState.
    /// Bridges reflexive evaluators, deliberative GOAP planning, and action dispatch off the main thread.
    /// </summary>
    public interface IAgentCognitionService
    {
        UniTask EvaluateAgentCognitionAsync(
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
