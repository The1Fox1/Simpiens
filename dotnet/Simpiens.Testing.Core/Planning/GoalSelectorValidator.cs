using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Planning
{
    /// <summary>
    /// The one goal selector (Epic LIFE-1, L0.4, D3): the best three, highest first; ties to the earlier goal; invalid and
    /// satisfied goals and priorities of -1 or less never ranked; and a choice allocates nothing. Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class GoalSelectorValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[GoalSelectorValidator] Beginning goal selector validation...");
            ValidateTheRanking();
            ValidateTies();
            ValidateWhatIsNeverRanked();
            ValidateNoAllocation();
            ValidateTheFallback();
            TestLog.Log("<color=green>[GoalSelectorValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static AgentContext Context() => new AgentContext(default, default, AgentNeeds.Default, 0f, null, null, 0);

        private void ValidateTheRanking()
        {
            var goals = new List<GoapGoal> { new Fixed("A", 10f), new Fixed("B", 40f), new Fixed("C", 25f), new Fixed("D", 30f), new Fixed("E", 5f) };
            var ranked = GoalSelector.Rank(goals, Context(), WorldState.Empty);
            Assert(ranked.Count == 3, $"Three ranked ({ranked.Count}).");
            Assert(ranked.Goal1.Name == "B" && ranked.Goal2.Name == "D" && ranked.Goal3.Name == "C", $"Highest first: B, D, C ({ranked.Goal1?.Name}, {ranked.Goal2?.Name}, {ranked.Goal3?.Name}).");
            Assert(ranked.Priority1 == 40f && ranked.Priority2 == 30f && ranked.Priority3 == 25f, "Each with its priority.");
            Assert(ranked.GoalAt(1) == ranked.Goal2 && ranked.PriorityAt(2) == 25f && ranked.GoalAt(3) == null, "Read by place.");
            var one = GoalSelector.Rank(new List<GoapGoal> { new Fixed("Only", 3f) }, Context(), WorldState.Empty);
            Assert(one.Count == 1 && one.Goal1.Name == "Only" && one.Priority2 == -1f, "One goal fills one place.");
        }

        private void ValidateTies()
        {
            var goals = new List<GoapGoal> { new Fixed("First", 20f), new Fixed("Second", 20f), new Fixed("Third", 20f) };
            var ranked = GoalSelector.Rank(goals, Context(), WorldState.Empty);
            Assert(ranked.Goal1.Name == "First" && ranked.Goal2.Name == "Second" && ranked.Goal3.Name == "Third",
                $"Ties keep the list's order: a later goal must be strictly higher ({ranked.Goal1?.Name}, {ranked.Goal2?.Name}, {ranked.Goal3?.Name}).");
        }

        private void ValidateWhatIsNeverRanked()
        {
            var goals = new List<GoapGoal>
            {
                new Fixed("Invalid", 99f, valid: false),
                new Fixed("Satisfied", 98f, satisfied: true),
                new Fixed("Minus one", -1f),
                new Fixed("Below", -5f),
                new Fixed("Zero", 0f),
            };
            var ranked = GoalSelector.Rank(goals, Context(), WorldState.Empty);
            Assert(ranked.Count == 1 && ranked.Goal1.Name == "Zero", $"Invalid, satisfied and -1 or less are never ranked; 0 is ({ranked.Goal1?.Name}, {ranked.Count}).");
            Assert(GoalSelector.Rank(null, Context(), WorldState.Empty).Count == 0, "No goals, nothing ranked.");
        }

        private void ValidateNoAllocation()
        {
            var goals = new List<GoapGoal> { new Fixed("A", 10f), new Fixed("B", 40f), new Fixed("C", 25f), new Fixed("D", 30f, valid: false) };
            var context = Context();
            var state = WorldState.Empty;
            GoalSelector.Rank(goals, context, state); // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            float sum = 0f;
            for (int i = 0; i < 1000; i++) sum += GoalSelector.Rank(goals, context, state).Priority1;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0, $"A thousand choices allocate nothing ({allocated} bytes).");
            Assert(sum == 40000f, "And each chose the same.");
        }

        private void ValidateTheFallback()
        {
            var goals = new List<GoapGoal> { new Fixed("Best", 40f), new Fixed("Second", 30f), new Fixed("Third", 20f) };
            var ranked = GoalSelector.Rank(goals, Context(), WorldState.Empty);
            var plan = new GoapPlan(4);
            var graph = new ActionGraph();
            var everything = new Planner();
            Assert(GoalSelector.PlanFirst(ranked, 2, everything, WorldState.Empty, graph, Context(), plan) == 0 && everything.Asked == 1, "The best planned, nothing else asked.");
            var notBest = new Planner("Best");
            Assert(GoalSelector.PlanFirst(ranked, 2, notBest, WorldState.Empty, graph, Context(), plan) == 1 && !plan.IsEmpty, "The best unplannable: the second is planned.");
            var onlyThird = new Planner("Best", "Second");
            Assert(GoalSelector.PlanFirst(ranked, 2, onlyThird, WorldState.Empty, graph, Context(), plan) == 2, "Then the third.");
            Assert(GoalSelector.PlanFirst(ranked, 1, new Planner("Best", "Second"), WorldState.Empty, graph, Context(), plan) == -1, "A depth of 1 stops at the second.");
            var none = new Planner("Best", "Second", "Third");
            Assert(GoalSelector.PlanFirst(ranked, 2, none, WorldState.Empty, graph, Context(), plan) == -1 && none.Asked == 3, "None plannable: Tier 3, after three asked.");
            var noDepth = new Planner("Best");
            Assert(GoalSelector.PlanFirst(ranked, 0, noDepth, WorldState.Empty, graph, Context(), plan) == -1 && noDepth.Asked == 1, "A depth of 0 is today's rule: the best or nothing.");
            var two = GoalSelector.Rank(new List<GoapGoal> { new Fixed("Best", 40f) }, Context(), WorldState.Empty);
            Assert(GoalSelector.PlanFirst(two, 2, new Planner("Best"), WorldState.Empty, graph, Context(), plan) == -1, "No goal after the best: nothing more to try.");
        }

        /// <summary>A planner that plans every goal but those named, one step each.</summary>
        private sealed class Planner : IGoapPlanner
        {
            private readonly HashSet<string> _refused;
            public int Asked;

            public Planner(params string[] refused) => _refused = new HashSet<string>(refused);

            public bool Plan(in WorldState currentState, GoapGoal goal, ActionGraph actionGraph, AgentContext context, GoapPlan outPlan)
            {
                Asked++;
                outPlan.Clear();
                if (_refused.Contains(goal.Name)) return false;
                outPlan.AddAction(new Simpiens.Cognition.Planning.Actions.RestAction(), 1f);
                return true;
            }
        }

        /// <summary>A goal with a fixed priority, never satisfied unless told.</summary>
        private sealed class Fixed : GoapGoal
        {
            private readonly float _priority;
            private readonly bool _valid;

            public Fixed(string name, float priority, bool valid = true, bool satisfied = false)
                : base(name, satisfied ? WorldState.Empty : WorldState.Empty.With(StateFact.IsResting, true))
            {
                _priority = priority;
                _valid = valid;
            }

            public override float BasePriority(AgentContext context) => _priority;
            public override bool IsValid(AgentContext context) => _valid;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[GoalSelectorValidator] FAILED: " + message);
        }
    }
}
