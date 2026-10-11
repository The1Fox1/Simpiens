using Simpiens.Cognition.Groups;
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
        public readonly Simpiens.Cognition.Memory.IAgentMemory Memory;
        public readonly uint CurrentTick;
        public readonly float Frustration;

        // Sociopolitical & Identity standing
        public readonly GroupMembership Membership;
        public readonly IdentityTag Identity;

        // Economic Inventory & Wallet (Epic 9)
        public readonly Simpiens.Cognition.Inventory.IAgentInventory Inventory;

        // Scout bounty id for which the agent holds a completed sector report (0 = none)
        public readonly ushort ScoutReportBountyId;

        // Stable traits (review §3.2); default is neutral
        public readonly AgentPersonality Personality;

        public AgentContext(GUID agentId, Vector2 position, in AgentNeeds needs, float frustration,
                            SharedWorldSnapshot snapshot, Simpiens.Cognition.Memory.IAgentMemory memory, uint currentTick,
                            in GroupMembership membership = default, in IdentityTag identity = default,
                            Simpiens.Cognition.Inventory.IAgentInventory inventory = null,
                            ushort scoutReportBountyId = 0,
                            in AgentPersonality personality = default)
        {
            ScoutReportBountyId = scoutReportBountyId;
            Personality = personality;
            AgentId = agentId;
            Position = position;
            Needs = needs;
            Frustration = frustration;
            Snapshot = snapshot;
            Memory = memory;
            CurrentTick = currentTick;
            Membership = membership;
            Identity = identity;
            Inventory = inventory;
        }

        // Backward-compatible constructor
        public AgentContext(GUID agentId, Vector2 position, float hunger, float energy, float frustration,
                            SharedWorldSnapshot snapshot, Simpiens.Cognition.Memory.IAgentMemory memory, uint currentTick)
            : this(agentId, position, new AgentNeeds(hunger, energy, 50f), frustration, snapshot, memory, currentTick, GroupMembership.None, IdentityTag.Default, null)
        {
        }
    }
}
