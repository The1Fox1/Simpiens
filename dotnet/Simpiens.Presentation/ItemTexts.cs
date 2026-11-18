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
            _texts[(int)ItemType.ToolPickaxe] = new ItemText("Pickaxe", "Forged from three ore", "Pickaxe", "Pickaxe", "PICKAXE");
            _texts[(int)ItemType.HealingDraught] = new ItemText("Healing draught", "Made at the still from two herbs; heals 80 at once", "Draught", "Draught", "DRAUGHT");
            _texts[(int)ItemType.Respirator] = new ItemText("Respirator", "Forged; for bad air", "Respirator", "Respirator", "RESPIRATOR");
            _texts[(int)ItemType.HardHat] = new ItemText("Hard hat", "Forged; for falling rock", "Hard hat", "Hard hat", "HARD HAT");
            // Melee arms and armour (Epic 10 Part 3, K3.1, K3.2)
            _texts[(int)ItemType.Cudgel] = new ItemText("Cudgel", "From the caravan; a cheap club", "Cudgel", "Cudgel", "CUDGEL");
            _texts[(int)ItemType.IronBlade] = new ItemText("Iron blade", "Forged from three ore", "Iron blade", "Iron blade", "IRON BLADE");
            _texts[(int)ItemType.Sling] = new ItemText("Sling", "From the caravan; a cheap sling", "Sling", "Sling", "SLING");
            _texts[(int)ItemType.Crossbow] = new ItemText("Crossbow", "Forged from ore and a hide", "Crossbow", "Crossbow", "CROSSBOW");
            // Manuals (Epic 11, S3.2): read at rest, teaching a skill to Apprentice's door
            _texts[(int)ItemType.MeleePrimer] = new ItemText("Primer of the Blade", "From the caravan; teaches melee to 3", "Blade primer", "Primer", "BLADE PRIMER");
            _texts[(int)ItemType.RangedPrimer] = new ItemText("Primer of the Sling", "From the caravan; teaches ranged to 3", "Sling primer", "Primer", "SLING PRIMER");
            _texts[(int)ItemType.MiningPrimer] = new ItemText("Primer of the Seam", "From the caravan; teaches mining to 3", "Seam primer", "Primer", "SEAM PRIMER");
            _texts[(int)ItemType.ForagingPrimer] = new ItemText("Primer of the Grove", "From the caravan; teaches foraging to 3", "Grove primer", "Primer", "GROVE PRIMER");
            _texts[(int)ItemType.SmithingPrimer] = new ItemText("Primer of the Forge", "From the caravan; teaches smithing to 3", "Forge primer", "Primer", "FORGE PRIMER");
            _texts[(int)ItemType.AlchemyPrimer] = new ItemText("Primer of the Still", "From the caravan; teaches alchemy to 3", "Still primer", "Primer", "STILL PRIMER");
            _texts[(int)ItemType.MedicinePrimer] = new ItemText("Primer of Mending", "From the caravan; teaches medicine to 3", "Mending primer", "Primer", "MENDING PRIMER");
            _texts[(int)ItemType.GlowstoneEdge] = new ItemText("Glowstone edge", "Forged from ore and glowstone", "Glowstone edge", "Glowstone edge", "GLOWSTONE EDGE");
            _texts[(int)ItemType.IronHelm] = new ItemText("Iron helm", "Forged from two ore; turns blows", "Iron helm", "Iron helm", "IRON HELM");
            _texts[(int)ItemType.RingCoat] = new ItemText("Ring coat", "Forged from four ore; turns blows", "Ring coat", "Ring coat", "RING COAT");
            // Loot (Epic 10 Part 3, K4.2, K4.4)
            _texts[(int)ItemType.Hide] = new ItemText("Hides", "Taken from slain rats", "Hide", "Hides", "HIDES");
            _texts[(int)ItemType.Chitin] = new ItemText("Chitin", "Taken from slain beetles", "Chitin", "Chitin", "CHITIN");
            _texts[(int)ItemType.LeatherJerkin] = new ItemText("Leather jerkin", "Forged from two hides; turns blows", "Leather jerkin", "Leather jerkin", "LEATHER JERKIN");
            _texts[(int)ItemType.ChitinPlate] = new ItemText("Chitin plate", "Forged from chitin and ore; turns blows", "Chitin plate", "Chitin plate", "CHITIN PLATE");
            _texts[(int)ItemType.FangKnife] = new ItemText("Fang knife", "A rat's rare fang, quick in the hand", "Fang knife", "Fang knife", "FANG KNIFE");
            _texts[(int)ItemType.StoneheartCoat] = new ItemText("Stoneheart coat", "A beetle's rare heart-shell", "Stoneheart coat", "Stoneheart coat", "STONEHEART COAT");
            // The bosses' trophies (Epic 10 Part 3, K10)
            _texts[(int)ItemType.MatriarchPelt] = new ItemText("Matriarch's pelt", "Taken from the warren's Matriarch", "Matriarch's pelt", "Matriarch's pelts", "MATRIARCH'S PELT");
            _texts[(int)ItemType.KeeperStone] = new ItemText("Keeper's stone", "The Gatekeeper's ember heart", "Keeper's stone", "Keeper's stones", "KEEPER'S STONE");
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
