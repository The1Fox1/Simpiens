namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// The bunk the guild keeps for a member in its barracks (Epic 10 Part 2, W5.2, D62), copied from the guild every tick
    /// for cognition, as its expedition order is, with where it stands (cognition's snapshot can't look an entity up).
    /// Default when it has none.
    /// </summary>
    public readonly struct KeptBunk
    {
        public readonly EntityId BunkId;
        public readonly Vec2 Position;

        public KeptBunk(EntityId bunkId, Vec2 position)
        {
            BunkId = bunkId;
            Position = position;
        }

        public bool IsKept => BunkId != default;
    }
}
