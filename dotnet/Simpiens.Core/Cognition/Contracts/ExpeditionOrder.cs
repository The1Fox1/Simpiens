using Simpiens.Simulation.Guild;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A crawler's part in the guild's expedition (Epic 10 G5), copied from the guild every tick for cognition, as its
    /// memberships are: the stage, the sector, and at work the node it claimed. Default when it isn't with a party.
    /// </summary>
    public readonly struct ExpeditionOrder
    {
        public readonly ExpeditionStage Stage;
        public readonly byte TargetSectorId;
        public readonly EntityId NodeId;
        public readonly Vec2 NodePosition;

        /// <summary>A clear (Epic 10 Part 3, K7.1): its lair, and at the fight the party's focus, the creature it fights now (K7.2a).</summary>
        public readonly ExpeditionKind Kind;
        public readonly byte LairIndex;
        public readonly EntityId Focus;

        /// <summary>Rescue under orders (K7.2c, D42): the fallen mate this member is sent to carry out; default for the rest, who hold.</summary>
        public readonly EntityId Carry;

        /// <summary>Its calling's part in the party (Epic 11, S5.4, D22): front, back, carrier or leader; none for the rest.</summary>
        public readonly Simpiens.Simulation.Skills.PartyRole Role;

        public ExpeditionOrder(ExpeditionStage stage, byte targetSectorId, EntityId nodeId, Vec2 nodePosition,
            ExpeditionKind kind = ExpeditionKind.Gather, byte lairIndex = 0, EntityId focus = default, EntityId carry = default,
            Simpiens.Simulation.Skills.PartyRole role = Simpiens.Simulation.Skills.PartyRole.None)
        {
            Role = role;
            Stage = stage;
            TargetSectorId = targetSectorId;
            NodeId = nodeId;
            NodePosition = nodePosition;
            Kind = kind;
            LairIndex = lairIndex;
            Focus = focus;
            Carry = carry;
        }

        public bool IsActive => Stage != ExpeditionStage.None;

        /// <summary>At a clear's fight (K7.1): it fights the lair's creatures under orders.</summary>
        public bool IsFighting => Stage == ExpeditionStage.Fight && Kind == ExpeditionKind.Clear;

        /// <summary>At a clear's fight holding the front (Epic 11, S5.4, D22): the creatures are drawn onto it.</summary>
        public bool HoldsTheFront => IsFighting && Role == Simpiens.Simulation.Skills.PartyRole.Front;

        /// <summary>At work, with a node to work.</summary>
        public bool IsWorking => Stage == ExpeditionStage.Work && NodeId != default;
    }
}
