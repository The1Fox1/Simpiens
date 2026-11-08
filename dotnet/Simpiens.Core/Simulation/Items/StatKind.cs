namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A crawler's stats, as item effects and equipment name them (the item system refactor, 2026-10-26). The first five are
    /// today's needs and health; the rest are for equipment: HazardResistance (any hazard), its kinds' (Epic 10 Part 2, W3)
    /// and HarvestSpeed are read.
    /// </summary>
    public enum StatKind : byte
    {
        None = 0,
        Health = 1,
        Hunger = 2,
        Energy = 3,
        Social = 4,
        Frustration = 5,

        HazardResistance = 16,
        HarvestSpeed = 17,
        Attack = 18,
        Defense = 19,
        MoveSpeed = 20,

        /// <summary>Cuts the air hazards: foul air and flooding (a respirator, Epic 10 Part 2, D47).</summary>
        AirResistance = 21,
        /// <summary>Cuts falling rock (a hard hat, D47).</summary>
        RockResistance = 22
    }

    /// <summary>What consuming an item does to one stat: Hunger −40, Health +30.</summary>
    public readonly struct ConsumeEffect
    {
        public readonly StatKind Stat;
        public readonly float Amount;

        public ConsumeEffect(StatKind stat, float amount)
        {
            Stat = stat;
            Amount = amount;
        }

        public override string ToString() => $"{Stat} {Amount:+0.##;-0.##}";
    }
}
