using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation.Guild
{
    /// <summary>What the guild needs from the simulation (SimulationManager implements it), as the supply service has <see cref="ISupplyHost"/>.</summary>
    public interface IGuildHost
    {
        bool TryGetCrawler(EntityId crawlerId, out AgentState crawler);

        // The map, for expeditions (Epic 10 G5)
        bool TryGetSector(byte sectorId, out SectorDefinition sector);
        bool IsSectorCharted(byte sectorId);
        /// <summary>The map's resource nodes in a sector, ready or dormant, by id.</summary>
        void NodesIn(byte sectorId, List<EntityId> into);
        bool TryGetResource(EntityId resourceId, out ResourceData resource);
        bool TryGetNodePosition(EntityId resourceId, out Vec2 position);

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
    public sealed partial class GuildService
    {
        private readonly List<GuildContract> _members = new List<GuildContract>(8);
        private readonly Dictionary<EntityId, uint> _cooldowns = new Dictionary<EntityId, uint>(8);
        private readonly List<GuildContract> _quitting = new List<GuildContract>(4);
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
            _members.Add(new GuildContract(crawlerId, wage, tick, bunk: FreeBunk())); // a bunk in the barracks, if one is free (Epic 10 Part 2, W5.2)
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

        /// <summary>
        /// The caravan's visit pays every member's wage from <paramref name="stall"/>, in join order, each in full or not at
        /// all (D3): no debt, as with upkeep. A member with a bunk in the barracks is paid its wage less the lodging fee
        /// (Epic 10 Part 2, W5.2, D63): the fee stays in the treasury. A paid member's trust, warmth and allegiance rise (the payment is an
        /// interaction, so its trust doesn't drift back); an unpaid one's fall, and its unpaid visits count up. Then a
        /// member quits below the trust floor or after too many unpaid visits in a row (D4), leaving the guild only.
        /// Wages are transfers from the stall to the member, conserved. Returns the stall as it is after.
        /// </summary>
        public TradeStallData PayWages(IGuildHost host, TradeStallData stall, uint tick)
        {
            // The guild pays from the stall it pays its fees from (the first), once a visit
            if (_members.Count == 0 || host.ActiveStalls.Count == 0 || stall.StallEntityId != host.ActiveStalls[0].StallEntityId) return stall;
            var tuning = Tuning(host);
            var groups = host.Groups;
            bool changed = false;
            for (int i = 0; i < _members.Count; i++)
            {
                var contract = _members[i];
                if (!host.TryGetCrawler(contract.CrawlerId, out var member)) continue;
                uint pay = WagePaid(contract, tuning);
                bool paid = stall.TreasuryGold >= pay && member.Inventory != null;
                if (paid)
                {
                    if (pay > 0 && host.EconomyLedger != null) host.EconomyLedger.TryPayFromStall(stall.StallEntityId, ref stall, member.Inventory, pay, StallPayment.GuildWage);
                    else if (pay > 0)
                    {
                        stall = stall.WithTreasury(stall.TreasuryGold - pay);
                        member.Inventory.AddGold((int)pay);
                    }
                    changed = true;
                    member.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, tuning.Paid.Warmth, tuning.Paid.Trust, 0, tuning.Paid.Respect, tick);
                    groups?.UpdateAllegiance(contract.CrawlerId, _groupId, tuning.PaidAllegiance);
                    _members[i] = contract.WithUnpaidVisits(0);
                }
                else
                {
                    member.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, tuning.Unpaid.Warmth, tuning.Unpaid.Trust, 0, tuning.Unpaid.Respect, tick);
                    groups?.UpdateAllegiance(contract.CrawlerId, _groupId, tuning.UnpaidAllegiance);
                    _members[i] = contract.WithUnpaidVisits((byte)Math.Min(byte.MaxValue, contract.UnpaidVisits + 1));
                    SimLog.Economy.ZLogWarning($"{SimLog.Agent(contract.CrawlerId)} went unpaid ({contract.Wage}g; the treasury holds {stall.TreasuryGold}g), {_members[i].UnpaidVisits} visit(s) in a row");
                    host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.GuildUnpaid, contract.CrawlerId, WorldRegistry.PlayerEntityId,
                        host.GetSectorForPosition(member.Position), tick, ChronicleIntensity.Notable, contract.Wage));
                }
                if (groups != null) member.Memberships = groups.GetMemberships(contract.CrawlerId);
            }
            if (changed) host.UpdateStall(stall);

            // Quitting, in join order: below the trust floor, or after too many unpaid visits in a row (D4)
            _quitting.Clear();
            for (int i = 0; i < _members.Count; i++)
            {
                var contract = _members[i];
                if (!host.TryGetCrawler(contract.CrawlerId, out var member)) continue;
                var (trust, _) = GuildRules.Standing(member, tick);
                if (trust < tuning.QuitTrust || contract.UnpaidVisits >= tuning.MaxUnpaidVisits) _quitting.Add(contract);
            }
            for (int i = 0; i < _quitting.Count; i++)
            {
                var contract = _quitting[i];
                host.TryGetCrawler(contract.CrawlerId, out var member);
                Leave(host, contract.CrawlerId, tick, tuning);
                SimLog.Economy.ZLogInformation($"{SimLog.Agent(contract.CrawlerId)} quit the guild (trust {GuildRules.Standing(member, tick).Trust}, {contract.UnpaidVisits} unpaid visit(s) in a row)");
                host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.GuildQuit, contract.CrawlerId, WorldRegistry.PlayerEntityId,
                    host.GetSectorForPosition(member.Position), tick, ChronicleIntensity.Dramatic));
            }
            return stall;
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

        /// <summary>Forgets everything, the barracks too (a respawn: the next map's bunks are added as it spawns).</summary>
        public void Reset()
        {
            ClearState();
            _barracks.Clear();
        }

        /// <summary>Forgets the guild's state, keeping the barracks: they are the map's, and a load rebuilds the same map.</summary>
        private void ClearState()
        {
            _members.Clear();
            _cooldowns.Clear();
            _groupId = 0;
            ResetParties();
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

            // The parties with a group or a roster, in slot order (Epic 10 Part 2, W5, D60)
            int formed = 0;
            for (int i = 0; i < _parties.Length; i++)
            {
                if (_parties[i].GroupId != 0 || _parties[i].Roster.Count > 0) formed++;
            }
            var parties = new GuildPartySave[formed];
            for (int i = 0, next = 0; i < _parties.Length; i++)
            {
                var party = _parties[i];
                if (party.GroupId == 0 && party.Roster.Count == 0) continue;
                parties[next++] = new GuildPartySave { Slot = party.Slot, GroupId = party.GroupId, Expedition = party.Record, Members = party.Roster.ToArray() };
            }
            save.Parties = parties;
        }

        public void RestoreFrom(GuildSave save)
        {
            ClearState();
            if (save == null) return;
            _groupId = save.GroupId;
            if (save.Members != null) _members.AddRange(save.Members);
            if (save.Cooldowns != null)
            {
                for (int i = 0; i < save.Cooldowns.Length; i++) _cooldowns[save.Cooldowns[i].Id] = save.Cooldowns[i].Tick;
            }
            if (save.Parties == null) return;
            for (int i = 0; i < save.Parties.Length; i++)
            {
                var saved = save.Parties[i];
                if (saved == null || saved.Slot < 1 || saved.Slot > _parties.Length) throw new ArgumentException($"A saved guild party has slot {saved?.Slot}, outside 1 to {_parties.Length}.", nameof(save));
                var party = _parties[saved.Slot - 1];
                party.GroupId = saved.GroupId;
                party.Record = saved.Expedition;
                if (saved.Members != null) party.Roster.AddRange(saved.Members);
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
