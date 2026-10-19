using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.UI;
using Simpiens.Simulation;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>A sector in the dashboard's list: its crawler count, or SHROUDED while uncharted.</summary>
    public readonly struct SectorRow
    {
        public readonly int SectorId;
        public readonly string Name;
        public readonly string Badge;
        public readonly bool Shrouded;
        public readonly bool Selected;

        public SectorRow(int sectorId, string name, string badge, bool shrouded, bool selected)
        {
            SectorId = sectorId;
            Name = name;
            Badge = badge;
            Shrouded = shrouded;
            Selected = selected;
        }
    }

    /// <summary>A crawler in the selected sector, as Unity's resident card showed it.</summary>
    public readonly struct ResidentRow
    {
        public readonly EntityId CrawlerId;
        public readonly string Name;
        public readonly string Activity;
        public readonly AgentVisualState State;
        public readonly string Badge;
        public readonly bool Selected;

        public ResidentRow(EntityId crawlerId, string name, string activity, AgentVisualState state, bool selected)
        {
            CrawlerId = crawlerId;
            Name = name;
            Activity = activity;
            State = state;
            Badge = state.ToString().ToUpperInvariant();
            Selected = selected;
        }
    }

    /// <summary>
    /// The dashboard's first two columns (Unity's sectors panel and sector detail): every sector with its occupancy,
    /// then the selected sector's description, its residents and its last 100 chronicle events.
    /// </summary>
    public sealed class SectorPanelModel
    {
        public const int MaxEvents = 100;
        public const string UnchartedSuffix = " [UNCHARTED: post a Scout bounty to have it mapped]";

        private readonly DashboardContext _context;
        private readonly List<SectorRow> _sectors = new List<SectorRow>(16);
        private readonly List<ResidentRow> _residents = new List<ResidentRow>(32);
        private readonly List<string> _events = new List<string>(MaxEvents);
        private readonly List<WorldEntity> _inZone = new List<WorldEntity>(32);
        private readonly List<ChronicleEvent> _chronicle = new List<ChronicleEvent>(MaxEvents);

        internal SectorPanelModel(DashboardContext context) => _context = context;

        /// <summary>The sector shown in the detail column (the map's first real sector by default).</summary>
        public int SelectedSectorId { get; internal set; } = -1;

        public IReadOnlyList<SectorRow> Sectors => _sectors;
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public IReadOnlyList<ResidentRow> Residents => _residents;
        public IReadOnlyList<string> Events => _events;

        /// <summary>Keeps the selection while it exists on this map; otherwise picks the map's first real sector (id 0 is transit).</summary>
        internal void EnsureSelection()
        {
            if (_context.Zones.TryGetZone(SelectedSectorId, out _)) return;
            var zones = _context.Zones.GetAllZones();
            SelectedSectorId = 0;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].ZoneId == 0) continue;
                SelectedSectorId = zones[i].ZoneId;
                break;
            }
        }

        public void Refresh(EntityId selectedCrawler)
        {
            EnsureSelection();
            var zones = _context.Zones.GetAllZones();
            _sectors.Clear();
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                bool explored = _context.Zones.IsSectorExplored(zone.ZoneId);
                string badge = explored ? _context.Zones.GetOccupancyCount(zone.ZoneId, _context.Registry).ToString(System.Globalization.CultureInfo.InvariantCulture) : "SHROUDED";
                _sectors.Add(new SectorRow(zone.ZoneId, zone.Name, badge, !explored, zone.ZoneId == SelectedSectorId));
            }

            var selected = _context.Zones.GetZoneById(SelectedSectorId);
            Title = (selected.Name ?? string.Empty).ToUpperInvariant();
            Description = _context.Zones.IsSectorExplored(SelectedSectorId) ? selected.Description : selected.Description + UnchartedSuffix;

            _residents.Clear();
            _context.Zones.GetAgentsInZone(SelectedSectorId, _context.Registry, _inZone);
            for (int i = 0; i < _inZone.Count; i++)
            {
                var node = _inZone[i];
                if (node == null) continue;
                var agent = node.Agent;
                var state = agent != null ? agent.VisualState : AgentVisualState.Idle;
                _residents.Add(new ResidentRow(node.Id, $"{_context.FriendlyName(node.Id, node.Name)} [{node.Name}]", ActivityOf(agent), state, node.Id == selectedCrawler));
            }

            _events.Clear();
            _chronicle.Clear();
            _context.Chronicle?.GetSectorEvents(SelectedSectorId, _chronicle, MaxEvents);
            uint now = _context.Clock != null ? (uint)_context.Clock.CurrentTick : 0u;
            for (int i = 0; i < _chronicle.Count; i++)
            {
                var evt = _chronicle[i];
                string narrative = _context.Formatter != null ? _context.Formatter.FormatEvent(evt) : Invariant($"{evt.EventType} (Sector {evt.SectorId})");
                _events.Add($"{RelativeTimeFormatter.Format(evt.SimulationTick, now)} {narrative}");
            }
        }

        /// <summary>What a crawler is doing, in words (Unity's resident card).</summary>
        public static string ActivityOf(AgentState agent)
        {
            if (agent == null) return "Unknown";
            if (agent.IsInTantrum) return "Throwing tantrum in anger";
            if (agent.IsStarvationPanicking) return "Panicking from severe hunger!";
            if (agent.IsExhaustionCollapsed) return "Collapsed from exhaustion";
            if (agent.IsYielding) return "Yielding to dominant peer";
            if (agent.IsSharing) return "Sharing food rations";
            if (agent.IsGossiping) return "Gossiping with neighbor";
            if (agent.HasActivePlan && agent.ActivePlan.CurrentAction != null) return agent.ActivePlan.CurrentAction.GetType().Name.Replace("Action", "");
            return "Loitering / Idle";
        }
    }
}
