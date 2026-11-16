using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Loot
{
    /// <summary>
    /// What a crawler takes from a pile (Epic 10 Part 3, K4.3c, D24): the one rule the take (<see cref="PileService.Take"/>)
    /// and cognition's choice of a pile share, so nobody walks to a pile it would take nothing from.
    /// <list type="bullet">
    /// <item>Materials, food and medicine: what fits its pack.</item>
    /// <item>The purse, whole.</item>
    /// <item>A piece of equipment only when wearing it outfits the taker better for fighting and against every hazard
    /// (<see cref="Need"/>), by stats (K1.4); it is worn at once, and what it replaces is left in the pile in its place.
    /// Nobody carries gear in its pack to no purpose: the stall doesn't buy it.</item>
    /// </list>
    /// </summary>
    public static class LootRules
    {
        /// <summary>What a found piece is judged for: fighting, and every hazard, so armour never displaces hazard gear that does more.</summary>
        public static OutfitNeed Need { get; } = OutfitNeed.EveryHazard.AndFighting();

        /// <summary>True when the taker would take a unit of <paramref name="item"/> from a pile: room in its pack, or a piece it would wear.</summary>
        public static bool WouldTake(IAgentInventory inventory, ItemType item)
        {
            if (inventory == null || item == ItemType.None) return false;
            if (ItemCatalog.Get(item)?.Equipment != null) return WouldWear(inventory, Outfitting.NewPiece(item, Grades.Standard), out _);
            return HasRoomFor(inventory, item);
        }

        /// <summary>True when the taker would take anything from <paramref name="pile"/>: its purse, or an item it would take.</summary>
        public static bool WouldTakeAny(IAgentInventory inventory, in PileView pile)
        {
            if (pile.Gold > 0) return true;
            if (pile.HoldsNoItems) return false;
            var items = ItemCatalog.All;
            for (int i = 0; i < items.Count; i++)
            {
                if (pile.Holds(items[i].Id) && WouldTake(inventory, items[i].Id)) return true;
            }
            return false;
        }

        /// <summary>
        /// True when wearing <paramref name="piece"/> outfits the taker better than what its slot holds (<see cref="Need"/>);
        /// <paramref name="slot"/> is where it would go (<see cref="WornGear.TryReplacing"/>).
        /// </summary>
        public static bool WouldWear(IAgentInventory inventory, in ItemInstance piece, out EquipmentSlot slot)
        {
            slot = default;
            if (inventory == null || piece.IsEmpty || !WornGear.TryReplacing(inventory, piece.Type, out slot)) return false;
            return Outfitting.Gain(inventory, Need, slot, piece.WithQuantity(1)) > 0f;
        }

        /// <summary>True when a unit of <paramref name="item"/> fits the pack: a stack of it with room, or an empty slot.</summary>
        public static bool HasRoomFor(IAgentInventory inventory, ItemType item)
        {
            ushort maxStack = ItemCatalog.Get(item)?.MaxStack ?? 1;
            for (int i = 0; i < inventory.Capacity; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot.IsEmpty || (slot.Type == item && slot.Quality == 0 && slot.Quantity < maxStack)) return true;
            }
            return false;
        }
    }
}
