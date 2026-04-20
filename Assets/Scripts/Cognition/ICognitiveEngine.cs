using System.Threading;
using System.Threading.Tasks;

namespace Simpiens.Cognition
{
    public interface ICognitiveEngine
    {
        /// <summary>
        /// Initiates the cognitive loop for a specific agent.
        /// This runs asynchronously outside the main Unity frame cycle.
        /// </summary>
        Task<AgentDecision?> ProcessAgentCognitionAsync(string agentId, CancellationToken cancellationToken);
    }
}
