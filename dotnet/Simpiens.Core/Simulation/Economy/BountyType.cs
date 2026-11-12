namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Classifies the type of task required by a public bounty contract.
    /// Byte-backed for zero-allocation memory packing.
    /// </summary>
    public enum BountyType : byte
    {
        None = 0,
        ScoutSector = 1,
        GatherResource = 2,
        Hunt = 3 // Epic 10 Part 3, K6: on a charted lair, claimed by up to three, paid by damage when it is empty
    }
}
