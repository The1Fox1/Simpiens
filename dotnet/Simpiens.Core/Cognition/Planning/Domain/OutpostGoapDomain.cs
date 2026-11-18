using System.Collections.Generic;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;

namespace Simpiens.Cognition.Planning.Domain
{
    /// <summary>
    /// The production GOAP domain for the outpost session: every action agents can sequence and every goal they can
    /// pursue. Previously assembled inside the test harness (SwarmSpawner, ROADMAP §1A.2 #9).
    /// </summary>
    public static class OutpostGoapDomain
    {
        public static ActionGraph BuildActionGraph(IAsyncPathfinder pathfinder)
        {
            var graph = new ActionGraph();
            graph.RegisterAction(new SearchResourceAction(pathfinder: pathfinder));
            graph.RegisterAction(new TravelToResourceAction(pathfinder: pathfinder));
            graph.RegisterAction(new HarvestResourceAction(pathfinder: pathfinder));
            graph.RegisterAction(new EatCarriedFoodAction());
            graph.RegisterAction(new RestAction());
            graph.RegisterAction(new TravelToBunkAction(pathfinder));
            graph.RegisterAction(new SleepAtBunkAction());
            graph.RegisterAction(new TravelToPeerAction());
            graph.RegisterAction(new ShareFoodAction(pathfinder: pathfinder));
            graph.RegisterAction(new TravelToAnchorAction(pathfinder));
            graph.RegisterAction(new RelaxAtAnchorAction());
            graph.RegisterAction(new TravelToDirectiveAction(pathfinder));
            graph.RegisterAction(new ExecuteDirectiveAction());
            graph.RegisterAction(new WorkExpeditionNodeAction(pathfinder));
            graph.RegisterAction(new WorkStationAction(pathfinder));
            graph.RegisterAction(new TravelToStallAction(pathfinder));
            graph.RegisterAction(new BuyProvisionsAction());
            graph.RegisterAction(new BuyHazardGearAction());
            graph.RegisterAction(new BuyToolAction());
            graph.RegisterAction(new BuyWeaponAction()); // Epic 10 Part 3, K3.3
            graph.RegisterAction(new BuyDraughtAction());
            graph.RegisterAction(new SellSalvageAction());
            graph.RegisterAction(new ClaimGatherBountyAction());
            graph.RegisterAction(new ClaimScoutBountyAction());
            graph.RegisterAction(new DeliverBountyAction());
            graph.RegisterAction(new SearchSalvageAction(pathfinder));
            graph.RegisterAction(new GatherSalvageAction(pathfinder));
            graph.RegisterAction(new ScoutSectorAction(pathfinder));
            graph.RegisterAction(new ReportScoutBountyAction());
            graph.RegisterAction(new LeaveOutpostAction(pathfinder));
            graph.RegisterAction(new PickUpLootAction(pathfinder)); // Epic 10 Part 3, K4.3
            graph.RegisterAction(new RescueAction(pathfinder)); // K5.3
            graph.RegisterAction(new ClaimHuntAction()); // K6.2
            graph.RegisterAction(new HuntLairAction(pathfinder));
            graph.RegisterAction(new FightForPartyAction(pathfinder)); // K7.2a
            graph.RegisterAction(new BuyManualAction()); // Epic 11, S3.3
            graph.RegisterAction(new ReadManualAction());
            graph.RegisterAction(new DrillAction(pathfinder)); // Epic 11, S3.4
            return graph;
        }

        public static List<GoapGoal> BuildGoals()
        {
            return new List<GoapGoal>
            {
                new SatiateHungerGoal(),
                new RestGoal(),
                new SocializeGoal(),
                new AidCompanionGoal(),
                new GatherAtAnchorGoal(),
                new ComplyDirectiveGoal(),
                new PurchaseProvisionsGoal(),
                new OutfitForDangerGoal(),
                new EquipForWorkGoal(),
                new ArmForFightGoal(), // Epic 10 Part 3, K3.3
                new CollectLootGoal(), // Epic 10 Part 3, K4.3
                new RescueGoal(), // K5.3
                new HuntGoal(), // K6.2
                new CarryMedicineGoal(),
                new StudyGoal(), // Epic 11, S3.3
                new ReadManualGoal(),
                new DrillGoal(), // Epic 11, S3.4
                new LiquidateSalvageGoal(),
                new FulfillBountyGoal(),
                new EarnIncomeGoal(),
                new LeaveOutpostGoal()
            };
        }
    }
}
