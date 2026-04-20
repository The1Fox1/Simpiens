using System.Threading;
using System.Threading.Tasks;
using Simpiens.Simulation;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Processes agent reasoning asynchronously via Tasks/message queues.
    /// This engine isolates slow LLM queries or heavy pathfinding from the main thread.
    /// </summary>
    public class CognitiveEngine : ICognitiveEngine
    {
        private readonly ISimulationClock _simulationClock;
        private readonly ContextValidator _contextValidator;

        public CognitiveEngine(ISimulationClock simulationClock, ContextValidator contextValidator)
        {
            _simulationClock = simulationClock;
            _contextValidator = contextValidator;
        }

        public async Task<AgentDecision?> ProcessAgentCognitionAsync(string agentId, CancellationToken cancellationToken)
        {
            long initiatedTick = _simulationClock.CurrentTick;

            try
            {
                // Note: Avoid Unity API calls in here. Use pure C# data structures.
                
                // Example simulated async delay representing inference or complex reasoning
                await Task.Delay(100, cancellationToken);
                
                // Ensure task stops if cancellation was requested
                cancellationToken.ThrowIfCancellationRequested();

                // Mocking the result of the heavy computation
                var decision = new AgentDecision("move_to_resource", initiatedTick);

                // Check against the current tick before returning the decision
                if (!_contextValidator.IsDecisionValid(decision))
                {
                    return null; // Decision is stale, abort.
                }

                return decision;
            }
            catch (TaskCanceledException)
            {
                // Task was cleanly canceled due to an interrupt flag or agent death
                return null;
            }
        }
    }
}
