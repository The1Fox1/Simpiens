using System;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// Wants (Epic LIFE-1 plan 2, W.1–W.3, D20): the table (words, bounds, triggers, targets and the thoughts each leaves); the
    /// rules (a want takes a free slot once, three at most; runs out lazily; comes true for the spell or the meeting that answers
    /// it); and the crawler's entries, which think a want's thoughts. Runs as a test (ValidatorTests).
    /// </summary>
    public class WantsValidator
    {
        private static readonly EntityId Ama = new EntityId(1u, 0u, 0u, 1u), Bram = new EntityId(2u, 0u, 0u, 2u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[WantsValidator] Beginning wants validation...");
            ValidateTheTable();
            ValidateTheRules();
            ValidateTheEntries();
            TestLog.Log("<color=green>[WantsValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTable()
        {
            var all = WantTable.All;
            Assert(all.Count == 5, $"D20's five wants ({all.Count}).");
            foreach (var row in all)
            {
                Assert(row.Name == row.Id.ToString() && !string.IsNullOrEmpty(row.Words) && row.Words.All(c => c < 256), $"{row.Id}: its name and Latin-1 words.");
                Assert(row.Hours > 0 && row.Hours <= WantTable.MaxHours && row.Strength > 0 && row.Strength <= WantTable.MaxStrength, $"{row.Id}: its lifetime and strength within bounds.");
                Assert(row.Trigger != WantTrigger.None, $"{row.Id}: a trigger.");
                Assert((row.Target == WantTarget.Activity) == (row.Activity != Activity.None), $"{row.Id}: an activity exactly when it is for one.");
                if (row.Activity != Activity.None) Assert(ActivityTable.Get(row.Activity)?.IsRecreation == true, $"{row.Id}: its activity is a recreation.");
                bool names = row.Words.Contains("{0}");
                Assert(names == (row.Target == WantTarget.Friend), $"{row.Id}: names the friend exactly when it is for one.");
                var fulfilled = ThoughtTable.Get(row.Fulfilled);
                Assert(fulfilled != null && fulfilled.Value > 0 && fulfilled.NamesSubject == names, $"{row.Id}: a good thought when it comes true, naming as it does.");
                if (row.Expired != Thought.None) Assert(ThoughtTable.Get(row.Expired).Value < 0 && ThoughtTable.Get(row.Expired).NamesSubject == names, $"{row.Id}: a small bad one when it runs out.");
            }
        }

        private void ValidateTheRules()
        {
            var life = default(CrawlerLife);
            var supper = new WantSlot { Want = (ushort)Want.TavernSupper, Activity = (byte)Activity.Supper, ExpiresAt = 1000 };
            Assert(WantRules.Add(ref life, supper) && !WantRules.Add(ref life, supper), "A want is held once.");
            Assert(WantRules.Add(ref life, new WantSlot { Want = (ushort)Want.QuietWalk, Activity = (byte)Activity.Walk, ExpiresAt = 500 }), "A second.");
            Assert(WantRules.Add(ref life, new WantSlot { Want = (ushort)Want.SeeAFriend, ExpiresAt = 2000, Friend = Bram }), "A third.");
            Assert(!WantRules.Add(ref life, new WantSlot { Want = (ushort)Want.NightOfDice, Activity = (byte)Activity.Dice, ExpiresAt = 900 }), "Three at most.");
            Span<WantSlot> ended = stackalloc WantSlot[3];
            Assert(WantRules.Expire(ref life, 600, ended) == 1 && ended[0].Want == (ushort)Want.QuietWalk && !WantRules.Holds(life, Want.QuietWalk, 600), "The walk ran out at 500.");
            Assert(WantRules.Fulfil(ref life, Activity.Ale, 4, 700, ended) == 0, "An ale answers no supper.");
            Assert(WantRules.Fulfil(ref life, Activity.Supper, 4, 700, ended) == 1 && !WantRules.Holds(life, Want.TavernSupper, 700), "A supper does.");
            Assert(!WantRules.Saw(ref life, Ama, 800) && WantRules.Saw(ref life, Bram, 800), "Meeting Bram answers the want to see Bram.");
        }

        private void ValidateTheEntries()
        {
            var ama = new AgentState(Ama);
            Assert(ama.Want(Want.QuietWalk, 100, Activity.Walk), "Ama wants a quiet walk.");
            ama.FulfilWants(Activity.Walk, 2, 200);
            Assert(!WantRules.Holds(ama.Life, Want.QuietWalk, 200) && ThoughtRules.Holds(ama.Life, Thought.AQuietWalk, 200), "Walked: it comes true and leaves its thought.");
            Assert(ama.Want(Want.TavernSupper, 300, Activity.Supper), "Then a supper.");
            uint late = 300 + 25 * (uint)DayClock.TicksPerHour;
            ama.ExpireWants(late);
            Assert(!WantRules.Holds(ama.Life, Want.TavernSupper, late) && ThoughtRules.Holds(ama.Life, Thought.MissedMySupper, late), "Missed: it runs out with its small bad thought.");
            Assert(ama.Want(Want.SeeAFriend, late, friend: Bram), "To see Bram.");
            ama.Saw(Bram, late + 10);
            Assert(ThoughtRules.Holds(ama.Life, Thought.SawAFriend, late + 10) && ama.Life.SubjectAt(1) == Bram || ama.Life.SubjectAt(2) == Bram, "Saw Bram, naming him.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[WantsValidator] FAILED: " + message);
        }
    }
}
