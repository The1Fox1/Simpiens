using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Reading a manual (Epic 11, S3.3, Q10, D11): read through, a primer is used up and its 300 experience practised at
    /// source Manual, times the reader's passion, to its ceiling; interrupted, the reader keeps the book and gains nothing;
    /// a reader past the ceiling uses the book up for nothing. Runs as a test (ValidatorTests).
    /// </summary>
    public class ReadingValidator
    {
        private SimulationManager _sim;
        private WorldRegistry _world;
        private readonly List<(AgentState Who, Skill Skill, int Experience, PracticeSource Source)> _acts = new List<(AgentState, Skill, int, PracticeSource)>();

        public void RunAllValidationTests()
        {
            TestLog.Log("[ReadingValidator] Beginning reading validation...");
            ValidateAReadingThrough();
            ValidateAPassionAndTheCeiling();
            ValidateAnInterruptedReading();
            TestLog.Log("<color=green>[ReadingValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateAReadingThrough()
        {
            NewWorld();
            var (node, reader) = Reader(Passion.None);
            int ticks = Ticks();
            var result = _sim.RunToCompletion(node, Read(node), ticks + 5);
            Assert(result == IntentResult.Success, $"Read through ({result}).");
            Assert(reader.Inventory.GetItemCount(ItemType.RangedPrimer) == 0, "The primer is used up.");
            Assert(reader.Skills.Experience(Skill.Ranged) == ManualBlock.PrimerExperience, $"Its 300 experience, from nothing to Ranged 2 ({reader.Skills.Experience(Skill.Ranged)}).");
            var act = _acts.Find(a => a.Who == reader);
            Assert(act.Source.Kind == PracticeKind.Manual && act.Source.Ceiling == ManualBlock.PrimerCeiling && act.Skill == Skill.Ranged, "Told at source Manual, to 3.");
            Assert(ticks == 2 * DayClock.TicksPerHour, $"A reading takes two game hours ({ticks} ticks).");
            TestLog.Log("[PASS] Test 1: read through, a primer is used up and gives its 300 experience at source Manual.");
        }

        private void ValidateAPassionAndTheCeiling()
        {
            NewWorld();
            var (node, reader) = Reader(Passion.Keen);
            var skills = reader.Skills;
            skills.Set(Skill.Ranged, 2);
            reader.Skills = skills;
            _sim.RunToCompletion(node, Read(node), Ticks() + 5);
            Assert(reader.Skills.Level(Skill.Ranged) == ManualBlock.PrimerCeiling && reader.Skills.Experience(Skill.Ranged) == SkillRules.ExperienceFor(ManualBlock.PrimerCeiling),
                $"Keen, 450 from Ranged 2 would pass 3: held to the primer's ceiling ({reader.Skills.Experience(Skill.Ranged)}).");
            // Past the ceiling, the book is used up for nothing
            reader.Inventory.TryAddItem(new ItemInstance(ItemType.RangedPrimer, 1, 30), out _);
            int had = reader.Skills.Experience(Skill.Ranged);
            Assert(_sim.RunToCompletion(node, Read(node), Ticks() + 5) == IntentResult.Success && reader.Skills.Experience(Skill.Ranged) == had
                && reader.Inventory.GetItemCount(ItemType.RangedPrimer) == 0, "A reader at the ceiling uses the book up and gains nothing.");
            TestLog.Log("[PASS] Test 2: a passion multiplies the reading, and the primer's ceiling holds.");
        }

        private void ValidateAnInterruptedReading()
        {
            NewWorld();
            var (node, reader) = Reader(Passion.Burning);
            var state = Read(node);
            Assert(_sim.RunToCompletion(node, state, Ticks() / 2) == IntentResult.InProgress, "Halfway, still reading.");
            Assert(reader.Skills.Experience(Skill.Ranged) == 0 && reader.Inventory.GetItemCount(ItemType.RangedPrimer) == 1, "Interrupted halfway: nothing gained, the book kept.");
            reader.Inventory.TryRemoveItem(ItemType.RangedPrimer, 1);
            Assert(_sim.RunToCompletion(node, Read(node), 5) == IntentResult.TargetMissing, "With no manual, nothing to read.");
            TestLog.Log("[PASS] Test 3: an interrupted reading gains nothing and keeps the book; with none, nothing to read.");
        }

        private void NewWorld()
        {
            _world = new WorldRegistry();
            _sim = new SimulationManager(_world, null);
            _sim.SetEconomy(EconomyTuning.Default);
            _acts.Clear();
            _sim.Practised += (who, skill, experience, source) => _acts.Add((who, skill, experience, source));
        }

        private (WorldEntity Node, AgentState Reader) Reader(Passion passion)
        {
            var crawler = HeadlessEntities.Crawler(_world, _sim, null, Vec2.Zero, id: EntityId.Generate());
            var reader = crawler.Agent;
            reader.Skills = default;
            var traits = default(CrawlerTraits);
            traits.SetPassion(Skill.Ranged, passion);
            reader.Traits = traits;
            reader.Inventory.TryAddItem(new ItemInstance(ItemType.RangedPrimer, 1, 30), out _);
            return (crawler.Node, reader);
        }

        private static ActiveIntentState Read(WorldEntity node)
            => new ActiveIntentState { Intent = new ReadManualIntent(node.Id), CurrentWaypointIndex = 0, LastSampledPosition = node.Position };

        private int Ticks() => (int)Math.Round(_sim.Economy.Day.Actions.ReadSeconds / SimulationClock.FixedTickDeltaTime);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ReadingValidator] FAILED: " + message);
        }
    }
}
