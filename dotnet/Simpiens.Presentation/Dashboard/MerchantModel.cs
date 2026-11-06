using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// One priced good in the merchant terminal: the item, whether the stall buys or sells it, the slider's price and its
    /// range, from <see cref="MerchantModel.MinPrice"/> to <see cref="MaxPrice"/>.
    /// </summary>
    public sealed class MerchantRow
    {
        internal MerchantRow(ItemType item, StallRole role, int price, int maxPrice)
        {
            Item = item;
            Role = role;
            MaxPrice = maxPrice;
            Price = MerchantModel.Clamp(price, maxPrice);
        }

        public ItemType Item { get; }
        public StallRole Role { get; }
        public int Price { get; set; }

        /// <summary>The highest price the slider offers: four times the opening price or the stall's own, whichever is higher, at least 10 (Epic 10 Part 2, T6).</summary>
        public int MaxPrice { get; }

        /// <summary>The stall's stock of it in words, "10 in stock" (Epic 10 Part 2, W1.5a: shown in its row, as the goods outgrew one line).</summary>
        public string Stock { get; internal set; } = string.Empty;

        public string Label => ItemTexts.PriceLabel(Item, Role);
        public string Hint => ItemTexts.PriceHint(Item, Role);

        /// <summary>The best grade on the shelf (Epic 10 Part 2, W4): what a sale hands out first.</summary>
        public byte Grade { get; internal set; }

        /// <summary>The good's icon (graphics V4), rimmed for the best grade in stock (W4).</summary>
        public string Icon => ItemIcons.Of(Item, Grade);
    }

    /// <summary>
    /// The merchant terminal (Unity's merchant window): the stall's state, and the prices the player sets, one row per
    /// good the stall trades (item system I6): the food it sells, then what it buys in id order, then the rest it sells
    /// (rations; ore, herbs, glowstone; hazard gear). Apply queues one <see cref="UpdateStallPricesCommand"/>; the stall
    /// changes on the next tick. The window works on one stall, picked by id when it opens (the outpost has one), and
    /// names it by the room it stands in (review S7).
    /// </summary>
    public sealed class MerchantModel : IDashboardWindow
    {
        public const int MinPrice = 1;

        /// <summary>
        /// A good's slider reaches four times its opening price, at least 10 (Epic 10 Part 2, T6): wide enough for a greedy
        /// stall's doubled rations or a generous one's 1.75 times the ore price, and fine enough to move a 5g price by one
        /// gold. A fixed 1-50 left the 24-hour day's 5g ore at the slider's far end.
        /// </summary>
        public static int MaxPriceFor(int openingPrice) => openingPrice * 4 < MinRangeTop ? MinRangeTop : openingPrice * 4;
        private const int MinRangeTop = 10;

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
                for (int i = 0; i < rows.Count; i++)
                {
                    parts[i] = Invariant($"{stall.StockOf(rows[i].Item)} {ItemTexts.Of(rows[i].Item).StockWord}");
                    rows[i].Stock = StockText(stall, rows[i].Item);
                    rows[i].Grade = stall.BestGradeOf(rows[i].Item);
                }
            }
            else
            {
                StallStatus = "No active stall established";
                Treasury = "Treasury: 0 Gold";
                for (int i = 0; i < rows.Count; i++)
                {
                    parts[i] = $"0 {ItemTexts.Of(rows[i].Item).StockWord}";
                    rows[i].Stock = "0 in stock";
                    rows[i].Grade = Grades.Standard;
                }
            }
            Stock = "Stock: " + string.Join(" | ", parts);
        }

        /// <summary>"3 in stock", with the best grade on the shelf and the worn pieces waiting (W4): "3 in stock, best Fine, 2 worn".</summary>
        public static string StockText(in Simpiens.Simulation.Economy.TradeStallData stall, ItemType item)
        {
            string text = Invariant($"{stall.StockOf(item)} in stock");
            byte grade = stall.BestGradeOf(item);
            if (grade != Grades.Standard) text += ", best " + Grades.NameOf(grade);
            int worn = stall.WornOf(item);
            if (worn > 0) text += Invariant($", {worn} worn");
            return text;
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
                ushort price = (ushort)Clamp(rows[i].Price, rows[i].MaxPrice);
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
            var economy = _context.Simulation?.Economy ?? EconomyTuning.Default;
            for (int i = 0; i < picked.Count; i++)
            {
                // The range covers the opening price and the stall's own, so a price the player raised is never cut back
                int opening = economy.Terms(picked[i].Item).StallPrice;
                _rows.Add(new MerchantRow(picked[i].Item, picked[i].Role, picked[i].Price, MaxPriceFor(opening > picked[i].Price ? opening : picked[i].Price)));
            }
        }

        internal static int Clamp(int price, int maxPrice) => price < MinPrice ? MinPrice : price > maxPrice ? maxPrice : price;
    }
}
