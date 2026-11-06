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

    /// <summary>
    /// One good on a stall's table: the item, whether the stall buys or sells it, its price and the stall's stock; of that
    /// stock, the pieces made Fine and Masterwork (Epic 10 Part 2, W4, D57: the rest are Standard); and worn pieces handed
    /// back, waiting for the forge's repair and not for sale (D58).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StallGood
    {
        public ItemType Item;
        public StallRole Role;
        public ushort Price;
        public ushort Stock;
        public byte Fine;
        public byte Masterwork;
        public byte Worn;

        public StallGood(ItemType item, StallRole role, ushort price, ushort stock)
        {
            Item = item;
            Role = role;
            Price = price;
            Stock = stock;
            Fine = 0;
            Masterwork = 0;
            Worn = 0;
        }

        /// <summary>The best grade in stock (<see cref="Simpiens.Simulation.Items.Grades"/>): what a sale or a gift hands out first.</summary>
        public readonly byte BestGrade => Stock == 0 ? Simpiens.Simulation.Items.Grades.Standard
            : Masterwork > 0 ? Simpiens.Simulation.Items.Grades.Masterwork : Fine > 0 ? Simpiens.Simulation.Items.Grades.Fine : Simpiens.Simulation.Items.Grades.Standard;

        /// <summary>The same good with a new stock, its graded pieces kept within it (Standard ones go first, the best last).</summary>
        public readonly StallGood WithStock(ushort stock)
        {
            var good = this;
            good.Stock = stock;
            int over = good.Fine + good.Masterwork - stock;
            if (over > 0)
            {
                int fine = over < good.Fine ? over : good.Fine;
                good.Fine -= (byte)fine;
                good.Masterwork -= (byte)(over - fine);
            }
            return good;
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
        public const int GoodsCapacity = 10; // 8 until Epic 10 Part 2's W3 (D49): ten goods with the forge's protection by kind

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

        /// <summary>The same stall with a new stock of an item it trades (unchanged when it doesn't trade it); its graded pieces kept within it.</summary>
        public TradeStallData WithStock(ItemType item, ushort stock) =>
            TryGetGood(item, out var good) ? WithGood(good.WithStock(stock)) : this;

        /// <summary>The same stall with a new price for an item it trades (unchanged when it doesn't trade it).</summary>
        public TradeStallData WithPrice(ItemType item, ushort price)
        {
            if (!TryGetGood(item, out var good)) return this;
            good.Price = price;
            return WithGood(good);
        }

        /// <summary>The best grade of an item in stock (D57); Standard when there is none.</summary>
        public byte BestGradeOf(ItemType item) => TryGetGood(item, out var good) ? good.BestGrade : Simpiens.Simulation.Items.Grades.Standard;

        /// <summary>The same stall with <paramref name="count"/> more of an item it trades, at <paramref name="grade"/> (a make, D57).</summary>
        public TradeStallData WithStocked(ItemType item, ushort count, byte grade)
        {
            if (!TryGetGood(item, out var good)) return this;
            good.Stock = (ushort)(good.Stock + count);
            if (grade == Simpiens.Simulation.Items.Grades.Fine) good.Fine = (byte)Math.Min(byte.MaxValue, good.Fine + count);
            else if (grade >= Simpiens.Simulation.Items.Grades.Masterwork) good.Masterwork = (byte)Math.Min(byte.MaxValue, good.Masterwork + count);
            return WithGood(good.WithStock(good.Stock));
        }

        /// <summary>The same stall with one piece of an item fewer, the best grade first (a sale or a gift, D57); the grade taken.</summary>
        public TradeStallData TakeBest(ItemType item, out byte grade)
        {
            grade = Simpiens.Simulation.Items.Grades.Standard;
            if (!TryGetGood(item, out var good) || good.Stock == 0) return this;
            grade = good.BestGrade;
            if (grade == Simpiens.Simulation.Items.Grades.Masterwork) good.Masterwork--;
            else if (grade == Simpiens.Simulation.Items.Grades.Fine) good.Fine--;
            good.Stock--;
            return WithGood(good);
        }

        /// <summary>Worn pieces of an item waiting for repair (D58).</summary>
        public int WornOf(ItemType item) => TryGetGood(item, out var good) ? good.Worn : 0;

        /// <summary>The same stall with <paramref name="worn"/> worn pieces of an item it trades (D58).</summary>
        public TradeStallData WithWorn(ItemType item, byte worn)
        {
            if (!TryGetGood(item, out var good)) return this;
            good.Worn = worn;
            return WithGood(good);
        }

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
