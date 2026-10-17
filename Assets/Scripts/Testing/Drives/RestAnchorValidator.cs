using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Domain;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Core;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Testing.Drives
{
    /// <summary>
    /// The rest anchor (Epic MAP Phase 3, epic §2.3): a tired crawler who knows a free bunk walks there and sleeps;
    /// one with no bunk, a taken bunk, or too little energy to walk rests where it stands. A bunk holds one sleeper,
    /// and a claim never outlives the sleep.
    /// </summary>
    public class RestAnchorValidator
    {
        private static readonly Vec2 Agent = Vec2.Zero;
        private static readonly EntityId AgentId = new EntityId(61u, 1u, 1u, 1u);
        private static readonly EntityId BunkId = new EntityId(62u, 2u, 2u, 2u);
        private static readonly EntityId HearthId = new EntityId(63u, 3u, 3u, 3u);

        public void RunAllValidationTests()
        {
            Debug.Log("[RestAnchorValidator] Beginning rest anchor validation...");

            ValidateTiredCrawlerWalksToAFreeBunk();
            ValidateFieldRestFallbacks();
            ValidateBunkBeatsTheHearthForSleep();
            ValidateSingleOccupancy();
            ValidateClaimsNeverOutliveTheSleep();
            ValidateLastStepOntoTheBunk();

            Debug.Log("<color=green>[RestAnchorValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTiredCrawlerWalksToAFreeBunk()
        {
            var plan = PlanRest(energy: 30f, bunkAt: new Vec2(10f, 0f), occupied: false, hearthAt: null);
            Assert(plan.Actions.Count == 2 && plan.Actions[0] is TravelToBunkAction && plan.Actions[1] is SleepAtBunkAction,
                $"A tired crawler who knows a free bunk plans TravelToBunk -> SleepAtBunk (got {Describe(plan)}).");

            var far = PlanRest(energy: 30f, bunkAt: new Vec2(40f, 0f), occupied: false, hearthAt: null);
            Assert(far.Actions.Count == 2 && far.Actions[0] is TravelToBunkAction, $"A bunk across the floor (40 units) still beats resting in place (got {Describe(far)}).");

            var atBunk = PlanRest(energy: 30f, bunkAt: new Vec2(0.3f, 0f), occupied: false, hearthAt: null);
            Assert(atBunk.Actions.Count == 1 && atBunk.Actions[0] is SleepAtBunkAction, $"Standing at the bunk, the crawler just sleeps (got {Describe(atBunk)}).");
            // A walk that ended a step short: the sleep makes the last step itself; further away it does not
            var sleep = new SleepAtBunkAction();
            var oneShort = sleep.CreateIntent(Context(energy: 30f, bunkAt: new Vec2(1f, 0f), occupied: false, hearthAt: null));
            Assert(oneShort is SleepAtBunkIntent s1 && s1.BunkId == BunkId, "One unit short of the bunk, the crawler still goes to sleep there (the handler makes the last step).");
            var tooFar = sleep.CreateIntent(Context(energy: 30f, bunkAt: new Vec2(2f, 0f), occupied: false, hearthAt: null));
            Assert(tooFar is IdleIntent, "Two units away is not a last step: the plan is re-evaluated instead.");
            Debug.Log("[PASS] Test 1: A tired crawler walks to a free bunk it knows and sleeps there, making the last step itself.");
        }

        private void ValidateFieldRestFallbacks()
        {
            var none = PlanRest(energy: 30f, bunkAt: null, occupied: false, hearthAt: null);
            Assert(none.Actions.Count == 1 && none.Actions[0] is RestAction, $"No bunk known: rest in place (got {Describe(none)}).");

            var taken = PlanRest(energy: 30f, bunkAt: new Vec2(10f, 0f), occupied: true, hearthAt: null);
            Assert(taken.Actions.Count == 1 && taken.Actions[0] is RestAction, $"The only bunk is taken: rest in place (got {Describe(taken)}).");

            var spent = PlanRest(energy: Considerations.TooTiredToWalkEnergy - 1f, bunkAt: new Vec2(10f, 0f), occupied: false, hearthAt: null);
            Assert(spent.Actions.Count == 1 && spent.Actions[0] is RestAction, $"Too tired to walk: rest in place (got {Describe(spent)}).");
            Debug.Log("[PASS] Test 2: With no free bunk, or too tired to walk, a crawler rests where it stands.");
        }

        private void ValidateBunkBeatsTheHearthForSleep()
        {
            var plan = PlanRest(energy: 30f, bunkAt: new Vec2(20f, 0f), occupied: false, hearthAt: new Vec2(3f, 0f));
            Assert(plan.Actions.Count == 2 && plan.Actions[0] is TravelToBunkAction,
                $"To sleep, a bunk 20 units away beats a nearby hearth (got {Describe(plan)}).");
            Debug.Log("[PASS] Test 3: For rest, the bunk is preferred over relaxing at a nearby hearth.");
        }

        private void ValidateSingleOccupancy()
        {
            var sim = new SimulationManager(new WorldRegistry(), null);
            var bunkPos = new Vec2(-6f, -8f);
            sim.RegisterBunk(BunkId, bunkPos);
            var other = new EntityId(64u, 4u, 4u, 4u);

            Assert(!sim.TryClaimBunk(BunkId, AgentId, bunkPos + new Vec2(2f, 0f)), "A bunk cannot be claimed from two units away.");
            Assert(sim.TryClaimBunk(BunkId, AgentId, bunkPos + new Vec2(0.4f, 0f)), "A bunk can be claimed by a crawler standing at it.");
            Assert(sim.TryClaimBunk(BunkId, AgentId, bunkPos), "Re-claiming one's own bunk succeeds.");
            Assert(!sim.TryClaimBunk(BunkId, other, bunkPos), "A second crawler cannot claim a taken bunk.");
            sim.ReleaseBunk(BunkId, other);
            Assert(sim.BunkOccupants.TryGetValue(BunkId, out var occupant) && occupant == AgentId, "Only the sleeper can release its bunk.");
            sim.ReleaseBunk(BunkId, AgentId);
            Assert(!sim.BunkOccupants.ContainsKey(BunkId) && sim.TryClaimBunk(BunkId, other, bunkPos), "Once released the bunk is free for the next crawler.");
            Assert(!sim.TryClaimBunk(new EntityId(65u, 5u, 5u, 5u), AgentId, bunkPos), "An unknown bunk cannot be claimed.");
            Debug.Log("[PASS] Test 4: A bunk holds one sleeper, standing at it.");
        }

        private void ValidateClaimsNeverOutliveTheSleep()
        {
            var sim = new SimulationManager(new WorldRegistry(), null);
            var bunkPos = new Vec2(-6f, -8f);
            sim.RegisterBunk(BunkId, bunkPos);

            Assert(sim.TryClaimBunk(BunkId, AgentId, bunkPos), "Precondition: claimed.");
            sim.ActiveIntents[AgentId] = new ActiveIntentState { Intent = new SleepAtBunkIntent(AgentId, BunkId, 3f), ElapsedTime = 0.5f };
            sim.ValidateBunkOccupancy();
            Assert(sim.BunkOccupants.ContainsKey(BunkId), "A crawler still sleeping keeps its bunk.");

            sim.ActiveIntents[AgentId] = new ActiveIntentState { Intent = new IdleIntent(AgentId, 1f) };
            sim.ValidateBunkOccupancy();
            Assert(!sim.BunkOccupants.ContainsKey(BunkId), "A sleeper preempted by another intent loses the bunk.");

            Assert(sim.TryClaimBunk(BunkId, AgentId, bunkPos), "Precondition: claimed again.");
            sim.ActiveIntents.Remove(AgentId);
            sim.ValidateBunkOccupancy();
            Assert(!sim.BunkOccupants.ContainsKey(BunkId), "A sleeper with no intent (removed, cancelled) loses the bunk.");

            sim.TryClaimBunk(BunkId, AgentId, bunkPos);
            sim.ResetState();
            Assert(!sim.BunkOccupants.ContainsKey(BunkId) && !sim.TryClaimBunk(BunkId, AgentId, bunkPos), "A session reset forgets bunks and claims.");
            Debug.Log("[PASS] Test 5: Bunk claims are dropped as soon as the sleep ends for any reason.");
        }

        /// <summary>
        /// Walks stop up to a unit short of their target (arrival tolerance, a pawn in the way); the sleeper then steps
        /// onto the bunk itself, unless someone else holds it.
        /// </summary>
        private void ValidateLastStepOntoTheBunk()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);
            var bunkPos = new Vec2(-2f, -8f);
            sim.RegisterBunk(BunkId, bunkPos);

            var go = new GameObject("[Test_Sleeper]");
            go.transform.position = (bunkPos + new Vec2(0.9f, 0.9f)).ToUnity();
            var node = go.AddComponent<Simpiens.Entities.NodeController>();
            node.Type = EntityType.Pawn;
            node.Initialize(AgentId, new Simpiens.Entities.NodeConfiguration(2f));
            registry.RegisterNode(node);
            try
            {
                var result = IntentResult.InProgress;
                int ticks = 0;
                while (result == IntentResult.InProgress && ticks++ < Simpiens.Cognition.Planning.Handlers.SleepAtBunkIntentHandler.MaxApproachTicks)
                {
                    result = sim.StepOntoBunk(node, BunkId);
                }
                Assert(result == IntentResult.Success && Vec2.Distance(node.Position, bunkPos) <= SimulationManager.BunkSettleDistance,
                    $"From 1.3 units away the sleeper steps onto the bunk's exact position ({result} after {ticks} ticks, at {node.Position}).");
                Assert(sim.TryClaimBunk(BunkId, AgentId, node.Position), "And can then claim it.");

                sim.ReleaseBunk(BunkId, AgentId);
                Assert(sim.TryClaimBunk(BunkId, new EntityId(66u, 6u, 6u, 6u), bunkPos), "Precondition: someone else takes it.");
                Assert(sim.StepOntoBunk(node, BunkId) == IntentResult.TargetMissing, "A bunk someone else holds is not stepped onto.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("[PASS] Test 6: A sleeper a step short walks onto a free bunk, never onto a taken one.");
        }

        private static GoapPlan PlanRest(float energy, Vec2? bunkAt, bool occupied, Vec2? hearthAt)
        {
            var context = Context(energy, bunkAt, occupied, hearthAt);
            var initial = WorldStateBuilder.BuildWorldState(context);
            var plan = new GoapPlan();
            bool ok = new GoapPlanner().Plan(initial, new RestGoal(), OutpostGoapDomain.BuildActionGraph(null), context, plan);
            Assert(ok, "A rest plan is found.");
            return plan;
        }

        private static AgentContext Context(float energy, Vec2? bunkAt, bool occupied, Vec2? hearthAt)
        {
            var memory = new AgentMemory();
            if (bunkAt.HasValue)
            {
                memory.SpatialMemoryMap[BunkId] = new SpatialMemoryRecord(BunkId, EntityType.Bunk, GridPoint.RoundToInt(bunkAt.Value), 1);
            }
            if (hearthAt.HasValue)
            {
                memory.SpatialMemoryMap[HearthId] = new SpatialMemoryRecord(HearthId, EntityType.Anchor, GridPoint.RoundToInt(hearthAt.Value), 1);
            }

            var snapshot = new SharedWorldSnapshot(100, 100, 1f, 8, null);
            if (occupied) snapshot.SetOccupiedBunks(new[] { BunkId });

            return new AgentContext(AgentId, Agent, new AgentNeeds(20f, energy, 60f), 0f, snapshot, memory, 10);
        }

        private static string Describe(GoapPlan plan)
        {
            var names = new string[plan.Actions.Count];
            for (int i = 0; i < names.Length; i++) names[i] = plan.Actions[i].Name;
            return string.Join(" -> ", names);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[RestAnchorValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
