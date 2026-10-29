using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>One priced good in the merchant terminal: the item, whether the stall buys or sells it, and the slider's price.</summary>
    public sealed class MerchantRow
    {
        internal MerchantRow(ItemType item, StallRole role, int price)
        {
            Item = item;
            Role = role;
            Price = price;
        }

        public ItemType Item { get; }
        public StallRole Role { get; }
        public int Price { get; set; }

        public string Label => ItemTexts.PriceLabel(Item, Role);
        public string Hint => ItemTexts.Of(Item).MerchantHint;
        /// <summary>The good's icon (graphics V4).</summary>
        public string Icon => ItemIcons.Of(Item);
    }

    /// <summary>
    /// The merchant terminal (Unity's merchant window): the stall's state, and the prices the player sets, one row per
    /// good the stall trades (item system I6): the food it sells, then what it buys in id order, then the rest it sells
    /// (rations; ore, herbs, glowstone; hazard gear). Apply queues one <see cref="UpdateStallPricesCommand"/>; the stall
    /// changes on the next tick. The window works on one stall, picked by id when it opens (the outpost has one), and
    /// names it by the room it stands in (review S7).
    /// </summary>
    public sealed class MerchantModel
    {
        public const int MinPrice = 1;
        public const int MaxPrice = 50;

        private readonly DashboardContext _context;
        private readonly List<MerchantRow> _rows = new List<MerchantRow>(8);

        internal MerchantModel(DashboardContext context) => _context = context;

        public bool IsOpen { get; private set; }

        /// <summary>The stall the window shows and prices, or default when none is open.</summary>
        public EntityId StallId { get; private set; }

        public string StallStatus { get; private set; } = string.Empty;
        public string Treasury { get; private set; } = string.Empty;
        public string Stock { get; private set; } = string.Empty;

        /// <summary>The priced goods, in the terminal's order: the stall's when one is open, the session's opening terms before.</summary>
        public IReadOnlyList<MerchantRow> Rows
        {
            get
            {
                if (_rows.Count == 0) LoadRows();
                return _rows;
            }
        }

        public Feedback Feedback { get; private set; } = Feedback.None;

        public static string PriceText(int gold) => Invariant($"{gold} Gold");

        public MerchantRow Row(ItemType item)
        {
            var rows = Rows;
            for (int i = 0; i < rows.Count; i++) if (rows[i].Item == item) return rows[i];
            return null;
        }

        public void Open()
        {
            IsOpen = true;
            StallId = _context.TryGetStall(out var stall) ? stall.StallEntityId : default;
            LoadRows();
            Refresh();
        }

        public void Close() => IsOpen = false;

        public void Refresh()
        {
            if (!IsOpen) return;
            var rows = Rows;
            var parts = new string[rows.Count];
            if (_context.TryGetStall(StallId, out var stall))
            {
                StallStatus = _context.StallLabel(stall);
                Treasury = stall.InArrears
                    ? Invariant($"Treasury: {stall.TreasuryGold} Gold (in arrears: not buying salvage)")
                    : Invariant($"Treasury: {stall.TreasuryGold} Gold");
                for (int i = 0; i < rows.Count; i++) parts[i] = Invariant($"{stall.StockOf(rows[i].Item)} {ItemTexts.Of(rows[i].Item).StockWord}");
            }
            else
            {
                StallStatus = "No active stall established";
                Treasury = "Treasury: 0 Gold";
                for (int i = 0; i < rows.Count; i++) parts[i] = $"0 {ItemTexts.Of(rows[i].Item).StockWord}";
            }
            Stock = "Stock: " + string.Join(" | ", parts);
        }

        /// <summary>Queues every row's price for the stall.</summary>
        public void Apply()
        {
            if (_context.Intervention == null)
            {
                Feedback = new Feedback("No intervention queue available.", FeedbackTone.Error);
                return;
            }
            var rows = Rows;
            var prices = new UpdateStallPricesCommand(StallId);
            var parts = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                ushort price = (ushort)Clamp(rows[i].Price);
                prices = prices.With(rows[i].Item, price);
                parts[i] = Invariant($"{ItemTexts.Of(rows[i].Item).ShortWord} {price}g");
            }
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices("Player", prices));
            Feedback = new Feedback("Rates updated: " + string.Join(" | ", parts), FeedbackTone.Ok);
        }

        /// <summary>The rows from the open stall's goods, or the session's opening terms before a stall opens.</summary>
        private void LoadRows()
        {
            var goods = new List<StallGood>(TradeStallData.GoodsCapacity);
            if (_context.TryGetStall(StallId, out var stall) || _context.TryGetStall(out stall))
            {
                for (int i = 0; i < TradeStallData.GoodsCapacity; i++)
                {
                    var good = stall.GoodAt(i);
                    if (good.Item != ItemType.None) goods.Add(good);
                }
            }
            else
            {
                (_context.Simulation?.Economy ?? EconomyTuning.Default).StallGoods(goods);
            }

            _rows.Clear();
            // The food it sells, then what it buys in id order, then the rest it sells
            AddRows(goods, g => g.Role == StallRole.Sells && ItemCatalog.Is(g.Item, ItemCategory.Food));
            AddRows(goods, g => g.Role == StallRole.Buys);
            AddRows(goods, g => g.Role == StallRole.Sells && !ItemCatalog.Is(g.Item, ItemCategory.Food));
        }

        private void AddRows(List<StallGood> goods, System.Func<StallGood, bool> which)
        {
            var picked = new List<StallGood>();
            for (int i = 0; i < goods.Count; i++) if (which(goods[i])) picked.Add(goods[i]);
            picked.Sort((a, b) => a.Item.CompareTo(b.Item));
            for (int i = 0; i < picked.Count; i++) _rows.Add(new MerchantRow(picked[i].Item, picked[i].Role, Clamp(picked[i].Price)));
        }

        private static int Clamp(int price) => price < MinPrice ? MinPrice : price > MaxPrice ? MaxPrice : price;
    }
}
