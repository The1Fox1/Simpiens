using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>How a technique stands in a crawler's tree (Epic 11, S4.11, D25).</summary>
    public enum TechniqueNodeState : byte
    {
        /// <summary>Its band isn't reached yet.</summary>
        Locked = 0,
        /// <summary>The crawler holds it.</summary>
        Held = 1,
        /// <summary>Its band reached and not held: the patron may sponsor it now.</summary>
        Open = 2,
        /// <summary>Its band reached and not held, but sponsorship's rule refuses it now: trust, nature, the treasury, or not one of the patron's people.</summary>
        Barred = 3
    }

    /// <summary>One of a technique's effects in words, and whether it is an upside or its cost.</summary>
    public readonly struct EffectLine
    {
        public EffectLine(string text, bool isUpside)
        {
            Text = text;
            IsUpside = isUpside;
        }

        public string Text { get; }
        public bool IsUpside { get; }
    }

    /// <summary>A technique in the tree: its name, its effects, how it stands for the crawler and the word on its corner.</summary>
    public sealed class TechniqueNode
    {
        internal TechniqueNode() { }

        public Technique Technique { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public IReadOnlyList<EffectLine> Effects { get; internal set; } = System.Array.Empty<EffectLine>();
        public TechniqueNodeState State { get; internal set; }

        /// <summary>"HELD", the price ("20g") when its band is reached, or the level that offers it ("level 6").</summary>
        public string Tag { get; internal set; } = string.Empty;

        /// <summary>The player has picked it: the detail shows it.</summary>
        public bool Selected { get; internal set; }

        /// <summary>The patron's price for it, by its band.</summary>
        public ushort Price { get; internal set; }
    }

    /// <summary>A band of the tree: its name, the level that reaches it, whether the crawler has, and its two techniques.</summary>
    public sealed class TechniqueTier
    {
        private readonly TechniqueNode[] _nodes;

        internal TechniqueTier(SkillBand band)
        {
            Band = band;
            Label = SkillRules.NameOf(band).ToUpperInvariant();
            GateLevel = TechniqueTreeModel.FirstLevelOf(band);
            Gate = Invariant($"level {GateLevel}");
            _nodes = new TechniqueNode[TechniqueTreeModel.NodesPerTier];
            for (int i = 0; i < _nodes.Length; i++) _nodes[i] = new TechniqueNode();
        }

        public SkillBand Band { get; }

        /// <summary>"APPRENTICE".</summary>
        public string Label { get; }

        /// <summary>The first level of the band, and in words: "level 3".</summary>
        public int GateLevel { get; }
        public string Gate { get; }

        public bool Reached { get; internal set; }

        /// <summary>The band's techniques, in the table's order: always <see cref="TechniqueTreeModel.NodesPerTier"/> (a band offers two).</summary>
        public IReadOnlyList<TechniqueNode> Nodes => _nodes;

        internal TechniqueNode Node(int index) => _nodes[index];
    }

    /// <summary>A skill in the tree's list: its words, its band, and whether it is the one shown.</summary>
    public readonly struct TreeSkillRow
    {
        public TreeSkillRow(Skill skill, string text, string band, bool selected)
        {
            Skill = skill;
            Text = text;
            Band = band;
            Selected = selected;
        }

        public Skill Skill { get; }

        /// <summary>"Melee 4".</summary>
        public string Text { get; }

        /// <summary>"Apprentice".</summary>
        public string Band { get; }
        public bool Selected { get; }
    }

    /// <summary>
    /// A crawler's technique tree (Epic 11, S4.11, D25; the producer's look at S4: "a proper skill tree diagram"): a window over
    /// the map with each skill that offers techniques, and for the one picked, its bands from Apprentice to Master, two
    /// techniques at each, the crawler's level climbing the trunk between them. A technique is picked to read it; one of the
    /// patron's people may be offered gold to take up one at a band it has reached (<see cref="SponsorRules"/>, D16), and the
    /// offer leaves as a command. Anyone's tree can be read; only the patron's people take its gold.
    /// </summary>
    public sealed class TechniqueTreeModel : IDashboardWindow
    {
        /// <summary>The bands above Novice, each a tier, and the techniques each offers.</summary>
        public const int Tiers = 3, NodesPerTier = 2;

        private static readonly SkillBand[] Bands = { SkillBand.Apprentice, SkillBand.Journeyman, SkillBand.Master };
        private static readonly EffectLine[][] LinesById = new EffectLine[256][];

        /// <summary>The skills that offer techniques, in the table's display order: the tree's list. A skill added with techniques joins it.</summary>
        public static IReadOnlyList<Skill> TreeSkills { get; } = BuildTreeSkills();

        private readonly DashboardContext _context;
        private readonly MoodTracker _moods;
        private readonly TechniqueTier[] _tiers = new TechniqueTier[Tiers];
        private readonly List<TreeSkillRow> _skills = new List<TreeSkillRow>(SkillTable.Capacity);
        private Technique _pending;
        private long _pendingAt;
        private ushort _pendingPrice;

        internal TechniqueTreeModel(DashboardContext context, MoodTracker moods = null)
        {
            _context = context;
            _moods = moods ?? new MoodTracker();
            for (int i = 0; i < Tiers; i++) _tiers[i] = new TechniqueTier(Bands[i]);
        }

        public bool IsOpen { get; private set; }

        /// <summary>Whose tree is shown.</summary>
        public EntityId CrawlerId { get; private set; }

        /// <summary>One of the patron's people (a guild member or an artisan): it may be sponsored.</summary>
        public bool IsOurs { get; private set; }

        /// <summary>"Bram the Warden".</summary>
        public string Name { get; private set; } = string.Empty;

        /// <summary>"Guild member · trust 80 (a sponsor needs 50)", with its warmth where a sponsor needs any; or why its tree takes no gold.</summary>
        public string Standing { get; private set; } = string.Empty;

        /// <summary>"Treasury 1000g": what sponsorship is paid from.</summary>
        public string Treasury { get; private set; } = string.Empty;

        public bool HasPortrait { get; private set; }
        public PortraitKey Portrait { get; private set; }

        public IReadOnlyList<TreeSkillRow> Skills => _skills;

        /// <summary>The skill whose tree is shown.</summary>
        public Skill SelectedSkill { get; private set; }

        /// <summary>The trunk's root: "Melee 4".</summary>
        public string RootText { get; private set; } = string.Empty;

        /// <summary>
        /// How far up the trunk the crawler's level has climbed, in tiers: 0 at the root, 1 at Apprentice, 2 at Journeyman, 3 at
        /// Master, and between them by the levels between their gates.
        /// </summary>
        public float Progress { get; private set; }

        public IReadOnlyList<TechniqueTier> TierRows => _tiers;

        /// <summary>The technique picked: the detail shows it.</summary>
        public Technique Selected { get; private set; }

        /// <summary>"Reckless Fury".</summary>
        public string DetailTitle { get; private set; } = string.Empty;

        /// <summary>"Melee · Apprentice, from level 3".</summary>
        public string DetailSubtitle { get; private set; } = string.Empty;

        /// <summary>What the crawler would say taking it up: "Hit first, and hit hardest."</summary>
        public string Vow { get; private set; } = string.Empty;

        public IReadOnlyList<EffectLine> DetailEffects { get; private set; } = System.Array.Empty<EffectLine>();

        /// <summary>What sponsoring it would do, or why it can't: "In place of Shield Wall.", "Bram holds it.", "Bram isn't Journeyman yet: Melee 4 of 6."</summary>
        public string SponsorLine { get; private set; } = string.Empty;

        /// <summary>The offer's button: "Offer 20g".</summary>
        public string OfferText { get; private set; } = string.Empty;

        /// <summary>The offer would be taken now, and none is waiting on a tick.</summary>
        public bool CanOffer { get; private set; }

        /// <summary>The last offer: sent, taken up, or refused when the tick came.</summary>
        public Feedback Feedback { get; private set; } = Feedback.None;

        /// <summary>
        /// Opens <paramref name="crawler"/>'s tree on what the dossier would offer (D18: the other technique at the top band of its
        /// best skill), or its best skill's first technique when it holds none.
        /// </summary>
        public void Open(EntityId crawler)
        {
            var agent = AgentOf(crawler);
            if (agent == null) return;
            CrawlerId = crawler;
            Feedback = Feedback.None;
            _pending = Technique.None;
            var offer = SponsorRules.Offer(agent.Skills, agent.Traits, out _);
            if (offer != Technique.None)
            {
                SelectedSkill = TechniqueTable.Get(offer).Skill;
                Selected = offer;
            }
            else
            {
                SelectedSkill = BestSkill(agent.Skills);
                Selected = DefaultFor(SelectedSkill, agent);
            }
            IsOpen = true;
            Refresh();
        }

        public void Close()
        {
            IsOpen = false;
            _pending = Technique.None;
        }

        /// <summary>Shows <paramref name="skill"/>'s tree, picking the technique its top band reached would be sponsored for.</summary>
        public void SelectSkill(Skill skill)
        {
            var agent = AgentOf(CrawlerId);
            if (!IsOpen || agent == null || !IsTreeSkill(skill)) return;
            SelectedSkill = skill;
            Selected = DefaultFor(skill, agent);
            Refresh();
        }

        /// <summary>Picks <paramref name="technique"/> of the skill shown, to read it and offer for it.</summary>
        public void SelectTechnique(Technique technique)
        {
            var row = TechniqueTable.Get(technique);
            if (!IsOpen || row == null || row.Skill != SelectedSkill) return;
            Selected = technique;
            Refresh();
        }

        /// <summary>Offers the patron's gold for the technique picked: a sponsorship command, taken up on the next tick as the rule allows then.</summary>
        public void Offer()
        {
            if (!CanOffer || _context.Intervention == null) return;
            var agent = AgentOf(CrawlerId);
            var row = TechniqueTable.Get(Selected);
            if (agent == null || row == null) return;
            ushort price = PriceOf(row);
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateSponsorTechnique("Player", CrawlerId, Selected));
            _pending = Selected;
            _pendingAt = _context.Clock?.CurrentTick ?? 0;
            _pendingPrice = price;
            Feedback = new Feedback(Invariant($"Offered {NameOf(agent)} {price}g to take up {row.DisplayName}."), FeedbackTone.Notice);
            Refresh();
        }

        /// <summary>Re-reads the crawler: its skills, what it holds, how each technique stands, the offer's fate. Closes when it has left the world.</summary>
        public void Refresh()
        {
            if (!IsOpen) return;
            var agent = AgentOf(CrawlerId);
            if (agent == null)
            {
                Close();
                return;
            }
            var sim = _context.Simulation;
            var host = sim as IGuildHost;
            uint tick = (uint)(_context.Clock?.CurrentTick ?? 0);
            string name = NameOf(agent);
            IsOurs = sim != null && (sim.Guild.TryGetContract(CrawlerId, out _) || (sim.Workshops != null && sim.Workshops.IsArtisan(CrawlerId)));
            ResolvePending(agent, sim, host, tick, name);

            string title = CallingTexts.Title(agent.Skills, agent.Traits);
            string friendly = _context.FriendlyName(CrawlerId, agent.GivenName ?? DossierModel.UnknownName);
            Name = Simpiens.Simulation.Skills.EpithetRules.NameWith(friendly, _context.EpithetOf(agent.Traits), title); // with its epithet (Epic 11, S6.6)
            Standing = StandingOf(agent, sim, tick);
            Treasury = sim != null && sim.ActiveStalls != null && sim.ActiveStalls.Count > 0 ? Invariant($"Treasury {sim.ActiveStalls[0].TreasuryGold}g") : string.Empty;
            HasPortrait = agent.Look.IsComplete;
            Portrait = PortraitKey.Of(CrawlerId, agent, _moods, tick);

            _skills.Clear();
            for (int i = 0; i < TreeSkills.Count; i++)
            {
                var skill = TreeSkills[i];
                int level = agent.Skills.Level(skill);
                _skills.Add(new TreeSkillRow(skill, Invariant($"{SkillTable.Get(skill).DisplayName} {level}"), SkillRules.NameOf(SkillRules.BandOf(level)), skill == SelectedSkill));
            }

            int shownLevel = agent.Skills.Level(SelectedSkill);
            RootText = Invariant($"{SkillTable.Get(SelectedSkill)?.DisplayName} {shownLevel}");
            Progress = ProgressOf(shownLevel);
            var band = agent.Skills.Band(SelectedSkill);
            for (int t = 0; t < Tiers; t++)
            {
                var tier = _tiers[t];
                tier.Reached = band >= tier.Band;
                var options = TechniqueTable.Options(SelectedSkill, tier.Band);
                var held = agent.Traits.TechniqueIn(SelectedSkill, tier.Band);
                for (int n = 0; n < NodesPerTier; n++)
                {
                    var node = tier.Node(n);
                    var row = n < options.Count ? options[n] : null;
                    node.Technique = row?.Id ?? Technique.None;
                    node.Name = row?.DisplayName ?? string.Empty;
                    node.Effects = row != null ? LinesOf(row) : System.Array.Empty<EffectLine>();
                    node.Price = row != null ? PriceOf(row) : (ushort)0;
                    node.Selected = row != null && row.Id == Selected;
                    if (row == null) node.State = TechniqueNodeState.Locked;
                    else if (!tier.Reached) node.State = TechniqueNodeState.Locked;
                    else if (held == row.Id) node.State = TechniqueNodeState.Held;
                    else node.State = Check(agent, row.Id, sim, host, tick) == SponsorRules.Check.Ok ? TechniqueNodeState.Open : TechniqueNodeState.Barred;
                    node.Tag = row == null ? string.Empty
                        : node.State == TechniqueNodeState.Held ? HeldTag
                        : node.State == TechniqueNodeState.Locked ? tier.Gate
                        : Invariant($"{node.Price}g");
                }
            }
            Describe(agent, sim, host, tick, name, shownLevel);
        }

        public const string HeldTag = "HELD";

        // The detail of the technique picked, and whether the offer can go
        private void Describe(Simpiens.Cognition.AgentState agent, ISimulationManager sim, IGuildHost host, uint tick, string name, int level)
        {
            var row = TechniqueTable.Get(Selected);
            CanOffer = false;
            if (row == null)
            {
                DetailTitle = DetailSubtitle = Vow = SponsorLine = OfferText = string.Empty;
                DetailEffects = System.Array.Empty<EffectLine>();
                return;
            }
            DetailTitle = row.DisplayName;
            DetailSubtitle = Invariant($"{SkillTable.Get(row.Skill)?.DisplayName} · {SkillRules.NameOf(row.Band)}, from level {FirstLevelOf(row.Band)}");
            Vow = "\"" + row.Vow + "\"";
            DetailEffects = LinesOf(row);
            ushort price = PriceOf(row);
            OfferText = Invariant($"Offer {price}g");
            var state = StateOf(row);
            if (state == TechniqueNodeState.Held)
            {
                SponsorLine = Invariant($"{name} holds it.");
                return;
            }
            if (state == TechniqueNodeState.Locked)
            {
                SponsorLine = Invariant($"{name} isn't {SkillRules.NameOf(row.Band)} yet: {SkillTable.Get(row.Skill)?.DisplayName} {level} of {FirstLevelOf(row.Band)}.");
                return;
            }
            var check = Check(agent, row.Id, sim, host, tick);
            if (check != SponsorRules.Check.Ok)
            {
                SponsorLine = SponsorRules.Describe(check, agent, row.Id, price, sim?.Economy?.Guild ?? Simpiens.Simulation.Economy.GuildTuning.Default, tick);
                return;
            }
            var replacing = TechniqueTable.Get(agent.Traits.TechniqueIn(row.Skill, row.Band));
            SponsorLine = replacing != null ? Invariant($"In place of {replacing.DisplayName}.") : Invariant($"{name} takes it up at {SkillRules.NameOf(row.Band)}.");
            CanOffer = _pending == Technique.None && _context.Intervention != null;
        }

        // The offer sent: taken up (it holds it now), or refused when its tick came (the rule's reason then)
        private void ResolvePending(Simpiens.Cognition.AgentState agent, ISimulationManager sim, IGuildHost host, uint tick, string name)
        {
            var row = TechniqueTable.Get(_pending);
            if (row == null) return;
            if (agent.Traits.TechniqueIn(row.Skill, row.Band) == _pending)
            {
                Feedback = new Feedback(Invariant($"{name} took up {row.DisplayName} for {_pendingPrice}g."), FeedbackTone.Ok);
                _pending = Technique.None;
                return;
            }
            if (tick <= _pendingAt) return; // its tick hasn't come
            var check = Check(agent, row.Id, sim, host, tick);
            if (check == SponsorRules.Check.Ok) return;
            Feedback = new Feedback("Refused: " + SponsorRules.Describe(check, agent, row.Id, PriceOf(row), sim?.Economy?.Guild ?? Simpiens.Simulation.Economy.GuildTuning.Default, tick), FeedbackTone.Error);
            _pending = Technique.None;
        }

        private TechniqueNodeState StateOf(TechniqueDefinition row)
        {
            for (int t = 0; t < Tiers; t++)
                for (int n = 0; n < NodesPerTier; n++)
                    if (_tiers[t].Nodes[n].Technique == row.Id) return _tiers[t].Nodes[n].State;
            return TechniqueNodeState.Locked;
        }

        private SponsorRules.Check Check(Simpiens.Cognition.AgentState agent, Technique technique, ISimulationManager sim, IGuildHost host, uint tick)
            => sim == null || host == null ? SponsorRules.Check.NoSuchCrawler : SponsorRules.CheckSponsor(sim.Guild, sim.Workshops, host, agent.AgentId, technique, tick, out _);

        private ushort PriceOf(TechniqueDefinition row) => (_context.Simulation?.Economy?.Guild ?? Simpiens.Simulation.Economy.GuildTuning.Default).SponsorPrice(row.Band);

        private string StandingOf(Simpiens.Cognition.AgentState agent, ISimulationManager sim, uint tick)
        {
            string who = sim != null && sim.Guild.TryGetContract(CrawlerId, out _) ? "Guild member"
                : sim?.Workshops != null && sim.Workshops.IsArtisan(CrawlerId) ? "Artisan"
                : DossierModel.StandingText(WorldViewFrame.StandingOf(agent));
            if (!IsOurs) return who + " · only your guild members and artisans take a patron's gold";
            var tuning = sim?.Economy?.Guild ?? Simpiens.Simulation.Economy.GuildTuning.Default;
            var (trust, warmth) = GuildRules.Standing(agent, tick);
            // Warmth only where a sponsor needs any
            return tuning.SponsorWarmth > 0
                ? Invariant($"{who} · trust {trust}, warmth {warmth} (a sponsor needs {tuning.SponsorTrust} and {tuning.SponsorWarmth})")
                : Invariant($"{who} · trust {trust} (a sponsor needs {tuning.SponsorTrust})");
        }

        private Simpiens.Cognition.AgentState AgentOf(EntityId id) => id != default ? _context.Registry?.GetEntity(id)?.Agent : null;

        private string NameOf(Simpiens.Cognition.AgentState agent) => agent.GivenName ?? _context.FriendlyName(agent.AgentId, DossierModel.UnknownName);

        /// <summary>
        /// How many techniques <paramref name="agent"/> could be sponsored for now (its bands reached, not held, the rule
        /// allowing), and the cheapest; the dossier's sway line (D25).
        /// </summary>
        public static int OpenCount(ISimulationManager sim, Simpiens.Cognition.AgentState agent, uint tick, out ushort cheapest)
        {
            cheapest = 0;
            var host = sim as IGuildHost;
            if (sim == null || host == null || agent == null) return 0;
            int count = 0;
            var all = TechniqueTable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var row = all[i];
                if (agent.Skills.Band(row.Skill) < row.Band || agent.Traits.TechniqueIn(row.Skill, row.Band) == row.Id) continue;
                if (SponsorRules.CheckSponsor(sim.Guild, sim.Workshops, host, agent.AgentId, row.Id, tick, out ushort price) != SponsorRules.Check.Ok) continue;
                if (count == 0 || price < cheapest) cheapest = price;
                count++;
            }
            return count;
        }

        /// <summary>The first level of <paramref name="band"/>: where its tier's techniques are offered.</summary>
        public static int FirstLevelOf(SkillBand band)
        {
            for (int level = 0; level <= SkillRules.MaxLevel; level++) if (SkillRules.BandOf(level) == band) return level;
            return SkillRules.MaxLevel;
        }

        /// <summary>The trunk's climb for <paramref name="level"/>, in tiers (see <see cref="Progress"/>).</summary>
        public static float ProgressOf(int level)
        {
            int below = 0;
            for (int t = 0; t < Tiers; t++)
            {
                int gate = FirstLevelOf(Bands[t]);
                if (level < gate) return t + (float)(level - below) / (gate - below);
                below = gate;
            }
            return Tiers;
        }

        private static bool IsTreeSkill(Skill skill)
        {
            for (int i = 0; i < TreeSkills.Count; i++) if (TreeSkills[i] == skill) return true;
            return false;
        }

        // The skill to open on when it holds no technique: the best of the tree's, ties in display order
        private static Skill BestSkill(in SkillSet skills)
        {
            var best = TreeSkills.Count > 0 ? TreeSkills[0] : Skill.None;
            for (int i = 1; i < TreeSkills.Count; i++) if (skills.Level(TreeSkills[i]) > skills.Level(best)) best = TreeSkills[i];
            return best;
        }

        // The technique to pick on a skill shown: the other at its top band reached (what a sponsor offers), else the first of Apprentice
        private static Technique DefaultFor(Skill skill, Simpiens.Cognition.AgentState agent)
        {
            var band = agent.Skills.Band(skill);
            for (int t = Tiers - 1; t >= 0; t--)
            {
                if (band < Bands[t]) continue;
                var held = agent.Traits.TechniqueIn(skill, Bands[t]);
                var other = TechniqueTable.OtherThan(held);
                if (other != Technique.None) return other;
                var options = TechniqueTable.Options(skill, Bands[t]);
                if (options.Count > 0) return options[0].Id;
            }
            var first = TechniqueTable.Options(skill, SkillBand.Apprentice);
            return first.Count > 0 ? first[0].Id : Technique.None;
        }

        private static EffectLine[] LinesOf(TechniqueDefinition row)
        {
            var lines = LinesById[(byte)row.Id];
            if (lines != null) return lines;
            lines = new EffectLine[row.Effects.Count];
            for (int i = 0; i < lines.Length; i++) lines[i] = new EffectLine(TechniqueRules.Describe(row.Effects[i]), row.Effects[i].IsUpside);
            LinesById[(byte)row.Id] = lines;
            return lines;
        }

        private static Skill[] BuildTreeSkills()
        {
            var skills = new List<Skill>(SkillTable.Capacity);
            var shown = SkillTable.InDisplayOrder;
            for (int i = 0; i < shown.Count; i++) if (TechniqueTable.Options(shown[i].Id, SkillBand.Apprentice).Count > 0) skills.Add(shown[i].Id);
            return skills.ToArray();
        }
    }
}
