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

        /// <summary>The session's pace (Epic 10 Part 2, milestone T): what sleep and rest restore. Set with the session's economy.</summary>
        void SetPace(in Simpiens.Simulation.Economy.DayTuning day);
    }
}
