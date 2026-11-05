using Simpiens.Cognition.Inventory;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A crawler's stats as its equipment makes them (the item system refactor, 2026-10-26): a base value, then every
    /// equipped item's Add modifiers, then its Multiply modifiers, in slot order (deterministic). Code asks here
    /// ("HazardResistance") instead of reading a tuning.
    /// </summary>
    public static class CrawlerStats
    {
        public static float Get(IAgentInventory inventory, StatKind stat, float baseValue = 0f)
        {
            float value = baseValue;
            if (inventory == null) return value;
            for (int s = 0; s < AgentInventory.EquipmentSlots; s++)
            {
                var equipment = Equipment(inventory, s);
                if (equipment == null) continue;
                for (int m = 0; m < equipment.ModifierCount; m++)
                {
                    var modifier = equipment.GetModifier(m);
                    if (modifier.Stat == stat && modifier.Op == ModifierOp.Add) value += modifier.Value;
                }
            }
            for (int s = 0; s < AgentInventory.EquipmentSlots; s++)
            {
                var equipment = Equipment(inventory, s);
                if (equipment == null) continue;
                for (int m = 0; m < equipment.ModifierCount; m++)
                {
                    var modifier = equipment.GetModifier(m);
                    if (modifier.Stat == stat && modifier.Op == ModifierOp.Multiply) value *= modifier.Value;
                }
            }
            return value;
        }

        /// <summary>
        /// How much faster this inventory's wearer mines <paramref name="item"/> (Epic 10 Part 2, W1.4, D36): its
        /// <see cref="StatKind.HarvestSpeed"/> (1.5 with a pickaxe) for a mineral; 1 for anything else, and with no inventory.
        /// </summary>
        public static float MiningSpeed(IAgentInventory inventory, Simpiens.Simulation.Economy.ItemType item)
        {
            if (inventory == null || !ItemCatalog.Is(item, ItemCategory.Mineral)) return 1f;
            float speed = Get(inventory, StatKind.HarvestSpeed, 1f);
            return speed > 0.1f ? speed : 0.1f;
        }

        private static EquipmentBlock Equipment(IAgentInventory inventory, int slot)
        {
            var item = inventory.GetEquipped((EquipmentSlot)slot);
            return item.IsEmpty ? null : ItemCatalog.Get(item.Type)?.Equipment;
        }
    }
}
