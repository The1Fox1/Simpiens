using System;
using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Deeds (Epic 11, S0.5, D2): the table's rows are sound; a crawler's traits record counts each deed, up to its counter's
    /// most, and ignores no deed and ids past its room; a kill counts the slayer's creature slain, and a boss's both. The
    /// other deeds are counted where they happen in the live scenarios that bring them about (a make, a rescue, a downing
    /// survived, a hunt paid, a clear won). Runs as a test (ValidatorTests).
    /// </summary>
    public class DeedsValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[DeedsValidator] Beginning deeds validation...");
            ValidateTheTable();
            ValidateTheCounters();
            ValidateAKillIsCounted();
            TestLog.Log("<color=green>[DeedsValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheTable()
        {
            var all = DeedTable.All;
            Assert(all.Count == 8, $"Eight deeds (Epic 11, S3.1 added the taught), {all.Count} in the table.");
            var names = new HashSet<string>();
            byte last = 0;
            foreach (var deed in all)
            {
                Assert((byte)deed.Id > last && (byte)deed.Id < DeedTable.Capacity, $"{deed.Name}: ids rise, under {DeedTable.Capacity}.");
                last = (byte)deed.Id;
                Assert(names.Add(deed.Name) && !string.IsNullOrEmpty(deed.DisplayName), $"{deed.Name}: a unique name and the player's words.");
                Assert(DeedTable.Get(deed.Id) == deed && DeedTable.NameOf(deed.Id) == deed.Name, $"{deed.Name} is found by its id.");
            }
            foreach (Deed id in Enum.GetValues(typeof(Deed)))
            {
                Assert(id == Deed.None || DeedTable.Get(id) != null, $"Deed {id} has a row.");
            }
            Assert(DeedTable.Get(Deed.None) == null && DeedTable.Get((Deed)DeedTable.Capacity) == null, "No deed and an id past the room have no row.");
            TestLog.Log("[PASS] Test 1: every deed has one row, its id under the room, with a name and the player's words.");
        }

        private void ValidateTheCounters()
        {
            var traits = default(CrawlerTraits);
            Assert(traits.IsEmpty, "A new record is empty.");
            traits.Add(Deed.Rescue);
            traits.Add(Deed.Rescue);
            traits.Add(Deed.Make);
            Assert(traits.Count(Deed.Rescue) == 2 && traits.Count(Deed.Make) == 1 && traits.Count(Deed.ClearWon) == 0 && !traits.IsEmpty, "Each deed has its own count.");
            traits.Add(Deed.None);
            traits.Add((Deed)DeedTable.Capacity);
            Assert(traits.Count(Deed.None) == 0 && traits.Count((Deed)DeedTable.Capacity) == 0, "No deed, and an id past the room, count nothing.");
            for (int i = 0; i < ushort.MaxValue + 5; i++) traits.Add(Deed.HuntPaid);
            Assert(traits.Count(Deed.HuntPaid) == ushort.MaxValue, "A count stops at its counter's most.");
            Assert(traits.PassionFor(Skill.Melee) == Passion.None && traits.TeacherOf(Skill.Melee) == default, "Counting a deed writes nothing else.");
            TestLog.Log("[PASS] Test 2: the traits record counts each deed to its counter's most, and ignores what isn't one.");
        }

        private void ValidateAKillIsCounted()
        {
            // Where a kill happens (the creatures' world's CreatureSlain), the slayer counts it; a boss counts twice over
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);
            var slayer = HeadlessEntities.Crawler(registry, sim, null, new Vec2(1f, 1f));
            var bystander = HeadlessEntities.Crawler(registry, sim, null, new Vec2(2f, 1f));
            var world = (ICreatureWorld)sim;
            world.CreatureSlain(slayer.Id, new CreatureState(EntityId.Generate(), CreatureKind.BurrowRat, 0, 0, new Vec2(3f, 3f)), ReadOnlySpan<Simpiens.Simulation.Economy.ItemInstance>.Empty, 10);
            Assert(slayer.Agent.Traits.Count(Deed.CreatureSlain) == 1 && slayer.Agent.Traits.Count(Deed.BossSlain) == 0, "A rat slain is one creature slain.");
            world.CreatureSlain(slayer.Id, new CreatureState(EntityId.Generate(), CreatureKind.Gatekeeper, 0, 0, new Vec2(3f, 3f)), ReadOnlySpan<Simpiens.Simulation.Economy.ItemInstance>.Empty, 20);
            Assert(slayer.Agent.Traits.Count(Deed.CreatureSlain) == 2 && slayer.Agent.Traits.Count(Deed.BossSlain) == 1, "A boss slain is a creature slain and a boss slain.");
            Assert(bystander.Agent.Traits.IsEmpty, "Nobody else counts the slayer's deed.");
            world.CreatureSlain(EntityId.Generate(), new CreatureState(EntityId.Generate(), CreatureKind.BurrowRat, 0, 0, new Vec2(3f, 3f)), ReadOnlySpan<Simpiens.Simulation.Economy.ItemInstance>.Empty, 30);
            Assert(slayer.Agent.Traits.Count(Deed.CreatureSlain) == 2, "A slayer who isn't a crawler here counts nowhere.");
            TestLog.Log("[PASS] Test 3: a kill is the slayer's creature slain; a boss's is a boss slain as well.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[DeedsValidator] FAILED: " + message);
        }
    }
}
