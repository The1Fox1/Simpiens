using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The header over the map (Unity's header bar): the outpost's treasury and stock, the active bounties, the tick, and
    /// the pause and speed controls, whose clicks and keys leave as host commands (review D3). Unity's emoji are words
    /// here: the UI font has none (plan D5).
    /// </summary>
    public sealed class HeaderModel
    {
        public const string Title = "SIMPIENS FACILITY OS";

        /// <summary>The speeds the player picks from, as multiples of real time: the speed control cycles them, keys 1 to 4 pick one.</summary>
        public static readonly float[] Speeds = { 1f, 2f, 5f, 10f };

        private readonly DashboardContext _context;
        private int _speedIndex;

        internal HeaderModel(DashboardContext context) => _context = context;

        /// <summary>The status pill: Unity's never changed.</summary>
        public string Status { get; } = "OPERATIONAL";

        public string Treasury { get; private set; } = "0 GOLD";
        public string Rations { get; private set; } = "0 RATIONS";
        public string Salvage { get; private set; } = "0 ORE | 0 HERB";
        public string Bounties { get; private set; } = "0 BOUNTIES";
        public string Tick { get; private set; } = "TICK: 00000";

        /// <summary>The world's clock is stopped: the pause control offers to play.</summary>
        public bool Paused { get; private set; }

        /// <summary>The speed the world runs at, or will run at when the player resumes ("1x" to "10x").</summary>
        public string Speed => Invariant($"{Speeds[_speedIndex]:0.#}x");

        /// <summary>What the pause control says it will do.</summary>
        public string PauseLabel => Paused ? "PLAY" : "PAUSE";

        /// <summary>The controls work only with a host to ask.</summary>
        public bool CanControlSpeed => _context.HostCommands != null;

        /// <summary>Pauses a running world, or resumes a paused one at its speed.</summary>
        public void TogglePause() => _context.HostCommands?.Enqueue(Paused ? HostCommand.Resume : HostCommand.Pause);

        /// <summary>The next speed, round from 10x to 1x.</summary>
        public void NextSpeed() => SetSpeedStep((_speedIndex + 1) % Speeds.Length);

        /// <summary>Picks <see cref="Speeds"/>[<paramref name="step"/>]. A paused world stays paused and resumes at it.</summary>
        public void SetSpeedStep(int step)
        {
            if (_context.HostCommands == null || step < 0 || step >= Speeds.Length) return;
            _speedIndex = step;
            _context.HostCommands.Enqueue(HostCommand.SetSpeed(Speeds[step]));
        }

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
                Paused = clock.TimeMultiplier <= 0f;
                if (!Paused) _speedIndex = NearestSpeed(clock.TimeMultiplier); // a running clock says its speed; a paused one keeps the last
            }
        }

        private static int NearestSpeed(float multiplier)
        {
            int best = 0;
            for (int i = 1; i < Speeds.Length; i++)
            {
                if (System.Math.Abs(Speeds[i] - multiplier) < System.Math.Abs(Speeds[best] - multiplier)) best = i;
            }
            return best;
        }
    }
}
