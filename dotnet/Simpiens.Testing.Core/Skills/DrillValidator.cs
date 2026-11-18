using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.Workshops;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// The drill yard (Epic 11, S3.4, Q10, D10): a station on Floor 1's guildhall, built for 200g into the Construction sink,
    /// with no artisan and no recipes; a guild member with no orders, fed and rested, would drill there, never over orders,
    /// needs or the yard's ceiling; a game hour's drill practises the fight work of what it wields, 30 at source Yard, times
    /// its passion, to 5. Runs as a test (ValidatorTests).
    /// </summary>
    public class DrillValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[DrillValidator] Beginning drill yard validation...");
            ValidateTheYardIsAStation();
            ValidateWhoWouldDrill();
            ValidateAnHoursDrill();
            TestLog.Log("<color=green>[DrillValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheYardIsAStation()
        {
            var (sim, ledger) = Outpost(250);
            int yard = sim.Workshops.IndexOf(StationKind.DrillYard);
            Assert(yard >= 0 && Floor1Layout.Definition.Stations[yard].Key == Floor1Layout.DrillYard, "Floor 1's guildhall holds a drill yard's place.");
            var place = Floor1Layout.Definition.Stations[yard].Position;
            Assert(Floor1Layout.Definition.FindSector(place).Id == Floor1Layout.GuildhallSectorId, "In the guildhall.");
            Assert(sim.Economy.Workshops.CostOf(StationKind.DrillYard) == 200 && sim.Economy.Workshops.CostOf(StationKind.Forge) == 300, "The yard costs 200g, the forge 300g.");
            Assert(sim.Workshops.Build(sim, yard, 0) == WorkshopRules.Check.Ok && sim.ActiveStalls[0].TreasuryGold == 50, "Built for 200g.");
            Assert(ledger.GetSinkTotal(WorkshopService.ConstructionSinkName) == 200, "Paid into the Construction sink.");
            Assert((sim.Workshops.BuiltMask & (1UL << yard)) != 0, "The built mask holds it, for the snapshot.");
            var crawler = HeadlessEntities.Crawler(new WorldRegistry(), sim, null, Vec2.Zero, id: EntityId.Generate()).Agent;
            Assert(WorkshopRules.CheckHire(sim.Workshops, sim.Guild, crawler, yard, 5, 0, sim.Economy.Workshops, sim.Economy.Guild) == WorkshopRules.Check.NoArtisanHere,
                "It takes no artisan.");
            Assert(WorkshopRules.CheckOrder(sim.Workshops, yard, ItemType.IronBlade, 1) == WorkshopRules.Check.NotThisStationsRecipe, "And makes nothing.");
            Assert(WorkshopRules.Describe(WorkshopRules.Check.CannotPay, sim.Workshops, yard, sim.Economy.Workshops) == "The stall can't pay the 200g to build the drill yard.", "Its refusal names its own cost.");
            TestLog.Log("[PASS] Test 1: the drill yard is a station in the guildhall, built for 200g, with no artisan and no recipe.");
        }

        private static void ValidateWhoWouldDrill()
        {
            var map = Floor1Layout.Definition;
            int yard = -1;
            for (int i = 0; i < map.Stations.Count; i++) if (map.Stations[i].Kind == StationKind.DrillYard) yard = i;
            var built = Snapshot(map, 1UL << yard);
            var unbuilt = Snapshot(map, 0);
            Assert(Drill.WouldDrill(Context(built)), "A member, fed and rested, with the yard standing, would drill.");
            Assert(Drill.TryFindYard(Context(built), out int found, out var at, out _) && found == yard && at == map.Stations[yard].Position, "It finds the yard.");
            Assert(!Drill.WouldDrill(Context(unbuilt)), "Not with the yard unbuilt.");
            Assert(!Drill.WouldDrill(Context(built, guild: 0)), "Not a resident outside the guild.");
            Assert(!Drill.WouldDrill(Context(built, hunger: 60f)) && !Drill.WouldDrill(Context(built, energy: 40f)), "Not hungry or tired: needs come first.");
            Assert(!Drill.WouldDrill(Context(built, expedition: new ExpeditionOrder(ExpeditionStage.Muster, 1, default, Vec2.Zero))), "Not on an expedition: orders come first.");
            Assert(!Drill.WouldDrill(Context(built, station: new StationOrder(0, Vec2.Zero, 1f))), "Not on a shift.");
            var veteran = default(SkillSet);
            veteran.Set(Skill.Melee, SkillRules.YardCeiling);
            Assert(!Drill.WouldDrill(Context(built, skills: veteran)), "Not at the yard's ceiling: it teaches to 5.");
            var archer = new Simpiens.Cognition.Inventory.AgentInventory();
            archer.TryEquip(new ItemInstance(1, ItemType.Sling, 1, 12, durability: 100));
            Assert(Drill.WorkOf(archer).Equals(SkillWork.Fight(ranged: true)) && Drill.WorkOf(new Simpiens.Cognition.Inventory.AgentInventory()).Equals(SkillWork.Fight(ranged: false)),
                "A sling drills shooting; the unarmed drill blows.");
            TestLog.Log("[PASS] Test 2: a guild member with no orders, fed and rested, below 5, would drill at a standing yard; nobody else, and never over orders or needs.");
        }

        private static void ValidateAnHoursDrill()
        {
            var registry = new WorldRegistry();
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition); // the map, and its stations, are the sectors' (as a respawn loads them)
            var sim = new SimulationManager(registry, null, sectors: sectors);
            sim.SetEconomy(EconomyTuning.Default);
            int yard = -1;
            for (int i = 0; i < Floor1Layout.Definition.Stations.Count; i++) if (Floor1Layout.Definition.Stations[i].Kind == StationKind.DrillYard) yard = i;
            var at = Floor1Layout.Definition.Stations[yard].Position;
            var told = new List<(AgentState Who, Skill Skill, int Experience, PracticeSource Source)>();
            sim.Practised += (who, skill, experience, source) => told.Add((who, skill, experience, source));

            var plain = Member(registry, sim, at, Passion.None, 0);
            var burning = Member(registry, sim, at, Passion.Burning, 0);
            var near = Member(registry, sim, at, Passion.Burning, 5);
            foreach (var m in new[] { plain, burning, near })
            {
                var state = new ActiveIntentState { Intent = new DrillIntent(m.Node.Id, yard, null), CurrentWaypointIndex = 0, LastSampledPosition = m.Node.Position };
                Assert(sim.RunToCompletion(m.Node, state, DayClock.TicksPerHour + 5) == IntentResult.Success, "A game hour's drill ends.");
            }
            Assert(plain.Agent.Skills.Experience(Skill.Melee) == SkillRules.YardExperiencePerHour, $"An hour's drill: 30 at melee ({plain.Agent.Skills.Experience(Skill.Melee)}).");
            Assert(told.TrueForAll(t => t.Source.Kind == PracticeKind.Yard && t.Source.Ceiling == SkillRules.YardCeiling), "Told at source Yard, to 5.");
            Assert(told.FindAll(t => t.Who == plain.Agent).Count == SkillRules.YardLotsPerHour, "In six lots.");
            Assert(burning.Agent.Skills.Experience(Skill.Melee) == 2 * SkillRules.YardExperiencePerHour, "Burning for melee, twice that.");
            Assert(near.Agent.Skills.Experience(Skill.Melee) == SkillRules.ExperienceFor(SkillRules.YardCeiling), "At 5, the yard teaches nothing more.");
            Assert(plain.Agent.LastDrilledAt == sim.CurrentTick && plain.Agent.VisualState == Simpiens.Entities.AgentVisualState.Idle, "It marks when it drilled, and stands down.");
            TestLog.Log("[PASS] Test 3: a game hour's drill practises the work of what it wields, 30 at source Yard, times its passion, to 5.");
        }

        private static (SimulationManager, EconomyLedger) Outpost(uint treasury)
        {
            var world = new WorldRegistry();
            var ledger = new EconomyLedger();
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            var sim = new SimulationManager(world, null, sectors: sectors, economyLedger: ledger);
            sim.SetEconomy(EconomyTuning.Default);
            sim.RegisterTradeStall(TestStalls.Stall(EntityId.Generate(), Vec2.Zero, treasuryGold: treasury));
            ledger.RecordFaucet(treasury, "InitialSeed");
            return (sim, ledger);
        }

        private static SharedWorldSnapshot Snapshot(MapDefinition map, ulong built)
        {
            var snapshot = new SharedWorldSnapshot(8, 8, 8f, 4, null);
            snapshot.SetMap(map);
            snapshot.SetBuiltStations(built);
            return snapshot;
        }

        private static AgentContext Context(SharedWorldSnapshot snapshot, byte guild = 1, float hunger = 20f, float energy = 90f, ExpeditionOrder expedition = default,
            StationOrder station = default, SkillSet skills = default)
            => new AgentContext(EntityId.Generate(), Vec2.Zero, new AgentNeeds(hunger, energy, 100f), 0f, snapshot, null, 100,
                inventory: new Simpiens.Cognition.Inventory.AgentInventory(), expedition: expedition, station: station, skills: skills, guildGroupId: guild);

        private static (WorldEntity Node, AgentState Agent) Member(WorldRegistry registry, SimulationManager sim, Vec2 at, Passion passion, int level)
        {
            var crawler = HeadlessEntities.Crawler(registry, sim, null, at, id: EntityId.Generate());
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, level);
            crawler.Agent.Skills = skills;
            var traits = default(CrawlerTraits);
            traits.SetPassion(Skill.Melee, passion);
            crawler.Agent.Traits = traits;
            return (crawler.Node, crawler.Agent);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[DrillValidator] FAILED: " + message);
        }
    }
}
