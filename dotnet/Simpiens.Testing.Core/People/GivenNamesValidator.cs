using System;
using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.People;

namespace Simpiens.Testing.People
{
    /// <summary>
    /// Epic UI-Beta B1, given names (Q7; plan D5, D7): the lists are long enough, never share a name, hold only Latin-1
    /// letters within the length the layouts allow; a draw is deterministic, fits the body frame, and skips names already
    /// seen while any are left, then only the living's.
    /// </summary>
    public class GivenNamesValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[GivenNamesValidator] Beginning given-name validation...");

            ValidateLists();
            ValidateDrawFitsTheFrame();
            ValidateDrawSkipsTakenNames();

            TestLog.Log("<color=green>[GivenNamesValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateLists()
        {
            var all = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (label, list) in new[] { ("feminine", GivenNames.Feminine), ("masculine", GivenNames.Masculine), ("neutral", GivenNames.Neutral) })
            {
                Assert(list.Count >= 48, $"The {label} list has {list.Count} names; Q7 asks for at least 48.");
                foreach (var name in list)
                {
                    Assert(all.Add(name), $"'{name}' is in two lists, or twice in one.");
                    Assert(name.Length >= 2 && name.Length <= GivenNames.MaxLength, $"'{name}' is {name.Length} characters; at most {GivenNames.MaxLength} fit the layouts.");
                    Assert(char.IsUpper(name[0]), $"'{name}' starts with a capital.");
                    foreach (char c in name)
                    {
                        Assert(c <= 'ÿ' && char.IsLetter(c), $"'{name}' holds '{c}': names are Latin-1 letters, what the baked fonts draw.");
                    }
                }
            }
            TestLog.Log($"[PASS] Test 1: {all.Count} names in three lists, none shared, Latin-1 letters, at most {GivenNames.MaxLength} characters.");
        }

        private static void ValidateDrawFitsTheFrame()
        {
            byte female = LookCatalog.IdOf(LookGene.Frame, "female"), male = LookCatalog.IdOf(LookGene.Frame, "male");
            var feminine = new HashSet<string>(GivenNames.Feminine);
            var masculine = new HashSet<string>(GivenNames.Masculine);
            int neutral = 0;
            for (uint i = 0; i < 2000; i++)
            {
                var id = new EntityId(i + 1u, 9u, 9u, 9u);
                string a = GivenNames.Draw(42, id, female, null, null);
                Assert(a == GivenNames.Draw(42, new EntityId(i + 1u, 9u, 9u, 9u), female, null, null), "The same seed and id give the same name.");
                Assert(!masculine.Contains(a), $"A female frame drew '{a}' from the masculine list.");
                string b = GivenNames.Draw(42, id, male, null, null);
                Assert(!feminine.Contains(b), $"A male frame drew '{b}' from the feminine list.");
                if (!feminine.Contains(a)) neutral++;
            }
            Assert(neutral > 500 && neutral < 1500, $"About half of a frame's names come from the neutral list ({neutral} of 2000).");
            TestLog.Log("[PASS] Test 2: A name is deterministic per (seed, id) and fits the body frame (its list or the neutral one).");
        }

        private static void ValidateDrawSkipsTakenNames()
        {
            byte female = LookCatalog.IdOf(LookGene.Frame, "female");
            var seen = new HashSet<string>();
            for (uint i = 0; i < (uint)(GivenNames.Feminine.Count + GivenNames.Neutral.Count); i++)
            {
                string name = GivenNames.Draw(7, new EntityId(i + 1u, 1u, 1u, 1u), female, seen, seen);
                Assert(seen.Add(name), $"Draw {i} repeated '{name}' while unseen names were left.");
            }

            // Every name seen: a living one is still never repeated; a departed one may come back
            var living = new HashSet<string> { GivenNames.Feminine[0] };
            string again = GivenNames.Draw(7, new EntityId(999u, 1u, 1u, 1u), female, seen, living);
            Assert(!living.Contains(again) && seen.Contains(again), $"With every name seen, the draw takes a departed crawler's name, not a living one's ('{again}').");
            TestLog.Log("[PASS] Test 3: Draws never repeat a seen name while any is left, then never a living one's.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
