namespace Simpiens.Simulation.World
{
    /// <summary>
    /// What has happened to the world that stays happened (Epic 10 Part 3, K10.4): flags the simulation owns, saved in the
    /// world's block and held by the world hash. A flag is set by the rule that earns it and never cleared but by a new session.
    /// A new flag takes the next bit; a bit is never reused.
    /// </summary>
    [System.Flags]
    public enum WorldFlags : ulong
    {
        None = 0,

        /// <summary>The descent gate is breached (Q13): its keeper was slain, and stays beaten (the Old Stair's stairs go down into the dark, for the descent).</summary>
        GateBreached = 1UL << 0,
    }
}
