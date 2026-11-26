using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Items;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// A spell of free time (Epic LIFE-1 plan 2, L2.5, D14), with the intent forced on the outpost's map: a walk in the Garden
    /// lasts its 45 game minutes, strolls from spot to spot, eases boredom at its row's rate and leaves its thought; browsing
    /// the stall its 30; a crawler out of reach with no path, or sent to a seat or venue that isn't there, is refused. (A blow,
    /// starvation or an order cuts a spell short as it cuts any intent: the runtime's, not the handler's.) Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class RecreateValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[RecreateValidator] Beginning recreation validation...");
            ValidateAWalk();
            ValidateABrowse();
            ValidateTheRefusals();
            ValidateASupperAtTheTavern();
            TestLog.Log("<color=green>[RecreateValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateAWalk()
        {
            var (sim, registry) = Outpost();
            int garden = Venue(Floor1Layout.GardenVenue);
            var seat = Floor1Layout.Definition.Venues[garden].Seats[0];
            var crawler = HeadlessEntities.Crawler(registry, sim, null, seat, id: EntityId.Generate());
            Bore(crawler.Agent, 50f);
            var state = new ActiveIntentState { Intent = new RecreateIntent(crawler.Node.Id, garden, Activity.Walk, 0, null), CurrentWaypointIndex = 0, LastSampledPosition = seat };
            int ticks = 45 * DayClock.TicksPerMinute;
            var result = sim.RunToCompletion(crawler.Node, state, ticks + 5);
            Assert(result == IntentResult.Success, $"A walk ends ({result}).");
            Assert(Math.Abs(crawler.Agent.Life.Boredom - (50f - 15f * 0.75f)) < 0.1f, $"45 game minutes at 15 an hour ease 11.25 ({crawler.Agent.Life.Boredom:F2}).");
            Assert(ThoughtRules.Holds(crawler.Agent.Life, Thought.AWalkInTheGarden, sim.CurrentTick), "It leaves its thought.");
            Assert(Vec2.Distance(crawler.Node.Position, seat) > 1f, $"It strolled from its first spot ({crawler.Node.Position}).");
            Assert(crawler.Agent.VisualState == Simpiens.Entities.AgentVisualState.Idle, "It stands down.");
        }

        private void ValidateABrowse()
        {
            var (sim, registry) = Outpost();
            int stall = Venue(Floor1Layout.StallVenue);
            var seat = Floor1Layout.Definition.Venues[stall].Seats[2];
            var crawler = HeadlessEntities.Crawler(registry, sim, null, seat, id: EntityId.Generate());
            Bore(crawler.Agent, 20f);
            var state = new ActiveIntentState { Intent = new RecreateIntent(crawler.Node.Id, stall, Activity.Browse, 2, null), CurrentWaypointIndex = 0, LastSampledPosition = seat };
            int ticks = 0;
            var result = IntentResult.InProgress;
            while (result == IntentResult.InProgress && ticks < 400) { result = sim.ExecuteIntent(crawler.Node, state); ticks++; }
            Assert(result == IntentResult.Success && ticks == 30 * DayClock.TicksPerMinute, $"Browsing lasts 30 game minutes ({ticks} ticks).");
            Assert(Math.Abs(crawler.Agent.Life.Boredom - 15f) < 0.1f, $"At 10 an hour, half an hour eases 5 ({crawler.Agent.Life.Boredom:F2}).");
            Assert(ThoughtRules.Holds(crawler.Agent.Life, Thought.BrowsedTheMarket, sim.CurrentTick), "It leaves its thought.");
            Assert(Vec2.Distance(crawler.Node.Position, seat) < 0.01f, "Browsing, it stays at its spot.");
        }

        private void ValidateTheRefusals()
        {
            var (sim, registry) = Outpost();
            int garden = Venue(Floor1Layout.GardenVenue);
            var far = HeadlessEntities.Crawler(registry, sim, null, new Vec2(-4f, -9f), id: EntityId.Generate());
            Assert(Run(sim, far, new RecreateIntent(far.Node.Id, garden, Activity.Walk, 0, null)) == IntentResult.PathBlocked, "Out of reach with no path: blocked.");
            Assert(Run(sim, far, new RecreateIntent(far.Node.Id, garden, Activity.Walk, 9, null)) == IntentResult.TargetMissing, "No such seat.");
            Assert(Run(sim, far, new RecreateIntent(far.Node.Id, 31, Activity.Walk, 0, null)) == IntentResult.TargetMissing, "No such venue.");
            Assert(Run(sim, far, new RecreateIntent(far.Node.Id, Venue(Floor1Layout.HearthVenue), Activity.Fireside, 0, null)) == IntentResult.TargetMissing, "The fireside isn't Recreate's.");
        }

        /// <summary>
        /// A supper at the tavern (plan 2's T.5, D19): its stew bought from the stall's stock as it begins, 10g to the treasury
        /// through the ledger, one off the shelf, its meal eaten, its thought thought, told as served; with none in stock, or a
        /// purse short of its price, refused with nothing changed.
        /// </summary>
        private void ValidateASupperAtTheTavern()
        {
            var registry = new WorldRegistry();
            var ledger = new EconomyLedger();
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            var sim = new SimulationManager(registry, null, sectors: sectors, economyLedger: ledger);
            sim.SetEconomy(EconomyTuning.Default);
            var goods = new System.Collections.Generic.List<StallGood>(TradeStallData.GoodsCapacity);
            EconomyTuning.Default.StallGoods(goods);
            var stall = new TradeStallData(EntityId.Generate(), Vec2.Zero, 100, 3f);
            foreach (var good in goods) stall = stall.WithGood(good);
            sim.RegisterTradeStall(stall.WithStock(ItemType.Stew, 1));
            ledger.RecordFaucet(100, "InitialSeed");
            int served = 0;
            uint takings = 0;
            sim.Served += (who, item, price) => { served++; takings += price; };

            int tavern = Venue(Floor1Layout.TavernVenue);
            var seat = Floor1Layout.Definition.Venues[tavern].Seats[0];
            var crawler = HeadlessEntities.Crawler(registry, sim, null, seat, id: EntityId.Generate());
            crawler.Agent.Inventory.Gold = 25;
            crawler.Agent.Hunger = 50f;
            ledger.RecordFaucet(25, "InitialSeed");
            var state = new ActiveIntentState { Intent = new RecreateIntent(crawler.Node.Id, tavern, Activity.Supper, 0, null), CurrentWaypointIndex = 0, LastSampledPosition = seat };
            var result = sim.RunToCompletion(crawler.Node, state, 45 * DayClock.TicksPerMinute + 5);
            var after = sim.ActiveStalls[0];
            Assert(result == IntentResult.Success, $"A supper ends ({result}).");
            Assert(crawler.Agent.Inventory.Gold == 15 && after.TreasuryGold == 110 && after.SellStockOf(ItemType.Stew) == 0, "10g to the treasury, the stew off the shelf.");
            Assert(Math.Abs(crawler.Agent.Hunger - 10f) < 0.01f && ThoughtRules.Holds(crawler.Agent.Life, Thought.ASupperAtTheTavern, sim.CurrentTick), "Its meal eaten, its thought thought.");
            Assert(crawler.Agent.Inventory.CountOf(ItemCategory.Served) == 0, "Served and taken there: nothing in the pack.");
            Assert(served == 1 && takings == 10, $"Told as served at its price ({served}, {takings}g).");
            Assert(ledger.ValidateConservation(after.TreasuryGold + (uint)crawler.Agent.Inventory.Gold, 0, out int discrepancy), $"The ledger balances ({discrepancy}g).");

            var again = new ActiveIntentState { Intent = new RecreateIntent(crawler.Node.Id, tavern, Activity.Supper, 0, null), CurrentWaypointIndex = 0, LastSampledPosition = seat };
            Assert(sim.ExecuteIntent(crawler.Node, again) == IntentResult.Failed && crawler.Agent.Inventory.Gold == 15, "None left: refused, nothing paid.");
            sim.UpdateStall(sim.ActiveStalls[0].WithStock(ItemType.Stew, 3));
            crawler.Agent.Inventory.Gold = 5;
            var poor = new ActiveIntentState { Intent = new RecreateIntent(crawler.Node.Id, tavern, Activity.Supper, 0, null), CurrentWaypointIndex = 0, LastSampledPosition = seat };
            Assert(sim.ExecuteIntent(crawler.Node, poor) == IntentResult.Failed && sim.ActiveStalls[0].SellStockOf(ItemType.Stew) == 3, "Short of its price: refused, the stock untouched.");
        }

        private static IntentResult Run(SimulationManager sim, TestCrawler crawler, RecreateIntent intent)
            => sim.ExecuteIntent(crawler.Node, new ActiveIntentState { Intent = intent, CurrentWaypointIndex = 0, LastSampledPosition = crawler.Node.Position });

        private static void Bore(AgentState agent, float boredom)
        {
            var life = agent.Life;
            life.Bore(boredom);
            agent.Life = life;
        }

        private static (SimulationManager, WorldRegistry) Outpost()
        {
            var registry = new WorldRegistry();
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            var sim = new SimulationManager(registry, null, sectors: sectors);
            sim.SetEconomy(EconomyTuning.Default);
            return (sim, registry);
        }

        private static int Venue(string key)
        {
            var venues = Floor1Layout.Definition.Venues;
            for (int i = 0; i < venues.Count; i++) if (venues[i].Key == key) return i;
            throw new InvalidOperationException($"[RecreateValidator] FAILED: no venue {key}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[RecreateValidator] FAILED: " + message);
        }
    }
}
