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
        GatherResource = 2
    }
}
