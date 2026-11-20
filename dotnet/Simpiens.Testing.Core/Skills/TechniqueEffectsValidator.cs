using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation;
using Simpiens.Narrative;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// What techniques do (Epic 11, S4.3–S4.5, A4, A5, D14), a test per kind of effect, each through the one door
    /// (<see cref="CrawlerTechniques"/>). The fight's: a stance's defense and harm, a quick blade's swing, a long shot's
    /// range, a steady aim's hit, a last stand below half health; scoped to the fight its holder fights; stacked within their
    /// bounds; untouched with none held; and cognition judging a lair by the same fighter. The work's: the harvest's pace,
    /// read by the salvage choice too; a make's grade and pace; the carry's pace and a medicine's healing; practice beside the
    /// passion; a mentor's lesson. The rule flag: unyielding, the cautious and armed standing when struck. A calling's capstone
    /// at Master (S5.5, D23). Runs as a test (ValidatorTests).
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
            ValidateTheHarvest();
            ValidateTheMake();
            ValidateTheFallen();
            ValidatePracticeAndLessons();
            ValidateUnyielding();
            ValidateCapstones();
            TestLog.Log("<color=green>[TechniqueEffectsValidator] ALL 11 TESTS PASSED SUCCESSFULLY!</color>");
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

        private static void ValidateTheHarvest()
        {
            var bare = new AgentInventory();
            var miner = Skilled(Skill.Mining, 4, Skill.Foraging, 4);
            float plain = Simpiens.Cognition.Planning.Handlers.HarvestResourceIntentHandler.SkillSpeed(miner, bare, default, ItemType.IronOre);
            float driver = Simpiens.Cognition.Planning.Handlers.HarvestResourceIntentHandler.SkillSpeed(miner, bare, Holding(Skill.Mining, SkillBand.Apprentice, Technique.HardDriver), ItemType.IronOre);
            float student = Simpiens.Cognition.Planning.Handlers.HarvestResourceIntentHandler.SkillSpeed(miner, bare, Holding(Skill.Mining, SkillBand.Apprentice, Technique.SeamStudent), ItemType.IronOre);
            Assert(plain == SkillRules.HarvestFactor(4), "With none held, the harvest's pace is its level's.");
            Assert(Near(driver, plain * 1.15f) && Near(student, plain * 0.9f), $"Hard Driver mines 15% faster, Seam Student 10% slower ({plain:0.###}: {driver:0.###}, {student:0.###}).");
            float herbs = Simpiens.Cognition.Planning.Handlers.HarvestResourceIntentHandler.SkillSpeed(miner, bare, Holding(Skill.Mining, SkillBand.Apprentice, Technique.HardDriver), ItemType.MedicinalHerb);
            Assert(herbs == SkillRules.HarvestFactor(4), "A miner's technique does nothing at the herbs.");
            TestLog.Log("[PASS] Test 6: the harvest's pace: Hard Driver mines faster, Seam Student slower, and neither forages differently.");
        }

        private static void ValidateTheMake()
        {
            var (_, sim, world) = Outpost();
            var smith = Crawler(world, sim, Vec2.Zero, Skill.Smithing, 4);
            Assert(WorkshopService.GradeOf(smith, StationKind.Forge) == Grades.Fine && WorkshopService.PaceOf(smith, StationKind.Forge) == SkillRules.MakeFactor(4), "An Apprentice 4 makes Fine at its level's pace.");
            smith.Traits = Holding(Skill.Smithing, SkillBand.Apprentice, Technique.FineTemper);
            Assert(WorkshopService.GradeOf(smith, StationKind.Forge) == Grades.Masterwork, "Fine Temper: an Apprentice 4 makes as a 6, a Masterwork.");
            Assert(Near(WorkshopService.PaceOf(smith, StationKind.Forge), SkillRules.MakeFactor(4) * 0.8f), "Fine Temper works 20% slower.");
            Assert(WorkshopService.GradeOf(smith, StationKind.Still) == Grades.Standard && WorkshopService.PaceOf(smith, StationKind.Still) == SkillRules.MakeFactor(0), "None of it at the still.");
            var hasty = Crawler(world, sim, Vec2.Zero, Skill.Smithing, 3);
            hasty.Traits = Holding(Skill.Smithing, SkillBand.Apprentice, Technique.FastForge);
            Assert(WorkshopService.GradeOf(hasty, StationKind.Forge) == Grades.Standard && Near(WorkshopService.PaceOf(hasty, StationKind.Forge), SkillRules.MakeFactor(3) * 1.2f),
                "Fast Forge: an Apprentice 3 makes as a 1, Standard, 20% faster.");
            var master = Crawler(world, sim, Vec2.Zero, Skill.Smithing, 10);
            master.Traits = Holding(Skill.Smithing, SkillBand.Apprentice, Technique.FineTemper);
            Assert(WorkshopService.GradeOf(master, StationKind.Forge) == Grades.Masterwork, "The grade's levels stop at 10.");
            TestLog.Log("[PASS] Test 7: a make's grade counts a technique's levels (Fine Temper to Masterwork, Fast Forge to Standard), its pace their cost.");
        }

        private static void ValidateTheFallen()
        {
            var (_, sim, world) = Outpost();
            var medic = Crawler(world, sim, Vec2.Zero, Skill.Medicine, 3);
            float carry = Vitality.CarrySpeed(medic), heal = Vitality.MedicineFactor(medic);
            Assert(carry == (float)(Vitality.CarrySpeedFactor + SkillRules.CarryBonus(3)) && heal == SkillRules.HealFactor(3), "With none held, the carry and the medicine are its level's.");
            medic.Traits = Holding(Skill.Medicine, SkillBand.Apprentice, Technique.StrongBack);
            Assert(Near(Vitality.CarrySpeed(medic), carry * 1.25f) && Near(Vitality.MedicineFactor(medic), heal * 0.75f), "Strong Back carries 25% faster, its medicine heals 25% less.");
            medic.Traits = Holding(Skill.Medicine, SkillBand.Apprentice, Technique.SteadyHands);
            Assert(Near(Vitality.CarrySpeed(medic), carry * 0.85f) && Near(Vitality.MedicineFactor(medic), heal * 1.4f), "Steady Hands heals 40% more, carries 15% slower.");
            Assert(Vitality.CarrySpeed(null) == Vitality.CarrySpeedFactor && Vitality.MedicineFactor(null) == 1f, "Nobody: the numbers before skills.");
            TestLog.Log("[PASS] Test 8: tending the fallen: Strong Back carries faster, Steady Hands heals more, each for the other's cost.");
        }

        private static void ValidatePracticeAndLessons()
        {
            var (_, sim, world) = Outpost();
            var ore = SkillWork.Harvest(ItemType.IronOre);
            var source = PracticeSource.WorkAt(SkillRules.MaxLevel);
            var plain = Crawler(world, sim, new Vec2(0f, 40f), Skill.Mining, 3);
            var student = Crawler(world, sim, new Vec2(20f, 40f), Skill.Mining, 3);
            student.Traits = Holding(Skill.Mining, SkillBand.Apprentice, Technique.SeamStudent);
            var keen = Crawler(world, sim, new Vec2(40f, 40f), Skill.Mining, 3);
            var keenTraits = Holding(Skill.Mining, SkillBand.Apprentice, Technique.SeamStudent);
            keenTraits.SetPassion(Skill.Mining, Passion.Keen);
            keen.Traits = keenTraits;
            var driver = Crawler(world, sim, new Vec2(60f, 40f), Skill.Mining, 3);
            driver.Traits = Holding(Skill.Mining, SkillBand.Apprentice, Technique.HardDriver);
            int Gain(AgentState who)
            {
                int had = who.Skills.Experience(Skill.Mining);
                sim.Practise(who, ore, SkillRules.UnitExperience, source);
                return who.Skills.Experience(Skill.Mining) - had;
            }
            Assert(Gain(plain) == 10 && Gain(student) == 13 && Gain(keen) == 19 && Gain(driver) == 8,
                "A unit's 10: a seam student learns 25% more (13), with a keen passion too 19, a hard driver 20% less (8).");

            // A mentor's lessons: half the act, three quarters from a Master holding Old Hand's Lessons
            var (_, sim2, world2) = Outpost();
            var master = Crawler(world2, sim2, new Vec2(0f, 0f), Skill.Mining, 9);
            var teacher = master.Traits;
            teacher.SetTechnique(Skill.Mining, SkillBand.Master, Technique.OldHandsLessons);
            master.Traits = teacher;
            var learner = Crawler(world2, sim2, new Vec2(2f, 0f), Skill.Mining, 2);
            sim2.Practise(master, ore, SkillRules.UnitExperience, source);
            int before = learner.Skills.Experience(Skill.Mining);
            sim2.Practise(learner, ore, SkillRules.UnitExperience, source);
            Assert(learner.Skills.Experience(Skill.Mining) - before == 10 + 7, $"Its own 10, and a lesson of three quarters (7) from the Master ({learner.Skills.Experience(Skill.Mining) - before}).");
            TestLog.Log("[PASS] Test 9: practice counts a technique's share beside the passion; a Master's lessons teach three quarters of the act.");
        }

        private static void ValidateUnyielding()
        {
            var cautious = new Simpiens.Cognition.Contracts.AgentPersonality(0f, -0.5f, 0f, 0f, 0f);
            var blade = new AgentInventory();
            blade.TryEquip(new ItemInstance(1, ItemType.IronBlade, 1, 12, durability: 100));
            var unyielding = Holding(Skill.Melee, SkillBand.Master, Technique.Unyielding);
            Assert(!Vitality.FightsBack(Context(cautious, blade, default)), "Struck, the cautious flee, armed or not.");
            Assert(Vitality.FightsBack(Context(cautious, blade, unyielding)), "Unyielding and armed, the cautious stand and fight.");
            Assert(!Vitality.FightsBack(Context(cautious, new AgentInventory(), unyielding)), "Unarmed, unyielding or not, they flee.");
            Assert(!Vitality.FightsBack(Context(cautious, Sling(), unyielding)), "A melee technique doesn't hold a shooter.");
            Assert(!Vitality.FightsBack(Context(cautious, blade, unyielding, health: 10f)), "Below the retreat line, even the unyielding go.");
            TestLog.Log("[PASS] Test 10: unyielding: the cautious and armed stand when struck, and nobody else does.");
        }

        private static void ValidateCapstones()
        {
            var traits = Holding(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetCalling(Calling.Warden);
            var fight = SkillWork.Fight(ranged: false);
            var journeyman = default(SkillSet);
            journeyman.Set(Skill.Melee, 8);
            var master = default(SkillSet);
            master.Set(Skill.Melee, 9);
            Assert(CrawlerTechniques.Get(traits, journeyman, fight, TechniqueStat.Defense) == 3, "A Journeyman Warden: Shield Wall's +3 defense alone.");
            Assert(CrawlerTechniques.Get(traits, master, fight, TechniqueStat.Defense) == 6, "A Master Warden: +3 more from its capstone.");
            Assert(CrawlerTechniques.Get(traits, master, SkillWork.Fight(ranged: true), TechniqueStat.Defense) == 0, "Not while it shoots: the capstone is melee's.");
            var surgeon = default(CrawlerTraits);
            surgeon.SetCalling(Calling.FieldSurgeon);
            var medic = default(SkillSet);
            medic.Set(Skill.Medicine, 10);
            Assert(CrawlerTechniques.Get(surgeon, medic, SkillWork.Tend(), TechniqueStat.Heal) == 20, "A Master Surgeon's medicine heals 20% more.");
            var captain = default(CrawlerTraits);
            captain.SetCalling(Calling.Captain);
            var leader = default(SkillSet);
            leader.Set(Skill.Leadership, 10);
            Assert(CrawlerTechniques.Get(captain, leader, SkillWork.Lead(), TechniqueStat.Pace) == 0, "A Commander has no capstone stat yet.");
            TestLog.Log("[PASS] Test 11: a calling's capstone counts at Master in its skill: a Master Warden's +3 defense, a Master Surgeon's +20% healing.");
        }

        private static Simpiens.Cognition.Contracts.AgentContext Context(in Simpiens.Cognition.Contracts.AgentPersonality personality, IAgentInventory worn, in CrawlerTraits traits, float health = Vitality.MaxHealth) =>
            new Simpiens.Cognition.Contracts.AgentContext(EntityId.Generate(), Vec2.Zero, new Simpiens.Cognition.Contracts.AgentNeeds(20f, 90f, 100f), 0f, null, null, 100,
                inventory: worn, personality: personality, health: health, skills: Fighter, traits: traits);

        private static (NarrativeChronicleService, SimulationManager, WorldRegistry) Outpost()
        {
            var world = new WorldRegistry();
            var clock = new SimulationClock();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(world, null, clock: clock, chronicleService: chronicle) { Seed = 7 };
            sim.SetEconomy(EconomyTuning.Default);
            clock.AdvanceTicks(100);
            return (chronicle, sim, world);
        }

        private static AgentState Crawler(WorldRegistry world, SimulationManager sim, Vec2 at, Skill skill, int level)
        {
            var crawler = HeadlessEntities.Crawler(world, sim, null, at, id: EntityId.Generate()).Agent;
            crawler.Position = at;
            crawler.Traits = default;
            var skills = default(SkillSet);
            skills.Set(skill, level);
            crawler.Skills = skills;
            return crawler;
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
