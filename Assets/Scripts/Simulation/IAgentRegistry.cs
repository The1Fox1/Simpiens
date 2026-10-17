using System.Collections.Generic;
using Simpiens.Cognition;
namespace Simpiens.Simulation
{
    /// <summary>
    /// Authoritative registry managing all active AgentState instances in the simulation.
    /// Completely isolated from Unity scene GameObjects to support headless server execution.
    /// </summary>
    public interface IAgentRegistry
    {
        int Count { get; }
        IReadOnlyList<AgentState> AllAgents { get; }

        void RegisterAgent(AgentState agent);
        bool UnregisterAgent(EntityId agentId);
        bool TryGetAgent(EntityId agentId, out AgentState agent);
        AgentState GetAgent(EntityId agentId);
        void Clear();
    }
}
