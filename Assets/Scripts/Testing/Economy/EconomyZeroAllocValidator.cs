using System;
using System.Collections.Generic;
using System.Threading;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Epic 9 §6 item 3 (Milestone 1 completion plan, step 1): the economy allocates nothing in steady state.
    /// 1,000 simulation ticks of trading and bounties on the main thread, and 1,000 rounds of the trade and bounty
    /// evaluation that cognition runs off-thread, each measured with GC.GetAllocatedBytesForCurrentThread.
    /// Log channels are silenced while measuring: the rule is that a disabled log call allocates nothing, and an
    /// enabled one formats a line for its sinks.
    /// </summary>
    public class EconomyZeroAllocValidator
    {
        public const int Ticks = 1000;

        private const string PlayerSource = "Player";
        private const ushort StandingGatherId = 1;
        private const ushort StandingScoutId = 2;
        private const ushort DeliveredId = 3;
        private const ushort ExpiringId = 4;
        private const ushort CancelledId = 5;

        public void RunAllValidationTests()
        {
            Debug.Log("[EconomyZeroAllocValidator] Beginning economy zero-allocation validation...");

            var previousLog = SimLog.Settings;
            SimLog.Shutdown();
            try
            {
                ValidateTradingAndBountyTicksAllocateNothing();
                ValidateOffThreadEvaluationAllocatesNothing();
            }
            finally
            {
                if (previousLog != null) SimLog.Configure(previousLog);
            }

            Debug.Log("<color=green>[EconomyZeroAllocValidator] ALL 2 TESTS PASSED SUCCESSFULLY!</color>");
        }

        /// <summary>
        /// Every tick: the player changes prices, posts a bounty and cancels it; a crawler claims a Gather bounty,
        /// delivers its ore for the escrowed reward, buys a ration and sells ore; a bounty nobody takes expires and is
        /// refunded; and the simulation tick scans the board, runs the supply service and applies the commands.
        /// </summary>
        private void ValidateTradingAndBountyTicksAllocateNothing()
        {
            var registry = new WorldRegistry();
            var clock = new SimulationClock();
            var ledger = new EconomyLedger();
            var interventions = new PlayerInterventionService();
            var sim = new SimulationManager(registry, null, clock, new GroupRegistry(), new NarrativeChronicleService(),
                interventionService: interventions, economyLedger: ledger, supplyService: new SupplyService());
            sim.SetEconomy(EconomyTuning.Default);

            var stallId = new EntityId(41u, 42u, 43u, 44u);
            sim.RegisterTradeStall(new TradeStallData(stallId, Vec2.Zero, rationSellPrice: 5, rationStock: 5000,
                oreBuyPrice: 8, herbBuyPrice: 12, treasuryGold: 1000000));
            // A standing board, scanned for expiry on every tick
            sim.RegisterBounty(new BountyContract(StandingGatherId, BountyType.GatherResource, 0, ItemType.IronOre, 1, 10, 0, uint.MaxValue - 1, creatorStallId: stallId));
            sim.RegisterBounty(new BountyContract(StandingScoutId, BountyType.ScoutSector, 0, ItemType.None, 1, 10, 0, uint.MaxValue - 1, creatorStallId: stallId));

            var go = new GameObject("[EconomyZeroAlloc_Crawler]");
            try
            {
                var node = go.AddComponent<NodeController>();
                node.Initialize(new EntityId(51u, 52u, 53u, 54u), new NodeConfiguration(2f));
                node.transform.position = (new Vec2(0f, 0.5f)).ToUnity(); // within the stall's reach
                var agent = go.AddComponent<AutonomousAgent>();
                agent.Initialize(node.Id, null, null, sim, clock);
                agent.Inventory.Gold = 1000000;
                registry.RegisterNode(node);

                var buy = Active(new TradeIntent(node.Id, stallId, TradeActionType.BuyItem, ItemType.Ration, 1), node);
                var sell = Active(new TradeIntent(node.Id, stallId, TradeActionType.SellItem, ItemType.IronOre, 1), node);
                var claim = Active(new ClaimBountyIntent(node.Id, DeliveredId), node);
                var deliver = Active(new DeliverBountyIntent(node.Id, DeliveredId, stallId), node);
                var ore = new ItemInstance((ushort)ItemType.IronOre, ItemType.IronOre, 1, 8);
                int failures = 0;

                for (int i = 0; i < 20; i++) PlayOneTick(); // warm up: dictionary entries, chronicle sector buffers
                var flowsBefore = ledger.Flows;
                int failuresBefore = failures;

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < Ticks; i++) PlayOneTick();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                var flows = ledger.Flows;
                Assert(failures == failuresBefore && failures == 0, $"{failures} trade or bounty steps failed; every step must succeed for the measurement to mean anything.");
                Assert(flows.Purchases - flowsBefore.Purchases == Ticks && flows.Sales - flowsBefore.Sales == Ticks,
                    $"Expected {Ticks} purchases and sales, saw {flows.Purchases - flowsBefore.Purchases} and {flows.Sales - flowsBefore.Sales}.");
                Assert(flows.EscrowReleasedGold - flowsBefore.EscrowReleasedGold == Ticks * 10u, "Every delivery was paid from escrow.");
                Assert(flows.EscrowRefundedGold - flowsBefore.EscrowRefundedGold == Ticks * 20u, "Every expired and cancelled bounty was refunded.");
                Assert(sim.TryGetBounty(ExpiringId, out var expired) && expired.Status == BountyStatus.Expired, "The untaken bounty expired.");
                Assert(sim.TryGetBounty(CancelledId, out var cancelled) && cancelled.Status == BountyStatus.Cancelled, "The posted bounty was cancelled.");
                Assert(allocated == 0, $"{Ticks} ticks of trading and bounties allocated {allocated} bytes (expected 0).");

                Debug.Log($"[PASS] Test 1: {Ticks} ticks of trades, bounty claims, deliveries, expiries, player commands and supply ticks allocate 0 bytes.");

                void PlayOneTick()
                {
                    uint tick = sim.CurrentTick;

                    // The player's commands are drained by the next simulation tick: a price change, a post and its cancel
                    interventions.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices(PlayerSource,
                        new UpdateStallPricesCommand(stallId, (ushort)(5 + tick % 3), 8, 12)));
                    interventions.QueueInfluence(InfluenceEvent.CreatePostBounty(PlayerSource,
                        new PostBountyCommand(CancelledId, BountyType.GatherResource, 0, ItemType.MedicinalHerb, 1, 10, 500, default, stallId)));
                    interventions.QueueInfluence(InfluenceEvent.CreateCancelBounty(PlayerSource, CancelledId));

                    // A crawler at the stall: claim a Gather bounty, deliver its ore, buy a ration, sell ore
                    sim.RegisterBounty(new BountyContract(DeliveredId, BountyType.GatherResource, 0, ItemType.IronOre, 1, 10, tick, tick + 1000, creatorStallId: stallId));
                    Count(sim.ExecuteIntent(node, claim));
                    agent.Inventory.TryAddItem(ore, out _);
                    Count(sim.ExecuteIntent(node, deliver));
                    Count(sim.ExecuteIntent(node, buy));
                    agent.Inventory.TryAddItem(ore, out _);
                    Count(sim.ExecuteIntent(node, sell));

                    // A bounty nobody takes: it expires on the next tick and its escrow goes back to the stall
                    sim.RegisterBounty(new BountyContract(ExpiringId, BountyType.GatherResource, 0, ItemType.IronOre, 1, 10, tick, tick, creatorStallId: stallId));

                    // Bookkeeping, not under test: keep the pack and the stall from filling up
                    agent.Inventory.TryRemoveItem(ItemType.Ration, 1);
                    sim.TryGetTradeStall(stallId, out var stall);
                    sim.UpdateStall(stall.WithStock(5000, 0, 0));

                    clock.AdvanceTicks(1);
                }

                void Count(IntentResult result)
                {
                    if (result != IntentResult.Success) failures++;
                }
            }
            finally
            {
                sim.Dispose();
                GameObject.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// What cognition evaluates off the main thread for trade and bounties: the world-state facts, bounty choice
        /// and scoring, reservation and minimum prices, and the economic goals' priorities and actions' costs. Runs on
        /// a worker thread against a snapshot, as background cognition does.
        /// </summary>
        private void ValidateOffThreadEvaluationAllocatesNothing()
        {
            var agentId = new EntityId(61u, 62u, 63u, 64u);
            var stallId = new EntityId(71u, 72u, 73u, 74u);

            var snapshot = new SharedWorldSnapshot(40, 40, 1f, 8, _ => { });
            snapshot.SetEconomy(EconomyTuning.Default);
            snapshot.SetStalls(new[] { new TradeStallData(stallId, new Vec2(2f, 0f), rationSellPrice: 15, rationStock: 20, treasuryGold: 300) }, 1);
            snapshot.SetBounties(new[]
            {
                new BountyContract(1, BountyType.GatherResource, 0, ItemType.IronOre, 2, 25, 0, 100000, new Vec2(5f, 5f), creatorStallId: stallId),
                new BountyContract(2, BountyType.ScoutSector, 0, ItemType.None, 1, 15, 0, 100000, new Vec2(-5f, 5f), creatorStallId: stallId),
                new BountyContract(3, BountyType.GatherResource, 0, ItemType.MedicinalHerb, 1, 30, 0, 100000, new Vec2(8f, -2f), agentId, stallId, BountyStatus.Claimed),
            }, 3);

            var state = new AgentState(agentId) { Position = new Vec2(1f, 0f), Hunger = 65f, Personality = new AgentPersonality(0.4f, 0f, -0.2f, 0f, 0.3f) };
            state.Inventory.Gold = 40;
            state.Inventory.TryAddItem(new ItemInstance((ushort)ItemType.IronOre, ItemType.IronOre, 2, 8), out _);
            state.Inventory.TryAddItem(new ItemInstance((ushort)ItemType.MedicinalHerb, ItemType.MedicinalHerb, 1, 12), out _);
            var context = state.CreateContext(snapshot, 500); // main thread, as in production

            var goals = new List<GoapGoal> { new PurchaseProvisionsGoal(), new LiquidateSalvageGoal(), new FulfillBountyGoal(), new EarnIncomeGoal() };
            var actions = new List<GoapAction>
            {
                new TravelToStallAction(), new BuyProvisionsAction(), new SellSalvageAction(), new ClaimGatherBountyAction(),
                new ClaimScoutBountyAction(), new DeliverBountyAction(), new ReportScoutBountyAction()
            };

            long allocated = -1;
            int openFound = 0, claimedFound = 0;
            float checksum = 0f;
            Exception failure = null;

            var worker = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < 20; i++) Evaluate(); // warm up on this thread
                    openFound = claimedFound = 0;
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < Ticks; i++) Evaluate();
                    allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            worker.Start();
            worker.Join();

            if (failure != null) throw new Exception("[ASSERTION FAILED] The off-thread evaluation threw.", failure);
            Assert(openFound == Ticks && claimedFound == Ticks, $"Every round found an open bounty and the claimed one ({openFound}, {claimedFound} of {Ticks}).");
            Assert(allocated == 0, $"{Ticks} rounds of off-thread trade and bounty evaluation allocated {allocated} bytes (expected 0).");

            Debug.Log($"[PASS] Test 2: {Ticks} rounds of off-thread trade and bounty evaluation allocate 0 bytes (checksum {checksum:F1}).");

            void Evaluate()
            {
                var facts = WorldStateBuilder.BuildWorldState(context, context.Inventory);
                checksum += facts.Get(StateFact.HasSalvageInInventory) ? 1f : 0f;

                if (BountyQueries.TryGetOpenBounty(context, BountyType.None, out var open))
                {
                    openFound++;
                    checksum += open.RewardGold;
                }
                if (BountyQueries.TryGetClaimedBounty(context, out var claimed))
                {
                    claimedFound++;
                    checksum += BountyQueries.HasBountyItems(context, claimed) ? 1f : 0f;
                }

                checksum += Considerations.RationReservationPrice(context)
                    + Considerations.MinimumSalePrice(context, ItemType.IronOre)
                    + Considerations.MinimumSalePrice(context, ItemType.MedicinalHerb);

                for (int g = 0; g < goals.Count; g++)
                {
                    if (goals[g].IsValid(context)) checksum += goals[g].CalculatePriority(context);
                }
                for (int a = 0; a < actions.Count; a++)
                {
                    if (actions[a].IsValid(context)) checksum += actions[a].GetCost(context);
                }
            }
        }

        private static ActiveIntentState Active(AgentIntent intent, NodeController node)
        {
            return new ActiveIntentState { Intent = intent, CurrentWaypointIndex = 0, LastSampledPosition = node.Position };
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
