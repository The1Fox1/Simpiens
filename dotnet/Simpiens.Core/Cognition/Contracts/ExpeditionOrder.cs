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

        public ExpeditionOrder(ExpeditionStage stage, byte targetSectorId, EntityId nodeId, Vec2 nodePosition)
        {
            Stage = stage;
            TargetSectorId = targetSectorId;
            NodeId = nodeId;
            NodePosition = nodePosition;
        }

        public bool IsActive => Stage != ExpeditionStage.None;

        /// <summary>At work, with a node to work.</summary>
        public bool IsWorking => Stage == ExpeditionStage.Work && NodeId != default;
    }
}
