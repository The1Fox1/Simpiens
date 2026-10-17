using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Sociopolitical
{
    /// <summary>
    /// Automated domain validation suite for Epic Beta Phase 1:
    /// Resource Contention & Yielding (Fear & Respect on Finite Nodes).
    /// </summary>
    public class ResourceContentionValidator : IStartable
    {
        public void Start()
        {
            Debug.Log("[ResourceContentionValidator] Beginning automated validation of Resource Contention & Social Yielding...");

            ValidateFearYielding();
            ValidateRespectYielding();
            ValidateSymmetricalContention();
            ValidateYieldingVisualSpriteAndState();
            ValidateZeroAllocationContentionBenchmark();

            Debug.Log("<color=green>[ResourceContentionValidator] ALL 5 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateFearYielding()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var resId = EntityId.Generate();
            simManager.RegisterResource(new ResourceData { EntityId = resId, RemainingYield = 5 });

            // Dominant Pawn A at (0, 0)
            var goA = new GameObject("[Test_PawnA_Dominant]");
            var nodeA = goA.AddComponent<NodeController>();
            nodeA.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeA.transform.position = Vec2.Zero.ToUnity();
            var agentA = goA.AddComponent<AutonomousAgent>();
            agentA.Initialize(nodeA.Id, null, null, simManager, clock);
            registry.RegisterNode(nodeA);

            // Submissive Pawn B at (0, 0.5) fearing Pawn A
            var goB = new GameObject("[Test_PawnB_Submissive]");
            var nodeB = goB.AddComponent<NodeController>();
            nodeB.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeB.transform.position = (new Vec2(0f, 0.5f)).ToUnity();
            var agentB = goB.AddComponent<AutonomousAgent>();
            agentB.Initialize(nodeB.Id, null, null, simManager, clock);
            registry.RegisterNode(nodeB);

            // Pawn B fears Pawn A (Fear = 70)
            agentB.Memory.RecordInteraction(nodeA.Id, deltaWarmth: 0, deltaTrust: 0, deltaFear: 70, deltaRespect: 0, 0);

            // Enqueue harvest intents for both targeting the same resource
            var pathA = new PathResponse { Length = 0, IsValid = true };
            var pathB = new PathResponse { Length = 0, IsValid = true };
            simManager.EnqueueIntent(new HarvestResourceIntent(nodeA.Id, resId, pathA, gatherDuration: 2.0f));
            simManager.EnqueueIntent(new HarvestResourceIntent(nodeB.Id, resId, pathB, gatherDuration: 2.0f));

            // Tick simulation
            simManager.Tick();

            // Assertions
            Assert(agentB.VisualState == AgentVisualState.Yielding, $"Submissive Pawn B assumed Yielding state (actual: {agentB.VisualState})");
            Assert(nodeB.transform.position != new Vector3(0f, 0.5f, 0f), "Submissive Pawn B stepped backward away from dominant competitor.");
            Assert(agentB.Memory.IsBlacklisted(resId, 0), "Submissive Pawn B blacklisted the contested resource node.");

            // Check memory ledger events
            bool bYieldedLogged = false;
            for (int i = 0; i < agentB.Memory.EventLedger.Count; i++)
            {
                var evt = agentB.Memory.EventLedger.GetEvent(i);
                if (evt.EventType == MemoryEventType.ResourceYielded && evt.SubjectId == nodeA.Id)
                {
                    bYieldedLogged = true;
                    break;
                }
            }
            Assert(bYieldedLogged, "Pawn B logged ResourceYielded event with Pawn A as subject.");

            bool aClaimedLogged = false;
            for (int i = 0; i < agentA.Memory.EventLedger.Count; i++)
            {
                var evt = agentA.Memory.EventLedger.GetEvent(i);
                if (evt.EventType == MemoryEventType.ResourceClaimed && evt.SubjectId == nodeB.Id)
                {
                    aClaimedLogged = true;
                    break;
                }
            }
            Assert(aClaimedLogged, "Pawn A logged ResourceClaimed event with Pawn B as subject.");

            // Clean up test GameObjects
            GameObject.DestroyImmediate(goA);
            GameObject.DestroyImmediate(goB);
        }

        private void ValidateRespectYielding()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var resId = EntityId.Generate();
            simManager.RegisterResource(new ResourceData { EntityId = resId, RemainingYield = 5 });

            // Respected Elder C at (2, 2)
            var goC = new GameObject("[Test_PawnC_Elder]");
            var nodeC = goC.AddComponent<NodeController>();
            nodeC.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeC.transform.position = (new Vec2(2f, 2f)).ToUnity();
            var agentC = goC.AddComponent<AutonomousAgent>();
            agentC.Initialize(nodeC.Id, null, null, simManager, clock);
            registry.RegisterNode(nodeC);

            // Follower D at (2, 2.3) respecting Elder C
            var goD = new GameObject("[Test_PawnD_Follower]");
            var nodeD = goD.AddComponent<NodeController>();
            nodeD.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeD.transform.position = (new Vec2(2f, 2.3f)).ToUnity();
            var agentD = goD.AddComponent<AutonomousAgent>();
            agentD.Initialize(nodeD.Id, null, null, simManager, clock);
            registry.RegisterNode(nodeD);

            // Pawn D respects Pawn C (Respect = 80)
            agentD.Memory.RecordInteraction(nodeC.Id, deltaWarmth: 10, deltaTrust: 10, deltaFear: 0, deltaRespect: 80, 0);

            var pathC = new PathResponse { Length = 0, IsValid = true };
            var pathD = new PathResponse { Length = 0, IsValid = true };
            simManager.EnqueueIntent(new HarvestResourceIntent(nodeC.Id, resId, pathC, gatherDuration: 2.0f));
            simManager.EnqueueIntent(new HarvestResourceIntent(nodeD.Id, resId, pathD, gatherDuration: 2.0f));

            simManager.Tick();

            Assert(agentD.VisualState == AgentVisualState.Yielding, $"Follower D yielded to Elder C with Yielding visual state (actual: {agentD.VisualState})");
            Assert(agentD.Memory.IsBlacklisted(resId, 0), "Follower D blacklisted contested node.");

            bool dYieldedLogged = false;
            for (int i = 0; i < agentD.Memory.EventLedger.Count; i++)
            {
                var evt = agentD.Memory.EventLedger.GetEvent(i);
                if (evt.EventType == MemoryEventType.ResourceYielded && evt.SubjectId == nodeC.Id)
                {
                    dYieldedLogged = true;
                    break;
                }
            }
            Assert(dYieldedLogged, "Pawn D logged ResourceYielded event for respected Elder C.");

            GameObject.DestroyImmediate(goC);
            GameObject.DestroyImmediate(goD);
        }

        private void ValidateSymmetricalContention()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            // Finite single-yield node
            var resId = EntityId.Generate();
            simManager.RegisterResource(new ResourceData { EntityId = resId, RemainingYield = 1 });

            // Mutual rivals E and F at same node
            var goE = new GameObject("[Test_PawnE_Rival]");
            var nodeE = goE.AddComponent<NodeController>();
            nodeE.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeE.transform.position = (new Vec2(5f, 5f)).ToUnity();
            var agentE = goE.AddComponent<AutonomousAgent>();
            agentE.Initialize(nodeE.Id, null, null, simManager, clock);
            registry.RegisterNode(nodeE);

            var goF = new GameObject("[Test_PawnF_Rival]");
            var nodeF = goF.AddComponent<NodeController>();
            nodeF.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            nodeF.transform.position = (new Vec2(5f, 5.2f)).ToUnity();
            var agentF = goF.AddComponent<AutonomousAgent>();
            agentF.Initialize(nodeF.Id, null, null, simManager, clock);
            registry.RegisterNode(nodeF);

            // Symmetrical low affinity: mutual contempt and zero fear
            agentE.Memory.RecordInteraction(nodeF.Id, deltaWarmth: -30, deltaTrust: -20, deltaFear: 0, deltaRespect: -20, 0);
            agentF.Memory.RecordInteraction(nodeE.Id, deltaWarmth: -30, deltaTrust: -20, deltaFear: 0, deltaRespect: -20, 0);

            // Start harvest for both with 1.0s gather duration
            var pathE = new PathResponse { Length = 0, IsValid = true };
            var pathF = new PathResponse { Length = 0, IsValid = true };
            simManager.EnqueueIntent(new HarvestResourceIntent(nodeE.Id, resId, pathE, gatherDuration: 1.0f));
            simManager.EnqueueIntent(new HarvestResourceIntent(nodeF.Id, resId, pathF, gatherDuration: 1.0f));

            // Tick 1: Both arrive, neither yields, both enter Harvesting
            simManager.Tick();
            Assert(agentE.VisualState == AgentVisualState.Harvesting, "Pawn E entered Harvesting without yielding.");
            Assert(agentF.VisualState == AgentVisualState.Harvesting, "Pawn F entered Harvesting without yielding.");

            // Manually advance state to simulate E finishing first and claiming the single yield
            // Call ExecuteIntent directly to test deterministic resolution
            var activeE = new ActiveIntentState { Intent = new HarvestResourceIntent(nodeE.Id, resId, pathE, 1.0f), ElapsedTime = 1.05f };
            var resultE = simManager.ExecuteIntent(nodeE, activeE);
            Assert(resultE == IntentResult.Success, "Pawn E successfully harvested the single yield.");

            // Now Pawn F finishes slightly later and finds node depleted
            var activeF = new ActiveIntentState { Intent = new HarvestResourceIntent(nodeF.Id, resId, pathF, 1.0f), ElapsedTime = 1.05f, ContestedCompetitorId = nodeE.Id };
            var resultF = simManager.ExecuteIntent(nodeF, activeF);
            Assert(resultF == IntentResult.TargetMissing, "Pawn F failed harvest with TargetMissing.");
            Assert(agentF.Frustration == 25f, $"Pawn F suffered +25 Frustration penalty (actual: {agentF.Frustration})");
            Assert(agentF.VisualState == AgentVisualState.Panicking, $"Pawn F assumed Panicking tantrum state (actual: {agentF.VisualState})");

            // Verify ContentionFaced memory event was logged
            bool fContentionLogged = false;
            for (int i = 0; i < agentF.Memory.EventLedger.Count; i++)
            {
                var evt = agentF.Memory.EventLedger.GetEvent(i);
                if (evt.EventType == MemoryEventType.ContentionFaced && evt.SubjectId == nodeE.Id)
                {
                    fContentionLogged = true;
                    break;
                }
            }
            Assert(fContentionLogged, "Pawn F logged ContentionFaced memory event.");

            // Verify dyadic penalty was applied
            var affF = agentF.Memory.GetAffinity(nodeE.Id, 0);
            Assert(affF.Warmth < -30, $"Pawn F warmth decreased after contested loss (actual: {affF.Warmth})");

            GameObject.DestroyImmediate(goE);
            GameObject.DestroyImmediate(goF);
        }

        private void ValidateYieldingVisualSpriteAndState()
        {
            var sprite0 = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Yielding, 0);
            var sprite1 = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Yielding, 1);

            Assert(sprite0 != null, "AgentSpriteLibrary returned non-null sprite for AgentVisualState.Yielding frame 0.");
            Assert(sprite1 != null, "AgentSpriteLibrary returned non-null sprite for AgentVisualState.Yielding frame 1.");
            Assert(sprite0.name == "Agent_Yield_0", $"Sprite 0 name is Agent_Yield_0 (actual: {sprite0.name})");
            Assert(sprite1.name == "Agent_Yield_1", $"Sprite 1 name is Agent_Yield_1 (actual: {sprite1.name})");
        }

        private void ValidateZeroAllocationContentionBenchmark()
        {
            var memory = new AgentMemory();
            var peerId = EntityId.Generate();
            memory.RecordInteraction(peerId, deltaWarmth: 10, deltaTrust: 20, deltaFear: 40, deltaRespect: 50, 0);

            // Warm-up to JIT compile methods
            for (int i = 0; i < 50; i++)
            {
                memory.CalculateSubmissionScore(peerId, 0);
            }

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                float score = memory.CalculateSubmissionScore(peerId, 0);
                if (score > 100f) score = 100f;
            }

            long endAlloc = GC.GetAllocatedBytesForCurrentThread();
            long totalAlloc = endAlloc - startAlloc;

            Assert(totalAlloc == 0, $"Strict zero-allocation benchmark failed! Allocated {totalAlloc} heap bytes across 1,000 contention checks.");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
