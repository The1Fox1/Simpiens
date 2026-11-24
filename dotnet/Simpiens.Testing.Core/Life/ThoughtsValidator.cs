using System;
using System.Collections.Generic;
using Simpiens.Simulation.Life;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Life
{
    /// <summary>
    /// The thoughts' table (Epic LIFE-1, L1.1, D7): every row is found by its id, names a subject exactly when its words hold
    /// "{0}", speaks in the client's fonts (Latin-1), and keeps its value, duration and stacks within the table's bounds; the
    /// compounding stack rule adds less with each stack. Runs as a test (ValidatorTests).
    /// </summary>
    public class ThoughtsValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[ThoughtsValidator] Beginning thoughts validation...");
            ValidateTheTable();
            ValidateTheBounds();
            ValidateStacking();
            TestLog.Log("<color=green>[ThoughtsValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTable()
        {
            var all = ThoughtTable.All;
            Assert(all.Count == 36, $"D7's 21 thoughts, plan 2's six activities' and nine wants' ({all.Count} in the table).");
            var names = new HashSet<string>();
            ushort last = 0;
            foreach (var row in all)
            {
                Assert((ushort)row.Id > last && (ushort)row.Id < ThoughtTable.Capacity, $"{row.Name}: ids rise, under {ThoughtTable.Capacity}.");
                last = (ushort)row.Id;
                Assert(row.Name == row.Id.ToString(), $"{row.Name}: its code name is its enum's ({row.Id}).");
                Assert(names.Add(row.Name), $"{row.Name}: a unique name.");
                Assert(ThoughtTable.Get(row.Id) == row && ThoughtTable.NameOf(row.Id) == row.Name, $"{row.Name} is found by its id.");
            }
            foreach (Thought id in Enum.GetValues(typeof(Thought)))
                Assert(id == Thought.None ? ThoughtTable.Get(id) == null : ThoughtTable.Get(id) != null, $"{id}: every thought but None has a row.");
            Assert(ThoughtTable.Get((Thought)999) == null && ThoughtTable.NameOf(Thought.None) == "None", "No row past the table; None has none.");
        }

        private void ValidateTheBounds()
        {
            foreach (var row in ThoughtTable.All)
            {
                Assert(!string.IsNullOrEmpty(row.Words) && char.IsUpper(row.Words[0]) || row.Words.StartsWith("{0}"), $"{row.Name}: words that start a line.");
                foreach (char c in row.Words) Assert(c <= 'ÿ', $"{row.Name}: Latin-1 only, the client's fonts hold no more ('{c}').");
                Assert(row.Value != 0 && Math.Abs(row.Value) <= ThoughtTable.MaxValue, $"{row.Name}: a value from 1 to {ThoughtTable.MaxValue} either way ({row.Value}).");
                Assert(row.Hours >= 1 && row.Hours <= ThoughtTable.MaxHours, $"{row.Name}: lasts 1 to {ThoughtTable.MaxHours} game hours ({row.Hours}).");
                Assert(row.StackLimit >= 1 && row.StackLimit <= ThoughtTable.MaxStacks, $"{row.Name}: 1 to {ThoughtTable.MaxStacks} stacks ({row.StackLimit}).");
                Assert(row.StackFactor > 0f && row.StackFactor <= 1f, $"{row.Name}: a stack factor in (0, 1] ({row.StackFactor}).");
                Assert(row.NamesSubject == (row.Kind == ThoughtKind.Social && row.Id != Thought.AGoodTalk && row.Id != Thought.SharedMyFood),
                    $"{row.Name}: names a crawler exactly when it is about one ({row.Words}).");
            }
        }

        private void ValidateStacking()
        {
            var meal = ThoughtTable.Get(Thought.AGoodMeal); // 3, factor 0.5
            Assert(meal.ValueAt(0) == 0f && meal.ValueAt(1) == 3f && meal.ValueAt(2) == 4.5f, $"Stacks compound down: 3, then 4.5 ({meal.ValueAt(2)}).");
            var talk = ThoughtTable.Get(Thought.AGoodTalk); // 2, factor 0.5, three stacks
            Assert(talk.ValueAt(3) == 3.5f, $"Three talks: 2 + 1 + 0.5 ({talk.ValueAt(3)}).");
            var death = ThoughtTable.Get(Thought.FriendDied);
            Assert(death.ValueAt(1) == -25f && death.NamesSubject, "A friend's death: -25 (L1.3), naming the dead.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ThoughtsValidator] FAILED: " + message);
        }
    }
}
