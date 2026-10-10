using System.Collections.Generic;
using Simpiens.Cognition;
using UnityEngine;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Default in-memory implementation of IAgentRegistry.
    /// Thread-safe for simulation read loops on the main thread and provides O(1) lookups.
    /// </summary>
    public class AgentRegistry : IAgentRegistry
    {
        private readonly Dictionary<GUID, AgentState> _agentsById = new Dictionary<GUID, AgentState>(256);
        private readonly List<AgentState> _agentList = new List<AgentState>(256);

        public int Count => _agentList.Count;
        public IReadOnlyList<AgentState> AllAgents => _agentList;

        public void RegisterAgent(AgentState agent)
        {
            if (agent == null) return;
            if (_agentsById.ContainsKey(agent.AgentId))
            {
                _agentsById[agent.AgentId] = agent;
                for (int i = 0; i < _agentList.Count; i++)
                {
                    if (_agentList[i].AgentId == agent.AgentId)
                    {
                        _agentList[i] = agent;
                        return;
                    }
                }
                return;
            }

            _agentsById[agent.AgentId] = agent;
            _agentList.Add(agent);
        }

        public bool UnregisterAgent(GUID agentId)
        {
            if (agentId == default) return false;
            if (_agentsById.Remove(agentId))
            {
                for (int i = 0; i < _agentList.Count; i++)
                {
                    if (_agentList[i].AgentId == agentId)
                    {
                        _agentList.RemoveAt(i);
                        break;
                    }
                }
                return true;
            }
            return false;
        }

        public bool TryGetAgent(GUID agentId, out AgentState agent)
        {
            if (agentId == default)
            {
                agent = null;
                return false;
            }
            return _agentsById.TryGetValue(agentId, out agent);
        }

        public AgentState GetAgent(GUID agentId)
        {
            if (agentId == default) return null;
            _agentsById.TryGetValue(agentId, out var agent);
            return agent;
        }

        public void Clear()
        {
            _agentsById.Clear();
            _agentList.Clear();
        }
    }
}
