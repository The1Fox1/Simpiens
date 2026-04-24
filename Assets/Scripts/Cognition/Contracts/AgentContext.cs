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
        
        // Internal Drives (0.0f to 100.0f)
        public readonly float Hunger;
        public readonly float Energy;

        public readonly SharedWorldSnapshot Snapshot;

        public AgentContext(GUID agentId, Vector2 position, float hunger, float energy, SharedWorldSnapshot snapshot)
        {
            AgentId = agentId;
            Position = position;
            Hunger = hunger;
            Energy = energy;
            Snapshot = snapshot;
        }
    }
}
