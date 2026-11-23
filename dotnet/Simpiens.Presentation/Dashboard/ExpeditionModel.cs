using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>One member of the party out, in the tracker.</summary>
    public sealed class ExpeditionMemberRow
    {
        public string Name { get; internal set; } = string.Empty;
        public float Health { get; internal set; }
        public bool Downed { get; internal set; }
        public bool WithParty { get; internal set; }

        /// <summary>"With the party", "Downed, limping home", "Stayed behind", "Deserted", "Left the guild".</summary>
        public string Status { get; internal set; } = string.Empty;

        public string Gear { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// One party's tracker (Epic 10 G5; one per party since Epic 10 Part 2, W5.3b): where it went and under which rule, its
    /// stage and time out, the haul so far, and each member's health, gear and part. Out only while the party is.
    /// </summary>
    public sealed class PartyTrackerModel
    {
        internal readonly List<ExpeditionMemberRow> Rows = new List<ExpeditionMemberRow>(DispatchExpeditionCommand.MaxMembers);

        internal PartyTrackerModel(byte slot)
        {
            Slot = slot;
        }

        public byte Slot { get; }
        public bool IsOut { get; internal set; }

        /// <summary>"The Drowned Gallery · Steady".</summary>
        public string Title { get; internal set; } = string.Empty;

        /// <summary>"Mustering at the stall", "Marching to The Drowned Gallery", "Working The Drowned Gallery", "Coming home", "Turning back".</summary>
        public string StageText { get; internal set; } = string.Empty;

        /// <summary>"1:45 out".</summary>
        public string TimeOut { get; internal set; } = string.Empty;

        /// <summary>"Haul 2 of 4"; a clear's "Picked up 2 hide" (Epic 10 Part 3, K7.3).</summary>
        public string HaulText { get; internal set; } = string.Empty;

        /// <summary>A clear's fight (Epic 10 Part 3, K7.3): "Burrow rats: 2 of 4 left · party health 68"; empty for a gathering.</summary>
        public string FightText { get; internal set; } = string.Empty;

        /// <summary>True for a clear: the tracker shows its fight.</summary>
        public bool IsClear { get; internal set; }

        public IReadOnlyList<ExpeditionMemberRow> Members => Rows;
    }

    /// <summary>
    /// Expeditions on screen (Epic 10 G5, D7): the party builder in the roster window (which of the guild's parties, the
    /// target among the charted hazard sectors, the goal, the retreat rule, outfitting from the stall's stock; Dispatch
    /// and Recall) and a tracker for each party out (Epic 10 Part 2, W5.3b). A party's members are chosen on the roster's
    /// cards (W5, D65). The rules are the guild's
    /// (<see cref="GuildRules"/>); the player's actions leave as typed commands, applied on the next tick. Views decide
    /// nothing.
    /// </summary>
    public sealed class ExpeditionModel
    {
        private readonly DashboardContext _context;
        private readonly GuildModel _guild;
        private readonly List<(byte Id, string Name, bool Lair)> _targets = new List<(byte, string, bool)>(6);
        private readonly PartyTrackerModel[] _trackers = { new PartyTrackerModel(1), new PartyTrackerModel(2) };
        private readonly List<EntityId> _party = new List<EntityId>(DispatchExpeditionCommand.MaxMembers);
        private int _target;

        internal ExpeditionModel(DashboardContext context, GuildModel guild)
        {
            _context = context;
            _guild = guild;
        }

        // --- The builder ---

        /// <summary>The party it sends (Epic 10 Part 2, W5): 1 or 2.</summary>
        public byte PartySlot { get; private set; } = 1;

        /// <summary>"Party 1 · 4": the party and how many it holds.</summary>
        public string PartyText { get; private set; } = "Party 1";

        /// <summary>The next party, and round again.</summary>
        public void NextParty()
        {
            PartySlot = (byte)(PartySlot % GuildService.PartySlots + 1);
            Refresh();
        }

        /// <summary>
        /// Where a party can go: the charted hazard sectors, in map order, to gather; then the charted lairs with a creature in
        /// them, to clear (Epic 10 Part 3, K7.3: <c>Lair</c>, its id the lair's index). The builder starts on the most dangerous
        /// sector, where nobody goes alone (D11).
        /// </summary>
        public IReadOnlyList<(byte Id, string Name, bool Lair)> Targets => _targets;
        public string TargetName => _targets.Count > 0 ? _targets[_target].Name : "No charted danger";

        /// <summary>True when the target is a lair: the order is a clear, with no goal (Epic 10 Part 3, K7.3).</summary>
        public bool IsClear => _targets.Count > 0 && _targets[_target].Lair;
        public byte Goal { get; private set; } = 4;
        public RetreatRule Rule { get; private set; } = RetreatRule.Steady;

        /// <summary>Outfit from stock: members without protection, or with it worn to half its life, are given some from the stall as the party is sent (D12).</summary>
        public bool Outfit { get; private set; } = true;

        /// <summary>A clear's arming gives the back half of the party bows (Epic 10 Part 3, K8.2c, D47).</summary>
        public bool Bows { get; private set; }

        /// <summary>How many at the back of the party a clear arms to shoot: half of it, with bows chosen; else none.</summary>
        public int Shooters => IsClear && Outfit && Bows ? _party.Count / 2 : 0;

        /// <summary>
        /// The outfit button's words: "No outfitting", or the gear it gives for the target's hazard (Epic 10 Part 2, W3, D50),
        /// a piece for each slot that has one in stock (Epic 10 Part 3, K1.4), in the slots' display order: "Outfit:
        /// respirator, hazard gear", "Outfit: hazard gear"; "Outfit from stock" while there is none.
        /// </summary>
        public string OutfitText
        {
            get
            {
                bool clear = IsClear;
                if (!Outfit) return clear ? "No arming" : "No outfitting";
                var host = Host;
                if (host == null || _targets.Count == 0 || host.ActiveStalls.Count == 0) return clear ? "Arm from stock" : "Outfit from stock";
                var hazard = clear ? Simpiens.Simulation.World.HazardKind.None : GuildRules.HazardOf(host, _targets[_target].Id);
                var stall = host.ActiveStalls[0];
                var text = new System.Text.StringBuilder();
                var slots = Simpiens.Simulation.Items.EquipmentSlots.InDisplayOrder;
                for (int i = 0; i < slots.Count; i++)
                {
                    // A clear arms by what a piece adds for a fight (Epic 10 Part 3, K7.3)
                    var gear = clear ? GuildRules.BestForFight(stall, slots[i].Id) : GuildRules.BestForBare(stall, slots[i].Id, hazard);
                    if (gear == ItemType.None) continue;
                    text.Append(text.Length == 0 ? (clear ? "Arm: " : "Outfit: ") : ", ").Append(ItemTexts.Of(gear).MerchantName.ToLowerInvariant());
                }
                if (clear && Bows)
                {
                    // The back half's bow (Epic 10 Part 3, K8.2c): "Arm: iron blade · 2 with crossbow"
                    var bow = GuildRules.BestBow(stall);
                    string back = bow != ItemType.None ? Invariant($"{SafeHalf()} with {ItemTexts.Of(bow).MerchantName.ToLowerInvariant()}") : "no bows in stock";
                    return (text.Length > 0 ? text.ToString() : "Arm") + " · " + back;
                }
                return text.Length > 0 ? text.ToString() : clear ? "Arm from stock" : "Outfit from stock";
            }
        }

        /// <summary>Draughts each member is given at the muster, as stock allows (Epic 10 Part 2, W2, D44): 0 to <see cref="DispatchExpeditionCommand.MaxDraughts"/>.</summary>
        public byte Draughts { get; private set; }

        /// <summary>"No draughts", "1 draught each", "2 draughts each".</summary>
        public string DraughtsText => Draughts == 0 ? "No draughts" : Draughts == 1 ? "1 draught each" : Invariant($"{Draughts} draughts each");

        /// <summary>"Send 4 to The Drowned Gallery for 4 glowstone", or why not.</summary>
        public string DispatchText { get; private set; } = string.Empty;
        public bool CanDispatch { get; private set; }
        public bool CanRecall => Guild != null && Guild.Parties[PartySlot - 1].IsOut && _context.Intervention != null;
        public Feedback Feedback { get; private set; } = Feedback.None;

        public void NextTarget()
        {
            if (_targets.Count > 0) _target = (_target + 1) % _targets.Count;
            Refresh();
        }

        public void MoreGoal()
        {
            if (Goal < MaxGoal) Goal++;
            Refresh();
        }

        public void LessGoal()
        {
            if (Goal > 1) Goal--;
            Refresh();
        }

        public void NextRule()
        {
            Rule = Rule == RetreatRule.Bold ? RetreatRule.Cautious : (RetreatRule)(Rule + 1);
            Refresh();
        }

        /// <summary>Outfitting on and off; for a clear, round three: arm, arm with bows at the back, no arming (Epic 10 Part 3, K8.2c).</summary>
        public void ToggleOutfit()
        {
            if (IsClear && Outfit && !Bows) Bows = true;
            else if (IsClear && Outfit) { Outfit = false; Bows = false; }
            else { Outfit = !Outfit; Bows = false; }
            Refresh();
        }

        private int SafeHalf() => System.Math.Max(1, _party.Count / 2);

        /// <summary>None, one or two draughts each, and round again.</summary>
        public void NextDraughts()
        {
            Draughts = (byte)((Draughts + 1) % (DispatchExpeditionCommand.MaxDraughts + 1));
            Refresh();
        }

        /// <summary>"Steady: turn back below 55 health".</summary>
        public string RuleText => Invariant($"{Rule}: turn back below {Tuning.RetreatHealth(Rule):F0} health");

        /// <summary>
        /// Dispatch: gear from stock for each member in the party who needs it (if <see cref="Outfit"/>, as stock allows),
        /// then the <see cref="DispatchExpeditionCommand"/>; they apply in that order on the next tick.
        /// </summary>
        public void Dispatch()
        {
            if (!CanDispatch) return;
            var host = Host;
            if (Outfit && host != null && host.ActiveStalls.Count > 0 && IsClear)
            {
                // A clear: the best piece in stock for a fight, slot by slot (Epic 10 Part 3, K7.3), as the scripted player arms
                var stock = host.ActiveStalls[0];
                var gifts = new GearGift[_party.Count * Simpiens.Simulation.Items.EquipmentSlots.Capacity];
                int count = 0;
                GuildRules.PlanArms(Guild, host, _party, ref stock, gifts, ref count, Shooters);
                for (int i = 0; i < count; i++) _context.Intervention.QueueInfluence(InfluenceEvent.CreateGiveGear("Player", gifts[i].Crawler, gifts[i].Item, Simpiens.Simulation.World.HazardKind.None, forFighting: true));
            }
            else if (Outfit && host != null && host.ActiveStalls.Count > 0)
            {
                // The best piece in stock for each slot against the target's hazard (Epic 10 Part 2, D50; Epic 10 Part 3, K1.4),
                // members with no sound protection first, as the scripted player outfits
                var hazard = GuildRules.HazardOf(host, _targets[_target].Id);
                var stock = host.ActiveStalls[0];
                var gifts = new GearGift[_party.Count * Simpiens.Simulation.Items.EquipmentSlots.Capacity];
                int count = 0;
                GuildRules.PlanOutfit(Guild, host, _party, hazard, OutfitRound.Both, ref stock, gifts, ref count);
                for (int i = 0; i < count; i++) _context.Intervention.QueueInfluence(InfluenceEvent.CreateGiveGear("Player", gifts[i].Crawler, gifts[i].Item, hazard));
            }
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateDispatchExpedition("Player", Order()));
            Feedback = new Feedback(IsClear
                ? Invariant($"Party {PartySlot} sets out to clear {LairName(_targets[_target].Id)}, {_party.Count} strong.")
                : Invariant($"Party {PartySlot} sets out for {TargetName}, {_party.Count} strong."), FeedbackTone.Ok);
        }

        public void Recall()
        {
            if (!CanRecall) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateRecallExpedition("Player", PartySlot));
            Feedback = new Feedback(Invariant($"Party {PartySlot} is called home."), FeedbackTone.Ok);
        }

        // --- The trackers ---

        /// <summary>A tracker for each of the guild's parties, in slot order.</summary>
        public IReadOnlyList<PartyTrackerModel> Trackers => _trackers;

        /// <summary>True while a party is out.</summary>
        public bool IsOut => _trackers[0].IsOut || _trackers[1].IsOut;

        public void Refresh()
        {
            RefreshTargets();
            RefreshTracker();
            RefreshBuilder();
        }

        private GuildService Guild => _context.Simulation?.Guild;
        private IGuildHost Host => _context.Simulation as IGuildHost;
        private ExpeditionTuning Tuning => (_context.Simulation?.Economy ?? EconomyTuning.Default).Guild.Expedition;
        private byte MaxGoal => Tuning.MaxGoal;
        private uint Tick => (uint)(_context.Clock?.CurrentTick ?? 0);

        private void RefreshTargets()
        {
            var sectors = _context.Sectors;
            bool had = _targets.Count > 0;
            var chosen = had ? _targets[_target] : default;
            _targets.Clear();
            _target = 0;
            if (sectors == null) return;
            float worst = 0f;
            bool found = false;
            for (int i = 0; i < sectors.All.Count; i++)
            {
                var sector = sectors.All[i];
                if (!sector.Hazard.IsHazardous || !sectors.IsExplored(sector.Id)) continue;
                if (had ? !chosen.Lair && sector.Id == chosen.Id : sector.Hazard.DamagePerSecond > worst)
                {
                    _target = _targets.Count;
                    worst = sector.Hazard.DamagePerSecond;
                    found = true;
                }
                _targets.Add((sector.Id, sector.Name, false));
            }
            // The charted lairs with a creature in them, to clear (Epic 10 Part 3, K7.3)
            var creatures = _context.Simulation?.Creatures;
            for (int i = 0; creatures != null && i < creatures.Lairs.Count && i < 64; i++)
            {
                if (!creatures.IsCharted(i) || creatures.Lairs[i].Alive == 0) continue;
                if (had && chosen.Lair && chosen.Id == i) { _target = _targets.Count; found = true; }
                _targets.Add(((byte)i, "Clear " + LairWords.Pack(creatures.Lairs, i), true));
            }
            if (had && !found) _target = 0;
        }

        /// <summary>"the burrow rats of the Overgrowth": a lair in the player's words (<see cref="LairWords"/>) and where it lies.</summary>
        private string LairName(int lairIndex)
        {
            var lairs = _context.Simulation?.Creatures?.Lairs;
            string where = "the floor";
            if (lairs != null && lairIndex >= 0 && lairIndex < lairs.Count && _context.Sectors != null
                && _context.Zones != null && _context.Zones.TryGetZone(_context.Sectors.GetSectorAt(lairs[lairIndex].Definition.Position).Id, out var zone)) where = zone.Name;
            return Invariant($"{LairWords.Pack(lairs, lairIndex)} of {where}");
        }

        private void RefreshBuilder()
        {
            var guild = Guild;
            var host = Host;
            _party.Clear();
            if (guild != null)
            {
                var members = guild.Parties[PartySlot - 1].Members;
                for (int i = 0; i < members.Count; i++) _party.Add(members[i].CrawlerId);
            }
            PartyText = _party.Count == 0 ? Invariant($"Party {PartySlot}") : Invariant($"Party {PartySlot} · {_party.Count}");
            if (guild == null || host == null || _context.Intervention == null || _targets.Count == 0)
            {
                CanDispatch = false;
                DispatchText = _targets.Count == 0 ? "Chart a hazard sector or a lair to send a party there." : string.Empty;
                return;
            }
            if (_party.Count == 0)
            {
                CanDispatch = false;
                DispatchText = Invariant($"Party {PartySlot} has no members: choose them on the Members tab.");
                return;
            }
            var check = guild.CanDispatch(host, Order());
            CanDispatch = check == GuildRules.Check.Ok;
            DispatchText = CanDispatch
                ? (IsClear ? Invariant($"Send party {PartySlot}, {_party.Count} strong, to clear {LairName(_targets[_target].Id)}.")
                    : Invariant($"Send party {PartySlot}, {_party.Count} strong, to {TargetName} for {Goal}.")) + DraughtsInStock(host)
                : GuildRules.Describe(check, string.Empty, null, guild, Tick, (_context.Simulation.Economy ?? EconomyTuning.Default).Guild);
        }

        private DispatchExpeditionCommand Order() =>
            IsClear
                ? new DispatchExpeditionCommand(PartySlot, 0, 1, Rule, Draughts, ExpeditionKind.Clear, _targets[_target].Id) // a clear has no goal: the lair empty
                : new DispatchExpeditionCommand(PartySlot, _targets.Count > 0 ? _targets[_target].Id : (byte)0, Goal, Rule, Draughts);

        /// <summary>" 3 of 8 draughts in stock." when the party is to carry some and the stall can't give them all; empty otherwise.</summary>
        private string DraughtsInStock(IGuildHost host)
        {
            if (Draughts == 0 || host.ActiveStalls.Count == 0) return string.Empty;
            var stall = host.ActiveStalls[0];
            int stock = 0;
            var medicines = Simpiens.Simulation.Items.ItemCatalog.InCategory(Simpiens.Simulation.Items.ItemCategory.Medicine);
            for (int i = 0; i < medicines.Count; i++) stock += stall.SellStockOf(medicines[i]);
            int wanted = Draughts * _party.Count;
            return stock >= wanted ? string.Empty : Invariant($" {stock} of {wanted} draughts in stock.");
        }

        private void RefreshTracker()
        {
            for (int i = 0; i < _trackers.Length; i++) RefreshTracker(_trackers[i]);
        }

        private void RefreshTracker(PartyTrackerModel tracker)
        {
            var guild = Guild;
            var host = Host;
            var party = guild != null && tracker.Slot <= guild.Parties.Count ? guild.Parties[tracker.Slot - 1] : null;
            var expedition = party != null ? party.Expedition : default;
            tracker.IsOut = expedition.IsActive && host != null;
            var rows = tracker.Rows;
            if (!tracker.IsOut)
            {
                rows.Clear();
                return;
            }
            string sector = _context.Sectors != null && _context.Sectors.TryGetSector(expedition.TargetSectorId, out var target) ? target.Name : "the sector";
            tracker.IsClear = expedition.IsClear;
            string foes = expedition.IsClear ? LairWords.Pack(_context.Simulation?.Creatures?.Lairs, expedition.LairIndex) : string.Empty;
            // A clear's title names the pack, not its place, so a boss's fits the tracker (K10.5d: "the Matriarch's brood")
            tracker.Title = expedition.IsClear ? Invariant($"Clear: {Capitalised(foes)} · {expedition.Rule}") : Invariant($"{sector} · {expedition.Rule}");
            tracker.StageText = expedition.Stage switch
            {
                ExpeditionStage.Muster => "Mustering at the stall",
                ExpeditionStage.March => expedition.IsClear ? $"Marching on {foes}" : $"Marching to {sector}",
                ExpeditionStage.Work => $"Working {sector}",
                ExpeditionStage.Fight => $"Fighting {foes}",
                ExpeditionStage.Return => expedition.TurnedBack ? "Turning back" : expedition.Cleared ? "The lair is clear: coming home" : "Coming home",
                _ => "Delivering",
            };
            tracker.TimeOut = OutpostClock.Span(Tick - expedition.DispatchTick) + " out";
            tracker.HaulText = expedition.IsClear
                ? Invariant($"Picked up {party.Gathered(host)} {ItemTexts.Of(expedition.HaulItem).MerchantName.ToLowerInvariant()}")
                : Invariant($"Haul {party.Gathered(host)} of {expedition.Goal}");
            tracker.FightText = expedition.IsClear ? FightLine(host, party, expedition) : string.Empty;

            var roster = party.Members;
            while (rows.Count < roster.Count) rows.Add(new ExpeditionMemberRow());
            if (rows.Count > roster.Count) rows.RemoveRange(roster.Count, rows.Count - roster.Count);
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                var row = rows[i];
                row.Name = _context.FriendlyName(member.CrawlerId, DossierModel.UnknownName); // plain: the tracker is narrow (Epic 11, S6.6)
                // Its part in the party (Epic 11, S5.7, D24): "Bram · front"
                var who = _context.Registry?.GetEntity(member.CrawlerId)?.Agent;
                string role = who != null ? CallingTexts.RoleWord(Simpiens.Simulation.Skills.CallingTable.RoleOf(who.Traits.Calling)) : string.Empty;
                if (role.Length > 0) row.Name += " · " + role;
                row.WithParty = member.IsWithParty;
                row.Downed = member.Status == ExpeditionMemberStatus.Downed;
                row.Status = member.Status switch
                {
                    ExpeditionMemberStatus.Going => "With the party",
                    ExpeditionMemberStatus.Downed => "Downed, limping home",
                    ExpeditionMemberStatus.StayedBehind => "Stayed behind",
                    ExpeditionMemberStatus.Deserted => "Deserted",
                    _ => "Left the guild",
                };
                if (host.TryGetCrawler(member.CrawlerId, out var crawler))
                {
                    row.Health = crawler.Health;
                    row.Gear = WornPieces.Describe(crawler.Inventory); // every slot, in display order (Epic 10 Part 3, K1.5)
                    // The fallen and their carriers (Epic 10 Part 3, K7.3)
                    if (member.IsWithParty && crawler.IsCollapsed)
                        row.Status = crawler.CarriedBy != default ? "Carried out by " + _context.FriendlyName(crawler.CarriedBy, DossierModel.UnknownName) : "Fallen, bleeding";
                    else if (member.IsWithParty && crawler.Carrying != default)
                        row.Status = "Carrying " + _context.FriendlyName(crawler.Carrying, DossierModel.UnknownName) + " out";
                    else if (member.IsWithParty && party.Carrier == member.CrawlerId)
                        row.Status = "Going for " + _context.FriendlyName(party.Fallen, DossierModel.UnknownName);
                }
            }
        }

        /// <summary>"Burrow rats: 2 of 4 left · party health 68": the lair's living and its pack, and the health of those with the party on average.</summary>
        private string FightLine(IGuildHost host, GuildParty party, in ExpeditionRecord expedition)
        {
            var lairs = host.Lairs;
            if (lairs == null || expedition.LairIndex >= lairs.Count) return string.Empty;
            var lair = lairs[expedition.LairIndex];
            float health = 0f;
            int with = 0;
            var roster = party.Members;
            for (int i = 0; i < roster.Count; i++)
            {
                if (!roster[i].IsWithParty || !host.TryGetCrawler(roster[i].CrawlerId, out var c)) continue;
                health += c.Health;
                with++;
            }
            string foes = Capitalised(LairWords.Pack(_context.Simulation?.Creatures?.Lairs, expedition.LairIndex));
            return with == 0 ? Invariant($"{foes}: {lair.Alive} of {lair.Pack} left")
                : Invariant($"{foes}: {lair.Alive} of {lair.Pack} left · party health {health / with:F0}");
        }

        private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
