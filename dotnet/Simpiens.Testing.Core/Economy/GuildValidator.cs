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
    /// four members at most, a recruit keeping its settlement, dismissal and its cooldown, wages, the commands applied on
    /// the tick, and the player as patron (no place on the roster, never succeeded).
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
            ValidateFourAtMostAndDismissal();
            ValidateCommandsOnTheTick();
            ValidateThePatron();

            TestLog.Log("<color=green>[GuildValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
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

            TestLog.Log("[PASS] Test 1: Each refusal, in order, with its reason: drifter, downed, not yet trusted, wage, fee.");
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

        private static void ValidateFourAtMostAndDismissal()
        {
            var o = NewOutpost(treasury: 500);
            var tuning = o.Sim.Economy.Guild;
            var four = Enumerable.Range(0, 4).Select(_ => Crawler(o, trust: 80, warmth: 80)).ToArray();
            foreach (var c in four) Assert(o.Sim.Guild.Recruit(o.Sim, c.AgentId, 5, 10) == GuildRules.Check.Ok, "Four sign on.");
            var fifth = Crawler(o, trust: 80, warmth: 80);
            Assert(o.Sim.Guild.Recruit(o.Sim, fifth.AgentId, 5, 10) == GuildRules.Check.GuildFull, "A fifth is refused: four at most (Q4).");

            int trustBefore = four[0].Memory.GetAffinity(WorldRegistry.PlayerEntityId, 20).Trust;
            Assert(o.Sim.Guild.Dismiss(o.Sim, four[0].AgentId, 20) == GuildRules.Check.Ok, "The player lets a member go.");
            Assert(!o.Sim.Guild.IsMember(four[0].AgentId) && !o.Groups.TryGetMembership(four[0].AgentId, o.Sim.Guild.GroupId, out _), "It leaves the guild and its group.");
            Assert(four[0].Memory.GetAffinity(WorldRegistry.PlayerEntityId, 21).Trust == trustBefore + tuning.DismissTrust, "Dismissal costs trust.");
            Assert(o.Sim.Guild.Recruit(o.Sim, four[0].AgentId, 5, 21) == GuildRules.Check.CoolingDown, "It can't sign again at once.");
            Assert(o.Sim.Guild.Recruit(o.Sim, four[0].AgentId, 5, 20 + tuning.RejoinCooldownTicks) == GuildRules.Check.Ok, "After the cooldown it can.");
            Assert(o.Sim.Guild.Dismiss(o.Sim, fifth.AgentId, 30) == GuildRules.Check.NotMember, "Only a member can be dismissed.");

            o.Groups.TryGetGroup(o.Sim.Guild.GroupId, out var data);
            Assert(data.MemberCount == 4, $"The roster holds the four members and not the patron ({data.MemberCount}).");

            TestLog.Log("[PASS] Test 3: Four members at most; dismissal leaves the guild only, costs trust, and has a cooldown.");
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
