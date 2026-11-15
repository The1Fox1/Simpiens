using System;
using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Creatures;

namespace Simpiens.Testing.Combat
{
    /// <summary>
    /// The creature catalogue (Epic 10 Part 3, K0.4; seam A6): every kind is consistent; the three kinds are what the draft's
    /// Q5 says (weak rats in fleeing packs, armoured slow beetles where a weapon matters, spinners that spit); a creature
    /// fights through the one combat rule; a new kind needs no code beyond its row; and reading one allocates nothing.
    /// Nothing spawns yet (lairs are K2.1). Runs as a test (ValidatorTests).
    /// </summary>
    public class CreatureCatalogValidator
    {
        private const float CrawlerHealth = 100f;
        private const float CrawlerSpeed = WorldEntity.DefaultBaseSpeed;
        private const float CrawlerRadius = 0.35f;

        public void RunAllValidationTests()
        {
            TestLog.Log("[CreatureCatalogValidator] Beginning creature catalogue validation...");

            ValidateDefinitionsAreConsistent();
            ValidateTheKindsAreWhatQ5Says();
            ValidateACreatureFightsThroughTheRule();
            ValidateAKindIsARow();
            ValidateReadingAllocatesNothing();
            ValidateTheBossesAreWhatQ13Says();

            TestLog.Log("<color=green>[CreatureCatalogValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateDefinitionsAreConsistent()
        {
            Assert(CreatureCatalog.Get(CreatureKind.None) == null && !CreatureCatalog.IsDefined(CreatureKind.None), "None is no kind.");
            Assert(CreatureCatalog.All.Count >= 3, "The catalogue holds Q5's three kinds.");
            var names = new HashSet<string>();
            var artKeys = new HashSet<string>(StringComparer.Ordinal);
            var previous = CreatureKind.None;
            foreach (var kind in CreatureCatalog.All)
            {
                string k = kind.Name;
                Assert(kind.Id > previous, $"{k}: All is in id order.");
                previous = kind.Id;
                Assert(ReferenceEquals(CreatureCatalog.Get(kind.Id), kind), $"{k}: looked up by its id.");
                Assert(!string.IsNullOrEmpty(kind.Name) && names.Add(kind.Name), $"{k}: a unique code name.");
                Assert(!string.IsNullOrEmpty(kind.DisplayName), $"{k}: a display name.");
                Assert(IsKebabCase(kind.ArtKey) && artKeys.Add(kind.ArtKey), $"{k}: a unique art key in kebab case ('{kind.ArtKey}').");

                // Its body and its fight
                Assert(kind.MaxHealth > 0f && kind.Attack > 0f && kind.Defense >= 0f, $"{k}: health {kind.MaxHealth}, attack {kind.Attack}, defense {kind.Defense}.");
                Assert(kind.Reach > 0f && kind.SwingIntervalSeconds > 0f && kind.SwingIntervalTicks >= 1, $"{k}: reach {kind.Reach}, a swing every {kind.SwingIntervalSeconds} s.");
                Assert(kind.BodyRadius > 0f && kind.BodyRadius <= 1.5f, $"{k}: a body of radius {kind.BodyRadius}, within a boss's 64-pixel sprite.");
                Assert(kind.Speed > 0f, $"{k}: moves ({kind.Speed}).");
                Assert(kind.Range == 0f || kind.Range > kind.Reach, $"{k}: a range ({kind.Range}) is past its reach ({kind.Reach}).");

                // How it hunts: it can chase what it sees from home, and see what it shoots
                Assert(kind.Sight > 0f && kind.Leash >= kind.Sight, $"{k}: its leash ({kind.Leash}) reaches past its sight ({kind.Sight}).");
                Assert(!kind.IsRanged || kind.Sight >= kind.Range, $"{k}: it sees ({kind.Sight}) as far as it shoots ({kind.Range}).");
                Assert(kind.FleeBelow >= 0f && kind.FleeBelow < 1f && kind.Flees == kind.FleeBelow > 0f, $"{k}: flees below {kind.FleeBelow:P0} of its health, or never.");

                // Its lair and the day
                Assert(kind.PackMin >= 1 && kind.PackMax >= kind.PackMin, $"{k}: a pack of {kind.PackMin} to {kind.PackMax}.");
                Assert((kind.HealHours > 0 && kind.HealPerTick > 0f) || (kind.HealHours == 0 && kind.WholeAfterHours > 0), $"{k}: heals over {kind.HealHours} game hours at its lair, or stands whole once its lair is left ({kind.WholeAfterHours} h).");
                Assert(kind.HealHours == 0 || SimMath.Approximately(kind.HealPerTick * kind.HealHours * DayClock.TicksPerHour, kind.MaxHealth), $"{k}: healing over {kind.HealHours} hours makes it whole.");
                Assert(kind.WholeAfterHours >= 0 && kind.WholeAfterTicks == (kind.WholeAfterHours > 0 ? DayClock.Hours(kind.WholeAfterHours) : 0u), $"{k}: whole after {kind.WholeAfterHours} game hours left alone, or never.");
                Assert(kind.FuryBelow >= 0f && kind.FuryBelow < 1f && kind.FuryAttack >= 1f && kind.FuryInterval > 0f && kind.FuryInterval <= 1f, $"{k}: fury below {kind.FuryBelow:P0} strengthens it (attack x{kind.FuryAttack}, swing x{kind.FuryInterval}).");
                Assert(kind.CallsBroodBelow >= 0f && kind.CallsBroodBelow < 1f, $"{k}: calls its brood below {kind.CallsBroodBelow:P0}, or never.");
                Assert(kind.IsBoss || (!kind.Slam && !kind.HasFury && kind.CallsBroodBelow == 0f && kind.WholeAfterHours == 0), $"{k}: only a boss has a boss's traits.");
                Assert(kind.RespawnHours >= 0 && kind.RespawnTicks == DayClock.Hours(kind.RespawnHours) && kind.Respawns == kind.RespawnHours > 0, $"{k}: returns after {kind.RespawnHours} game hours, or never.");
                // What it drops (K4.2): a table that exists, whose rolls fit a kill's drops, of defined items in whole stacks
                Assert(CreatureLoot.Tables.TryGet(kind.LootTableId, out var loot), $"{k}: its loot table ({kind.LootTableId}) exists.");
                int stacks = loot.Every ? loot.EntryCount : loot.MaxRolls;
                Assert(stacks <= CreatureLoot.MaxDrops && loot.TotalWeight > 0, $"{k}: at most {CreatureLoot.MaxDrops} stacks a kill ({stacks}).");
                for (int e = 0; e < loot.EntryCount; e++)
                {
                    var entry = loot.GetEntry(e);
                    Assert(entry.Kind != Simpiens.Simulation.Items.LootEntryKind.Table, $"{k}: its table nests none, so a kill's drops are its rolls.");
                    if (entry.Kind != Simpiens.Simulation.Items.LootEntryKind.Item) continue;
                    var item = Simpiens.Simulation.Items.ItemCatalog.Get(entry.Item);
                    Assert(item != null && entry.MaxQuantity <= item.MaxStack, $"{k}: drops {entry.Item}, defined, in one stack ({entry.MaxQuantity}).");
                }
            }
            TestLog.Log($"[PASS] Test 1: {CreatureCatalog.All.Count} kinds, each consistent: a body, a fight, a hunt that can reach what it sees, a pack, healing and a return.");
        }

        private void ValidateTheKindsAreWhatQ5Says()
        {
            var rat = CreatureCatalog.Get(CreatureKind.BurrowRat);
            var beetle = CreatureCatalog.Get(CreatureKind.StoneBeetle);
            var spinner = CreatureCatalog.Get(CreatureKind.CaveSpinner);
            Assert(rat != null && beetle != null && spinner != null, "Rats, beetles and spinners are defined.");

            // Rats: weak, in packs of three or four, and they flee. The vermin are judged among themselves; the bosses (K10) by Test 6
            var vermin = new List<CreatureDefinition>();
            foreach (var kind in CreatureCatalog.All) if (!kind.IsBoss) vermin.Add(kind);
            foreach (var kind in CreatureCatalog.All) Assert(kind == rat || kind.MaxHealth > rat.MaxHealth, $"Rats are the weakest ({rat.MaxHealth} against {kind.Name}'s {kind.MaxHealth}).");
            Assert(rat.PackMin == 3 && rat.PackMax == 4 && rat.Flees, "Rats come in packs of three or four, and flee.");

            // Beetles: armoured and slow, where a weapon matters; a crawler can walk away from one
            foreach (var kind in vermin) Assert(kind == beetle || kind.Defense < beetle.Defense, $"Beetles are the most armoured ({beetle.Defense}).");
            foreach (var kind in vermin) Assert(kind == beetle || kind.Speed > beetle.Speed, $"Beetles are the slowest ({beetle.Speed}).");
            Assert(beetle.Speed < CrawlerSpeed && !beetle.Flees, "A crawler walks away from a beetle, which never flees.");
            Assert(CombatRules.Mitigated(CombatRules.UnarmedAttack, beetle.Defense) < 0.6f * CombatRules.UnarmedAttack, "A bare hand barely scratches a beetle.");

            // Spinners: spit from range
            Assert(spinner.IsRanged && !rat.IsRanged && !beetle.IsRanged, "Only spinners strike from range.");

            // A cleared lair refills after two game days
            foreach (var kind in vermin) Assert(kind.RespawnHours == 2 * DayClock.HoursPerDay, $"{kind.Name} returns after 2 game days.");
            TestLog.Log("[PASS] Test 2: rats are the weakest and flee in packs of three or four; beetles are the most armoured and the slowest of the vermin; spinners spit; all return after 2 game days.");
        }

        private void ValidateACreatureFightsThroughTheRule()
        {
            var beetle = CreatureCatalog.Get(CreatureKind.StoneBeetle);
            var id = new EntityId(12, 0, 0, 0);
            var read = Combatant.ForCreature(beetle, id, new Vec2(4f, 2f), beetle.MaxHealth);
            Assert(read.Id == id && read.Position == new Vec2(4f, 2f) && read.Health == beetle.MaxHealth && read.Standing, $"Who, where and how whole: {read}.");
            Assert(read.Attack == beetle.Attack && read.Defense == beetle.Defense && read.Reach == beetle.Reach && read.BodyRadius == beetle.BodyRadius, "What it brings is its kind's.");
            Assert(read.SwingInterval == 20 && read.Range == 0f && read.Proficiency == 0f, $"A swing every 2 s is 20 ticks ({read.SwingInterval}); no range, no practice.");
            Assert(!Combatant.ForCreature(beetle, id, default, 0f).Standing, "A creature at 0 health is down.");
            Assert(Combatant.ForCreature(CreatureCatalog.Get(CreatureKind.CaveSpinner), id, default, 30f).IsRanged, "A spinner's read is ranged.");

            // The fights Q5 describes, by the rule's expectation: seconds for each side to fell the other
            var bare = Crawler(attack: CombatRules.UnarmedAttack, intervalSeconds: CombatRules.UnarmedIntervalSeconds);
            var armed = Crawler(attack: 6f, intervalSeconds: 1.2f); // K3's placeholder cudgel: +4 attack, a fifth quicker
            var rat = CreatureCatalog.Get(CreatureKind.BurrowRat);
            Assert(SecondsToFell(bare, beetle) > 2f * SecondsToDown(beetle, 1), "A bare-handed crawler loses to a beetle.");
            Assert(SecondsToFell(armed, beetle) < SecondsToDown(beetle, 1), "An armed crawler fells a beetle first.");
            Assert(SecondsToFell(bare, rat) < SecondsToDown(rat, 1), "A bare-handed crawler outlasts one rat.");
            Assert(SecondsToFell(bare, rat) * rat.PackMin > SecondsToDown(rat, rat.PackMin), "A pack of rats downs a lone, bare-handed crawler.");
            TestLog.Log($"[PASS] Test 3: a creature's read is its kind's; through the one rule, a bare hand needs {SecondsToFell(bare, beetle):0} s against a beetle that downs it in {SecondsToDown(beetle, 1):0} s, a weapon {SecondsToFell(armed, beetle):0} s; a pack of rats downs a bare-handed crawler in {SecondsToDown(rat, rat.PackMin):0} s.");
        }

        private void ValidateAKindIsARow()
        {
            var mole = (CreatureKind)200;
            var row = new CreatureDefinition(mole, "TestMole", "Test Mole")
            {
                MaxHealth = 15f, Attack = 2f, Defense = 1f, Reach = 0.3f, SwingIntervalSeconds = 1.2f, Speed = 1.5f, BodyRadius = 0.3f,
                Sight = 3f, Leash = 5f, PackMin = 2, PackMax = 5, HealHours = 6, RespawnHours = 24
            };
            using (CreatureCatalog.ExtendForTest(row))
            {
                Assert(CreatureCatalog.IsDefined(mole) && ReferenceEquals(CreatureCatalog.Get(mole), row) && CreatureCatalog.All[CreatureCatalog.All.Count - 1] == row, "A row is a kind, last in id order.");
                Assert(CreatureCatalog.NameOf(mole) == "TestMole" && row.ArtKey == "test-mole", "Its names and art key follow from its row.");
                var fighter = Combatant.ForCreature(row, new EntityId(13, 0, 0, 0), default, row.MaxHealth);
                var outcome = CombatRules.Swing(424242UL, 5, fighter, Combatant.ForCreature(CreatureCatalog.Get(CreatureKind.BurrowRat), new EntityId(14, 0, 0, 0), new Vec2(0.8f, 0f), 20f));
                Assert(fighter.SwingInterval == 12 && (outcome.Hit ? outcome.Damage == 2f : outcome.Damage == 0f), "It fights through the rule with no code of its own.");
            }
            Assert(!CreatureCatalog.IsDefined(mole), "The test's kind is gone after its scope.");

            bool refused = false;
            try { using (CreatureCatalog.ExtendForTest(new CreatureDefinition(CreatureKind.BurrowRat, "Twin", "Twin"))) { } }
            catch (ArgumentException) { refused = true; }
            Assert(refused && CreatureCatalog.Get(CreatureKind.BurrowRat).Name == "BurrowRat", "Two kinds with one id are refused, and the catalogue is unchanged.");
            TestLog.Log("[PASS] Test 4: a new kind is a row: found by its id, named and fought with no code of its own; a duplicate id is refused.");
        }

        private void ValidateReadingAllocatesNothing()
        {
            var id = new EntityId(15, 0, 0, 0);
            Combatant.ForCreature(CreatureCatalog.Get(CreatureKind.BurrowRat), id, default, 20f); // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            float total = 0f;
            for (int i = 0; i < 10000; i++)
            {
                var kind = CreatureCatalog.Get((CreatureKind)(1 + i % 3));
                total += Combatant.ForCreature(kind, id, default, kind.MaxHealth).Attack + kind.HealPerTick;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0 && total > 0f, $"10,000 reads allocated {allocated} bytes.");
            TestLog.Log("[PASS] Test 5: looking a kind up and reading a creature as a fighter allocate nothing.");
        }

        private static Combatant Crawler(float attack, float intervalSeconds) =>
            new Combatant(new EntityId(1, 0, 0, 0), default, CrawlerRadius, CrawlerHealth, standing: true, attack, defense: 0f, CombatRules.UnarmedReach, CombatRules.SecondsToTicks(intervalSeconds));

        /// <summary>The rule's expectation: seconds for <paramref name="crawler"/> to fell one of <paramref name="kind"/>, whole.</summary>
        private static float SecondsToFell(in Combatant crawler, CreatureDefinition kind)
        {
            float perHit = CombatRules.Mitigated(crawler.Attack, kind.Defense);
            float perSecond = CombatRules.HitChance(crawler) * perHit * CombatRules.TicksPerSecond / crawler.SwingInterval;
            return kind.MaxHealth / perSecond;
        }

        /// <summary>The rule's expectation: seconds for <paramref name="count"/> of <paramref name="kind"/> to down a bare, unarmoured crawler.</summary>
        private static float SecondsToDown(CreatureDefinition kind, int count)
        {
            var creature = Combatant.ForCreature(kind, new EntityId(2, 0, 0, 0), default, kind.MaxHealth);
            float perSecond = count * CombatRules.HitChance(creature) * CombatRules.Mitigated(kind.Attack, 0f) * CombatRules.TicksPerSecond / creature.SwingInterval;
            return CrawlerHealth / perSecond;
        }

        private static bool IsKebabCase(string key)
            => !string.IsNullOrEmpty(key) && key[0] != '-' && key[key.Length - 1] != '-' && !key.Contains("--") && key == key.ToLowerInvariant();

        /// <summary>
        /// Q13's bosses (Epic 10 Part 3, K10): rows with traits. The Matriarch is stronger than her brood, calls it at half
        /// health and returns after a game week; the Gatekeeper slams, rages under a quarter of its health, stands whole once
        /// left for a game hour and never returns. Neither flees, and each drops a sure trophy and a rare piece (Q12).
        /// </summary>
        private void ValidateTheBossesAreWhatQ13Says()
        {
            var rat = CreatureCatalog.Get(CreatureKind.BurrowRat);
            var matriarch = CreatureCatalog.Get(CreatureKind.Matriarch);
            var keeper = CreatureCatalog.Get(CreatureKind.Gatekeeper);
            Assert(matriarch != null && keeper != null && matriarch.IsBoss && keeper.IsBoss, "The Matriarch and the Gatekeeper are bosses.");
            var drops = new Simpiens.Simulation.Economy.ItemInstance[CreatureLoot.MaxDrops];
            foreach (var boss in new[] { matriarch, keeper })
            {
                Assert(!boss.Flees && boss.PackMin == 1 && boss.PackMax == 1, $"{boss.Name}: one of its kind, and it never flees.");
                Assert(CreatureLoot.Tables.TryGet(boss.LootTableId, out var loot) && loot.Every && loot.EntryCount == 2, $"{boss.Name}: every entry of its table drops.");
                var trophy = Simpiens.Simulation.Items.ItemCatalog.Get(loot.GetEntry(0).Item);
                var piece = Simpiens.Simulation.Items.ItemCatalog.Get(loot.GetEntry(1).Item);
                Assert(trophy != null && Simpiens.Simulation.Items.ItemCatalog.Is(trophy.Id, Simpiens.Simulation.Items.ItemCategory.Creature) && trophy.Equipment == null, $"{boss.Name}: a sure trophy ({trophy?.Name}).");
                Assert(piece != null && piece.Equipment != null && piece.Rarity >= Simpiens.Simulation.Items.ItemRarity.Rare, $"{boss.Name}: a sure rare piece ({piece?.Name}).");
                for (int seed = 1; seed <= 20; seed++)
                {
                    int n = Simpiens.Simulation.Items.LootRoller.Roll(CreatureLoot.Tables, boss.LootTableId, (ulong)seed, new EntityId(0, 0, 0, (uint)seed), seed * 7, drops);
                    Assert(n == 2 && drops[0].Type == trophy.Id && drops[1].Type == piece.Id, $"{boss.Name}: its trophy and its piece on every kill (seed {seed}: {n} drops).");
                }
            }

            // The Matriarch: a rat grown strong, who calls her brood at half health and returns after a week
            Assert(matriarch.MaxHealth > rat.MaxHealth * rat.PackMin && matriarch.Attack > rat.Attack, "The Matriarch outweighs a pack of her brood and bites harder.");
            Assert(matriarch.CallsBroodBelow == 0.5f && !matriarch.CallsBrood(matriarch.MaxHealth) && matriarch.CallsBrood(0.4f * matriarch.MaxHealth), "She calls her brood under half her health.");
            Assert(matriarch.RespawnHours == 7 * DayClock.HoursPerDay, "Slain, she returns after a game week.");
            Assert(!matriarch.Slam && !matriarch.HasFury && matriarch.WholeAfterHours == 0, "She has none of the Gatekeeper's traits.");

            // The Gatekeeper: a slam, fury under a quarter, whole once left a game hour, beaten once
            Assert(keeper.Slam && keeper.Reach >= 2f * CombatRules.UnarmedReach, "The Gatekeeper's slam reaches past a crawler's arm.");
            Assert(keeper.FuryBelow == 0.25f && !keeper.InFury(0.3f * keeper.MaxHealth) && keeper.InFury(0.2f * keeper.MaxHealth) && !keeper.InFury(0f), "It rages under a quarter of its health.");
            Assert(keeper.FuryAttack > 1f && keeper.FuryInterval < 1f, "Fury strikes harder and faster.");
            Assert(keeper.HealHours == 0 && keeper.WholeAfterHours == 1 && keeper.WholeAfterTicks == DayClock.Hours(1), "It heals nothing resting, but stands whole once its lair has been empty a game hour.");
            Assert(!keeper.Respawns, "Beaten once, it stays beaten.");
            foreach (var kind in CreatureCatalog.All) Assert(kind == keeper || kind.MaxHealth < keeper.MaxHealth, $"It is the strongest ({keeper.MaxHealth} against {kind.Name}'s {kind.MaxHealth}).");
            TestLog.Log($"[PASS] Test 6: the Matriarch ({matriarch.MaxHealth} health, calls her brood at half, back in a week) and the Gatekeeper ({keeper.MaxHealth} health, a slam, fury under a quarter, whole an hour after its lair is left, never back) are rows with traits; each drops a sure trophy and a rare piece.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[CreatureCatalogValidator ASSERTION FAILED] {message}");
        }
    }
}
