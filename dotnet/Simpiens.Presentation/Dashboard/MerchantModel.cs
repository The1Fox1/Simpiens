using Simpiens.Intervention;
using Simpiens.Simulation;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The merchant terminal (Unity's merchant window): the stall's state, and three prices the player sets. Apply
    /// queues one <see cref="UpdateStallPricesCommand"/>; the stall changes on the next tick.
    /// </summary>
    public sealed class MerchantModel
    {
        public const int MinPrice = 1;
        public const int MaxPrice = 50;

        private readonly DashboardContext _context;

        internal MerchantModel(DashboardContext context) => _context = context;

        public bool IsOpen { get; private set; }

        public string StallStatus { get; private set; } = string.Empty;
        public string Treasury { get; private set; } = string.Empty;
        public string Stock { get; private set; } = string.Empty;

        /// <summary>The sliders' values, loaded from the stall when the window opens (Unity's defaults otherwise).</summary>
        public int RationPrice { get; set; } = 15;
        public int OrePrice { get; set; } = 20;
        public int HerbPrice { get; set; } = 25;

        public Feedback Feedback { get; private set; } = Feedback.None;

        public static string PriceText(int gold) => Invariant($"{gold} Gold");

        public void Open()
        {
            IsOpen = true;
            if (_context.TryGetStall(out var stall))
            {
                RationPrice = Clamp(stall.RationSellPrice);
                OrePrice = Clamp(stall.OreBuyPrice);
                HerbPrice = Clamp(stall.HerbBuyPrice);
            }
            Refresh();
        }

        public void Close() => IsOpen = false;

        public void Refresh()
        {
            if (!IsOpen) return;
            if (_context.TryGetStall(out var stall))
            {
                StallStatus = "Outpost Stall @ Central Corridor";
                Treasury = stall.InArrears
                    ? Invariant($"Treasury: {stall.TreasuryGold} Gold (in arrears: not buying salvage)")
                    : Invariant($"Treasury: {stall.TreasuryGold} Gold");
                Stock = Invariant($"Stock: {stall.RationStock} Rations | {stall.OreStock} Ore | {stall.HerbStock} Herb");
            }
            else
            {
                StallStatus = "No active stall established";
                Treasury = "Treasury: 0 Gold";
                Stock = "Stock: 0 Rations | 0 Ore | 0 Herb";
            }
        }

        /// <summary>Queues the three prices for the stall.</summary>
        public void Apply()
        {
            if (_context.Intervention == null)
            {
                Feedback = new Feedback("No intervention queue available.", FeedbackTone.Error);
                return;
            }
            ushort ration = (ushort)Clamp(RationPrice), ore = (ushort)Clamp(OrePrice), herb = (ushort)Clamp(HerbPrice);
            EntityId stallId = _context.TryGetStall(out var stall) ? stall.StallEntityId : default;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices("Player", new UpdateStallPricesCommand(stallId, ration, ore, herb)));
            Feedback = new Feedback(Invariant($"Rates updated: Ration {ration}g | Ore {ore}g | Herb {herb}g"), FeedbackTone.Ok);
        }

        private static int Clamp(int price) => price < MinPrice ? MinPrice : price > MaxPrice ? MaxPrice : price;
    }
}
