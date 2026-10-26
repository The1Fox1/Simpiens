using Simpiens.Intervention;
using Simpiens.Simulation;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The merchant terminal (Unity's merchant window): the stall's state, and the prices the player sets (rations,
    /// ore, herbs, glowstone since Epic 10 G2, hazard gear since G3). Apply
    /// queues one <see cref="UpdateStallPricesCommand"/>; the stall changes on the next tick. The window works on one
    /// stall, picked by id when it opens (the outpost has one), and names it by the room it stands in (review S7).
    /// </summary>
    public sealed class MerchantModel
    {
        public const int MinPrice = 1;
        public const int MaxPrice = 50;

        private readonly DashboardContext _context;

        internal MerchantModel(DashboardContext context) => _context = context;

        public bool IsOpen { get; private set; }

        /// <summary>The stall the window shows and prices, or default when none is open.</summary>
        public EntityId StallId { get; private set; }

        public string StallStatus { get; private set; } = string.Empty;
        public string Treasury { get; private set; } = string.Empty;
        public string Stock { get; private set; } = string.Empty;

        /// <summary>The sliders' values, loaded from the stall when the window opens (Unity's defaults otherwise).</summary>
        public int RationPrice { get; set; } = 15;
        public int OrePrice { get; set; } = 20;
        public int HerbPrice { get; set; } = 25;
        public int GlowstonePrice { get; set; } = 45; // the rare salvage from the hazard sectors (Epic 10 G2)
        public int GearPrice { get; set; } = 30; // what the stall sells hazard gear for (Epic 10 G3)

        public Feedback Feedback { get; private set; } = Feedback.None;

        public static string PriceText(int gold) => Invariant($"{gold} Gold");

        public void Open()
        {
            IsOpen = true;
            StallId = _context.TryGetStall(out var stall) ? stall.StallEntityId : default;
            if (StallId != default)
            {
                RationPrice = Clamp(stall.RationSellPrice);
                OrePrice = Clamp(stall.OreBuyPrice);
                HerbPrice = Clamp(stall.HerbBuyPrice);
                GlowstonePrice = Clamp(stall.GlowstoneBuyPrice);
                GearPrice = Clamp(stall.HazardGearSellPrice);
            }
            Refresh();
        }

        public void Close() => IsOpen = false;

        public void Refresh()
        {
            if (!IsOpen) return;
            if (_context.TryGetStall(StallId, out var stall))
            {
                StallStatus = _context.StallLabel(stall);
                Treasury = stall.InArrears
                    ? Invariant($"Treasury: {stall.TreasuryGold} Gold (in arrears: not buying salvage)")
                    : Invariant($"Treasury: {stall.TreasuryGold} Gold");
                Stock = Invariant($"Stock: {stall.RationStock} Rations | {stall.OreStock} Ore | {stall.HerbStock} Herb | {stall.GlowstoneStock} Glowstone | {stall.HazardGearStock} Gear");
            }
            else
            {
                StallStatus = "No active stall established";
                Treasury = "Treasury: 0 Gold";
                Stock = "Stock: 0 Rations | 0 Ore | 0 Herb | 0 Glowstone | 0 Gear";
            }
        }

        /// <summary>Queues the five prices for the stall.</summary>
        public void Apply()
        {
            if (_context.Intervention == null)
            {
                Feedback = new Feedback("No intervention queue available.", FeedbackTone.Error);
                return;
            }
            ushort ration = (ushort)Clamp(RationPrice), ore = (ushort)Clamp(OrePrice), herb = (ushort)Clamp(HerbPrice), glow = (ushort)Clamp(GlowstonePrice), gear = (ushort)Clamp(GearPrice);
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices("Player", new UpdateStallPricesCommand(StallId, ration, ore, herb, glow, gear)));
            Feedback = new Feedback(Invariant($"Rates updated: Ration {ration}g | Ore {ore}g | Herb {herb}g | Glowstone {glow}g | Gear {gear}g"), FeedbackTone.Ok);
        }

        private static int Clamp(int price) => price < MinPrice ? MinPrice : price > MaxPrice ? MaxPrice : price;
    }
}
