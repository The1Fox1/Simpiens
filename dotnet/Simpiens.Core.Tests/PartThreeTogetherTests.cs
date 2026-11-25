using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// K11 (Epic 10 Part 3): the whole part played together, and saved at its hardest moments.
    /// <list type="bullet">
    /// <item><b>Every scenario together</b> (K11.1a): every scripted player at once on one seed, through commands: the forge
    /// making blades and ring coats, the still draughts, the hunt player posting on a lair the bold would brave, the player
    /// charting the Old Stair, and the guild clearing the web and breaking the Gatekeeper. Kills, a hunt paid, the breach,
    /// no death, the days held, the ledger balanced.</item>
    /// <item><b>Saves at the hard moments</b> (K11.1b): mid-fight at the gate, with a crawler bleeding out, and with one being
    /// carried, each loaded into a fresh session byte for byte and to the same world hash, then played on to its end. Work in
    /// progress isn't saved (crawlers decide afresh after a load), so what is compared is the world at the save's tick.</item>
    /// </list>
    /// </summary>
    public class PartThreeTogetherTests
    {
        private const uint Reserve = 150;

        // Every player at once (K11.1a): the gate's players (GateScenarioTests) and the hunt player
        private static GuildPlayer Players(LocalSimulation sim, bool hunts)
        {
            new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4, reserve: Reserve).Also(ItemType.RingCoat, keep: 4);
            new WorkshopPlayer(sim, StationKind.Still, ItemType.HealingDraught, batch: 2, reserve: Reserve);
            if (hunts) new HuntPlayer(sim, CreatureKind.None);
            var guild = new GuildPlayer(sim, targetSize: 4, reserve: Reserve);
            guild.SendClears(new[] { CreatureKind.CaveSpinner, CreatureKind.Gatekeeper }, RetreatRule.Steady, draughts: 2, stockpile: 8);
            var commands = (Simpiens.Intervention.IPlayerInterventionService)sim.Services.GetService(typeof(Simpiens.Intervention.IPlayerInterventionService));
            commands.QueueInfluence(Simpiens.Intervention.InfluenceEvent.CreatePostBounty("Test", new Simpiens.Intervention.PostBountyCommand(0, BountyType.ScoutSector,
                Floor1Layout.OldStairSectorId, ItemType.None, 1, 40, durationTicks: DayClock.Hours(48))));
            return guild;
        }

        /// <summary>
        /// Every scenario's seed (the default, measured with K11.1a: see the epic's K11 entry), and the bleeding saves'. Every
        /// scenario's is gate seed 424242 since Epic LIFE-1's Recreate (plan 2, L2.6, `docs/life-model.md` §5.8): free time moved the outpost's history, and on the default not every scenario came about within its days.
        /// Together, seed 8 since Epic LIFE-1 plan 3's rhythm (nights, routines, weights by the hour and days off, R.2-R.9) moved the outpost's history, and seed 424242's moment no longer came.
        /// Bleeding, seed 1 since Epic LIFE-1 plan 3's rhythm (nights, routines, weights by the hour and days off, R.2-R.9) moved the outpost's history, and seed 3's moment no longer came.
        /// Mid-fight, seed 424242 since Epic LIFE-1 plan 3's rhythm (nights, routines, weights by the hour and days off, R.2-R.9) moved the outpost's history, and the default seed (20261009)'s moment no longer came.
        /// </summary>
        private static readonly ulong TogetherSeed = ScenarioSeed.Or(8), BleedingSeed = ScenarioSeed.Or(1), MidFightSeed = ScenarioSeed.Or(424242);

        /// <summary>The default seed (measured with K11.1a: see the epic's K11 entry).</summary>
        [Fact]
        public void EveryScenarioPlaysTogether()
        {
            using var sim = LocalSimulation.Create(TogetherSeed);
            sim.Respawn(OutpostSession.SessionId);
            Players(sim, hunts: true);
            var watch = new OutpostWatch(sim);
            var told = new List<ChronicleEvent>(512);
            int huntsPaid = 0, huntsOpen = 0;
            long breached = -1;
            while (sim.Clock.CurrentTick < 16 * DayClock.TicksPerDay && breached < 0)
            {
                watch.Run(1);
                // A hunt paid: the hunts' list emptied with a bounty fulfilled on the tick (as HuntScenarioTests judges)
                int open = sim.Simulation.Hunts.Count;
                if (huntsOpen > 0 && open < huntsOpen)
                {
                    told.Clear();
                    sim.Chronicle.GetAllRecentEvents(told, 64);
                    if (told.Any(e => e.SimulationTick == sim.Clock.CurrentTick && e.EventType == ChronicleEventType.BountyFulfilled)) huntsPaid++;
                }
                huntsOpen = open;
                if ((sim.Simulation.WorldFlags & WorldFlags.GateBreached) != 0) breached = sim.Clock.CurrentTick;
            }
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine($"breached t{breached}, kills {watch.Kills}, hunts paid {huntsPaid}, deaths {watch.Deaths}\n{watch.CreatureSummary()}\n{watch.Report()}");
            Assert.True(breached > 0, "The guild broke the Gatekeeper.");
            Assert.True(watch.Kills >= 5, $"Kills: {watch.Kills}.");
            Assert.True(huntsPaid >= 1, "A hunt was posted, joined and paid.");
            Assert.Equal(0, watch.Deaths);
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
            var failures = watch.DayFailures();
            Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
        }

        /// <summary>K11.1b: saved mid-fight at the gate (the party at the fight, the keeper after one of them), loaded and played on until the fight is over.</summary>
        [Fact]
        public void ASaveMidFightLoadsByteForByteAndPlaysOn()
        {
            using var played = LocalSimulation.Create(MidFightSeed);
            played.Respawn(OutpostSession.SessionId);
            Players(played, hunts: false);
            var party = played.Simulation.Guild.Parties[0];
            var creatures = played.Simulation.Creatures;
            int gate = Enumerable.Range(0, creatures.Lairs.Count).First(l => creatures.Lairs[l].Definition.GuardsGate);
            bool MidFight() => party.IsOut && party.Expedition.IsClear && party.Expedition.LairIndex == gate && party.Expedition.Stage == ExpeditionStage.Fight
                && creatures.TryGet(creatures.Lairs[gate].MemberIn(0), out var k) && k.Mode == CreatureMode.Chase && k.Health < k.Definition.MaxHealth;
            while (played.Clock.CurrentTick < 16 * DayClock.TicksPerDay && !MidFight()) played.Step(1);
            Assert.True(MidFight(), "The guild came to blows at the gate.");
            var loaded = SaveAndLoad(played, MidFightSeed, out long tick);
            using (loaded)
            {
                var lp = loaded.Simulation.Guild.Parties[0];
                Assert.True(lp.IsOut && lp.Expedition.Stage == ExpeditionStage.Fight, "The party came back at the fight.");
                Assert.True(loaded.Simulation.Creatures.TryGet(creatures.Lairs[gate].MemberIn(0), out var keeper) && keeper.Mode == CreatureMode.Chase && keeper.LastVisitAt > 0, "The keeper came back after its quarry, its clock with it.");
                Players(loaded, hunts: false);
                var watch = new OutpostWatch(loaded);
                while (loaded.Clock.CurrentTick < tick + DayClock.TicksPerDay && lp.IsOut && lp.Expedition.Stage == ExpeditionStage.Fight) watch.Run(1);
                watch.Finish();
                Assert.False(lp.IsOut && lp.Expedition.Stage == ExpeditionStage.Fight, "The fight came to its end.");
                Assert.Equal(0, watch.ConservationViolations);
                Assert.Equal(0, watch.IntentFaults);
            }
        }

        /// <summary>
        /// K11.1b: saved with a crawler bleeding out in danger, and later with one being carried out (K7's carry, gate seed 6),
        /// each loaded and played on: the fallen is carried out, or its clock runs out, as it would have. Gate seed 3 since Epic LIFE-1's Recreate (plan 2, L2.6, `docs/life-model.md` §5.8): free time moved the outpost's history,
        /// and on seed 6 nobody fell (the carry's seed moved with it); the load now follows the scenario's seed.
        /// </summary>
        [Fact]
        public void SavesWithACrawlerBleedingAndOneCarriedLoadByteForByte()
        {
            using var played = LocalSimulation.Create(BleedingSeed);
            played.Respawn(OutpostSession.SessionId);
            new WorkshopPlayer(played, StationKind.Forge, ItemType.IronBlade, batch: 4);
            new GuildPlayer(played, targetSize: 4).SendClear(CreatureKind.None, RetreatRule.Steady, arm: true);
            var states = played.Host.CrawlerStates;

            // A crawler collapsed in danger, its clock running
            while (played.Clock.CurrentTick < 8 * DayClock.TicksPerDay && !states.Any(c => c.IsCollapsed)) played.Step(1);
            var fallen = states.FirstOrDefault(c => c.IsCollapsed);
            Assert.True(fallen != null, "A crawler fell in danger.");
            uint bleeds = fallen.BleedsOutAt;
            var loaded = SaveAndLoad(played, BleedingSeed, out long tick);
            using (loaded)
            {
                var again = loaded.Host.CrawlerStates.First(c => c.AgentId == fallen.AgentId);
                Assert.True(again.IsCollapsed && again.BleedsOutAt == bleeds, "It came back collapsed, its clock as it was.");
                var watch = new OutpostWatch(loaded);
                while (loaded.Clock.CurrentTick <= bleeds + 1 && again.IsCollapsed && !again.HasLeft) watch.Run(1);
                watch.Finish();
                Assert.True(!again.IsCollapsed || again.HasLeft, "Carried out or stood up, or its clock ran out: it didn't lie on past it.");
                Assert.Equal(0, watch.ConservationViolations);
            }

            // Later, one being carried
            while (played.Clock.CurrentTick < 8 * DayClock.TicksPerDay && !states.Any(c => c.CarriedBy != default)) played.Step(1);
            var carried = states.FirstOrDefault(c => c.CarriedBy != default);
            Assert.True(carried != null, "A crawler was carried.");
            loaded = SaveAndLoad(played, BleedingSeed, out tick);
            using (loaded)
            {
                var again = loaded.Host.CrawlerStates.First(c => c.AgentId == carried.AgentId);
                var watch = new OutpostWatch(loaded);
                while (loaded.Clock.CurrentTick < tick + DayClock.TicksPerDay && (again.IsCollapsed || again.CarriedBy != default) && !again.HasLeft) watch.Run(1);
                watch.Finish();
                Assert.False(again.CarriedBy != default, "Set down again.");
                Assert.Equal(0, watch.ConservationViolations);
                Assert.Equal(0, watch.IntentFaults);
            }
        }

        // The session saved now, loaded into a fresh one of the same seed: the save again byte for byte, the world hash the same
        private static LocalSimulation SaveAndLoad(LocalSimulation played, ulong seed, out long tick)
        {
            tick = played.Clock.CurrentTick;
            byte[] saved = SaveSerializer.Serialize(played.Host.Capture());
            var hash = WorldStateHash.Capture(tick, played.Host, played.Simulation, played.Sectors, includeWork: false);
            var loaded = LocalSimulation.Create(seed);
            loaded.Respawn(OutpostSession.SessionId);
            Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Null(hash.DescribeDifference(WorldStateHash.Capture(tick, loaded.Host, loaded.Simulation, loaded.Sectors, includeWork: false)));
            return loaded;
        }
    }
}
