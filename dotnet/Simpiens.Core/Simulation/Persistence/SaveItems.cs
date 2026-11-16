using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// Every item a save holds must be one the game defines (ItemCatalog): a removed item's id is never reused, and a
    /// save that still holds one is refused on load, with where it was found (the item system refactor, 2026-10-26).
    /// </summary>
    public static class SaveItems
    {
        /// <summary>Where the save holds an item the catalog doesn't define ("item 77 in SwarmAgent_3's pack"), or null.</summary>
        public static string FirstUnknown(SessionSave save)
        {
            if (save == null) return null;
            if (save.Crawlers != null)
            {
                for (int c = 0; c < save.Crawlers.Length; c++)
                {
                    var crawler = save.Crawlers[c];
                    if (crawler == null) continue;
                    var slots = crawler.Inventory?.Slots;
                    if (slots != null)
                    {
                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (!slots[i].IsEmpty && Unknown(slots[i].Type)) return Where(slots[i].Type, $"{crawler.Name}'s pack");
                        }
                    }
                    if (crawler.Inventory != null)
                    {
                        var equipped = crawler.Inventory.Equipped;
                        for (int i = 0; i < Simpiens.Simulation.Items.EquipmentSlots.Capacity; i++)
                        {
                            if (!equipped[i].IsEmpty && Unknown(equipped[i].Type)) return Where(equipped[i].Type, $"{crawler.Name}'s equipment");
                        }
                    }
                    var sightings = crawler.Memory?.Sightings;
                    if (sightings != null)
                    {
                        for (int i = 0; i < sightings.Length; i++)
                        {
                            if (Unknown(sightings[i].ResourceKind)) return Where(sightings[i].ResourceKind, $"{crawler.Name}'s memory");
                        }
                    }
                }
            }
            var bounties = save.Economy?.Bounties;
            if (bounties != null)
            {
                for (int i = 0; i < bounties.Length; i++)
                {
                    if (Unknown(bounties[i].TargetItemType)) return Where(bounties[i].TargetItemType, $"bounty #{bounties[i].BountyId}");
                }
            }
            var world = save.World;
            if (world?.Resources != null)
            {
                for (int i = 0; i < world.Resources.Length; i++)
                {
                    if (Unknown(world.Resources[i].YieldItem)) return Where(world.Resources[i].YieldItem, "a resource node");
                }
            }
            if (world?.Harvested != null)
            {
                for (int i = 0; i < world.Harvested.Length; i++)
                {
                    if (Unknown(world.Harvested[i].Item)) return Where(world.Harvested[i].Item, "the harvest tally");
                }
            }
            var stacks = world?.Piles?.Stacks; // the items on the ground (Epic 10 Part 3, K4.1)
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    if (!stacks[i].IsEmpty && Unknown(stacks[i].Type)) return Where(stacks[i].Type, "a pile on the ground");
                }
            }
            return null;
        }

        /// <summary>
        /// Where the save holds a piece worn where this game can't wear it (Epic 10 Part 3, K1.2): in a slot the table doesn't
        /// define (retired, as protection's 0 is), or in a slot of another kind ("a HardHat in SwarmAgent_3's Body slot"); or
        /// null. Read after <see cref="FirstUnknown"/>, so every piece is a defined item.
        /// </summary>
        public static string FirstMisworn(SessionSave save)
        {
            if (save?.Crawlers == null) return null;
            for (int c = 0; c < save.Crawlers.Length; c++)
            {
                var crawler = save.Crawlers[c];
                if (crawler?.Inventory == null) continue;
                var equipped = crawler.Inventory.Equipped;
                for (int i = 0; i < Simpiens.Simulation.Items.EquipmentSlots.Capacity; i++)
                {
                    if (equipped[i].IsEmpty) continue;
                    var slot = (Simpiens.Simulation.Items.EquipmentSlot)i;
                    string item = ItemCatalog.NameOf(equipped[i].Type);
                    if (!Simpiens.Simulation.Items.EquipmentSlots.IsDefined(slot)) return $"a {item} in {crawler.Name}'s slot {i}, which this game doesn't have";
                    if (!Simpiens.Simulation.Items.EquipmentSlots.Fits(slot, equipped[i].Type)) return $"a {item} in {crawler.Name}'s {Simpiens.Simulation.Items.EquipmentSlots.NameOf(slot)} slot";
                }
            }
            return null;
        }

        // None is "no item" (an untyped node yields food), never unknown
        private static bool Unknown(ItemType item) => item != ItemType.None && !ItemCatalog.IsDefined(item);

        private static string Where(ItemType item, string place) => $"item {(byte)item} in {place}";
    }
}
