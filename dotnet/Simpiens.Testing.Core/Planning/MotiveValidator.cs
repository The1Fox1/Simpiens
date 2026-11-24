using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Domain;
using Simpiens.Simulation.Guild;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Planning
{
    /// <summary>
    /// Motives and families (Epic LIFE-1, L0.2, D1): every production goal declares what it is for and the part of the day it
    /// belongs to, as the plan's table has them (docs/life-model.md §2 carries the same table); a goal may name its family by
    /// context (loot under a clear's orders is unweighted). Runs as a test (ValidatorTests).
    /// </summary>
    public class MotiveValidator
    {
        // D1's table: a goal's name, its motive and its family. A goal added to the domain is added here with its row.
        private static readonly (string Goal, Motive Motive, ActivityFamily Family)[] Table =
        {
            ("SatiateHunger", Motive.Hunger, ActivityFamily.Unweighted),
            ("Rest", Motive.Rest, ActivityFamily.Rest),
            ("Socialize", Motive.Company, ActivityFamily.Social),
            ("AidCompanion", Motive.Aid, ActivityFamily.Unweighted),
            ("GatherAtAnchor", Motive.Company, ActivityFamily.Social),
            ("ComplyDirective", Motive.Duty, ActivityFamily.Unweighted),
            ("PurchaseProvisions", Motive.Hunger, ActivityFamily.Unweighted),
            ("OutfitForDanger", Motive.Gear, ActivityFamily.Work),
            ("EquipForWork", Motive.Gear, ActivityFamily.Work),
            ("ArmForFight", Motive.Gear, ActivityFamily.Work),
            ("CollectLoot", Motive.Income, ActivityFamily.Work),
            ("Rescue", Motive.Aid, ActivityFamily.Unweighted),
            ("Hunt", Motive.Income, ActivityFamily.Work),
            ("CarryMedicine", Motive.Gear, ActivityFamily.Work),
            ("Study", Motive.Growth, ActivityFamily.Leisure),
            ("ReadManual", Motive.Growth, ActivityFamily.Leisure),
            ("Drill", Motive.Growth, ActivityFamily.Leisure),
            ("Recreate", Motive.Fun, ActivityFamily.Leisure), // Epic LIFE-1 plan 2, L2.6
            ("LiquidateSalvage", Motive.Income, ActivityFamily.Work),
            ("FulfillBounty", Motive.Income, ActivityFamily.Work),
            ("EarnIncome", Motive.Income, ActivityFamily.Work),
            ("LeaveOutpost", Motive.Leave, ActivityFamily.Unweighted),
        };

        public void RunAllValidationTests()
        {
            TestLog.Log("[MotiveValidator] Beginning motive validation...");
            ValidateEveryGoalDeclares();
            ValidateTheTable();
            ValidateFamilyByContext();
            TestLog.Log("<color=green>[MotiveValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateEveryGoalDeclares()
        {
            foreach (var goal in OutpostGoapDomain.BuildGoals())
            {
                Assert(goal.Motive != Motive.None, $"{goal.Name} declares a motive.");
                Assert(Enum.IsDefined(typeof(Motive), goal.Motive), $"{goal.Name}: its motive is one of the enum's.");
                Assert(Enum.IsDefined(typeof(ActivityFamily), goal.Family), $"{goal.Name}: its family is one of the enum's.");
            }
        }

        private void ValidateTheTable()
        {
            var goals = OutpostGoapDomain.BuildGoals();
            Assert(goals.Count == Table.Length, $"D1's table has a row for each of the domain's {goals.Count} goals ({Table.Length} rows).");
            var byName = new Dictionary<string, GoapGoal>();
            foreach (var goal in goals) byName[goal.Name] = goal;
            foreach (var row in Table)
            {
                Assert(byName.TryGetValue(row.Goal, out var goal), $"{row.Goal} is in the domain.");
                Assert(goal.Motive == row.Motive, $"{row.Goal}'s motive is {row.Motive} ({goal.Motive}).");
                Assert(goal.Family == row.Family, $"{row.Goal}'s family is {row.Family} ({goal.Family}).");
            }
        }

        private void ValidateFamilyByContext()
        {
            GoapGoal loot = null;
            foreach (var goal in OutpostGoapDomain.BuildGoals()) if (goal.Name == "CollectLoot") loot = goal;
            var plain = new AgentContext(default, default, AgentNeeds.Default, 0f, null, null, 0);
            Assert(loot.FamilyFor(plain) == ActivityFamily.Work, "Loot gathered on its own is work.");
            var clear = new AgentContext(default, default, AgentNeeds.Default, 0f, null, null, 0,
                expedition: new ExpeditionOrder(ExpeditionStage.Fight, 1, default, default, ExpeditionKind.Clear));
            Assert(loot.FamilyFor(clear) == ActivityFamily.Unweighted, "Loot gathered under a clear's orders is unweighted, as orders are.");
            foreach (var goal in OutpostGoapDomain.BuildGoals())
            {
                if (goal.Name == "CollectLoot") continue;
                Assert(goal.FamilyFor(clear) == goal.Family, $"{goal.Name} keeps its family under any orders.");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MotiveValidator] FAILED: " + message);
        }
    }
}
