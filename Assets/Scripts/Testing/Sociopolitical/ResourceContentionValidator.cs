using Simpiens.Testing.Environment;
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
            var goACrawler = HeadlessEntities.Crawler(null, simManager, clock, Vec2.Zero, id: EntityId.Generate(), name: "[Test_PawnA_Dominant]", register: false);
            var nodeA = goACrawler.Node;
            var agentA = goACrawler.Agent;
            registry.Register(nodeA);

            // Submissive Pawn B at (0, 0.5) fearing Pawn A
            var goBCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(0f, 0.5f), id: EntityId.Generate(), name: "[Test_PawnB_Submissive]", register: false);
            var nodeB = goBCrawler.Node;
            var agentB = goBCrawler.Agent;
            registry.Register(nodeB);

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
            Assert(nodeB.Position != new Vec2(0f, 0.5f), "Submissive Pawn B stepped backward away from dominant competitor.");
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
            HeadlessEntities.Remove(registry, goACrawler);
            HeadlessEntities.Remove(registry, goBCrawler);
        }

        private void ValidateRespectYielding()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var simManager = new SimulationManager(registry, null, clock);

            var resId = EntityId.Generate();
            simManager.RegisterResource(new ResourceData { EntityId = resId, RemainingYield = 5 });

            // Respected Elder C at (2, 2)
            var goCCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(2f, 2f), id: EntityId.Generate(), name: "[Test_PawnC_Elder]", register: false);
            var nodeC = goCCrawler.Node;
            var agentC = goCCrawler.Agent;
            registry.Register(nodeC);

            // Follower D at (2, 2.3) respecting Elder C
            var goDCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(2f, 2.3f), id: EntityId.Generate(), name: "[Test_PawnD_Follower]", register: false);
            var nodeD = goDCrawler.Node;
            var agentD = goDCrawler.Agent;
            registry.Register(nodeD);

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

            HeadlessEntities.Remove(registry, goCCrawler);
            HeadlessEntities.Remove(registry, goDCrawler);
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
            var goECrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(5f, 5f), id: EntityId.Generate(), name: "[Test_PawnE_Rival]", register: false);
            var nodeE = goECrawler.Node;
            var agentE = goECrawler.Agent;
            registry.Register(nodeE);

            var goFCrawler = HeadlessEntities.Crawler(null, simManager, clock, new Vec2(5f, 5.2f), id: EntityId.Generate(), name: "[Test_PawnF_Rival]", register: false);
            var nodeF = goFCrawler.Node;
            var agentF = goFCrawler.Agent;
            registry.Register(nodeF);

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

            HeadlessEntities.Remove(registry, goECrawler);
            HeadlessEntities.Remove(registry, goFCrawler);
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
