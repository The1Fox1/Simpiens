using System;
using System.Collections.Generic;
using Simpiens.Core.Session.Layouts;
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
    /// pays the construction from the stall into the <c>Construction</c> sink, conserved by the ledger; each refusal
    /// comes in order, with its words, and changes nothing; the command applies on the tick and the chronicle tells of
    /// it. Runs as a test (ValidatorTests), never at boot.
    /// </summary>
    public class WorkshopValidator
    {
        private sealed class Outpost
        {
            public WorldRegistry World;
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

            TestLog.Log("<color=green>[WorkshopValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
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

        private static Outpost NewOutpost(uint treasury)
        {
            var o = new Outpost { World = new WorldRegistry(), Ledger = new EconomyLedger(), Commands = new PlayerInterventionService(), Chronicle = new NarrativeChronicleService(), Sectors = Floor1Sectors() };
            o.Sim = new SimulationManager(o.World, null, chronicleService: o.Chronicle, sectors: o.Sectors, interventionService: o.Commands, economyLedger: o.Ledger);
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
