namespace Simpiens.Simulation.Economy
{
    /// <summary>Affinity change toward the player from one completed trade (warmth, trust, respect).</summary>
    public readonly struct TrustDelta
    {
        public readonly sbyte Warmth;
        public readonly sbyte Trust;
        public readonly sbyte Respect;

        public TrustDelta(sbyte warmth, sbyte trust, sbyte respect)
        {
            Warmth = warmth;
            Trust = trust;
            Respect = respect;
        }
    }

    /// <summary>
    /// How a completed trade at the player's stall moves the crawler's affinity toward the player (review §3.3 D3).
    /// Ratios compare the stall's price with the crawler's own valuation (see TradeFairness).
    /// </summary>
    public readonly struct TradeTrustTuning
    {
        /// <summary>A ration bought at or below this share of the buyer's calm valuation is generous.</summary>
        public readonly float GenerousBuyRatio;

        /// <summary>At or below this share it is fair; above it, the buyer was gouged.</summary>
        public readonly float FairBuyRatio;

        /// <summary>Salvage bought at or above this multiple of the seller's minimum is generous.</summary>
        public readonly float GenerousSaleRatio;

        /// <summary>At or above this multiple it is fair; below it (but at the minimum or more, or it wouldn't sell), stingy.</summary>
        public readonly float FairSaleRatio;

        public readonly TrustDelta GenerousPurchase;
        public readonly TrustDelta FairPurchase;
        public readonly TrustDelta GougedPurchase;
        public readonly TrustDelta GenerousSale;
        public readonly TrustDelta FairSale;
        public readonly TrustDelta StingySale;

        public TradeTrustTuning(float generousBuyRatio, float fairBuyRatio, float generousSaleRatio, float fairSaleRatio,
            TrustDelta generousPurchase, TrustDelta fairPurchase, TrustDelta gougedPurchase,
            TrustDelta generousSale, TrustDelta fairSale, TrustDelta stingySale)
        {
            GenerousBuyRatio = generousBuyRatio;
            FairBuyRatio = fairBuyRatio;
            GenerousSaleRatio = generousSaleRatio;
            FairSaleRatio = fairSaleRatio;
            GenerousPurchase = generousPurchase;
            FairPurchase = fairPurchase;
            GougedPurchase = gougedPurchase;
            GenerousSale = generousSale;
            FairSale = fairSale;
            StingySale = stingySale;
        }
    }

    /// <summary>
    /// How a session trades one item (item system I5): its base value (a neutral crawler's valuation: the minimum sale
    /// price of salvage, the calm value of gear) and the stall's terms for it: whether the stall buys or sells it, the
    /// opening price, the opening stock. What the item *is* lives in the item catalog.
    /// </summary>
    public readonly struct ItemTerms
    {
        public readonly ushort BaseValue;
        public readonly StallRole StallRole;
        public readonly ushort StallPrice;
        public readonly ushort StallStock;

        public ItemTerms(ushort baseValue, StallRole stallRole = StallRole.None, ushort stallPrice = 0, ushort stallStock = 0)
        {
            BaseValue = baseValue;
            StallRole = stallRole;
            StallPrice = stallPrice;
            StallStock = stallStock;
        }

        public ItemTerms WithBaseValue(ushort baseValue) => new ItemTerms(baseValue, StallRole, StallPrice, StallStock);
        public ItemTerms WithStallPrice(ushort price) => new ItemTerms(BaseValue, StallRole, price, StallStock);
    }

    /// <summary>
    /// Every economy number a session runs on (review §3.3, step 1): item values, starting gold, the stall's opening
    /// terms, seeded bounty rewards, the supply caravan, the trade-to-trust rule and the crawlers' reference ration price.
    /// Each session exposes one (ISessionDefinition.Economy); the spawner applies it on every respawn. Immutable, so
    /// background cognition reads it from the world snapshot without locks. Garden yields and regrowth are map data
    /// (the layout), and the hunger rate is a drive (AgentState): neither lives here.
    /// </summary>
    public sealed class EconomyTuning
    {
        /// <summary>AgentInventory's default gold, for agents no session spawned (validators). Sessions use StartingGold.</summary>
        public const int InventoryDefaultGold = 10;

        public static readonly EconomyTuning Default = new EconomyTuning();

        // Each item's terms, by id (item system I5): the default economy's are below. Copies clone the table.
        private ItemTerms[] _items = DefaultItemTerms();

        private static ItemTerms[] DefaultItemTerms()
        {
            var items = new ItemTerms[Simpiens.Simulation.Items.ItemCatalog.Capacity];
            // The stall sells rations (15g, 50 to start) and hazard gear (Epic 10 G3: 30g, 0.75 of a neutral crawler's calm
            // gear value, as the ration's 15g is of 20g; 3 to start); it buys ore (20g, 10 held), herbs (25g, 10 held) and
            // glowstone, the hazard sectors' rare salvage (Epic 10 G2: 45g; the caravan pays 70, SupplyTuning)
            items[(int)ItemType.Ration] = new ItemTerms(5, StallRole.Sells, 15, 50);
            items[(int)ItemType.IronOre] = new ItemTerms(8, StallRole.Buys, 20, 10);
            items[(int)ItemType.MedicinalHerb] = new ItemTerms(12, StallRole.Buys, 25, 10);
            items[(int)ItemType.ToolPickaxe] = new ItemTerms(30);
            items[(int)ItemType.HazardGear] = new ItemTerms(40, StallRole.Sells, 30, 3);
            items[(int)ItemType.Glowstone] = new ItemTerms(30, StallRole.Buys, 45, 0);
            return items;
        }

        /// <summary>How this session trades an item.</summary>
        public ItemTerms Terms(ItemType item) => _items[(byte)item];

        /// <summary>Gold a crawler arrives with: one ration at the stall's default price (review §3.3 D6).</summary>
        public int StartingGold { get; private set; } = 15;

        /// <summary>What a neutral crawler at hunger 50 will pay for a ration (Considerations.RationReservationPrice).</summary>
        public float RationReferencePrice { get; private set; } = 20f;

        /// <summary>The stall's opening treasury; its opening goods are the items' terms (<see cref="StallGoods"/>).</summary>
        public uint StallTreasury { get; private set; } = 250;

        /// <summary>The stall's opening goods: every item it buys or sells, in id order, at its opening price and stock.</summary>
        public void StallGoods(System.Collections.Generic.List<StallGood> into)
        {
            into.Clear();
            var all = Simpiens.Simulation.Items.ItemCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                var terms = _items[(byte)all[i].Id];
                if (terms.StallRole != StallRole.None) into.Add(new StallGood(all[i].Id, terms.StallRole, terms.StallPrice, terms.StallStock));
            }
        }

        /// <summary>Salvage units of each type the stall holds at most; full, it stops buying that type until the caravan comes.</summary>
        public ushort StallSalvageCapacity { get; private set; } = 20;

        /// <summary>True when the stall has room for <paramref name="quantity"/> more units of a salvage type.</summary>
        public bool StallHasRoom(in TradeStallData stall, ItemType item, int quantity = 1)
        {
            return stall.StockOf(item) + quantity <= StallSalvageCapacity;
        }

        // Seeded bounty rewards
        public ushort SeedGatherBountyReward { get; private set; } = 25;
        public ushort SeedScoutBountyReward { get; private set; } = 15;

        public SupplyTuning Supply { get; private set; } = SupplyTuning.Default;

        /// <summary>Strangers who arrive with their own gold and leave (Epic 10 G1).</summary>
        public DrifterTuning Drifters { get; private set; } = DrifterTuning.Default;

        public TradeTrustTuning TradeTrust { get; private set; } = new TradeTrustTuning(
            generousBuyRatio: 0.6f,
            fairBuyRatio: 1.0f,
            generousSaleRatio: 3.0f,
            fairSaleRatio: 1.5f,
            generousPurchase: new TrustDelta(warmth: 5, trust: 10, respect: 5),
            fairPurchase: new TrustDelta(warmth: 2, trust: 3, respect: 2),
            gougedPurchase: new TrustDelta(warmth: -10, trust: -15, respect: 0),
            generousSale: new TrustDelta(warmth: 5, trust: 10, respect: 5),
            fairSale: new TrustDelta(warmth: 2, trust: 3, respect: 2),
            stingySale: new TrustDelta(warmth: -3, trust: -5, respect: 0));

        /// <summary>Nominal per-unit value of an item type (0 for one the session doesn't value).</summary>
        public ushort BaseValue(ItemType type) => _items[(byte)type].BaseValue;

        // --- Copies with one group changed (soak profiles, validators, measurement sessions) ---

        public EconomyTuning WithSupply(in SupplyTuning supply)
        {
            var copy = Copy();
            copy.Supply = supply;
            return copy;
        }

        public EconomyTuning WithDrifters(in DrifterTuning drifters)
        {
            var copy = Copy();
            copy.Drifters = drifters;
            return copy;
        }

        /// <summary>A copy with one item's terms changed.</summary>
        public EconomyTuning WithItemTerms(ItemType item, in ItemTerms terms)
        {
            var copy = Copy();
            copy._items = (ItemTerms[])_items.Clone();
            copy._items[(byte)item] = terms;
            return copy;
        }

        public EconomyTuning WithStallTreasury(uint treasury)
        {
            var copy = Copy();
            copy.StallTreasury = treasury;
            return copy;
        }

        public EconomyTuning WithTradeTrust(in TradeTrustTuning tradeTrust)
        {
            var copy = Copy();
            copy.TradeTrust = tradeTrust;
            return copy;
        }

        public EconomyTuning WithStallSalvageCapacity(ushort capacity)
        {
            var copy = Copy();
            copy.StallSalvageCapacity = capacity;
            return copy;
        }

        public EconomyTuning WithStartingGold(int startingGold)
        {
            var copy = Copy();
            copy.StartingGold = startingGold;
            return copy;
        }

        public EconomyTuning WithRationReferencePrice(float price)
        {
            var copy = Copy();
            copy.RationReferencePrice = price;
            return copy;
        }

        private EconomyTuning Copy() => (EconomyTuning)MemberwiseClone();
    }
}
