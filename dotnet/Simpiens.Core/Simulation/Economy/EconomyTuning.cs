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
    /// How a completed trade at the player's stall moves the crawler's affinity toward the player (review §3.3 D3), and
    /// how a delivered bounty does (Epic 10 G4, D5): judged as a sale is. Ratios compare the stall's price, or the
    /// bounty's reward, with the crawler's own valuation (see TradeFairness).
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

        /// <summary>A bounty's reward at or above this multiple of the claimant's minimum for the work is generous (D5).</summary>
        public readonly float GenerousBountyRatio;

        /// <summary>At or above this multiple it is fair; below it, stingy.</summary>
        public readonly float FairBountyRatio;

        /// <summary>What a scout report is worth to a neutral crawler, in gold: a Scout bounty's minimum, times the claimant's greed factor.</summary>
        public readonly ushort ScoutingValue;

        public readonly TrustDelta GenerousBounty;
        public readonly TrustDelta FairBounty;
        public readonly TrustDelta StingyBounty;

        public TradeTrustTuning(float generousBuyRatio, float fairBuyRatio, float generousSaleRatio, float fairSaleRatio,
            TrustDelta generousPurchase, TrustDelta fairPurchase, TrustDelta gougedPurchase,
            TrustDelta generousSale, TrustDelta fairSale, TrustDelta stingySale,
            float generousBountyRatio = 3f, float fairBountyRatio = 1.5f, ushort scoutingValue = 5,
            TrustDelta generousBounty = default, TrustDelta fairBounty = default, TrustDelta stingyBounty = default)
        {
            GenerousBountyRatio = generousBountyRatio;
            FairBountyRatio = fairBountyRatio;
            ScoutingValue = scoutingValue;
            GenerousBounty = generousBounty;
            FairBounty = fairBounty;
            StingyBounty = stingyBounty;
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

        /// <summary>
        /// The 24-hour day's economy (Epic 10 Part 2, milestone T; <c>docs/time-model.md</c>, <c>docs/economy-model.md</c>):
        /// <list type="bullet">
        /// <item>the day's pace (<see cref="DayTuning.Default"/>);</item>
        /// <item>the caravan every 6 game hours (T4.1, D24: <see cref="SupplyTuning.Default"/>);</item>
        /// <item>crawlers earn toward 80g, about two days' meals (T4.2, D25);</item>
        /// <item>salvage pays a quarter of Part 1's price, so a day's meals take hours of work (T4.2b, D29): ore 5g (worth 2g
        /// to a neutral seller), herbs 6g (3g). Food, purses and glowstone kept Part 1's terms.</item>
        /// </list>
        /// </summary>
        public static readonly EconomyTuning Default = new EconomyTuning();

        // Each item's terms, by id (item system I5): the default economy's are below. Copies clone the table.
        private ItemTerms[] _items = DefaultItemTerms();

        private static ItemTerms[] DefaultItemTerms()
        {
            var items = new ItemTerms[Simpiens.Simulation.Items.ItemCatalog.Capacity];
            // The stall sells rations (15g, 50 to start) and hazard gear (Epic 10 G3: 30g, 0.75 of a neutral crawler's calm
            // gear value, as the ration's 15g is of 20g; 3 to start); it buys ore (5g, worth 2g to a neutral seller; 10 held),
            // herbs (6g, worth 3g; 10 held) and glowstone, the hazard sectors' rare salvage (Epic 10 G2: 45g; the caravan pays
            // 70, SupplyTuning). Salvage is a quarter of Part 1's prices on the 24-hour day (Epic 10 Part 2, T4.2b, D29)
            items[(int)ItemType.Ration] = new ItemTerms(5, StallRole.Sells, 15, 50);
            items[(int)ItemType.IronOre] = new ItemTerms(2, StallRole.Buys, 5, 10);
            items[(int)ItemType.MedicinalHerb] = new ItemTerms(3, StallRole.Buys, 6, 10);
            // The forge's pickaxe (Epic 10 Part 2, W1): the stall sells what the forge makes, none to start; its price is W1.4's
            items[(int)ItemType.ToolPickaxe] = new ItemTerms(30, StallRole.Sells, 30, 0);
            items[(int)ItemType.HazardGear] = new ItemTerms(40, StallRole.Sells, 30, 3);
            items[(int)ItemType.Glowstone] = new ItemTerms(30, StallRole.Buys, 45, 0);
            // The still's draught (Epic 10 Part 2, W2, D46): worth 25g to a neutral crawler, sold at 20g (two herbs at 6g and
            // an hour's work), none to start
            items[(int)ItemType.HealingDraught] = new ItemTerms(25, StallRole.Sells, 20, 0);
            // The forge's protection by kind (W3, D48): worth 80g to a neutral crawler, sold at 60g (a glowstone at 45g and its
            // herbs or ore, two hours' work), none to start
            items[(int)ItemType.Respirator] = new ItemTerms(80, StallRole.Sells, 60, 0);
            items[(int)ItemType.HardHat] = new ItemTerms(80, StallRole.Sells, 60, 0);
            // Melee arms and armour (Epic 10 Part 3, K3.1, K3.2): the caravan's cudgel at 12g; what the forge makes priced as
            // the forge's goods are, its inputs and its hours under what a neutral crawler would pay; none to start
            items[(int)ItemType.Cudgel] = new ItemTerms(15, StallRole.Sells, 12, 0);
            items[(int)ItemType.IronBlade] = new ItemTerms(50, StallRole.Sells, 40, 0);
            items[(int)ItemType.GlowstoneEdge] = new ItemTerms(110, StallRole.Sells, 85, 0);
            items[(int)ItemType.IronHelm] = new ItemTerms(40, StallRole.Sells, 30, 0);
            items[(int)ItemType.RingCoat] = new ItemTerms(70, StallRole.Sells, 55, 0);
            // Loot (Epic 10 Part 3, K4.2): valued, not yet traded (the stall's terms for materials and their armour are K4.4's);
            // the rare pieces are never the stall's
            items[(int)ItemType.Hide] = new ItemTerms(3, StallRole.Buys, 5, 0); // K4.4 (D25): the forge's, the surplus the caravan's
            items[(int)ItemType.Chitin] = new ItemTerms(5, StallRole.Buys, 8, 0);
            // The bosses' trophies (Epic 10 Part 3, K10, Q12): the stall buys them dear and the caravan takes them on
            items[(int)ItemType.MatriarchPelt] = new ItemTerms(30, StallRole.Buys, 36, 0);
            items[(int)ItemType.KeeperStone] = new ItemTerms(120, StallRole.Buys, 140, 0);
            items[(int)ItemType.LeatherJerkin] = new ItemTerms(45, StallRole.Sells, 35, 0); // made at the forge (K4.4)
            items[(int)ItemType.ChitinPlate] = new ItemTerms(100, StallRole.Sells, 70, 0);
            items[(int)ItemType.FangKnife] = new ItemTerms(150);
            items[(int)ItemType.StoneheartCoat] = new ItemTerms(180);
            // Ranged arms (Epic 10 Part 3, K8.1, D44): the caravan's sling at 10g; the forge's crossbow priced as its goods are
            // (two ore and a hide, two hours' work), none to start
            items[(int)ItemType.Sling] = new ItemTerms(12, StallRole.Sells, 10, 0);
            items[(int)ItemType.Crossbow] = new ItemTerms(60, StallRole.Sells, 45, 0);
            // Manuals (Epic 11, S3.2, D11): primers at 25g, worth 30g to a neutral reader; the caravan brings them, none to start
            for (var primer = ItemType.MeleePrimer; primer <= ItemType.MedicinePrimer; primer++) items[(int)primer] = new ItemTerms(30, StallRole.Sells, 25, 0);
            // The tavern's menu (Epic LIFE-1 plan 2, T.3, D19): on the stall's shelf, served at the tavern; an ale 4g, a stew supper
            // 10g against a ration's 15g; none until the caravan brings them for a tavern that stands
            items[(int)ItemType.Ale] = new ItemTerms(3, StallRole.Sells, 4, 0);
            items[(int)ItemType.Stew] = new ItemTerms(6, StallRole.Sells, 10, 0);
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

        /// <summary>The player's guild: recruiting, the fee, wages and quitting (Epic 10 G4).</summary>
        public GuildTuning Guild { get; private set; } = GuildTuning.Default;

        /// <summary>Hunt bounties (Epic 10 Part 3, K6, Q7).</summary>
        public HuntTuning Hunt { get; private set; } = HuntTuning.Default;

        /// <summary>The stations and their artisans (Epic 10 Part 2).</summary>
        public WorkshopTuning Workshops { get; private set; } = WorkshopTuning.Default;

        /// <summary>
        /// The purse a crawler earns toward: below it, it works for gold (EarnIncomeGoal). 80g, a day's needs and a margin
        /// (Epic 10 Part 2, T4.2, D25; 40g before the 24-hour day).
        /// </summary>
        public int ComfortablePurse { get; private set; } = 80;

        /// <summary>The pace of a crawler's day: needs, sleep and rest (Epic 10 Part 2, milestone T).</summary>
        public DayTuning Day { get; private set; } = DayTuning.Default;

        /// <summary>The life core's numbers (Epic LIFE-1, the epic's A11): the motive model's weights, commitment and the fallback. The default changes nothing.</summary>
        public Simpiens.Simulation.Life.LifeTuning Life { get; private set; } = Simpiens.Simulation.Life.LifeTuning.Default;

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
            stingySale: new TrustDelta(warmth: -3, trust: -5, respect: 0),
            // Bounties (Epic 10 G4, D5): the generous delta is the flat one every delivery got before
            generousBountyRatio: 3.0f,
            fairBountyRatio: 1.5f,
            scoutingValue: 5,
            generousBounty: new TrustDelta(warmth: 10, trust: 15, respect: 10),
            fairBounty: new TrustDelta(warmth: 5, trust: 8, respect: 5),
            stingyBounty: new TrustDelta(warmth: -3, trust: -5, respect: 0));

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

        public EconomyTuning WithGuild(in GuildTuning guild)
        {
            var copy = Copy();
            copy.Guild = guild;
            return copy;
        }

        public EconomyTuning WithWorkshops(in WorkshopTuning workshops)
        {
            var copy = Copy();
            copy.Workshops = workshops;
            return copy;
        }

        public EconomyTuning WithComfortablePurse(int gold)
        {
            var copy = Copy();
            copy.ComfortablePurse = gold;
            return copy;
        }

        public EconomyTuning WithDay(in DayTuning day)
        {
            var copy = Copy();
            copy.Day = day;
            return copy;
        }

        public EconomyTuning WithLife(in Simpiens.Simulation.Life.LifeTuning life)
        {
            var copy = Copy();
            copy.Life = life;
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
