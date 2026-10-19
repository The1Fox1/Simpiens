using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Simulation;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The selected crawler's dossier (Unity's third column): name and title, state, the four drives, the GOAP goal and
    /// action, and its affinities.
    /// </summary>
    public sealed class DossierModel
    {
        /// <summary>The name shown for the player in a crawler's affinities (trades and bounties are with the outpost; Unity printed the id's first characters).</summary>
        public const string PlayerName = "You (the outpost)";

        public const string EmptyText = "Select an agent from the sector roster to view live psychological & biological telemetry.";

        private readonly DashboardContext _context;
        private readonly List<string> _affinities = new List<string>(32);

        internal DossierModel(DashboardContext context) => _context = context;

        /// <summary>False: the empty state shows instead.</summary>
        public bool HasCrawler { get; private set; }
        public EntityId CrawlerId { get; private set; }

        public string Name { get; private set; } = string.Empty;
        public AgentVisualState State { get; private set; }
        public string Badge { get; private set; } = string.Empty;

        public float Hunger { get; private set; }
        public float Energy { get; private set; }
        public float Social { get; private set; }
        public float Frustration { get; private set; }

        public string Goal { get; private set; } = string.Empty;
        public string Action { get; private set; } = string.Empty;

        /// <summary>"Name: W:+3 | T:+1 | F:0 | R:+2 (4x)", in the memory's order.</summary>
        public IReadOnlyList<string> Affinities => _affinities;

        public static string Percent(float value) => Invariant($"{value:F0}%");

        /// <summary>The Calm Frustration button works only with a crawler shown and a queue to send to.</summary>
        public bool CanCalm => HasCrawler && _context.Intervention != null;

        /// <summary>Calm Frustration: queues a <see cref="CalmCrawlerCommand"/>; the crawler calms on the next tick (Unity changed it at once, from the UI).</summary>
        public void Calm()
        {
            if (!CanCalm) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateCalmCrawler("Player", CrawlerId));
        }

        public void Refresh(EntityId crawler)
        {
            var node = crawler != default ? _context.Registry?.GetEntity(crawler) : null;
            var agent = node?.Agent;
            HasCrawler = agent != null;
            CrawlerId = HasCrawler ? node.Id : default;
            _affinities.Clear();
            if (!HasCrawler) return;

            string friendly = _context.FriendlyName(node.Id, node.Name);
            string title = _context.Identity != null ? _context.Identity.GetFriendlyTitle(node.Id) : string.Empty;
            Name = string.IsNullOrEmpty(title) ? $"{friendly} [{node.Name}]" : $"{friendly} the {title} [{node.Name}]";

            State = agent.VisualState;
            Badge = State.ToString().ToUpperInvariant();
            Hunger = agent.Hunger;
            Energy = agent.Energy;
            Social = agent.Social;
            Frustration = agent.Frustration;

            Goal = agent.ActiveGoal != null ? agent.ActiveGoal.GetType().Name : "(None)";
            Action = agent.HasActivePlan && agent.ActivePlan.CurrentAction != null ? agent.ActivePlan.CurrentAction.GetType().Name : "(Idle)";

            var map = agent.Memory?.AffinityMap;
            if (map == null) return;
            foreach (var pair in map)
            {
                var peer = _context.Registry.GetEntity(pair.Key);
                string peerName = pair.Key == WorldRegistry.PlayerEntityId ? PlayerName
                    : peer != null ? _context.FriendlyName(peer.Id, peer.Name) : pair.Key.ToString().Substring(0, 6);
                var rel = pair.Value;
                _affinities.Add(Invariant($"{peerName}: W:{rel.Warmth:+0;-#;0} | T:{rel.Trust:+0;-#;0} | F:{rel.Fear:+0;-#;0} | R:{rel.Respect:+0;-#;0} ({rel.InteractionCount}x)"));
            }
        }
    }
}
