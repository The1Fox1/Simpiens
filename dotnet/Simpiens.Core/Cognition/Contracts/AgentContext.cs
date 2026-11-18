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

        /// <summary>When a creature last swung at it, hit or miss (Epic 10 Part 3, K2.3); 0 for never. Flight reads it.</summary>
        public readonly uint StruckAtTick;

        /// <summary>The creature whose blow fell on it last (K3.3: whom it fights back).</summary>
        public readonly EntityId StruckBy;

        /// <summary>Downed in danger, the tick it bleeds out (Epic 10 Part 3, K5.1); 0 while it isn't bleeding.</summary>
        public readonly uint BleedsOutAt;

        /// <summary>Its party's group, if it is in one (Epic 10 Part 3, K5.3: party mates come for each other); 0 for none.</summary>
        public readonly byte PartyGroupId;

        /// <summary>Its skills (Epic 10 Part 3, K9.4): a copy, for what its own trade pays it an hour.</summary>
        public readonly Simpiens.Simulation.Skills.SkillSet Skills;

        /// <summary>Who it has become (Epic 11, A3): a copy of its traits record, for what its passions weigh (S1).</summary>
        public readonly Simpiens.Simulation.Skills.CrawlerTraits Traits;

        /// <summary>The player's guild it belongs to, its group id, or 0 (Epic 11, S3.4: a member may drill at the guildhall's yard).</summary>
        public readonly byte GuildGroupId;
        public bool IsGuildMember => GuildGroupId != 0;

        /// <summary>Collapsed in danger, its clock running (K5.1): it lies where it fell.</summary>
        public bool IsCollapsed => IsDowned && BleedsOutAt != 0;

        /// <summary>True when a creature's blow fell on it within <see cref="Vitality.FlightSeconds"/>: it flees.</summary>
        public bool StruckRecently => Vitality.StruckRecently(StruckAtTick, CurrentTick);

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
                            in KeptBunk bunk = default,
                            uint struckAtTick = 0,
                            EntityId struckBy = default,
                            uint bleedsOutAt = 0,
                            byte partyGroupId = 0,
                            in Simpiens.Simulation.Skills.SkillSet skills = default,
                            in Simpiens.Simulation.Skills.CrawlerTraits traits = default,
                            byte guildGroupId = 0)
        {
            GuildGroupId = guildGroupId;
            Skills = skills;
            Traits = traits;
            BleedsOutAt = bleedsOutAt;
            PartyGroupId = partyGroupId;
            StruckAtTick = struckAtTick;
            StruckBy = struckBy;
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
