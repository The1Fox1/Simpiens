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
    /// with the stage. One expedition at a time. Main thread: the stages are judged every tick after the intents run,
    /// from what the members' states show.
    /// </summary>
    public sealed partial class GuildService
    {
        private ExpeditionRecord _expedition;
        private readonly List<ExpeditionMember> _party = new List<ExpeditionMember>(DispatchExpeditionCommand.MaxMembers);
        private readonly List<EntityId> _nodes = new List<EntityId>(8);
        private readonly List<EntityId> _readyNodes = new List<EntityId>(8);
        private readonly List<int> _claimOrder = new List<int>(DispatchExpeditionCommand.MaxMembers);

        /// <summary>The expedition out, if any (<see cref="ExpeditionRecord.IsActive"/>).</summary>
        public ExpeditionRecord Expedition => _expedition;

        /// <summary>Its party, in the order it was sent, with each member's part.</summary>
        public IReadOnlyList<ExpeditionMember> Party => _party;

        public bool HasExpedition => _expedition.IsActive;

        /// <summary>
        /// <paramref name="crawlerId"/>'s part in the expedition, for cognition (Epic 10 G5.4): the stage, the sector and, at
        /// work, its node. Default when it isn't with the party.
        /// </summary>
        public Simpiens.Cognition.Contracts.ExpeditionOrder OrderFor(EntityId crawlerId)
        {
            int i = PartyIndexOf(crawlerId);
            if (i < 0 || !_party[i].IsWithParty) return default;
            var member = _party[i];
            return new Simpiens.Cognition.Contracts.ExpeditionOrder(_expedition.Stage, _expedition.TargetSectorId, member.NodeId, member.NodePosition);
        }

        /// <summary>
        /// A member judged the order and refused it (D8, from cognition's directive evaluation): staying behind takes it off
        /// the party; deserting takes it out of the guild too, keeping its settlement. Main thread.
        /// </summary>
        public void RefuseOrder(IGuildHost host, EntityId crawlerId, bool deserted, uint tick)
        {
            int i = PartyIndexOf(crawlerId);
            if (i < 0 || !_party[i].IsWithParty) return;
            var member = _party[i];
            member.Status = deserted ? ExpeditionMemberStatus.Deserted : ExpeditionMemberStatus.StayedBehind;
            _party[i] = member;
            LeaveParty(host, crawlerId);
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

        /// <summary>True when <paramref name="crawlerId"/> is with the party of the expedition out.</summary>
        public bool IsOnExpedition(EntityId crawlerId)
        {
            int i = PartyIndexOf(crawlerId);
            return i >= 0 && _party[i].IsWithParty;
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
            byte party = check == GuildRules.Check.Ok && groups != null
                ? groups.CreateGroup(GroupType.TemporaryParty, WorldRegistry.PlayerEntityId, host.ActiveStalls[0].Position, 100, _groupId)
                : (byte)0;
            if (check == GuildRules.Check.Ok && party == 0) check = GuildRules.Check.BadParty; // no group to make it with
            if (check != GuildRules.Check.Ok)
            {
                LogRefusal(host, "Dispatch", order.Count > 0 ? order[0] : default, check, tick);
                return check;
            }

            var tuning = Tuning(host);
            var haul = SectorYield(host, order.TargetSectorId);
            _expedition = new ExpeditionRecord
            {
                DispatchTick = tick,
                StageTick = tick,
                PartyGroupId = party,
                TargetSectorId = order.TargetSectorId,
                HaulItem = haul,
                Goal = order.Goal,
                Rule = order.Rule,
                Stage = ExpeditionStage.Muster,
                Draughts = order.Draughts
            };
            _party.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                var id = order[i];
                host.TryGetCrawler(id, out var crawler);
                byte allegiance = groups.TryGetMembership(id, _groupId, out var guild) ? guild.Allegiance : tuning.InitialAllegiance;
                groups.AddMember(party, id, RoleRank.Follower, allegiance);
                crawler.Memberships = groups.GetMemberships(id);
                int carried = crawler.Inventory != null ? crawler.Inventory.GetItemCount(haul) : 0;
                _party.Add(new ExpeditionMember(id, (ushort)Math.Min(ushort.MaxValue, carried)));
            }
            Direct(host);

            SimLog.Economy.ZLogInformation($"The guild sent {order.Count} to sector {order.TargetSectorId} for {order.Goal} {haul} ({order.Rule}, {order.Draughts} draughts each); party group {party}");
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
            SimLog.Economy.ZLogInformation($"The player recalled the party ({_expedition.Stage})");
            if (_expedition.Stage == ExpeditionStage.Muster) EndExpedition(host, tick);
            else if (_expedition.Stage == ExpeditionStage.March || _expedition.Stage == ExpeditionStage.Work) TurnBack(host, tick);
            return GuildRules.Check.Ok;
        }

        public GuildRules.Check CanGiveGear(IGuildHost host, EntityId crawlerId, ItemType item, HazardKind against = HazardKind.None)
            => GuildRules.CheckGiveGear(this, host, crawlerId, item, against);

        /// <summary>
        /// The player gives a member <paramref name="item"/> from the stall's stock (D12): the stock falls by one, the member
        /// wears it, no gold moves. A worn-down piece in the slot is discarded; a sound one, replaced by gear better against
        /// the trip's hazard (Epic 10 Part 2, D50), goes back into the stall's stock, as the guild gave it.
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
                if (!GuildRules.WornOut(worn) && stall.TryGetGood(worn.Type, out _)) stall = stall.WithStock(worn.Type, (ushort)(stall.StockOf(worn.Type) + 1));
            }
            // The best grade on the shelf first (Epic 10 Part 2, D57), its life the grade's
            stall = stall.TakeBest(item, out byte grade);
            crawler.Inventory.TryEquip(new ItemInstance(1, item, 1, (ushort)stall.SellPriceOf(item), quality: grade, durability: equipment.DurabilityAt(grade)));
            host.UpdateStall(stall);
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} was given {item} from the stall's stock{(worn.IsEmpty ? string.Empty : $" in place of {worn.Type}")}");
            return GuildRules.Check.Ok;
        }

        /// <summary>
        /// Moves the expedition on (D9, D10), every tick after the intents run. First who is still with the party: a member
        /// who left the guild or the outpost, or deserted the order (cognition takes it off the party's roster), is gone,
        /// and a member downed after the muster limps home with it and loses trust. Then the stage: the muster waits for
        /// every member at the stall, fed and well, or its time; the march for the first member in the sector; the work for
        /// the goal, the nodes to be spent or the time; the way home for every member at the stall, or its time. On the way
        /// out and at work the retreat rule turns the party back.
        /// </summary>
        public void TickExpedition(IGuildHost host, uint tick)
        {
            if (!_expedition.IsActive) return;
            var tuning = Tuning(host).Expedition;

            int withParty = 0;
            for (int i = 0; i < _party.Count; i++)
            {
                var member = _party[i];
                if (!member.IsWithParty) continue;
                if (!host.TryGetCrawler(member.CrawlerId, out var crawler) || crawler.HasLeft || !IsMember(member.CrawlerId))
                {
                    member.Status = ExpeditionMemberStatus.Left;
                    LeaveParty(host, member.CrawlerId);
                }
                else if (!InParty(host, member.CrawlerId))
                {
                    member.Status = ExpeditionMemberStatus.Deserted;
                }
                else if (crawler.IsDowned && member.Status == ExpeditionMemberStatus.Going && _expedition.Stage != ExpeditionStage.Muster)
                {
                    member.Status = ExpeditionMemberStatus.Downed;
                    crawler.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, 0, tuning.DownedTrust, 0, 0, tick);
                    SimLog.Economy.ZLogInformation($"{SimLog.Agent(member.CrawlerId)} was downed on the expedition");
                    host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ExpeditionDowned, member.CrawlerId, WorldRegistry.PlayerEntityId,
                        host.GetSectorForPosition(crawler.Position), tick, ChronicleIntensity.Dramatic));
                }
                _party[i] = member;
                if (member.IsWithParty) withParty++;
            }
            if (withParty == 0)
            {
                if (_expedition.Stage != ExpeditionStage.Muster) _expedition.TurnedBack = true;
                EndExpedition(host, tick);
                return;
            }

            uint elapsed = tick - _expedition.StageTick;
            var home = host.ActiveStalls.Count > 0 ? host.ActiveStalls[0].Position : Vec2.Zero;
            switch (_expedition.Stage)
            {
                case ExpeditionStage.Muster:
                    Provision(host, home, tuning);
                    if (AllWithParty(host, PartyTest.Mustered, home, tuning)) SetStage(host, ExpeditionStage.March, tick);
                    else if (elapsed >= tuning.MusterTicks)
                    {
                        // Whoever isn't there, fed and well, stays behind; the rest set out
                        for (int i = 0; i < _party.Count; i++)
                        {
                            var member = _party[i];
                            if (!member.IsWithParty || (host.TryGetCrawler(member.CrawlerId, out var c) && Mustered(c, home, tuning))) continue;
                            member.Status = ExpeditionMemberStatus.StayedBehind;
                            _party[i] = member;
                            LeaveParty(host, member.CrawlerId);
                            SimLog.Economy.ZLogInformation($"{SimLog.Agent(member.CrawlerId)} stayed behind: not at the muster, fed and well");
                        }
                        if (CountWithParty() == 0) EndExpedition(host, tick);
                        else SetStage(host, ExpeditionStage.March, tick);
                    }
                    break;

                case ExpeditionStage.March:
                    // The work starts when the first member reaches the sector, so nobody waits in danger for the rest
                    if (ShouldTurnBack(host, tuning)) TurnBack(host, tick);
                    else if (AnyInSector(host)) SetStage(host, ExpeditionStage.Work, tick);
                    else if (elapsed >= tuning.MarchTicks) TurnBack(host, tick);
                    break;

                case ExpeditionStage.Work:
                    // The goal met or the nodes spent, the party goes home as ordered; short of them, the retreat rule turns it back
                    if (Gathered(host) >= _expedition.Goal || !FindReadyNodes(host)) SetStage(host, ExpeditionStage.Return, tick);
                    else if (ShouldTurnBack(host, tuning)) TurnBack(host, tick);
                    else if (elapsed >= tuning.WorkTicks) SetStage(host, ExpeditionStage.Return, tick);
                    break;

                case ExpeditionStage.Return:
                    if (AllWithParty(host, PartyTest.Home, home, tuning) || elapsed >= tuning.ReturnTicks)
                    {
                        SetStage(host, ExpeditionStage.Deliver, tick);
                        Deliver(host, tick, home, tuning);
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
        private void Deliver(IGuildHost host, uint tick, Vec2 home, in ExpeditionTuning tuning)
        {
            var item = _expedition.HaulItem;
            int haul = 0;
            for (int i = 0; i < _party.Count; i++)
            {
                var member = _party[i];
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
                int payees = CountWithParty();
                if (pool > 0 && payees > 0)
                {
                    uint each = pool / (uint)payees, remainder = pool % (uint)payees;
                    for (int i = 0; i < _party.Count; i++)
                    {
                        var member = _party[i];
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

            for (int i = 0; i < _party.Count && haul > 0; i++)
            {
                var member = _party[i];
                if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler)) continue;
                crawler.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, 0, tuning.ReturnedTrust, 0, 0, tick);
                host.Groups?.UpdateAllegiance(member.CrawlerId, _groupId, tuning.ReturnedAllegiance);
            }

            SimLog.Economy.ZLogInformation($"The party came back with {haul} {item}{(_expedition.TurnedBack ? " (turned back)" : "")}");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(_expedition.TurnedBack && haul == 0 ? ChronicleEventType.ExpeditionTurnedBack : ChronicleEventType.ExpeditionReturned,
                FirstWithParty(), WorldRegistry.PlayerEntityId, _expedition.TargetSectorId, tick, ChronicleIntensity.Dramatic, haul));
            EndExpedition(host, tick);
        }

        /// <summary>
        /// The guild provisions its party (D9, as D12 gears it): each member at the muster carrying fewer than the trip's
        /// rations is given the rest from the stall's stock, no gold changing hands. Members can't fund a trip of about 90
        /// hunger themselves (two rations cost 30g; a member earns 5g a visit), and eat on the way.
        /// </summary>
        /// <summary>
        /// At the muster the guild gives each member at the stall what the trip needs from the stall's stock: its rations
        /// (G5), and the order's draughts (Epic 10 Part 2, W2, D44), up to what it carries already. No gold moves (D12).
        /// </summary>
        private void Provision(IGuildHost host, Vec2 home, in ExpeditionTuning tuning)
        {
            if (host.ActiveStalls.Count == 0) return;
            var stall = host.ActiveStalls[0];
            bool changed = Give(host, ref stall, home, tuning, ItemCategory.Food, tuning.Rations);
            if (_expedition.Draughts > 0) changed |= Give(host, ref stall, home, tuning, ItemCategory.Medicine, _expedition.Draughts);
            if (changed) host.UpdateStall(stall);
        }

        /// <summary>Tops each member at the muster up to <paramref name="count"/> of the first <paramref name="group"/> the stall sells and holds.</summary>
        private bool Give(IGuildHost host, ref TradeStallData stall, Vec2 home, in ExpeditionTuning tuning, ItemCategory group, int count)
        {
            var item = ItemType.None;
            var members = ItemCatalog.InCategory(group);
            for (int i = 0; i < members.Count && item == ItemType.None; i++)
            {
                if (stall.SellStockOf(members[i]) > 0) item = members[i];
            }
            if (item == ItemType.None) return false;

            bool changed = false;
            for (int i = 0; i < _party.Count; i++)
            {
                var member = _party[i];
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
        private void EndExpedition(IGuildHost host, uint tick)
        {
            var groups = host.Groups;
            byte party = _expedition.PartyGroupId;
            if (groups != null && party != 0)
            {
                groups.ClearDirective(party);
                groups.DisbandGroup(party);
            }
            for (int i = 0; i < _party.Count; i++)
            {
                if (groups != null && host.TryGetCrawler(_party[i].CrawlerId, out var crawler)) crawler.Memberships = groups.GetMemberships(_party[i].CrawlerId);
            }
            SimLog.Economy.ZLogInformation($"The expedition ended at t{tick} ({_expedition.Stage}, {(_expedition.TurnedBack ? "turned back" : "as ordered")})");
            ResetExpedition();
        }

        private void ResetExpedition()
        {
            _expedition = default;
            _party.Clear();
        }

        private void TurnBack(IGuildHost host, uint tick)
        {
            _expedition.TurnedBack = true;
            SimLog.Economy.ZLogInformation($"The party turned back from sector {_expedition.TargetSectorId} ({_expedition.Stage})");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ExpeditionTurnedBack, FirstWithParty(), WorldRegistry.PlayerEntityId,
                _expedition.TargetSectorId, tick, ChronicleIntensity.Notable));
            SetStage(host, ExpeditionStage.Return, tick);
        }

        private void SetStage(IGuildHost host, ExpeditionStage stage, uint tick)
        {
            _expedition.Stage = stage;
            _expedition.StageTick = tick;
            if (stage == ExpeditionStage.Work) ClaimNodes(host);
            Direct(host);
        }

        /// <summary>The party's directive, issued at dispatch (so members judge it once) and aimed at the stage's place: the stall, or the sector.</summary>
        private void Direct(IGuildHost host)
        {
            var groups = host.Groups;
            if (groups == null || !_expedition.IsActive) return;
            var target = host.ActiveStalls.Count > 0 ? host.ActiveStalls[0].Position : Vec2.Zero;
            if ((_expedition.Stage == ExpeditionStage.March || _expedition.Stage == ExpeditionStage.Work) && host.TryGetSector(_expedition.TargetSectorId, out var sector))
            {
                target = sector.TargetPoint;
            }
            groups.SetDirective(_expedition.PartyGroupId, new GroupDirective(DirectiveType.Expedition, _expedition.PartyGroupId, WorldRegistry.PlayerEntityId,
                target, default, _expedition.DispatchTick, uint.MaxValue, Tuning(host).Expedition.MusterRadius));
        }

        /// <summary>Each member with the party takes a ready node of the haul's item, in member id order, sharing when there are fewer nodes than members.</summary>
        private void ClaimNodes(IGuildHost host)
        {
            if (!FindReadyNodes(host)) return;
            _claimOrder.Clear();
            for (int i = 0; i < _party.Count; i++)
            {
                if (_party[i].IsWithParty) _claimOrder.Add(i);
            }
            // In member id order (an insertion sort: four at most, and no delegate)
            for (int k = 1; k < _claimOrder.Count; k++)
            {
                int index = _claimOrder[k], j = k - 1;
                while (j >= 0 && _party[_claimOrder[j]].CrawlerId.CompareTo(_party[index].CrawlerId) > 0)
                {
                    _claimOrder[j + 1] = _claimOrder[j];
                    j--;
                }
                _claimOrder[j + 1] = index;
            }
            for (int k = 0; k < _claimOrder.Count; k++)
            {
                var member = _party[_claimOrder[k]];
                member.NodeId = _readyNodes[k % _readyNodes.Count];
                host.TryGetNodePosition(member.NodeId, out member.NodePosition);
                _party[_claimOrder[k]] = member;
            }
        }

        /// <summary>Fills the target sector's ready nodes of the haul's item, by id; true when there is one.</summary>
        private bool FindReadyNodes(IGuildHost host)
        {
            _readyNodes.Clear();
            host.NodesIn(_expedition.TargetSectorId, _nodes);
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (host.TryGetResource(_nodes[i], out var node) && !node.IsDormant && node.RemainingYield > 0 && node.YieldItem == _expedition.HaulItem) _readyNodes.Add(_nodes[i]);
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

        /// <summary>What the members with the party gathered of the haul's item since they were sent (the tracker's haul).</summary>
        public int Gathered(IGuildHost host)
        {
            int gathered = 0;
            for (int i = 0; i < _party.Count; i++)
            {
                var member = _party[i];
                if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler) || crawler.Inventory == null) continue;
                gathered += Math.Max(0, crawler.Inventory.GetItemCount(_expedition.HaulItem) - member.CarriedAtDispatch);
            }
            return gathered;
        }

        /// <summary>
        /// The retreat rule (D10): a member downed, or below the rule's health. A member still carrying medicine (a draught,
        /// Epic 10 Part 2, W2, D43) doesn't count: it drinks at the treat line instead of turning the party back, so what the
        /// party carries is how long it keeps working.
        /// </summary>
        private bool ShouldTurnBack(IGuildHost host, in ExpeditionTuning tuning)
        {
            float floor = tuning.RetreatHealth(_expedition.Rule);
            for (int i = 0; i < _party.Count; i++)
            {
                var member = _party[i];
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

        private bool AnyInSector(IGuildHost host)
        {
            for (int i = 0; i < _party.Count; i++)
            {
                if (_party[i].IsWithParty && host.TryGetCrawler(_party[i].CrawlerId, out var crawler)
                    && host.GetSectorForPosition(crawler.Position) == _expedition.TargetSectorId) return true;
            }
            return false;
        }

        /// <summary>True when every member with the party passes <paramref name="test"/>: at the muster, or home at the stall.</summary>
        private bool AllWithParty(IGuildHost host, PartyTest test, Vec2 home, in ExpeditionTuning tuning)
        {
            for (int i = 0; i < _party.Count; i++)
            {
                var member = _party[i];
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

        private int CountWithParty()
        {
            int n = 0;
            for (int i = 0; i < _party.Count; i++)
            {
                if (_party[i].IsWithParty) n++;
            }
            return n;
        }

        private EntityId FirstWithParty()
        {
            for (int i = 0; i < _party.Count; i++)
            {
                if (_party[i].IsWithParty) return _party[i].CrawlerId;
            }
            return _party.Count > 0 ? _party[0].CrawlerId : default;
        }

        private bool InParty(IGuildHost host, EntityId crawlerId) =>
            host.Groups == null || host.Groups.TryGetMembership(crawlerId, _expedition.PartyGroupId, out _);

        private void LeaveParty(IGuildHost host, EntityId crawlerId)
        {
            var groups = host.Groups;
            if (groups == null) return;
            groups.RemoveMember(_expedition.PartyGroupId, crawlerId);
            if (host.TryGetCrawler(crawlerId, out var crawler)) crawler.Memberships = groups.GetMemberships(crawlerId);
        }

        private int PartyIndexOf(EntityId crawlerId)
        {
            for (int i = 0; i < _party.Count; i++)
            {
                if (_party[i].CrawlerId == crawlerId) return i;
            }
            return -1;
        }
    }
}
