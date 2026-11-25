using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The engine-free domain validators (Simpiens.Testing.Core). A validator throws on its first failed assertion; its
    /// log is the test output.
    /// </summary>
    public class ValidatorTests
    {
        [Fact]
        public void RestAnchor() => new Simpiens.Testing.Drives.RestAnchorValidator().RunAllValidationTests();

        [Fact]
        public void CalmCommand() => new Simpiens.Testing.Drives.CalmCommandValidator().RunAllValidationTests();

        [Fact]
        public void EconomyTuning() => new Simpiens.Testing.Economy.EconomyTuningValidator().RunAllValidationTests();

        [Fact]
        public void PersistenceToolchain() => new Simpiens.Testing.Persistence.PersistenceToolchainValidator().RunAllValidationTests();

        [Fact]
        public void SessionCapture() => new Simpiens.Testing.Persistence.SessionCaptureValidator().RunAllValidationTests();

        [Fact]
        public void SessionRestore() => new Simpiens.Testing.Persistence.SessionRestoreValidator().RunAllValidationTests();

        [Fact]
        public void SaveFile() => new Simpiens.Testing.Persistence.SaveFileValidator().RunAllValidationTests();

        [Fact]
        public void EconomySerialization() => new Simpiens.Testing.Economy.EconomySerializationValidator().RunAllValidationTests();

        [Fact]
        public void EconomyZeroAlloc() => new Simpiens.Testing.Economy.EconomyZeroAllocValidator().RunAllValidationTests();

        [Fact]
        public void Personality() => new Simpiens.Testing.Planning.PersonalityValidator().RunAllValidationTests();

        [Fact]
        public void Motives() => new Simpiens.Testing.Planning.MotiveValidator().RunAllValidationTests(); // Epic LIFE-1, L0.2

        [Fact]
        public void GoalSelector() => new Simpiens.Testing.Planning.GoalSelectorValidator().RunAllValidationTests(); // Epic LIFE-1, L0.4

        [Fact]
        public void MotiveModel() => new Simpiens.Testing.Planning.MotiveModelValidator().RunAllValidationTests(); // Epic LIFE-1, L0.5

        [Fact]
        public void Thoughts() => new Simpiens.Testing.Life.ThoughtsValidator().RunAllValidationTests(); // Epic LIFE-1, L1.1

        [Fact]
        public void ThoughtRules() => new Simpiens.Testing.Life.ThoughtRulesValidator().RunAllValidationTests(); // Epic LIFE-1, L1.2

        [Fact]
        public void Mood() => new Simpiens.Testing.Life.MoodValidator().RunAllValidationTests(); // Epic LIFE-1, L1.3

        [Fact]
        public void Fun() => new Simpiens.Testing.Life.FunValidator().RunAllValidationTests(); // Epic LIFE-1 plan 2, L2.1

        [Fact]
        public void Activities() => new Simpiens.Testing.Life.ActivityValidator().RunAllValidationTests(); // Epic LIFE-1 plan 2, L2.2

        [Fact]
        public void Venues() => new Simpiens.Testing.World.VenueValidator().RunAllValidationTests(); // Epic LIFE-1 plan 2, L2.3

        [Fact]
        public void Recreate() => new Simpiens.Testing.Life.RecreateValidator().RunAllValidationTests(); // Epic LIFE-1 plan 2, L2.5

        [Fact]
        public void Stations() => new Simpiens.Testing.Economy.StationsValidator().RunAllValidationTests(); // Epic LIFE-1 plan 2, T.1

        [Fact]
        public void TavernMenu() => new Simpiens.Testing.Economy.TavernMenuValidator().RunAllValidationTests(); // Epic LIFE-1 plan 2, T.3

        [Fact]
        public void Wants() => new Simpiens.Testing.Life.WantsValidator().RunAllValidationTests(); // Epic LIFE-1 plan 2, W.1-W.3

        [Fact]
        public void Routine() => new Simpiens.Testing.Life.RoutineValidator().RunAllValidationTests(); // Epic LIFE-1 plan 3, R.1

        [Fact]
        public void Weariness() => new Simpiens.Testing.Life.WearinessValidator().RunAllValidationTests(); // Epic LIFE-1 plan 3, R.5

        [Fact]
        public void Intention() => new Simpiens.Testing.Life.IntentionValidator().RunAllValidationTests(); // Epic LIFE-1 plan 3, R.7

        [Fact]
        public void Look() => new Simpiens.Testing.People.LookValidator().RunAllValidationTests();

        [Fact]
        public void Skills() => new Simpiens.Testing.Skills.SkillsValidator().RunAllValidationTests();

        [Fact]
        public void Deeds() => new Simpiens.Testing.Skills.DeedsValidator().RunAllValidationTests();

        [Fact]
        public void Passions() => new Simpiens.Testing.Skills.PassionValidator().RunAllValidationTests();

        [Fact]
        public void Upkeep() => new Simpiens.Testing.Skills.UpkeepValidator().RunAllValidationTests();

        [Fact]
        public void Mentors() => new Simpiens.Testing.Skills.MentorValidator().RunAllValidationTests();

        [Fact]
        public void Manuals() => new Simpiens.Testing.Skills.ManualValidator().RunAllValidationTests();

        [Fact]
        public void Reading() => new Simpiens.Testing.Skills.ReadingValidator().RunAllValidationTests();

        [Fact]
        public void DrillYard() => new Simpiens.Testing.Skills.DrillValidator().RunAllValidationTests();

        [Fact]
        public void Techniques() => new Simpiens.Testing.Skills.TechniqueValidator().RunAllValidationTests();

        [Fact]
        public void TechniquePicks() => new Simpiens.Testing.Skills.TechniquePickValidator().RunAllValidationTests();

        [Fact]
        public void TechniqueEffects() => new Simpiens.Testing.Skills.TechniqueEffectsValidator().RunAllValidationTests();

        [Fact]
        public void Sponsorship() => new Simpiens.Testing.Skills.SponsorValidator().RunAllValidationTests();

        [Fact]
        public void Leadership() => new Simpiens.Testing.Skills.LeadershipValidator().RunAllValidationTests();

        [Fact]
        public void Callings() => new Simpiens.Testing.Skills.CallingValidator().RunAllValidationTests();

        [Fact]
        public void PartyRoles() => new Simpiens.Testing.Skills.PartyRoleValidator().RunAllValidationTests();

        [Fact]
        public void Epithets() => new Simpiens.Testing.Skills.EpithetValidator().RunAllValidationTests();

        [Fact]
        public void Scars() => new Simpiens.Testing.Skills.ScarValidator().RunAllValidationTests();

        [Fact]
        public void EarnedTraits() => new Simpiens.Testing.Skills.EarnedTraitValidator().RunAllValidationTests();

        [Fact]
        public void GivenNames() => new Simpiens.Testing.People.GivenNamesValidator().RunAllValidationTests();

        [Fact]
        public void Guild() => new Simpiens.Testing.Economy.GuildValidator().RunAllValidationTests();

        [Fact]
        public void GuildWages() => new Simpiens.Testing.Economy.GuildWageValidator().RunAllValidationTests();

        [Fact]
        public void AffinityMatrix() => new Simpiens.Testing.Sociopolitical.AffinityMatrixValidator().Start();

        [Fact]
        public void AgentMemory() => new Simpiens.Testing.Memory.AgentMemoryValidator().Start();

        [Fact]
        public void AltruisticSharing() => new Simpiens.Testing.Sociopolitical.AltruisticSharingValidator().Start();

        [Fact]
        public void BountyFulfillment() => new Simpiens.Testing.Economy.BountyFulfillmentValidator().Start();

        [Fact]
        public void DirectiveCompliance() => new Simpiens.Testing.Sociopolitical.DirectiveComplianceValidator().Start();

        [Fact]
        public void DayPace() => new Simpiens.Testing.Drives.DayPaceValidator().Start();

        [Fact]
        public void DriveDecay() => new Simpiens.Testing.Drives.DriveDecayValidator().Start();

        [Fact]
        public void DriveSystem() => new Simpiens.Testing.Drives.DriveSystemValidator().Start();

        [Fact]
        public void GatheringAnchor() => new Simpiens.Testing.Sociopolitical.GatheringAnchorValidator().Start();

        [Fact]
        public void GoapDomain() => new Simpiens.Testing.Planning.GoapDomainValidator().Start();

        [Fact]
        public void GoapPlanner() => new Simpiens.Testing.Planning.GoapPlannerValidator().Start();

        [Fact]
        public void GroupPrimitive() => new Simpiens.Testing.Sociopolitical.GroupPrimitiveValidator().Start();

        [Fact]
        public void Inventory() => new Simpiens.Testing.Economy.InventoryValidator().Start();

        [Fact]
        public void MentalBreak() => new Simpiens.Testing.Drives.MentalBreakValidator().Start();

        [Fact]
        public void MotorWatchdog() => new Simpiens.Testing.Navigation.MotorWatchdogValidator().Start();

        [Fact]
        public void NarrativeChronicle() => new Simpiens.Testing.UI.NarrativeChronicleValidator().RunAllValidationTests();

        [Fact]
        public void PlanExecution() => new Simpiens.Testing.Planning.PlanExecutionValidator().Start();

        [Fact]
        public void ResourceContention() => new Simpiens.Testing.Sociopolitical.ResourceContentionValidator().Start();

        [Fact]
        public void Succession() => new Simpiens.Testing.Sociopolitical.SuccessionValidator().Start();

        [Fact]
        public void TradeTransaction() => new Simpiens.Testing.Economy.TradeTransactionValidator().Start();

        [Fact]
        public void EconomyCounters() => new Simpiens.Testing.Economy.EconomyCountersValidator().RunAllValidationTests();

        [Fact]
        public void GoapLiveLoopFixes() => new Simpiens.Testing.Planning.GoapLiveLoopFixesValidator().Start();

        [Fact]
        public void Logging() => new Simpiens.Testing.Diagnostics.LoggingValidator().Start();

        [Fact]
        public void SupplyLoop() => new Simpiens.Testing.Economy.SupplyLoopValidator().Start();

        [Fact]
        public void MapDefinition() => new Simpiens.Testing.World.MapDefinitionValidator().RunAllValidationTests();

        [Fact]
        public void SectorCharting() => new Simpiens.Testing.World.SectorChartingValidator().RunAllValidationTests();

        [Fact]
        public void WorldMap() => new Simpiens.Testing.World.WorldMapValidator().RunAllValidationTests();

        [Fact]
        public void SimulationClock() => new Simpiens.Testing.Environment.SimulationClockValidator().RunAllValidationTests();

        [Fact]
        public void AgentState() => new Simpiens.Testing.Environment.AgentStateValidator().RunAllValidationTests();

        [Fact]
        public void SingleWriter() => new Simpiens.Testing.Environment.SingleWriterMemoryValidator().RunAllValidationTests();

        [Fact]
        public void CognitionScheduler() => new Simpiens.Testing.Environment.CognitionSchedulerValidator().RunAllValidationTests();

        [Fact]
        public void SimulationManagerModularization() => new Simpiens.Testing.Environment.SimulationManagerModularizationValidator().RunAllValidationTests();

        [Fact]
        public void GridAStar() => new Simpiens.Testing.Navigation.GridAStarValidator().RunAllValidationTests();

        [Fact]
        public void Exploration() => new Simpiens.Testing.Navigation.ExplorationValidator().RunAllValidationTests();

        [Fact]
        public void NavigationClearance() => new Simpiens.Testing.Navigation.NavigationClearanceValidator().RunAllValidationTests();

        [Fact]
        public void Determinism() => new Simpiens.Testing.Environment.DeterminismValidator().RunAllValidationTests();

        [Fact]
        public void ItemCatalog() => new Simpiens.Testing.Economy.ItemCatalogValidator().RunAllValidationTests();

        [Fact]
        public void EquipmentSlots() => new Simpiens.Testing.Economy.EquipmentSlotsValidator().RunAllValidationTests();

        [Fact]
        public void Outfitting() => new Simpiens.Testing.Economy.OutfittingValidator().RunAllValidationTests();

        [Fact]
        public void Lairs() => new Simpiens.Testing.Combat.LairValidator().RunAllValidationTests();

        [Fact]
        public void NamedCreatures() => new Simpiens.Testing.Combat.NamedCreatureValidator().RunAllValidationTests();

        [Fact]
        public void Grudges() => new Simpiens.Testing.Combat.GrudgeValidator().RunAllValidationTests();

        [Fact]
        public void Masterpieces() => new Simpiens.Testing.Economy.MasterpieceValidator().RunAllValidationTests();

        [Fact]
        public void Inspiration() => new Simpiens.Testing.Economy.InspirationValidator().RunAllValidationTests();

        [Fact]
        public void Recipes() => new Simpiens.Testing.Economy.RecipeValidator().RunAllValidationTests();

        [Fact]
        public void Crafting() => new Simpiens.Testing.Economy.CraftingValidator().RunAllValidationTests();

        [Fact]
        public void Workshops() => new Simpiens.Testing.Economy.WorkshopValidator().RunAllValidationTests();

        [Fact]
        public void Tools() => new Simpiens.Testing.Economy.ToolValidator().RunAllValidationTests();

        [Fact]
        public void Loot() => new Simpiens.Testing.Economy.LootValidator().RunAllValidationTests();

        [Fact]
        public void Combat() => new Simpiens.Testing.Combat.CombatValidator().RunAllValidationTests();

        [Fact]
        public void CreatureCatalog() => new Simpiens.Testing.Combat.CreatureCatalogValidator().RunAllValidationTests();
    }
}
