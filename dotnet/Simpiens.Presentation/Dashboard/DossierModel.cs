using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation.People;
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
        private readonly GuildModel _guild;
        private readonly MoodTracker _moods;
        private CrawlerLook _describedLook;
        private CrawlerStanding _describedStanding;
        private Mood _moodShown;
        private int _healthShown = -1;
        private readonly List<string> _affinities = new List<string>(32);
        private readonly List<AffinityRow> _rows = new List<AffinityRow>(32);
        private readonly List<AffinityRow> _shownRows = new List<AffinityRow>(32);

        internal DossierModel(DashboardContext context, GuildModel guild = null, MoodTracker moods = null)
        {
            _context = context;
            _guild = guild;
            _moods = moods ?? new MoodTracker();
        }

        /// <summary>Whether the crawler has a look to show (Epic UI-Beta B3): every crawler in play does; one built by hand may not.</summary>
        public bool HasPortrait { get; private set; }

        /// <summary>The portrait the header shows: its look, its mood now, its standing (the frame) and its helm.</summary>
        public PortraitKey Portrait { get; private set; }

        /// <summary>How it feels (<see cref="MoodRule"/>, held by the dashboard's tracker).</summary>
        public Mood Mood { get; private set; }

        /// <summary>"Human, average build. Resident."</summary>
        public string Description { get; private set; } = string.Empty;

        /// <summary>"Content. Health 100%."</summary>
        public string MoodLine { get; private set; } = string.Empty;

        /// <summary>How a crawler feels and its health, in words: "Content. Health 100%.", or "Hurt. Downed."</summary>
        public static string MoodText(Mood mood, float health, bool downed)
            => downed ? $"{mood}. Downed." : Invariant($"{mood}. Health {(int)System.MathF.Round(health)}%.");

        /// <summary>A standing in words.</summary>
        public static string StandingText(CrawlerStanding standing) => standing switch
        {
            CrawlerStanding.Drifter => "Drifter",
            CrawlerStanding.GuildMember => "Guild member",
            _ => "Resident",
        };

        /// <summary>The crawler's groups (Epic 10 G4): "Settlement: led by Mara · Guild: member, 5g a visit", or "No groups".</summary>
        public string Groups { get; private set; } = string.Empty;

        /// <summary>Whether it would sign on with the guild now, in words; empty for a member.</summary>
        public string RecruitText { get; private set; } = string.Empty;

        /// <summary>The Recruit button works only when the guild's rule would let it sign on now.</summary>
        public bool CanRecruit { get; private set; }

        /// <summary>Recruit: a recruit command at the default wage; it signs on the next tick.</summary>
        public void Recruit()
        {
            if (CanRecruit) _guild?.Recruit(CrawlerId);
        }

        /// <summary>False: the empty state shows instead.</summary>
        public bool HasCrawler { get; private set; }

        /// <summary>
        /// The crawler shown is dead (Epic 10 Part 3, K5.8c): its dossier from the departed list, its portrait greyed, its
        /// death in place of its description; nothing to do for it.
        /// </summary>
        public bool IsDead { get; private set; }
        public const string DeadBadge = "DEAD";
        public EntityId CrawlerId { get; private set; }

        public string Name { get; private set; } = string.Empty;
        public AgentVisualState State { get; private set; }
        public string Badge { get; private set; } = string.Empty;

        /// <summary>Health, 0 to 100 (Epic 10 G2); a downed crawler's badge reads <see cref="DownedBadge"/>.</summary>
        public float Health { get; private set; }
        public const string DownedBadge = "DOWNED";

        /// <summary>
        /// What it wears and each piece's wear, a row for each slot in the table's display order (Epic 10 G3's protection and
        /// W1.6b's tool, every slot since Epic 10 Part 3, K1.5): "Hard Hat 64%", "Hazard Gear 80%"; none worn reads <see cref="NoGear"/>.
        /// </summary>
        public WornPieces Equipment { get; } = new WornPieces();

        /// <summary>Its skills, the best four first (Epic 10 Part 3, K9.5): "Smithing 5 · Foraging 4 · Melee 1 · Mining 1"; so a recruit's show before it is signed.</summary>
        public string Skills { get; private set; } = string.Empty;

        /// <summary>
        /// Its skills as the dossier lists them, each with its passion (Epic 11, S1.6): the best four, and every skill it loves
        /// even at 0 (its nature); a flame marks keen and burning.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<SkillTexts.Shown> SkillsShown => _skillsShown;
        private readonly System.Collections.Generic.List<SkillTexts.Shown> _skillsShown = new System.Collections.Generic.List<SkillTexts.Shown>(Simpiens.Simulation.Skills.SkillTable.Capacity);
        public const string NoGear = "None";

        /// <summary>A skill the mastery budget holds (Epic 11, S2.5, D7): "Foraging held at 5: two crafts past Apprentice"; empty when none is.</summary>
        public string SkillHeld { get; private set; } = string.Empty;

        /// <summary>
        /// Who taught it (Epic 11, S3.5, D9): each skill's first teacher from its traits record, "Mining taught by Bram", the
        /// departed named from the departed roll; joined by " · ", empty when nobody taught it.
        /// </summary>
        public string Taught { get; private set; } = string.Empty;

        /// <summary>The techniques it holds (Epic 11, S4.9, D18), by name in the skills' order: "Shield Wall · Seam Student"; empty for none.</summary>
        public string Techniques { get; private set; } = string.Empty;

        /// <summary>Each technique with its skill and what it does, a line each, for the line's tooltip: "Shield Wall (Melee): +3 defense, 15% less damage".</summary>
        public string TechniquesTip { get; private set; } = string.Empty;

        /// <summary>A guild member's calling's part in a party (Epic 11, S5.8): its badge at the portrait's corner; none for the rest.</summary>
        public Simpiens.Simulation.Skills.PartyRole Role { get; private set; }

        /// <summary>One of the patron's people (a guild member or an artisan): the sway's second action sponsors a technique rather than recruits.</summary>
        public bool IsOurs { get; private set; }

        /// <summary>
        /// The sponsorship on offer (Epic 11, S4.9, D18): the other technique at the top band of its best skill, "Sponsor
        /// Reckless Fury for Shield Wall: 20g", or why not; empty for one not of the patron's people or holding none.
        /// </summary>
        public string SponsorText { get; private set; } = string.Empty;

        /// <summary>The Sponsor button works only when sponsorship's rule would let it take the offer now.</summary>
        public bool CanSponsor { get; private set; }

        /// <summary>The technique the Sponsor button offers; none when there is no offer.</summary>
        public Simpiens.Simulation.Skills.Technique SponsorOffer { get; private set; }

        /// <summary>Sponsor: queues a sponsorship of the offer; it takes it up on the next tick, as the rule allows then.</summary>
        public void Sponsor()
        {
            if (!CanSponsor || _context.Intervention == null) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateSponsorTechnique("Player", CrawlerId, SponsorOffer));
        }

        /// <summary>The line under the skills: who taught it, then a skill the budget holds; empty when neither.</summary>
        public string SkillNotes => Taught.Length == 0 ? SkillHeld : SkillHeld.Length == 0 ? Taught : Taught + " · " + SkillHeld;

        /// <summary>A member's or artisan's asking wage above its wage (Epic 11, S2.5, D8): "asks 7g"; empty otherwise.</summary>
        public string Asks { get; private set; } = string.Empty;

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

        /// <summary>The affinities as rows (Epic UI-Beta B4): the peer, its face when it is a crawler with a look, and the line.</summary>
        public IReadOnlyList<AffinityRow> AffinityRows => _shownRows;

        public static string Percent(float value) => Invariant($"{value:F0}%");

        /// <summary>The Calm Frustration button works only with a crawler shown and a queue to send to.</summary>
        public bool CanCalm => HasCrawler && !IsDead && _context.Intervention != null;

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
            IsDead = false;
            CrawlerId = HasCrawler ? node.Id : default;
            _affinities.Clear();
            _shownRows.Clear();
            Equipment.Refresh(null);
            Skills = string.Empty;
            _skillsShown.Clear();
            SkillHeld = string.Empty;
            Asks = string.Empty;
            Taught = string.Empty;
            Techniques = TechniquesTip = SponsorText = string.Empty;
            IsOurs = CanSponsor = false;
            Role = Simpiens.Simulation.Skills.PartyRole.None;
            SponsorOffer = Simpiens.Simulation.Skills.Technique.None;
            if (!HasCrawler)
            {
                if (crawler != default) ShowTheDead(crawler);
                return;
            }

            string friendly = _context.FriendlyName(node.Id, node.Name);
            // Its calling's title (Epic 11, S5.7, D24: "Bram the Warden"), else the identity's placeholder title
            string title = CallingTexts.Title(agent.Skills, agent.Traits);
            if (title.Length == 0) title = _context.Identity != null ? _context.Identity.GetFriendlyTitle(node.Id) : string.Empty;
            Name = (string.IsNullOrEmpty(title) ? friendly : $"{friendly} the {title}") + _context.DebugSuffix(node.Name);
            Residency = Stay(agent.DepartsAtTick, _context.Clock?.CurrentTick ?? 0);
            if (Residency.Length > 0) Name += " · " + Residency;

            State = agent.VisualState;
            Badge = agent.IsDowned ? DownedBadge : State.ToString().ToUpperInvariant();
            Health = agent.Health;
            Describe(node.Id, agent);
            Equipment.Refresh(agent.Inventory);
            Skills = SkillTexts.Best(agent.Skills, 4);
            SkillTexts.BestShown(agent.Skills, agent.Traits, 4, keepPassions: true, _skillsShown);
            SkillHeld = SkillTexts.Held(agent.Skills);
            Taught = TaughtBy(agent.Traits);
            Asks = AsksOf(agent);
            DescribeTechniques(agent);
            Hunger = agent.Hunger;
            Energy = agent.Energy;
            Social = agent.Social;
            Frustration = agent.Frustration;

            Goal = PlanText.Goal(agent.ActiveGoal);
            Action = PlanText.Action(agent.HasActivePlan ? agent.ActivePlan.CurrentAction : null);

            Groups = _guild != null ? _guild.GroupsOf(node.Id) : string.Empty;
            RecruitText = _guild != null ? _guild.RecruitText(node.Id) : string.Empty;
            CanRecruit = _guild != null && _guild.CanRecruit(node.Id);

            var map = agent.Memory?.AffinityMap;
            if (map == null) return;
            _rows.Clear();
            foreach (var pair in map)
            {
                var peer = _context.Registry.GetEntity(pair.Key);
                string peerName = pair.Key == WorldRegistry.PlayerEntityId ? PlayerName
                    : peer != null ? _context.FriendlyName(peer.Id, peer.Name) : UnknownName;
                var rel = pair.Value;
                var peerAgent = peer?.Agent;
                bool face = peerAgent != null && peerAgent.Look.IsComplete;
                var portrait = face ? PortraitKey.Of(peer.Id, peerAgent, _moods, _context.Clock?.CurrentTick ?? 0) : default;
                _rows.Add(new AffinityRow(pair.Key, rel.Warmth, peerName, Invariant($"{peerName}: W:{rel.Warmth:+0;-#;0} | T:{rel.Trust:+0;-#;0} | F:{rel.Fear:+0;-#;0} | R:{rel.Respect:+0;-#;0} ({rel.InteractionCount}x)"), face, portrait));
            }
            _rows.Sort(WarmestFirst);
            for (int i = 0; i < _rows.Count; i++)
            {
                _affinities.Add(_rows[i].Line);
                _shownRows.Add(_rows[i]);
            }
        }

        // The techniques it holds, and the sponsorship on offer to one of the patron's people (Epic 11, S4.9, D18)
        private void DescribeTechniques(Simpiens.Cognition.AgentState agent)
        {
            System.Span<Simpiens.Simulation.Skills.Technique> held = stackalloc Simpiens.Simulation.Skills.Technique[Simpiens.Simulation.Skills.CrawlerTraits.TechniqueSlots];
            int count = Simpiens.Simulation.Skills.CrawlerTechniques.Held(agent.Traits, held);
            var names = new System.Text.StringBuilder();
            var tip = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var row = Simpiens.Simulation.Skills.TechniqueTable.Get(held[i]);
                if (row == null) continue;
                if (names.Length > 0) { names.Append(" · "); tip.Append('\n'); }
                names.Append(row.DisplayName);
                tip.Append(row.DisplayName).Append(" (").Append(Simpiens.Simulation.Skills.SkillTable.Get(row.Skill)?.DisplayName).Append("): ").Append(row.Words);
            }
            Techniques = names.ToString();
            TechniquesTip = tip.ToString();

            var sim = _context.Simulation;
            var host = sim as Simpiens.Simulation.Guild.IGuildHost;
            if (sim == null || host == null) return;
            bool member = sim.Guild.TryGetContract(agent.AgentId, out _);
            Role = member ? Simpiens.Simulation.Skills.CallingTable.RoleOf(agent.Traits.Calling) : Simpiens.Simulation.Skills.PartyRole.None;
            IsOurs = member || (sim.Workshops != null && sim.Workshops.IsArtisan(agent.AgentId));
            if (!IsOurs) return;
            var offer = Simpiens.Simulation.Guild.SponsorRules.Offer(agent.Skills, agent.Traits, out var replacing);
            if (offer == Simpiens.Simulation.Skills.Technique.None)
            {
                SponsorText = "No technique to sponsor yet.";
                return;
            }
            uint tick = (uint)(_context.Clock?.CurrentTick ?? 0);
            var check = Simpiens.Simulation.Guild.SponsorRules.CheckSponsor(sim.Guild, sim.Workshops, host, agent.AgentId, offer, tick, out ushort price);
            SponsorOffer = offer;
            CanSponsor = check == Simpiens.Simulation.Guild.SponsorRules.Check.Ok && _context.Intervention != null;
            string name = Simpiens.Simulation.Skills.TechniqueTable.Get(offer).DisplayName, instead = Simpiens.Simulation.Skills.TechniqueTable.Get(replacing)?.DisplayName;
            SponsorText = check == Simpiens.Simulation.Guild.SponsorRules.Check.Ok
                ? Invariant($"Sponsor {name} for {instead}: {price}g.")
                : Invariant($"Sponsor {name}? ") + Simpiens.Simulation.Guild.SponsorRules.Describe(check, agent, offer, price, sim.Economy.Guild, tick);
        }

        // Each skill's first teacher (Epic 11, S3.5): a living teacher by its name here, the departed by the roll's
        private string TaughtBy(in Simpiens.Simulation.Skills.CrawlerTraits traits)
        {
            var parts = new System.Collections.Generic.List<string>(Simpiens.Simulation.Skills.CrawlerTraits.TeacherSlots);
            for (int i = 0; i < Simpiens.Simulation.Skills.CrawlerTraits.TeacherSlots; i++)
            {
                var record = traits.TeacherAt(i);
                if (record.Skill == Simpiens.Simulation.Skills.Skill.None || record.Teacher == default) continue;
                var skill = Simpiens.Simulation.Skills.SkillTable.Get(record.Skill);
                if (skill == null) continue;
                parts.Add($"{skill.DisplayName} taught by {TeacherName(record.Teacher)}");
            }
            return string.Join(" · ", parts);
        }

        private string TeacherName(EntityId teacher)
        {
            var node = _context.Registry?.GetEntity(teacher);
            if (node?.Agent != null) return _context.FriendlyName(teacher, node.Agent.GivenName ?? UnknownName);
            var departed = _context.Departed?.Departed;
            for (int i = departed != null ? departed.Count - 1 : -1; i >= 0; i--)
                if (departed[i].Id == teacher && !string.IsNullOrEmpty(departed[i].GivenName)) return departed[i].GivenName;
            return _context.FriendlyName(teacher, UnknownName);
        }

        // A member's or artisan's asking wage against its contract's (Epic 11, S2.5, D8)
        private string AsksOf(Simpiens.Cognition.AgentState agent)
        {
            var sim = _context.Simulation;
            if (sim == null) return string.Empty;
            if (sim.Guild.TryGetContract(agent.AgentId, out var member))
                return SkillTexts.Asks(member.Wage, Simpiens.Simulation.Guild.GuildRules.AskingWage(agent, sim.Economy.Guild));
            if (sim.Workshops != null && sim.Workshops.TryGetContract(agent.AgentId, out var artisan))
                return SkillTexts.Asks(artisan.Wage, Simpiens.Simulation.Workshops.WorkshopRules.AskingWage(agent, sim.Economy.Workshops, sim.Economy.Guild));
            return string.Empty;
        }

        // The header (Epic UI-Beta B3): the portrait's key, and its words, rebuilt only when they change
        // The dossier of the dead (K5.8c): who it was, greyed, and what it died of, from the departed list
        private void ShowTheDead(EntityId id)
        {
            var departed = _context.Departed?.Departed;
            if (departed == null) return;
            Simpiens.Simulation.People.DepartedCrawler dead = null;
            for (int i = departed.Count - 1; i >= 0 && dead == null; i--) if (departed[i].Id == id && departed[i].Died) dead = departed[i];
            if (dead == null) return;
            HasCrawler = true;
            IsDead = true;
            CrawlerId = id;
            Name = (dead.GivenName ?? _context.FriendlyName(id, "Unknown")) + " · died";
            Description = DeathText(dead);
            MoodLine = "At rest.";
            HasPortrait = dead.Look.IsComplete;
            Portrait = new PortraitKey(dead.Look, Simpiens.Presentation.Art.People.Mood.Content, CrawlerStanding.Resident, default, greyed: true);
            State = AgentVisualState.Idle;
            Badge = DeadBadge;
            Health = Hunger = Energy = Social = Frustration = 0f;
            Goal = "(None)";
            Action = "(Gone)";
            Residency = string.Empty;
            Groups = string.Empty;
            RecruitText = string.Empty;
            CanRecruit = false;
        }

        /// <summary>What the departed list says of a death: "Downed by a stone beetle; bled out, Day 2 · 14:20" (K5.8c).</summary>
        public static string DeathText(Simpiens.Simulation.People.DepartedCrawler dead)
        {
            string when = dead.Tick > 0 ? ", " + OutpostClock.Text(dead.Tick) : string.Empty;
            switch (dead.Cause)
            {
                case Simpiens.Simulation.DeathCause.Creature:
                    string kind = Simpiens.Simulation.Creatures.CreatureCatalog.Get((Simpiens.Simulation.Creatures.CreatureKind)dead.CauseDetail)?.DisplayName ?? "creature";
                    return $"Downed by a {kind.ToLowerInvariant()}; bled out{when}.";
                case Simpiens.Simulation.DeathCause.Hazard:
                    return $"Downed by {HazardWords((Simpiens.Simulation.World.HazardKind)dead.CauseDetail)}; bled out{when}.";
                default:
                    return $"Gone{when}.";
            }
        }

        private static string HazardWords(Simpiens.Simulation.World.HazardKind kind) => kind switch
        {
            Simpiens.Simulation.World.HazardKind.FoulAir => "foul air",
            Simpiens.Simulation.World.HazardKind.FallingRock => "falling rock",
            Simpiens.Simulation.World.HazardKind.Flooding => "the flood",
            _ => "danger",
        };

        private void Describe(EntityId id, Simpiens.Cognition.AgentState agent)
        {
            var look = agent.Look;
            var standing = WorldViewFrame.StandingOf(agent);
            HasPortrait = look.IsComplete;
            Portrait = PortraitKey.Of(id, agent, _moods, _context.Clock?.CurrentTick ?? 0);
            Mood = Portrait.Mood;
            if (!look.Equals(_describedLook) || standing != _describedStanding || Description.Length == 0)
            {
                _describedLook = look;
                _describedStanding = standing;
                var lineage = LookCatalog.Lineage(look.Lineage);
                Description = HasPortrait
                    ? $"{lineage?.Name ?? "Unknown"}, {LookCatalog.Option(LookGene.Build, look.Build)?.Name ?? "unknown"} build. {StandingText(standing)}."
                    : StandingText(standing) + ".";
            }
            int health = (int)System.MathF.Round(agent.Health);
            if (Mood != _moodShown || health != _healthShown || MoodLine.Length == 0)
            {
                _moodShown = Mood;
                _healthShown = health;
                MoodLine = MoodText(Mood, agent.Health, agent.IsDowned);
            }
        }

        /// <summary>
        /// Equipment in words, by its catalog name: what is left of its life (its definition's durability), rounded up so
        /// equipment that still works never reads 0%.
        /// </summary>
        public static string GearText(in ItemInstance gear) => gear.IsEmpty ? NoGear : Invariant($"{PieceName(gear)}, {LifeLeft(gear)}% left");

        /// <summary>What is left of a piece's life, in percent, rounded up so equipment that still works never reads 0 (a grade's life is its whole, Epic 10 Part 2, D56).</summary>
        public static int LifeLeft(in ItemInstance gear)
        {
            var definition = ItemCatalog.Get(gear.Type);
            ushort life = definition?.Equipment != null ? definition.Equipment.DurabilityAt(gear.Quality) : (ushort)0;
            return life > 0 ? (gear.Durability * 100 + life - 1) / life : 100;
        }

        /// <summary>A piece's name, by its catalog name, its grade before it when graded (Epic 10 Part 2, W4): "Fine Hard Hat".</summary>
        public static string PieceName(in ItemInstance gear)
        {
            var definition = ItemCatalog.Get(gear.Type);
            string name = definition?.DisplayName ?? ItemCatalog.NameOf(gear.Type);
            return gear.Quality != Grades.Standard ? Grades.NameOf(gear.Quality) + " " + name : name;
        }

        /// <summary>A drifter's stay in words, in game time (Epic 10 Part 2, T6: "drifter, leaves in 3 h 20 min"); empty for a resident.</summary>
        public static string Stay(uint departsAtTick, long now)
        {
            if (departsAtTick == 0) return string.Empty;
            long left = departsAtTick - now;
            if (left <= 0) return "drifter, leaving";
            return "drifter, leaves in " + OutpostClock.Span(left);
        }

        /// <summary>One affinity: the peer (the player, a crawler, or one no longer in the world), its face if it has one, and the line.</summary>
        public readonly struct AffinityRow
        {
            public AffinityRow(EntityId peerId, float warmth, string name, string line, bool hasPortrait = false, PortraitKey portrait = default)
            {
                PeerId = peerId;
                Warmth = warmth;
                Name = name;
                Line = line;
                HasPortrait = hasPortrait;
                Portrait = portrait;
            }

            public EntityId PeerId { get; }
            public float Warmth { get; }
            public string Name { get; }
            public string Line { get; }
            public bool HasPortrait { get; }
            public PortraitKey Portrait { get; }
        }
    }
}
