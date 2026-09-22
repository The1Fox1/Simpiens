using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Cognition.Social;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Sociopolitical
{
    /// <summary>
    /// Automated domain validation suite for Epic 7 Phase 1:
    /// The 4-Axis Social Affinity Matrix (Warmth, Trust, Fear, Respect),
    /// Selective Socialization, and GOAP Relational Facts.
    /// </summary>
    public class AffinityMatrixValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[AffinityMatrixValidator] Beginning automated validation of 4-Axis Social Affinity Matrix...");

            ValidateAffinityRecordDynamicsAndRegression();
            ValidateGossipRelationalEvolution();
            ValidateSelectiveSocializationAndRefusal();
            ValidateWorldStateRelationalFactMapping();
            ValidateZeroAllocationAffinityBenchmark();

            Debug.Log("<color=green>[AffinityMatrixValidator] ALL 5 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateAffinityRecordDynamicsAndRegression()
        {
            var initial = AffinityRecord.Neutral;
            Assert(initial.Warmth == 0 && initial.Trust == 0 && initial.Fear == 0 && initial.Respect == 0, "Neutral record initializes to 0 on all axes.");

            // 1. Clamping bounds on interaction
            var mutated = initial.WithInteraction(deltaWarmth: 120, deltaTrust: -120, deltaFear: 120, deltaRespect: 80, currentTick: 100);
            Assert(mutated.Warmth == 100, $"Warmth clamped to +100 (actual: {mutated.Warmth})");
            Assert(mutated.Trust == -100, $"Trust clamped to -100 (actual: {mutated.Trust})");
            Assert(mutated.Fear == 100, $"Fear clamped to +100 (actual: {mutated.Fear})");
            Assert(mutated.Respect == 80, $"Respect set to +80 (actual: {mutated.Respect})");
            Assert(mutated.InteractionCount == 1, $"InteractionCount incremented to 1 (actual: {mutated.InteractionCount})");
            Assert(mutated.LastInteractionTick == 100, "LastInteractionTick tracked correctly.");

            // 2. Minimum bounds clamping (Fear cannot be negative)
            var calmed = mutated.WithInteraction(0, 0, deltaFear: -100, 0, currentTick: 200);
            Assert(calmed.Fear == 0, $"Fear clamped at 0 minimum (actual: {calmed.Fear})");

            // 3. Temporal regression toward neutral
            var highRecord = new AffinityRecord(warmth: 50, trust: 50, fear: 50, respect: 50, interactionCount: 1, lastInteractionTick: 1000);
            // After 2,000 ticks (2 intervals of 1,000 ticks -> 2 * 5 = 10 decay)
            var regressed = highRecord.RegressTowardNeutral(currentTick: 3000, regressionPeriod: 1000);
            Assert(regressed.Warmth == 40, $"Warmth regressed to 40 (actual: {regressed.Warmth})");
            Assert(regressed.Trust == 40, $"Trust regressed to 40 (actual: {regressed.Trust})");
            Assert(regressed.Fear == 40, $"Fear regressed to 40 (actual: {regressed.Fear})");
            Assert(regressed.Respect == 40, $"Respect regressed to 40 (actual: {regressed.Respect})");

            // Complete decay over long horizon
            var fullyNeutralized = highRecord.RegressTowardNeutral(currentTick: 20000, regressionPeriod: 1000);
            Assert(fullyNeutralized.Warmth == 0 && fullyNeutralized.Trust == 0 && fullyNeutralized.Fear == 0 && fullyNeutralized.Respect == 0,
                "All relational valences regressed to neutral baseline over 19,000 ticks.");

            Debug.Log("[PASS] Test 1: 4-Axis AffinityRecord clamping and temporal regression verified.");
        }

        private void ValidateGossipRelationalEvolution()
        {
            var memA = new AgentMemory();
            var memB = new AgentMemory();
            var idA = GUID.Generate();
            var idB = GUID.Generate();

            // First gossip exchange at tick 100
            bool success = memA.TryGossip(memB, idB, idA, currentTick: 100);
            Assert(success, "Initial gossip exchange succeeded.");

            var affAtoB = memA.GetAffinity(idB, currentTick: 100);
            Assert(affAtoB.Warmth == 5, $"Gossip awarded +5 Warmth (actual: {affAtoB.Warmth})");
            Assert(affAtoB.Trust == 10, $"Gossip awarded +10 Trust (actual: {affAtoB.Trust})");
            Assert(affAtoB.Fear == 0, $"Fear remains 0 (actual: {affAtoB.Fear})");
            Assert(affAtoB.Respect == 5, $"Gossip awarded +5 Respect (actual: {affAtoB.Respect})");
            Assert(affAtoB.InteractionCount == 1, "InteractionCount is 1.");

            var affBtoA = memB.GetAffinity(idA, currentTick: 100);
            Assert(affBtoA.Warmth == 5 && affBtoA.Trust == 10 && affBtoA.Respect == 5, "Bilateral affinity recorded symmetrically.");

            // Second gossip exchange after cooldown at tick 450
            bool secondSuccess = memA.TryGossip(memB, idB, idA, currentTick: 450);
            Assert(secondSuccess, "Second gossip exchange succeeded after cooldown.");

            var affAtoB2 = memA.GetAffinity(idB, currentTick: 450);
            Assert(affAtoB2.Warmth == 10, $"Warmth accumulated to 10 (actual: {affAtoB2.Warmth})");
            Assert(affAtoB2.Trust == 20, $"Trust accumulated to 20 (actual: {affAtoB2.Trust})");
            Assert(affAtoB2.Respect == 10, $"Respect accumulated to 10 (actual: {affAtoB2.Respect})");
            Assert(affAtoB2.IsTrusted, "Peer now qualifies as trusted (Trust >= 20).");

            Debug.Log("[PASS] Test 2: Gossip relational evolution across 4 axes verified.");
        }

        private void ValidateSelectiveSocializationAndRefusal()
        {
            var memA = new AgentMemory();
            var memB = new AgentMemory();
            var idA = GUID.Generate();
            var idB = GUID.Generate();

            // Scenario A: Distrust Refusal (Trust < -20)
            memA.RecordInteraction(idB, deltaWarmth: -10, deltaTrust: -25, deltaFear: 0, deltaRespect: 0, currentTick: 100);
            Assert(memA.IsDistrusted(idB, 100), "Agent A actively distrusts Agent B.");

            bool distrustRefused = !memA.TryGossip(memB, idB, idA, currentTick: 100);
            Assert(distrustRefused, "Gossip refused due to negative trust (selective socialization).");

            // Scenario B: Terror Refusal (Fear > 60)
            var memC = new AgentMemory();
            var idC = GUID.Generate();
            memA.RecordInteraction(idC, deltaWarmth: 0, deltaTrust: 0, deltaFear: 70, deltaRespect: 0, currentTick: 100);
            Assert(memA.IsFeared(idC, 100), "Agent A actively fears Agent C.");

            bool fearRefused = !memA.TryGossip(memC, idC, idA, currentTick: 100);
            Assert(fearRefused, "Gossip refused due to excessive fear.");

            Debug.Log("[PASS] Test 3: Selective socialization and gossip refusal under distrust/fear verified.");
        }

        private void ValidateWorldStateRelationalFactMapping()
        {
            var mem = new AgentMemory();
            var dummyGuid = GUID.Generate();

            // 1. Initial State: No relationships
            var contextNeutral = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(50f, 100f, 50f), 0f, null, mem, 100);
            var stateNeutral = WorldStateBuilder.BuildWorldState(contextNeutral);

            Assert(stateNeutral.Get(StateFact.HasWarmCompanion) == false, "HasWarmCompanion is false initially.");
            Assert(stateNeutral.Get(StateFact.HasTrustedPeer) == false, "HasTrustedPeer is false initially.");
            Assert(stateNeutral.Get(StateFact.HasFearedThreat) == false, "HasFearedThreat is false initially.");
            Assert(stateNeutral.Get(StateFact.HasRespectedLeader) == false, "HasRespectedLeader is false initially.");

            // 2. Add Warm, Trusted, and Respected ally
            var allyId = GUID.Generate();
            mem.RecordInteraction(allyId, deltaWarmth: 35, deltaTrust: 40, deltaFear: 0, deltaRespect: 45, currentTick: 100);

            // 3. Add Feared threat
            var threatId = GUID.Generate();
            mem.RecordInteraction(threatId, deltaWarmth: -20, deltaTrust: -30, deltaFear: 65, deltaRespect: 0, currentTick: 100);

            var contextPopulated = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(50f, 100f, 50f), 0f, null, mem, 100);
            var statePopulated = WorldStateBuilder.BuildWorldState(contextPopulated);

            Assert(statePopulated.Get(StateFact.HasWarmCompanion) == true, "HasWarmCompanion is true when Warmth >= 30.");
            Assert(statePopulated.Get(StateFact.HasTrustedPeer) == true, "HasTrustedPeer is true when Trust >= 30.");
            Assert(statePopulated.Get(StateFact.HasFearedThreat) == true, "HasFearedThreat is true when Fear >= 50.");
            Assert(statePopulated.Get(StateFact.HasRespectedLeader) == true, "HasRespectedLeader is true when Respect >= 30.");

            // 4. Validate SocializeGoal priority bonus
            var socializeGoal = new SocializeGoal();
            float neutralPriority = socializeGoal.CalculatePriority(contextNeutral);
            float allyPriority = socializeGoal.CalculatePriority(contextPopulated);
            Assert(allyPriority > neutralPriority, $"SocializeGoal priority increased when warm/trusted companions are known ({allyPriority} > {neutralPriority}).");

            Debug.Log("[PASS] Test 4: GOAP WorldState relational fact mapping and goal priority weighting verified.");
        }

        private void ValidateZeroAllocationAffinityBenchmark()
        {
            var mem = new AgentMemory();
            var dummyGuid = GUID.Generate();
            var peerA = GUID.Generate();
            var peerB = GUID.Generate();

            mem.RecordInteraction(peerA, 30, 30, 0, 20, 100);
            mem.RecordInteraction(peerB, -20, -30, 60, 0, 100);

            var context = new AgentContext(dummyGuid, Vector2.zero, new AgentNeeds(50f, 100f, 50f), 0f, null, mem, 100);

            // Warmup
            for (int i = 0; i < 50; i++)
            {
                mem.GetAffinity(peerA, 100);
                mem.GetAffinity(peerB, 100);
                WorldStateBuilder.BuildWorldState(context);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                var affA = mem.GetAffinity(peerA, 100);
                var affB = mem.GetAffinity(peerB, 100);
                bool trusted = affA.IsTrusted;
                bool feared = affB.IsFeared;
                var state = WorldStateBuilder.BuildWorldState(context);
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero-allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 relational evaluations.");

            Debug.Log("[PASS] Test 5: Strict zero-allocation guarantee verified (0 bytes across 1,000 evaluations).");
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
