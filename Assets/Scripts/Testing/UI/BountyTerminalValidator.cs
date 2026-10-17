using System;
using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.UI;
using Simpiens.UI.Pooling;
using Simpiens.UI.Zones;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Testing.UI
{
    /// <summary>
    /// Automated domain validation suite for Epic UI-2: The Merchant Terminal & Bounty Board.
    /// Validates price updates via InfluenceEvents, bounty contract posting with treasury escrow,
    /// bounty cancellation with escrow refund, sector exploration and shroud tracking,
    /// zero-allocation pooling guarantees, and defensive null-safety.
    /// </summary>
    public class BountyTerminalValidator : IStartable
    {
        private readonly ISimulationManager _simulationManager;
        private readonly IPlayerInterventionService _interventionService;
        private readonly IFacilityZoneService _zoneService;

        [Inject]
        public BountyTerminalValidator(
            ISimulationManager simulationManager = null,
            IPlayerInterventionService interventionService = null,
            IFacilityZoneService zoneService = null)
        {
            _simulationManager = simulationManager;
            _interventionService = interventionService;
            _zoneService = zoneService;
        }

        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("<color=#38bdf8><b>[BountyTerminalValidator]</b> >>> Starting Epic UI-2 Domain Validation Suite <<<</color>");

            ValidatePriceUpdateInfluenceDispatch();
            ValidateBountyPostingAndEscrow();
            ValidateBountyCancellationAndRefund();
            ValidateSectorShroudTracking();
            ValidateZeroAllocationElementPool();
            ValidateDefensiveNullSafety();
            ValidateBountyTargetRules();

            Debug.Log("<color=#10b981><b>[BountyTerminalValidator]</b> >>> ALL 7 BOUNTY TERMINAL TESTS PASSED CLEANLY! <<<</color>");
        }

        public void ValidatePriceUpdateInfluenceDispatch()
        {
            var interventionService = new PlayerInterventionService();
            var simManager = new SimulationManager(
                worldRegistry: null,
                spatialPartition: null,
                clock: null,
                groupRegistry: null,
                chronicleService: null,
                sectors: null,
                interventionService: interventionService);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(
                stallId,
                position: Vector2.zero,
                rationSellPrice: 10,
                rationStock: 50,
                oreBuyPrice: 15,
                oreStock: 10,
                herbBuyPrice: 20,
                herbStock: 10,
                treasuryGold: 300,
                interactionRadius: 3f);

            simManager.RegisterTradeStall(stall);

            // Queue UpdateStallPrices command via PlayerInterventionService
            interventionService.QueueInfluence(new InfluenceEvent(
                "Player",
                "UpdateStallPrices",
                $"{stallId},25,35,45"));

            // Process tick to drain and execute intervention queue
            simManager.Tick();

            if (!simManager.TryGetTradeStall(stallId, out var updatedStall))
            {
                throw new Exception("Trade stall missing after price update tick!");
            }

            if (updatedStall.RationSellPrice != 25)
            {
                throw new Exception($"Expected RationSellPrice 25, got {updatedStall.RationSellPrice}");
            }
            if (updatedStall.OreBuyPrice != 35)
            {
                throw new Exception($"Expected OreBuyPrice 35, got {updatedStall.OreBuyPrice}");
            }
            if (updatedStall.HerbBuyPrice != 45)
            {
                throw new Exception($"Expected HerbBuyPrice 45, got {updatedStall.HerbBuyPrice}");
            }

            Debug.Log("  ✔ ValidatePriceUpdateInfluenceDispatch passed: Stall prices updated deterministically through InfluenceEvent pipeline.");
        }

        public void ValidateBountyPostingAndEscrow()
        {
            var interventionService = new PlayerInterventionService();
            var chronicle = new NarrativeChronicleService(capacity: 32);
            var simManager = new SimulationManager(
                worldRegistry: null,
                spatialPartition: null,
                clock: null,
                groupRegistry: null,
                chronicleService: chronicle,
                sectors: null,
                interventionService: interventionService);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(
                stallId,
                position: Vector2.zero,
                rationSellPrice: 15,
                rationStock: 50,
                oreBuyPrice: 20,
                oreStock: 10,
                herbBuyPrice: 25,
                herbStock: 10,
                treasuryGold: 250,
                interactionRadius: 3f);
            simManager.RegisterTradeStall(stall);

            // Post bounty with 50 gold reward
            // Payload format: "{bountyId},{type},{sectorId},{itemType},{qty},{rewardGold},{durationTicks}"
            interventionService.QueueInfluence(new InfluenceEvent(
                "Player",
                "PostBounty",
                "101,GatherResource,2,IronOre,2,50,1500"));

            simManager.Tick();

            if (simManager.ActiveBounties.Count != 1)
            {
                throw new Exception($"Expected 1 active bounty after posting, got {simManager.ActiveBounties.Count}");
            }

            if (!simManager.TryGetBounty(101, out var bounty))
            {
                throw new Exception("Bounty #101 was not registered in SimulationManager!");
            }

            if (bounty.Type != BountyType.GatherResource)
            {
                throw new Exception($"Expected BountyType GatherResource, got {bounty.Type}");
            }
            if (bounty.TargetSectorId != 2)
            {
                throw new Exception($"Expected TargetSectorId 2, got {bounty.TargetSectorId}");
            }
            if (bounty.TargetItemType != ItemType.IronOre)
            {
                throw new Exception($"Expected ItemType IronOre, got {bounty.TargetItemType}");
            }
            if (bounty.TargetQuantity != 2)
            {
                throw new Exception($"Expected TargetQuantity 2, got {bounty.TargetQuantity}");
            }
            if (bounty.RewardGold != 50)
            {
                throw new Exception($"Expected RewardGold 50, got {bounty.RewardGold}");
            }
            if (bounty.Status != BountyStatus.Open)
            {
                throw new Exception($"Expected Status Open, got {bounty.Status}");
            }

            // Verify escrow deducted 50 gold from stall treasury (250 - 50 = 200)
            if (!simManager.TryGetTradeStall(stallId, out var updatedStall))
            {
                throw new Exception("Trade stall disappeared after posting bounty!");
            }
            if (updatedStall.TreasuryGold != 200)
            {
                throw new Exception($"Expected treasury gold 200 after escrow, got {updatedStall.TreasuryGold}");
            }

            // Verify narrative chronicle recorded the event
            var events = new List<ChronicleEvent>(8);
            chronicle.GetAllRecentEvents(events, 5);
            if (events.Count == 0 || events[0].EventType != ChronicleEventType.BountyPosted)
            {
                throw new Exception("Narrative chronicle did not record BountyPosted event!");
            }

            Debug.Log("  ✔ ValidateBountyPostingAndEscrow passed: Bounty #101 registered with gold escrow deducted from outpost treasury.");
        }

        /// <summary>
        /// Epic MAP Phase 4: on a loaded map the simulation refuses a Scout bounty on a charted sector and any bounty on
        /// an unknown sector, before escrow; a Gather bounty may wait on an uncharted sector.
        /// </summary>
        public void ValidateBountyTargetRules()
        {
            var interventionService = new PlayerInterventionService();
            var sectors = new Simpiens.Simulation.World.SectorRegistry();
            sectors.Load(Simpiens.Core.Session.Layouts.Floor1Layout.Definition);
            var simManager = new SimulationManager(
                worldRegistry: null,
                spatialPartition: null,
                clock: null,
                groupRegistry: null,
                chronicleService: null,
                sectors: sectors,
                interventionService: interventionService);

            var stallId = new EntityId(9u, 9u, 9u, 9u);
            simManager.RegisterTradeStall(new TradeStallData(stallId, Vector2.zero, 15, 50, 20, 10, 25, 10, treasuryGold: 250, interactionRadius: 3f));

            byte hub = Simpiens.Core.Session.Layouts.Floor1Layout.HubSectorId;
            byte deepSeam = Simpiens.Core.Session.Layouts.Floor1Layout.DeepSeamSectorId;
            // Payload format: "{bountyId},{type},{sectorId},{itemType},{qty},{rewardGold},{durationTicks}"
            interventionService.QueueInfluence(new InfluenceEvent("Player", "PostBounty", $"201,ScoutSector,{hub},None,1,30,1500"));
            interventionService.QueueInfluence(new InfluenceEvent("Player", "PostBounty", "202,ScoutSector,99,None,1,30,1500"));
            interventionService.QueueInfluence(new InfluenceEvent("Player", "PostBounty", "203,GatherResource,99,IronOre,1,30,1500"));
            simManager.Tick();
            if (simManager.TryGetBounty(201, out _)) throw new Exception("A Scout bounty on the charted Hub was accepted.");
            if (simManager.TryGetBounty(202, out _) || simManager.TryGetBounty(203, out _)) throw new Exception("A bounty on an unknown sector was accepted.");
            if (!simManager.TryGetTradeStall(stallId, out var stall) || stall.TreasuryGold != 250) throw new Exception($"Refused bounties took escrow (treasury {stall.TreasuryGold}g, expected 250g).");
            if (BountyRules.CheckTarget(BountyType.ScoutSector, hub, sectors) != BountyRules.TargetCheck.AlreadyCharted
                || BountyRules.CheckTarget(BountyType.GatherResource, 99, sectors) != BountyRules.TargetCheck.UnknownSector)
            {
                throw new Exception("BountyRules does not report the refusal reasons the board shows.");
            }

            interventionService.QueueInfluence(new InfluenceEvent("Player", "PostBounty", $"204,ScoutSector,{deepSeam},None,1,30,1500"));
            interventionService.QueueInfluence(new InfluenceEvent("Player", "PostBounty", $"205,GatherResource,{deepSeam},IronOre,1,20,1500"));
            simManager.Tick();
            if (!simManager.TryGetBounty(204, out _)) throw new Exception("A Scout bounty on the uncharted Deep Seam was refused.");
            if (!simManager.TryGetBounty(205, out _)) throw new Exception("A Gather bounty waiting on the uncharted Deep Seam was refused.");

            sectors.SetExplored(deepSeam, true);
            if (BountyRules.CheckTarget(BountyType.ScoutSector, deepSeam, sectors) != BountyRules.TargetCheck.AlreadyCharted) throw new Exception("Once charted, the Deep Seam can't be scouted again.");
            if (BountyRules.CheckTarget(BountyType.ScoutSector, hub, null) != BountyRules.TargetCheck.Ok) throw new Exception("Without a registry nothing is checked (headless validators).");

            Debug.Log("  ✔ ValidateBountyTargetRules passed: impossible targets refused before escrow; Scout and Gather on an uncharted sector accepted.");
        }

        public void ValidateBountyCancellationAndRefund()
        {
            var interventionService = new PlayerInterventionService();
            var chronicle = new NarrativeChronicleService(capacity: 32);
            var simManager = new SimulationManager(
                worldRegistry: null,
                spatialPartition: null,
                clock: null,
                groupRegistry: null,
                chronicleService: chronicle,
                sectors: null,
                interventionService: interventionService);

            var stallId = EntityId.Generate();
            var stall = new TradeStallData(
                stallId,
                position: Vector2.zero,
                rationSellPrice: 15,
                rationStock: 50,
                oreBuyPrice: 20,
                oreStock: 10,
                herbBuyPrice: 25,
                herbStock: 10,
                treasuryGold: 250,
                interactionRadius: 3f);
            simManager.RegisterTradeStall(stall);

            // 1. Post bounty (250 -> 180 gold, 70 reward)
            interventionService.QueueInfluence(new InfluenceEvent(
                "Player",
                "PostBounty",
                "202,ScoutSector,6,None,1,70,2000"));
            simManager.Tick();

            simManager.TryGetTradeStall(stallId, out var escrowedStall);
            if (escrowedStall.TreasuryGold != 180)
            {
                throw new Exception($"Expected 180 treasury after escrow, got {escrowedStall.TreasuryGold}");
            }

            // 2. Cancel bounty
            interventionService.QueueInfluence(new InfluenceEvent(
                "Player",
                "CancelBounty",
                "202"));
            simManager.Tick();

            if (simManager.ActiveBounties.Count != 0)
            {
                throw new Exception($"Expected 0 active bounties after cancellation, got {simManager.ActiveBounties.Count}");
            }

            if (!simManager.TryGetBounty(202, out var cancelledBounty))
            {
                throw new Exception("Bounty #202 not found in registry!");
            }
            if (cancelledBounty.Status != BountyStatus.Cancelled)
            {
                throw new Exception($"Expected status Cancelled, got {cancelledBounty.Status}");
            }

            // 3. Verify gold refund (180 + 70 = 250)
            simManager.TryGetTradeStall(stallId, out var refundedStall);
            if (refundedStall.TreasuryGold != 250)
            {
                throw new Exception($"Expected treasury refunded to 250, got {refundedStall.TreasuryGold}");
            }

            Debug.Log("  ✔ ValidateBountyCancellationAndRefund passed: Cancelled contract refunded 70 gold back to outpost treasury.");
        }

        public void ValidateSectorShroudTracking()
        {
            var zoneService = new FacilityZoneService(Simpiens.Testing.World.TestWorlds.CommonsYardSectors());
            int market = Simpiens.Testing.World.CommonsYardFixture.OutpostMarketSectorId;
            int shrouded = Simpiens.Testing.World.CommonsYardFixture.OreRidgeSectorId;

            // The fallback and the market are explored by default
            if (!zoneService.IsSectorExplored(0)) throw new Exception("Sector 0 should be explored by default");
            if (!zoneService.IsSectorExplored(market)) throw new Exception($"Sector {market} should be explored by default");

            // Ore Ridge should start shrouded
            if (zoneService.IsSectorExplored(shrouded))
            {
                throw new Exception($"Sector {shrouded} should start shrouded/unexplored by default!");
            }

            // Explore it
            zoneService.SetSectorExplored(shrouded, true);
            if (!zoneService.IsSectorExplored(shrouded))
            {
                throw new Exception($"Sector {shrouded} was not marked as explored!");
            }

            // Shroud it again
            zoneService.SetSectorExplored(shrouded, false);
            if (zoneService.IsSectorExplored(shrouded))
            {
                throw new Exception($"Sector {shrouded} should be shrouded after being set to false!");
            }

            // Test bulk exploration
            zoneService.SetAllSectorsExplored(true);
            var allZones = zoneService.GetAllZones();
            for (int i = 0; i < allZones.Count; i++)
            {
                if (!zoneService.IsSectorExplored(allZones[i].ZoneId))
                {
                    throw new Exception($"Zone {allZones[i].ZoneId} was not explored after SetAllSectorsExplored(true)!");
                }
            }

            Debug.Log("  ✔ ValidateSectorShroudTracking passed: Sector fog of war and exploration status track dynamically.");
        }

        public void ValidateZeroAllocationElementPool()
        {
            var parent = new VisualElement();
            var pool = new VisualElementPool<BountyContract, VisualElement>(
                parent,
                factoryMethod: () =>
                {
                    var card = new VisualElement();
                    card.Add(new Label());
                    return card;
                },
                bindAction: (el, bounty, idx) =>
                {
                    var label = el.Q<Label>();
                    if (label != null) label.text = bounty.BountyId.ToString();
                },
                initialCapacity: 16);

            var contracts = new BountyContract[]
            {
                new BountyContract(1, BountyType.GatherResource, 2, ItemType.IronOre, 1, 25, 0, 1000),
                new BountyContract(2, BountyType.ScoutSector, 7, ItemType.None, 1, 15, 0, 2000),
                new BountyContract(3, BountyType.GatherResource, 4, ItemType.MedicinalHerb, 3, 40, 0, 1500),
                new BountyContract(4, BountyType.GatherResource, 1, ItemType.IronOre, 5, 50, 0, 3000)
            };

            // Warm up pool so all elements are pre-allocated
            pool.Sync(contracts);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(true);

            // Execute 1,000 sync refreshes simulating 100 seconds of 10 Hz UI updates
            for (int i = 0; i < 1000; i++)
            {
                pool.Sync(contracts);
            }

            long memoryAfter = GC.GetTotalMemory(false);
            long allocated = memoryAfter - memoryBefore;

            if (allocated > 0)
            {
                throw new Exception($"Zero-allocation pool guarantee violated! Allocated {allocated} bytes across 1,000 sync passes.");
            }

            Debug.Log("  ✔ ValidateZeroAllocationElementPool passed: 1,000 VisualElementPool sync cycles produced exactly 0 bytes GC heap allocation.");
        }

        public void ValidateDefensiveNullSafety()
        {
            // Presenter headless construction with all nulls
            var go = new GameObject("[HeadlessPresenterNullTest]");
            try
            {
                var presenter = go.AddComponent<FacilityDashboardPresenter>();
                presenter.Construct(
                    zoneService: null,
                    worldRegistry: null,
                    simulationClock: null,
                    simulationManager: null,
                    interventionService: null,
                    chronicleService: null,
                    identityService: null,
                    narrativeFormatter: null);

                // These methods must not throw NullReferenceException when dependencies are null
                presenter.HandleApplyPrices();
                presenter.HandlePostBounty();
                presenter.HandleCancelBounty(999);
                presenter.ToggleMerchantTerminal();
                presenter.ToggleBountyBoard();
                presenter.SelectZone(1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            Debug.Log("  ✔ ValidateDefensiveNullSafety passed: Headless presentation operations safely no-op under null dependencies.");
        }
    }
}
