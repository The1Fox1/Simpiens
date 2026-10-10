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
        [Timeout(300000)]
        public IEnumerator Scenario_OutpostSupplyLoop_EmergesWithoutInjection()
        {
            yield return ExecuteScenario(new OutpostSupplyLoopScenario());
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
        public void Scenario_OutpostMapBuilder_AllDomainValidationTestsPass()
        {
            var validator = new Simpiens.Testing.Environment.OutpostMapBuilderValidator();
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

            // 3. Cleanly reset simulation world and respawn fresh entities for this scenario
            var spawner = container.Resolve<SwarmSpawner>();
            Assert.IsNotNull(spawner, "SwarmSpawner service is not registered.");
            spawner.Respawn();

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
