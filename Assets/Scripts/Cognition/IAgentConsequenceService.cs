using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Service responsible for applying action consequences and rewards/penalties to an AgentState
    /// upon the completion, failure, or cancellation of an AgentIntent.
    /// </summary>
    public interface IAgentConsequenceService
    {
        void ApplyConsequences(AgentState agent, AgentIntent intent, IntentResult result, uint currentTick);
    }
}
