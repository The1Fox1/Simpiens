using System;
using System.Runtime.InteropServices;
namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// An unmanaged, zero-allocation sequential snapshot struct representing an outpost trade stall.
    /// Shared with background cognitive worker threads for lock-free price and stock evaluation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TradeStallData : IEquatable<TradeStallData>
    {
        public readonly EntityId StallEntityId;
        public readonly Vec2 Position;
        public readonly ushort RationSellPrice;
        public readonly ushort RationStock;
        public readonly ushort OreBuyPrice;
        public readonly ushort OreStock;
        public readonly ushort HerbBuyPrice;
        public readonly ushort HerbStock;
        public readonly uint TreasuryGold;
        public readonly float InteractionRadius;

        /// <summary>
        /// The stall couldn't pay its upkeep in full (review §3.3 D2). In arrears it buys no salvage but keeps selling
        /// rations, until a caravan visit finds the upkeep paid in full.
        /// </summary>
        public readonly bool InArrears;

        /// <summary>The rare salvage (Epic 10 G2): what the stall pays a crawler for one, and how many it holds for the caravan.</summary>
        public readonly ushort GlowstoneBuyPrice;
        public readonly ushort GlowstoneStock;

        /// <summary>Hazard gear (Epic 10 G3): what the stall sells one for, and how many it has (the caravan restocks them).</summary>
        public readonly ushort HazardGearSellPrice;
        public readonly ushort HazardGearStock;

        public static readonly TradeStallData Empty = default;

        public bool IsActive => StallEntityId != default;

        public TradeStallData(
            EntityId stallEntityId,
            Vec2 position,
            ushort rationSellPrice = 5,
            ushort rationStock = 50,
            ushort oreBuyPrice = 8,
            ushort oreStock = 0,
            ushort herbBuyPrice = 12,
            ushort herbStock = 0,
            uint treasuryGold = 500,
            float interactionRadius = 2.5f,
            bool inArrears = false,
            ushort glowstoneBuyPrice = 0,
            ushort glowstoneStock = 0,
            ushort hazardGearSellPrice = 0,
            ushort hazardGearStock = 0)
        {
            HazardGearSellPrice = hazardGearSellPrice;
            HazardGearStock = hazardGearStock;
            GlowstoneBuyPrice = glowstoneBuyPrice;
            GlowstoneStock = glowstoneStock;
            StallEntityId = stallEntityId;
            Position = position;
            RationSellPrice = rationSellPrice;
            RationStock = rationStock;
            OreBuyPrice = oreBuyPrice;
            OreStock = oreStock;
            HerbBuyPrice = herbBuyPrice;
            HerbStock = herbStock;
            TreasuryGold = treasuryGold;
            InteractionRadius = interactionRadius;
            InArrears = inArrears;
        }

        public TradeStallData WithStock(ushort rationStock, ushort oreStock, ushort herbStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, rationStock,
                OreBuyPrice, oreStock,
                HerbBuyPrice, herbStock,
                TreasuryGold, InteractionRadius, InArrears, GlowstoneBuyPrice, GlowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public TradeStallData WithRationStock(ushort rationStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, rationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears, GlowstoneBuyPrice, GlowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public TradeStallData WithOreStock(ushort oreStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, oreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears, GlowstoneBuyPrice, GlowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public TradeStallData WithHerbStock(ushort herbStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, herbStock,
                TreasuryGold, InteractionRadius, InArrears, GlowstoneBuyPrice, GlowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public TradeStallData WithTreasury(uint newTreasury)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                newTreasury, InteractionRadius, InArrears, GlowstoneBuyPrice, GlowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public TradeStallData WithPrices(ushort rationSellPrice, ushort oreBuyPrice, ushort herbBuyPrice)
        {
            return new TradeStallData(
                StallEntityId, Position,
                rationSellPrice, RationStock,
                oreBuyPrice, OreStock,
                herbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears, GlowstoneBuyPrice, GlowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public TradeStallData WithGlowstone(ushort glowstoneBuyPrice, ushort glowstoneStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears, glowstoneBuyPrice, glowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public TradeStallData WithGlowstoneStock(ushort glowstoneStock) => WithGlowstone(GlowstoneBuyPrice, glowstoneStock);

        public TradeStallData WithHazardGear(ushort hazardGearSellPrice, ushort hazardGearStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears, GlowstoneBuyPrice, GlowstoneStock, hazardGearSellPrice, hazardGearStock);
        }

        public TradeStallData WithHazardGearStock(ushort hazardGearStock) => WithHazardGear(HazardGearSellPrice, hazardGearStock);

        /// <summary>Units of a good the stall sells (rations, hazard gear), and its price (0 for anything else).</summary>
        public int SellStockOf(ItemType item) => item == ItemType.Ration ? RationStock : item == ItemType.HazardGear ? HazardGearStock : 0;

        public ushort SellPriceOf(ItemType item) => item == ItemType.Ration ? RationSellPrice : item == ItemType.HazardGear ? HazardGearSellPrice : (ushort)0;

        /// <summary>The same stall with <paramref name="stock"/> units of a good it sells.</summary>
        public TradeStallData WithSellStock(ItemType item, ushort stock) =>
            item == ItemType.Ration ? WithRationStock(stock) : item == ItemType.HazardGear ? WithHazardGearStock(stock) : this;

        /// <summary>Held units of a salvage kind, and what the stall pays for one (0 for anything else).</summary>
        public int StockOf(ItemType item) => item == ItemType.IronOre ? OreStock : item == ItemType.MedicinalHerb ? HerbStock : item == ItemType.Glowstone ? GlowstoneStock : 0;

        public ushort BuyPriceOf(ItemType item) => item == ItemType.IronOre ? OreBuyPrice : item == ItemType.MedicinalHerb ? HerbBuyPrice : item == ItemType.Glowstone ? GlowstoneBuyPrice : (ushort)0;

        public TradeStallData WithArrears(bool inArrears)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, inArrears, GlowstoneBuyPrice, GlowstoneStock, HazardGearSellPrice, HazardGearStock);
        }

        public bool Equals(TradeStallData other) => StallEntityId == other.StallEntityId;
        public override bool Equals(object obj) => obj is TradeStallData other && Equals(other);
        public override int GetHashCode() => StallEntityId.GetHashCode();
        public static bool operator ==(TradeStallData left, TradeStallData right) => left.Equals(right);
        public static bool operator !=(TradeStallData left, TradeStallData right) => !left.Equals(right);
    }
}
