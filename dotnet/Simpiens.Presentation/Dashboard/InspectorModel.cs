using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The map inspector (Epic UI-Beta B4, Q10): a card at the bottom left of the map view while a crawler (or, since Epic
    /// 10 Part 3's K2.5, a creature) is selected and the panels are hidden. Its portrait, name, state, how it feels, health and needs, and what it is doing, read live as
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

        /// <summary>
        /// A creature is shown (Epic 10 Part 3, K2.5): its kind's card in place of a portrait, its kind's name, what it is
        /// doing, its health; no needs, and no dossier.
        /// </summary>
        public bool IsCreature { get; private set; }

        /// <summary>The creature's kind, while one is shown.</summary>
        public Simpiens.Simulation.Creatures.CreatureKind CreatureKind { get; private set; }

        /// <summary>
        /// Re-reads <paramref name="crawler"/>, or <paramref name="creature"/> when no crawler is given; neither (none
        /// selected, or the panels shown) hides the card.
        /// </summary>
        public void Refresh(EntityId crawler, bool following, EntityId creature = default)
        {
            IsCreature = false;
            if (crawler == default && creature != default)
            {
                RefreshCreature(creature, following);
                return;
            }
            var node = crawler != default ? _context.Registry?.GetEntity(crawler) : null;
            var agent = node?.Agent;
            IsShown = agent != null;
            if (!IsShown) return;
            CrawlerId = node.Id;
            Following = following;
            Name = _context.LegendName(node.Id, node.Name); // with its epithet (Epic 11, S6.6)
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

        // A creature (K2.5): its kind, its lair's room, what it does (after whom), its health against its kind's
        private void RefreshCreature(EntityId id, bool following)
        {
            var node = _context.Registry?.GetEntity(id);
            var creature = node?.Creature;
            var kind = creature?.Definition;
            IsShown = kind != null;
            if (!IsShown) return;
            IsCreature = true;
            CreatureKind = creature.Kind;
            CrawlerId = node.Id;
            Following = following;
            HasPortrait = false;
            Name = kind.DisplayName;
            State = StateOf(creature.Mode);
            Badge = BadgeOf(creature.Mode);
            var map = _context.Sectors?.Map;
            string room = map != null && creature.LairIndex >= 0 && creature.LairIndex < map.Lairs.Count ? map.FindSector(map.Lairs[creature.LairIndex].Position).Name : null;
            MoodLine = room != null ? $"Wild. Its lair is in {room}." : "Wild.";
            Health = kind.MaxHealth > 0f ? 100f * creature.Health / kind.MaxHealth : 0f;
            Hunger = Energy = Social = Frustration = 0f;
            Action = ActionOf(creature);
        }

        /// <summary>A creature's badge by what it does (K2.5).</summary>
        public static string BadgeOf(Simpiens.Simulation.Creatures.CreatureMode mode) => mode switch
        {
            Simpiens.Simulation.Creatures.CreatureMode.Roam => "ROAMING",
            Simpiens.Simulation.Creatures.CreatureMode.Chase => "HUNTING",
            Simpiens.Simulation.Creatures.CreatureMode.Flee => "FLEEING",
            Simpiens.Simulation.Creatures.CreatureMode.Home => "HOMEWARD",
            _ => "RESTING",
        };

        // The badge's colour: the crawlers' state colours, by what reads alike
        private static Simpiens.Entities.AgentVisualState StateOf(Simpiens.Simulation.Creatures.CreatureMode mode) => mode switch
        {
            Simpiens.Simulation.Creatures.CreatureMode.Roam => Simpiens.Entities.AgentVisualState.Walking,
            Simpiens.Simulation.Creatures.CreatureMode.Chase => Simpiens.Entities.AgentVisualState.Panicking,
            Simpiens.Simulation.Creatures.CreatureMode.Flee => Simpiens.Entities.AgentVisualState.Yielding,
            Simpiens.Simulation.Creatures.CreatureMode.Home => Simpiens.Entities.AgentVisualState.Walking,
            _ => Simpiens.Entities.AgentVisualState.Resting,
        };

        private string ActionOf(Simpiens.Simulation.Creatures.CreatureState creature)
        {
            switch (creature.Mode)
            {
                case Simpiens.Simulation.Creatures.CreatureMode.Roam: return "Roaming near its lair";
                case Simpiens.Simulation.Creatures.CreatureMode.Chase:
                    var quarry = _context.Registry?.GetEntity(creature.Target);
                    return quarry != null ? $"Hunting {_context.FriendlyName(quarry.Id, quarry.Name)}" : "Hunting";
                case Simpiens.Simulation.Creatures.CreatureMode.Flee: return "Fleeing to its lair, hurt";
                case Simpiens.Simulation.Creatures.CreatureMode.Home: return "Going back to its lair";
                default: return "Resting in its lair";
            }
        }
    }
}
