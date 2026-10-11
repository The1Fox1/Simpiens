using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Milestone 1 closure, Phase 2 (ROADMAP §1A.1 rules 3–4): seeded randomness, reproducible entity ids, and
    /// intent application that doesn't depend on the order cognition results arrive in.
    /// Scope: same-tick ordering. The tick on which an async cognition result arrives is still thread-timing dependent
    /// (§1A.2 #6); that belongs to the multiplayer work.
    /// </summary>
    public class DeterminismValidator : IStartable
    {
        private const int PawnCount = 8;
        private const int SimTicks = 60;

        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[DeterminismValidator] Beginning determinism validation...");

            ValidateSimRandomIsReproducible();
            ValidateEntityIdsAreReproducible();
            ValidateIntentArrivalOrderDoesNotMatter();
            ValidateSimRandomIsZeroAlloc();

            Debug.Log("<color=green>[DeterminismValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateSimRandomIsReproducible()
        {
            var agent = new GUID(11u, 22u, 33u, 44u);
            var sameAgent = new GUID(11u, 22u, 33u, 44u);

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

            Debug.Log("[PASS] Test 1: SimRandom streams are reproducible and keyed by seed, entity, tick and salt.");
        }

        private void ValidateEntityIdsAreReproducible()
        {
            var first = new EntityIdAllocator(new SimulationSettings(42));
            var second = new EntityIdAllocator(new SimulationSettings(42));
            var other = new EntityIdAllocator(new SimulationSettings(43));

            var seen = new HashSet<GUID>();
            var sequence = new GUID[64];
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

            Debug.Log("[PASS] Test 2: Entity ids are reproducible per seed and replay after Reset.");
        }

        private void ValidateIntentArrivalOrderDoesNotMatter()
        {
            // Pawns on a ring all walk to the centre, so separation decides who gets where: order-sensitive by design
            var inOrder = RunConvergence(reverseEnqueue: false, reverseRegistration: false);
            var reversed = RunConvergence(reverseEnqueue: true, reverseRegistration: false);
            var reRegistered = RunConvergence(reverseEnqueue: true, reverseRegistration: true);

            for (int i = 0; i < PawnCount; i++)
            {
                // Exact float equality (Vector2 == is approximate): determinism means bit-identical
                Assert(inOrder[i].x == reversed[i].x && inOrder[i].y == reversed[i].y,
                    $"Pawn {i}: {inOrder[i]:F6} vs {reversed[i]:F6} after reversing intent arrival order.");
                Assert(inOrder[i].x == reRegistered[i].x && inOrder[i].y == reRegistered[i].y,
                    $"Pawn {i}: {inOrder[i]:F6} vs {reRegistered[i]:F6} after reversing registration order.");
            }

            bool crowded = false;
            for (int i = 0; i < PawnCount && !crowded; i++)
            {
                if (inOrder[i].sqrMagnitude > 0.01f) crowded = true;
            }
            Assert(crowded, "Scenario must actually be contended (not every pawn reached the centre).");

            Debug.Log("[PASS] Test 3: Final positions are identical regardless of intent arrival or registration order.");
        }

        private Vector2[] RunConvergence(bool reverseEnqueue, bool reverseRegistration)
        {
            var ids = new EntityIdAllocator(new SimulationSettings(99));
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(20f, 20f, 1f);
            var sim = new SimulationManager(registry, grid);

            var nodes = new NodeController[PawnCount];
            for (int i = 0; i < PawnCount; i++)
            {
                float angle = i * Mathf.PI * 2f / PawnCount;
                var go = new GameObject($"[Determinism_Pawn{i}]");
                go.transform.position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3f;
                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.35f;
                var node = go.AddComponent<NodeController>();
                node.Type = EntityType.Pawn;
                node.Initialize(ids.Next(), new NodeConfiguration(2f));
                nodes[i] = node;
            }

            for (int k = 0; k < PawnCount; k++)
            {
                registry.RegisterNode(nodes[reverseRegistration ? PawnCount - 1 - k : k]);
            }
            grid.UpdateFromRegistry(registry);

            for (int k = 0; k < PawnCount; k++)
            {
                var node = nodes[reverseEnqueue ? PawnCount - 1 - k : k];
                var path = new PathResponse { Waypoints = new[] { Vector2.zero }, Length = 1, IsValid = true };
                sim.EnqueueIntent(new WanderIntent(node.Id, path));
            }

            for (int t = 0; t < SimTicks; t++)
            {
                sim.Tick();
            }

            var result = new Vector2[PawnCount];
            for (int i = 0; i < PawnCount; i++)
            {
                result[i] = nodes[i].Position;
                GameObject.DestroyImmediate(nodes[i].gameObject);
            }
            return result;
        }

        private void ValidateSimRandomIsZeroAlloc()
        {
            var agent = new GUID(5u, 6u, 7u, 8u);
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

            Debug.Log($"[PASS] Test 4: SimRandom is allocation-free (checksum {sink:F1}).");
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
