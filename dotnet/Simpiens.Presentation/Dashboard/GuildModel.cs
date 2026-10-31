using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>One member's card in the guild roster (Epic 10 G4, D6).</summary>
    public sealed class GuildRosterRow
    {
        public EntityId CrawlerId { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public string Doing { get; internal set; } = string.Empty;
        public float Health { get; internal set; }
        public bool Downed { get; internal set; }
        public string Gear { get; internal set; } = string.Empty;
        public string GearIcon { get; internal set; }
        public string Standing { get; internal set; } = string.Empty;
        public int UnpaidVisits { get; internal set; }

        /// <summary>The wage it is paid now.</summary>
        public int Wage { get; internal set; }

        /// <summary>The wage the slider sets; Apply sends it.</summary>
        public int PendingWage { get; set; }
    }

    /// <summary>
    /// The guild on screen (Epic 10 G4, D6): the roster window (a card per member, with its wage and Dismiss), the
    /// header's count, and what the dossier says about the selected crawler's groups and whether it would sign on. The
    /// rule is the core's (<see cref="GuildRules"/>); the player's actions leave as typed commands, applied on the next
    /// tick. Views decide nothing.
    /// </summary>
    public sealed class GuildModel
    {
        public const string EmptyText = "No members yet. Recruit trusted crawlers from the dossier.";

        private readonly DashboardContext _context;
        private readonly List<GuildRosterRow> _rows = new List<GuildRosterRow>(4);

        internal GuildModel(DashboardContext context) => _context = context;

        public bool IsOpen { get; private set; }
        public IReadOnlyList<GuildRosterRow> Rows => _rows;

        /// <summary>"2/4": members out of the most, for the header.</summary>
        public string Count { get; private set; } = "0/4";

        /// <summary>"2 of 4 members · 11g in wages a visit".</summary>
        public string Status { get; private set; } = string.Empty;

        public Feedback Feedback { get; private set; } = Feedback.None;

        public void Open()
        {
            IsOpen = true;
            Feedback = Feedback.None;
            Refresh();
            for (int i = 0; i < _rows.Count; i++) _rows[i].PendingWage = _rows[i].Wage;
        }

        public void Close() => IsOpen = false;

        public void Refresh()
        {
            var guild = _context.Simulation?.Guild;
            var tuning = _context.Simulation?.Economy?.Guild ?? Simpiens.Simulation.Economy.GuildTuning.Default;
            int members = guild?.Members.Count ?? 0;
            Count = Invariant($"{members}/{tuning.MaxMembers}");
            if (!IsOpen || guild == null) return;

            int wages = 0;
            for (int i = 0; i < members; i++)
            {
                var contract = guild.Members[i];
                if (i == _rows.Count) _rows.Add(new GuildRosterRow { PendingWage = contract.Wage });
                var row = _rows[i];
                if (row.CrawlerId != contract.CrawlerId) row.PendingWage = contract.Wage; // a new member in this place
                row.CrawlerId = contract.CrawlerId;
                row.Wage = contract.Wage;
                row.UnpaidVisits = contract.UnpaidVisits;
                wages += contract.Wage;
                var node = _context.Registry?.GetEntity(contract.CrawlerId);
                var agent = node?.Agent;
                row.Name = node != null ? _context.FriendlyName(node.Id, node.Name) : DossierModel.UnknownName;
                if (agent == null) continue;
                row.Doing = PlanText.Action(agent.HasActivePlan ? agent.ActivePlan.CurrentAction : null);
                row.Health = agent.Health;
                row.Downed = agent.IsDowned;
                var worn = agent.Inventory != null ? agent.Inventory.GetEquipped(EquipmentSlot.Protection) : default;
                row.Gear = DossierModel.GearText(worn);
                row.GearIcon = worn.IsEmpty ? null : ItemIcons.Of(worn.Type);
                var (trust, warmth) = GuildRules.Standing(agent, (uint)(_context.Clock?.CurrentTick ?? 0));
                row.Standing = Invariant($"Trust {trust} · Warmth {warmth}");
            }
            if (_rows.Count > members) _rows.RemoveRange(members, _rows.Count - members);
            Status = Invariant($"{members} of {tuning.MaxMembers} members · {wages}g in wages a visit");
        }

        /// <summary>Apply: a <see cref="SetWageCommand"/> for each member whose slider moved.</summary>
        public void ApplyWages()
        {
            if (_context.Intervention == null) return;
            int sent = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].PendingWage == _rows[i].Wage) continue;
                _context.Intervention.QueueInfluence(InfluenceEvent.CreateSetWage("Player", _rows[i].CrawlerId, (ushort)_rows[i].PendingWage));
                sent++;
            }
            Feedback = sent > 0 ? new Feedback(Invariant($"{sent} wage(s) set for the next visit."), FeedbackTone.Ok) : Feedback.None;
        }

        /// <summary>Dismiss: a <see cref="DismissCommand"/>; the member leaves on the next tick.</summary>
        public void Dismiss(EntityId crawlerId)
        {
            if (_context.Intervention == null) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateDismiss("Player", crawlerId));
            Feedback = new Feedback($"{_context.FriendlyName(crawlerId, DossierModel.UnknownName)} will be let go.", FeedbackTone.Ok);
        }

        // --- What the dossier shows about the selected crawler ---

        /// <summary>"Settlement: led by Mara · Guild: member, 5g a visit", or "No groups".</summary>
        public string GroupsOf(EntityId crawlerId)
        {
            var groups = _context.Simulation?.Groups;
            if (groups == null || crawlerId == default) return "No groups";
            var memberships = groups.GetMemberships(crawlerId);
            if (!memberships.HasAny) return "No groups";
            var parts = new List<string>(memberships.Count);
            for (int i = memberships.Count - 1; i >= 0; i--) // settlement first: it is where a crawler lives
            {
                var membership = memberships[i];
                if (!groups.TryGetGroup(membership.GroupId, out var group)) continue;
                parts.Add(group.Type switch
                {
                    GroupType.Settlement => membership.Role == RoleRank.Leader ? "Settlement: its leader"
                        : $"Settlement: led by {_context.FriendlyName(group.LeaderId, DossierModel.UnknownName)}",
                    GroupType.FactionGuild => _context.Simulation.Guild.TryGetContract(crawlerId, out var contract)
                        ? Invariant($"Guild: member, {contract.Wage}g a visit") : "Guild: member",
                    _ => "Party",
                });
            }
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Whether the selected crawler would sign on now, in words: "Will join (fee 20g)", or the rule's reason ("Not yet:
        /// trust 45 of 60, warmth 22 of 40"); empty for a member.
        /// </summary>
        public string RecruitText(EntityId crawlerId)
        {
            var check = Check(crawlerId, out var agent, out var tuning);
            if (check == GuildRules.Check.AlreadyMember) return string.Empty;
            if (check == GuildRules.Check.Ok) return Invariant($"Will join (fee {tuning.SigningFee}g, {tuning.DefaultWage}g a visit)");
            return GuildRules.Describe(check, _context.FriendlyName(crawlerId, DossierModel.UnknownName), agent, _context.Simulation.Guild, Tick, tuning);
        }

        public bool CanRecruit(EntityId crawlerId) => _context.Intervention != null && Check(crawlerId, out _, out _) == GuildRules.Check.Ok;

        /// <summary>Recruit: a <see cref="RecruitCommand"/> at the default wage; the crawler signs on the next tick if the rule still allows it.</summary>
        public void Recruit(EntityId crawlerId)
        {
            if (!CanRecruit(crawlerId)) return;
            var tuning = _context.Simulation.Economy.Guild;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateRecruit("Player", crawlerId, tuning.DefaultWage));
        }

        private uint Tick => (uint)(_context.Clock?.CurrentTick ?? 0);

        private GuildRules.Check Check(EntityId crawlerId, out Simpiens.Cognition.AgentState agent, out Simpiens.Simulation.Economy.GuildTuning tuning)
        {
            agent = _context.Registry?.GetEntity(crawlerId)?.Agent;
            var sim = _context.Simulation;
            tuning = sim?.Economy?.Guild ?? Simpiens.Simulation.Economy.GuildTuning.Default;
            if (sim is not IGuildHost host) return GuildRules.Check.NoCrawler;
            return sim.Guild.CanRecruit(host, crawlerId, tuning.DefaultWage, Tick);
        }
    }
}
