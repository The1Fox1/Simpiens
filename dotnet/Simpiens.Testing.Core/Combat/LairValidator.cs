using System;
using System.Collections.Generic;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Combat
{
    /// <summary>
    /// The lairs (Epic 10 Part 3, K2.1; Q5): every lair is filled at the start to a pack drawn from the seed within its
    /// kind's range, each member whole in its place; the same seed fills them the same, with ids from the creatures' own
    /// sequence (never the entities'); the creatures are kept in id order (seam A4); a slain member returns to its place
    /// after its kind's hours of the day, and not before; and the phase allocates nothing while nobody is due. Runs as a
    /// test (ValidatorTests).
    /// </summary>
    public class LairValidator
    {
        private const ulong Seed = 424242;

        public void RunAllValidationTests()
        {
            TestLog.Log("[LairValidator] Beginning lair validation...");

            ValidateTheLairsFillOnTheSeed();
            ValidateTheSameSeedFillsThemTheSame();
            ValidateEveryPackSizeTurnsUp();
            ValidateASlainMemberReturnsInItsHours();
            ValidateThePhaseAllocatesNothing();
            ValidateALairHeadedByABoss();

            TestLog.Log("<color=green>[LairValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheLairsFillOnTheSeed()
        {
            var (service, world) = Filled(Seed);
            Assert(service.Lairs.Count == 2, "Floor1's two lairs are at play.");
            int expected = 0;
            for (int l = 0; l < service.Lairs.Count; l++)
            {
                var lair = service.Lairs[l];
                var kind = CreatureCatalog.Get(lair.Definition.Kind);
                Assert(lair.Pack >= kind.PackMin && lair.Pack <= kind.PackMax && lair.Alive == lair.Pack, $"{lair.Definition.Key}: {lair.Pack} {kind.Name}, all there (range {kind.PackMin}-{kind.PackMax}).");
                expected += lair.Pack;
                for (int slot = 0; slot < lair.Pack; slot++)
                {
                    Assert(service.TryGet(lair.MemberIn(slot), out var c), $"{lair.Definition.Key}.{slot}: held by a creature it knows.");
                    Assert(c.Kind == kind.Id && c.LairIndex == l && c.Slot == slot && c.Health == kind.MaxHealth, $"{c}: of its lair's kind, in its place, whole.");
                    Assert(c.Position == CreatureService.PlaceOf(lair.Definition.Position, slot) && c.Home == c.Position, $"{c}: resting at its place.");
                    Assert(world.Entities.TryGetValue(c.Id, out var entity) && entity.Type == EntityType.Creature && entity.Creature == c && entity.Position == c.Position, $"{c}: in the world as a creature entity, its position the creature's.");
                    Assert(entity.Radius == kind.BodyRadius && entity.BaseSpeed == kind.Speed, $"{c}: its kind's body and speed.");
                }
            }
            Assert(service.Creatures.Count == expected && world.Entities.Count == expected, $"{expected} creatures, every one in the world.");
            for (int i = 1; i < service.Creatures.Count; i++)
                Assert(service.Creatures[i - 1].Id.CompareTo(service.Creatures[i].Id) < 0, "The creatures are in id order, the phase's order (A4).");
            TestLog.Log($"[PASS] Test 1: the lairs fill on the seed: {service.Lairs[0].Pack} rats in the warren, {service.Lairs[1].Pack} beetles in the hollow, each whole in its place.");
        }

        private void ValidateTheSameSeedFillsThemTheSame()
        {
            var (a, _) = Filled(Seed);
            var (b, _) = Filled(Seed);
            Assert(a.Creatures.Count == b.Creatures.Count, "The same seed, the same packs.");
            for (int i = 0; i < a.Creatures.Count; i++) Assert(a.Creatures[i].Id == b.Creatures[i].Id && a.Creatures[i].Slot == b.Creatures[i].Slot, "The same seed, the same ids.");
            Assert(a.IdCounter == a.Creatures.Count, "One id from the creatures' sequence a creature.");

            // Never the entities' sequence: the first hundred entity ids of the seed are none of them
            var entities = new HashSet<EntityId>();
            var allocator = new EntityIdAllocator(new SimulationSettings(Seed));
            for (int i = 0; i < 100; i++) entities.Add(allocator.Next());
            for (int i = 0; i < a.Creatures.Count; i++) Assert(!entities.Contains(a.Creatures[i].Id), "A creature's id comes from its own sequence.");
            var (other, _) = Filled(Seed + 1);
            Assert(other.Creatures[0].Id != a.Creatures[0].Id, "Another seed, other ids.");
            TestLog.Log("[PASS] Test 2: the same seed fills the lairs the same, with ids of the creatures' own sequence.");
        }

        private void ValidateEveryPackSizeTurnsUp()
        {
            var seen = new HashSet<(int, int)>();
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var (service, _) = Filled(seed);
                for (int l = 0; l < service.Lairs.Count; l++) seen.Add((l, service.Lairs[l].Pack));
            }
            Assert(seen.SetEquals(new[] { (0, 3), (0, 4), (1, 1), (1, 2) }), $"Across 60 seeds every pack size in range turns up, and no other ({string.Join(", ", seen)}).");
            TestLog.Log("[PASS] Test 3: every pack size in its kind's range turns up across the seeds: the warren holds 3 or 4 rats, the hollow 1 or 2 beetles.");
        }

        private void ValidateASlainMemberReturnsInItsHours()
        {
            var (service, world) = Filled(Seed);
            var warren = service.Lairs[0];
            var slain = warren.MemberIn(1);
            const uint At = 5000;
            uint due = At + DayClock.Hours(CreatureCatalog.Get(CreatureKind.BurrowRat).RespawnHours);
            Assert(service.Slay(world, slain, At), "A rat is slain.");
            Assert(!service.TryGet(slain, out _) && !world.Entities.ContainsKey(slain), "It is out of the world.");
            Assert(warren.Alive == warren.Pack - 1 && warren.MemberIn(1) == default && warren.ReturnAt(1) == due, $"Its place is empty until t{due}, two game days on.");
            Assert(!service.Slay(world, slain, At), "Slain once is slain.");
            service.Tick(world, due - 1);
            Assert(warren.Alive == warren.Pack - 1, "Not a tick before its time.");
            service.Tick(world, due);
            var back = warren.MemberIn(1);
            Assert(back != default && back != slain && service.TryGet(back, out var c) && c.Slot == 1 && c.Health == c.Definition.MaxHealth && c.Position == CreatureService.PlaceOf(warren.Definition.Position, 1),
                "A new rat is back in the same place, whole, with a new id.");
            Assert(warren.Alive == warren.Pack && warren.ReturnAt(1) == 0 && world.Entities.ContainsKey(back), "The warren is whole again, the rat in the world.");
            service.Tick(world, due + DayClock.TicksPerDay);
            Assert(service.Creatures.Count == world.Entities.Count, "Nobody more comes: a lair never holds more than its pack.");
            for (int i = 1; i < service.Creatures.Count; i++) Assert(service.Creatures[i - 1].Id.CompareTo(service.Creatures[i].Id) < 0, "Still in id order.");
            TestLog.Log("[PASS] Test 4: a slain rat leaves the world, and a new one returns to its place two game days on, not a tick before.");
        }

        private void ValidateThePhaseAllocatesNothing()
        {
            var (service, world) = Filled(Seed);
            service.Tick(world, 1); // warm
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint t = 2; t < 2000; t++) service.Tick(world, t);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0, $"2,000 ticks of the phase allocated {allocated} bytes.");
            TestLog.Log("[PASS] Test 5: the creatures' phase allocates nothing while nobody is due.");
        }

        /// <summary>
        /// A lair headed by a boss (Epic 10 Part 3, K10, Q13: the Matriarch heads the warren), on a test variant of the floor:
        /// the boss holds the first place, at the heart, and the brood the places after; the lair's reach, danger and the lone
        /// judgement count her; a save's check wants her kind in her place.
        /// </summary>
        private void ValidateALairHeadedByABoss()
        {
            var heart = new Simpiens.Vec2(-13f, 1f);
            var map = Floor1Layout.BuildWith(b => b.Lair("test.brood", CreatureKind.BurrowRat, heart, boss: CreatureKind.Matriarch));
            var service = new CreatureService().Follow(map);
            var world = new FakeWorld();
            service.Populate(world, Seed);
            int index = service.Lairs.Count - 1;
            var lair = service.Lairs[index];
            var definition = lair.Definition;
            var rat = CreatureCatalog.Get(CreatureKind.BurrowRat);
            var matriarch = CreatureCatalog.Get(CreatureKind.Matriarch);
            Assert(definition.HasBoss && definition.KindIn(0) == CreatureKind.Matriarch && definition.KindIn(1) == CreatureKind.BurrowRat, "The lair names its boss for its first place, its brood after.");
            Assert(lair.Pack == 1 + CreatureService.PackSize(Seed, index, rat) && lair.Pack == CreatureService.PackOf(Seed, index, definition), $"Her place and the brood's drawn pack: {lair.Pack}.");
            Assert(service.TryGet(lair.MemberIn(0), out var her) && her.Kind == CreatureKind.Matriarch && her.Home == heart && her.Health == matriarch.MaxHealth, "The Matriarch rests at the heart, whole.");
            for (int slot = 1; slot < lair.Pack; slot++)
                Assert(service.TryGet(lair.MemberIn(slot), out var c) && c.Kind == CreatureKind.BurrowRat, $"Place {slot} holds a rat of her brood.");

            // Her reach, danger and the lone judgement
            var plain = new Simpiens.Simulation.World.LairDefinition("plain", CreatureKind.BurrowRat, heart);
            Assert(LairReach.Of(definition) == SimMath.Max(rat.Leash, matriarch.Leash), "Her lair holds as far as the longer leash.");
            Assert(LairReach.Danger(definition, 0f) > LairReach.Danger(plain, 0f) && SimMath.Approximately(LairReach.Danger(definition, 0f), LairReach.Danger(plain, 0f) + LairReach.HarmPerSecond(matriarch, 0f)), "Her lair's danger is the brood's and hers.");
            var skills = default(Simpiens.Simulation.Skills.SkillSet);
            var armed = new Simpiens.Cognition.Inventory.AgentInventory();
            armed.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(Simpiens.Simulation.Economy.ItemType.IronBlade, Simpiens.Simulation.Items.Grades.Standard));
            float without = Simpiens.Cognition.Vitality.HarmToClear(plain, skills, armed), with = Simpiens.Cognition.Vitality.HarmToClear(definition, skills, armed);
            Assert(with > without, $"Clearing her lair costs more than her brood alone ({with:0} against {without:0}).");
            Assert(!Simpiens.Cognition.Vitality.BravesLair(definition, skills, armed, 100f, 100, downed: false), $"Nobody braves her lair alone with an iron blade ({with:0} expected harm).");

            // A save wants her in her place
            var save = service.Capture();
            Assert(CreatureService.Check(save, map) == null, "Her lair's save checks.");
            var wrong = service.Capture();
            for (int i = 0; i < wrong.Creatures.Length; i++)
                if (wrong.Creatures[i].Lair == index && wrong.Creatures[i].Slot == 0) wrong.Creatures[i].Kind = CreatureKind.BurrowRat;
            Assert(CreatureService.Check(wrong, map) != null, "A rat in her place is refused.");
            TestLog.Log($"[PASS] Test 6: a lair headed by its boss: the Matriarch at the heart and {lair.Pack - 1} of her brood; her lair's reach, danger ({LairReach.Danger(definition, 0f):0.0} a second) and cost to clear ({with:0}, {without:0} without her) count her; a save wants her in her place.");
        }

        private static (CreatureService Service, FakeWorld World) Filled(ulong seed)
        {
            var service = new CreatureService().Follow(Floor1Layout.Definition);
            var world = new FakeWorld();
            service.Populate(world, seed);
            return (service, world);
        }

        private sealed class FakeWorld : ICreatureWorld
        {
            public readonly Dictionary<EntityId, WorldEntity> Entities = new Dictionary<EntityId, WorldEntity>();
            private readonly List<WorldEntity> _none = new List<WorldEntity>();
            public void AddCreature(WorldEntity entity) => Entities.Add(entity.Id, entity);
            public void RemoveCreature(EntityId id) => Entities.Remove(id);
            public void Practise(Simpiens.Cognition.AgentState agent, in Simpiens.Simulation.Skills.SkillWork work, int experience, in Simpiens.Simulation.Skills.PracticeSource source) => agent?.Practise(Simpiens.Simulation.Skills.SkillTable.SkillFor(work), experience, out _, source.Ceiling);
            public Simpiens.Simulation.Spatial.SharedWorldSnapshot Snapshot => null;
            IReadOnlyList<WorldEntity> ICreatureWorld.Entities => _none;
            public void Strike(WorldEntity crawler, EntityId attacker, in Simpiens.Simulation.Combat.SwingOutcome outcome, uint tick) { }
            public void CreatureSlain(EntityId slayer, CreatureState creature, System.ReadOnlySpan<Simpiens.Simulation.Economy.ItemInstance> drops, uint tick) { }
            public void CreatureHit(EntityId crawler, CreatureState creature, float damage) { }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[LairValidator ASSERTION FAILED] {message}");
        }
    }
}
