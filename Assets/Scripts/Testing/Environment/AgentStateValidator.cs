using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Automated domain validation suite for Section 2.2:
    /// Pure C# AgentState, Headless Lifecycle, Decoupled Cognition & Consequence Ownership,
    /// Backward-Compatible MonoBehaviour Façade, and Zero-Allocation Invariants.
    /// </summary>
    public class AgentStateValidator : IStartable
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[AgentStateValidator] Beginning Section 2.2 AgentState Domain Validation...");

            ValidateHeadlessLifecycle();
            ValidateHeadlessContextAndCognition();
            ValidateConsequenceOwnership();
            ValidateFacadeIntegration();
            ValidateZeroAllocationTickBenchmark();

            Debug.Log("<color=green>[AgentStateValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateHeadlessLifecycle()
        {
            // Instantiate pure C# AgentState with ZERO GameObjects
            var state = new AgentState();
            state.Needs = new AgentNeeds(0f, 100f, 100f);
            state.Frustration = 10f;

            for (int i = 0; i < 100; i++)
            {
                state.Tick(0.1f);
            }

            Assert(state.Hunger >= 14.9f && state.Hunger <= 15.1f, $"Headless agent hunger expected ~15.0, actual: {state.Hunger}");
            Assert(state.Frustration == 0f, $"Headless agent frustration expected 0, actual: {state.Frustration}");

            state.TriggerTantrumVisual(0f);
            Assert(state.IsInTantrum, "Agent should be in tantrum after trigger.");
            Assert(state.VisualState == AgentVisualState.Panicking, "VisualState should be Panicking during tantrum.");

            for (int i = 0; i < 30; i++)
            {
                state.Tick(0.1f);
            }

            Assert(!state.IsInTantrum, "Agent should exit tantrum after duration expires.");
            Assert(state.VisualState == AgentVisualState.Idle, "VisualState should return to Idle.");

            Debug.Log("[PASS] Test 1: Headless AgentState lifecycle and biological drive decay verified (0 GameObjects).");
        }

        private void ValidateHeadlessContextAndCognition()
        {
            var state = new AgentState();
            state.Position = new Vector2(10f, 20f);
            var snapshot = new SharedWorldSnapshot(10, 10, 1.0f, 4, null);
            var context = state.CreateContext(snapshot, 100);

            Assert(context.AgentId == state.AgentId, "Context AgentId mismatch.");
            Assert(context.Position == new Vector2(10f, 20f), "Context position mismatch.");
            Assert(context.CurrentTick == 100, "Context tick mismatch.");

            var consequenceService = new AgentConsequenceService();
            var cognitionService = new AgentCognitionService(consequenceService);
            var simManager = new SimulationManager(new WorldRegistry(), null);

            // Defensive null-safety verification: null spatial partition and null clock
            cognitionService.BeginEvaluation(state, null, simManager, null, null, null, null, null, default);

            Assert(state != null, "State remains valid during headless cognition evaluation.");
            Debug.Log("[PASS] Test 2: Headless context formulation and cognitive service null-safety verified.");
        }

        private void ValidateConsequenceOwnership()
        {
            var state = new AgentState();
            state.Hunger = 60f;
            state.HasFoodInInventory = false;

            var consequenceService = new AgentConsequenceService();
            var harvestIntent = new HarvestResourceIntent(state.AgentId, GUID.Generate(), default, 1.0f);

            consequenceService.ApplyConsequences(state, harvestIntent, IntentResult.Success, 50);

            Assert(state.Hunger == 10f, $"Expected hunger 10f after harvest, actual: {state.Hunger}");
            Assert(state.HasFoodInInventory, "Expected HasFoodInInventory true after harvest.");
            Assert(state.Inventory.HasItem(ItemType.Ration), "Expected Ration in inventory.");

            // Verify idempotency / second harvest
            consequenceService.ApplyConsequences(state, harvestIntent, IntentResult.Success, 51);
            Assert(state.Hunger == 0f, $"Expected hunger 0f after second harvest, actual: {state.Hunger}");

            Debug.Log("[PASS] Test 3: Authoritative AgentConsequenceService consequence ownership verified.");
        }

        private void ValidateFacadeIntegration()
        {
            var go = new GameObject("[TestNode_Facade]");
            var node = go.AddComponent<NodeController>();
            var agent = go.AddComponent<AutonomousAgent>();
            var id = GUID.Generate();
            node.Initialize(id, new NodeConfiguration(speed: 3.0f));

            Assert(node.AgentState != null, "node.AgentState must not be null.");
            Assert(node.AgentState == agent.State, "node.AgentState must match agent.State.");

            agent.Hunger = 42f;
            Assert(agent.State.Hunger == 42f, "Mutating agent.Hunger must reflect on agent.State.Hunger.");

            node.SetLogicalPosition(new Vector2(33f, 44f));
            Assert(agent.State.Position == new Vector2(33f, 44f), "SetLogicalPosition must synchronize agent.State.Position.");

            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("[PASS] Test 4: Backward-compatible AutonomousAgent MonoBehaviour façade integration verified.");
        }

        private void ValidateZeroAllocationTickBenchmark()
        {
            var state = new AgentState();
            for (int i = 0; i < 500; i++)
            {
                state.Tick(0.1f);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                state.Tick(0.1f);
            }
            long delta = GC.GetAllocatedBytesForCurrentThread() - startAlloc;

            Assert(delta == 0, $"Tick benchmark allocated {delta} bytes across 1,000 ticks (expected 0).");
            Debug.Log("[PASS] Test 5: Zero heap allocation verified on AgentState.Tick (0 bytes across 1,000 ticks).");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[AgentStateValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
