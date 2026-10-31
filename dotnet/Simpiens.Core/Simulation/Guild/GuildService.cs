using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using ZLogger;

namespace Simpiens.Simulation.Guild
{
    /// <summary>What the guild needs from the simulation (SimulationManager implements it), as the supply service has <see cref="ISupplyHost"/>.</summary>
    public interface IGuildHost
    {
        bool TryGetCrawler(EntityId crawlerId, out AgentState crawler);
        IGroupRegistry Groups { get; }
        IReadOnlyList<TradeStallData> ActiveStalls { get; }
        void UpdateStall(TradeStallData stall);
        IEconomyLedger EconomyLedger { get; }
        INarrativeChronicleService ChronicleService { get; }
        EconomyTuning Economy { get; }
        int GetSectorForPosition(Vec2 position);
    }

    /// <summary>
    /// The player's guild (Epic 10 G4, decisions D2–D4). The guild is an Epic 8 <see cref="GroupType.FactionGuild"/> group
    /// led by the player as patron (D1), made at the first recruit and kept while empty; this service owns what the group
    /// primitive doesn't: each member's contract (its wage and unpaid visits) and who can't sign again yet. Commands
    /// arrive typed, applied on the tick in queue order; <see cref="GuildRules"/> decides each and explains a refusal.
    /// Main thread.
    /// </summary>
    public sealed class GuildService
    {
        private readonly List<GuildContract> _members = new List<GuildContract>(8);
        private readonly Dictionary<EntityId, uint> _cooldowns = new Dictionary<EntityId, uint>(8);
        private byte _groupId;

        /// <summary>The guild's group, or 0 before the first recruit.</summary>
        public byte GroupId => _groupId;

        /// <summary>Every member's contract, in join order (the order wages are paid in).</summary>
        public IReadOnlyList<GuildContract> Members => _members;

        public bool IsMember(EntityId crawlerId) => IndexOf(crawlerId) >= 0;

        public bool TryGetContract(EntityId crawlerId, out GuildContract contract)
        {
            int i = IndexOf(crawlerId);
            contract = i >= 0 ? _members[i] : default;
            return i >= 0;
        }

        /// <summary>The tick before which <paramref name="crawlerId"/> can't sign again (0 when it can).</summary>
        public uint CooldownUntil(EntityId crawlerId) => _cooldowns.TryGetValue(crawlerId, out uint until) ? until : 0;

        /// <summary>The rule's verdict on recruiting <paramref name="crawlerId"/> at <paramref name="wage"/> now, without acting.</summary>
        public GuildRules.Check CanRecruit(IGuildHost host, EntityId crawlerId, ushort wage, uint tick)
        {
            host.TryGetCrawler(crawlerId, out var crawler);
            bool hasStall = host.ActiveStalls.Count > 0;
            return GuildRules.CheckRecruit(this, crawler, wage, tick, Tuning(host), hasStall, hasStall ? host.ActiveStalls[0].TreasuryGold : 0);
        }

        /// <summary>
        /// Signs <paramref name="crawlerId"/> on at <paramref name="wage"/> a visit (D2): the stall pays the fee, the crawler
        /// joins the guild's group, keeping its settlement (D1), and its contract starts. Refused, it changes nothing.
        /// </summary>
        public GuildRules.Check Recruit(IGuildHost host, EntityId crawlerId, ushort wage, uint tick)
        {
            var check = CanRecruit(host, crawlerId, wage, tick);
            if (check != GuildRules.Check.Ok)
            {
                LogRefusal(host, "Recruit", crawlerId, check, tick);
                return check;
            }
            host.TryGetCrawler(crawlerId, out var crawler);
            var tuning = Tuning(host);
            var stall = host.ActiveStalls[0];

            // The fee: a transfer from the stall to the recruit
            if (host.EconomyLedger != null) host.EconomyLedger.TryPayFromStall(stall.StallEntityId, ref stall, crawler.Inventory, tuning.SigningFee, StallPayment.GuildFee);
            else
            {
                stall = stall.WithTreasury(stall.TreasuryGold - tuning.SigningFee);
                crawler.Inventory?.AddGold(tuning.SigningFee);
            }
            host.UpdateStall(stall);

            var groups = host.Groups;
            if (groups != null)
            {
                if (_groupId == 0 || !groups.TryGetGroup(_groupId, out _))
                {
                    _groupId = groups.CreateGroup(GroupType.FactionGuild, WorldRegistry.PlayerEntityId, stall.Position, initialCohesion: 100);
                }
                groups.AddMember(_groupId, crawlerId, RoleRank.Follower, tuning.InitialAllegiance);
                crawler.Memberships = groups.GetMemberships(crawlerId);
            }
            _members.Add(new GuildContract(crawlerId, wage, tick));
            _cooldowns.Remove(crawlerId);

            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} signed on with the guild at {wage}g a visit; the stall paid a {tuning.SigningFee}g fee ({_members.Count} of {tuning.MaxMembers})");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.GuildJoined, crawlerId, WorldRegistry.PlayerEntityId,
                host.GetSectorForPosition(crawler.Position), tick, ChronicleIntensity.Dramatic, wage));
            return GuildRules.Check.Ok;
        }

        /// <summary>The player lets a member go (D4): it leaves the guild only, loses trust, and can't sign again for a while.</summary>
        public GuildRules.Check Dismiss(IGuildHost host, EntityId crawlerId, uint tick)
        {
            var check = GuildRules.CheckDismiss(this, crawlerId);
            if (check != GuildRules.Check.Ok)
            {
                LogRefusal(host, "Dismiss", crawlerId, check, tick);
                return check;
            }
            var tuning = Tuning(host);
            Leave(host, crawlerId, tick, tuning);
            if (host.TryGetCrawler(crawlerId, out var crawler))
            {
                crawler.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, 0, tuning.DismissTrust, 0, 0, tick);
                host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.GuildDismissed, crawlerId, WorldRegistry.PlayerEntityId,
                    host.GetSectorForPosition(crawler.Position), tick, ChronicleIntensity.Notable));
            }
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} was dismissed from the guild");
            return GuildRules.Check.Ok;
        }

        /// <summary>The player sets a member's wage per caravan visit.</summary>
        public GuildRules.Check SetWage(IGuildHost host, EntityId crawlerId, ushort wage, uint tick)
        {
            var check = GuildRules.CheckSetWage(this, crawlerId, wage, Tuning(host));
            if (check != GuildRules.Check.Ok)
            {
                LogRefusal(host, "Set wage", crawlerId, check, tick);
                return check;
            }
            int i = IndexOf(crawlerId);
            _members[i] = _members[i].WithWage(wage);
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)}'s guild wage is now {wage}g a visit");
            return GuildRules.Check.Ok;
        }

        /// <summary>Takes a member out of the guild (quitting or dismissal): its contract ends, it leaves the guild's group only, and it can't sign again for a while.</summary>
        internal void Leave(IGuildHost host, EntityId crawlerId, uint tick, in GuildTuning tuning)
        {
            int i = IndexOf(crawlerId);
            if (i < 0) return;
            _members.RemoveAt(i);
            _cooldowns[crawlerId] = tick + tuning.RejoinCooldownTicks;
            var groups = host.Groups;
            if (groups != null && _groupId != 0)
            {
                groups.RemoveMember(_groupId, crawlerId);
                if (host.TryGetCrawler(crawlerId, out var crawler)) crawler.Memberships = groups.GetMemberships(crawlerId);
            }
        }

        /// <summary>A crawler who left the outpost is no longer a member; its contract ends with it (the groups already let it go).</summary>
        public void Forget(EntityId crawlerId)
        {
            int i = IndexOf(crawlerId);
            if (i >= 0) _members.RemoveAt(i);
            _cooldowns.Remove(crawlerId);
        }

        public void Reset()
        {
            _members.Clear();
            _cooldowns.Clear();
            _groupId = 0;
        }

        public void CaptureTo(GuildSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save.GroupId = _groupId;
            save.Members = _members.ToArray();
            var cooldowns = new IdTickRecord[_cooldowns.Count];
            int n = 0;
            foreach (var kvp in _cooldowns) cooldowns[n++] = new IdTickRecord { Id = kvp.Key, Tick = kvp.Value };
            Array.Sort(cooldowns, (a, b) => a.Id.CompareTo(b.Id));
            save.Cooldowns = cooldowns;
        }

        public void RestoreFrom(GuildSave save)
        {
            Reset();
            if (save == null) return;
            _groupId = save.GroupId;
            if (save.Members != null) _members.AddRange(save.Members);
            if (save.Cooldowns != null)
            {
                for (int i = 0; i < save.Cooldowns.Length; i++) _cooldowns[save.Cooldowns[i].Id] = save.Cooldowns[i].Tick;
            }
        }

        private int IndexOf(EntityId crawlerId)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].CrawlerId == crawlerId) return i;
            }
            return -1;
        }

        private static GuildTuning Tuning(IGuildHost host) => (host.Economy ?? EconomyTuning.Default).Guild;

        private void LogRefusal(IGuildHost host, string command, EntityId crawlerId, GuildRules.Check check, uint tick)
        {
            if (!SimLog.Intervention.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning)) return;
            host.TryGetCrawler(crawlerId, out var crawler);
            SimLog.Intervention.ZLogWarning($"{command} refused for {SimLog.Agent(crawlerId)}: {GuildRules.Describe(check, SimLog.Agent(crawlerId), crawler, this, tick, Tuning(host))}");
        }
    }
}
