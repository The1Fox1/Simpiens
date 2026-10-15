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

    /// <summary>How a completed trade at the player's stall moves the crawler's affinity toward the player.</summary>
    public readonly struct TradeTrustTuning
    {
        /// <summary>Ration price at or below which a purchase counts as generous.</summary>
        public readonly ushort FairRationPrice;

        /// <summary>A ration price above FairRationPrice × this counts as gouging.</summary>
        public readonly float GougingMultiplier;

        public readonly TrustDelta GenerousPurchase;
        public readonly TrustDelta ModeratePurchase;
        public readonly TrustDelta GougingPurchase;
        public readonly TrustDelta SalvageSale;

        public TradeTrustTuning(ushort fairRationPrice, float gougingMultiplier, TrustDelta generousPurchase,
            TrustDelta moderatePurchase, TrustDelta gougingPurchase, TrustDelta salvageSale)
        {
            FairRationPrice = fairRationPrice;
            GougingMultiplier = gougingMultiplier;
            GenerousPurchase = generousPurchase;
            ModeratePurchase = moderatePurchase;
            GougingPurchase = gougingPurchase;
            SalvageSale = salvageSale;
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
        /// <summary>Gold a crawler arrives with (also AgentInventory's default).</summary>
        public const int DefaultStartingGold = 10;

        public static readonly EconomyTuning Default = new EconomyTuning();

        // Item base values (stamped on harvested items; a neutral crawler's minimum sale price)
        public ushort RationBaseValue { get; private set; } = 5;
        public ushort OreBaseValue { get; private set; } = 8;
        public ushort HerbBaseValue { get; private set; } = 12;
        public ushort PickaxeBaseValue { get; private set; } = 30;
        public ushort WeaponBaseValue { get; private set; } = 40;

        public int StartingGold { get; private set; } = DefaultStartingGold;

        /// <summary>What a neutral crawler at hunger 50 will pay for a ration (Considerations.RationReservationPrice).</summary>
        public float RationReferencePrice { get; private set; } = 20f;

        // The stall's opening terms
        public ushort StallRationPrice { get; private set; } = 15;
        public ushort StallRationStock { get; private set; } = 50;
        public ushort StallOreBuyPrice { get; private set; } = 20;
        public ushort StallOreStock { get; private set; } = 10;
        public ushort StallHerbBuyPrice { get; private set; } = 25;
        public ushort StallHerbStock { get; private set; } = 10;
        public uint StallTreasury { get; private set; } = 250;

        // Seeded bounty rewards
        public ushort SeedGatherBountyReward { get; private set; } = 25;
        public ushort SeedScoutBountyReward { get; private set; } = 15;

        public SupplyTuning Supply { get; private set; } = SupplyTuning.Default;

        public TradeTrustTuning TradeTrust { get; private set; } = new TradeTrustTuning(
            fairRationPrice: 5,
            gougingMultiplier: 1.5f,
            generousPurchase: new TrustDelta(warmth: 5, trust: 10, respect: 5),
            moderatePurchase: new TrustDelta(warmth: 2, trust: 3, respect: 2),
            gougingPurchase: new TrustDelta(warmth: -10, trust: -15, respect: 0),
            salvageSale: new TrustDelta(warmth: 5, trust: 10, respect: 5));

        /// <summary>Nominal per-unit value of an item type.</summary>
        public ushort BaseValue(ItemType type)
        {
            switch (type)
            {
                case ItemType.Ration: return RationBaseValue;
                case ItemType.IronOre: return OreBaseValue;
                case ItemType.MedicinalHerb: return HerbBaseValue;
                case ItemType.ToolPickaxe: return PickaxeBaseValue;
                case ItemType.BasicWeapon: return WeaponBaseValue;
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
