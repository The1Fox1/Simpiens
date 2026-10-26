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

        // Item base values (stamped on harvested items; a neutral crawler's minimum sale price)
        public ushort RationBaseValue { get; private set; } = 5;
        public ushort OreBaseValue { get; private set; } = 8;
        public ushort HerbBaseValue { get; private set; } = 12;
        public ushort PickaxeBaseValue { get; private set; } = 30;
        public ushort HazardGearBaseValue { get; private set; } = 40;
        public ushort GlowstoneBaseValue { get; private set; } = 30; // rare salvage from the hazard sectors (Epic 10 G2)

        /// <summary>Gold a crawler arrives with: one ration at the stall's default price (review §3.3 D6).</summary>
        public int StartingGold { get; private set; } = 15;

        /// <summary>What a neutral crawler at hunger 50 will pay for a ration (Considerations.RationReservationPrice).</summary>
        public float RationReferencePrice { get; private set; } = 20f;

        // The stall's opening terms
        public ushort StallRationPrice { get; private set; } = 15;
        public ushort StallRationStock { get; private set; } = 50;
        public ushort StallOreBuyPrice { get; private set; } = 20;
        public ushort StallOreStock { get; private set; } = 10;
        public ushort StallHerbBuyPrice { get; private set; } = 25;
        public ushort StallHerbStock { get; private set; } = 10;
        /// <summary>What the stall pays for Glowstone, the rare salvage (Epic 10 G2); the caravan pays 70 (SupplyTuning).</summary>
        public ushort StallGlowstoneBuyPrice { get; private set; } = 45;
        /// <summary>What the stall sells hazard gear for (Epic 10 G3): its base value, fair to a neutral crawler; and its opening stock.</summary>
        public ushort StallHazardGearPrice { get; private set; } = 40;
        public ushort StallHazardGearStock { get; private set; } = 3;
        public uint StallTreasury { get; private set; } = 250;

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

        /// <summary>What hazard gear does and how long it lasts (Epic 10 G3).</summary>
        public GearTuning Gear { get; private set; } = GearTuning.Default;

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

        /// <summary>Nominal per-unit value of an item type.</summary>
        public ushort BaseValue(ItemType type)
        {
            switch (type)
            {
                case ItemType.Ration: return RationBaseValue;
                case ItemType.IronOre: return OreBaseValue;
                case ItemType.MedicinalHerb: return HerbBaseValue;
                case ItemType.ToolPickaxe: return PickaxeBaseValue;
                case ItemType.HazardGear: return HazardGearBaseValue;
                case ItemType.Glowstone: return GlowstoneBaseValue;
                default: return 0;
            }
        }

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

        public EconomyTuning WithGear(in GearTuning gear)
        {
            var copy = Copy();
            copy.Gear = gear;
            return copy;
        }

        public EconomyTuning WithStallHazardGear(ushort price, ushort stock)
        {
            var copy = Copy();
            copy.StallHazardGearPrice = price;
            copy.StallHazardGearStock = stock;
            return copy;
        }

        public EconomyTuning WithTradeTrust(in TradeTrustTuning tradeTrust)
        {
            var copy = Copy();
            copy.TradeTrust = tradeTrust;
            return copy;
        }

        public EconomyTuning WithStallTerms(ushort rationPrice, ushort oreBuyPrice, ushort herbBuyPrice, uint treasury)
        {
            var copy = Copy();
            copy.StallRationPrice = rationPrice;
            copy.StallOreBuyPrice = oreBuyPrice;
            copy.StallHerbBuyPrice = herbBuyPrice;
            copy.StallTreasury = treasury;
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

        public EconomyTuning WithBaseValues(ushort ration, ushort ore, ushort herb)
        {
            var copy = Copy();
            copy.RationBaseValue = ration;
            copy.OreBaseValue = ore;
            copy.HerbBaseValue = herb;
            return copy;
        }

        private EconomyTuning Copy() => (EconomyTuning)MemberwiseClone();
    }
}
