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
            TestLog.Log("<color=green>[GoalSelectorValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
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
