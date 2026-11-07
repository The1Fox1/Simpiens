using System;
using System.Linq;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Social;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Epic 10 G4.6, the guild (decisions D1, D2, D4): the rule and each refusal, the signing fee as a conserved transfer,
    /// eight members at most (Epic 10 Part 2, D61), a recruit keeping its settlement, dismissal and its cooldown, wages, the
    /// commands applied on the tick, the player as patron (no place on the roster, never succeeded), and the barracks (W5.2,
    /// D62, D63): a bunk kept per member, freed when it leaves, a lodging fee taken from its wage, its own bunk first.
    /// </summary>
    public class GuildValidator
    {
        private sealed class Outpost
        {
            public WorldRegistry World;
            public GroupRegistry Groups;
            public EconomyLedger Ledger;
            public PlayerInterventionService Commands;
            public SimulationManager Sim;
            public EntityId StallId;
        }

        public void RunAllValidationTests()
        {
            TestLog.Log("[GuildValidator] Beginning guild validation...");

            ValidateRefusals();
            ValidateRecruiting();
            ValidateFullAtTheLimitAndDismissal();
            ValidateCommandsOnTheTick();
            ValidateThePatron();
            ValidateTheBarracks();

            TestLog.Log("<color=green>[GuildValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateRefusals()
        {
            var o = NewOutpost(treasury: 500);
            var tuning = o.Sim.Economy.Guild;
            var drifter = Crawler(o, trust: 80, warmth: 80);
            drifter.DepartsAtTick = 5000;
            Expect(o, drifter.AgentId, 5, GuildRules.Check.Drifter, "drifters don't sign on");

            var downed = Crawler(o, trust: 80, warmth: 80);
            downed.IsDowned = true;
            Expect(o, downed.AgentId, 5, GuildRules.Check.Downed, "a downed crawler doesn't sign");

            // One role at a time (Epic 10 Part 2, Q4, D6): a station's artisan doesn't sign on, however trusted
            var artisan = Crawler(o, trust: 80, warmth: 80);
            byte workshop = o.Groups.CreateGroup(GroupType.Workshop, WorldRegistry.PlayerEntityId, Vec2.Zero);
            o.Groups.AddMember(workshop, artisan.AgentId);
            artisan.Memberships = o.Groups.GetMemberships(artisan.AgentId);
            Expect(o, artisan.AgentId, 5, GuildRules.Check.WorksAStation, "an artisan works a station");
            Assert(GuildRules.Describe(GuildRules.Check.WorksAStation, "Maya", artisan, o.Sim.Guild, 0, tuning) == "Maya works a station: one role at a time.", "The refusal says why.");

            var unsure = Crawler(o, trust: 45, warmth: 22);
            Expect(o, unsure.AgentId, 5, GuildRules.Check.NotYetTrusted, "trust 45 is below 60");
            string why = GuildRules.Describe(GuildRules.Check.NotYetTrusted, "Maya", unsure, o.Sim.Guild, 0, tuning);
            Assert(why == "Not yet: trust 45 of 60, warmth 22 of 40.", $"The refusal explains the threshold ({why}).");

            var greedy = Crawler(o, trust: 80, warmth: 80);
            greedy.Personality = new AgentPersonality(1f, 0f, 0f, 0f, 0f);
            ushort asks = GuildRules.ReservationWage(greedy, tuning);
            Assert(asks > tuning.ReservationWage, $"A greedy crawler asks more than {tuning.ReservationWage}g ({asks}g).");
            Expect(o, greedy.AgentId, (ushort)(asks - 1), GuildRules.Check.WageTooLow, "a wage below its reservation");
            Expect(o, greedy.AgentId, 0, GuildRules.Check.WageOutOfRange, "a wage of 0");
            Expect(o, greedy.AgentId, (ushort)(tuning.MaxWage + 1), GuildRules.Check.WageOutOfRange, "a wage above the most");
            Expect(o, EntityId.Generate(), 5, GuildRules.Check.NoCrawler, "nobody by that id");

            var poor = NewOutpost(treasury: 10);
            var willing = Crawler(poor, trust: 80, warmth: 80);
            Expect(poor, willing.AgentId, 5, GuildRules.Check.CannotPayFee, "a treasury of 10g can't pay the 20g fee");
            Assert(poor.Sim.ActiveStalls[0].TreasuryGold == 10 && willing.Inventory.Gold == 0, "A refusal changes nothing.");

            TestLog.Log("[PASS] Test 1: Each refusal, in order, with its reason: drifter, downed, an artisan, not yet trusted, wage, fee.");
        }

        private static void ValidateRecruiting()
        {
            var o = NewOutpost(treasury: 500);
            var recruit = Crawler(o, trust: 70, warmth: 50);
            byte settlement = o.Groups.CreateGroup(GroupType.Settlement, EntityId.Generate(), Vec2.Zero);
            o.Groups.AddMember(settlement, recruit.AgentId);
            uint before = o.Sim.ActiveStalls[0].TreasuryGold + (uint)recruit.Inventory.Gold;

            var check = o.Sim.Guild.Recruit(o.Sim, recruit.AgentId, 6, 100);
            Assert(check == GuildRules.Check.Ok, $"A trusted resident signs on ({check}).");
            var tuning = o.Sim.Economy.Guild;
            Assert(o.Sim.ActiveStalls[0].TreasuryGold == 500 - tuning.SigningFee && recruit.Inventory.Gold == tuning.SigningFee, "The stall paid the recruit the signing fee.");
            Assert(o.Sim.ActiveStalls[0].TreasuryGold + (uint)recruit.Inventory.Gold == before, "The fee is a transfer: no gold made or lost.");
            Assert(o.Ledger.Flows.Fees == 1 && o.Ledger.Flows.FeeGold == tuning.SigningFee, "The ledger counts the fee.");

            byte guild = o.Sim.Guild.GroupId;
            Assert(o.Groups.TryGetGroup(guild, out var data) && data.Type == GroupType.FactionGuild && data.IsPatronLed, "The guild is a FactionGuild led by the player as patron.");
            Assert(o.Groups.TryGetMembership(recruit.AgentId, guild, out var member) && member.Allegiance == tuning.InitialAllegiance, "The recruit is in the guild's group.");
            Assert(o.Groups.TryGetMembership(recruit.AgentId, settlement, out _), "It keeps its settlement (D1).");
            Assert(recruit.Memberships.Has(GroupType.FactionGuild) && recruit.Memberships.Has(GroupType.Settlement), "Its own copy holds both groups.");
            Assert(o.Sim.Guild.TryGetContract(recruit.AgentId, out var contract) && contract.Wage == 6 && contract.JoinTick == 100, "Its contract holds the wage and the tick it signed.");
            Assert(o.Sim.Guild.Recruit(o.Sim, recruit.AgentId, 6, 101) == GuildRules.Check.AlreadyMember, "Signing twice is refused.");

            Assert(o.Sim.Guild.SetWage(o.Sim, recruit.AgentId, 9, 102) == GuildRules.Check.Ok && o.Sim.Guild.TryGetContract(recruit.AgentId, out contract) && contract.Wage == 9, "The player sets a member's wage.");
            Assert(o.Sim.Guild.SetWage(o.Sim, EntityId.Generate(), 9, 102) == GuildRules.Check.NotMember, "Only a member has a wage.");

            TestLog.Log("[PASS] Test 2: A recruit is paid the fee from the stall (conserved), joins the patron's guild and keeps its settlement.");
        }

        private static void ValidateFullAtTheLimitAndDismissal()
        {
            var o = NewOutpost(treasury: 500);
            var tuning = o.Sim.Economy.Guild;
            Assert(tuning.MaxMembers == 8, "Eight at most since the guildhall (Epic 10 Part 2, Q9, D61).");
            var full = Enumerable.Range(0, tuning.MaxMembers).Select(_ => Crawler(o, trust: 80, warmth: 80)).ToArray();
            foreach (var c in full) Assert(o.Sim.Guild.Recruit(o.Sim, c.AgentId, 5, 10) == GuildRules.Check.Ok, "Eight sign on.");
            var ninth = Crawler(o, trust: 80, warmth: 80);
            Assert(o.Sim.Guild.Recruit(o.Sim, ninth.AgentId, 5, 10) == GuildRules.Check.GuildFull, "A ninth is refused: eight at most.");

            int trustBefore = full[0].Memory.GetAffinity(WorldRegistry.PlayerEntityId, 20).Trust;
            Assert(o.Sim.Guild.Dismiss(o.Sim, full[0].AgentId, 20) == GuildRules.Check.Ok, "The player lets a member go.");
            Assert(!o.Sim.Guild.IsMember(full[0].AgentId) && !o.Groups.TryGetMembership(full[0].AgentId, o.Sim.Guild.GroupId, out _), "It leaves the guild and its group.");
            Assert(full[0].Memory.GetAffinity(WorldRegistry.PlayerEntityId, 21).Trust == trustBefore + tuning.DismissTrust, "Dismissal costs trust.");
            Assert(o.Sim.Guild.Recruit(o.Sim, full[0].AgentId, 5, 21) == GuildRules.Check.CoolingDown, "It can't sign again at once.");
            Assert(o.Sim.Guild.Recruit(o.Sim, full[0].AgentId, 5, 20 + tuning.RejoinCooldownTicks) == GuildRules.Check.Ok, "After the cooldown it can.");
            Assert(o.Sim.Guild.Dismiss(o.Sim, ninth.AgentId, 30) == GuildRules.Check.NotMember, "Only a member can be dismissed.");

            o.Groups.TryGetGroup(o.Sim.Guild.GroupId, out var data);
            Assert(data.MemberCount == tuning.MaxMembers, $"The roster holds the eight members and not the patron ({data.MemberCount}).");

            TestLog.Log("[PASS] Test 3: Eight members at most; dismissal leaves the guild only, costs trust, and has a cooldown.");
        }

        private static void ValidateCommandsOnTheTick()
        {
            var o = NewOutpost(treasury: 500);
            var a = Crawler(o, trust: 80, warmth: 80);
            var b = Crawler(o, trust: 10, warmth: 10);
            o.Commands.QueueInfluence(InfluenceEvent.CreateRecruit("test", a.AgentId, 7));
            o.Commands.QueueInfluence(InfluenceEvent.CreateRecruit("test", b.AgentId, 7));
            o.Commands.QueueInfluence(InfluenceEvent.CreateSetWage("test", a.AgentId, 8));
            Assert(!o.Sim.Guild.IsMember(a.AgentId), "Nothing happens before the tick.");
            o.Sim.Tick(1);
            Assert(o.Sim.Guild.TryGetContract(a.AgentId, out var contract) && contract.Wage == 8, "On the tick: recruited, then its wage set, in queue order.");
            Assert(!o.Sim.Guild.IsMember(b.AgentId), "An untrusted crawler's command is refused on the tick.");
            o.Commands.QueueInfluence(InfluenceEvent.CreateDismiss("test", a.AgentId));
            o.Sim.Tick(2);
            Assert(!o.Sim.Guild.IsMember(a.AgentId), "Dismiss applies on the tick.");

            TestLog.Log("[PASS] Test 4: Recruit, Set Wage and Dismiss arrive as typed commands, applied on the tick in queue order.");
        }

        private static void ValidateThePatron()
        {
            var o = NewOutpost(treasury: 500);
            var member = Crawler(o, trust: 80, warmth: 80);
            o.Sim.Guild.Recruit(o.Sim, member.AgentId, 5, 1);
            byte guild = o.Sim.Guild.GroupId;
            Assert(!o.Groups.GetMembers(guild).Contains(WorldRegistry.PlayerEntityId) && !o.Groups.GetMemberships(WorldRegistry.PlayerEntityId).HasAny,
                "The patron leads without a place on the roster.");

            // Succession runs every 50 ticks; a crawler-led group of one with a missing leader would be disbanded
            o.Sim.Tick(50);
            Assert(o.Groups.TryGetGroup(guild, out var data) && data.LeaderId == WorldRegistry.PlayerEntityId && data.MemberCount == 1, "Succession never replaces the patron.");

            o.Sim.Guild.Dismiss(o.Sim, member.AgentId, 60);
            o.Sim.Tick(100);
            Assert(o.Groups.TryGetGroup(guild, out data) && data.MemberCount == 0, "The guild is kept while empty.");

            TestLog.Log("[PASS] Test 5: The player as patron: no place on the roster, never succeeded, the guild kept while empty.");
        }

        private static void ValidateTheBarracks()
        {
            var o = NewOutpost(treasury: 500);
            var tuning = o.Sim.Economy.Guild;
            EntityId first = EntityId.Generate(), second = EntityId.Generate();
            Vec2 firstAt = new Vec2(16.5f, -20.5f), secondAt = new Vec2(18f, -20.5f);
            o.Sim.RegisterBarracksBunk(first, firstAt);
            o.Sim.RegisterBarracksBunk(second, secondAt);
            Assert(o.Sim.Guild.Barracks.Count == 2, "The barracks hold the bunks the map spawned.");

            // Kept in signing order, the first free one; with none free, none
            var a = Crawler(o, trust: 80, warmth: 80);
            var b = Crawler(o, trust: 80, warmth: 80);
            var c = Crawler(o, trust: 80, warmth: 80);
            foreach (var member in new[] { a, b, c }) o.Sim.Guild.Recruit(o.Sim, member.AgentId, 5, 1);
            var kept = o.Sim.Guild.KeptBunkFor(a.AgentId);
            Assert(kept.IsKept && kept.BunkId == first && kept.Position == firstAt, "The first to sign keeps the first bunk.");
            Assert(o.Sim.Guild.KeptBunkFor(b.AgentId).BunkId == second, "The second keeps the second.");
            Assert(!o.Sim.Guild.KeptBunkFor(c.AgentId).IsKept, "With every bunk kept, the third has none: it sleeps in the common bunks.");
            Assert(!o.Sim.Guild.KeptBunkFor(EntityId.Generate()).IsKept, "A crawler outside the guild keeps none.");

            // Freed when its member leaves, and kept for the next to sign
            o.Sim.Guild.Dismiss(o.Sim, a.AgentId, 2);
            var d = Crawler(o, trust: 80, warmth: 80);
            o.Sim.Guild.Recruit(o.Sim, d.AgentId, 5, 3);
            Assert(o.Sim.Guild.KeptBunkFor(d.AgentId).BunkId == first, "A member who leaves frees its bunk for the next.");

            // The lodging fee, from the wage of each member with a bunk (D63): the stall pays 4g of a 5g wage, 5g to the member without
            uint before = o.Sim.ActiveStalls[0].TreasuryGold;
            uint wagesBefore = o.Ledger.Flows.WageGold;
            o.Sim.Guild.SetWage(o.Sim, b.AgentId, 1, 4); // a wage no more than the fee: paid nothing, never a debt
            o.Sim.Guild.PayWages(o.Sim, o.Sim.ActiveStalls[0], 600);
            Assert(tuning.LodgingFee == 1, "The lodging fee is 1g a visit.");
            Assert(b.Inventory.Gold == 20 && c.Inventory.Gold == 20 + 5 && d.Inventory.Gold == 20 + 4, $"The wages less the fee: {b.Inventory.Gold - 20}g, {c.Inventory.Gold - 20}g, {d.Inventory.Gold - 20}g after the 20g fees.");
            Assert(before - o.Sim.ActiveStalls[0].TreasuryGold == 9 && o.Ledger.Flows.WageGold - wagesBefore == 9, "The stall paid 9g; the fees stayed in its treasury, and the ledger counts what was paid.");
            Assert(o.Sim.Guild.TryGetContract(b.AgentId, out var paidNothing) && paidNothing.UnpaidVisits == 0, "A wage the fee takes whole is still paid: no unpaid visit.");

            // A load keeps the barracks (the map's) and each member's bunk (its contract); a respawn forgets them
            var save = new Simpiens.Simulation.Persistence.GuildSave();
            o.Sim.Guild.CaptureTo(save);
            o.Sim.Guild.RestoreFrom(save);
            Assert(o.Sim.Guild.Barracks.Count == 2 && o.Sim.Guild.KeptBunkFor(d.AgentId).BunkId == first, "A load keeps the barracks and the bunks kept.");
            o.Sim.Guild.Reset();
            Assert(o.Sim.Guild.Barracks.Count == 0, "A respawn forgets the barracks; the next map's spawn adds its own.");

            // Its own bunk first, wherever it is, though a common bunk it remembers is nearer; a downed crawler takes the nearest
            var memory = (Simpiens.Cognition.Memory.AgentMemory)b.Memory;
            var common = EntityId.Generate();
            memory.SpatialMemoryMap[common] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(common, Simpiens.Simulation.Spatial.EntityType.Bunk, new GridPoint(1, 0), 1);
            var context = new AgentContext(b.AgentId, Vec2.Zero, new AgentNeeds(10f, 20f, 50f), 0f, null, memory, 10, bunk: new KeptBunk(second, secondAt));
            Assert(Simpiens.Cognition.Planning.BunkTargeting.TryGetNearestFreeBunk(context, out var bunkId, out var bunkAt) && bunkId == second && bunkAt == secondAt,
                "A member goes to its own bunk in the barracks before a nearer common one.");
            Assert(Simpiens.Cognition.Planning.BunkTargeting.TryGetNearestFreeBunk(context, out bunkId, out _, ownFirst: false) && bunkId == common,
                "Asked for the nearest (a downed crawler where it lies), it takes the common bunk.");
            var nonMember = new AgentContext(c.AgentId, Vec2.Zero, new AgentNeeds(10f, 20f, 50f), 0f, null, memory, 10);
            Assert(Simpiens.Cognition.Planning.BunkTargeting.TryGetNearestFreeBunk(nonMember, out bunkId, out _) && bunkId == common, "Without a kept bunk, the nearest it remembers.");

            TestLog.Log("[PASS] Test 6: The barracks: a bunk kept per member in signing order and freed when it leaves, kept across a load; a lodging fee from the wage, never a debt; its own bunk first.");
        }

        private static Outpost NewOutpost(uint treasury)
        {
            var o = new Outpost { World = new WorldRegistry(), Groups = new GroupRegistry(), Ledger = new EconomyLedger(), Commands = new PlayerInterventionService() };
            o.Sim = new SimulationManager(o.World, null, groupRegistry: o.Groups, interventionService: o.Commands, economyLedger: o.Ledger);
            o.StallId = EntityId.Generate();
            o.Sim.RegisterTradeStall(TestStalls.Stall(o.StallId, Vec2.Zero, rationSellPrice: 5, rationStock: 50, treasuryGold: treasury));
            return o;
        }

        private static Simpiens.Cognition.AgentState Crawler(Outpost o, sbyte trust, sbyte warmth)
        {
            var crawler = HeadlessEntities.Crawler(o.World, o.Sim, null, Vec2.Zero, id: EntityId.Generate());
            crawler.Agent.Inventory.Gold = 0;
            crawler.Agent.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth, trust, 0, 0, 1, 0));
            return crawler.Agent;
        }

        private static void Expect(Outpost o, EntityId crawlerId, ushort wage, GuildRules.Check expected, string what)
        {
            var check = o.Sim.Guild.Recruit(o.Sim, crawlerId, wage, 0);
            Assert(check == expected, $"Refused ({what}): expected {expected}, got {check}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
