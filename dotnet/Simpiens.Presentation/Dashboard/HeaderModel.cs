using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The header over the map (Unity's header bar): the outpost's treasury and stock, the active bounties, the tick and
    /// the speed. Unity's emoji are words here: the UI font has none (plan D5).
    /// </summary>
    public sealed class HeaderModel
    {
        public const string Title = "SIMPIENS FACILITY OS";

        private readonly DashboardContext _context;

        internal HeaderModel(DashboardContext context) => _context = context;

        /// <summary>The status pill: Unity's never changed.</summary>
        public string Status { get; } = "OPERATIONAL";

        public string Treasury { get; private set; } = "0 GOLD";
        public string Rations { get; private set; } = "0 RATIONS";
        public string Salvage { get; private set; } = "0 ORE | 0 HERB";
        public string Bounties { get; private set; } = "0 BOUNTIES";
        public string Tick { get; private set; } = "TICK: 00000";
        public string Speed { get; private set; } = "SPEED: 1.0x";

        public void Refresh()
        {
            if (_context.TryGetStall(out var stall))
            {
                Treasury = Invariant($"{stall.TreasuryGold} GOLD");
                Rations = Invariant($"{stall.RationStock} RATIONS");
                Salvage = Invariant($"{stall.OreStock} ORE | {stall.HerbStock} HERB");
            }
            else
            {
                Treasury = "0 GOLD";
                Rations = "0 RATIONS";
                Salvage = "0 ORE | 0 HERB";
            }

            int active = _context.Simulation?.ActiveBounties?.Count ?? 0;
            Bounties = Invariant($"{active} BOUNTIES");

            var clock = _context.Clock;
            if (clock != null)
            {
                Tick = Invariant($"TICK: {clock.CurrentTick:D5}");
                Speed = Invariant($"SPEED: {clock.TimeMultiplier:F1}x");
            }
        }
    }
}
