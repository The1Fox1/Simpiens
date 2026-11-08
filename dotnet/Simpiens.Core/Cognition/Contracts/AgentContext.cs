using Simpiens.Cognition.Groups;
using Simpiens.Simulation.Spatial;
namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A pure C# struct holding the agent's internal state and a read-only snapshot 
    /// of their local surroundings. Used by the cognitive engine to evaluate utility.
    /// </summary>
    public readonly struct AgentContext
    {
        public readonly EntityId AgentId;
        public readonly Vec2 Position;
        
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
        /// <summary>
        /// The membership whose orders the crawler follows (Epic 10 G4, D1): the first of its groups, in kind order (party,
        /// guild, settlement), with an active directive; else its first group; None without any.
        /// </summary>
        public readonly GroupMembership DirectiveMembership;
        public readonly IdentityTag Identity;

        // Economic Inventory & Wallet (Epic 9)
        public readonly Simpiens.Cognition.Inventory.IAgentInventory Inventory;

        // Scout bounty id for which the agent holds a completed sector report (0 = none)
        public readonly ushort ScoutReportBountyId;

        // Stable traits (review §3.2); default is neutral
        public readonly AgentPersonality Personality;

        /// <summary>A drifter's departure tick (its stay ends); 0 for a resident (Epic 10 G1).</summary>
        public readonly uint DepartsAtTick;

        public bool IsDrifter => DepartsAtTick > 0;

        /// <summary>A drifter whose stay is over: it heads for the Road Gate.</summary>
        public bool StayIsOver => DepartsAtTick > 0 && CurrentTick >= DepartsAtTick;

        /// <summary>Health, 0 to 100, and whether the crawler is downed (Epic 10 G2).</summary>
        public readonly float Health;
        public readonly bool IsDowned;

        /// <summary>Its part in the guild's expedition, if any (Epic 10 G5).</summary>
        public readonly ExpeditionOrder Expedition;

        /// <summary>Its part in its station's work, if it is an artisan on shift (Epic 10 Part 2, W1.3).</summary>
        public readonly StationOrder Station;

        /// <summary>The bunk the guild keeps for it in the barracks, if it is a member (Epic 10 Part 2, W5.2).</summary>
        public readonly KeptBunk Bunk;

        public AgentContext(EntityId agentId, Vec2 position, in AgentNeeds needs, float frustration,
                            SharedWorldSnapshot snapshot, Simpiens.Cognition.Memory.IAgentMemory memory, uint currentTick,
                            in GroupMembership directiveMembership = default, in IdentityTag identity = default,
                            Simpiens.Cognition.Inventory.IAgentInventory inventory = null,
                            ushort scoutReportBountyId = 0,
                            in AgentPersonality personality = default,
                            uint departsAtTick = 0,
                            float health = Vitality.MaxHealth,
                            bool isDowned = false,
                            in ExpeditionOrder expedition = default,
                            in StationOrder station = default,
                            in KeptBunk bunk = default)
        {
            Expedition = expedition;
            Station = station;
            Bunk = bunk;
            DepartsAtTick = departsAtTick;
            Health = health;
            IsDowned = isDowned;
            ScoutReportBountyId = scoutReportBountyId;
            Personality = personality;
            AgentId = agentId;
            Position = position;
            Needs = needs;
            Frustration = frustration;
            Snapshot = snapshot;
            Memory = memory;
            CurrentTick = currentTick;
            DirectiveMembership = directiveMembership;
            Identity = identity;
            Inventory = inventory;
        }

        // Backward-compatible constructor
        public AgentContext(EntityId agentId, Vec2 position, float hunger, float energy, float frustration,
                            SharedWorldSnapshot snapshot, Simpiens.Cognition.Memory.IAgentMemory memory, uint currentTick)
            : this(agentId, position, new AgentNeeds(hunger, energy, 50f), frustration, snapshot, memory, currentTick, GroupMembership.None, IdentityTag.Default, null)
        {
        }
    }
}
