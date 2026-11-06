using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;

namespace Simpiens.Hosting
{
    /// <summary>
    /// A scripted player for the workshops (Epic 10 Part 2, W1.5a): it acts only through the typed commands a real player
    /// sends, as <see cref="GuildPlayer"/> does. After every tick it looks at what the station window shows (the
    /// workshops' own rules, read-only) and, for its station: builds it once the stall can pay; hires the first resident,
    /// in spawn order, who would take the work at its wage; gives the artisan its shifts (the day shift by default); and
    /// keeps an order for the station's make, ordering again when the last is done. Shared by the Definition-of-Done
    /// scenarios, the soak and the console.
    /// </summary>
    public sealed class WorkshopPlayer
    {
        public const string SourceId = "WorkshopPlayer";

        private readonly LocalSimulation _sim;
        private readonly IPlayerInterventionService _commands;
        private readonly StationKind _kind;
        private readonly ItemType _make;
        private readonly ushort _batch;
        private readonly byte _shifts;
        private readonly ushort _wage;
        private readonly List<EntityId> _hired = new List<EntityId>(2);

        /// <param name="batch">How many makes each order asks for.</param>
        /// <param name="shifts">The blocks the artisan works (<see cref="ShiftBlocks"/>).</param>
        /// <param name="wage">The wage it offers; the session's default wage when 0.</param>
        public WorkshopPlayer(LocalSimulation sim, StationKind kind, ItemType make, ushort batch = 2, byte shifts = ShiftBlocks.DayShift, ushort wage = 0)
        {
            _sim = sim;
            _commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            _kind = kind;
            _make = make;
            _batch = batch;
            _shifts = shifts;
            _wage = wage != 0 ? wage : sim.Simulation.Economy.Workshops.DefaultWage;
            sim.Host.TickCompleted += Act;
        }

        /// <summary>When it built its station, first hired, and first ordered; -1 until it has.</summary>
        public long BuiltTick { get; private set; } = -1;
        public long FirstHireTick { get; private set; } = -1;
        public long FirstOrderTick { get; private set; } = -1;

        /// <summary>Everyone it has hired, in order, and how many orders it has placed.</summary>
        public IReadOnlyList<EntityId> Hired => _hired;
        public int Orders { get; private set; }

        /// <summary>Orders to mend worn pieces it gave (Epic 10 Part 2, W4).</summary>
        public int Repairs { get; private set; }

        /// <summary>Stops acting (a scenario that takes over).</summary>
        public void Stop() => _sim.Host.TickCompleted -= Act;

        // One command a tick at most: each applies at the start of the next, so the window it reads is up to date again
        private void Act(long tick)
        {
            if (_commands == null) return;
            var workshops = _sim.Simulation.Workshops;
            var host = (IWorkshopHost)_sim.Simulation;
            int index = workshops.IndexOf(_kind);
            if (index < 0) return;

            if (!workshops[index].Built)
            {
                if (workshops.CanBuild(host, index) != WorkshopRules.Check.Ok) return;
                _commands.QueueInfluence(InfluenceEvent.CreateBuildStation(SourceId, (byte)index));
                if (BuiltTick < 0) BuiltTick = tick;
                return;
            }

            var artisan = workshops.ArtisanAt(index);
            if (artisan == default)
            {
                var crawlers = _sim.Host.CrawlerStates;
                for (int i = 0; i < crawlers.Count; i++)
                {
                    if (workshops.CanHire(host, crawlers[i].AgentId, index, _wage, (uint)tick) != WorkshopRules.Check.Ok) continue;
                    _commands.QueueInfluence(InfluenceEvent.CreateHireArtisan(SourceId, crawlers[i].AgentId, (byte)index, _wage));
                    _hired.Add(crawlers[i].AgentId);
                    if (FirstHireTick < 0) FirstHireTick = tick;
                    return;
                }
                return;
            }

            if (workshops.TryGetContract(artisan, out var contract) && contract.Shifts != _shifts)
            {
                _commands.QueueInfluence(InfluenceEvent.CreateSetShifts(SourceId, artisan, _shifts));
                return;
            }

            if (workshops[index].HasOrder) return;
            // Worn pieces of its make waiting on the shelf are mended first (Epic 10 Part 2, W4, D59): half the inputs, no glowstone
            int worn = _sim.Simulation.ActiveStalls.Count > 0 ? _sim.Simulation.ActiveStalls[0].WornOf(_make) : 0;
            if (worn > 0 && WorkshopRules.CheckOrder(workshops, index, _make, (ushort)worn, repair: true) == WorkshopRules.Check.Ok)
            {
                _commands.QueueInfluence(InfluenceEvent.CreateOrderStation(SourceId, (byte)index, _make, (ushort)worn, repair: true));
                Repairs++;
                return;
            }
            if (WorkshopRules.CheckOrder(workshops, index, _make, _batch) == WorkshopRules.Check.Ok)
            {
                _commands.QueueInfluence(InfluenceEvent.CreateOrderStation(SourceId, (byte)index, _make, _batch));
                Orders++;
                if (FirstOrderTick < 0) FirstOrderTick = tick;
            }
        }
    }
}
