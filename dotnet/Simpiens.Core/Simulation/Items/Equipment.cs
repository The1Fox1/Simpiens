using System;

namespace Simpiens.Simulation.Items
{
    /// <summary>Where a crawler wears or wields an item: one item per slot (the item system refactor, 2026-10-26).</summary>
    public enum EquipmentSlot : byte
    {
        Protection = 0,
        Weapon = 1,
        Tool = 2
    }

    /// <summary>What wears an item out: nothing, each tick in a hazard sector (hazard gear), or each use (reserved: a tool per harvest, a weapon per hit).</summary>
    public enum WearRule : byte
    {
        None = 0,
        PerHazardTick = 1,
        PerUse = 2
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

    /// <summary>An equipment item's slot, stat modifiers and how long it lasts.</summary>
    public sealed class EquipmentBlock
    {
        private readonly StatModifier[] _modifiers;

        public EquipmentBlock(EquipmentSlot slot, ushort durability, WearRule wear, params StatModifier[] modifiers)
        {
            Slot = slot;
            Durability = durability;
            Wear = wear;
            _modifiers = modifiers != null ? (StatModifier[])modifiers.Clone() : Array.Empty<StatModifier>();
        }

        public EquipmentSlot Slot { get; }

        /// <summary>A new item's durability: the units of wear it takes before it breaks (for PerHazardTick, ticks in danger).</summary>
        public ushort Durability { get; }

        public WearRule Wear { get; }

        public int ModifierCount => _modifiers.Length;
        public StatModifier GetModifier(int index) => _modifiers[index];
    }
}
