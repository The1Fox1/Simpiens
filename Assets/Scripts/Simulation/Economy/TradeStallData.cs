using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// An unmanaged, zero-allocation sequential snapshot struct representing an outpost trade stall.
    /// Shared with background cognitive worker threads for lock-free price and stock evaluation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TradeStallData : IEquatable<TradeStallData>
    {
        public readonly GUID StallEntityId;
        public readonly Vector2 Position;
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

        public static readonly TradeStallData Empty = default;

        public bool IsActive => StallEntityId != default;

        public TradeStallData(
            GUID stallEntityId,
            Vector2 position,
            ushort rationSellPrice = 5,
            ushort rationStock = 50,
            ushort oreBuyPrice = 8,
            ushort oreStock = 0,
            ushort herbBuyPrice = 12,
            ushort herbStock = 0,
            uint treasuryGold = 500,
            float interactionRadius = 2.5f,
            bool inArrears = false)
        {
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
                TreasuryGold, InteractionRadius, InArrears);
        }

        public TradeStallData WithRationStock(ushort rationStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, rationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears);
        }

        public TradeStallData WithOreStock(ushort oreStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, oreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears);
        }

        public TradeStallData WithHerbStock(ushort herbStock)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, herbStock,
                TreasuryGold, InteractionRadius, InArrears);
        }

        public TradeStallData WithTreasury(uint newTreasury)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                newTreasury, InteractionRadius, InArrears);
        }

        public TradeStallData WithPrices(ushort rationSellPrice, ushort oreBuyPrice, ushort herbBuyPrice)
        {
            return new TradeStallData(
                StallEntityId, Position,
                rationSellPrice, RationStock,
                oreBuyPrice, OreStock,
                herbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, InArrears);
        }

        public TradeStallData WithArrears(bool inArrears)
        {
            return new TradeStallData(
                StallEntityId, Position,
                RationSellPrice, RationStock,
                OreBuyPrice, OreStock,
                HerbBuyPrice, HerbStock,
                TreasuryGold, InteractionRadius, inArrears);
        }

        public bool Equals(TradeStallData other) => StallEntityId == other.StallEntityId;
        public override bool Equals(object obj) => obj is TradeStallData other && Equals(other);
        public override int GetHashCode() => StallEntityId.GetHashCode();
        public static bool operator ==(TradeStallData left, TradeStallData right) => left.Equals(right);
        public static bool operator !=(TradeStallData left, TradeStallData right) => !left.Equals(right);
    }
}
