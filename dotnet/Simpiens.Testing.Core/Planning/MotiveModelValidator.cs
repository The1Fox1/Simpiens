using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Domain;
using Simpiens.Simulation;
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
            ValidateMoodWeighs();
            ValidateTheRoutineWeighs();
            ValidateTheRhythmWeighsWork();
            TestLog.Log("<color=green>[MotiveModelValidator] ALL 7 TESTS PASSED SUCCESSFULLY!</color>");
        }

        /// <summary>
        /// A context at tick 0 under <paramref name="economy"/> (the default's), with the routine's weights set aside: the tests
        /// of the base, the session's weights and mood read their own factors alone (the routine's are
        /// <see cref="ValidateTheRoutineWeighs"/>'s).
        /// </summary>
        private static AgentContext Context(in AgentNeeds needs, EconomyTuning economy = null, in StationOrder station = default)
        {
            economy ??= EconomyTuning.Default;
            var snapshot = new SharedWorldSnapshot(10, 10, 1f, 4, _ => { });
            snapshot.SetEconomy(economy.WithLife(economy.Life.WithoutRoutine()));
            return new AgentContext(default, default, needs, 0f, snapshot, null, 0, station: station);
        }

        private void ValidateNeutralIsExact()
        {
            var needs = new AgentNeeds(63.7f, 31.3f, 22.9f); // hungry and lonely: a low mood, so mood's weights are set aside here
            var context = Context(needs, EconomyTuning.Default.WithLife(LifeTuning.Default.WithoutMood().WithoutRoutine()));
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

        private void ValidateMoodWeighs()
        {
            var steady = Context(new AgentNeeds(30f, 80f, 60f));      // comfortable: Steady
            var low = Context(new AgentNeeds(65f, 25f, 20f));         // hungry, tired, lonely: -17, Low
            Assert(steady.Mood == Simpiens.Simulation.Life.MoodBand.Steady && low.Mood == Simpiens.Simulation.Life.MoodBand.Low, $"The context reads its mood ({steady.Mood}, {low.Mood}).");
            var socialize = new Simpiens.Cognition.Planning.Goals.SocializeGoal();
            var earn = new Simpiens.Cognition.Planning.Goals.EarnIncomeGoal();
            var eat = new Simpiens.Cognition.Planning.Goals.SatiateHungerGoal();
            Assert(MotiveModel.Weight(socialize, steady) == 1f && MotiveModel.Weight(earn, steady) == 1f, "Steady, mood weighs nothing.");
            Assert(MotiveModel.Weight(earn, low) == 0.9f && MotiveModel.Weight(socialize, low) == 1f && MotiveModel.Weight(eat, low) == 1f,
                $"Low: income pulls 0.9; company (until plan 2's fun, F12) and hunger as ever ({MotiveModel.Weight(earn, low)}, {MotiveModel.Weight(socialize, low)}).");
            var life = LifeTuning.Default;
            Assert(life.MoodWeight(Simpiens.Simulation.Life.MoodBand.Breaking, (int)Motive.Income) == 0.75f && life.MoodWeight(Simpiens.Simulation.Life.MoodBand.Breaking, (int)Motive.Company) == 1f, "Breaking: income 0.75, company 1.");
            Assert(life.MoodWeight(Simpiens.Simulation.Life.MoodBand.Low, (int)Motive.Duty) == 0.85f && life.MoodWeight(Simpiens.Simulation.Life.MoodBand.Breaking, (int)Motive.Duty) == 0.6f
                && life.MoodWeight(Simpiens.Simulation.Life.MoodBand.Steady, (int)Motive.Duty) == 1f, "Duty: low 0.85, breaking 0.6, steady 1 (plan 2's D25).");
            // Under orders, mood weighs as anywhere (the producer, 2026-11-24: orders don't override a crawler's autonomy; D25)
            var shift = Context(new AgentNeeds(65f, 25f, 20f), station: new StationOrder(0, default, 1f));
            var comply = new Simpiens.Cognition.Planning.Goals.ComplyDirectiveGoal();
            Assert(shift.Mood == Simpiens.Simulation.Life.MoodBand.Low && shift.Station.IsActive && MotiveModel.Weight(earn, shift) == 0.9f && MotiveModel.Weight(comply, shift) == 0.85f,
                $"Under orders (on shift), a low mood weighs earning 0.9 and duty 0.85 ({MotiveModel.Weight(earn, shift)}, {MotiveModel.Weight(comply, shift)}).");
            Assert(life.MoodFrustrationPerHour(Simpiens.Simulation.Life.MoodBand.Breaking) == 15f && life.MoodFrustrationPerHour(Simpiens.Simulation.Life.MoodBand.Low) == 5f
                && life.MoodFrustrationPerHour(Simpiens.Simulation.Life.MoodBand.Steady) == 0f && life.MoodFrustrationPerHour(Simpiens.Simulation.Life.MoodBand.Happy) == -5f, "Mood's frustration: +15, +5, 0, -5.");
            var quiet = life.WithoutMood();
            Assert(quiet.MoodWeight(Simpiens.Simulation.Life.MoodBand.Breaking, (int)Motive.Income) == 1f && quiet.MoodFrustrationPerHour(Simpiens.Simulation.Life.MoodBand.Breaking) == 0f && quiet.FallbackDepth == 2,
                "Without mood: no weights, no frustration, the rest kept.");
            Assert(default(LifeTuning).MoodWeight(Simpiens.Simulation.Life.MoodBand.Low, (int)Motive.Income) == 1f, "A zeroed tuning: mood weighs nothing.");
        }

        /// <summary>
        /// The routine's weights (Epic LIFE-1 plan 3, R.3, R.4, D30, D31): in its night rest weighs 1.5 (the bedtime pull) and is
        /// valid from the bedtime line; outside the night rest weighs 1 and waits for the tired line; work, leisure and company by
        /// block as D30's table has them; an artisan's shifts are its working block; the unweighted family (and a clear's loot)
        /// is never scaled; and a tuning without the routine weighs nothing by the hour.
        /// </summary>
        private void ValidateTheRoutineWeighs()
        {
            var routine = Routine.Of(Chronotype.Average, 0f, default, DayClock.TicksPerDay + DayClock.Hours(4));
            uint working = (uint)(routine.WorkFrom + 7), leisure = (uint)(routine.WorkUntil + 7), night = (uint)(routine.NightFrom + 7);
            AgentContext At(uint tick, float energy) => new AgentContext(default, default, new AgentNeeds(20f, energy, 80f), 0f, null, null, tick);
            Assert(At(working, 60f).Block == RoutineBlock.Working && At(leisure, 60f).Block == RoutineBlock.Leisure && At(night, 60f).Block == RoutineBlock.Night,
                "The contexts are in the blocks the routine says.");
            var rest = new Simpiens.Cognition.Planning.Goals.RestGoal();
            var eat = new Simpiens.Cognition.Planning.Goals.SatiateHungerGoal();
            var comply = new Simpiens.Cognition.Planning.Goals.ComplyDirectiveGoal();
            Assert(MotiveModel.Weight(rest, At(night, 60f)) == 1.5f && MotiveModel.Weight(rest, At(working, 60f)) == 1f && MotiveModel.Weight(rest, At(leisure, 60f)) == 1f,
                "Rest weighs 1.5 in the night, 1 by day.");
            // D30's table (R.4): work, leisure and company by block
            var earn = new Simpiens.Cognition.Planning.Goals.EarnIncomeGoal();
            var recreate = new Simpiens.Cognition.Planning.Goals.RecreateGoal();
            var socialize = new Simpiens.Cognition.Planning.Goals.SocializeGoal();
            var gather = new Simpiens.Cognition.Planning.Goals.GatherAtAnchorGoal();
            (uint Tick, float Work, float Leisure, float Social)[] table = { (working, 1.2f, 0.8f, 1f), (leisure, 0.8f, 1.2f, 1f), (night, 0.6f, 0.7f, 0.8f) }; // company 1 by day (D30 amended)
            foreach (var row in table)
            {
                var context = At(row.Tick, 80f);
                Assert(MotiveModel.Weight(earn, context) == row.Work && MotiveModel.Weight(recreate, context) == row.Leisure
                    && MotiveModel.Weight(socialize, context) == row.Social && MotiveModel.Weight(gather, context) == row.Social,
                    $"{context.Block}: work {row.Work}, leisure {row.Leisure}, company {row.Social} ({MotiveModel.Weight(earn, context)}, {MotiveModel.Weight(recreate, context)}, {MotiveModel.Weight(socialize, context)}).");
                Assert(MotiveModel.Weight(eat, context) == 1f && MotiveModel.Weight(comply, context) == 1f, $"{context.Block}: hunger and orders unweighted.");
            }
            // An artisan works its shifts: in them its work weighs as the working block's, whatever its routine says
            var offShift = new AgentContext(default, default, new AgentNeeds(20f, 80f, 80f), 0f, null, null, night, station: StationOrder.OffShift(Simpiens.Simulation.Workshops.ShiftBlocks.All));
            Assert(offShift.Block == RoutineBlock.Working && MotiveModel.Weight(earn, offShift) == 1.2f, "An artisan with every block on shift is working at night.");
            // A clear's loot is unweighted, as orders are, at any hour
            var loot = new Simpiens.Cognition.Planning.Goals.CollectLootGoal();
            var clear = new AgentContext(default, default, new AgentNeeds(20f, 80f, 80f), 0f, null, null, night,
                expedition: new Simpiens.Cognition.Contracts.ExpeditionOrder(Simpiens.Simulation.Guild.ExpeditionStage.Fight, 1, default, default, Simpiens.Simulation.Guild.ExpeditionKind.Clear));
            Assert(MotiveModel.Weight(loot, clear) == 1f && MotiveModel.Weight(loot, At(night, 80f)) == 0.6f, "Loot under a clear's orders is unweighted; on its own it is night work.");
            Assert(MotiveModel.Weight(eat, At(night, 60f)) == 1f && MotiveModel.Weight(comply, At(night, 60f)) == 1f, "Hunger and orders are unweighted at any hour.");
            Assert(rest.IsValid(At(night, 60f)) && !rest.IsValid(At(night, 75f)) && !rest.IsValid(At(leisure, 60f)) && rest.IsValid(At(leisure, 35f)),
                "In the night rest is valid below the bedtime line (70); by day only below the tired line (40).");
            Assert(Considerations.IsBedtime(At(night, 69f)) && !Considerations.IsBedtime(At(night, 70f)) && !Considerations.IsBedtime(At(leisure, 30f)), "Bedtime is the night below 70.");
            var life = LifeTuning.Default;
            Assert(life.RoutineWeight(RoutineBlock.Night, (int)ActivityFamily.Unweighted) == 1f && life.RoutineWeight(RoutineBlock.None, (int)ActivityFamily.Rest) == 1f,
                "The unweighted family and no block weigh 1.");
            Assert(life.WithoutRoutine().RoutineWeight(RoutineBlock.Night, (int)ActivityFamily.Rest) == 1f && life.WithoutRoutine().MoodWeight(MoodBand.Low, (int)Motive.Income) == 0.9f,
                "Without the routine: no weight by the hour, mood's kept.");
            Assert(life.WithRoutineWeight(RoutineBlock.Night, (int)ActivityFamily.Unweighted, 2f).RoutineWeight(RoutineBlock.Night, (int)ActivityFamily.Unweighted) == 1f,
                "The unweighted family can't be given a routine weight.");
        }

        /// <summary>
        /// Satiation and weariness on work (Epic LIFE-1 plan 3, R.6, D32, D33): work weighs 1 up to the purse target (the
        /// comfortable purse by greed), falling to 0.6 at twice it; weariness takes up to 0.3 off; neither touches another family;
        /// and a zeroed rhythm weighs nothing.
        /// </summary>
        private void ValidateTheRhythmWeighsWork()
        {
            int purse = EconomyTuning.Default.ComfortablePurse;
            var earn = new Simpiens.Cognition.Planning.Goals.FulfillBountyGoal();
            var socialize = new Simpiens.Cognition.Planning.Goals.SocializeGoal();
            var quiet = EconomyTuning.Default.WithLife(LifeTuning.Default.WithoutRoutine().WithoutMood());
            AgentContext Rich(int gold, float greed = 0f, float weariness = 0f, EconomyTuning economy = null)
            {
                var snapshot = new SharedWorldSnapshot(10, 10, 1f, 4, _ => { });
                snapshot.SetEconomy(economy ?? quiet);
                var life = default(CrawlerLife);
                life.Weary(weariness);
                return new AgentContext(default, default, new AgentNeeds(20f, 80f, 80f), 0f, snapshot, null, 0,
                    inventory: new Simpiens.Cognition.Inventory.AgentInventory(4, startingGold: gold), personality: new AgentPersonality(greed, 0f, 0f, 0f, 0f), life: life);
            }
            Assert(Rhythm.PurseTarget(Rich(0)) == purse && Rhythm.PurseTarget(Rich(0, greed: 1f)) == (int)(purse * 1.5f) && Rhythm.PurseTarget(Rich(0, greed: -1f)) == purse / 2,
                $"The purse target: the comfortable purse ({purse}g), by greed half to one and a half times it.");
            Assert(MotiveModel.Weight(earn, Rich(purse)) == 1f && MotiveModel.Weight(earn, Rich(purse / 2)) == 1f, "Up to the target, work weighs 1.");
            Assert(Math.Abs(MotiveModel.Weight(earn, Rich(purse + purse / 2)) - 0.8f) < 1e-5f, $"Half again over it, 0.8 ({MotiveModel.Weight(earn, Rich(purse + purse / 2))}).");
            Assert(Math.Abs(MotiveModel.Weight(earn, Rich(2 * purse)) - 0.6f) < 1e-5f && Math.Abs(MotiveModel.Weight(earn, Rich(5 * purse)) - 0.6f) < 1e-5f, "At twice it and beyond, 0.6.");
            Assert(MotiveModel.Weight(earn, Rich(purse + purse / 2, greed: 1f)) == 1f, "The greedy work on: their target is higher.");
            Assert(Math.Abs(MotiveModel.Weight(earn, Rich(10, weariness: 100f)) - 0.7f) < 1e-5f && Math.Abs(MotiveModel.Weight(earn, Rich(10, weariness: 50f)) - 0.85f) < 1e-5f,
                "Weariness at its height takes 0.3 off work, half of it 0.15.");
            Assert(Math.Abs(MotiveModel.Weight(earn, Rich(2 * purse, weariness: 100f)) - 0.42f) < 1e-5f, "Rich and weary: the two multiply (0.6 x 0.7).");
            Assert(MotiveModel.Weight(socialize, Rich(5 * purse, weariness: 100f)) == 1f, "Company is untouched by the purse and weariness.");
            var none = EconomyTuning.Default.WithLife(LifeTuning.Default.WithoutRoutine().WithoutMood().WithRhythm(default));
            Assert(MotiveModel.Weight(earn, Rich(5 * purse, weariness: 100f, economy: none)) == 1f, "A zeroed rhythm weighs nothing.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MotiveModelValidator] FAILED: " + message);
        }
    }
}
