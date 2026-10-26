namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// What hazard gear does (Epic 10 G3; part of EconomyTuning). Worn gear cuts the damage a hazard sector deals, and
    /// wears a tick of durability for every tick its wearer spends in one; at 0 it breaks and is gone, so it is bought
    /// again (the crawlers' own gold sink, Epic 9 D2). The stall's price and the caravan's supply are economy terms
    /// (EconomyTuning, SupplyTuning).
    /// </summary>
    public readonly struct GearTuning
    {
        /// <summary>The share of hazard damage worn gear stops (0.5: half).</summary>
        public readonly float DamageReduction;

        /// <summary>Ticks of hazard exposure a new piece of gear lasts: its starting durability (500: 50 s).</summary>
        public readonly ushort LifeTicks;

        public GearTuning(float damageReduction, ushort lifeTicks)
        {
            DamageReduction = damageReduction;
            LifeTicks = lifeTicks;
        }

        public static GearTuning Default => new GearTuning(damageReduction: 0.5f, lifeTicks: 500);

        /// <summary>The damage per second a hazard deals through worn gear, or bare when <paramref name="geared"/> is false.</summary>
        public float DamageThrough(float damagePerSecond, bool geared) => geared ? (float)(damagePerSecond * (float)(1f - DamageReduction)) : damagePerSecond;
    }
}
