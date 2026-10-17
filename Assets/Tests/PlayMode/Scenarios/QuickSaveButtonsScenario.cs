using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// The dashboard's quick save and load (Milestone 1, D2 = A), through its header buttons, asserting view state only.
    /// Load with an empty slot says so; Save writes the slot at the tick it was pressed and says so; after the game moves
    /// on, Load brings that tick back and says so. The dashboard is built with its own slot in a temporary folder, so a
    /// test never touches the player's quick save. Also checks the dossier no longer offers Feed ration (D6 a).
    /// </summary>
    public class QuickSaveButtonsScenario : ISimulationScenario
    {
        private const long PlayTicks = 300;
        private const long MoreTicks = 100;
        private const float WallClockTimeoutSeconds = 60f;

        public string Name => "QuickSaveButtonsScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private readonly List<string> _failures = new List<string>();
        private readonly List<string> _notes = new List<string>();

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var spawner = resolver.Resolve<SwarmSpawner>();
            var clock = resolver.Resolve<ISimulationClock>();
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            Assert.IsNotNull(clock, "ISimulationClock not registered.");

            string directory = Path.Combine(Application.temporaryCachePath, "QuickSaveButtonsScenario");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            var slot = new SaveGameService(spawner, directory);

            // Loading the test scene destroyed the boot-time dashboard: build one as boot does, with the scenario's slot
            var scope = resolver.CreateScope(builder => builder.RegisterInstance<ISaveGameService>(slot));
            var dashboard = GameObject.Find("[FacilityDashboard]");
            if (dashboard != null) Object.Destroy(dashboard);
            yield return null;
            dashboard = Simpiens.Core.Bootstrapping.SimulationStarter.CreateDashboard(scope);
            yield return null; // presenter Start

            try
            {
                var ui = dashboard.GetComponent<UIDocument>()?.rootVisualElement;
                Check(ui != null, "The dashboard has a visual tree.");
                Check(ui?.Q<Button>("btn-feed-ration") == null, "The dossier no longer offers Feed ration (D6 a).");
                var status = ui?.Q<Label>("save-status-label");

                // An empty slot
                SetSpeed(clock, 0f);
                Press(ui, "btn-quick-load");
                Check(status != null && status.text == "There is no quick save yet.", $"Load with an empty slot says so (got '{status?.text}').");

                // Play, then save
                SetSpeed(clock, TimeMultiplier);
                yield return RunUntil(clock, PlayTicks);
                SetSpeed(clock, 0f);
                long savedTick = clock.CurrentTick;
                Press(ui, "btn-quick-save");
                float waited = 0f;
                while (slot.IsSaving || status == null || !status.text.StartsWith("Saved"))
                {
                    if (waited > 5f) break;
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                Check(status != null && status.text == $"Saved at tick {savedTick}.", $"Save says what it saved (got '{status?.text}', expected tick {savedTick}).");
                Check(File.Exists(slot.QuickSavePath), "Save wrote the slot.");
                if (File.Exists(slot.QuickSavePath) && SaveFile.TryDecode(File.ReadAllBytes(slot.QuickSavePath), out var onDisk, out _))
                {
                    Check(onDisk.Header.Tick == savedTick, $"The slot holds the tick Save was pressed at ({onDisk.Header.Tick} vs {savedTick}).");
                    Check(onDisk.Crawlers.Length == OutpostSession.DefaultCrawlerCount, "The slot holds every crawler.");
                }
                else
                {
                    _failures.Add("The slot doesn't hold a readable save.");
                }

                // Move on, then load
                SetSpeed(clock, TimeMultiplier);
                yield return RunUntil(clock, savedTick + MoreTicks);
                SetSpeed(clock, 0f);
                long beforeLoad = clock.CurrentTick;
                Press(ui, "btn-quick-load");
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) yield return null; // the header refreshes on a timer
                Check(clock.CurrentTick == savedTick, $"Load brings the saved tick back ({clock.CurrentTick}, saved {savedTick}, was {beforeLoad}).");
                Check(status != null && status.text == $"Loaded the save from tick {savedTick}.", $"Load says what it loaded (got '{status?.text}').");
                Check(spawner.Agents.Count == OutpostSession.DefaultCrawlerCount, $"Every crawler is back ({spawner.Agents.Count}).");
                var tickLabel = ui?.Q<Label>("clock-tick-label");
                Check(tickLabel != null && tickLabel.text.Contains(savedTick.ToString()), $"The header shows the loaded tick (got '{tickLabel?.text}').");
                _notes.Add($"saved t{savedTick}, moved to t{beforeLoad}, loaded t{clock.CurrentTick}");

                // The loaded game runs on
                SetSpeed(clock, TimeMultiplier);
                yield return RunUntil(clock, savedTick + MoreTicks);
                Check(clock.CurrentTick >= savedTick + MoreTicks, "The loaded game runs on.");
            }
            finally
            {
                Object.Destroy(dashboard);
                scope.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private void Press(VisualElement ui, string buttonName)
        {
            var button = ui?.Q<Button>(buttonName);
            Check(button != null, $"The header has {buttonName}.");
            if (button == null) return;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static void SetSpeed(ISimulationClock clock, float multiplier) => clock.SetTimeMultiplier(multiplier);

        private static IEnumerator RunUntil(ISimulationClock clock, long tick)
        {
            float elapsed = 0f;
            while (clock.CurrentTick < tick && elapsed < WallClockTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void Check(bool condition, string message)
        {
            if (!condition) _failures.Add(message);
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Debug.Log($"[QuickSaveButtonsScenario] {string.Join("; ", _notes)}");
            Assert.IsTrue(_failures.Count == 0, "Quick save buttons:\n  " + string.Join("\n  ", _failures));
        }
    }
}
