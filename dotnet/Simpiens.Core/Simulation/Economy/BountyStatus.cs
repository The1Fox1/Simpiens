namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Lifecycle state of a public bounty contract.
    /// Byte-backed for unmanaged struct alignment and zero GC allocations.
    /// </summary>
    public enum BountyStatus : byte
    {
        Open = 0,
        Claimed = 1,
        Completed = 2,
        Expired = 3,
        Cancelled = 4
    }
}
