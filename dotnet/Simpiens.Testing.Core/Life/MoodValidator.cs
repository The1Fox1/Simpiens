using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// Mood (Epic LIFE-1, L1.3, D8): the need terms and their bounds, which hold for which needs (one each for hunger and
    /// energy, the body's before social's, comfortable when none), the bands in order, the sum of terms and live thoughts,
    /// held within its bounds, and the rule pure: the same inputs at the same tick give the same mood, the context's as the
    /// state's. Runs as a test (ValidatorTests).
    /// </summary>
    public class MoodValidator
    {
        private static readonly EntityId Bram = new EntityId(2u, 0u, 0u, 2u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[MoodValidator] Beginning mood validation...");
            ValidateTheTerms();
            ValidateWhichTermsHold();
            ValidateTheBands();
            ValidateTheSum();
            TestLog.Log("<color=green>[MoodValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTerms()
        {
            Assert(MoodRules.AllTerms.Length == 9, $"Nine need terms, boredom's the ninth ({MoodRules.AllTerms.Length}).");
            foreach (var term in MoodRules.AllTerms)
            {
                Assert(MoodRules.Term(term.Id) == term && !string.IsNullOrEmpty(term.Words), $"{term.Id}: found by its id, with words.");
                Assert(term.Value != 0 && Math.Abs(term.Value) <= 30, $"{term.Id}: a value within 30 ({term.Value}).");
                Assert((term.Value > 0) == (term.Id == MoodTerm.Comfortable), $"{term.Id}: only comfort lifts mood.");
            }
            Assert(MoodRules.Term(MoodTerm.Starving).Value < MoodRules.Term(MoodTerm.Hungry).Value
                && MoodRules.Term(MoodTerm.Exhausted).Value < MoodRules.Term(MoodTerm.Tired).Value
                && MoodRules.Term(MoodTerm.Downed).Value < MoodRules.Term(MoodTerm.Hurt).Value, "Worse is worse: starving below hungry, exhausted below tired, downed below hurt.");
        }

        private void ValidateWhichTermsHold()
        {
            Span<MoodTerm> terms = stackalloc MoodTerm[4];
            Assert(MoodRules.TermsOf(new AgentNeeds(30f, 80f, 60f), 100f, false, terms) == 1 && terms[0] == MoodTerm.Comfortable, "Fed, rested, in company, whole: comfortable.");
            Assert(MoodRules.TermsOf(new AgentNeeds(65f, 80f, 60f), 100f, false, terms) == 1 && terms[0] == MoodTerm.Hungry, "Hunger 65: hungry, and no comfort.");
            Assert(MoodRules.TermsOf(new AgentNeeds(90f, 80f, 60f), 100f, false, terms) == 1 && terms[0] == MoodTerm.Starving, "Hunger 90: starving, not hungry too.");
            int n = MoodRules.TermsOf(new AgentNeeds(65f, 10f, 20f), 30f, false, terms);
            Assert(n == 4 && terms[0] == MoodTerm.Hungry && terms[1] == MoodTerm.Exhausted && terms[2] == MoodTerm.Hurt && terms[3] == MoodTerm.Lonely,
                "Hungry, exhausted, hurt and lonely at once, in that order.");
            Assert(MoodRules.TermsOf(new AgentNeeds(30f, 80f, 60f), 0f, true, terms) == 1 && terms[0] == MoodTerm.Downed, "Downed, not hurt too.");
            Assert(MoodRules.NeedsOf(new AgentNeeds(30f, 80f, 60f), 100f, false) == 4 && MoodRules.NeedsOf(new AgentNeeds(90f, 10f, 20f), 100f, false) == -33,
                "The terms' sums: comfortable 4; starving, exhausted and lonely -33.");
            // Boredom (Epic LIFE-1 plan 2, L2.6): fun at 25 or less, last in order, and no comfort beside it
            Span<MoodTerm> five = stackalloc MoodTerm[MoodRules.TermCapacity];
            Assert(MoodRules.TermsOf(new AgentNeeds(30f, 80f, 60f), 100f, false, 25f, five) == 1 && five[0] == MoodTerm.Bored, "Fun 25: bored, and no comfort.");
            Assert(MoodRules.TermsOf(new AgentNeeds(30f, 80f, 60f), 100f, false, 26f, five) == 1 && five[0] == MoodTerm.Comfortable, "Fun 26: comfortable.");
            Assert(MoodRules.TermsOf(new AgentNeeds(65f, 10f, 20f), 30f, false, 0f, five) == 5 && five[4] == MoodTerm.Bored, "Five at once, boredom last.");
            Assert(MoodRules.NeedsOf(new AgentNeeds(30f, 80f, 60f), 100f, false, 10f) == -5, "Bored alone: -5.");
            var bored = default(CrawlerLife);
            bored.Bore(80f);
            Assert(MoodRules.Of(new AgentNeeds(30f, 80f, 60f), 100f, false, bored, 1) == -5, "The mood reads its record's fun.");
        }

        private void ValidateTheBands()
        {
            Assert(MoodRules.BandOf(-100) == MoodBand.Breaking && MoodRules.BandOf(MoodRules.BreakingAtMost) == MoodBand.Breaking, "Breaking to -35.");
            Assert(MoodRules.BandOf(MoodRules.BreakingAtMost + 1) == MoodBand.Low && MoodRules.BandOf(MoodRules.LowAtMost) == MoodBand.Low, "Low to -10.");
            Assert(MoodRules.BandOf(MoodRules.LowAtMost + 1) == MoodBand.Steady && MoodRules.BandOf(MoodRules.GoodFrom - 1) == MoodBand.Steady, "Steady below 10.");
            Assert(MoodRules.BandOf(MoodRules.GoodFrom) == MoodBand.Good && MoodRules.BandOf(MoodRules.HappyFrom - 1) == MoodBand.Good, "Good below 30.");
            Assert(MoodRules.BandOf(MoodRules.HappyFrom) == MoodBand.Happy && MoodRules.BandOf(100) == MoodBand.Happy, "Happy from 30.");
            Assert(MoodBand.Breaking < MoodBand.Low && MoodBand.Low < MoodBand.Steady && MoodBand.Steady < MoodBand.Good && MoodBand.Good < MoodBand.Happy, "The bands are ordered.");
        }

        private void ValidateTheSum()
        {
            var needs = new AgentNeeds(30f, 80f, 60f); // comfortable, 4
            var life = default(CrawlerLife);
            ThoughtRules.Think(ref life, Thought.AGoodMeal, default, 100u);   // 3
            ThoughtRules.Think(ref life, Thought.SleptInABunk, default, 100u); // 3
            Assert(MoodRules.Of(needs, 100f, false, life, 100) == 10, $"Comfort and two good thoughts: 10 ({MoodRules.Of(needs, 100f, false, life, 100)}).");
            ThoughtRules.Think(ref life, Thought.FriendDied, Bram, 200u);       // -25
            Assert(MoodRules.Of(needs, 100f, false, life, 200) == -15 && MoodRules.BandOf(-15) == MoodBand.Low, "A friend's death brings it to Low: -15.");
            long mealEnds = 100 + 6L * DayClock.TicksPerHour;
            Assert(MoodRules.Of(needs, 100f, false, life, mealEnds) == -18, "The meal's thought ends: -18.");
            ThoughtRules.Think(ref life, Thought.AGoodMeal, default, 300u);   // 3 again, and a half-stack rounds away from zero
            ThoughtRules.Think(ref life, Thought.AGoodMeal, default, 310u);   // 4.5
            Assert(MoodRules.Of(needs, 100f, false, life, 400) == -14, $"4 + 3 + 4.5 - 25 = -13.5, rounded away from zero to -14 ({MoodRules.Of(needs, 100f, false, life, 400)}).");
            var grim = default(CrawlerLife);
            foreach (var who in new[] { new EntityId(5u, 0u, 0u, 5u), new EntityId(6u, 0u, 0u, 6u), new EntityId(7u, 0u, 0u, 7u), new EntityId(8u, 0u, 0u, 8u) })
                ThoughtRules.Think(ref grim, Thought.FriendDied, who, 0u);
            ThoughtRules.Think(ref grim, Thought.Downed, default, 0u);
            Assert(MoodRules.Of(new AgentNeeds(95f, 5f, 5f), 0f, true, grim, 1) == -MoodRules.Max, "Held at -100.");

            var context = new AgentContext(default, default, needs, 0f, null, null, 400u, health: 100f, life: life);
            Assert(MoodRules.Of(context) == MoodRules.Of(needs, 100f, false, life, 400), "The context's mood is the state's: cognition reads what the main thread reads.");
            Assert(MoodRules.Of(needs, 100f, false, life, 400) == MoodRules.Of(needs, 100f, false, life, 400), "Pure: the same inputs, the same mood.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MoodValidator] FAILED: " + message);
        }
    }
}
