namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// A creature's kind, by id (Epic 10 Part 3, K0.4; the draft's Q5, placeholder names). Each creature will be saved with
    /// its kind (K2.4), so a value is never reused: a kind taken out of the game keeps its number, marked retired, and a save
    /// holding it is refused. What each kind is lives in <see cref="CreatureCatalog"/>.
    /// </summary>
    public enum CreatureKind : byte
    {
        None = 0,

        /// <summary>Weak, in packs of three or four, and they flee: the Overgrowth's vermin.</summary>
        BurrowRat = 1,

        /// <summary>Armoured and slow, where a weapon matters: the Deep Seam's.</summary>
        StoneBeetle = 2,

        /// <summary>Spits from range, on the gate's approach (ranged combat is K8; the approach K10).</summary>
        CaveSpinner = 3,

        /// <summary>A boss (Epic 10 Part 3, K10, Q13): heads the rats' warren, calls her brood at half health, returns after a game week.</summary>
        Matriarch = 4,

        /// <summary>A boss (K10, Q13): holds the sealed descent gate in the Old Stair; its slam strikes all in reach; beaten once, it stays beaten.</summary>
        Gatekeeper = 5
    }
}
