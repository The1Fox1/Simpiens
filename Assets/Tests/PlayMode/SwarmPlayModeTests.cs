using Simpiens.Core.Session;
using System.Collections;
using System.Text;
using NUnit.Framework;
using Simpiens.Core.Bootstrapping;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using Simpiens.Tests.PlayMode.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Simpiens.Tests.PlayMode
{
    /// <summary>
    /// Formal PlayMode integration test suite for the Simpiens multi-agent simulation.
    /// Runs real Unity engine frames, physics, and view components, asserting zero visual or physical anomalies.
    /// </summary>
    public class SwarmPlayModeTests
    {
        [UnityTest]
        public IEnumerator Scenario_SwarmNavigation_ZeroVisualAnomalies()
        {
            yield return ExecuteScenario(new SwarmNavigationScenario());
        }

        [UnityTest]
        public IEnumerator Scenario_ResourceContention_NoPawnStacking()
        {
            yield return ExecuteScenario(new ResourceContentionScenario());
        }

        [UnityTest]
        public IEnumerator Scenario_DynamicWallInjection_UnstuckWithoutOscillation()
        {
            yield return ExecuteScenario(new DynamicChaosScenario());
        }

        [UnityTest]
        [Timeout(200000)]
        public IEnumerator Scenario_PenCanary_TenPenOutcomesHold()
        {
            yield return ExecuteScenario(new PenCanaryScenario());
        }

        [UnityTest]
        [Timeout(200000)]
        public IEnumerator Scenario_SessionSwitch_DevMapRoundTripResetsCleanly()
        {
            yield return ExecuteScenario(new SessionSwitchScenario());
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Scenario_BunkRest_TiredCrawlersSleepInTheBunkhouse()
        {
            yield return ExecuteScenario(new BunkRestScenario());
        }

        [Test]
        public void Scenario_RestAnchor_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Drives.RestAnchorValidator();
            validator.RunAllValidationTests();
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Scenario_PlayerScout_ChartsTheOvergrowthFromTheMap()
        {
            yield return ExecuteScenario(new PlayerScoutScenario());
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Scenario_MapHud_WorksOverTheMap()
        {
            yield return ExecuteScenario(new MapHudScenario());
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Scenario_ShroudView_FogFollowsTheChart()
        {
            yield return ExecuteScenario(new ShroudViewScenario());
        }

        /// <summary>Epic MAP Definition of Done: the outpost's whole day over 9,000 ticks, nothing injected.</summary>
        [UnityTest]
        [Timeout(500000)]
        public IEnumerator Scenario_OutpostLifecycle_TheOutpostsDayEmerges()
        {
            yield return ExecuteScenario(new OutpostLifecycleScenario());
        }

        /// <summary>Epic MAP determinism check: two same-seed runs of the outpost to t3,000, hashed every 100 ticks.</summary>
        [UnityTest]
        [Timeout(400000)]
        public IEnumerator Scenario_Determinism_SameSeedSameWorld()
        {
            yield return ExecuteScenario(new DeterminismScenario());
        }

        /// <summary>
        /// Epic CORE C2: the outpost reproduces its golden world hashes for this process's seed (the default seed in a
        /// full run; other seeds through scripts/golden-hashes.sh, which can also re-record them).
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Scenario_GoldenWorldHash_ReproducesTheBaseline()
        {
            yield return ExecuteScenario(new GoldenWorldHashScenario());
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Scenario_OutpostSupplyLoop_EmergesWithoutInjection()
        {
            yield return ExecuteScenario(new OutpostSupplyLoopScenario());
        }

        /// <summary>
        /// Economy soak (30 sim-minutes, ~3 min wall clock). Explicit: not part of the regular suite.
        /// Run with scripts/run-economy-soak.sh (profile / seed via SIMPIENS_SOAK_PROFILE / SIMPIENS_SEED).
        /// </summary>
        /// <summary>
        /// Population scaling measurement (Epic MAP Phase 3): 12, 16 and 24 crawlers on Floor 1, cognition latency and
        /// throughput to Logs/population-scaling.csv. Explicit: measurement, not a regression test.
        /// </summary>
        [UnityTest]
        [Explicit("Measurement only; run on its own (-testFilter Scenario_PopulationScaling_Measure)")]
        [Category("Probe")]
        [Timeout(900000)]
        public IEnumerator Scenario_PopulationScaling_Measure()
        {
            yield return ExecuteScenario(new PopulationScalingProbe());
        }

        [UnityTest]
        [Explicit("Long-running economy soak; run via scripts/run-economy-soak.sh")]
        [Category("Soak")]
        [Timeout(900000)]
        public IEnumerator Scenario_EconomySoak_PaceBands()
        {
            yield return ExecuteScenario(new EconomySoakScenario());
        }

        [UnityTest]
        [Timeout(400000)]
        public IEnumerator Scenario_PriceResponse_DemandFallsGradually()
        {
            yield return ExecuteScenario(new PriceResponseScenario());
        }

        [Test]
        public void Scenario_EconomyTuning_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Economy.EconomyTuningValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_PersistenceToolchain_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Persistence.PersistenceToolchainValidator().RunAllValidationTests();
        }

        [UnityTest]
        public IEnumerator Scenario_SessionCapture_SameStateSameBytes()
        {
            yield return ExecuteScenario(new SessionCaptureScenario());
        }

        [Test]
        public void Scenario_SessionCapture_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Persistence.SessionCaptureValidator().RunAllValidationTests();
        }

        /// <summary>The save/load Definition of Done (Milestone 1): saved mid-bounty, loaded into a fresh session, played on.</summary>
        [UnityTest]
        [Timeout(400000)]
        public IEnumerator Scenario_SaveLoad_LoadedOutpostPlaysOn()
        {
            yield return ExecuteScenario(new SaveLoadScenario());
        }

        [UnityTest]
        public IEnumerator Scenario_QuickSaveButtons_SaveAndLoadFromTheHeader()
        {
            yield return ExecuteScenario(new QuickSaveButtonsScenario());
        }

        [UnityTest]
        public IEnumerator Scenario_SessionRestore_LoadsBackExactly()
        {
            yield return ExecuteScenario(new SessionRestoreScenario());
        }

        [Test]
        public void Scenario_SessionRestore_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Persistence.SessionRestoreValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_SaveFile_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Persistence.SaveFileValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_EconomySerialization_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Economy.EconomySerializationValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_EconomyZeroAlloc_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Economy.EconomyZeroAllocValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_Personality_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Planning.PersonalityValidator();
            validator.RunAllValidationTests();
        }

        // Domain validators that previously ran only as boot-time IStartables (failures were only logged)
        [Test]
        public void Scenario_AffinityMatrix_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Sociopolitical.AffinityMatrixValidator().Start();
        }

        [Test]
        public void Scenario_AgentMemory_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Memory.AgentMemoryValidator().Start();
        }

        [Test]
        public void Scenario_AltruisticSharing_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Sociopolitical.AltruisticSharingValidator().Start();
        }

        [Test]
        public void Scenario_BountyFulfillment_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Economy.BountyFulfillmentValidator().Start();
        }

        [Test]
        public void Scenario_DirectiveCompliance_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Sociopolitical.DirectiveComplianceValidator().Start();
        }

        [Test]
        public void Scenario_DriveDecay_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Drives.DriveDecayValidator().Start();
        }

        [Test]
        public void Scenario_DriveSystem_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Drives.DriveSystemValidator().Start();
        }

        [Test]
        public void Scenario_CalmCommand_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Drives.CalmCommandValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_FacilityZoneService_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.UI.FacilityZoneServiceValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_GatheringAnchor_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Sociopolitical.GatheringAnchorValidator().Start();
        }

        [Test]
        public void Scenario_GoapDomain_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Planning.GoapDomainValidator().Start();
        }

        [Test]
        public void Scenario_GoapPlanner_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Planning.GoapPlannerValidator().Start();
        }

        [Test]
        public void Scenario_GroupPrimitive_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Sociopolitical.GroupPrimitiveValidator().Start();
        }

        [Test]
        public void Scenario_Inventory_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Economy.InventoryValidator().Start();
        }

        [Test]
        public void Scenario_MentalBreak_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Drives.MentalBreakValidator().Start();
        }

        [Test]
        public void Scenario_MotorWatchdog_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Navigation.MotorWatchdogValidator().Start();
        }

        [Test]
        public void Scenario_NarrativeChronicle_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.UI.NarrativeChronicleValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_PlanExecution_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Planning.PlanExecutionValidator().Start();
        }

        [Test]
        public void Scenario_ResourceContention_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Sociopolitical.ResourceContentionValidator().Start();
            new Simpiens.Testing.UI.AgentSpriteValidator().RunAllValidationTests(); // its sprite check, split off (Epic CORE C7)
        }

        [Test]
        public void Scenario_Succession_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Sociopolitical.SuccessionValidator().Start();
        }

        [Test]
        public void Scenario_TradeTransaction_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Economy.TradeTransactionValidator().Start();
        }

        [Test]
        public void Scenario_VisualAnomalyDetector_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Diagnostics.VisualAnomalyDetectorValidator().Start();
        }

        [Test]
        public void Scenario_EconomyCounters_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Economy.EconomyCountersValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_GoapLiveLoopFixes_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Planning.GoapLiveLoopFixesValidator();
            validator.Start();
        }

        [Test]
        public void Scenario_Logging_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Diagnostics.LoggingValidator();
            validator.Start();
        }

        [Test]
        public void Scenario_SupplyLoop_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Economy.SupplyLoopValidator();
            validator.Start();
        }

        [Test]
        public void Scenario_BountyTerminal_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.UI.BountyTerminalValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_MapDefinition_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.World.MapDefinitionValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_SectorCharting_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.World.SectorChartingValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_CameraRig_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.UI.CameraRigValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_MapView_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.World.MapViewValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_WorldMap_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.World.WorldMapValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_SimulationClock_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Environment.SimulationClockValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_AgentState_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Environment.AgentStateValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_SingleWriter_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Environment.SingleWriterMemoryValidator();
            validator.RunAllValidationTests();
        }

        /// <summary>Epic CORE C1: decisions land at T + 1 in agent-id order, applied on the main thread.</summary>
        [Test]
        public void Scenario_CognitionScheduler_AllDomainValidationTestsPass()
        {
            // Two cases fail on purpose (a preparation and an evaluation that throw), and the scheduler logs them as errors
            LogAssert.ignoreFailingMessages = true;
            try
            {
                new Simpiens.Testing.Environment.CognitionSchedulerValidator().RunAllValidationTests();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>Epic CORE C3: EntityId is bit-compatible with UnityEngine.GUID, which it replaced.</summary>
        [Test]
        public void Scenario_EntityIdCompatibility_AllDomainValidationTestsPass()
        {
            new Simpiens.Testing.Environment.EntityIdCompatibilityValidator().RunAllValidationTests();
        }

        [Test]
        public void Scenario_SimulationManagerModularization_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Environment.SimulationManagerModularizationValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_GridAStar_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Navigation.GridAStarValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_Exploration_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Navigation.ExplorationValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_NavigationClearance_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Navigation.NavigationClearanceValidator();
            validator.RunAllValidationTests();
        }

        [Test]
        public void Scenario_Determinism_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Environment.DeterminismValidator();
            validator.RunAllValidationTests();
        }

        private IEnumerator ExecuteScenario(ISimulationScenario scenario)
        {
            Debug.Log($"[SwarmPlayModeTests] >>> Starting Scenario: {scenario.Name} <<<");

            // 1. Ensure the simulation scene is loaded
            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                var loadOp = SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
                while (!loadOp.isDone)
                {
                    yield return null;
                }
            }

            // 2. Wait for VContainer LifetimeScope initialization
            float timeout = 5.0f;
            while ((AppBootstrapper.Instance == null || AppBootstrapper.Instance.Container == null) && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(AppBootstrapper.Instance, "AppBootstrapper failed to initialize.");
            var container = AppBootstrapper.Instance.Container;
            Assert.IsNotNull(container, "VContainer resolver failed to build.");

            // 3. Cleanly reset simulation world and respawn the scenario's session (never whatever was loaded last)
            var spawner = container.Resolve<SwarmSpawner>();
            Assert.IsNotNull(spawner, "SwarmSpawner service is not registered.");
            var session = spawner.Find(scenario.SessionId);
            Assert.IsNotNull(session, $"Session '{scenario.SessionId}' is not registered.");
            spawner.Respawn(session);

            // Allow 1 frame for physics and spatial grid to settle
            yield return null;

            // 4. Obtain the runtime anomaly detector and reset metrics for this scenario
            var detector = container.Resolve<IVisualAnomalyDetector>();
            Assert.IsNotNull(detector, "IVisualAnomalyDetector service is not registered.");
            detector.ResetMetrics();

            // 5. Run scenario setup (configuring speed multipliers, initial state)
            yield return scenario.SetupAsync(container);

            // 6. Execute simulation for the scenario duration
            yield return new WaitForSeconds(scenario.DurationSeconds);

            // 6. Assert results and report any anomaly diagnostic traces
            try
            {
                scenario.AssertResults(detector);
                Debug.Log($"<color=green>[SwarmPlayModeTests] Scenario {scenario.Name} PASSED with 0 unexpected anomalies.</color>");
            }
            catch (AssertionException ex)
            {
                var anomalies = detector.GetRecentAnomalies(10);
                var sb = new StringBuilder();
                sb.AppendLine($"[SwarmPlayModeTests] Scenario {scenario.Name} FAILED: {ex.Message}");
                sb.AppendLine("Recent Visual Anomalies Captured:");
                for (int i = 0; i < anomalies.Count; i++)
                {
                    sb.AppendLine($"  #{i + 1}: {anomalies[i]}");
                }
                Debug.LogError(sb.ToString());
                throw;
            }
        }
    }
}
