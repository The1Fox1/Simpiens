using Simpiens.Cognition.Inventory;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A crawler's stats as its equipment makes them (.agents/item_system_plan.md §3.7): a base value, then every
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

        private static EquipmentBlock Equipment(IAgentInventory inventory, int slot)
        {
            var item = inventory.GetEquipped((EquipmentSlot)slot);
            return item.IsEmpty ? null : ItemCatalog.Get(item.Type)?.Equipment;
        }
    }
}
