using System;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// How thoughts are held (Epic LIFE-1, L1.2, D7): one entry thinks a thought; the same thought naming the same crawler
    /// stacks to its limit and starts again; a thought ends after its duration and is never counted past it; a full record
    /// gives the slot worth least; four subjects at most, the lightest let go with its thoughts; and a subject frees when no
    /// thought names it. Runs as a test (ValidatorTests).
    /// </summary>
    public class ThoughtRulesValidator
    {
        private static readonly EntityId Ama = new EntityId(1u, 0u, 0u, 1u), Bram = new EntityId(2u, 0u, 0u, 2u), Corrin = new EntityId(3u, 0u, 0u, 3u),
            Dara = new EntityId(4u, 0u, 0u, 4u), Eli = new EntityId(5u, 0u, 0u, 5u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[ThoughtRulesValidator] Beginning thought rules validation...");
            ValidateThinkingAndStacking();
            ValidateExpiry();
            ValidateAFullRecord();
            ValidateSubjects();
            ValidateTheEntry();
            TestLog.Log("<color=green>[ThoughtRulesValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateThinkingAndStacking()
        {
            var life = default(CrawlerLife);
            Assert(ThoughtRules.Think(ref life, Thought.AGoodMeal, default, 100u), "A meal thought.");
            Assert(ThoughtRules.Sum(life, 100) == 3f && ThoughtRules.Holds(life, Thought.AGoodMeal, 100), "It counts 3.");
            Assert(ThoughtRules.Think(ref life, Thought.AGoodMeal, default, 200u), "A second meal.");
            var slot = life.ThoughtAt(0);
            Assert(slot.Stacks == 2 && slot.BegunAt == 200u && life.ThoughtAt(1).Thought == 0, "The same thought stacks in its slot, its time started again.");
            Assert(ThoughtRules.Sum(life, 200) == 4.5f, $"Two stacks: 3 + 1.5 ({ThoughtRules.Sum(life, 200)}).");
            ThoughtRules.Think(ref life, Thought.AGoodMeal, default, 300u);
            Assert(life.ThoughtAt(0).Stacks == 2, "No more than its limit (2).");
            Assert(!ThoughtRules.Think(ref life, Thought.None, default, 300u) && !ThoughtRules.Think(ref life, (Thought)999, default, 300u), "No thought, no row: nothing held.");
            Assert(!ThoughtRules.Think(ref life, Thought.FriendDied, default, 300u), "A thought that names a crawler needs one.");
        }

        private void ValidateExpiry()
        {
            var life = default(CrawlerLife);
            ThoughtRules.Think(ref life, Thought.AGoodMeal, default, 1000u); // 6 game hours
            long end = 1000 + 6L * DayClock.TicksPerHour;
            Assert(ThoughtRules.EndsAt(life.ThoughtAt(0)) == end, "It ends 6 game hours after it began.");
            Assert(ThoughtRules.Holds(life, Thought.AGoodMeal, end - 1) && ThoughtRules.Sum(life, end - 1) == 3f, "Live until its last tick.");
            Assert(!ThoughtRules.Holds(life, Thought.AGoodMeal, end) && ThoughtRules.Sum(life, end) == 0f, "Never counted past it, though the slot still holds it.");
            Assert(life.ThoughtAt(0).Thought != 0, "Lazy: nothing sweeps the record.");
            ThoughtRules.Expire(ref life, end);
            Assert(life.ThoughtAt(0).Thought == 0 && life.IsEmpty, "Cleared when the record is next written.");
        }

        private void ValidateAFullRecord()
        {
            var life = default(CrawlerLife);
            // Sixteen slots: a talk's 2 is the weakest; everything else is worth more
            ThoughtRules.Think(ref life, Thought.AGoodTalk, default, 0u);
            var others = new[] { Thought.AGoodMeal, Thought.AteAtTheGarden, Thought.SleptInABunk, Thought.SleptOnTheFloor, Thought.SharedMyFood, Thought.PaidInFull,
                Thought.WageUnpaid, Thought.RaiseUnpaid, Thought.AGoodPayday, Thought.BeatenToIt, Thought.Hurt, Thought.Downed, Thought.AKill, Thought.GrewInMyCraft, Thought.EarnedAName };
            foreach (var t in others) ThoughtRules.Think(ref life, t, default, 0u);
            int held = 0;
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++) if (life.ThoughtAt(i).Thought != 0) held++;
            Assert(held == 16, $"Sixteen thoughts fill the record ({held}).");
            ThoughtRules.Think(ref life, Thought.MadeAMasterpiece, default, 10u);
            Assert(ThoughtRules.Holds(life, Thought.MadeAMasterpiece, 10) && !ThoughtRules.Holds(life, Thought.AGoodTalk, 10),
                "A seventeenth takes the slot worth least (the talk: 2 for 6 hours).");
        }

        private void ValidateSubjects()
        {
            var life = default(CrawlerLife);
            ThoughtRules.Think(ref life, Thought.FedBy, Ama, 0u);
            ThoughtRules.Think(ref life, Thought.RescuedBy, Ama, 0u);
            Assert(life.SubjectAt(1) == Ama && life.ThoughtAt(0).Subject == 1 && life.ThoughtAt(1).Subject == 1, "Two thoughts of Ama share her slot.");
            ThoughtRules.Think(ref life, Thought.FedBy, Bram, 0u);
            Assert(life.ThoughtAt(2).Thought == (ushort)Thought.FedBy && life.ThoughtAt(2).Subject == 2, "Fed by Bram is a thought of its own, not a stack of Ama's.");
            ThoughtRules.Think(ref life, Thought.FedBy, Ama, 50u);
            Assert(life.ThoughtAt(0).Stacks == 1 && life.ThoughtAt(0).BegunAt == 50u, "Fed by Ama again: its time started again (its limit is 1).");
            ThoughtRules.Think(ref life, Thought.Saved, Corrin, 0u);
            ThoughtRules.Think(ref life, Thought.FriendDied, Dara, 0u);
            Assert(life.SubjectAt(4) == Dara, "Four subjects named.");
            // A fifth: the lightest subject (Bram, fed: 4) is let go with its thoughts
            ThoughtRules.Think(ref life, Thought.FedBy, Eli, 60u);
            bool bramNamed = false, eliNamed = false;
            for (int s = 1; s <= CrawlerLife.SubjectCapacity; s++) { bramNamed |= life.SubjectAt(s) == Bram; eliNamed |= life.SubjectAt(s) == Eli; }
            Assert(!bramNamed && eliNamed, "The fifth subject takes the lightest one's place (Bram's 4).");
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
                if (life.ThoughtAt(i).Subject != 0) Assert(life.SubjectAt(life.ThoughtAt(i).Subject) != Bram, "No thought still names Bram.");
            // A subject frees when its last thought ends
            long fedEnds = 60 + 24L * DayClock.TicksPerHour;
            ThoughtRules.Expire(ref life, fedEnds + 1); // Fed by Ama (from 50) and by Eli (from 60) end; Ama's rescue (72 h) lives on
            bool amaNamed = false; eliNamed = false;
            for (int s = 1; s <= CrawlerLife.SubjectCapacity; s++) { amaNamed |= life.SubjectAt(s) == Ama; eliNamed |= life.SubjectAt(s) == Eli; }
            Assert(amaNamed && !eliNamed, "Eli, named by no live thought, is let go; Ama, still rescuing, stays.");
        }

        private void ValidateTheEntry()
        {
            var state = new AgentState(Ama);
            Assert(state.Think(Thought.FriendDied, 500u, Bram) && ThoughtRules.Holds(state.Life, Thought.FriendDied, 500), "AgentState.Think holds it in the crawler's life record.");
            Assert(state.Life.SubjectAt(1) == Bram, "Naming the dead.");
            Assert(!state.Think(Thought.FriendDied, 501u), "No one named, nothing held.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ThoughtRulesValidator] FAILED: " + message);
        }
    }
}
