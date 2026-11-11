namespace Simpiens.Simulation
{
    /// <summary>
    /// Why a crawler left the world (Epic 10 Part 3, K5.4a): it walked out, or it died of what. Saved in the departed list
    /// by value: never reorder or reuse one.
    /// </summary>
    public enum DeathCause : byte
    {
        /// <summary>It walked out by the Road Gate (a drifter's departure): no death.</summary>
        None = 0,

        /// <summary>It bled out, downed by a hazard where it lay (K5.1); the detail is the hazard's kind.</summary>
        Hazard = 1,

        /// <summary>It bled out, downed by a creature's blows (K5.1); the detail is the creature's kind.</summary>
        Creature = 2,
    }
}
