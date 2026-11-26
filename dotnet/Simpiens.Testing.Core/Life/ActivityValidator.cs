using System;
using System.Linq;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// The activities' table (Epic LIFE-1 plan 2, L2.2, D12): ids and words, fills and durations within their bounds, company
    /// in range, variety groups within the life record's eight bytes, and each recreation's thought a row of the thoughts'
    /// table that names no one. The fireside is company's (its relax is <c>GatherAtAnchor</c>'s) and leaves no thought.
    /// Runs as a test (ValidatorTests).
    /// </summary>
    public class ActivityValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[ActivityValidator] Beginning activity validation...");
            ValidateTheRows();
            ValidateTheThoughts();
            TestLog.Log("<color=green>[ActivityValidator] ALL 2 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheRows()
        {
            var all = ActivityTable.All;
            Assert(all.Count == 7, $"D12's seven activities ({all.Count}).");
            Assert(all.Select(a => a.Id).Distinct().Count() == all.Count && all.All(a => ActivityTable.Get(a.Id) == a), "Each found by its own id.");
            Assert(ActivityTable.Get(Activity.None) == null, "No row for none.");
            foreach (var row in all)
            {
                Assert(row.Name == row.Id.ToString() && !string.IsNullOrEmpty(row.Words) && row.Words.All(c => c < 256), $"{row.Id}: its name, and words in Latin-1.");
                Assert(row.FunPerHour > 0 && row.FunPerHour <= ActivityTable.MaxFunPerHour, $"{row.Id}: fun a game hour within 1..{ActivityTable.MaxFunPerHour} ({row.FunPerHour}).");
                Assert(row.SocialPerHour <= ActivityTable.MaxSocialPerHour, $"{row.Id}: company a game hour within {ActivityTable.MaxSocialPerHour}.");
                Assert(row.Hunger <= 0, $"{row.Id}: an activity eases hunger or leaves it.");
                Assert(row.Minutes <= ActivityTable.MaxMinutes, $"{row.Id}: a spell within {ActivityTable.MaxMinutes} minutes.");
                Assert((byte)row.Group < ActivityTable.VarietyGroups, $"{row.Id}: its variety group within the record's eight.");
                Assert(row.CompanyMin <= row.CompanyMax || row.CompanyMax == 0, $"{row.Id}: company from {row.CompanyMin} to {row.CompanyMax}.");
                Assert(row.CompanyMax <= ActivityTable.MaxCompany, $"{row.Id}: at most {ActivityTable.MaxCompany} at one activity.");
                Assert(row.IsRecreation == (row.Id != Activity.Fireside), $"{row.Id}: Recreate offers every activity but the fireside.");
            }
        }

        private void ValidateTheThoughts()
        {
            foreach (var row in ActivityTable.All)
            {
                if (!row.IsRecreation)
                {
                    Assert(row.Thought == Thought.None, $"{row.Id}: the fireside's talk is gossip's; it leaves no thought of its own.");
                    continue;
                }
                var thought = ThoughtTable.Get(row.Thought);
                Assert(thought != null && thought.Value > 0 && !thought.NamesSubject, $"{row.Id}: its thought is a good one that names no one ({row.Thought}).");
                Assert(ActivityTable.All.Count(a => a.Thought == row.Thought) == 1, $"{row.Id}: its thought is its own.");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ActivityValidator] FAILED: " + message);
        }
    }
}
