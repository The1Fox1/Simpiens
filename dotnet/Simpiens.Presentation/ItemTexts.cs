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
            _texts[(int)ItemType.Ration] = new ItemText("Provision", "(Outpost sells rations to hungry agents)", "Rations", "Ration", "RATIONS");
            _texts[(int)ItemType.IronOre] = new ItemText("Iron Ore", "(Outpost purchases ore from salvagers)", "Ore", "Ore", "ORE");
            _texts[(int)ItemType.MedicinalHerb] = new ItemText("Herb", "(Outpost purchases medicinal flora)", "Herb", "Herb", "HERB");
            _texts[(int)ItemType.Glowstone] = new ItemText("Glowstone", "(Rare salvage from the hazard sectors)", "Glowstone", "Glowstone", "GLOWSTONE");
            _texts[(int)ItemType.HazardGear] = new ItemText("Hazard Gear", "(Worn into danger: halves the harm, then wears out)", "Gear", "Gear", "GEAR");
        }

        public static ItemText Of(ItemType item)
        {
            var text = _texts[(byte)item];
            if (text.MerchantName != null) return text;
            string name = ItemCatalog.Get(item)?.DisplayName ?? ItemCatalog.NameOf(item);
            return new ItemText(name, string.Empty, name, name, name.ToUpperInvariant());
        }

        /// <summary>"Provision Sell Price:", "Iron Ore Buy Price:".</summary>
        public static string PriceLabel(ItemType item, StallRole role) => $"{Of(item).MerchantName} {(role == StallRole.Sells ? "Sell" : "Buy")} Price:";
    }
}
