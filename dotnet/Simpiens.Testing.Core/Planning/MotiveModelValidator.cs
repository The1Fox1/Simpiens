using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Domain;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Planning
{
    /// <summary>
    /// The motive model (Epic LIFE-1, L0.5, D2): with every weight at 1, a goal's priority is its base to the bit, so no
    /// choice changes; a weight set on a motive scales every goal of that motive and can change a choice; the session's
    /// tuning defaults to nothing set; and a motive's urgency is its need. Runs as a test (ValidatorTests).
    /// </summary>
    public class MotiveModelValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[MotiveModelValidator] Beginning motive model validation...");
            ValidateNeutralIsExact();
            ValidateAWeightChangesAChoice();
            ValidateTheTuning();
            ValidateUrgency();
            TestLog.Log("<color=green>[MotiveModelValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static AgentContext Context(in AgentNeeds needs, EconomyTuning economy = null)
        {
            SharedWorldSnapshot snapshot = null;
            if (economy != null)
            {
                snapshot = new SharedWorldSnapshot(10, 10, 1f, 4, _ => { });
                snapshot.SetEconomy(economy);
            }
            return new AgentContext(default, default, needs, 0f, snapshot, null, 0);
        }

        private void ValidateNeutralIsExact()
        {
            var needs = new AgentNeeds(63.7f, 31.3f, 22.9f);
            var context = Context(needs);
            foreach (var goal in OutpostGoapDomain.BuildGoals())
            {
                float basePriority = goal.BasePriority(context);
                float priority = MotiveModel.Score(goal, context, out float reportedBase, out float weight);
                Assert(weight == 1f, $"{goal.Name}: every weight is 1 by default ({weight}).");
                Assert(BitConverter.SingleToInt32Bits(priority) == BitConverter.SingleToInt32Bits(basePriority) && reportedBase == basePriority,
                    $"{goal.Name}: its priority is its base to the bit ({priority} against {basePriority}).");
                Assert(MotiveModel.Priority(goal, context) == priority, $"{goal.Name}: Priority and Score agree.");
            }
        }

        private void ValidateAWeightChangesAChoice()
        {
            // Hungry and lonely: hunger 70 against social's 100 - 40 = 60 (Socialize, no friends known, neutral sociability)
            var needs = new AgentNeeds(70f, 100f, 40f);
            var goals = new List<GoapGoal> { new Simpiens.Cognition.Planning.Goals.SatiateHungerGoal(), new Simpiens.Cognition.Planning.Goals.SocializeGoal() };
            var hungry = WorldState.Empty.With(StateFact.IsHungry, true); // so SatiateHunger isn't satisfied
            var plain = GoalSelector.Rank(goals, Context(needs), hungry);
            Assert(plain.Goal1.Name == "SatiateHunger" && plain.Priority1 == 70f && plain.Priority2 == 60f, $"Unweighted, hunger wins, 70 to 60 ({plain.Goal1?.Name} {plain.Priority1}, {plain.Priority2}).");

            var weighted = EconomyTuning.Default.WithLife(LifeTuning.Default.WithMotiveWeight((int)Motive.Company, 1.25f));
            var ranked = GoalSelector.Rank(goals, Context(needs, weighted), hungry);
            Assert(ranked.Goal1.Name == "Socialize" && ranked.Priority1 == 75f && ranked.Base1 == 60f && ranked.Weight1 == 1.25f,
                $"Company weighed 1.25, Socialize wins, 75 (60 x 1.25) to 70 ({ranked.Goal1?.Name} {ranked.Priority1}, base {ranked.Base1}, weight {ranked.Weight1}).");
            Assert(ranked.Priority2 == 70f, "Hunger's own weight stays 1.");

            var gather = new Simpiens.Cognition.Planning.Goals.GatherAtAnchorGoal();
            float gatherBase = gather.BasePriority(Context(needs));
            Assert(MotiveModel.Priority(gather, Context(needs, weighted)) == gatherBase * 1.25f, "Every goal of the motive is weighed alike (GatherAtAnchor too).");
        }

        private void ValidateTheTuning()
        {
            var life = LifeTuning.Default;
            for (int id = 0; id < LifeTuning.MotiveCapacity; id++) Assert(life.MotiveWeight(id) == 1f, $"Motive {id}: weight 1 by default.");
            Assert(life.Commitment == 0f && life.FallbackDepth == 2, "No commitment by default, and the fallback to the next two goals (L0.6).");
            Assert(default(LifeTuning).FallbackDepth == 0 && default(LifeTuning).MotiveWeight((int)Motive.Rest) == 1f, "A zeroed tuning: no fallback, every weight 1.");
            Assert(EconomyTuning.Default.Life.MotiveWeight((int)Motive.Income) == 1f, "The session's default tuning sets nothing.");
            Assert(life.MotiveWeight(-1) == 1f && life.MotiveWeight(LifeTuning.MotiveCapacity) == 1f, "An id out of range weighs 1.");
            var set = life.WithMotiveWeight((int)Motive.Income, 0.75f).WithCommitment(4f).WithFallbackDepth(2);
            Assert(set.MotiveWeight((int)Motive.Income) == 0.75f && set.MotiveWeight((int)Motive.Rest) == 1f, "A weight set on one motive only.");
            Assert(set.Commitment == 4f && set.FallbackDepth == 2, "Commitment and depth kept beside the weights.");
            Assert(set.WithMotiveWeight((int)Motive.Rest, 1.5f).MotiveWeight((int)Motive.Income) == 0.75f, "Setting another keeps the first.");
        }

        private void ValidateUrgency()
        {
            var context = Context(new AgentNeeds(64f, 30f, 45f));
            Assert(Urgency.Of(Motive.Hunger, context) == 64f, "Hunger's urgency is hunger.");
            Assert(Urgency.Of(Motive.Rest, context) == 70f, "Rest's is 100 - energy.");
            Assert(Urgency.Of(Motive.Company, context) == 55f, "Company's is 100 - social.");
            Assert(Urgency.Of(Motive.Income, context) == 0f && Urgency.Of(Motive.Duty, context) == 0f, "A motive with no need behind it has none.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MotiveModelValidator] FAILED: " + message);
        }
    }
}
