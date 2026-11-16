namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A crawler's stats, as item effects and equipment name them (the item system refactor, 2026-10-26). The first five are
    /// today's needs and health; the rest are for equipment: HazardResistance (any hazard), its kinds' (Epic 10 Part 2, W3)
    /// and HarvestSpeed are read, and the combat rule reads Attack, Defense, Reach, AttackInterval and Range (Epic 10
    /// Part 3, K0.3).
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
        RockResistance = 22,

        /// <summary>How far a blow reaches across the gap between two bodies, in world units (Epic 10 Part 3, K0.3; base 0.5).</summary>
        Reach = 23,
        /// <summary>Seconds between two swings (K0.3; base 1.5): a quick weapon multiplies it down.</summary>
        AttackInterval = 24,
        /// <summary>How far a shot carries, in world units (K0.3; 0 for no ranged attack, until ranged weapons, K8).</summary>
        Range = 25
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
