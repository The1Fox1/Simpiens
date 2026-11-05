using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session.Layouts;
using Simpiens.Testing.Environment;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The player's workshop commands (Epic 10 Part 2, W1), on a simulation with Floor 1's map: building a station
    /// pays the construction from the stall into the <c>Construction</c> sink, conserved by the ledger; hiring puts a
    /// cautious, trusted resident in the station's crew on a contract, paid after the guild's members and quitting as a
    /// member does; Dismiss and SetWage reach an artisan by who it is; each refusal comes in order, with its words, and
    /// changes nothing; commands apply on the tick and the chronicle tells of them. Runs as a test (ValidatorTests),
    /// never at boot.
    /// </summary>
    public class WorkshopValidator
    {
        private sealed class Outpost
        {
            public WorldRegistry World;
            public GroupRegistry Groups;
            public EconomyLedger Ledger;
            public PlayerInterventionService Commands;
            public NarrativeChronicleService Chronicle;
            public SectorRegistry Sectors;
            public SimulationManager Sim;
            public int Forge, Still;

            public uint Treasury => Sim.ActiveStalls[0].TreasuryGold;
        }

        public void RunAllValidationTests()
        {
            TestLog.Log("[WorkshopValidator] Beginning workshop validation...");

            ValidateBuilding();
            ValidateBuildRefusals();
            ValidateBuildCommandOnTheTick();
            ValidateHiring();
            ValidateHireRefusals();
            ValidateWagesAfterTheGuildsAndQuitting();
            ValidateCommandsReachAnArtisanByWhoItIs();
            ValidateOrdersAndShiftsAsCommands();
            ValidateShiftBlocks();

            TestLog.Log("<color=green>[WorkshopValidator] ALL 9 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateBuilding()
        {
            var o = NewOutpost(treasury: 500);
            uint cost = o.Sim.Economy.Workshops.ConstructionCost;
            Assert(o.Sim.Workshops.CanBuild(o.Sim, o.Forge) == WorkshopRules.Check.Ok, "The forge can be built.");
            Assert(o.Sim.Workshops.Build(o.Sim, o.Forge, 10) == WorkshopRules.Check.Ok && o.Sim.Workshops[o.Forge].Built, "The forge is built.");
            Assert(o.Treasury == 500 - cost, $"The stall paid {cost}g ({o.Treasury}g left).");
            Assert(o.Ledger.GetSinkTotal(WorkshopService.ConstructionSinkName) == cost && o.Ledger.TotalSinkedGold == cost, "The construction is a sink, named in the ledger.");
            Assert(o.Ledger.ValidateConservation(o.Treasury, 0, out int discrepancy) && discrepancy == 0, "The ledger balances: what the stall paid left the economy.");
            Assert(!o.Sim.Workshops[o.Still].Built, "Only the forge stands.");

            var events = new List<ChronicleEvent>();
            o.Chronicle.GetAllRecentEvents(events, 8);
            Assert(events.Count == 1 && events[0].EventType == ChronicleEventType.StationBuilt && events[0].SectorId == Floor1Layout.WorkshopRowSectorId
                && (StationKind)(byte)events[0].ExtraData == StationKind.Forge, "The chronicle tells of the forge built in the workshop row.");
            string told = new TemplateNarrativeFormatter(null, o.Sectors).FormatEvent(events[0]);
            Assert(told == "The forge was built in Workshop Row.", $"In words: '{told}'.");
            TestLog.Log($"[PASS] Test 1: Building the forge pays {cost}g into the Construction sink; the ledger balances; the chronicle tells of it.");
        }

        private static void ValidateBuildRefusals()
        {
            var o = NewOutpost(treasury: 250);
            var tuning = o.Sim.Economy.Workshops;
            Expect(o, 7, WorkshopRules.Check.NoStation, "no station 7 on Floor 1");
            Expect(o, o.Forge, WorkshopRules.Check.CannotPay, "250g can't pay 300g");
            Assert(WorkshopRules.Describe(WorkshopRules.Check.CannotPay, o.Sim.Workshops, o.Forge, tuning) == "The stall can't pay the 300g to build the forge.", "The refusal says why.");
            Assert(o.Treasury == 250 && o.Ledger.TotalSinkedGold == 0 && !o.Sim.Workshops[o.Forge].Built, "A refusal changes nothing.");

            var rich = NewOutpost(treasury: 1000);
            rich.Sim.Workshops.Build(rich.Sim, rich.Forge, 0);
            Expect(rich, rich.Forge, WorkshopRules.Check.AlreadyBuilt, "the forge already stands");
            Assert(rich.Treasury == 1000 - tuning.ConstructionCost, "Built once, paid once.");

            var noStall = new SimulationManager(new WorldRegistry(), null, sectors: Floor1Sectors());
            Assert(noStall.Workshops.Build(noStall, 0, 0) == WorkshopRules.Check.NoStall, "Without a stall nothing pays for it.");
            TestLog.Log("[PASS] Test 2: Refusals in order, with words: no station, already built, no stall, can't pay; none changes anything.");
        }

        private static void ValidateBuildCommandOnTheTick()
        {
            var o = NewOutpost(treasury: 1000);
            o.Commands.QueueInfluence(InfluenceEvent.CreateBuildStation("test", (byte)o.Still));
            o.Commands.QueueInfluence(InfluenceEvent.CreateBuildStation("test", (byte)o.Still));
            Assert(!o.Sim.Workshops[o.Still].Built, "Nothing happens before the tick.");
            o.Sim.Tick(1);
            Assert(o.Sim.Workshops[o.Still].Built && o.Treasury == 1000 - o.Sim.Economy.Workshops.ConstructionCost, "On the tick the still is built; the second command is refused, paying nothing.");
            TestLog.Log("[PASS] Test 3: BuildStation arrives as a typed command, applied on the tick in queue order.");
        }

        private static void ValidateHiring()
        {
            var o = NewOutpost(treasury: 1000);
            o.Sim.Workshops.Build(o.Sim, o.Forge, 0);
            var smith = Crawler(o, trust: 60, warmth: 40);
            uint before = o.Treasury;
            Assert(o.Sim.Workshops.Hire(o.Sim, smith.AgentId, o.Forge, 6, 100) == WorkshopRules.Check.Ok, "A cautious, trusted resident takes up work at the forge.");
            Assert(o.Treasury == before && smith.Inventory.Gold == 0, "No fee changes hands (D9).");
            Assert(o.Sim.Workshops.TryGetContract(smith.AgentId, out var contract) && contract.Wage == 6 && contract.StationIndex == o.Forge && contract.JoinTick == 100,
                "Its contract holds the wage, the station and the tick it was hired.");
            Assert(o.Sim.Workshops.ArtisanAt(o.Forge) == smith.AgentId && o.Sim.Workshops.ArtisanAt(o.Still) == default, "It works the forge; the still has nobody.");

            byte crew = o.Sim.Workshops[o.Forge].CrewGroupId;
            Assert(crew != 0 && o.Groups.TryGetGroup(crew, out var group) && group.Type == GroupType.Workshop && group.IsPatronLed
                && group.AnchorPosition == o.Sim.Workshops.DefinitionAt(o.Forge).Position, "The forge's crew is a Workshop group, led by the patron, anchored at the forge (D11).");
            Assert(o.Groups.TryGetMembership(smith.AgentId, crew, out var membership) && membership.Allegiance == o.Sim.Economy.Workshops.InitialAllegiance, "The artisan is in its crew.");
            Assert(smith.Memberships.Has(GroupType.Workshop), "Its own copy holds the crew.");
            Assert(o.Sim.Guild.Recruit(o.Sim, smith.AgentId, 5, 101) == Simpiens.Simulation.Guild.GuildRules.Check.WorksAStation, "The guild won't take it: one role at a time.");

            var events = new List<ChronicleEvent>();
            o.Chronicle.GetAllRecentEvents(events, 8);
            var hired = events.Find(e => e.EventType == ChronicleEventType.ArtisanHired);
            Assert(hired.ActorAId == smith.AgentId && hired.SectorId == Floor1Layout.WorkshopRowSectorId && (int)hired.ExtraData == 6, "The chronicle tells of the hire, in the workshop row, at its wage.");
            TestLog.Log("[PASS] Test 4: A cautious, trusted resident is hired into the forge's crew on a contract, with no fee (D9, D11).");
        }

        private static void ValidateHireRefusals()
        {
            var o = NewOutpost(treasury: 2000);
            var tuning = o.Sim.Economy.Workshops;
            var smith = Crawler(o, trust: 80, warmth: 80);
            ExpectHire(o, smith.AgentId, 9, 5, WorkshopRules.Check.NoStation, "no station 9");
            ExpectHire(o, smith.AgentId, o.Forge, 5, WorkshopRules.Check.NotBuilt, "the forge isn't built");
            o.Sim.Workshops.Build(o.Sim, o.Forge, 0);
            o.Sim.Workshops.Build(o.Sim, o.Still, 0);
            ExpectHire(o, EntityId.Generate(), o.Forge, 5, WorkshopRules.Check.NoCrawler, "nobody by that id");

            var drifter = Crawler(o, trust: 80, warmth: 80);
            drifter.DepartsAtTick = 5000;
            ExpectHire(o, drifter.AgentId, o.Forge, 5, WorkshopRules.Check.Drifter, "drifters don't take work");
            var downed = Crawler(o, trust: 80, warmth: 80);
            downed.IsDowned = true;
            ExpectHire(o, downed.AgentId, o.Forge, 5, WorkshopRules.Check.Downed, "a downed crawler doesn't");
            var member = Crawler(o, trust: 80, warmth: 80);
            Assert(o.Sim.Guild.Recruit(o.Sim, member.AgentId, 5, 0) == Simpiens.Simulation.Guild.GuildRules.Check.Ok, "Precondition: a guild member.");
            ExpectHire(o, member.AgentId, o.Forge, 5, WorkshopRules.Check.GuildMember, "one role at a time");

            var unsure = Crawler(o, trust: 45, warmth: 22);
            ExpectHire(o, unsure.AgentId, o.Forge, 5, WorkshopRules.Check.NotYetTrusted, "trust 45 is below 50");
            Assert(WorkshopRules.Describe(WorkshopRules.Check.NotYetTrusted, o.Sim.Workshops, o.Forge, tuning, "Maya", unsure) == "Not yet: trust 45 of 50, warmth 22 of 30.", "The refusal explains the threshold.");
            var bold = Crawler(o, trust: 80, warmth: 80, risk: 0.5f);
            ExpectHire(o, bold.AgentId, o.Forge, 5, WorkshopRules.Check.TooBold, "risk tolerance 50% is not under 30%");
            Assert(WorkshopRules.Describe(WorkshopRules.Check.TooBold, o.Sim.Workshops, o.Forge, tuning, "Maya", bold) == "Maya would rather go out into danger than work the forge.", "The refusal says why.");

            ExpectHire(o, smith.AgentId, o.Forge, 0, WorkshopRules.Check.WageOutOfRange, "a wage of 0");
            ExpectHire(o, smith.AgentId, o.Forge, (ushort)(tuning.MaxWage + 1), WorkshopRules.Check.WageOutOfRange, "a wage above the most");
            var greedy = Crawler(o, trust: 80, warmth: 80, greed: 1f);
            ushort asks = WorkshopRules.ReservationWage(greedy, tuning);
            Assert(asks > tuning.ReservationWage, $"A greedy crawler asks more than {tuning.ReservationWage}g ({asks}g).");
            ExpectHire(o, greedy.AgentId, o.Forge, (ushort)(asks - 1), WorkshopRules.Check.WageTooLow, "a wage below its reservation");

            Assert(o.Sim.Workshops.Hire(o.Sim, smith.AgentId, o.Forge, 5, 10) == WorkshopRules.Check.Ok, "Precondition: the forge's artisan.");
            var second = Crawler(o, trust: 80, warmth: 80);
            ExpectHire(o, second.AgentId, o.Forge, 5, WorkshopRules.Check.Staffed, "one artisan to a station (Q3)");
            ExpectHire(o, smith.AgentId, o.Still, 5, WorkshopRules.Check.AlreadyAnArtisan, "it already works the forge");

            Assert(o.Sim.Workshops.Dismiss(o.Sim, smith.AgentId, 20) == WorkshopRules.Check.Ok, "Let go.");
            ExpectHire(o, smith.AgentId, o.Still, 5, WorkshopRules.Check.CoolingDown, "let go too recently");
            Assert(o.Sim.Workshops.Hire(o.Sim, smith.AgentId, o.Still, 5, 20 + tuning.RehireCooldownTicks) == WorkshopRules.Check.Ok, "After the cooldown it can be hired again.");
            TestLog.Log("[PASS] Test 5: Hiring refusals in order, with words: station, crawler, roles, trust, boldness, wage, staffed, cooldown; none changes anything.");
        }

        private static void ValidateWagesAfterTheGuildsAndQuitting()
        {
            var o = NewOutpost(treasury: 1000);
            var tuning = o.Sim.Economy.Workshops;
            o.Sim.Workshops.Build(o.Sim, o.Forge, 0);
            var member = Crawler(o, trust: 80, warmth: 80);
            var smith = Crawler(o, trust: 60, warmth: 40);
            o.Sim.Guild.Recruit(o.Sim, member.AgentId, 5, 0);
            o.Sim.Workshops.Hire(o.Sim, smith.AgentId, o.Forge, 7, 0);
            var supply = (ISupplyHost)o.Sim;
            int trustBefore = smith.Memory.GetAffinity(WorldRegistry.PlayerEntityId, 600).Trust;

            uint treasury = o.Treasury;
            supply.PayWages(o.Sim.ActiveStalls[0], 600);
            Assert(o.Treasury == treasury - 5 - 7 && member.Inventory.Gold == o.Sim.Economy.Guild.SigningFee + 5 && smith.Inventory.Gold == 7, "The member's wage, then the artisan's, from the stall.");
            Assert(o.Ledger.Flows.ArtisanWages == 1 && o.Ledger.Flows.ArtisanWageGold == 7 && o.Ledger.Flows.WageGold == 5, "The artisan's wage is counted on its own.");
            Assert(o.Ledger.ValidateConservation(o.Treasury, (uint)(member.Inventory.Gold + smith.Inventory.Gold), out _), "Wages are transfers: the ledger balances.");
            Assert(smith.Memory.GetAffinity(WorldRegistry.PlayerEntityId, 600).Trust > trustBefore, "A paid artisan's trust rises.");

            // A treasury that covers the member's wage but not the artisan's: the guild is paid first (D9)
            var poor = o.Sim.ActiveStalls[0].WithTreasury(9);
            o.Sim.UpdateStall(poor);
            supply.PayWages(o.Sim.ActiveStalls[0], 1200);
            Assert(o.Treasury == 4 && o.Sim.Workshops.TryGetContract(smith.AgentId, out var contract) && contract.UnpaidVisits == 1, "The member was paid; the artisan went unpaid, once.");
            supply.PayWages(o.Sim.ActiveStalls[0], 1800);
            Assert(!o.Sim.Workshops.IsArtisan(smith.AgentId) && !smith.Memberships.Has(GroupType.Workshop), $"After {tuning.MaxUnpaidVisits} unpaid visits in a row the artisan quits, leaving its crew.");
            Assert(o.Sim.Workshops.CooldownUntil(smith.AgentId) == 1800 + tuning.RehireCooldownTicks && o.Sim.Workshops.ArtisanAt(o.Forge) == default, "It can't be hired again for a while; the forge has nobody.");
            Assert(o.Groups.TryGetGroup(o.Sim.Workshops[o.Forge].CrewGroupId, out _), "The crew's group stays while the forge stands.");

            var events = new List<ChronicleEvent>();
            o.Chronicle.GetAllRecentEvents(events, 16);
            Assert(events.Exists(e => e.EventType == ChronicleEventType.ArtisanUnpaid) && events.Exists(e => e.EventType == ChronicleEventType.ArtisanQuit && e.ActorAId == smith.AgentId),
                "The chronicle tells of the unpaid visit and the quitting.");
            TestLog.Log("[PASS] Test 6: Wages after the guild's, counted on their own and conserved; an unpaid artisan quits, and the crew stays.");
        }

        private static void ValidateCommandsReachAnArtisanByWhoItIs()
        {
            var o = NewOutpost(treasury: 1000);
            o.Sim.Workshops.Build(o.Sim, o.Still, 0);
            var brewer = Crawler(o, trust: 60, warmth: 40);
            var member = Crawler(o, trust: 80, warmth: 80);
            o.Sim.Guild.Recruit(o.Sim, member.AgentId, 5, 0);
            o.Commands.QueueInfluence(InfluenceEvent.CreateHireArtisan("test", brewer.AgentId, (byte)o.Still, 5));
            o.Commands.QueueInfluence(InfluenceEvent.CreateSetWage("test", brewer.AgentId, 9));
            o.Commands.QueueInfluence(InfluenceEvent.CreateSetWage("test", member.AgentId, 8));
            Assert(!o.Sim.Workshops.IsArtisan(brewer.AgentId), "Nothing happens before the tick.");
            o.Sim.Tick(1);
            Assert(o.Sim.Workshops.TryGetContract(brewer.AgentId, out var contract) && contract.Wage == 9, "On the tick: hired, then the artisan's wage set.");
            Assert(o.Sim.Guild.TryGetContract(member.AgentId, out var memberContract) && memberContract.Wage == 8, "SetWage still reaches a member.");

            int trust = brewer.Memory.GetAffinity(WorldRegistry.PlayerEntityId, 2).Trust;
            o.Commands.QueueInfluence(InfluenceEvent.CreateDismiss("test", brewer.AgentId));
            o.Sim.Tick(2);
            Assert(!o.Sim.Workshops.IsArtisan(brewer.AgentId) && o.Sim.Guild.IsMember(member.AgentId), "Dismiss lets the artisan go, and only it.");
            Assert(brewer.Memory.GetAffinity(WorldRegistry.PlayerEntityId, 2).Trust == trust + o.Sim.Economy.Workshops.DismissTrust, "Being let go costs trust.");
            TestLog.Log("[PASS] Test 7: HireArtisan arrives as a typed command; Dismiss and SetWage reach an artisan or a member by who it is.");
        }

        // W1.3a (D12, D32): the player orders makes at a station and sets an artisan's blocks of the day, as typed commands
        // applied on the tick; each refusal comes with its words and changes nothing
        private static void ValidateOrdersAndShiftsAsCommands()
        {
            var o = NewOutpost(treasury: 1000);
            var tuning = o.Sim.Economy.Workshops;
            var workshops = o.Sim.Workshops;
            Assert(WorkshopRules.CheckOrder(workshops, o.Forge, ItemType.ToolPickaxe, 2) == WorkshopRules.Check.NotBuilt, "An unbuilt forge takes no order.");
            workshops.Build(o.Sim, o.Forge, 0);
            Assert(WorkshopRules.CheckOrder(workshops, 7, ItemType.ToolPickaxe, 2) == WorkshopRules.Check.NoStation, "No station 7.");
            Assert(WorkshopRules.CheckOrder(workshops, o.Forge, ItemType.Ration, 2) == WorkshopRules.Check.NotThisStationsRecipe
                && WorkshopRules.Describe(WorkshopRules.Check.NotThisStationsRecipe, workshops, o.Forge, tuning) == "That isn't made at the forge.", "A ration isn't the forge's to make, and the refusal says so.");

            o.Commands.QueueInfluence(InfluenceEvent.CreateOrderStation("test", (byte)o.Forge, ItemType.ToolPickaxe, 3));
            o.Commands.QueueInfluence(InfluenceEvent.CreateOrderStation("test", (byte)o.Forge, ItemType.Ration, 1));
            Assert(!workshops[o.Forge].HasOrder, "Nothing happens before the tick.");
            o.Sim.Tick(1);
            Assert(workshops[o.Forge].Order == ItemType.ToolPickaxe && workshops[o.Forge].Remaining == 3, "On the tick the forge is ordered three pickaxes; the refused order changes nothing.");
            o.Commands.QueueInfluence(InfluenceEvent.CreateOrderStation("test", (byte)o.Forge, ItemType.None, 0));
            o.Sim.Tick(2);
            Assert(!workshops[o.Forge].HasOrder, "An order for nothing cancels.");

            var smith = Crawler(o, trust: 60, warmth: 40);
            workshops.Hire(o.Sim, smith.AgentId, o.Forge, 5, 2);
            Assert(workshops.TryGetContract(smith.AgentId, out var hired) && hired.Shifts == 0 && !hired.OnBreak, "A new hire works no block until the player sets its shifts (D12).");
            Assert(WorkshopRules.CheckSetShifts(workshops, EntityId.Generate(), ShiftBlocks.DayShift) == WorkshopRules.Check.NotAnArtisan, "Shifts are an artisan's.");
            Assert(WorkshopRules.CheckSetShifts(workshops, smith.AgentId, 0b1000000) == WorkshopRules.Check.NoSuchBlock
                && WorkshopRules.Describe(WorkshopRules.Check.NoSuchBlock, workshops, o.Forge, tuning) == "A day has six blocks of four hours, from 06:00.", "A day has six blocks.");
            o.Commands.QueueInfluence(InfluenceEvent.CreateSetShifts("test", smith.AgentId, ShiftBlocks.DayShift));
            o.Sim.Tick(3);
            Assert(workshops.TryGetContract(smith.AgentId, out var shifted) && shifted.Shifts == ShiftBlocks.DayShift && shifted.Wage == 5, "On the tick the smith works the day shift; the rest of its contract holds.");
            TestLog.Log("[PASS] Test 8: OrderStation and SetShifts arrive as typed commands; refusals come in words and change nothing; a new hire works no block.");
        }

        // D12: six blocks of four game hours from 06:00; a mask of them; runs counted around the day's turn
        private static void ValidateShiftBlocks()
        {
            Assert(ShiftBlocks.TicksEach == 4 * DayClock.TicksPerHour, "A block is four game hours.");
            Assert(ShiftBlocks.At(0) == 0 && ShiftBlocks.At(ShiftBlocks.TicksEach - 1) == 0 && ShiftBlocks.At(ShiftBlocks.TicksEach) == 1, "Block 0 runs from 06:00 to 10:00.");
            Assert(ShiftBlocks.At(DayClock.TicksPerDay - 1) == 5 && ShiftBlocks.At(DayClock.TicksPerDay) == 0, "Block 5, 02:00 to 06:00, ends the day; the next day starts block 0.");
            Assert(ShiftBlocks.StartHour(1) == 10 && ShiftBlocks.StartHour(4) == 22 && ShiftBlocks.StartHour(5) == 2, "The blocks start at 06, 10, 14, 18, 22 and 02.");
            long tenOClock = ShiftBlocks.TicksEach, sixInTheEvening = 3 * ShiftBlocks.TicksEach;
            Assert(ShiftBlocks.Covers(ShiftBlocks.DayShift, tenOClock) && ShiftBlocks.Covers(ShiftBlocks.DayShift, sixInTheEvening - 1) && !ShiftBlocks.Covers(ShiftBlocks.DayShift, sixInTheEvening)
                && !ShiftBlocks.Covers(ShiftBlocks.DayShift, 0), "The day shift covers 10:00 to 18:00.");
            Assert(ShiftBlocks.CountOf(ShiftBlocks.DayShift) == 2 && ShiftBlocks.LongestRun(ShiftBlocks.DayShift) == 2, "The day shift is two blocks in a row.");
            Assert(ShiftBlocks.LongestRun(0b100001) == 2 && ShiftBlocks.LongestRun(0b010101) == 1 && ShiftBlocks.LongestRun(0b000111) == 3 && ShiftBlocks.LongestRun(ShiftBlocks.All) == 6,
                "Runs count around the day's turn: 02-06 runs on into 06-10.");
            TestLog.Log("[PASS] Test 9: Six blocks of four game hours from 06:00; the day shift is 10:00 to 18:00; runs wrap the day's turn.");
        }

        private static Simpiens.Cognition.AgentState Crawler(Outpost o, sbyte trust, sbyte warmth, float risk = 0.1f, float greed = 0f)
        {
            var crawler = HeadlessEntities.Crawler(o.World, o.Sim, null, Vec2.Zero, id: EntityId.Generate());
            crawler.Agent.Inventory.Gold = 0;
            crawler.Agent.Personality = new AgentPersonality(greed, risk, 0f, 0f, 0f);
            crawler.Agent.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth, trust, 0, 0, 1, 0));
            return crawler.Agent;
        }

        private static void ExpectHire(Outpost o, EntityId crawlerId, int index, ushort wage, WorkshopRules.Check expected, string what)
        {
            int artisans = o.Sim.Workshops.Artisans.Count;
            var check = o.Sim.Workshops.Hire(o.Sim, crawlerId, index, wage, 0);
            Assert(check == expected, $"Refused ({what}): expected {expected}, got {check}.");
            Assert(o.Sim.Workshops.Artisans.Count == artisans, $"A refusal ({what}) hires nobody.");
        }

        private static Outpost NewOutpost(uint treasury)
        {
            var o = new Outpost { World = new WorldRegistry(), Groups = new GroupRegistry(), Ledger = new EconomyLedger(), Commands = new PlayerInterventionService(), Chronicle = new NarrativeChronicleService(), Sectors = Floor1Sectors() };
            o.Sim = new SimulationManager(o.World, null, groupRegistry: o.Groups, chronicleService: o.Chronicle, sectors: o.Sectors, interventionService: o.Commands, economyLedger: o.Ledger);
            o.Sim.RegisterTradeStall(TestStalls.Stall(EntityId.Generate(), Vec2.Zero, treasuryGold: treasury));
            o.Ledger.RecordFaucet(treasury, "InitialSeed");
            o.Forge = o.Sim.Workshops.IndexOf(StationKind.Forge);
            o.Still = o.Sim.Workshops.IndexOf(StationKind.Still);
            Assert(o.Forge >= 0 && o.Still >= 0, "Floor 1's forge and still are the simulation's stations.");
            return o;
        }

        private static SectorRegistry Floor1Sectors()
        {
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            return sectors;
        }

        private static void Expect(Outpost o, int index, WorkshopRules.Check expected, string what)
        {
            var check = o.Sim.Workshops.Build(o.Sim, index, 0);
            Assert(check == expected, $"Refused ({what}): expected {expected}, got {check}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[WorkshopValidator ASSERTION FAILED] {message}");
        }
    }
}
