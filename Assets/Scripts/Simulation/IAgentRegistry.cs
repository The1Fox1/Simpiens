using System.Collections.Generic;
using Simpiens.Cognition;
using UnityEngine;

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
        bool UnregisterAgent(GUID agentId);
        bool TryGetAgent(GUID agentId, out AgentState agent);
        AgentState GetAgent(GUID agentId);
        void Clear();
    }
}
