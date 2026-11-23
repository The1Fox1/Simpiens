using System;
using System.Threading.Tasks;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Testing.Combat
{
    /// <summary>
    /// The combat rule (Epic 10 Part 3, K0.3): the hit roll is seeded and the same on any thread; swings land at their chance,
    /// within its bounds; defense cuts a blow on a curve that never reaches zero; a crawler's read comes from its state and
    /// equipment; reach is measured between bodies and swings keep their interval; a weapon wears per landed hit; one rule
    /// serves a crawler and a creature alike; and none of it allocates. Nothing fights in the live game yet (K2 and K3), so
    /// this is the rule's whole evidence until then. Runs as a test (ValidatorTests).
    /// </summary>
    public class CombatValidator
    {
        private const ulong Seed = 424242UL;
        private const float CrawlerRadius = 0.35f;

        // Test-only items, added to the catalog for the tests that wear them (as ItemSystemProofTests adds its own)
        private static readonly ItemType Cudgel = (ItemType)210;
        private static readonly ItemType Brigandine = (ItemType)211;

        public void RunAllValidationTests()
        {
            TestLog.Log("[CombatValidator] Beginning combat rule validation...");

            ValidateTheRollIsSeeded();
            ValidateHitsLandAtTheirChance();
            ValidateTheChanceIsBounded();
            ValidateDefenseNeverStopsABlowWhole();
            ValidateACrawlersRead();
            ValidateReachAndTiming();
            ValidateAWeaponWearsPerHit();
            ValidateOneRuleOverTwoKindsOfFighter();
            ValidateNothingAllocates();
            ValidateAShotCarriesFallsOffAndWears();
            ValidateASlamDrawsOnceAFoeInIdOrder();
            ValidateFuryFollowsHealth();

            TestLog.Log("<color=green>[CombatValidator] ALL 12 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheRollIsSeeded()
        {
            var attacker = Fighter(1, new Vec2(0f, 0f));
            var target = Fighter(2, new Vec2(1f, 0f));
            var expected = new bool[2000];
            for (int t = 0; t < expected.Length; t++)
            {
                var a = CombatRules.Swing(Seed, t, attacker, target);
                var b = CombatRules.Swing(Seed, t, attacker, target);
                Assert(a.Hit == b.Hit && a.Damage == b.Damage, $"t{t}: the same seed, attacker and tick gave two outcomes.");
                expected[t] = a.Hit;
            }

            // On other threads, in any order: the same outcomes
            var parallel = new bool[expected.Length];
            Parallel.For(0, expected.Length, t => parallel[t] = CombatRules.Swing(Seed, t, attacker, target).Hit);
            for (int t = 0; t < expected.Length; t++) Assert(parallel[t] == expected[t], $"t{t}: a swing on another thread differs.");

            // Another attacker, seed or tick draws another stream; the target doesn't enter the roll
            int sameAsOtherAttacker = 0, sameAsOtherSeed = 0;
            var other = Fighter(3, new Vec2(0f, 1f));
            for (int t = 0; t < expected.Length; t++)
            {
                if (CombatRules.Swing(Seed, t, other, target).Hit == expected[t]) sameAsOtherAttacker++;
                if (CombatRules.Swing(Seed + 1, t, attacker, target).Hit == expected[t]) sameAsOtherSeed++;
                Assert(CombatRules.Swing(Seed, t, attacker, other).Hit == expected[t], $"t{t}: the target changed the roll.");
            }
            Assert(sameAsOtherAttacker < expected.Length && sameAsOtherSeed < expected.Length, "Other attackers and seeds roll their own streams.");

            // A stream drawn once a foe (a slam): the first draw is the single swing's
            var random = CombatRules.RandomFor(Seed, attacker.Id, 77);
            Assert(CombatRules.Swing(ref random, attacker, target).Hit == expected[77], "The first draw of a swing's stream is the swing's own roll.");
            TestLog.Log("[PASS] Test 1: a swing's roll is seeded by (seed, attacker, tick): the same on any thread, its own for each attacker and seed.");
        }

        private void ValidateHitsLandAtTheirChance()
        {
            var target = Fighter(2, new Vec2(1f, 0f));
            AssertHitShare(Fighter(1, default), target, CombatRules.BaseHitChance, "an unpractised fighter");
            AssertHitShare(Fighter(1, default, proficiency: 0.15f), target, 0.85f, "a practised fighter (+0.15)");
            AssertHitShare(Fighter(1, default, proficiency: -0.3f), target, 0.4f, "a clumsy fighter (-0.3)");
            TestLog.Log($"[PASS] Test 2: swings land at their chance, {CombatRules.BaseHitChance:P0} before practice, within 5 standard deviations over 100,000 swings.");
        }

        private void ValidateTheChanceIsBounded()
        {
            Assert(CombatRules.HitChance(Fighter(1, default, proficiency: -1f)) == CombatRules.MinHitChance, "However clumsy, a swing can land.");
            Assert(CombatRules.HitChance(Fighter(1, default, proficiency: 1f)) == CombatRules.MaxHitChance, "However practised, a swing can miss.");
            var target = Fighter(2, new Vec2(1f, 0f));
            AssertHitShare(Fighter(1, default, proficiency: -1f), target, CombatRules.MinHitChance, "the clumsiest");
            AssertHitShare(Fighter(1, default, proficiency: 1f), target, CombatRules.MaxHitChance, "the most practised");
            TestLog.Log($"[PASS] Test 3: the chance stays within {CombatRules.MinHitChance:P0} and {CombatRules.MaxHitChance:P0}.");
        }

        private void ValidateDefenseNeverStopsABlowWhole()
        {
            Assert(CombatRules.Mitigated(6f, 0f) == 6f, "No defense stops nothing.");
            Assert(CombatRules.Mitigated(6f, 10f) == 3f, "Defense 10 halves a blow.");
            Assert(CombatRules.Mitigated(8f, 30f) == 2f, "Defense 30 leaves a quarter.");
            Assert(CombatRules.Mitigated(6f, -5f) == 6f, "Negative defense counts as none.");
            Assert(CombatRules.Mitigated(0f, 0f) == 0f && CombatRules.Mitigated(-3f, 0f) == 0f, "No attack, no harm.");
            float previous = float.MaxValue;
            for (int d = 0; d <= 1000; d++)
            {
                float harm = CombatRules.Mitigated(2f, d);
                Assert(harm > 0f && harm < previous, $"Defense {d}: {harm} is above zero and below defense {d - 1}'s {previous}.");
                previous = harm;
            }

            // A landed swing does the mitigated harm; a miss does none
            var attacker = Fighter(1, default, attack: 6f);
            var armoured = Fighter(2, new Vec2(1f, 0f), defense: 10f);
            bool sawHit = false, sawMiss = false;
            for (int t = 0; t < 200; t++)
            {
                var outcome = CombatRules.Swing(Seed, t, attacker, armoured);
                if (outcome.Hit) { sawHit = true; Assert(outcome.Damage == 3f, $"t{t}: a hit through defense 10 did {outcome.Damage}, not 3."); }
                else { sawMiss = true; Assert(outcome.Damage == 0f, $"t{t}: a miss did harm."); }
            }
            Assert(sawHit && sawMiss, "Both hits and misses came up.");
            TestLog.Log("[PASS] Test 4: defense D stops D / (D + 10) of a blow, falling with D and never to zero; a miss does no harm.");
        }

        private void ValidateACrawlersRead()
        {
            var crawler = new AgentState(new EntityId(41, 0, 0, 0)) { Position = new Vec2(3f, 4f), Health = 80f };
            var bare = Combatant.ForCrawler(crawler, CrawlerRadius);
            Assert(bare.Id == crawler.AgentId && bare.Position == crawler.Position && bare.BodyRadius == CrawlerRadius && bare.Health == 80f && bare.Standing,
                $"Who and where: {bare}.");
            Assert(bare.Attack == CombatRules.UnarmedAttack && bare.Attack == 2f, $"Bare-handed, a crawler hits for 2 ({bare.Attack}).");
            Assert(bare.Defense == 0f && bare.Reach == CombatRules.UnarmedReach && bare.SwingInterval == 15, $"No defense, a bare hand's reach, a swing every 1.5 s ({bare}).");
            Assert(bare.Range == 0f && !bare.IsRanged && bare.Proficiency == 0f, "No ranged attack (K8) and no practice (K9) yet.");

            crawler.IsDowned = true;
            Assert(!Combatant.ForCrawler(crawler, CrawlerRadius).Standing, "A downed crawler isn't on its feet.");
            crawler.IsDowned = false;
            crawler.Health = 0f;
            Assert(!Combatant.ForCrawler(crawler, CrawlerRadius).Standing, "Nor is one at 0 health.");
            crawler.Health = 80f;

            using (TestItems())
            {
                Assert(crawler.Inventory.TryEquip(new ItemInstance(Cudgel, 1, 10, durability: 3)), "The test cudgel goes in the weapon slot.");
                Assert(crawler.Inventory.TryEquip(new ItemInstance(Brigandine, 1, 10, durability: 100)), "The test brigandine goes in the protection slot.");
                var armed = Combatant.ForCrawler(crawler, CrawlerRadius);
                Assert(armed.Attack == 6f, $"A weapon adds to the bare hand: 2 + 4 = {armed.Attack}.");
                Assert(armed.SwingInterval == 12, $"A quick weapon multiplies the interval: 1.5 s x 0.8 = {armed.SwingInterval} ticks.");
                Assert(SimMath.Approximately(armed.Reach, 0.75f), $"A long weapon adds reach: 0.5 + 0.25 = {armed.Reach}.");
                Assert(armed.Defense == 5f, $"Armour gives defense ({armed.Defense}).");

                // A grade strengthens the effect (Epic 10 Part 2, D56): a Fine cudgel's +4 is +4.4
                crawler.Inventory.Unequip(EquipmentSlot.Weapon);
                Assert(crawler.Inventory.TryEquip(new ItemInstance(Cudgel, 1, 10, quality: Grades.Fine, durability: 3)), "A Fine cudgel.");
                Assert(SimMath.Approximately(Combatant.ForCrawler(crawler, CrawlerRadius).Attack, 6.4f), "A Fine weapon's attack is a tenth stronger.");
            }
            TestLog.Log("[PASS] Test 5: a crawler's read: 2 bare-handed, a swing every 15 ticks, reach 0.5; a weapon and armour add to it through CrawlerStats; down means not standing.");
        }

        private void ValidateReachAndTiming()
        {
            // Two crawlers' bodies (0.35 each) with a bare hand's reach (0.5): within 1.2 of each other's centres
            var a = Fighter(1, new Vec2(0f, 0f));
            Assert(CombatRules.InReach(a, Fighter(2, new Vec2(1.2f, 0f))), "At the edge of reach (gap 0.5): in reach.");
            Assert(!CombatRules.InReach(a, Fighter(2, new Vec2(1.25f, 0f))), "Just past it (gap 0.55): out of reach.");
            Assert(CombatRules.InReach(a, Fighter(2, new Vec2(0.7f, 0.7f))), "Diagonal neighbours (gap 0.29): in reach.");
            Assert(SimMath.Approximately(CombatRules.Gap(a, Fighter(2, new Vec2(3f, 4f))), 4.3f), "The gap is the distance less both radii.");
            Assert(CombatRules.Gap(a, Fighter(2, new Vec2(0.1f, 0f))) == 0f, "Overlapping bodies have no gap.");
            var big = new Combatant(new EntityId(9, 0, 0, 0), new Vec2(2f, 0f), bodyRadius: 1.2f, health: 100f, standing: true, attack: 5f, defense: 0f, reach: 0.5f, swingInterval: 20);
            Assert(CombatRules.InReach(a, big) && CombatRules.InReach(big, a), "A big body is reached sooner: reach is measured between bodies.");

            // Can strike: both standing, a foe, in reach, the swing due
            var b = Fighter(2, new Vec2(1f, 0f));
            Assert(CombatRules.CanStrike(a, b, 100, 100) && !CombatRules.CanStrike(a, b, 99, 100), "The swing waits until it is due.");
            Assert(!CombatRules.CanStrike(a, a, 100, 0), "Nobody strikes itself.");
            Assert(!CombatRules.CanStrike(a, Fighter(2, new Vec2(1f, 0f), standing: false), 100, 0), "Nobody strikes a foe that is down.");
            Assert(!CombatRules.CanStrike(Fighter(1, default, standing: false), b, 100, 0), "A downed fighter doesn't swing.");
            Assert(!CombatRules.CanStrike(a, Fighter(2, new Vec2(5f, 0f)), 100, 0), "Nobody strikes out of reach.");

            // A swing every interval: 15 ticks bare-handed, 4 swings in 60 ticks
            int swings = 0;
            long readyAt = 0;
            for (long tick = 0; tick < 60; tick++)
            {
                if (!CombatRules.CanStrike(a, b, tick, readyAt)) continue;
                swings++;
                readyAt = CombatRules.NextSwingAt(tick, a);
            }
            Assert(swings == 4, $"A swing every 15 ticks: {swings} in 60, not 4.");
            Assert(CombatRules.SecondsToTicks(1.5f) == 15 && CombatRules.SecondsToTicks(1.2f) == 12 && CombatRules.SecondsToTicks(0.01f) == 1, "Seconds as whole ticks, at least one.");
            Assert(SimMath.Approximately(CombatRules.TicksPerSecond * SimulationClock.FixedTickDeltaTime, 1f), "The rule's ticks a second are the clock's.");
            TestLog.Log("[PASS] Test 6: reach spans the gap between bodies; a fighter strikes only a standing foe in reach, once each interval.");
        }

        private void ValidateAWeaponWearsPerHit()
        {
            using (TestItems())
            {
                var inventory = new AgentInventory();
                Assert(inventory.TryEquip(new ItemInstance(Cudgel, 1, 10, durability: 3)), "The cudgel, 3 hits from breaking.");
                var hit = new SwingOutcome(true, 0.7f, 4f);
                var miss = new SwingOutcome(false, 0.7f, 0f);
                Assert(!CombatRules.WearOnHit(miss, inventory) && inventory.GetEquipped(EquipmentSlot.Weapon).Durability == 3, "A miss wears nothing.");
                Assert(!CombatRules.WearOnHit(hit, inventory) && inventory.GetEquipped(EquipmentSlot.Weapon).Durability == 2, "A hit wears one unit.");
                Assert(!CombatRules.WearOnHit(hit, inventory) && inventory.GetEquipped(EquipmentSlot.Weapon).Durability == 1, "Another hit, another unit.");
                Assert(CombatRules.WearOnHit(hit, inventory) && inventory.GetEquipped(EquipmentSlot.Weapon).IsEmpty, "The last unit breaks it, and it's gone.");
                Assert(!CombatRules.WearOnHit(hit, inventory), "A bare hand wears nothing.");
                Assert(!CombatRules.WearOnHit(hit, null), "Nor does a fighter with no inventory (a creature).");

                // Worn gear that isn't a weapon is untouched: a pickaxe in the tool slot, armour in the body slot
                Assert(inventory.TryEquip(new ItemInstance(ItemType.ToolPickaxe, 1, 30, durability: 40)), "A pickaxe in the tool slot.");
                Assert(inventory.TryEquip(new ItemInstance(Brigandine, 1, 10, durability: 100)), "The brigandine in the body slot.");
                CombatRules.WearOnHit(hit, inventory);
                Assert(inventory.GetEquipped(EquipmentSlot.Tool).Durability == 40 && inventory.GetEquipped(EquipmentSlot.Body).Durability == 100, "A hit wears only the weapon.");
            }
            TestLog.Log("[PASS] Test 7: a weapon that wears per hit loses a unit a landed hit and breaks at 0; misses, bare hands and other gear wear nothing.");
        }

        private void ValidateOneRuleOverTwoKindsOfFighter()
        {
            // A crawler with the test cudgel against a creature written by hand, as K0.4's catalogue will write one: tougher,
            // armoured, slower. They trade blows as the pass will (K2.2): every swing of a tick is decided from the tick's
            // start, then all harm is applied, so the order of resolution changes nothing
            using (TestItems())
            {
                int crawlerWins = 0, creatureWins = 0, both = 0;
                long firstEnd = -1;
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var (end, crawlerHealth, creatureHealth, blows) = Duel(seed);
                    var again = Duel(seed);
                    Assert(again.end == end && again.crawlerHealth == crawlerHealth && again.creatureHealth == creatureHealth && again.blows == blows, $"Seed {seed}: the same duel twice ended differently.");
                    var reversed = Duel(seed, crawlerFirst: false);
                    Assert(reversed.end == end && reversed.crawlerHealth == crawlerHealth && reversed.creatureHealth == creatureHealth, $"Seed {seed}: resolving the creature first changed the duel.");
                    if (crawlerHealth <= 0f && creatureHealth <= 0f) both++;
                    else if (creatureHealth <= 0f) crawlerWins++;
                    else creatureWins++;
                    if (seed == 1) firstEnd = end;
                }
                Assert(crawlerWins > 0 && creatureWins > 0, $"Over 40 seeds both sides win sometimes (crawler {crawlerWins}, creature {creatureWins}, both down {both}).");
                TestLog.Log($"[PASS] Test 8: one rule over a crawler and a creature: each duel is the same on a replay and in either order of resolution; over 40 seeds the crawler wins {crawlerWins}, the creature {creatureWins}, both fall {both} (seed 1 ends at t{firstEnd}).");
            }
        }

        private (long end, float crawlerHealth, float creatureHealth, int blows) Duel(ulong seed, bool crawlerFirst = true)
        {
            var crawler = new AgentState(new EntityId(5, 0, 0, 0)) { Position = new Vec2(0f, 0f), Health = 100f };
            crawler.Inventory.TryEquip(new ItemInstance(Cudgel, 1, 10, durability: 1000));
            var creatureId = new EntityId(6, 0, 0, 0);
            var creaturePosition = new Vec2(1f, 0f);
            float creatureHealth = 60f;
            long crawlerReady = 0, creatureReady = 0;
            int blows = 0;
            for (long tick = 0; tick < 6000; tick++)
            {
                var c = Combatant.ForCrawler(crawler, CrawlerRadius);
                var beast = new Combatant(creatureId, creaturePosition, bodyRadius: 0.4f, creatureHealth, standing: creatureHealth > 0f,
                    attack: 10f, defense: 4f, reach: 0.4f, swingInterval: CombatRules.SecondsToTicks(2f));
                // Decide every swing from the tick's start
                float toCreature = 0f, toCrawler = 0f;
                for (int turn = 0; turn < 2; turn++)
                {
                    bool crawlersTurn = (turn == 0) == crawlerFirst;
                    if (crawlersTurn && CombatRules.CanStrike(c, beast, tick, crawlerReady))
                    {
                        var outcome = CombatRules.Swing(seed, tick, c, beast);
                        toCreature += outcome.Damage;
                        crawlerReady = CombatRules.NextSwingAt(tick, c);
                        CombatRules.WearOnHit(outcome, crawler.Inventory);
                        blows++;
                    }
                    else if (!crawlersTurn && CombatRules.CanStrike(beast, c, tick, creatureReady))
                    {
                        toCrawler += CombatRules.Swing(seed, tick, beast, c).Damage;
                        creatureReady = CombatRules.NextSwingAt(tick, beast);
                        blows++;
                    }
                }
                // Then apply all harm
                creatureHealth = SimMath.Max(0f, (float)(creatureHealth - toCreature));
                crawler.Health = SimMath.Max(0f, (float)(crawler.Health - toCrawler));
                if (crawler.Health <= 0f) crawler.IsDowned = true;
                if (crawler.IsDowned || creatureHealth <= 0f) return (tick, crawler.Health, creatureHealth, blows);
            }
            return (-1, crawler.Health, creatureHealth, blows);
        }

        private void ValidateNothingAllocates()
        {
            var crawler = new AgentState(new EntityId(7, 0, 0, 0)) { Position = new Vec2(0f, 0f), Health = 100f };
            var target = Fighter(2, new Vec2(1f, 0f), defense: 3f);
            var inventory = new AgentInventory();
            // Warm up
            CombatRules.Swing(Seed, 0, Combatant.ForCrawler(crawler, CrawlerRadius), target);
            CombatRules.WearOnHit(new SwingOutcome(true, 0.7f, 2f), inventory);
            long before = GC.GetAllocatedBytesForCurrentThread();
            float harm = 0f;
            long readyAt = 0;
            for (long tick = 0; tick < 10000; tick++)
            {
                var attacker = Combatant.ForCrawler(crawler, CrawlerRadius);
                if (!CombatRules.CanStrike(attacker, target, tick, readyAt)) continue;
                var outcome = CombatRules.Swing(Seed, tick, attacker, target);
                harm += outcome.Damage;
                CombatRules.WearOnHit(outcome, inventory);
                readyAt = CombatRules.NextSwingAt(tick, attacker);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0 && harm > 0f, $"10,000 ticks of swings allocated {allocated} bytes (harm {harm}).");
            TestLog.Log("[PASS] Test 9: reading a fighter, striking, rolling and wearing allocate nothing.");
        }

        private static IDisposable TestItems() => ItemCatalog.ExtendForTest(
            // Attack +4, a fifth quicker, a quarter tile longer; three hits from breaking when made with durability 3
            new ItemDefinition(Cudgel, "TestCudgel", "Test Cudgel", ItemCategory.Equipment | ItemCategory.Weapon, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Weapon, durability: 3, WearRule.PerHit,
                    new StatModifier(StatKind.Attack, ModifierOp.Add, 4f),
                    new StatModifier(StatKind.AttackInterval, ModifierOp.Multiply, 0.8f),
                    new StatModifier(StatKind.Reach, ModifierOp.Add, 0.25f))),
            new ItemDefinition(Brigandine, "TestBrigandine", "Test Brigandine", ItemCategory.Equipment | ItemCategory.Protection, ItemRarity.Common,
                equipment: new EquipmentBlock(SlotKind.Body, durability: 100, WearRule.None,
                    new StatModifier(StatKind.Defense, ModifierOp.Add, 5f))));

        // A shot (Epic 10 Part 3, K8.1, D45): a ranged fighter strikes within its range across the gap between bodies; its chance
        // holds to half its range and falls evenly to half of it at full range; a melee fighter's chance is untouched; a ranged
        // weapon wears per shot, hit or miss, and a blade doesn't
        private void ValidateAShotCarriesFallsOffAndWears()
        {
            var archer = new Combatant(new EntityId(1, 0, 0, 0), Vec2.Zero, CrawlerRadius, 100f, true, 9f, 0f, CombatRules.UnarmedReach, 24, range: 7f);
            float span = CrawlerRadius * 2f; // the two bodies
            Combatant At(float gap) => Fighter(2, new Vec2(gap + span, 0f));
            Assert(archer.IsRanged && CombatRules.InRange(archer, At(7f)) && !CombatRules.InRange(archer, At(7.1f)), "In range up to 7 across the gap, not past it.");
            Assert(CombatRules.CanStrike(archer, At(5f), 0, 0) && !CombatRules.InReach(archer, At(5f)), "A shot at 5: out of reach, in range.");
            var brawler = Fighter(3, Vec2.Zero);
            Assert(!CombatRules.CanStrike(brawler, At(5f), 0, 0) && !CombatRules.InRange(brawler, At(0.1f)), "A fighter with no range strikes only in reach.");
            float baseChance = CombatRules.HitChance(archer);
            Assert(CombatRules.ShotChance(archer, At(3f)) == baseChance && CombatRules.ShotChance(archer, At(3.5f)) == baseChance, "Up to half its range, the base chance.");
            Assert(Math.Abs(CombatRules.ShotChance(archer, At(7f)) - baseChance * CombatRules.RangeFalloff) < 1e-5f, "At full range, half of it.");
            Assert(Math.Abs(CombatRules.ShotChance(archer, At(5.25f)) - baseChance * 0.75f) < 1e-5f, "Midway, three quarters.");
            Assert(CombatRules.ShotChance(brawler, At(5f)) == CombatRules.HitChance(brawler), "A melee fighter's chance doesn't fall with distance.");
            AssertHitShare(archer, At(7f), baseChance * CombatRules.RangeFalloff, "a shot at full range");

            var inventory = new AgentInventory();
            Assert(inventory.TryEquip(Outfitting.NewPiece(ItemType.Crossbow, Grades.Standard)), "A crossbow in the weapon slot.");
            int life = inventory.GetEquipped(EquipmentSlot.Weapon).Durability;
            Assert(!CombatRules.WearOnShot(inventory) && inventory.GetEquipped(EquipmentSlot.Weapon).Durability == life - 1, "A shot wears a unit, hit or miss.");
            Assert(!CombatRules.WearOnHit(new SwingOutcome(true, 0.7f, 5f), inventory) && inventory.GetEquipped(EquipmentSlot.Weapon).Durability == life - 1, "A hit wears it no more.");
            Assert(CrawlerStats.Get(inventory, StatKind.Range) == 7f && CrawlerStats.Get(new AgentInventory(), StatKind.Range) == 0f, "Its range is the wearer's; a bare hand has none.");
            var blade = new AgentInventory();
            Assert(blade.TryEquip(Outfitting.NewPiece(ItemType.IronBlade, Grades.Standard)), "A blade.");
            int edge = blade.GetEquipped(EquipmentSlot.Weapon).Durability;
            Assert(!CombatRules.WearOnShot(blade) && blade.GetEquipped(EquipmentSlot.Weapon).Durability == edge, "A blade doesn't wear per shot.");
            TestLog.Log("[PASS] Test 10: a shot carries to its range, its chance holds to half of it and halves at full range; a ranged weapon wears per shot.");
        }

        /// <summary>
        /// The slam (Epic 10 Part 3, K10.3a; D3): one swing over several foes draws once a foe, in the order given, from the
        /// attacker's stream at its tick, so its first foe's outcome is a lone swing's; the same foes give the same outcomes on
        /// any thread; each hit is the attack cut by that foe's own defense; and it allocates nothing.
        /// </summary>
        private void ValidateASlamDrawsOnceAFoeInIdOrder()
        {
            var keeper = Combatant.ForCreature(Simpiens.Simulation.Creatures.CreatureCatalog.Get(Simpiens.Simulation.Creatures.CreatureKind.Gatekeeper), new EntityId(77, 0, 0, 0), Vec2.Zero, 400f);
            var foes = new[] { Fighter(1, new Vec2(1.2f, 0f)), Fighter(2, new Vec2(-1.2f, 0f), defense: 6f), Fighter(3, new Vec2(0f, 1.2f)) };
            var outcomes = new SwingOutcome[foes.Length];
            int hits = 0, lone = 0, struck = 0;
            for (long tick = 1; tick <= 2000; tick++)
            {
                Assert(CombatRules.Slam(Seed, tick, keeper, foes, outcomes) == foes.Length, "One outcome a foe.");
                var random = CombatRules.RandomFor(Seed, keeper.Id, tick);
                for (int i = 0; i < foes.Length; i++)
                {
                    var expected = CombatRules.Swing(ref random, keeper, foes[i]);
                    Assert(outcomes[i].Hit == expected.Hit && outcomes[i].Damage == expected.Damage, $"t{tick}: foe {i}'s roll is the stream's draw {i}.");
                    if (outcomes[i].Hit)
                    {
                        hits++;
                        Assert(outcomes[i].Damage == CombatRules.Mitigated(keeper.Attack, foes[i].Defense), "A hit is the attack cut by that foe's own defense.");
                    }
                }
                if (outcomes[0].Hit == CombatRules.Swing(Seed, tick, keeper, foes[0]).Hit) lone++;
                if (outcomes[0].Hit && outcomes[1].Hit && outcomes[2].Hit) struck++;
            }
            Assert(lone == 2000, "Its first foe's outcome is a lone swing's.");
            float rate = hits / (3f * 2000f);
            Assert(SimMath.Abs(rate - CombatRules.HitChance(keeper)) < 0.02f, $"Each foe is hit at the chance ({rate:P1}).");
            var again = new SwingOutcome[foes.Length];
            CombatRules.Slam(Seed, 1234, keeper, foes, outcomes);
            System.Threading.Tasks.Task.Run(() => CombatRules.Slam(Seed, 1234, keeper, foes, again)).Wait();
            for (int i = 0; i < foes.Length; i++) Assert(outcomes[i].Hit == again[i].Hit, "The same slam on another thread.");
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (long tick = 1; tick <= 1000; tick++) CombatRules.Slam(Seed, tick, keeper, foes, outcomes);
            Assert(System.GC.GetAllocatedBytesForCurrentThread() - before == 0, "A slam allocates nothing.");
            TestLog.Log($"[PASS] Test 11: a slam draws once a foe in the order given from its swing's stream (all three struck on {struck} of 2,000 swings), each hit cut by that foe's defense, the same on any thread.");
        }

        /// <summary>
        /// Fury (Epic 10 Part 3, K10.3b): below its line a kind's attack and swing take their factors, and above it, or for a kind
        /// without fury, the read is its kind's; it follows health, so a fighter that heals leaves its fury.
        /// </summary>
        private void ValidateFuryFollowsHealth()
        {
            var kind = Simpiens.Simulation.Creatures.CreatureCatalog.Get(Simpiens.Simulation.Creatures.CreatureKind.Gatekeeper);
            var calm = Combatant.ForCreature(kind, default, default, 0.3f * kind.MaxHealth);
            var raging = Combatant.ForCreature(kind, default, default, 0.2f * kind.MaxHealth);
            Assert(calm.Attack == kind.Attack && calm.SwingInterval == kind.SwingIntervalTicks, "Above its line, its kind's attack and swing.");
            Assert(raging.Attack == kind.Attack * kind.FuryAttack && raging.SwingInterval == CombatRules.SecondsToTicks(kind.SwingIntervalSeconds * kind.FuryInterval) && raging.SwingInterval < calm.SwingInterval,
                $"Below it, harder ({raging.Attack}) and faster (every {raging.SwingInterval} ticks against {calm.SwingInterval}).");
            Assert(raging.Defense == calm.Defense && raging.Reach == calm.Reach && raging.Standing, "Fury changes the blow, not the body.");
            Assert(!Combatant.ForCreature(kind, default, default, 0f).Standing, "At no health it is down, not raging.");
            var beetle = Simpiens.Simulation.Creatures.CreatureCatalog.Get(Simpiens.Simulation.Creatures.CreatureKind.StoneBeetle);
            Assert(Combatant.ForCreature(beetle, default, default, 1f).Attack == beetle.Attack, "A kind without fury never rages.");
            TestLog.Log($"[PASS] Test 12: fury follows health: under a quarter the Gatekeeper strikes for {raging.Attack} every {raging.SwingInterval} ticks, above it {calm.Attack} every {calm.SwingInterval}.");
        }

        private static Combatant Fighter(uint id, Vec2 position, float attack = CombatRules.UnarmedAttack, float defense = 0f, float proficiency = 0f, bool standing = true) =>
            new Combatant(new EntityId(id, 0, 0, 0), position, CrawlerRadius, health: 100f, standing, attack, defense, CombatRules.UnarmedReach,
                CombatRules.SecondsToTicks(CombatRules.UnarmedIntervalSeconds), range: 0f, proficiency);

        private static void AssertHitShare(in Combatant attacker, in Combatant target, float expected, string who)
        {
            const int Swings = 100000;
            int hits = 0;
            for (long t = 0; t < Swings; t++)
            {
                if (CombatRules.Swing(Seed, t, attacker, target).Hit) hits++;
            }
            double sigma = Math.Sqrt(expected * (1 - expected) / Swings);
            double share = (double)hits / Swings;
            Assert(Math.Abs(share - expected) <= 5 * sigma, $"{who}: {share:P2} of swings landed, expected {expected:P2} (5 sigma = {5 * sigma:P2}).");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[CombatValidator ASSERTION FAILED] {message}");
        }
    }
}
