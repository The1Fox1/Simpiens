using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Milestone 1 closure, Phase 2 (ROADMAP §1A.1 rules 3–4): seeded randomness, reproducible entity ids, and
    /// intent application that doesn't depend on the order cognition results arrive in.
    /// Scope: same-tick ordering. The tick a decision lands on is fixed by the CognitionScheduler (§1A.2 #6, Epic CORE C1;
    /// see CognitionSchedulerValidator).
    /// </summary>
    public class DeterminismValidator
    {
        private const int PawnCount = 8;
        private const int SimTicks = 60;

        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            TestLog.Log("[DeterminismValidator] Beginning determinism validation...");

            ValidateSimRandomIsReproducible();
            ValidateEntityIdsAreReproducible();
            ValidateIntentArrivalOrderDoesNotMatter();
            ValidateSimRandomIsZeroAlloc();
            ValidateStaleSessionIntentsAreDropped();

            TestLog.Log("<color=green>[DeterminismValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateSimRandomIsReproducible()
        {
            var agent = new EntityId(11u, 22u, 33u, 44u);
            var sameAgent = new EntityId(11u, 22u, 33u, 44u);

            var a = SimRandom.For(7, agent, 100, SimRandomSalt.AmbientWander);
            var b = SimRandom.For(7, sameAgent, 100, SimRandomSalt.AmbientWander);
            for (int i = 0; i < 16; i++)
            {
                Assert(a.NextULong() == b.NextULong(), "Same (seed, entity, tick, salt) must produce the same stream.");
            }

            float baseline = SimRandom.For(7, agent, 100, SimRandomSalt.AmbientWander).NextFloat();
            Assert(SimRandom.For(8, agent, 100, SimRandomSalt.AmbientWander).NextFloat() != baseline, "A different seed must change the stream.");
            Assert(SimRandom.For(7, agent, 101, SimRandomSalt.AmbientWander).NextFloat() != baseline, "A different tick must change the stream.");
            Assert(SimRandom.For(7, agent, 100, SimRandomSalt.PanicDirection).NextFloat() != baseline, "A different salt must change the stream.");

            var rng = SimRandom.For(1, agent, 0, 0);
            double sum = 0;
            const int draws = 10000;
            for (int i = 0; i < draws; i++)
            {
                float v = rng.NextFloat();
                Assert(v >= 0f && v < 1f, $"NextFloat out of range: {v}");
                sum += v;
            }
            double mean = sum / draws;
            Assert(mean > 0.48 && mean < 0.52, $"NextFloat mean {mean:F3} is not ~0.5.");

            TestLog.Log("[PASS] Test 1: SimRandom streams are reproducible and keyed by seed, entity, tick and salt.");
        }

        private void ValidateEntityIdsAreReproducible()
        {
            var first = new EntityIdAllocator(new SimulationSettings(42));
            var second = new EntityIdAllocator(new SimulationSettings(42));
            var other = new EntityIdAllocator(new SimulationSettings(43));

            var seen = new HashSet<EntityId>();
            var sequence = new EntityId[64];
            bool anyDifferent = false;
            for (int i = 0; i < sequence.Length; i++)
            {
                sequence[i] = first.Next();
                Assert(sequence[i] != default && sequence[i] != WorldRegistry.PlayerEntityId, "Allocator must never issue reserved ids.");
                Assert(seen.Add(sequence[i]), "Allocator issued a duplicate id.");
                Assert(second.Next() == sequence[i], "Same seed must issue the same id sequence.");
                if (other.Next() != sequence[i]) anyDifferent = true;
            }
            Assert(anyDifferent, "A different seed must issue different ids.");

            first.Reset();
            for (int i = 0; i < sequence.Length; i++)
            {
                Assert(first.Next() == sequence[i], "Reset must replay the same id sequence (respawn reproducibility).");
            }

            TestLog.Log("[PASS] Test 2: Entity ids are reproducible per seed and replay after Reset.");
        }

        private void ValidateIntentArrivalOrderDoesNotMatter()
        {
            // Pawns on a ring all walk to the centre, so separation decides who gets where: order-sensitive by design
            var inOrder = RunConvergence(reverseEnqueue: false, reverseRegistration: false);
            var reversed = RunConvergence(reverseEnqueue: true, reverseRegistration: false);
            var reRegistered = RunConvergence(reverseEnqueue: true, reverseRegistration: true);

            for (int i = 0; i < PawnCount; i++)
            {
                // Exact float equality (Vec2 == is approximate): determinism means bit-identical
                Assert(inOrder[i].X == reversed[i].X && inOrder[i].Y == reversed[i].Y,
                    $"Pawn {i}: {inOrder[i]:F6} vs {reversed[i]:F6} after reversing intent arrival order.");
                Assert(inOrder[i].X == reRegistered[i].X && inOrder[i].Y == reRegistered[i].Y,
                    $"Pawn {i}: {inOrder[i]:F6} vs {reRegistered[i]:F6} after reversing registration order.");
            }

            bool crowded = false;
            for (int i = 0; i < PawnCount && !crowded; i++)
            {
                if (inOrder[i].SqrMagnitude > 0.01f) crowded = true;
            }
            Assert(crowded, "Scenario must actually be contended (not every pawn reached the centre).");

            TestLog.Log("[PASS] Test 3: Final positions are identical regardless of intent arrival or registration order.");
        }

        /// <summary>
        /// Epic MAP Phase 5: a respawn reissues the same entity ids, so an intent decided before it must not move the
        /// new agent that took the id. The simulation drops intents stamped with an earlier session.
        /// </summary>
        private void ValidateStaleSessionIntentsAreDropped()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20f, 20f, 1f);
            var sim = new SimulationManager(registry, grid);

            var node = HeadlessEntities.Prop(registry, EntityType.Pawn, Vec2.Zero, id: new EntityIdAllocator(new SimulationSettings(7)).Next(), radius: 0.35f, name: "[Determinism_Respawned]");
            grid.UpdateFromRegistry(registry);

            WanderIntent WalkEast() => new WanderIntent(node.Id, new PathResponse { Waypoints = new[] { new Vec2(3f, 0f) }, Length = 1, IsValid = true });

            uint before = sim.SessionEpoch;
            var stale = WalkEast();
            stale.SessionEpoch = before;
            sim.ResetState(); // a respawn
            Assert(sim.SessionEpoch == before + 1, $"A reset starts a new session ({before} -> {sim.SessionEpoch}).");
            sim.EnqueueIntent(stale);
            for (int t = 0; t < 10; t++) sim.Tick();
            Assert(node.Position.X == 0f, $"An intent from the previous session is dropped (pawn moved to {node.Position}).");

            var current = WalkEast();
            current.SessionEpoch = sim.SessionEpoch;
            sim.EnqueueIntent(current);
            for (int t = 0; t < 5; t++) sim.Tick();
            float afterCurrent = node.Position.X;
            Assert(afterCurrent > 0.5f, $"An intent from the current session runs (pawn at {node.Position}).");

            sim.EnqueueIntent(WalkEast()); // unstamped: built directly, always accepted
            for (int t = 0; t < 5; t++) sim.Tick();
            Assert(node.Position.X > afterCurrent, $"An unstamped intent runs (pawn at {node.Position}).");

            registry?.Unregister(node);
            TestLog.Log("[PASS] Test 5: Intents decided before a respawn are dropped; current and unstamped intents run.");
        }

        private Vec2[] RunConvergence(bool reverseEnqueue, bool reverseRegistration)
        {
            var ids = new EntityIdAllocator(new SimulationSettings(99));
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20f, 20f, 1f);
            var sim = new SimulationManager(registry, grid);

            var nodes = new WorldEntity[PawnCount];
            for (int i = 0; i < PawnCount; i++)
            {
                float angle = i * SimMath.PI * 2f / PawnCount;
                var node = HeadlessEntities.Prop(null, EntityType.Pawn, new Vec2(SimMath.Cos(angle), SimMath.Sin(angle)) * 3f, id: ids.Next(), radius: 0.35f, name: $"[Determinism_Pawn{i}]", register: false);
                nodes[i] = node;
            }

            for (int k = 0; k < PawnCount; k++)
            {
                registry.Register(nodes[reverseRegistration ? PawnCount - 1 - k : k]);
            }
            grid.UpdateFromRegistry(registry);

            for (int k = 0; k < PawnCount; k++)
            {
                var node = nodes[reverseEnqueue ? PawnCount - 1 - k : k];
                var path = new PathResponse { Waypoints = new[] { Vec2.Zero }, Length = 1, IsValid = true };
                sim.EnqueueIntent(new WanderIntent(node.Id, path));
            }

            for (int t = 0; t < SimTicks; t++)
            {
                sim.Tick();
            }

            var result = new Vec2[PawnCount];
            for (int i = 0; i < PawnCount; i++)
            {
                result[i] = nodes[i].Position;
            }
            return result;
        }

        private void ValidateSimRandomIsZeroAlloc()
        {
            var agent = new EntityId(5u, 6u, 7u, 8u);
            float sink = 0f;
            for (int i = 0; i < 100; i++) sink += SimRandom.For(3, agent, i, 1).NextFloat(); // warm up

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                var rng = SimRandom.For(3, agent, i, SimRandomSalt.AmbientWander);
                sink += rng.NextFloat() + rng.Range(1f, 2f);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0, $"SimRandom allocated {allocated} bytes across 10,000 streams.");

            TestLog.Log($"[PASS] Test 4: SimRandom is allocation-free (checksum {sink:F1}).");
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
