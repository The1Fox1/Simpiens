using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// What techniques do (Epic 11, S4.3–S4.5, A4, A5, D14), a test per kind of effect, each through the one door
    /// (<see cref="CrawlerTechniques"/>). The fight's: a stance's defense and harm, a quick blade's swing, a long shot's
    /// range, a steady aim's hit, a last stand below half health; scoped to the fight its holder fights; stacked within their
    /// bounds; untouched with none held; and cognition judging a lair by the same fighter. Runs as a test (ValidatorTests).
    /// </summary>
    public class TechniqueEffectsValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[TechniqueEffectsValidator] Beginning technique effects validation...");
            ValidateAStance();
            ValidateTheRhythm();
            ValidateTheShot();
            ValidateALastStand();
            ValidateTheScopeAndTheStack();
            TestLog.Log("<color=green>[TechniqueEffectsValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static readonly SkillSet Fighter = Skilled(Skill.Melee, 4, Skill.Ranged, 4);

        private static void ValidateAStance()
        {
            var bare = new AgentInventory();
            var plain = Fight(bare, default);
            var wall = Fight(bare, Holding(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall));
            var fury = Fight(bare, Holding(Skill.Melee, SkillBand.Apprentice, Technique.RecklessFury));
            Assert(wall.Defense == plain.Defense + 3f, $"Shield Wall: +3 defense ({plain.Defense} -> {wall.Defense}).");
            Assert(Near(wall.Attack, plain.Attack * 0.85f), $"Shield Wall: 15% less harm ({plain.Attack:0.###} -> {wall.Attack:0.###}).");
            Assert(CombatRules.Mitigated(6f, wall.Defense) < CombatRules.Mitigated(6f, plain.Defense), "A creature's blow takes less from Shield Wall's holder.");
            Assert(Near(fury.Attack, plain.Attack * 1.25f), $"Reckless Fury: 25% more harm ({fury.Attack:0.###}).");
            Assert(fury.Defense == 0f && plain.Defense == 0f, "Reckless Fury's -2 defense leaves the unarmoured at none, never below.");
            Assert(wall.SwingInterval == plain.SwingInterval && wall.Proficiency == plain.Proficiency && wall.Reach == plain.Reach, "A stance changes nothing else.");
            TestLog.Log($"[PASS] Test 1: Shield Wall takes +3 defense for 15% of its harm; Reckless Fury the other way, its defense never below none.");
        }

        private static void ValidateTheRhythm()
        {
            var bare = new AgentInventory();
            var plain = Fight(bare, default);
            var quick = Fight(bare, Holding(Skill.Melee, SkillBand.Journeyman, Technique.QuickBlade));
            var heavy = Fight(bare, Holding(Skill.Melee, SkillBand.Journeyman, Technique.HeavyBlows));
            Assert(quick.SwingInterval < plain.SwingInterval && Near(quick.Attack, plain.Attack * 0.9f), $"Quick Blade swings sooner ({plain.SwingInterval} -> {quick.SwingInterval} ticks) for 10% of its harm.");
            Assert(heavy.SwingInterval > plain.SwingInterval && Near(heavy.Attack, plain.Attack * 1.2f), $"Heavy Blows swings later ({heavy.SwingInterval} ticks) for 20% more harm.");
            Assert(quick.SwingInterval == CombatRules.SecondsToTicks((float)(CombatRules.UnarmedIntervalSeconds / 1.2f)), "Pace 20% shortens the interval to its fifth less.");
            TestLog.Log($"[PASS] Test 2: Quick Blade swings every {quick.SwingInterval} ticks for less, Heavy Blows every {heavy.SwingInterval} for more ({plain.SwingInterval} without).");
        }

        private static void ValidateTheShot()
        {
            var sling = Sling();
            var plain = Fight(sling, default);
            var aim = Fight(sling, Holding(Skill.Ranged, SkillBand.Apprentice, Technique.SteadyAim));
            var draw = Fight(sling, Holding(Skill.Ranged, SkillBand.Apprentice, Technique.QuickDraw));
            var longShot = Fight(sling, Holding(Skill.Ranged, SkillBand.Journeyman, Technique.LongShot));
            var blank = Fight(sling, Holding(Skill.Ranged, SkillBand.Journeyman, Technique.PointBlank));
            Assert(plain.IsRanged, "A sling shoots.");
            Assert(Near(aim.Proficiency, plain.Proficiency + 0.08f) && aim.SwingInterval > plain.SwingInterval, $"Steady Aim: +8 to hit ({CombatRules.HitChance(plain):P0} -> {CombatRules.HitChance(aim):P0}), shooting slower.");
            Assert(Near(draw.Proficiency, plain.Proficiency - 0.05f) && draw.SwingInterval < plain.SwingInterval, "Quick Draw: shoots sooner, -5 to hit.");
            Assert(Near(longShot.Range, plain.Range * 1.25f) && Near(longShot.Attack, plain.Attack * 0.9f), $"Long Shot: 25% more range ({plain.Range} -> {longShot.Range:0.##}) for 10% of its harm.");
            Assert(Near(blank.Range, plain.Range * 0.8f) && Near(blank.Attack, plain.Attack * 1.2f), "Point Blank: 20% more harm for a fifth of its range.");
            var bare = Fight(new AgentInventory(), Holding(Skill.Ranged, SkillBand.Journeyman, Technique.LongShot));
            Assert(bare.Range == 0f && !bare.IsRanged, "Long Shot gives no range to a hand with nothing to shoot.");
            TestLog.Log($"[PASS] Test 3: Steady Aim and Quick Draw trade the hit for the pace; Long Shot reaches {longShot.Range:0.#} where the sling reaches {plain.Range:0.#}.");
        }

        private static void ValidateALastStand()
        {
            var bare = new AgentInventory();
            var stand = Holding(Skill.Melee, SkillBand.Master, Technique.LastStand);
            var whole = Fight(bare, stand, health: 60f);
            var hurt = Fight(bare, stand, health: 40f);
            var hurtPlain = Fight(bare, default, health: 40f);
            Assert(Near(whole.Attack, Fight(bare, default, health: 60f).Attack), "Above half health a last stand adds nothing.");
            Assert(Near(hurt.Attack, hurtPlain.Attack * 1.4f), $"Below half, 40% more harm ({hurtPlain.Attack:0.###} -> {hurt.Attack:0.###}).");
            TestLog.Log("[PASS] Test 4: a last stand strikes 40% harder below half health, and no harder above it.");
        }

        private static void ValidateTheScopeAndTheStack()
        {
            // Scoped: a melee stance counts while its holder fights in reach, not while it shoots
            var wall = Holding(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            var sling = Sling();
            Assert(Fight(sling, wall).Defense == Fight(sling, default).Defense && Near(Fight(sling, wall).Attack, Fight(sling, default).Attack), "Shield Wall counts nothing while its holder shoots.");

            // Stacked: three bands of melee, summed and held within bounds
            var traits = Holding(Skill.Melee, SkillBand.Apprentice, Technique.RecklessFury);
            traits.SetTechnique(Skill.Melee, SkillBand.Journeyman, Technique.HeavyBlows);
            traits.SetTechnique(Skill.Melee, SkillBand.Master, Technique.LastStand);
            Assert(CrawlerTechniques.Get(traits, Fighter, SkillWork.Fight(ranged: false), TechniqueStat.Attack) == 45, "Reckless Fury and Heavy Blows: 45% more harm.");
            Assert(CrawlerTechniques.Get(traits, Fighter, SkillWork.Fight(ranged: false), TechniqueStat.Defense) == -4, "Two costs of -2 defense sum.");
            Assert(CrawlerTechniques.Get(traits, Fighter, SkillWork.Fight(ranged: true), TechniqueStat.Attack) == 0, "None of it while shooting.");
            Assert(CrawlerTechniques.Get(traits, Fighter, SkillWork.Harvest(ItemType.IronOre), TechniqueStat.Pace) == 0, "Nor at the seam.");
            Assert(CrawlerTechniques.Get(traits, Fighter, SkillWork.Fight(ranged: false), TechniqueStat.None) == 0, "No stat is nothing.");
            // A slot holding another skill's technique (no pick does it) counts nothing in either
            var crossed = default(CrawlerTraits);
            crossed.SetTechnique(Skill.Mining, SkillBand.Apprentice, Technique.RecklessFury);
            Assert(CrawlerTechniques.Get(crossed, Fighter, SkillWork.Fight(ranged: false), TechniqueStat.Attack) == 0
                && CrawlerTechniques.Get(crossed, Fighter, SkillWork.Harvest(ItemType.IronOre), TechniqueStat.Attack) == 0, "A technique counts only in its own skill.");

            // Untouched with none held, and cognition judges a lair by the same fighter
            var bare = new AgentInventory();
            var none = Fight(bare, default);
            var before = new Combatant(default, Vec2.Zero, 0.4f, Vitality.MaxHealth, true,
                (float)(CombatRules.UnarmedAttack * SkillRules.DamageFactor(4)), 0f, CombatRules.UnarmedReach,
                CombatRules.SecondsToTicks(CombatRules.UnarmedIntervalSeconds), 0f, SkillRules.HitBonus(4));
            Assert(none.Attack == before.Attack && none.Defense == before.Defense && none.SwingInterval == before.SwingInterval && none.Proficiency == before.Proficiency,
                "With no technique held, the fighter is the one K9 built.");
            var blade = new AgentInventory();
            blade.TryEquip(new ItemInstance(1, ItemType.IronBlade, 1, 12, durability: 100));
            var nest = Simpiens.Core.Session.Layouts.Floor1Layout.Definition.Lairs[0];
            float furyHarm = Vitality.HarmToClear(nest, true, Fighter, blade, Holding(Skill.Melee, SkillBand.Apprentice, Technique.RecklessFury));
            float plainHarm = Vitality.HarmToClear(nest, true, Fighter, blade, default);
            Assert(furyHarm < plainHarm, $"A fighter in Reckless Fury expects to clear a lair for less harm, killing faster ({plainHarm:0} -> {furyHarm:0}).");
            TestLog.Log("[PASS] Test 5: a technique counts in its own skill's fight only; stacked within bounds; untouched with none held; cognition judges a lair by the same fighter.");
        }

        private static Combatant Fight(IAgentInventory worn, in CrawlerTraits traits, float health = Vitality.MaxHealth) =>
            Combatant.ForCrawler(default, Vec2.Zero, 0.4f, health, standing: true, Fighter, worn, traits);

        private static CrawlerTraits Holding(Skill skill, SkillBand band, Technique technique)
        {
            var traits = default(CrawlerTraits);
            traits.SetTechnique(skill, band, technique);
            return traits;
        }

        private static SkillSet Skilled(Skill a, int levelA, Skill b, int levelB)
        {
            var skills = default(SkillSet);
            skills.Set(a, levelA);
            skills.Set(b, levelB);
            return skills;
        }

        private static AgentInventory Sling()
        {
            var inventory = new AgentInventory();
            inventory.TryEquip(new ItemInstance(1, ItemType.Sling, 1, 12, durability: 100));
            return inventory;
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0005f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[TechniqueEffectsValidator] FAILED: " + message);
        }
    }
}
