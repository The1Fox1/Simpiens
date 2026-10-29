using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
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

        /// <summary>The name shown for a peer no longer in the world (Unity printed six characters of its id).</summary>
        public const string UnknownName = "Unknown";

        private static readonly System.Comparison<AffinityRow> WarmestFirst = (a, b) =>
        {
            int byWarmth = b.Warmth.CompareTo(a.Warmth);
            return byWarmth != 0 ? byWarmth : string.CompareOrdinal(a.Name, b.Name);
        };

        private readonly DashboardContext _context;
        private readonly List<string> _affinities = new List<string>(32);
        private readonly List<AffinityRow> _rows = new List<AffinityRow>(32);

        internal DossierModel(DashboardContext context) => _context = context;

        /// <summary>False: the empty state shows instead.</summary>
        public bool HasCrawler { get; private set; }
        public EntityId CrawlerId { get; private set; }

        public string Name { get; private set; } = string.Empty;
        public AgentVisualState State { get; private set; }
        public string Badge { get; private set; } = string.Empty;

        /// <summary>Health, 0 to 100 (Epic 10 G2); a downed crawler's badge reads <see cref="DownedBadge"/>.</summary>
        public float Health { get; private set; }
        public const string DownedBadge = "DOWNED";

        /// <summary>The worn protection and its wear (Epic 10 G3): "Hazard Gear, 64% left", or "None".</summary>
        public string Gear { get; private set; } = string.Empty;
        public const string NoGear = "None";

        public float Hunger { get; private set; }
        public float Energy { get; private set; }
        public float Social { get; private set; }
        public float Frustration { get; private set; }

        public string Goal { get; private set; } = string.Empty;

        /// <summary>Empty for a resident; for a drifter (Epic 10 G1), when it moves on: "drifter, leaves in 3 min".</summary>
        public string Residency { get; private set; } = string.Empty;
        public string Action { get; private set; } = string.Empty;

        /// <summary>
        /// "Name: W:+3 | T:+1 | F:0 | R:+2 (4x)", warmest first and then by name (review S8: the memory's own order is a
        /// dictionary's, so the list reshuffled as relationships changed).
        /// </summary>
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
            Residency = Stay(agent.DepartsAtTick, _context.Clock?.CurrentTick ?? 0);
            if (Residency.Length > 0) Name += " · " + Residency;

            State = agent.VisualState;
            Badge = agent.IsDowned ? DownedBadge : State.ToString().ToUpperInvariant();
            Health = agent.Health;
            Gear = GearText(agent.Inventory != null ? agent.Inventory.GetEquipped(EquipmentSlot.Protection) : default);
            Hunger = agent.Hunger;
            Energy = agent.Energy;
            Social = agent.Social;
            Frustration = agent.Frustration;

            Goal = PlanText.Goal(agent.ActiveGoal);
            Action = PlanText.Action(agent.HasActivePlan ? agent.ActivePlan.CurrentAction : null);

            var map = agent.Memory?.AffinityMap;
            if (map == null) return;
            _rows.Clear();
            foreach (var pair in map)
            {
                var peer = _context.Registry.GetEntity(pair.Key);
                string peerName = pair.Key == WorldRegistry.PlayerEntityId ? PlayerName
                    : peer != null ? _context.FriendlyName(peer.Id, peer.Name) : UnknownName;
                var rel = pair.Value;
                _rows.Add(new AffinityRow(rel.Warmth, peerName, Invariant($"{peerName}: W:{rel.Warmth:+0;-#;0} | T:{rel.Trust:+0;-#;0} | F:{rel.Fear:+0;-#;0} | R:{rel.Respect:+0;-#;0} ({rel.InteractionCount}x)")));
            }
            _rows.Sort(WarmestFirst);
            for (int i = 0; i < _rows.Count; i++) _affinities.Add(_rows[i].Line);
        }

        /// <summary>
        /// Equipment in words, by its catalog name: what is left of its life (its definition's durability), rounded up so
        /// equipment that still works never reads 0%.
        /// </summary>
        public static string GearText(in ItemInstance gear)
        {
            if (gear.IsEmpty) return NoGear;
            var definition = ItemCatalog.Get(gear.Type);
            ushort life = definition?.Equipment?.Durability ?? 0;
            int left = life > 0 ? (gear.Durability * 100 + life - 1) / life : 100;
            return Invariant($"{definition?.DisplayName ?? ItemCatalog.NameOf(gear.Type)}, {left}% left");
        }

        /// <summary>A drifter's stay in words, by the sim-minute (600 ticks); empty for a resident.</summary>
        public static string Stay(uint departsAtTick, long now)
        {
            if (departsAtTick == 0) return string.Empty;
            long left = departsAtTick - now;
            if (left <= 0) return "drifter, leaving";
            long minutes = (left + 599) / 600;
            return minutes == 1 ? "drifter, leaves in 1 min" : Invariant($"drifter, leaves in {minutes} min");
        }

        private readonly struct AffinityRow
        {
            public AffinityRow(float warmth, string name, string line)
            {
                Warmth = warmth;
                Name = name;
                Line = line;
            }

            public float Warmth { get; }
            public string Name { get; }
            public string Line { get; }
        }
    }
}
