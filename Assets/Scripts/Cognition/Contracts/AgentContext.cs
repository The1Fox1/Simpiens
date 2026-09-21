using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A pure C# struct holding the agent's internal state and a read-only snapshot 
    /// of their local surroundings. Used by the cognitive engine to evaluate utility.
    /// </summary>
    public readonly struct AgentContext
    {
        public readonly GUID AgentId;
        public readonly Vector2 Position;
        
        // Internal Drives & Needs
        public readonly AgentNeeds Needs;
        public float Hunger => Needs.Hunger;
        public float Energy => Needs.Energy;
        public float Social => Needs.Social;

        public readonly SharedWorldSnapshot Snapshot;
        public readonly Simpiens.Cognition.Memory.AgentMemory Memory;
        public readonly uint CurrentTick;
        public readonly float Frustration;

        public AgentContext(GUID agentId, Vector2 position, in AgentNeeds needs, float frustration, SharedWorldSnapshot snapshot, Simpiens.Cognition.Memory.AgentMemory memory, uint currentTick)
        {
            AgentId = agentId;
            Position = position;
            Needs = needs;
            Frustration = frustration;
            Snapshot = snapshot;
            Memory = memory;
            CurrentTick = currentTick;
        }

        // Backward-compatible constructor
        public AgentContext(GUID agentId, Vector2 position, float hunger, float energy, float frustration, SharedWorldSnapshot snapshot, Simpiens.Cognition.Memory.AgentMemory memory, uint currentTick)
            : this(agentId, position, new AgentNeeds(hunger, energy, 50f), frustration, snapshot, memory, currentTick)
        {
        }
    }
}
