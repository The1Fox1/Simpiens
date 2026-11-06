using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Inventory;
using Simpiens.Diagnostics;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation.Guild
{
    /// <summary>
    /// The guild's expeditions (Epic 10 G5, D7–D13). The player sends a party of members to a charted hazard sector; it
    /// musters at the stall, marches, works the sector's nodes, comes back and hands its haul to the stall's stock, and
    /// each member is paid a share. The party is a <see cref="GroupType.TemporaryParty"/> group inside the guild, led by
    /// the patron, with an <see cref="DirectiveType.Expedition"/> directive issued once at dispatch, whose target moves
    /// with the stage. The guild holds its parties in slots (<see cref="GuildParty"/>, Epic 10 Part 2, W5, D60), each
    /// with its own expedition; one is out at a time. Main thread: the stages are judged every tick after the intents
    /// run, from what the members' states show.
    /// </summary>
    public sealed partial class GuildService
    {
        /// <summary>The guild's party slots (D61): parties 1 and 2.</summary>
        public const int PartySlots = 2;

        private readonly GuildParty[] _parties = { new GuildParty(1), new GuildParty(2) };
        private readonly List<EntityId> _nodes = new List<EntityId>(8);
        private readonly List<EntityId> _readyNodes = new List<EntityId>(8);
        private readonly List<int> _claimOrder = new List<int>(DispatchExpeditionCommand.MaxMembers);

        /// <summary>The guild's parties, in slot order.</summary>
        public IReadOnlyList<GuildParty> Parties => _parties;

        /// <summary>True while a party is out.</summary>
        public bool HasExpedition
        {
            get
            {
                for (int i = 0; i < _parties.Length; i++)
                {
                    if (_parties[i].IsOut) return true;
                }
                return false;
            }
        }

        /// <summary>The party <paramref name="crawlerId"/> is on the roster of, or null.</summary>
        public GuildParty PartyOf(EntityId crawlerId)
        {
            for (int i = 0; i < _parties.Length; i++)
            {
                if (_parties[i].IndexOf(crawlerId) >= 0) return _parties[i];
            }
            return null;
        }

        /// <summary>The party whose group is <paramref name="groupId"/>, or null.</summary>
        public GuildParty PartyByGroup(byte groupId)
        {
            for (int i = 0; groupId != 0 && i < _parties.Length; i++)
            {
                if (_parties[i].GroupId == groupId) return _parties[i];
            }
            return null;
        }

        /// <summary>
        /// <paramref name="crawlerId"/>'s part in an expedition, for cognition (Epic 10 G5.4): the stage, the sector and, at
        /// work, its node. Default when it isn't with a party out.
        /// </summary>
        public Simpiens.Cognition.Contracts.ExpeditionOrder OrderFor(EntityId crawlerId)
        {
            var party = PartyOf(crawlerId);
            if (party == null || !party.IsOut) return default;
            var member = party.Roster[party.IndexOf(crawlerId)];
            if (!member.IsWithParty) return default;
            return new Simpiens.Cognition.Contracts.ExpeditionOrder(party.Record.Stage, party.Record.TargetSectorId, member.NodeId, member.NodePosition);
        }

        /// <summary>
        /// A member judged the order and refused it (D8, from cognition's directive evaluation): staying behind takes it off
        /// the party; deserting takes it out of the guild too, keeping its settlement. Main thread.
        /// </summary>
        public void RefuseOrder(IGuildHost host, EntityId crawlerId, bool deserted, uint tick)
        {
            var party = PartyOf(crawlerId);
            if (party == null || !party.IsOut) return;
            int i = party.IndexOf(crawlerId);
            if (!party.Roster[i].IsWithParty) return;
            var member = party.Roster[i];
            member.Status = deserted ? ExpeditionMemberStatus.Deserted : ExpeditionMemberStatus.StayedBehind;
            party.Roster[i] = member;
            LeaveParty(host, party, crawlerId);
            if (deserted)
            {
                Leave(host, crawlerId, tick, Tuning(host));
                if (host.TryGetCrawler(crawlerId, out var crawler))
                {
                    host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.GuildQuit, crawlerId, WorldRegistry.PlayerEntityId,
                        host.GetSectorForPosition(crawler.Position), tick, ChronicleIntensity.Dramatic));
                }
            }
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} {(deserted ? "deserted the guild rather than go" : "stayed behind")}");
        }

        /// <summary>True when <paramref name="crawlerId"/> is with a party out.</summary>
        public bool IsOnExpedition(EntityId crawlerId)
        {
            var party = PartyOf(crawlerId);
            return party != null && party.IsOut && party.Roster[party.IndexOf(crawlerId)].IsWithParty;
        }

        /// <summary>The rule's verdict on <paramref name="order"/> now, without acting.</summary>
        public GuildRules.Check CanDispatch(IGuildHost host, in DispatchExpeditionCommand order)
            => GuildRules.CheckDispatch(this, host, order, SectorYield(host, order.TargetSectorId), Tuning(host));

        /// <summary>
        /// Sends a party (D7): the party's group, inside the guild and led by the patron, with each member at its guild
        /// allegiance; the order's directive; the muster. Refused, it changes nothing.
        /// </summary>
        public GuildRules.Check Dispatch(IGuildHost host, in DispatchExpeditionCommand order, uint tick)
        {
            var check = CanDispatch(host, order);
            var groups = host.Groups;
            byte group = check == GuildRules.Check.Ok && groups != null
                ? groups.CreateGroup(GroupType.TemporaryParty, WorldRegistry.PlayerEntityId, host.ActiveStalls[0].Position, 100, _groupId)
                : (byte)0;
            if (check == GuildRules.Check.Ok && group == 0) check = GuildRules.Check.BadParty; // no group to make it with
            if (check != GuildRules.Check.Ok)
            {
                LogRefusal(host, "Dispatch", order.Count > 0 ? order[0] : default, check, tick);
                return check;
            }

            var tuning = Tuning(host);
            var haul = SectorYield(host, order.TargetSectorId);
            var party = _parties[0];
            party.GroupId = group;
            party.Record = new ExpeditionRecord
            {
                DispatchTick = tick,
                StageTick = tick,
                PartyGroupId = group,
                TargetSectorId = order.TargetSectorId,
                HaulItem = haul,
                Goal = order.Goal,
                Rule = order.Rule,
                Stage = ExpeditionStage.Muster,
                Draughts = order.Draughts
            };
            party.Roster.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                var id = order[i];
                host.TryGetCrawler(id, out var crawler);
                byte allegiance = groups.TryGetMembership(id, _groupId, out var guild) ? guild.Allegiance : tuning.InitialAllegiance;
                groups.AddMember(group, id, RoleRank.Follower, allegiance);
                crawler.Memberships = groups.GetMemberships(id);
                int carried = crawler.Inventory != null ? crawler.Inventory.GetItemCount(haul) : 0;
                party.Roster.Add(new ExpeditionMember(id, (ushort)Math.Min(ushort.MaxValue, carried)));
            }
            Direct(host, party);

            SimLog.Economy.ZLogInformation($"The guild sent {order.Count} to sector {order.TargetSectorId} for {order.Goal} {haul} ({order.Rule}, {order.Draughts} draughts each); party group {group}");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ExpeditionSent, order[0], WorldRegistry.PlayerEntityId,
                order.TargetSectorId, tick, ChronicleIntensity.Dramatic, order.Count));
            return GuildRules.Check.Ok;
        }

        /// <summary>The player calls the party home (D7): before it sets out, the expedition ends; on the way or at work, it turns back.</summary>
        public GuildRules.Check Recall(IGuildHost host, uint tick)
        {
            var check = GuildRules.CheckRecall(this);
            if (check != GuildRules.Check.Ok)
            {
                LogRefusal(host, "Recall", default, check, tick);
                return check;
            }
            for (int i = 0; i < _parties.Length; i++)
            {
                var party = _parties[i];
                if (!party.IsOut) continue;
                SimLog.Economy.ZLogInformation($"The player recalled party {party.Slot} ({party.Record.Stage})");
                if (party.Record.Stage == ExpeditionStage.Muster) EndExpedition(host, party, tick);
                else if (party.Record.Stage == ExpeditionStage.March || party.Record.Stage == ExpeditionStage.Work) TurnBack(host, party, tick);
            }
            return GuildRules.Check.Ok;
        }

        public GuildRules.Check CanGiveGear(IGuildHost host, EntityId crawlerId, ItemType item, HazardKind against = HazardKind.None)
            => GuildRules.CheckGiveGear(this, host, crawlerId, item, against);

        /// <summary>
        /// The player gives a member <paramref name="item"/> from the stall's stock (D12): the stock falls by one, the member
        /// wears it, no gold moves. The piece it replaces, worn down or replaced by gear better against the trip's hazard
        /// (Epic 10 Part 2, D50), is handed back (<see cref="Simpiens.Simulation.Workshops.Crafting.HandBack"/>): to the shelf
        /// of worn pieces for the forge to mend (D58), or, hazard gear, back into stock while sound.
        /// </summary>
        public GuildRules.Check GiveGear(IGuildHost host, EntityId crawlerId, ItemType item, uint tick, HazardKind against = HazardKind.None)
        {
            var check = CanGiveGear(host, crawlerId, item, against);
            if (check != GuildRules.Check.Ok)
            {
                LogRefusal(host, "Give gear", crawlerId, check, tick);
                return check;
            }
            host.TryGetCrawler(crawlerId, out var crawler);
            var equipment = ItemCatalog.Get(item).Equipment;
            var stall = host.ActiveStalls[0];
            var worn = crawler.Inventory.GetEquipped(equipment.Slot);
            if (!worn.IsEmpty)
            {
                crawler.Inventory.Unequip(equipment.Slot);
                stall = Simpiens.Simulation.Workshops.Crafting.HandBack(stall, worn);
            }
            // The best grade on the shelf first (Epic 10 Part 2, D57), its life the grade's
            stall = stall.TakeBest(item, out byte grade);
            crawler.Inventory.TryEquip(new ItemInstance(1, item, 1, (ushort)stall.SellPriceOf(item), quality: grade, durability: equipment.DurabilityAt(grade)));
            host.UpdateStall(stall);
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} was given {item} from the stall's stock{(worn.IsEmpty ? string.Empty : $" in place of {worn.Type}")}");
            return GuildRules.Check.Ok;
        }

        /// <summary>Moves every party out on (D9, D10), in slot order, every tick after the intents run.</summary>
        public void TickExpedition(IGuildHost host, uint tick)
        {
            for (int i = 0; i < _parties.Length; i++)
            {
                if (_parties[i].IsOut) TickParty(host, _parties[i], tick);
            }
        }

        /// <summary>
        /// Moves <paramref name="party"/>'s expedition on. First who is still with the party: a member who left the guild or
        /// the outpost, or deserted the order (cognition takes it off the party's roster), is gone, and a member downed
        /// after the muster limps home with it and loses trust. Then the stage: the muster waits for every member at the
        /// stall, fed and well, or its time; the march for the first member in the sector; the work for the goal, the
        /// nodes to be spent or the time; the way home for every member at the stall, or its time. On the way out and at
        /// work the retreat rule turns the party back.
        /// </summary>
        private void TickParty(IGuildHost host, GuildParty party, uint tick)
        {
            var tuning = Tuning(host).Expedition;
            var roster = party.Roster;

            int withParty = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                if (!member.IsWithParty) continue;
                if (!host.TryGetCrawler(member.CrawlerId, out var crawler) || crawler.HasLeft || !IsMember(member.CrawlerId))
                {
                    member.Status = ExpeditionMemberStatus.Left;
                    LeaveParty(host, party, member.CrawlerId);
                }
                else if (!InParty(host, party, member.CrawlerId))
                {
                    member.Status = ExpeditionMemberStatus.Deserted;
                }
                else if (crawler.IsDowned && member.Status == ExpeditionMemberStatus.Going && party.Record.Stage != ExpeditionStage.Muster)
                {
                    member.Status = ExpeditionMemberStatus.Downed;
                    crawler.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, 0, tuning.DownedTrust, 0, 0, tick);
                    SimLog.Economy.ZLogInformation($"{SimLog.Agent(member.CrawlerId)} was downed on the expedition");
                    host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ExpeditionDowned, member.CrawlerId, WorldRegistry.PlayerEntityId,
                        host.GetSectorForPosition(crawler.Position), tick, ChronicleIntensity.Dramatic));
                }
                roster[i] = member;
                if (member.IsWithParty) withParty++;
            }
            if (withParty == 0)
            {
                if (party.Record.Stage != ExpeditionStage.Muster) party.Record.TurnedBack = true;
                EndExpedition(host, party, tick);
                return;
            }

            uint elapsed = tick - party.Record.StageTick;
            var home = host.ActiveStalls.Count > 0 ? host.ActiveStalls[0].Position : Vec2.Zero;
            switch (party.Record.Stage)
            {
                case ExpeditionStage.Muster:
                    Provision(host, party, home, tuning);
                    if (AllWithParty(host, party, PartyTest.Mustered, home, tuning)) SetStage(host, party, ExpeditionStage.March, tick);
                    else if (elapsed >= tuning.MusterTicks)
                    {
                        // Whoever isn't there, fed and well, stays behind; the rest set out
                        for (int i = 0; i < roster.Count; i++)
                        {
                            var member = roster[i];
                            if (!member.IsWithParty || (host.TryGetCrawler(member.CrawlerId, out var c) && Mustered(c, home, tuning))) continue;
                            member.Status = ExpeditionMemberStatus.StayedBehind;
                            roster[i] = member;
                            LeaveParty(host, party, member.CrawlerId);
                            SimLog.Economy.ZLogInformation($"{SimLog.Agent(member.CrawlerId)} stayed behind: not at the muster, fed and well");
                        }
                        if (party.CountWithParty() == 0) EndExpedition(host, party, tick);
                        else SetStage(host, party, ExpeditionStage.March, tick);
                    }
                    break;

                case ExpeditionStage.March:
                    // The work starts when the first member reaches the sector, so nobody waits in danger for the rest
                    if (ShouldTurnBack(host, party, tuning)) TurnBack(host, party, tick);
                    else if (AnyInSector(host, party)) SetStage(host, party, ExpeditionStage.Work, tick);
                    else if (elapsed >= tuning.MarchTicks) TurnBack(host, party, tick);
                    break;

                case ExpeditionStage.Work:
                    // The goal met or the nodes spent, the party goes home as ordered; short of them, the retreat rule turns it back
                    if (party.Gathered(host) >= party.Record.Goal || !FindReadyNodes(host, party)) SetStage(host, party, ExpeditionStage.Return, tick);
                    else if (ShouldTurnBack(host, party, tuning)) TurnBack(host, party, tick);
                    else if (elapsed >= tuning.WorkTicks) SetStage(host, party, ExpeditionStage.Return, tick);
                    break;

                case ExpeditionStage.Return:
                    if (AllWithParty(host, party, PartyTest.Home, home, tuning) || elapsed >= tuning.ReturnTicks)
                    {
                        SetStage(host, party, ExpeditionStage.Deliver, tick);
                        Deliver(host, party, tick, home, tuning);
                    }
                    break;
            }
        }

        /// <summary>
        /// The haul (D13): every member with the party at the stall hands over what it gathered of the haul's item, into
        /// the stall's stock without a sale, which closes its cycle. The members with the party split the share of its
        /// caravan value, paid from the stall as transfers (each in full or not at all, as wages), and with a haul each
        /// gains trust and allegiance. Then the expedition ends.
        /// </summary>
        private void Deliver(IGuildHost host, GuildParty party, uint tick, Vec2 home, in ExpeditionTuning tuning)
        {
            var roster = party.Roster;
            var item = party.Record.HaulItem;
            int haul = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler) || crawler.Inventory == null) continue;
                if (Vec2.Distance(crawler.Position, home) > tuning.MusterRadius) continue; // not home: it keeps what it carries
                int gathered = crawler.Inventory.GetItemCount(item) - member.CarriedAtDispatch;
                if (gathered <= 0 || !crawler.Inventory.TryRemoveItem(item, gathered)) continue;
                haul += gathered;
                crawler.Activity.RecordGuildDelivery(tick);
            }

            if (host.ActiveStalls.Count > 0)
            {
                var stall = host.ActiveStalls[0];
                if (haul > 0) stall = stall.WithStock(item, (ushort)Math.Min(ushort.MaxValue, stall.StockOf(item) + haul));

                // The shares: what the caravan pays for the haul, times the members' share, split evenly; the remainder goes
                // one gold each to the first members sent
                var economy = host.Economy ?? EconomyTuning.Default;
                var terms = economy.Supply.Terms(item);
                uint value = terms.Role == CaravanRole.Exports ? terms.Price : stall.BuyPriceOf(item);
                uint pool = (uint)haul * value * tuning.SharePercent / 100u;
                int payees = party.CountWithParty();
                if (pool > 0 && payees > 0)
                {
                    uint each = pool / (uint)payees, remainder = pool % (uint)payees;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var member = roster[i];
                        if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler) || crawler.Inventory == null) continue;
                        uint share = each + (remainder > 0 ? 1u : 0u);
                        if (remainder > 0) remainder--;
                        bool paid = host.EconomyLedger != null
                            ? host.EconomyLedger.TryPayFromStall(stall.StallEntityId, ref stall, crawler.Inventory, share, StallPayment.GuildShare)
                            : PayDirect(ref stall, crawler, share);
                        if (!paid) SimLog.Economy.ZLogWarning($"{SimLog.Agent(member.CrawlerId)}'s {share}g share went unpaid (the treasury holds {stall.TreasuryGold}g)");
                    }
                }
                host.UpdateStall(stall);
            }

            for (int i = 0; i < roster.Count && haul > 0; i++)
            {
                var member = roster[i];
                if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler)) continue;
                crawler.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, 0, tuning.ReturnedTrust, 0, 0, tick);
                host.Groups?.UpdateAllegiance(member.CrawlerId, _groupId, tuning.ReturnedAllegiance);
            }

            SimLog.Economy.ZLogInformation($"Party {party.Slot} came back with {haul} {item}{(party.Record.TurnedBack ? " (turned back)" : "")}");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(party.Record.TurnedBack && haul == 0 ? ChronicleEventType.ExpeditionTurnedBack : ChronicleEventType.ExpeditionReturned,
                party.FirstWithParty(), WorldRegistry.PlayerEntityId, party.Record.TargetSectorId, tick, ChronicleIntensity.Dramatic, haul));
            EndExpedition(host, party, tick);
        }

        /// <summary>
        /// At the muster the guild gives each member at the stall what the trip needs from the stall's stock: its rations
        /// (G5: members can't fund a trip themselves, and eat on the way), and the order's draughts (Epic 10 Part 2, W2,
        /// D44), up to what it carries already. No gold moves (D12).
        /// </summary>
        private void Provision(IGuildHost host, GuildParty party, Vec2 home, in ExpeditionTuning tuning)
        {
            if (host.ActiveStalls.Count == 0) return;
            var stall = host.ActiveStalls[0];
            bool changed = Give(host, party, ref stall, home, tuning, ItemCategory.Food, tuning.Rations);
            if (party.Record.Draughts > 0) changed |= Give(host, party, ref stall, home, tuning, ItemCategory.Medicine, party.Record.Draughts);
            if (changed) host.UpdateStall(stall);
        }

        /// <summary>Tops each member at the muster up to <paramref name="count"/> of the first <paramref name="group"/> the stall sells and holds.</summary>
        private static bool Give(IGuildHost host, GuildParty party, ref TradeStallData stall, Vec2 home, in ExpeditionTuning tuning, ItemCategory group, int count)
        {
            var item = ItemType.None;
            var members = ItemCatalog.InCategory(group);
            for (int i = 0; i < members.Count && item == ItemType.None; i++)
            {
                if (stall.SellStockOf(members[i]) > 0) item = members[i];
            }
            if (item == ItemType.None) return false;

            bool changed = false;
            var roster = party.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler) || crawler.Inventory == null) continue;
                if (Vec2.Distance(crawler.Position, home) > tuning.MusterRadius) continue;
                int given = Math.Min(count - crawler.Inventory.GetItemCount(item), stall.SellStockOf(item));
                if (given <= 0 || !crawler.Inventory.TryAddItem(new ItemInstance((ushort)item, item, (ushort)given, (ushort)stall.SellPriceOf(item)), out _)) continue;
                stall = stall.WithStock(item, (ushort)(stall.StockOf(item) - given));
                changed = true;
                SimLog.Economy.ZLogInformation($"{SimLog.Agent(member.CrawlerId)} was given {given} {item} from the stall's stock for the trip");
            }
            return changed;
        }

        private static bool PayDirect(ref TradeStallData stall, AgentState crawler, uint amount)
        {
            if (stall.TreasuryGold < amount) return false;
            stall = stall.WithTreasury(stall.TreasuryGold - amount);
            crawler.Inventory.AddGold((int)amount);
            return true;
        }

        /// <summary>The expedition ends: the directive is cleared and the party disbanded; its members stay in the guild.</summary>
        private void EndExpedition(IGuildHost host, GuildParty party, uint tick)
        {
            var groups = host.Groups;
            byte group = party.GroupId;
            if (groups != null && group != 0)
            {
                groups.ClearDirective(group);
                groups.DisbandGroup(group);
            }
            var roster = party.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                if (groups != null && host.TryGetCrawler(roster[i].CrawlerId, out var crawler)) crawler.Memberships = groups.GetMemberships(roster[i].CrawlerId);
            }
            SimLog.Economy.ZLogInformation($"Party {party.Slot}'s expedition ended at t{tick} ({party.Record.Stage}, {(party.Record.TurnedBack ? "turned back" : "as ordered")})");
            party.Clear();
        }

        private void TurnBack(IGuildHost host, GuildParty party, uint tick)
        {
            party.Record.TurnedBack = true;
            SimLog.Economy.ZLogInformation($"Party {party.Slot} turned back from sector {party.Record.TargetSectorId} ({party.Record.Stage})");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ExpeditionTurnedBack, party.FirstWithParty(), WorldRegistry.PlayerEntityId,
                party.Record.TargetSectorId, tick, ChronicleIntensity.Notable));
            SetStage(host, party, ExpeditionStage.Return, tick);
        }

        private void SetStage(IGuildHost host, GuildParty party, ExpeditionStage stage, uint tick)
        {
            party.Record.Stage = stage;
            party.Record.StageTick = tick;
            if (stage == ExpeditionStage.Work) ClaimNodes(host, party);
            Direct(host, party);
        }

        /// <summary>The party's directive, issued at dispatch (so members judge it once) and aimed at the stage's place: the stall, or the sector.</summary>
        private static void Direct(IGuildHost host, GuildParty party)
        {
            var groups = host.Groups;
            if (groups == null || !party.IsOut) return;
            var target = host.ActiveStalls.Count > 0 ? host.ActiveStalls[0].Position : Vec2.Zero;
            if ((party.Record.Stage == ExpeditionStage.March || party.Record.Stage == ExpeditionStage.Work) && host.TryGetSector(party.Record.TargetSectorId, out var sector))
            {
                target = sector.TargetPoint;
            }
            groups.SetDirective(party.GroupId, new GroupDirective(DirectiveType.Expedition, party.GroupId, WorldRegistry.PlayerEntityId,
                target, default, party.Record.DispatchTick, uint.MaxValue, Tuning(host).Expedition.MusterRadius));
        }

        /// <summary>Each member with the party takes a ready node of the haul's item, in member id order, sharing when there are fewer nodes than members.</summary>
        private void ClaimNodes(IGuildHost host, GuildParty party)
        {
            if (!FindReadyNodes(host, party)) return;
            var roster = party.Roster;
            _claimOrder.Clear();
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsWithParty) _claimOrder.Add(i);
            }
            // In member id order (an insertion sort: four at most, and no delegate)
            for (int k = 1; k < _claimOrder.Count; k++)
            {
                int index = _claimOrder[k], j = k - 1;
                while (j >= 0 && roster[_claimOrder[j]].CrawlerId.CompareTo(roster[index].CrawlerId) > 0)
                {
                    _claimOrder[j + 1] = _claimOrder[j];
                    j--;
                }
                _claimOrder[j + 1] = index;
            }
            for (int k = 0; k < _claimOrder.Count; k++)
            {
                var member = roster[_claimOrder[k]];
                member.NodeId = _readyNodes[k % _readyNodes.Count];
                host.TryGetNodePosition(member.NodeId, out member.NodePosition);
                roster[_claimOrder[k]] = member;
            }
        }

        /// <summary>Fills the target sector's ready nodes of the haul's item, by id; true when there is one.</summary>
        private bool FindReadyNodes(IGuildHost host, GuildParty party)
        {
            _readyNodes.Clear();
            host.NodesIn(party.Record.TargetSectorId, _nodes);
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (host.TryGetResource(_nodes[i], out var node) && !node.IsDormant && node.RemainingYield > 0 && node.YieldItem == party.Record.HaulItem) _readyNodes.Add(_nodes[i]);
            }
            return _readyNodes.Count > 0;
        }

        /// <summary>What the target sector's nodes yield (its first node's, by id), or None.</summary>
        private ItemType SectorYield(IGuildHost host, byte sectorId)
        {
            host.NodesIn(sectorId, _nodes);
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (host.TryGetResource(_nodes[i], out var node)) return node.YieldItem;
            }
            return ItemType.None;
        }

        /// <summary>
        /// The retreat rule (D10): a member downed, or below the rule's health. A member still carrying medicine (a draught,
        /// Epic 10 Part 2, W2, D43) doesn't count: it drinks at the treat line instead of turning the party back, so what the
        /// party carries is how long it keeps working.
        /// </summary>
        private static bool ShouldTurnBack(IGuildHost host, GuildParty party, in ExpeditionTuning tuning)
        {
            float floor = tuning.RetreatHealth(party.Record.Rule);
            var roster = party.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                if (member.Status == ExpeditionMemberStatus.Downed) return true;
                if (member.IsWithParty && host.TryGetCrawler(member.CrawlerId, out var crawler) && crawler.Health < floor
                    && !crawler.Inventory.HasAny(ItemCategory.Medicine)) return true;
            }
            return false;
        }

        /// <summary>At the stall, fed (below the muster's hunger, or carrying the trip's rations to eat on the way) and well.</summary>
        private static bool Mustered(AgentState crawler, Vec2 home, in ExpeditionTuning tuning) =>
            Vec2.Distance(crawler.Position, home) <= tuning.MusterRadius
            && (crawler.Hunger < tuning.MusterHunger || crawler.Inventory.CountOf(ItemCategory.Food) >= tuning.Rations)
            && crawler.Health >= tuning.MusterHealth && !crawler.IsDowned;

        private enum PartyTest : byte { Mustered, Home }

        private static bool AnyInSector(IGuildHost host, GuildParty party)
        {
            var roster = party.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsWithParty && host.TryGetCrawler(roster[i].CrawlerId, out var crawler)
                    && host.GetSectorForPosition(crawler.Position) == party.Record.TargetSectorId) return true;
            }
            return false;
        }

        /// <summary>True when every member with the party passes <paramref name="test"/>: at the muster, or home at the stall.</summary>
        private static bool AllWithParty(IGuildHost host, GuildParty party, PartyTest test, Vec2 home, in ExpeditionTuning tuning)
        {
            var roster = party.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                if (!member.IsWithParty) continue;
                if (!host.TryGetCrawler(member.CrawlerId, out var crawler)) return false;
                bool passes = test switch
                {
                    PartyTest.Mustered => Mustered(crawler, home, tuning),
                    // A downed member limps to the bunks (D10), not the stall: the way home doesn't wait for it
                    _ => member.Status == ExpeditionMemberStatus.Downed || Vec2.Distance(crawler.Position, home) <= tuning.MusterRadius,
                };
                if (!passes) return false;
            }
            return true;
        }

        private static bool InParty(IGuildHost host, GuildParty party, EntityId crawlerId) =>
            host.Groups == null || host.Groups.TryGetMembership(crawlerId, party.GroupId, out _);

        private static void LeaveParty(IGuildHost host, GuildParty party, EntityId crawlerId)
        {
            var groups = host.Groups;
            if (groups == null) return;
            groups.RemoveMember(party.GroupId, crawlerId);
            if (host.TryGetCrawler(crawlerId, out var crawler)) crawler.Memberships = groups.GetMemberships(crawlerId);
        }

        private void ResetParties()
        {
            for (int i = 0; i < _parties.Length; i++) _parties[i].Clear();
        }
    }
}
