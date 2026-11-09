using Simpiens.Cognition.Inventory;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A crawler's stats as its equipment makes them (the item system refactor, 2026-10-26): a base value, then every
    /// equipped item's Add modifiers, then its Multiply modifiers, over every slot of the table in id order (deterministic;
    /// Epic 10 Part 3, K1.1), held within the stat's bounds (<see cref="StatRules"/>, K1.3). Code asks here
    /// ("HazardResistance") instead of reading a tuning.
    /// </summary>
    public static class CrawlerStats
    {
        public static float Get(IAgentInventory inventory, StatKind stat, float baseValue = 0f)
        {
            float value = baseValue;
            if (inventory == null) return value;
            // Each worn piece's modifiers at its grade (Epic 10 Part 2, W4, D56)
            var slots = EquipmentSlots.All;
            for (int s = 0; s < slots.Count; s++)
            {
                var equipment = Equipment(inventory, slots[s].Id, out byte grade);
                if (equipment == null) continue;
                for (int m = 0; m < equipment.ModifierCount; m++)
                {
                    var modifier = equipment.GetModifier(m);
                    if (modifier.Stat == stat && modifier.Op == ModifierOp.Add) value += equipment.ValueAt(modifier, grade);
                }
            }
            for (int s = 0; s < slots.Count; s++)
            {
                var equipment = Equipment(inventory, slots[s].Id, out byte grade);
                if (equipment == null) continue;
                for (int m = 0; m < equipment.ModifierCount; m++)
                {
                    var modifier = equipment.GetModifier(m);
                    if (modifier.Stat == stat && modifier.Op == ModifierOp.Multiply) value *= equipment.ValueAt(modifier, grade);
                }
            }
            return StatRules.Of(stat).Bound(value);
        }

        /// <summary>
        /// How much faster this inventory's wearer mines <paramref name="item"/> (Epic 10 Part 2, W1.4, D36): its
        /// <see cref="StatKind.HarvestSpeed"/> (1.5 with a pickaxe) for a mineral; 1 for anything else, and with no inventory.
        /// </summary>
        public static float MiningSpeed(IAgentInventory inventory, Simpiens.Simulation.Economy.ItemType item)
        {
            if (inventory == null || !ItemCatalog.Is(item, ItemCategory.Mineral)) return 1f;
            return Get(inventory, StatKind.HarvestSpeed, 1f); // at least 0.1, by the stat's rule
        }

        /// <summary>The most of a hazard's harm worn protection can stop (D47): the resistances' bound (<see cref="StatRules.MaxResistance"/>).</summary>
        public const float MaxHazardResistance = StatRules.MaxResistance;

        /// <summary>
        /// How much of a hazard of <paramref name="kind"/>'s harm this inventory's wearer is spared (Epic 10 Part 2, W3, D47):
        /// its <see cref="StatKind.HazardResistance"/> (hazard gear's 0.5, against any hazard) plus its resistance to that
        /// kind (a respirator's 0.75 against the air hazards, a hard hat's against falling rock), at most
        /// <see cref="MaxHazardResistance"/>. Since K1.3 both may be worn at once (hazard gear on the body, a hard hat on the
        /// head): 0.5 and 0.75 together stop the most, 0.9.
        /// </summary>
        public static float HazardResistance(IAgentInventory inventory, Simpiens.Simulation.World.HazardKind kind)
        {
            float resistance = Get(inventory, StatKind.HazardResistance) + KindResistance(inventory, kind);
            return resistance < MaxHazardResistance ? resistance : MaxHazardResistance;
        }

        /// <summary>What gear made against <paramref name="kind"/> stops of it, apart from protection against any hazard (D51 judges by this).</summary>
        public static float KindResistance(IAgentInventory inventory, Simpiens.Simulation.World.HazardKind kind)
        {
            var stat = StatAgainst(kind);
            return stat == StatKind.None ? 0f : Get(inventory, stat);
        }

        /// <summary>What <paramref name="item"/>, worn, stops of a hazard of <paramref name="kind"/> by its kind alone, from its definition (D51).</summary>
        public static float KindResistanceOf(Simpiens.Simulation.Economy.ItemType item, Simpiens.Simulation.World.HazardKind kind)
        {
            var stat = StatAgainst(kind);
            var equipment = stat != StatKind.None && item != Simpiens.Simulation.Economy.ItemType.None ? ItemCatalog.Get(item)?.Equipment : null;
            if (equipment == null) return 0f;
            float value = 0f;
            for (int m = 0; m < equipment.ModifierCount; m++)
            {
                var modifier = equipment.GetModifier(m);
                if (modifier.Stat == stat && modifier.Op == ModifierOp.Add) value += modifier.Value;
            }
            return value;
        }

        /// <summary>The stat that cuts a hazard of <paramref name="kind"/>: the air hazards' or falling rock's; None for none.</summary>
        public static StatKind StatAgainst(Simpiens.Simulation.World.HazardKind kind) => kind switch
        {
            Simpiens.Simulation.World.HazardKind.FoulAir => StatKind.AirResistance,
            Simpiens.Simulation.World.HazardKind.Flooding => StatKind.AirResistance,
            Simpiens.Simulation.World.HazardKind.FallingRock => StatKind.RockResistance,
            _ => StatKind.None
        };

        private static EquipmentBlock Equipment(IAgentInventory inventory, EquipmentSlot slot, out byte grade)
        {
            var item = inventory.GetEquipped(slot);
            grade = item.IsEmpty ? Grades.Standard : item.Quality;
            return item.IsEmpty ? null : ItemCatalog.Get(item.Type)?.Equipment;
        }
    }
}
