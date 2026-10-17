using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The engine-free domain validators (Simpiens.Testing.Core), the same ones the Unity suite runs in
    /// SwarmPlayModeTests. A validator throws on its first failed assertion; its log is the test output.
    /// </summary>
    public class ValidatorTests
    {
        [Fact]
        public void RestAnchor() => new Simpiens.Testing.Drives.RestAnchorValidator().RunAllValidationTests();

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
    }
}
