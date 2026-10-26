using System;
using System.Collections.Generic;
using System.Text;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// What the player reads for a crawler's goal and action (review U5): a table of display names, not the planner's
    /// type names, so renaming a class doesn't change the dashboard and the words can be translated later. A type the
    /// table doesn't know yet reads as its name in words ("FooBarAction" as "Foo bar"); a test keeps the table complete.
    /// </summary>
    public static class PlanText
    {
        private static readonly Dictionary<Type, string> Names = new Dictionary<Type, string>
        {
            // Goals
            [typeof(AidCompanionGoal)] = "Aid a companion",
            [typeof(ComplyDirectiveGoal)] = "Follow the group's directive",
            [typeof(EarnIncomeGoal)] = "Earn income",
            [typeof(FulfillBountyGoal)] = "Fulfill a bounty",
            [typeof(GatherAtAnchorGoal)] = "Gather at the hearth",
            [typeof(LiquidateSalvageGoal)] = "Sell salvage",
            [typeof(PurchaseProvisionsGoal)] = "Buy provisions",
            [typeof(OutfitForDangerGoal)] = "Gear up for danger",
            [typeof(RestGoal)] = "Rest",
            [typeof(SatiateHungerGoal)] = "Satisfy hunger",
            [typeof(SocializeGoal)] = "Socialize",
            [typeof(LeaveOutpostGoal)] = "Move on",

            // Actions
            [typeof(BuyProvisionsAction)] = "Buying provisions",
            [typeof(BuyHazardGearAction)] = "Buying hazard gear",
            [typeof(ClaimBountyAction)] = "Claiming a bounty",
            [typeof(ClaimGatherBountyAction)] = "Claiming a gather bounty",
            [typeof(ClaimScoutBountyAction)] = "Claiming a scout bounty",
            [typeof(DeliverBountyAction)] = "Delivering a bounty",
            [typeof(EatCarriedFoodAction)] = "Eating",
            [typeof(ExecuteDirectiveAction)] = "Carrying out a directive",
            [typeof(GatherSalvageAction)] = "Gathering salvage",
            [typeof(HarvestResourceAction)] = "Harvesting",
            [typeof(RelaxAtAnchorAction)] = "Relaxing at the hearth",
            [typeof(ReportScoutBountyAction)] = "Reporting a survey",
            [typeof(RestAction)] = "Resting",
            [typeof(ScoutSectorAction)] = "Scouting",
            [typeof(SearchResourceAction)] = "Searching for a resource",
            [typeof(SearchSalvageAction)] = "Searching for salvage",
            [typeof(SellSalvageAction)] = "Selling salvage",
            [typeof(ShareFoodAction)] = "Sharing food",
            [typeof(SleepAtBunkAction)] = "Sleeping",
            [typeof(TravelToAnchorAction)] = "Going to the hearth",
            [typeof(TravelToBunkAction)] = "Going to bed",
            [typeof(TravelToDirectiveAction)] = "Going to a directive's site",
            [typeof(TravelToPeerAction)] = "Going to a peer",
            [typeof(TravelToResourceAction)] = "Going to a resource",
            [typeof(TravelToStallAction)] = "Going to the stall",
            [typeof(LeaveOutpostAction)] = "Leaving by the Road Gate",
        };

        /// <summary>The goal in words, or "(None)".</summary>
        public static string Goal(GoapGoal goal) => goal != null ? Of(goal.GetType()) : "(None)";

        /// <summary>The action in words, or "(Idle)".</summary>
        public static string Action(GoapAction action) => action != null ? Of(action.GetType()) : "(Idle)";

        /// <summary>True when the table names <paramref name="type"/> (tests: every goal and action is named).</summary>
        public static bool IsNamed(Type type) => Names.ContainsKey(type);

        private static string Of(Type type) => Names.TryGetValue(type, out var name) ? name : Words(type.Name);

        /// <summary>"TravelToStallAction" as "Travel to stall": the suffix dropped, words split at capitals.</summary>
        public static string Words(string typeName)
        {
            string name = typeName.EndsWith("Action", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 6)
                : typeName.EndsWith("Goal", StringComparison.Ordinal) ? typeName.Substring(0, typeName.Length - 4) : typeName;
            var words = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c))
                {
                    words.Append(' ');
                    c = char.ToLowerInvariant(c);
                }
                words.Append(c);
            }
            return words.ToString();
        }
    }
}
