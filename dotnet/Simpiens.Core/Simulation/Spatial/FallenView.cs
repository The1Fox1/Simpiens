namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// A crawler collapsed in danger as cognition sees it (Epic 10 Part 3, K5.3): a copy in every snapshot, taken on the main
    /// thread, so a rescuer reads no live state. Where it lies, when it bleeds out, who carries it (if anyone) and its party.
    /// </summary>
    public readonly struct FallenView
    {
        public readonly EntityId Id;
        public readonly Vec2 Position;
        public readonly uint BleedsOutAt;
        public readonly EntityId CarriedBy;
        public readonly byte PartyGroupId;

        public FallenView(EntityId id, Vec2 position, uint bleedsOutAt, EntityId carriedBy, byte partyGroupId)
        {
            Id = id;
            Position = position;
            BleedsOutAt = bleedsOutAt;
            CarriedBy = carriedBy;
            PartyGroupId = partyGroupId;
        }
    }
}
