namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Discrete world and agent state facts used by the GOAP planning system.
    /// Values correspond to bit positions in the 64-bit WorldState bitmask.
    /// </summary>
    public enum StateFact : byte
    {
        IsHungry = 0,
        HasFoodInInventory = 1,
        KnowsResourceLocation = 2,
        AtResourceLocation = 3,
        ResourceHarvested = 4,
        IsNearPeer = 5,
        HasExchangedGossip = 6,
        HasTool = 7,
        HasWeapon = 8,
        IsThreatened = 9,
        AtHomeLocation = 10,
        IsResting = 11,
        IsStressed = 12,
        HasWarmCompanion = 13,
        HasTrustedPeer = 14,
        HasFearedThreat = 15,
        HasRespectedLeader = 16
    }
}
