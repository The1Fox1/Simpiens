using System;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The anomaly detector's rules (Epic CLIENT M2.8): the cases of Unity's <c>VisualAnomalyDetectorValidator</c>, on the
    /// engine-free detector. Unity's frame-versus-tick case is gone with the frames: every check is per tick.
    /// </summary>
    public class AnomalyDetectorTests
    {
        private static WorldEntity Pawn(WorldRegistry registry, Vec2 at, string name) =>
            HeadlessEntities.Prop(registry, EntityType.Pawn, at, id: EntityId.Generate(), name: name);

        [Fact]
        public void BackAndForthEveryTickIsJitter()
        {
            var registry = new WorldRegistry();
            var detector = new AnomalyDetector(registry);
            var pawn = Pawn(registry, Vec2.Zero, "Jitter");
            float[] xs = { 0f, 0.5f, 0f, 0.5f, 0f };
            for (int t = 0; t < xs.Length; t++)
            {
                pawn.SetPosition(new Vec2(xs[t], 0f));
                detector.Observe(t);
            }
            Assert.Equal(1, detector.JitterCount);
            Assert.Equal(AnomalyType.OscillationJitter, detector.Recent[0].Type);
            Assert.Contains("last positions", detector.Recent[0].Description);
        }

        [Fact]
        public void TurningBackOnceOrSlowlyIsNotJitter()
        {
            var registry = new WorldRegistry();
            var detector = new AnomalyDetector(registry);
            var pawn = Pawn(registry, Vec2.Zero, "Turns");
            long t = 0;
            for (int k = 0; k <= 5; k++, t++) { pawn.SetPosition(new Vec2(k * 0.2f, 0f)); detector.Observe(t); }
            for (int k = 4; k >= 0; k--, t++) { pawn.SetPosition(new Vec2(k * 0.2f, 0f)); detector.Observe(t); }
            Assert.Equal(0, detector.JitterCount);

            // Back and forth every 3 ticks: 3 reversals span 6 ticks, beyond the 5-tick window
            float x = 0f;
            int dir = 1;
            for (int k = 0; k < 18; k++, t++)
            {
                if (k % 3 == 0) dir = -dir;
                x += dir * 0.2f;
                pawn.SetPosition(new Vec2(x, 5f));
                detector.Observe(t);
            }
            Assert.Equal(0, detector.JitterCount);
        }

        [Fact]
        public void TwoPawnsOnTopOfEachOtherForASecondAreStacked()
        {
            var registry = new WorldRegistry();
            var detector = new AnomalyDetector(registry);
            Pawn(registry, Vec2.Zero, "A");
            Pawn(registry, new Vec2(0.1f, 0f), "B");
            for (int t = 0; t < 9; t++) detector.Observe(t);
            Assert.Equal(0, detector.StackingCount); // 0.9 s
            for (int t = 9; t < 15; t++) detector.Observe(t);
            Assert.Equal(1, detector.StackingCount); // past 1 s, once a second at most
        }

        [Fact]
        public void FourStateChangesInHalfASecondAreThrashing()
        {
            var registry = new WorldRegistry();
            var detector = new AnomalyDetector(registry);
            var crawler = HeadlessEntities.Crawler(registry, null, null, Vec2.Zero, name: "Thrash");
            var states = new[] { AgentVisualState.Idle, AgentVisualState.Walking, AgentVisualState.Panicking, AgentVisualState.Yielding, AgentVisualState.Sharing };
            for (int t = 0; t < states.Length; t++)
            {
                crawler.Agent.VisualState = states[t];
                detector.Observe(t);
            }
            Assert.Equal(1, detector.ThrashingCount);
        }

        [Fact]
        public void ThrashingIsJudgedOnTheStateItIsGiven()
        {
            // A view that holds each state 3 ticks shows no thrashing where the simulation's state flickers every tick
            var registry = new WorldRegistry();
            var held = AgentVisualState.Idle;
            long since = 0;
            var detector = new AnomalyDetector(registry, shownState: (entity, tick) =>
            {
                if (entity.Agent.VisualState != held && tick - since >= 3) { held = entity.Agent.VisualState; since = tick; }
                return held;
            });
            var crawler = HeadlessEntities.Crawler(registry, null, null, Vec2.Zero, name: "Flicker");
            for (int t = 0; t < 30; t++)
            {
                crawler.Agent.VisualState = t % 2 == 0 ? AgentVisualState.Idle : AgentVisualState.Thinking;
                detector.Observe(t);
            }
            Assert.Equal(0, detector.ThrashingCount);
        }

        [Fact]
        public void APawnInsideAMapWallIsFlaggedAndOneBesideItIsNot()
        {
            var registry = new WorldRegistry();
            var map = new MapDefinitionBuilder("PenetrationProbe", new RectF(-10f, -10f, 20f, 20f))
                .WallRun(new Vec2(3f, -2f), new Vec2(3f, 2f))
                .Build();
            var worldMap = new WorldMap(null, null);
            worldMap.Load(map);
            var detector = new AnomalyDetector(registry, worldMap);
            var pawn = Pawn(registry, Vec2.Zero, "Penetration");
            for (int t = 0; t < 20; t++) detector.Observe(t);
            Assert.Equal(0, detector.PenetrationCount);

            pawn.SetPosition(new Vec2(3f, 0f));
            for (int t = 20; t < 40; t++) detector.Observe(t);
            Assert.True(detector.PenetrationCount >= 1, $"A pawn inside a map wall is flagged ({detector.PenetrationCount}).");
            Assert.Equal(0, detector.BoundaryBreachCount);

            pawn.SetPosition(new Vec2(12f, 0f));
            detector.Observe(40);
            Assert.Equal(1, detector.BoundaryBreachCount);
        }

        [Fact]
        public void APawnInsideAnInjectedWallIsFlagged()
        {
            var registry = new WorldRegistry();
            var detector = new AnomalyDetector(registry);
            Pawn(registry, Vec2.Zero, "Walker");
            HeadlessEntities.Prop(registry, EntityType.Wall, new Vec2(0.3f, 0f), name: "InjectedWall");
            detector.Observe(0);
            Assert.Equal(1, detector.PenetrationCount);
        }

        [Fact]
        public void ObservingAllocatesNothingOnceWarm()
        {
            var registry = new WorldRegistry();
            var detector = new AnomalyDetector(registry);
            for (int i = 0; i < 5; i++) Pawn(registry, new Vec2(i * 2f, 0f), $"Bench{i}");
            for (int t = 0; t < 50; t++) detector.Observe(t);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 50; t < 550; t++) detector.Observe(t);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
