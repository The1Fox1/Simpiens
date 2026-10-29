namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A crawler's stats, as item effects and equipment name them (the item system refactor, 2026-10-26). The first five are
    /// today's needs and health; the rest are reserved for equipment, and only HazardResistance is read so far.
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
        MoveSpeed = 20
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
