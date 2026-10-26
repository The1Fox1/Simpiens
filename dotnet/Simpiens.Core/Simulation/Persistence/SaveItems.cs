using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// Every item a save holds must be one the game defines (ItemCatalog): a removed item's id is never reused, and a
    /// save that still holds one is refused on load, with where it was found (.agents/item_system_plan.md §3.1).
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
                    var equipped = crawler.Inventory?.Equipped;
                    if (equipped != null)
                    {
                        for (int i = 0; i < equipped.Length; i++)
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
            return null;
        }

        // None is "no item" (an untyped node yields food), never unknown
        private static bool Unknown(ItemType item) => item != ItemType.None && !ItemCatalog.IsDefined(item);

        private static string Where(ItemType item, string place) => $"item {(byte)item} in {place}";
    }
}
