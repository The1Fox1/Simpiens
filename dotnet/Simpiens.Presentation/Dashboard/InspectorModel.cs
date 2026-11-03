using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The map inspector (Epic UI-Beta B4, Q10): a card at the bottom left of the map view while a crawler is selected and
    /// the panels are hidden. Its portrait, name, state, how it feels, health and needs, and what it is doing, read live as
    /// the dossier reads them; Open dossier and Follow go through the dashboard model. It decides nothing.
    /// </summary>
    public sealed class InspectorModel
    {
        private readonly DashboardContext _context;
        private readonly MoodTracker _moods;

        internal InspectorModel(DashboardContext context, MoodTracker moods)
        {
            _context = context;
            _moods = moods;
        }

        /// <summary>Shown: a crawler is selected on the map, with the panels hidden.</summary>
        public bool IsShown { get; private set; }

        public EntityId CrawlerId { get; private set; }
        public bool HasPortrait { get; private set; }
        public PortraitKey Portrait { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public Simpiens.Entities.AgentVisualState State { get; private set; }
        public string Badge { get; private set; } = string.Empty;
        public string MoodLine { get; private set; } = string.Empty;
        public float Health { get; private set; }
        public float Hunger { get; private set; }
        public float Energy { get; private set; }
        public float Social { get; private set; }
        public float Frustration { get; private set; }
        public string Action { get; private set; } = string.Empty;

        /// <summary>The camera follows it.</summary>
        public bool Following { get; private set; }

        /// <summary>The Follow button's words.</summary>
        public string FollowText => Following ? "Stop following" : "Follow";

        /// <summary>Re-reads <paramref name="crawler"/>; default (none selected, or the panels shown) hides the card.</summary>
        public void Refresh(EntityId crawler, bool following)
        {
            var node = crawler != default ? _context.Registry?.GetEntity(crawler) : null;
            var agent = node?.Agent;
            IsShown = agent != null;
            if (!IsShown) return;
            CrawlerId = node.Id;
            Following = following;
            Name = _context.FriendlyName(node.Id, node.Name);
            State = agent.VisualState;
            Badge = agent.IsDowned ? DossierModel.DownedBadge : State.ToString().ToUpperInvariant();
            HasPortrait = agent.Look.IsComplete;
            Portrait = PortraitKey.Of(node.Id, agent, _moods, _context.Clock?.CurrentTick ?? 0);
            MoodLine = DossierModel.MoodText(Portrait.Mood, agent.Health, agent.IsDowned);
            Health = agent.Health;
            Hunger = agent.Hunger;
            Energy = agent.Energy;
            Social = agent.Social;
            Frustration = agent.Frustration;
            Action = PlanText.Action(agent.HasActivePlan ? agent.ActivePlan.CurrentAction : null);
        }
    }
}
