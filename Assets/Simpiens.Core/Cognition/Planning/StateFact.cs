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
        HasRespectedLeader = 16,
        HasAidedPeer = 17,
        KnowsAnchorLocation = 18,
        AtAnchorLocation = 19,
        IsGatheredAtAnchor = 20,
        HasActiveDirective = 21,
        AtDirectiveLocation = 22,
        DirectiveFulfilled = 23,
        IsGroupLeader = 24,
        HasActiveBounty = 25,
        AtTradeStall = 26,
        BountyDelivered = 27,
        HasProvisions = 28,
        HasSalvageInInventory = 29,
        KnowsSalvageLocation = 30,
        HasGatherBounty = 31,
        HasScoutBounty = 32,
        HasBountyItem = 33,
        HasScoutReport = 34,
        HasSoldSalvage = 35,
        KnowsBunkLocation = 36,
        AtBunk = 37
    }
}
