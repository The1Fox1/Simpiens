using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Simpiens.Simulation.Economy
{
    /// <summary>Whether the stall buys an item from crawlers, sells it to them, or neither.</summary>
    public enum StallRole : byte
    {
        None = 0,
        Buys = 1,
        Sells = 2
    }

    /// <summary>One good on a stall's table: the item, whether the stall buys or sells it, its price and the stall's stock.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StallGood
    {
        public ItemType Item;
        public StallRole Role;
        public ushort Price;
        public ushort Stock;

        public StallGood(ItemType item, StallRole role, ushort price, ushort stock)
        {
            Item = item;
            Role = role;
            Price = price;
            Stock = stock;
        }
    }

    /// <summary>A stall's goods, a fixed table (unmanaged, saved as raw memory): unused entries hold <see cref="ItemType.None"/>.</summary>
    [InlineArray(TradeStallData.GoodsCapacity)]
    public struct StallGoods
    {
        private StallGood _entry;
    }

    /// <summary>
    /// An unmanaged, zero-allocation sequential snapshot struct representing an outpost trade stall.
    /// Shared with background cognitive worker threads for lock-free price and stock evaluation.
    /// <para>
    /// What it trades is a table of goods (item system I5), not a field per item: adding an item to the economy changes
    /// no field here. The table holds at most <see cref="GoodsCapacity"/> goods; raising it changes the save format.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TradeStallData : IEquatable<TradeStallData>
    {
        public const int GoodsCapacity = 8;

        public readonly EntityId StallEntityId;
        public readonly Vec2 Position;
        public readonly uint TreasuryGold;
        public readonly float InteractionRadius;

        /// <summary>
        /// The stall couldn't pay its upkeep in full (review §3.3 D2). In arrears it buys no salvage but keeps selling
        /// rations, until a caravan visit finds the upkeep paid in full.
        /// </summary>
        public readonly bool InArrears;

        private readonly StallGoods _goods;

        public static readonly TradeStallData Empty = default;

        public bool IsActive => StallEntityId != default;

        public TradeStallData(EntityId stallEntityId, Vec2 position, uint treasuryGold = 500, float interactionRadius = 2.5f, bool inArrears = false)
            : this(stallEntityId, position, treasuryGold, interactionRadius, inArrears, default)
        {
        }

        private TradeStallData(EntityId stallEntityId, Vec2 position, uint treasuryGold, float interactionRadius, bool inArrears, in StallGoods goods)
        {
            StallEntityId = stallEntityId;
            Position = position;
            TreasuryGold = treasuryGold;
            InteractionRadius = interactionRadius;
            InArrears = inArrears;
            _goods = goods;
        }

        // --- The goods table ---

        /// <summary>The goods in table order (the order they were added; empty entries skipped by callers).</summary>
        public StallGood GoodAt(int index) => _goods[index];

        /// <summary>The entry for an item, or false when the stall doesn't trade it.</summary>
        public bool TryGetGood(ItemType item, out StallGood good)
        {
            if (item != ItemType.None)
            {
                for (int i = 0; i < GoodsCapacity; i++)
                {
                    if (_goods[i].Item == item)
                    {
                        good = _goods[i];
                        return true;
                    }
                }
            }
            good = default;
            return false;
        }

        public bool Trades(ItemType item) => TryGetGood(item, out _);

        /// <summary>What the stall pays for one unit of an item it buys; 0 for anything else.</summary>
        public ushort BuyPriceOf(ItemType item) => TryGetGood(item, out var good) && good.Role == StallRole.Buys ? good.Price : (ushort)0;

        /// <summary>What the stall sells one unit of an item for; 0 for anything it doesn't sell.</summary>
        public ushort SellPriceOf(ItemType item) => TryGetGood(item, out var good) && good.Role == StallRole.Sells ? good.Price : (ushort)0;

        /// <summary>The stall's stock of an item it trades (held for the caravan, or for sale); 0 for anything else.</summary>
        public int StockOf(ItemType item) => TryGetGood(item, out var good) ? good.Stock : 0;

        /// <summary>Units of an item the stall sells; 0 for anything it doesn't sell.</summary>
        public int SellStockOf(ItemType item) => TryGetGood(item, out var good) && good.Role == StallRole.Sells ? good.Stock : 0;

        /// <summary>The same stall trading <paramref name="good"/>: its entry replaced, or added in the first free place.</summary>
        public TradeStallData WithGood(in StallGood good)
        {
            if (good.Item == ItemType.None) throw new ArgumentException("A good is an item.", nameof(good));
            var goods = _goods;
            int free = -1;
            for (int i = 0; i < GoodsCapacity; i++)
            {
                if (goods[i].Item == good.Item)
                {
                    goods[i] = good;
                    return new TradeStallData(StallEntityId, Position, TreasuryGold, InteractionRadius, InArrears, goods);
                }
                if (free < 0 && goods[i].Item == ItemType.None) free = i;
            }
            if (free < 0) throw new InvalidOperationException($"A stall trades at most {GoodsCapacity} goods.");
            goods[free] = good;
            return new TradeStallData(StallEntityId, Position, TreasuryGold, InteractionRadius, InArrears, goods);
        }

        /// <summary>The same stall with a new stock of an item it trades (unchanged when it doesn't trade it).</summary>
        public TradeStallData WithStock(ItemType item, ushort stock) =>
            TryGetGood(item, out var good) ? WithGood(new StallGood(item, good.Role, good.Price, stock)) : this;

        /// <summary>The same stall with a new price for an item it trades (unchanged when it doesn't trade it).</summary>
        public TradeStallData WithPrice(ItemType item, ushort price) =>
            TryGetGood(item, out var good) ? WithGood(new StallGood(item, good.Role, price, good.Stock)) : this;

        public TradeStallData WithTreasury(uint newTreasury) =>
            new TradeStallData(StallEntityId, Position, newTreasury, InteractionRadius, InArrears, _goods);

        public TradeStallData WithArrears(bool inArrears) =>
            new TradeStallData(StallEntityId, Position, TreasuryGold, InteractionRadius, inArrears, _goods);

        public bool Equals(TradeStallData other) => StallEntityId == other.StallEntityId;
        public override bool Equals(object obj) => obj is TradeStallData other && Equals(other);
        public override int GetHashCode() => StallEntityId.GetHashCode();
        public static bool operator ==(TradeStallData left, TradeStallData right) => left.Equals(right);
        public static bool operator !=(TradeStallData left, TradeStallData right) => !left.Equals(right);
    }
}
