using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation
{
    /// <summary>
    /// What the client calls an item where it trades (item system I6): the merchant row's name and hint, the word in the
    /// stock line, a short word, and the header's word.
    /// </summary>
    public readonly struct ItemText
    {
        public readonly string MerchantName;
        public readonly string MerchantHint;
        public readonly string StockWord;
        public readonly string ShortWord;
        public readonly string HeaderWord;

        public ItemText(string merchantName, string merchantHint, string stockWord, string shortWord, string headerWord)
        {
            MerchantName = merchantName;
            MerchantHint = merchantHint;
            StockWord = stockWord;
            ShortWord = shortWord;
            HeaderWord = headerWord;
        }
    }

    /// <summary>
    /// The client's words for items, by id: content, like the catalog. An item without an entry gets words made from its
    /// catalog name, so a new item shows without a client change; an entry gives it bespoke copy.
    /// </summary>
    public static class ItemTexts
    {
        private static readonly ItemText[] _texts = new ItemText[ItemCatalog.Capacity];

        static ItemTexts()
        {
            _texts[(int)ItemType.Ration] = new ItemText("Rations", "Sold to hungry crawlers", "Rations", "Ration", "RATIONS");
            _texts[(int)ItemType.IronOre] = new ItemText("Iron ore", "Bought from salvagers", "Ore", "Ore", "ORE");
            _texts[(int)ItemType.MedicinalHerb] = new ItemText("Herbs", "Bought from foragers", "Herb", "Herb", "HERB");
            _texts[(int)ItemType.Glowstone] = new ItemText("Glowstone", "Rare salvage from the hazards", "Glowstone", "Glowstone", "GLOWSTONE");
            _texts[(int)ItemType.HazardGear] = new ItemText("Hazard gear", "Worn into danger: halves the harm, then wears out", "Gear", "Gear", "GEAR");
            _texts[(int)ItemType.ToolPickaxe] = new ItemText("Pickaxe", "Made at the forge from three ore", "Pickaxe", "Pickaxe", "PICKAXE");
            _texts[(int)ItemType.HealingDraught] = new ItemText("Healing draught", "Made at the still from two herbs; heals 80 at once", "Draught", "Draught", "DRAUGHT");
        }

        public static ItemText Of(ItemType item)
        {
            var text = _texts[(byte)item];
            if (text.MerchantName != null) return text;
            string name = ItemCatalog.Get(item)?.DisplayName ?? ItemCatalog.NameOf(item);
            return new ItemText(name, string.Empty, name, name, name.ToUpperInvariant());
        }

        /// <summary>A merchant row's name: "Rations", "Iron ore" (the UI polish plan, U6: the hint says sold or bought).</summary>
        public static string PriceLabel(ItemType item, StallRole role) => Of(item).MerchantName;

        /// <summary>A merchant row's hint: the item's own words, or whether the stall sells or buys it.</summary>
        public static string PriceHint(ItemType item, StallRole role)
        {
            string hint = Of(item).MerchantHint;
            return !string.IsNullOrEmpty(hint) ? hint : role == StallRole.Sells ? "Sold by the stall" : "Bought by the stall";
        }
    }
}
