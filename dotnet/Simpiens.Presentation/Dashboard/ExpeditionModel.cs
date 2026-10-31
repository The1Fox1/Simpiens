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
    /// Expeditions on screen (Epic 10 G5, D7): the party builder in the roster window (the members to send, the target
    /// among the charted hazard sectors, the goal, the retreat rule, outfitting from the stall's stock; Dispatch and
    /// Recall) and the tracker while a party is out (the stage, the time out, the haul, each member's health, gear and
    /// part). The rules are the guild's (<see cref="GuildRules"/>); the player's actions leave as typed commands, applied
    /// on the next tick. Views decide nothing.
    /// </summary>
    public sealed class ExpeditionModel
    {
        private readonly DashboardContext _context;
        private readonly GuildModel _guild;
        private readonly List<(byte Id, string Name)> _targets = new List<(byte, string)>(4);
        private readonly List<ExpeditionMemberRow> _members = new List<ExpeditionMemberRow>(DispatchExpeditionCommand.MaxMembers);
        private readonly List<EntityId> _party = new List<EntityId>(DispatchExpeditionCommand.MaxMembers);
        private int _target;

        internal ExpeditionModel(DashboardContext context, GuildModel guild)
        {
            _context = context;
            _guild = guild;
        }

        // --- The builder ---

        /// <summary>Where a party can go: the charted hazard sectors, in map order. The builder starts on the most dangerous, where nobody goes alone (D11).</summary>
        public IReadOnlyList<(byte Id, string Name)> Targets => _targets;
        public string TargetName => _targets.Count > 0 ? _targets[_target].Name : "No charted danger";
        public byte Goal { get; private set; } = 4;
        public RetreatRule Rule { get; private set; } = RetreatRule.Steady;

        /// <summary>Outfit from stock: members without protection, or with it worn to half its life, are given some from the stall as the party is sent (D12).</summary>
        public bool Outfit { get; private set; } = true;

        /// <summary>"Send 4 to The Drowned Gallery for 4 glowstone", or why not.</summary>
        public string DispatchText { get; private set; } = string.Empty;
        public bool CanDispatch { get; private set; }
        public bool CanRecall => Guild?.HasExpedition == true && _context.Intervention != null;
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

        public void ToggleOutfit()
        {
            Outfit = !Outfit;
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
            if (Outfit && host != null)
            {
                var gear = GearInStock(out int stock);
                for (int i = 0; i < _party.Count && stock > 0; i++)
                {
                    if (Guild.CanGiveGear(host, _party[i], gear) != GuildRules.Check.Ok) continue;
                    _context.Intervention.QueueInfluence(InfluenceEvent.CreateGiveGear("Player", _party[i], gear));
                    stock--;
                }
            }
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateDispatchExpedition("Player", Order()));
            Feedback = new Feedback(Invariant($"{_party.Count} set out for {TargetName}."), FeedbackTone.Ok);
        }

        public void Recall()
        {
            if (!CanRecall) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateRecallExpedition("Player"));
            Feedback = new Feedback("The party is called home.", FeedbackTone.Ok);
        }

        // --- The tracker ---

        public bool IsOut { get; private set; }

        /// <summary>"The Drowned Gallery · Steady".</summary>
        public string Title { get; private set; } = string.Empty;

        /// <summary>"Mustering at the stall", "Marching to The Drowned Gallery", "Working The Drowned Gallery", "Coming home", "Turning back".</summary>
        public string StageText { get; private set; } = string.Empty;

        /// <summary>"1:45 out".</summary>
        public string TimeOut { get; private set; } = string.Empty;

        /// <summary>"Haul 2 of 4".</summary>
        public string HaulText { get; private set; } = string.Empty;

        public IReadOnlyList<ExpeditionMemberRow> Members => _members;

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
            byte chosen = _targets.Count > 0 ? _targets[_target].Id : (byte)0;
            _targets.Clear();
            _target = 0;
            if (sectors == null) return;
            float worst = 0f;
            for (int i = 0; i < sectors.All.Count; i++)
            {
                var sector = sectors.All[i];
                if (!sector.Hazard.IsHazardous || !sectors.IsExplored(sector.Id)) continue;
                if (chosen != 0 ? sector.Id == chosen : sector.Hazard.DamagePerSecond > worst)
                {
                    _target = _targets.Count;
                    worst = sector.Hazard.DamagePerSecond;
                }
                _targets.Add((sector.Id, sector.Name));
            }
        }

        private void RefreshBuilder()
        {
            var guild = Guild;
            var host = Host;
            _party.Clear();
            var rows = _guild.Rows;
            for (int i = 0; i < rows.Count && _party.Count < DispatchExpeditionCommand.MaxMembers; i++)
            {
                if (rows[i].InParty) _party.Add(rows[i].CrawlerId);
            }
            if (guild == null || host == null || _context.Intervention == null || _targets.Count == 0)
            {
                CanDispatch = false;
                DispatchText = _targets.Count == 0 ? "Chart a hazard sector to send a party there." : string.Empty;
                return;
            }
            var check = guild.CanDispatch(host, Order());
            CanDispatch = check == GuildRules.Check.Ok;
            DispatchText = CanDispatch
                ? Invariant($"Send {_party.Count} to {TargetName} for {Goal}.")
                : GuildRules.Describe(check, string.Empty, null, guild, Tick, (_context.Simulation.Economy ?? EconomyTuning.Default).Guild);
        }

        private DispatchExpeditionCommand Order() =>
            new DispatchExpeditionCommand(_targets.Count > 0 ? _targets[_target].Id : (byte)0, Goal, Rule, _party.ToArray());

        private void RefreshTracker()
        {
            var guild = Guild;
            var host = Host;
            var expedition = guild != null ? guild.Expedition : default;
            IsOut = expedition.IsActive;
            if (!IsOut || host == null)
            {
                _members.Clear();
                return;
            }
            string sector = _context.Sectors != null && _context.Sectors.TryGetSector(expedition.TargetSectorId, out var target) ? target.Name : "the sector";
            Title = Invariant($"{sector} · {expedition.Rule}");
            StageText = expedition.Stage switch
            {
                ExpeditionStage.Muster => "Mustering at the stall",
                ExpeditionStage.March => $"Marching to {sector}",
                ExpeditionStage.Work => $"Working {sector}",
                ExpeditionStage.Return => expedition.TurnedBack ? "Turning back" : "Coming home",
                _ => "Delivering",
            };
            uint seconds = (Tick - expedition.DispatchTick) / 10;
            TimeOut = Invariant($"{seconds / 60}:{seconds % 60:00} out");
            HaulText = Invariant($"Haul {guild.Gathered(host)} of {expedition.Goal}");

            var party = guild.Party;
            while (_members.Count < party.Count) _members.Add(new ExpeditionMemberRow());
            if (_members.Count > party.Count) _members.RemoveRange(party.Count, _members.Count - party.Count);
            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                var row = _members[i];
                row.Name = _context.FriendlyName(member.CrawlerId, DossierModel.UnknownName);
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
                    row.Gear = DossierModel.GearText(crawler.Inventory != null ? crawler.Inventory.GetEquipped(EquipmentSlot.Protection) : default);
                }
            }
        }

        /// <summary>The first protection the stall has in stock, and how many.</summary>
        private ItemType GearInStock(out int stock)
        {
            stock = 0;
            var stalls = _context.Simulation?.ActiveStalls;
            if (stalls == null || stalls.Count == 0) return ItemType.None;
            var protection = ItemCatalog.InCategory(ItemCategory.Protection);
            for (int i = 0; i < protection.Count; i++)
            {
                stock = stalls[0].SellStockOf(protection[i]);
                if (stock > 0) return protection[i];
            }
            return ItemType.None;
        }
    }
}
