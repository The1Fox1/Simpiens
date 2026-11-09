using System;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// What wears a worn piece out, whichever slot it is in (Epic 10 Part 3, K1.3): each piece wears by its own rule, and
    /// every piece with the rule wears when its event comes (<see cref="Simpiens.Cognition.Inventory.AgentInventory.WearBy"/>).
    /// Not saved: a value may be renamed, but keep each one's meaning.
    /// </summary>
    public enum WearRule : byte
    {
        None = 0,
        /// <summary>A tick in a hazard sector (hazard gear, a respirator, a hard hat; Epic 10 G3).</summary>
        PerHazardTick = 1,
        /// <summary>A mineral unit mined (a pickaxe; Epic 10 Part 2, D37).</summary>
        PerHarvest = 2,
        /// <summary>A blow landed (a weapon; Q10, the combat rule's D7).</summary>
        PerHit = 3,
        /// <summary>A blow taken (armour; D7 leaves it to K1.3 and K3).</summary>
        PerBlowTaken = 4
    }

    public enum ModifierOp : byte
    {
        Add = 0,
        Multiply = 1
    }

    /// <summary>What wearing an item does to one stat: HazardResistance +0.5, Attack +3.</summary>
    public readonly struct StatModifier
    {
        public readonly StatKind Stat;
        public readonly ModifierOp Op;
        public readonly float Value;

        public StatModifier(StatKind stat, ModifierOp op, float value)
        {
            Stat = stat;
            Op = op;
            Value = value;
        }

        public override string ToString() => Op == ModifierOp.Add ? $"{Stat} {Value:+0.##;-0.##}" : $"{Stat} x{Value:0.##}";
    }

    /// <summary>An equipment item's kind of slot, stat modifiers and how long it lasts.</summary>
    public sealed class EquipmentBlock
    {
        private readonly StatModifier[] _modifiers;

        public EquipmentBlock(SlotKind kind, ushort durability, WearRule wear, params StatModifier[] modifiers)
        {
            Kind = kind;
            Durability = durability;
            Wear = wear;
            _modifiers = modifiers != null ? (StatModifier[])modifiers.Clone() : Array.Empty<StatModifier>();
        }

        /// <summary>
        /// The kind of slot it is worn in (Epic 10 Part 3, K1.1): a kind, not a slot, so it fits any slot of the kind
        /// (<see cref="EquipmentSlots"/>).
        /// </summary>
        public SlotKind Kind { get; }

        /// <summary>A new item's durability: the units of wear it takes before it breaks (for PerHazardTick, ticks in danger; for PerHit, blows landed).</summary>
        public ushort Durability { get; }

        public WearRule Wear { get; }

        public int ModifierCount => _modifiers.Length;
        public StatModifier GetModifier(int index) => _modifiers[index];

        /// <summary>How much longer each grade above Standard makes a piece last (Epic 10 Part 2, W4, D56): a quarter.</summary>
        public float LifePerGrade { get; init; } = 0.25f;

        /// <summary>How much stronger each grade above Standard makes its effect (D56): a tenth.</summary>
        public float EffectPerGrade { get; init; } = 0.1f;

        /// <summary>A new piece's durability at <paramref name="grade"/>: a Fine pickaxe lasts 50 units, a Standard one 40.</summary>
        public ushort DurabilityAt(byte grade)
        {
            float life = Durability * (1f + LifePerGrade * grade);
            return (ushort)System.Math.Min(ushort.MaxValue, (int)System.Math.Round(life));
        }

        /// <summary>A modifier's value on a piece of <paramref name="grade"/>: an Add a tenth more a grade, a Multiply's bonus a tenth more a grade.</summary>
        public float ValueAt(in StatModifier modifier, byte grade)
        {
            if (grade == 0) return modifier.Value;
            float step = 1f + EffectPerGrade * grade;
            return modifier.Op == ModifierOp.Multiply ? 1f + (modifier.Value - 1f) * step : modifier.Value * step;
        }
    }
}
