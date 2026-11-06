using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Expeditions' mechanism on the real floor (Epic 10 G5.3 and G5.4, D7–D13), with the preconditions injected: members
    /// are signed on and put in place. The first tests drive the guild's stage machine tick by tick without the members'
    /// own decisions: the orders and their refusals; the party as a group inside the guild; the stages in order, with the
    /// haul, the shares and the ledger; the muster's time; the retreat rule and a downed member; recall; a member leaving;
    /// a save mid-stage; gear from stock. The last run the whole simulation, members deciding for themselves (G5.4): a
    /// geared party works the seam side by side and comes home, a bare one turns back, a member who distrusts the patron
    /// won't go, and a hungry one stocks up first. The live outcome, with nothing injected, is G5.5's scenario.
    /// </summary>
    public class ExpeditionTests
    {
        private sealed class Outpost : IDisposable
        {
            public LocalSimulation Sim;
            public IGuildHost Host;
            public GuildService Guild;
            public AgentState[] Members;
            public Vec2 Home;
            public Vec2 Gallery;
            public uint Tick = 1;

            public void Dispose() => Sim.Dispose();

            /// <summary>The stage machine alone, one tick on.</summary>
            public void Step(uint ticks = 1)
            {
                for (uint i = 0; i < ticks; i++) Guild.TickExpedition(Host, Tick++);
            }

            public void PutAll(Vec2 where, float hunger = 0f, float health = 100f)
            {
                for (int i = 0; i < Members.Length; i++)
                {
                    Members[i].Position = where + new Vec2(i * 0.5f, 0f);
                    Members[i].Hunger = hunger;
                    Members[i].Health = health;
                }
            }

            public DispatchExpeditionCommand Order(byte goal = 4, RetreatRule rule = RetreatRule.Steady, int count = -1, byte sector = Floor1Layout.DrownedGallerySectorId, byte draughts = 0)
            {
                int n = count < 0 ? Members.Length : count;
                var ids = new EntityId[n];
                for (int i = 0; i < n; i++) ids[i] = Members[i].AgentId;
                return new DispatchExpeditionCommand(sector, goal, rule, ids, draughts);
            }

            /// <summary>Draughts in the stall's stock, by hand (the still's are W2.5's).</summary>
            public void StockDraughts(ushort count)
            {
                var stall = Sim.Simulation.ActiveStalls[0];
                ((SimulationManager)Sim.Simulation).UpdateStall(stall.WithStock(ItemType.HealingDraught, count));
            }
        }

        /// <summary>The default seed's outpost with <paramref name="count"/> residents signed on at the most a wage may be (so the greedy sign too).</summary>
        private static Outpost NewOutpost(int count = 4)
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var o = new Outpost { Sim = sim, Host = (IGuildHost)sim.Simulation, Guild = sim.Simulation.Guild, Members = new AgentState[count] };
            var tuning = sim.Simulation.Economy.Guild;
            for (int i = 0; i < count; i++)
            {
                var crawler = sim.Host.CrawlerStates[i];
                crawler.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
                Assert.Equal(GuildRules.Check.Ok, o.Guild.Recruit(o.Host, crawler.AgentId, tuning.MaxWage, 0));
                o.Members[i] = crawler;
            }
            o.Home = sim.Simulation.ActiveStalls[0].Position;
            o.Host.TryGetSector(Floor1Layout.DrownedGallerySectorId, out var gallery);
            o.Gallery = gallery.TargetPoint;
            return o;
        }

        private static void Carry(AgentState crawler, ItemType item, int count) =>
            Assert.True(crawler.Inventory.TryAddItem(new ItemInstance(90, item, (ushort)count, 0), out _), "The haul fits in the pack.");

        private static int Trust(AgentState crawler, uint tick) => crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, tick).Trust;

        private static List<ChronicleEvent> Told(LocalSimulation sim, EntityId actor)
        {
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetActorEvents(actor, told);
            return told;
        }

        [Fact]
        public void OrdersAreRefusedWithAReason()
        {
            using var o = NewOutpost();
            var tuning = o.Sim.Simulation.Economy.Guild;
            var outsider = o.Sim.Host.CrawlerStates[6];
            Assert.Equal(GuildRules.Check.BadParty, o.Guild.CanDispatch(o.Host, o.Order(count: 0)));
            Assert.Equal(GuildRules.Check.BadParty, o.Guild.CanDispatch(o.Host, new DispatchExpeditionCommand(Floor1Layout.DrownedGallerySectorId, 4, RetreatRule.Steady, new[] { outsider.AgentId })));
            Assert.Equal(GuildRules.Check.BadParty, o.Guild.CanDispatch(o.Host, new DispatchExpeditionCommand(Floor1Layout.DrownedGallerySectorId, 4, RetreatRule.Steady, new[] { o.Members[0].AgentId, o.Members[0].AgentId })));
            o.Members[1].IsDowned = true;
            Assert.Equal(GuildRules.Check.BadParty, o.Guild.CanDispatch(o.Host, o.Order()));
            o.Members[1].IsDowned = false;
            Assert.Equal(GuildRules.Check.BadTarget, o.Guild.CanDispatch(o.Host, o.Order(sector: Floor1Layout.HubSectorId)));
            Assert.Equal(GuildRules.Check.BadTarget, o.Guild.CanDispatch(o.Host, o.Order(sector: Floor1Layout.DeepSeamSectorId)));
            Assert.Equal(GuildRules.Check.BadGoal, o.Guild.CanDispatch(o.Host, o.Order(goal: 0)));
            Assert.Equal(GuildRules.Check.BadGoal, o.Guild.CanDispatch(o.Host, o.Order(goal: (byte)(tuning.Expedition.MaxGoal + 1))));
            Assert.Equal(GuildRules.Check.NoExpedition, o.Guild.Recall(o.Host, o.Tick));

            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(), o.Tick));
            Assert.Equal(GuildRules.Check.ExpeditionOut, o.Guild.Dispatch(o.Host, o.Order(), o.Tick));
            Assert.Equal("A party is already out: one at a time.", GuildRules.Describe(GuildRules.Check.ExpeditionOut, "", null, o.Guild, o.Tick, tuning));

            var gear = ItemType.HazardGear;
            Assert.Equal(GuildRules.Check.NotMember, o.Guild.CanGiveGear(o.Host, outsider.AgentId, gear));
            Assert.Equal(GuildRules.Check.NotGear, o.Guild.CanGiveGear(o.Host, o.Members[0].AgentId, ItemType.Ration));
        }

        [Fact]
        public void APartyMustersMarchesWorksAndComesBackWithItsHaul()
        {
            using var o = NewOutpost();
            var sim = o.Sim.Simulation;
            var groups = sim.Groups;
            var guildGroup = o.Guild.GroupId;
            var settlements = new bool[o.Members.Length];
            for (int i = 0; i < o.Members.Length; i++) settlements[i] = groups.TryGetMembership(o.Members[i].AgentId, GroupType.Settlement, out _);
            o.PutAll(o.Home + new Vec2(0f, 8f)); // in the Garden, away from the muster

            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(goal: 4), o.Tick));
            var expedition = o.Guild.Parties[0].Expedition;
            Assert.True(expedition.IsActive && expedition.Stage == ExpeditionStage.Muster);
            Assert.True(groups.TryGetGroup(expedition.PartyGroupId, out var party) && party.Type == GroupType.TemporaryParty && party.IsPatronLed
                && party.ParentGroupId == guildGroup && party.MemberCount == 4, "The party is a group inside the guild, led by the patron, with all four.");
            Assert.True(party.ActiveDirective.Type == DirectiveType.Expedition && party.ActiveDirective.TargetLocation == o.Home, "Its order sends it to the stall first.");
            for (int i = 0; i < o.Members.Length; i++)
            {
                Assert.True(groups.TryGetMembership(o.Members[i].AgentId, expedition.PartyGroupId, out _) && groups.TryGetMembership(o.Members[i].AgentId, guildGroup, out _),
                    "Each member is in the party and still in the guild.");
                Assert.Equal(expedition.PartyGroupId, o.Members[i].Memberships.DirectiveMembership(null, o.Tick).GroupId);
            }
            Assert.Contains(Told(o.Sim, o.Members[0].AgentId), e => e.EventType == ChronicleEventType.ExpeditionSent && (int)e.ExtraData == 4);

            o.Step(10);
            Assert.Equal(ExpeditionStage.Muster, o.Guild.Parties[0].Expedition.Stage);
            var food = Simpiens.Simulation.Items.ItemCategory.Food;
            int rationsBefore = 0;
            for (int i = 0; i < o.Members.Length; i++) rationsBefore += o.Members[i].Inventory.CountOf(food);
            var stallAtMuster = sim.ActiveStalls[0];
            o.PutAll(o.Home, hunger: 60f, health: 70f);
            o.Step();
            Assert.Equal(ExpeditionStage.Muster, o.Guild.Parties[0].Expedition.Stage); // hurt: not ready
            for (int i = 0; i < o.Members.Length; i++) Assert.True(o.Members[i].Inventory.CountOf(food) >= sim.Economy.Guild.Expedition.Rations, "At the muster, each member is given the trip's rations.");
            int rationsAfter = 0;
            for (int i = 0; i < o.Members.Length; i++) rationsAfter += o.Members[i].Inventory.CountOf(food);
            Assert.Equal(stallAtMuster.TreasuryGold, sim.ActiveStalls[0].TreasuryGold); // from stock, no gold
            Assert.True(rationsAfter > rationsBefore);
            o.PutAll(o.Home, hunger: 60f); // hungry, but carrying its rations: fed
            o.Step();
            Assert.Equal(ExpeditionStage.March, o.Guild.Parties[0].Expedition.Stage);
            groups.TryGetGroup(expedition.PartyGroupId, out party);
            Assert.Equal(o.Gallery, party.ActiveDirective.TargetLocation);
            Assert.Equal(expedition.DispatchTick, party.ActiveDirective.IssuedTick); // judged once, at dispatch

            o.PutAll(o.Gallery, health: 90f);
            o.Step();
            Assert.Equal(ExpeditionStage.Work, o.Guild.Parties[0].Expedition.Stage);
            var seam = new List<EntityId>();
            o.Host.NodesIn(Floor1Layout.DrownedGallerySectorId, seam);
            Assert.Single(seam);
            for (int i = 0; i < o.Members.Length; i++) Assert.Equal(seam[0], o.Guild.Parties[0].Members[i].NodeId); // one seam: they share it

            var haul = o.Guild.Parties[0].Expedition.HaulItem;
            Carry(o.Members[0], haul, 2);
            o.Step();
            Assert.Equal(ExpeditionStage.Work, o.Guild.Parties[0].Expedition.Stage); // 2 of 4
            Carry(o.Members[1], haul, 1);
            Carry(o.Members[3], haul, 1);
            o.Step();
            Assert.Equal(ExpeditionStage.Return, o.Guild.Parties[0].Expedition.Stage);
            Assert.False(o.Guild.Parties[0].Expedition.TurnedBack);

            var stallBefore = sim.ActiveStalls[0];
            uint treasury = stallBefore.TreasuryGold;
            int stock = stallBefore.StockOf(haul);
            var gold = new int[4];
            var trust = new int[4];
            var allegiance = new int[4];
            for (int i = 0; i < 4; i++)
            {
                gold[i] = o.Members[i].Inventory.Gold;
                trust[i] = Trust(o.Members[i], o.Tick);
                groups.TryGetMembership(o.Members[i].AgentId, guildGroup, out var m);
                allegiance[i] = m.Allegiance;
            }
            var ledger = sim.EconomyLedger;
            var flows = ledger.Flows;
            o.PutAll(o.Home, health: 70f);
            o.Step();

            Assert.False(o.Guild.HasExpedition, "Delivered, the expedition ends.");
            var stall = sim.ActiveStalls[0];
            Assert.Equal(stock + 4, stall.StockOf(haul));
            for (int i = 0; i < 4; i++) Assert.Equal(0, o.Members[i].Inventory.GetItemCount(haul));
            uint pool = 4u * sim.Economy.Supply.Terms(haul).Price * sim.Economy.Guild.Expedition.SharePercent / 100u;
            Assert.Equal(84u, pool); // 30% of 70g, four times
            Assert.Equal(treasury - pool, stall.TreasuryGold);
            Assert.Equal(flows.Shares + 4, ledger.Flows.Shares);
            Assert.Equal(flows.ShareGold + pool, ledger.Flows.ShareGold);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(gold[i] + (int)(pool / 4), o.Members[i].Inventory.Gold);
                Assert.Equal(trust[i] + sim.Economy.Guild.Expedition.ReturnedTrust, Trust(o.Members[i], o.Tick));
                Assert.True(groups.TryGetMembership(o.Members[i].AgentId, guildGroup, out var inGuild), "Members stay in the guild.");
                Assert.Equal(Math.Min(100, allegiance[i] + sim.Economy.Guild.Expedition.ReturnedAllegiance), inGuild.Allegiance);
                Assert.Equal(settlements[i], groups.TryGetMembership(o.Members[i].AgentId, GroupType.Settlement, out _));
                Assert.False(groups.TryGetMembership(o.Members[i].AgentId, GroupType.TemporaryParty, out _), "The party is disbanded.");
            }
            Assert.Equal(1, o.Members[0].Activity.GuildDeliveries);
            Assert.Equal(0, o.Members[2].Activity.GuildDeliveries); // carried nothing
            Assert.False(groups.TryGetGroup(expedition.PartyGroupId, out _));
            Assert.Contains(Told(o.Sim, o.Members[0].AgentId), e => e.EventType == ChronicleEventType.ExpeditionReturned && (int)e.ExtraData == 4);
        }

        [Fact]
        public void WhoeverIsntAtTheMusterInTimeStaysBehind()
        {
            using var o = NewOutpost();
            o.PutAll(o.Home);
            o.Members[2].Position = o.Gallery; // far away
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(), o.Tick));
            var tuning = o.Sim.Simulation.Economy.Guild.Expedition;
            o.Step(tuning.MusterTicks); // the dispatch's own tick is the first, 0 into the muster
            Assert.Equal(ExpeditionStage.Muster, o.Guild.Parties[0].Expedition.Stage);
            o.Step();
            Assert.Equal(ExpeditionStage.March, o.Guild.Parties[0].Expedition.Stage);
            Assert.Equal(ExpeditionMemberStatus.StayedBehind, o.Guild.Parties[0].Members[2].Status);
            Assert.False(o.Guild.IsOnExpedition(o.Members[2].AgentId));
            Assert.False(o.Sim.Simulation.Groups.TryGetMembership(o.Members[2].AgentId, GroupType.TemporaryParty, out _), "It's out of the party.");
            Assert.True(o.Guild.IsMember(o.Members[2].AgentId), "It's still in the guild.");
        }

        [Fact]
        public void TheRetreatRuleAndADownedMemberTurnThePartyBack()
        {
            using var o = NewOutpost();
            o.PutAll(o.Home);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(rule: RetreatRule.Steady), o.Tick));
            o.Step();
            o.PutAll(o.Gallery);
            o.Step();
            Assert.Equal(ExpeditionStage.Work, o.Guild.Parties[0].Expedition.Stage);
            o.Members[1].Health = 56f;
            o.Step();
            Assert.Equal(ExpeditionStage.Work, o.Guild.Parties[0].Expedition.Stage); // Steady holds at 55
            o.Members[1].Health = 54f;
            o.Step();
            Assert.Equal(ExpeditionStage.Return, o.Guild.Parties[0].Expedition.Stage);
            Assert.True(o.Guild.Parties[0].Expedition.TurnedBack);
            Assert.Contains(Told(o.Sim, o.Members[0].AgentId), e => e.EventType == ChronicleEventType.ExpeditionTurnedBack);

            // A member downed on the way home limps with the party, and its trust in the player falls
            int trust = Trust(o.Members[3], o.Tick);
            o.Members[3].Health = 0f;
            o.Members[3].IsDowned = true;
            o.Step();
            Assert.Equal(ExpeditionMemberStatus.Downed, o.Guild.Parties[0].Members[3].Status);
            Assert.True(o.Guild.IsOnExpedition(o.Members[3].AgentId));
            Assert.Equal(trust + o.Sim.Simulation.Economy.Guild.Expedition.DownedTrust, Trust(o.Members[3], o.Tick));
            Assert.Contains(Told(o.Sim, o.Members[3].AgentId), e => e.EventType == ChronicleEventType.ExpeditionDowned);

            o.PutAll(o.Home, health: 60f);
            o.Members[3].IsDowned = true;
            o.Step();
            Assert.False(o.Guild.HasExpedition, "Home with nothing, it ends.");
            Assert.Contains(Told(o.Sim, o.Members[0].AgentId), e => e.EventType == ChronicleEventType.ExpeditionTurnedBack && e.SimulationTick == o.Tick - 1);
        }

        /// <summary>
        /// Draughts (Epic 10 Part 2, W2.2, D43, D44): the muster gives each member the order's draughts from the stall's
        /// stock, as stock allows; at work a member under the retreat line who still carries one doesn't turn the party
        /// back (it drinks at the treat line); once it carries none, the rule turns the party back as before.
        /// </summary>
        [Fact]
        public void TheMusterGivesTheOrdersDraughtsAndAMemberWithOneKeepsThePartyWorking()
        {
            using var o = NewOutpost();
            o.StockDraughts(6);
            Assert.Throws<ArgumentException>(() => o.Order(draughts: DispatchExpeditionCommand.MaxDraughts + 1));
            o.PutAll(o.Home);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(rule: RetreatRule.Steady, draughts: 2), o.Tick));
            Assert.Equal(2, o.Guild.Parties[0].Expedition.Draughts);
            o.Step();
            int given = o.Members.Sum(m => m.Inventory.GetItemCount(ItemType.HealingDraught));
            Assert.Equal(6, given); // as stock allows: the first three members get two each
            Assert.Equal(0, o.Sim.Simulation.ActiveStalls[0].StockOf(ItemType.HealingDraught));
            Assert.Equal(2, o.Members[1].Inventory.GetItemCount(ItemType.HealingDraught));
            Assert.Equal(0, o.Members[3].Inventory.GetItemCount(ItemType.HealingDraught));

            o.PutAll(o.Gallery);
            o.Step();
            Assert.Equal(ExpeditionStage.Work, o.Guild.Parties[0].Expedition.Stage);
            o.Members[1].Health = 50f; // under Steady's 55, with two draughts
            o.Step();
            Assert.Equal(ExpeditionStage.Work, o.Guild.Parties[0].Expedition.Stage);
            Assert.False(o.Guild.Parties[0].Expedition.TurnedBack, "A member with a draught drinks instead of turning the party back.");
            o.Members[1].Inventory.TryRemoveItem(ItemType.HealingDraught, 2); // drunk
            o.Step();
            Assert.True(o.Guild.Parties[0].Expedition.TurnedBack && o.Guild.Parties[0].Expedition.Stage == ExpeditionStage.Return, "With none left, the rule turns the party back.");
        }

        [Fact]
        public void ADownedMemberAtWorkTurnsThePartyBackWhateverTheRule()
        {
            using var o = NewOutpost();
            o.PutAll(o.Home);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(rule: RetreatRule.Bold), o.Tick));
            o.Step();
            o.PutAll(o.Gallery);
            o.Step();
            o.Members[0].Health = 0f;
            o.Members[0].IsDowned = true;
            o.Step();
            Assert.Equal(ExpeditionStage.Return, o.Guild.Parties[0].Expedition.Stage);
            Assert.Equal(ExpeditionMemberStatus.Downed, o.Guild.Parties[0].Members[0].Status);
        }

        [Fact]
        public void RecallEndsAMusterAndTurnsAMarchBack()
        {
            using var o = NewOutpost();
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(), o.Tick));
            byte party = o.Guild.Parties[0].Expedition.PartyGroupId;
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Recall(o.Host, o.Tick));
            Assert.False(o.Guild.HasExpedition);
            Assert.False(o.Sim.Simulation.Groups.TryGetGroup(party, out _), "Recalled at the muster, the party is disbanded.");

            o.PutAll(o.Home);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(), o.Tick));
            o.Step();
            Assert.Equal(ExpeditionStage.March, o.Guild.Parties[0].Expedition.Stage);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Recall(o.Host, o.Tick));
            Assert.Equal(ExpeditionStage.Return, o.Guild.Parties[0].Expedition.Stage);
            Assert.True(o.Guild.Parties[0].Expedition.TurnedBack);
            o.Sim.Simulation.Groups.TryGetGroup(o.Guild.Parties[0].Expedition.PartyGroupId, out var group);
            Assert.Equal(o.Home, group.ActiveDirective.TargetLocation);
        }

        [Fact]
        public void AMemberWhoLeavesTheGuildLeavesTheParty()
        {
            using var o = NewOutpost(2);
            o.PutAll(o.Home);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(), o.Tick));
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dismiss(o.Host, o.Members[0].AgentId, o.Tick));
            o.Step();
            Assert.Equal(ExpeditionMemberStatus.Left, o.Guild.Parties[0].Members[0].Status);
            Assert.False(o.Sim.Simulation.Groups.TryGetMembership(o.Members[0].AgentId, GroupType.TemporaryParty, out _));
            Assert.True(o.Guild.HasExpedition);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dismiss(o.Host, o.Members[1].AgentId, o.Tick));
            o.Step();
            Assert.False(o.Guild.HasExpedition, "With nobody left, it ends.");
        }

        [Fact]
        public void ASaveMidStageLoadsTheSameExpedition()
        {
            using var o = NewOutpost();
            o.PutAll(o.Home);
            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Order(), o.Tick));
            o.Step();
            o.PutAll(o.Gallery, health: 90f);
            o.Step();
            Carry(o.Members[2], o.Guild.Parties[0].Expedition.HaulItem, 1);
            Assert.Equal(ExpeditionStage.Work, o.Guild.Parties[0].Expedition.Stage);

            long tick = o.Sim.Clock.CurrentTick;
            var before = WorldStateHash.Capture(tick, o.Sim.Host, o.Sim.Simulation, o.Sim.Sectors, includeWork: false);
            var expedition = o.Guild.Parties[0].Expedition;
            var save = o.Sim.Host.Capture();

            Assert.True(o.Guild.Recall(o.Host, o.Tick) == GuildRules.Check.Ok && o.Guild.Parties[0].Expedition.Stage == ExpeditionStage.Return, "The world moves on after the save.");
            Assert.True(o.Sim.Host.TryRestore(save, out var refusal), refusal);
            var after = WorldStateHash.Capture(tick, o.Sim.Host, o.Sim.Simulation, o.Sim.Sectors, includeWork: false);
            Assert.True(before.Total == after.Total, before.DescribeDifference(after));
            Assert.Equal(expedition.Stage, o.Guild.Parties[0].Expedition.Stage);
            Assert.Equal(expedition.StageTick, o.Guild.Parties[0].Expedition.StageTick);
            Assert.True(o.Sim.Simulation.Groups.TryGetGroup(expedition.PartyGroupId, out var party) && party.ParentGroupId == o.Guild.GroupId
                && party.ActiveDirective.Type == DirectiveType.Expedition && party.MemberCount == 4, "The party, its guild and its order came back.");
        }

        [Fact]
        public void GearFromStockCostsNoGold()
        {
            using var o = NewOutpost(1);
            var member = o.Members[0];
            var gear = ItemType.HazardGear;
            var stall = o.Sim.Simulation.ActiveStalls[0];
            int stock = stall.SellStockOf(gear);
            uint treasury = stall.TreasuryGold;
            int gold = member.Inventory.Gold;
            Assert.True(stock > 0, "The stall stocks hazard gear.");

            var commands = (IPlayerInterventionService)o.Sim.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreateGiveGear("Test", member.AgentId, gear));
            o.Sim.Step(1); // applied on the tick, as every command

            var protection = Simpiens.Simulation.Items.EquipmentSlot.Protection;
            var worn = member.Inventory.GetEquipped(protection);
            Assert.Equal(gear, worn.Type);
            Assert.Equal(Simpiens.Simulation.Items.ItemCatalog.Get(gear).Equipment.Durability, worn.Durability);
            stall = o.Sim.Simulation.ActiveStalls[0];
            Assert.Equal(stock - 1, stall.SellStockOf(gear));
            Assert.Equal(treasury, stall.TreasuryGold);
            Assert.Equal(gold, member.Inventory.Gold);
            Assert.Equal(GuildRules.Check.AlreadyWearing, o.Guild.CanGiveGear(o.Host, member.AgentId, gear));
        }

        /// <summary>
        /// Outfitting by the target's hazard (Epic 10 Part 2, W3.2, D50): the best protection in stock for the Gallery's
        /// flooding is a respirator, for the Glow Caves' falling rock a hard hat, and with neither, hazard gear. Given for
        /// the Gallery, a respirator replaces sound hazard gear, which goes back into the stall's stock; a hard hat, no
        /// better against flooding, is refused.
        /// </summary>
        [Fact]
        public void GearIsGivenForTheTargetsHazardAndSoundGearGoesBackToStock()
        {
            using var o = NewOutpost(1);
            var member = o.Members[0];
            var flooding = GuildRules.HazardOf(o.Host, Floor1Layout.DrownedGallerySectorId);
            var rock = GuildRules.HazardOf(o.Host, Floor1Layout.GlowCavesSectorId);
            Assert.Equal(Simpiens.Simulation.World.HazardKind.Flooding, flooding);
            Assert.Equal(Simpiens.Simulation.World.HazardKind.FallingRock, rock);
            Assert.Equal(ItemType.HazardGear, GuildRules.BestProtectionInStock(o.Host, flooding)); // all the stall opens with

            var sm = (SimulationManager)o.Sim.Simulation;
            sm.UpdateStall(sm.ActiveStalls[0].WithStock(ItemType.Respirator, 1).WithStock(ItemType.HardHat, 1)); // by hand: the forge's are W3.5's
            Assert.Equal(ItemType.Respirator, GuildRules.BestProtectionInStock(o.Host, flooding));
            Assert.Equal(ItemType.HardHat, GuildRules.BestProtectionInStock(o.Host, rock));
            Assert.Equal(0.75f, GuildRules.ProtectionAgainst(ItemType.Respirator, flooding), 3);
            Assert.Equal(0.5f, GuildRules.ProtectionAgainst(ItemType.HazardGear, flooding), 3);
            Assert.Equal(0f, GuildRules.ProtectionAgainst(ItemType.HardHat, flooding), 3);

            var commands = (IPlayerInterventionService)o.Sim.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreateGiveGear("Test", member.AgentId, ItemType.HazardGear));
            o.Sim.Step(1);
            int gearStock = o.Sim.Simulation.ActiveStalls[0].SellStockOf(ItemType.HazardGear);
            Assert.Equal(GuildRules.Check.AlreadyWearing, o.Guild.CanGiveGear(o.Host, member.AgentId, ItemType.Respirator)); // for no hazard, as G3
            Assert.Equal(GuildRules.Check.AlreadyWearing, o.Guild.CanGiveGear(o.Host, member.AgentId, ItemType.HardHat, flooding));

            commands.QueueInfluence(InfluenceEvent.CreateGiveGear("Test", member.AgentId, ItemType.Respirator, flooding));
            o.Sim.Step(1);
            var protection = Simpiens.Simulation.Items.EquipmentSlot.Protection;
            Assert.Equal(ItemType.Respirator, member.Inventory.GetEquipped(protection).Type);
            Assert.Equal(gearStock + 1, o.Sim.Simulation.ActiveStalls[0].SellStockOf(ItemType.HazardGear)); // the sound gear went back
            Assert.Equal(0, o.Sim.Simulation.ActiveStalls[0].SellStockOf(ItemType.Respirator));
            Assert.Equal(GuildRules.Check.OutOfStock, o.Guild.CanGiveGear(o.Host, member.AgentId, ItemType.Respirator, flooding));
        }

        /// <summary>A worn piece the guild replaces waits on the stall's shelf for the forge to mend it (Epic 10 Part 2, W4.2, D58).</summary>
        [Fact]
        public void AWornPieceTheGuildReplacesWaitsForRepair()
        {
            using var o = NewOutpost(1);
            var member = o.Members[0];
            var sm = (SimulationManager)o.Sim.Simulation;
            sm.UpdateStall(sm.ActiveStalls[0].WithStock(ItemType.Respirator, 1)); // by hand
            Assert.True(member.Inventory.TryEquip(new ItemInstance(1, ItemType.Respirator, 1, 60, durability: 500)), "A respirator worn under half its life.");
            var commands = (IPlayerInterventionService)o.Sim.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreateGiveGear("Test", member.AgentId, ItemType.Respirator));
            o.Sim.Step(1);
            var worn = member.Inventory.GetEquipped(Simpiens.Simulation.Items.EquipmentSlot.Protection);
            Assert.Equal(1500, worn.Durability);
            Assert.Equal(1, o.Sim.Simulation.ActiveStalls[0].WornOf(ItemType.Respirator));
            Assert.Equal(0, o.Sim.Simulation.ActiveStalls[0].StockOf(ItemType.Respirator));
        }

        [Fact]
        public void ADispatchCommandAppliesOnTheTick()
        {
            using var o = NewOutpost();
            var commands = (IPlayerInterventionService)o.Sim.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreateDispatchExpedition("Test", o.Order()));
            Assert.False(o.Guild.HasExpedition);
            o.Sim.Step(1);
            Assert.True(o.Guild.HasExpedition && o.Guild.Parties[0].Members.Count == 4);
            commands.QueueInfluence(InfluenceEvent.CreateRecallExpedition("Test"));
            o.Sim.Step(1);
            Assert.False(o.Guild.HasExpedition, "Recalled before it set out, it ends.");
        }

        [Fact]
        public void TheChronicleTellsTheExpedition()
        {
            using var o = NewOutpost(2);
            var formatter = new TemplateNarrativeFormatter(o.Sim.Identity, o.Sim.Sectors);
            string first = o.Sim.Identity.GetFriendlyName(o.Members[0].AgentId);
            const byte gallery = Floor1Layout.DrownedGallerySectorId;
            string Say(ChronicleEventType type, float extra = 0f) =>
                formatter.FormatEvent(new ChronicleEvent(type, o.Members[0].AgentId, WorldRegistry.PlayerEntityId, gallery, 10u, ChronicleIntensity.Dramatic, extra));
            Assert.Equal($"{first} and 1 more set out for The Drowned Gallery.", Say(ChronicleEventType.ExpeditionSent, 2));
            Assert.Equal($"{first} set out alone for The Drowned Gallery.", Say(ChronicleEventType.ExpeditionSent, 1));
            Assert.Equal($"{first}'s party turned back from The Drowned Gallery.", Say(ChronicleEventType.ExpeditionTurnedBack));
            Assert.Equal($"{first}'s party came back from The Drowned Gallery with a haul of 4.", Say(ChronicleEventType.ExpeditionReturned, 4));
            Assert.Equal($"{first} was downed in The Drowned Gallery on an expedition.", Say(ChronicleEventType.ExpeditionDowned));
        }

        // --- Members deciding for themselves (G5.4): the whole simulation runs ---

        /// <summary>The default seed after 30 s, four residents signed on at their own wages, with hazard gear for each if <paramref name="geared"/>.</summary>
        private static Outpost LiveOutpost(bool geared)
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(300);
            var o = new Outpost { Sim = sim, Host = (IGuildHost)sim.Simulation, Guild = sim.Simulation.Guild, Members = new AgentState[4] };
            var tuning = sim.Simulation.Economy.Guild;
            uint tick = (uint)sim.Clock.CurrentTick;
            int n = 0;
            for (int i = 0; n < 4; i++)
            {
                var crawler = sim.Host.CrawlerStates[i];
                if (crawler.IsDrifter) continue;
                crawler.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, tick));
                Assert.Equal(GuildRules.Check.Ok, o.Guild.Recruit(o.Host, crawler.AgentId, GuildRules.ReservationWage(crawler, tuning), tick));
                o.Members[n++] = crawler;
            }
            o.Home = sim.Simulation.ActiveStalls[0].Position;
            // Four signing fees took most of the stall's 105g; shares are paid in full or not at all, as wages
            o.Host.UpdateStall(sim.Simulation.ActiveStalls[0].WithTreasury(300));
            if (geared)
            {
                var stall = sim.Simulation.ActiveStalls[0];
                if (stall.SellStockOf(ItemType.HazardGear) < 4) o.Host.UpdateStall(stall.WithStock(ItemType.HazardGear, 4)); // the stall stocks 3
                var commands = Commands(o);
                for (int i = 0; i < 4; i++) commands.QueueInfluence(InfluenceEvent.CreateGiveGear("Test", o.Members[i].AgentId, ItemType.HazardGear));
                sim.Step(1);
            }
            return o;
        }

        private static IPlayerInterventionService Commands(Outpost o) => (IPlayerInterventionService)o.Sim.Services.GetService(typeof(IPlayerInterventionService));

        /// <summary>
        /// Sends the party and runs until the expedition ends (or <paramref name="limit"/> ticks): the stages it went through,
        /// in order, and whether it turned back (a crawler's chronicle keeps only its latest events).
        /// </summary>
        private static (List<ExpeditionStage> Stages, bool TurnedBack) SendAndRun(Outpost o, in DispatchExpeditionCommand order, int limit = 3000)
        {
            Commands(o).QueueInfluence(InfluenceEvent.CreateDispatchExpedition("Test", order));
            var stages = new List<ExpeditionStage>();
            bool started = false, turnedBack = false;
            for (int t = 0; t < limit; t++)
            {
                o.Sim.Step(1);
                var stage = o.Guild.Parties[0].Expedition.Stage;
                if (stage != ExpeditionStage.None && (stages.Count == 0 || stages[stages.Count - 1] != stage)) stages.Add(stage);
                turnedBack |= o.Guild.Parties[0].Expedition.TurnedBack;
                started |= o.Guild.HasExpedition;
                if (started && !o.Guild.HasExpedition) break;
            }
            return (stages, turnedBack);
        }

        [Fact]
        public void AGearedPartyWorksTheSeamSideBySideAndComesHome()
        {
            using var o = LiveOutpost(geared: true);
            var shares = o.Sim.Simulation.EconomyLedger.Flows.Shares;
            uint sent = (uint)o.Sim.Clock.CurrentTick;
            var (stages, turnedBack) = SendAndRun(o, o.Order(goal: 4, rule: RetreatRule.Steady));
            TestContext.Current.TestOutputHelper?.WriteLine(string.Join(" > ", stages));

            Assert.False(o.Guild.HasExpedition, "The expedition ended.");
            Assert.False(turnedBack, "Geared, it didn't turn back.");
            // Delivering begins and ends within the tick the party is home, so it is never seen between ticks
            Assert.Equal(new[] { ExpeditionStage.Muster, ExpeditionStage.March, ExpeditionStage.Work, ExpeditionStage.Return }, stages);
            Assert.Contains(Told(o.Sim, o.Members[0].AgentId), e => e.EventType == ChronicleEventType.ExpeditionReturned && (int)e.ExtraData == 4);
            Assert.Equal(shares + 4, o.Sim.Simulation.EconomyLedger.Flows.Shares);
            for (int i = 0; i < 4; i++)
            {
                var mine = Told(o.Sim, o.Members[i].AgentId);
                Assert.DoesNotContain(mine, e => e.SimulationTick >= sent && (e.EventType == ChronicleEventType.ExpeditionDowned
                    || e.EventType == ChronicleEventType.TantrumStarted || e.EventType == ChronicleEventType.ResourceYielded));
                Assert.True(o.Guild.IsMember(o.Members[i].AgentId), "Everyone is still in the guild.");
            }
        }

        [Fact]
        public void ABarePartyTurnsBack()
        {
            using var o = LiveOutpost(geared: false);
            var (stages, turnedBack) = SendAndRun(o, o.Order(goal: 4, rule: RetreatRule.Steady));
            TestContext.Current.TestOutputHelper?.WriteLine(string.Join(" > ", stages));
            Assert.False(o.Guild.HasExpedition, "The expedition ended.");
            Assert.Contains(ExpeditionStage.Work, stages);
            Assert.True(turnedBack, "Bare, the Sump and the Gallery took the party below Steady's 55, and it turned back.");
        }

        [Fact]
        public void AMemberWhoDistrustsThePatronWontGo()
        {
            using var o = LiveOutpost(geared: true);
            uint tick = (uint)o.Sim.Clock.CurrentTick;
            var groups = o.Sim.Simulation.Groups;
            var unsure = o.Members[1];
            var deserter = o.Members[2];
            // Below 20 it stays behind; below 0 it deserts (D8): trust, warmth and respect toward the player, allegiance to the
            // guild, less hunger and frustration. 30 x 0.35 + 10 x 0.25 - 10 x 0.30 = 10
            unsure.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(0, 30, 0, 0, 1, tick));
            groups.UpdateAllegiance(unsure.AgentId, o.Guild.GroupId, -60);
            unsure.Hunger = 10f;
            unsure.Frustration = 0f;
            deserter.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(-40, -60, 0, -20, 1, tick));
            groups.UpdateAllegiance(deserter.AgentId, o.Guild.GroupId, -60);
            bool settled = groups.TryGetMembership(deserter.AgentId, GroupType.Settlement, out _);

            Commands(o).QueueInfluence(InfluenceEvent.CreateDispatchExpedition("Test", o.Order()));
            o.Sim.Step(1);
            Assert.True(o.Guild.IsOnExpedition(unsure.AgentId) && o.Guild.IsOnExpedition(deserter.AgentId), "All four are sent.");
            for (int t = 0; t < 200 && (o.Guild.IsOnExpedition(unsure.AgentId) || o.Guild.IsOnExpedition(deserter.AgentId)); t++) o.Sim.Step(1);

            Assert.True(o.Guild.HasExpedition, "The others go.");
            Assert.Equal(ExpeditionMemberStatus.StayedBehind, o.Guild.Parties[0].Members[1].Status);
            Assert.True(o.Guild.IsMember(unsure.AgentId), "Staying behind keeps it in the guild.");
            Assert.Equal(ExpeditionMemberStatus.Deserted, o.Guild.Parties[0].Members[2].Status);
            Assert.False(o.Guild.IsMember(deserter.AgentId), "Deserting takes it out of the guild.");
            Assert.Equal(settled, groups.TryGetMembership(deserter.AgentId, GroupType.Settlement, out _));
        }

        [Fact]
        public void AHungryPennilessMemberIsProvisionedAtTheMuster()
        {
            using var o = LiveOutpost(geared: true);
            var hungry = o.Members[0];
            var rations = Simpiens.Simulation.Items.ItemCategory.Food;
            hungry.Hunger = 45f;
            while (hungry.Inventory.CountOf(rations) > 0) hungry.Inventory.TryRemoveItem(hungry.Inventory.FirstHeld(rations), 1);
            hungry.Inventory.Gold = 0;

            Commands(o).QueueInfluence(InfluenceEvent.CreateDispatchExpedition("Test", o.Order()));
            for (int t = 0; t < 1200 && o.Guild.Parties[0].Expedition.Stage != ExpeditionStage.March; t++) o.Sim.Step(1);

            Assert.Equal(ExpeditionStage.March, o.Guild.Parties[0].Expedition.Stage);
            Assert.True(o.Guild.IsOnExpedition(hungry.AgentId), "It set out with the party.");
            Assert.True(hungry.Hunger < o.Sim.Simulation.Economy.Guild.Expedition.MusterHunger
                || hungry.Inventory.CountOf(rations) >= o.Sim.Simulation.Economy.Guild.Expedition.Rations, "It set out fed, from the stall's stock.");
            Assert.Equal(0, hungry.Inventory.Gold);
        }
    }
}
