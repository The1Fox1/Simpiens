using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// What a lair holds and who knows of it (Epic 10 Part 3, K2.3a; the epic's D16–D18), on the real outpost: its reach is
    /// its kind's leash around its heart; its danger is its pack's expected harm a second, past what anyone braves alone; a
    /// crawler who sees one of its creatures in a charted sector charts it for everyone, and one struck by one charts it even
    /// in the fog; the snapshot carries the chart to cognition, and a lone crawler would not go into a charted lair's reach.
    /// Mechanism tests, with crawlers put and held by hand and hunting switched on (it goes live with K2.3d).
    /// </summary>
    public class LairKnowledgeTests
    {
        private static readonly Vec2 NearTheWarren = new Vec2(20f, 14f); // overgrowth.3, 2.2 from the warren's heart

        // West of the warren: within a crawler's sight (6) of a rat resting at its third place, out of every resting rat's own (5)
        private static readonly Vec2 InSightOfTheWarren = new Vec2(14.7f, 16f);

        [Fact]
        public void AReachIsItsKindsLeashAroundItsHeart()
        {
            var map = Floor1Layout.Definition;
            Assert.True(map.TryGetLair(Floor1Layout.RatWarren, out var warren));
            Assert.True(map.TryGetLair(Floor1Layout.BeetleHollow, out var hollow));
            Assert.Equal(CreatureCatalog.Get(CreatureKind.BurrowRat).Leash, LairReach.Of(warren));
            Assert.Equal(CreatureCatalog.Get(CreatureKind.StoneBeetle).Leash, LairReach.Of(hollow));

            // Each holds the node beside it and no other, and not its sector's survey point
            Assert.True(LairReach.Holds(warren, Node(map, "overgrowth.3")));
            Assert.False(LairReach.Holds(warren, Node(map, "overgrowth.1")));
            Assert.False(LairReach.Holds(warren, Node(map, "overgrowth.2"))); // D20: the rats' leash of 6
            Assert.False(LairReach.Holds(warren, map.Sectors.First(s => s.Id == Floor1Layout.OvergrowthSectorId).TargetPoint));
            Assert.True(LairReach.Holds(hollow, Node(map, "seam.1")));
            Assert.False(LairReach.Holds(hollow, Node(map, "seam.2")));
            Assert.False(LairReach.Holds(hollow, Node(map, "seam.3")));
            Assert.False(LairReach.Holds(hollow, map.Sectors.First(s => s.Id == Floor1Layout.DeepSeamSectorId).TargetPoint));
            Assert.Equal(map.Lairs.ToList().FindIndex(l => l.Key == Floor1Layout.RatWarren), LairReach.LairAt(map, NearTheWarren));
            Assert.Equal(-1, LairReach.LairAt(map, Vec2.Zero));
        }

        [Fact]
        public void ALairsDangerIsItsPacksHarmASecondPastWhatAnyoneBravesAlone()
        {
            var rat = CreatureCatalog.Get(CreatureKind.BurrowRat);
            var beetle = CreatureCatalog.Get(CreatureKind.StoneBeetle);
            // The smallest pack, each at the base hit chance for its attack, every swing interval
            Assert.Equal(rat.PackMin * CombatRules.BaseHitChance * rat.Attack / rat.SwingIntervalSeconds, LairReach.Danger(rat, 0f), 3);
            Assert.Equal(beetle.PackMin * CombatRules.BaseHitChance * beetle.Attack / beetle.SwingIntervalSeconds, LairReach.Danger(beetle, 0f), 3);
            Assert.Equal(LairReach.Danger(rat, 0f) / 2f, LairReach.Danger(rat, CombatRules.MitigationScale), 3); // defense 10 halves it
            Assert.True(LairReach.Danger(rat, 0f) > Vitality.LoneHazardLimit && LairReach.Danger(beetle, 0f) > Vitality.LoneHazardLimit,
                "Bare, nobody braves a lair alone.");
        }

        [Fact]
        public void ACrawlerWhoSeesACreatureInAChartedSectorChartsItsLair()
        {
            using var sim = Outpost(hunting: true);
            var watcher = sim.Host.CrawlerStates[0];
            int warren = WarrenIndex(sim);

            // In the fog nothing is seen
            for (int t = 0; t < 5; t++)
            {
                watcher.Position = InSightOfTheWarren;
                sim.Step(1);
            }
            Assert.False(sim.Simulation.Creatures.IsCharted(warren));

            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            watcher.Position = InSightOfTheWarren;
            sim.Step(1);
            Assert.True(sim.Simulation.Creatures.IsCharted(warren), "Seen in the charted Overgrowth, the warren is charted.");
            Assert.False(sim.Simulation.Creatures.IsCharted(HollowIndex(sim)), "Nobody saw the beetles.");
            Assert.Equal(0u, watcher.StruckAtTick); // seen, not met
            sim.Step(1);
            Assert.True(Snapshot(sim).IsLairCharted(warren), "The next snapshot carries it to cognition.");
        }

        [Fact]
        public void ACrawlerStruckByACreatureChartsItsLairEvenInTheFog()
        {
            using var sim = Outpost(hunting: true);
            var stray = sim.Host.CrawlerStates[0];
            Assert.False(sim.Sectors.IsExplored(Floor1Layout.OvergrowthSectorId));
            for (int t = 0; t < 100 && stray.StruckAtTick == 0; t++)
            {
                stray.Position = NearTheWarren;
                sim.Step(1);
            }
            Assert.NotEqual(0u, stray.StruckAtTick);
            Assert.True(sim.Simulation.Creatures.IsCharted(WarrenIndex(sim)));
        }

        [Fact]
        public void ALoneCrawlerWouldNotGoIntoAChartedLairsReach()
        {
            using var sim = Outpost(hunting: true);
            var crawler = sim.Host.CrawlerStates.First(s => s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent);
            var context = crawler.CreateContext(Snapshot(sim), (uint)sim.Clock.CurrentTick);
            Assert.True(Vitality.WouldVenture(context));
            Assert.True(Vitality.WouldEnterAt(context, NearTheWarren), "Nobody knows of the warren yet.");
            Assert.Equal(0f, Vitality.LairDangerAt(context.Snapshot, NearTheWarren, context.Inventory));

            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            crawler.Position = InSightOfTheWarren;
            sim.Step(2);
            context = crawler.CreateContext(Snapshot(sim), (uint)sim.Clock.CurrentTick);
            Assert.True(context.Snapshot.IsLairCharted(WarrenIndex(sim)));
            Assert.False(Vitality.WouldEnterAt(context, NearTheWarren), "Even the bold keep out of a charted warren alone.");
            Assert.True(Vitality.LairDangerAt(context.Snapshot, NearTheWarren, context.Inventory) > Vitality.LoneHazardLimit);
            Assert.True(Vitality.WouldEnterAt(context, Node(sim.Host.Current.Map, "overgrowth.1")), "Out of its reach, the Overgrowth is open.");
        }

        /// <summary>
        /// K2.3b: a lone crawler who knows a node in a charted lair's reach looks past it for salvage, and an explorer's
        /// waypoint never lies in one (before the warren is charted, some do).
        /// </summary>
        [Fact]
        public void ALoneCrawlerLooksPastAChartedLairsReach()
        {
            using var sim = Outpost(hunting: true);
            var crawler = sim.Host.CrawlerStates.First(s => s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent);
            var map = sim.Host.Current.Map;
            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            sim.Step(1);
            // It has seen the Overgrowth's three herb patches from the middle of the room (the warren's rats rest out of its sight)
            crawler.Memory.UpdateMemory(Snapshot(sim), GridPoint.RoundToInt(new Vec2(16f, 12f)), AgentRuntime.DefaultVisionRadius, (uint)sim.Clock.CurrentTick);
            Assert.False(sim.Simulation.Creatures.IsCharted(WarrenIndex(sim)));
            crawler.Position = NearTheWarren;
            var context = crawler.CreateContext(Snapshot(sim), (uint)sim.Clock.CurrentTick);
            Assert.True(Simpiens.Cognition.Planning.ResourceTargeting.TrySelectSalvage(context, Simpiens.Simulation.Economy.ItemType.MedicinalHerb, out var before));
            Assert.Equal(Node(map, "overgrowth.3"), before.LastKnownLocation); // the nearest, while nobody knows of the warren
            // An explorer goes to the sector it stood in least long ago: another crawler, which has never been in the Overgrowth
            var explorer = sim.Host.CrawlerStates.First(s => s != crawler && !s.IsDrifter);
            var uncharted = Waypoints(explorer, sim);
            Assert.Contains(uncharted, w => LairReach.LairAt(map, w) == WarrenIndex(sim));

            crawler.Position = InSightOfTheWarren;
            sim.Step(2);
            Assert.True(sim.Simulation.Creatures.IsCharted(WarrenIndex(sim)));
            crawler.Position = NearTheWarren;
            context = crawler.CreateContext(Snapshot(sim), (uint)sim.Clock.CurrentTick);
            Assert.True(Simpiens.Cognition.Planning.ResourceTargeting.TrySelectSalvage(context, Simpiens.Simulation.Economy.ItemType.MedicinalHerb, out var after));
            Assert.False(LairReach.Holds(map.Lairs[WarrenIndex(sim)], after.LastKnownLocation), $"It chose {after.LastKnownLocation}, in the warren's reach.");
            var charted = Waypoints(explorer, sim);
            Assert.Contains(charted, w => map.FindSector(w).Id == Floor1Layout.OvergrowthSectorId); // it still explores the Overgrowth
            Assert.DoesNotContain(charted, w => LairReach.LairAt(map, w) == WarrenIndex(sim));
        }

        // Where it would explore from the hub, over many of the waypoint's windows
        private static System.Collections.Generic.List<Vec2> Waypoints(AgentState crawler, LocalSimulation sim)
        {
            var found = new System.Collections.Generic.List<Vec2>();
            var held = crawler.Position;
            crawler.Position = Vec2.Zero;
            for (uint w = 0; w < 400; w++)
            {
                var context = crawler.CreateContext(Snapshot(sim), w * Simpiens.Cognition.Planning.ResourceTargeting.SectorChoiceTicks);
                if (Simpiens.Cognition.Planning.ResourceTargeting.TryPickSectorWaypoint(context, sim.Host.Current.Map, out var waypoint)) found.Add(waypoint);
            }
            crawler.Position = held;
            return found;
        }

        [Fact]
        public void WithoutHuntingNothingIsChartedInADay()
        {
            using var sim = Outpost();
            sim.Step(Simpiens.Simulation.DayClock.TicksPerDay);
            Assert.Equal(0UL, sim.Simulation.Creatures.ChartedMask);
        }

        private static LocalSimulation Outpost(bool hunting = false)
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Simulation.Creatures.Hunting = hunting;
            return sim;
        }

        private static SharedWorldSnapshot Snapshot(LocalSimulation sim)
            => ((ISpatialPartition)sim.Services.GetService(typeof(ISpatialPartition))).GetActiveSnapshot();

        private static int WarrenIndex(LocalSimulation sim) => sim.Host.Current.Map.Lairs.ToList().FindIndex(l => l.Key == Floor1Layout.RatWarren);

        private static int HollowIndex(LocalSimulation sim) => sim.Host.Current.Map.Lairs.ToList().FindIndex(l => l.Key == Floor1Layout.BeetleHollow);

        private static Vec2 Node(Simpiens.Simulation.World.MapDefinition map, string key) => map.ResourceNodes.First(n => n.Key == key).Position;
    }
}
