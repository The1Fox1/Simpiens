using Simpiens.Core.Session;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using Simpiens.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Epic MAP Phase 4 (D2): the header and the Bounty window work over the map. With the full dashboard hidden, the
    /// header keeps updating; the Bounties and Merchant buttons open their windows over the map on the first press; the map stays clickable except
    /// where UI is (the header, an open window), and the full dashboard blocks map clicks while it is shown.
    /// </summary>
    public class MapHudScenario : ISimulationScenario
    {
        private const float WaitSeconds = 3f;

        public string Name => "MapHudScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private readonly List<string> _failures = new List<string>();

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            // Loading the test scene destroys the boot dashboard: build one for this scenario (as SessionSwitchScenario)
            GameObject created = null;
            if (GameObject.Find("[FacilityDashboard]") == null)
            {
                created = Simpiens.Core.Bootstrapping.SimulationStarter.CreateDashboard(resolver);
                yield return null;
            }
            try
            {
                yield return Run(resolver);
            }
            finally
            {
                if (created != null) Object.Destroy(created);
            }
        }

        private IEnumerator Run(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            var dashboard = GameObject.Find("[FacilityDashboard]");
            var presenter = dashboard != null ? dashboard.GetComponent<FacilityDashboardPresenter>() : null;
            var camera = Object.FindFirstObjectByType<CameraController>();
            var doc = presenter != null ? presenter.GetComponent<UIDocument>() : null;
            var ui = doc != null ? doc.rootVisualElement : null;
            Assert.IsNotNull(ui, "The dashboard's UI document is missing.");
            Assert.IsNotNull(camera, "The main camera has no CameraController.");

            bool wasVisible = presenter.IsVisible;
            presenter.IsVisible = false;
            Check(ui.Q<Button>("btn-view-toggle")?.text == "[TAB] DASHBOARD", $"The Tab button leads to the dashboard (got '{ui.Q<Button>("btn-view-toggle")?.text}').");

            // 1. The header keeps updating over the map
            var tickLabel = ui.Q<Label>("clock-tick-label");
            yield return Wait(0.5f);
            string before = tickLabel?.text;
            yield return Wait(1f);
            Check(tickLabel != null && tickLabel.text != before, $"The header's tick stays live over the map ('{before}' then '{tickLabel?.text}').");
            Check(Shown(ui.Q("header-bar")) && !Shown(ui.Q("dashboard-root")), "Over the map: header shown, dashboard panels hidden.");

            // 2. The map is clickable in the middle, not over the header
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var header = new Vector2(Screen.width * 0.5f, Screen.height - 5f); // the header is about 20 px tall at 640x480
            Check(!camera.IsPointerOverUi(centre), "The middle of the map takes clicks.");
            Check(camera.IsPointerOverUi(header), "The header blocks map clicks under it.");

            // 3. The Bounties button opens its window over the map on the first press (it used to need two: the toggle
            //    read the UXML's "display: none" as open), and the window then blocks map clicks
            Press(ui.Q<Button>("btn-bounty-board"));
            yield return Wait(0.2f);
            Check(Shown(ui.Q("bounty-board-overlay")) && !Shown(ui.Q("dashboard-root")), "The bounty board opens over the map.");
            Check(camera.IsPointerOverUi(centre), "An open window blocks map clicks.");
            Press(ui.Q<Button>("btn-close-bounties"));
            yield return Wait(0.2f);
            Check(!Shown(ui.Q("bounty-board-overlay")) && !camera.IsPointerOverUi(centre), "Closing the window gives the map back.");

            Press(ui.Q<Button>("btn-merchant-terminal"));
            yield return Wait(0.2f);
            Check(Shown(ui.Q("merchant-terminal-overlay")), "The merchant terminal opens over the map on the first press.");
            Press(ui.Q<Button>("btn-close-merchant"));
            yield return Wait(0.2f);
            Check(!Shown(ui.Q("merchant-terminal-overlay")), "The merchant terminal closes.");

            // 4. The full dashboard blocks map clicks while it is shown
            presenter.IsVisible = true;
            yield return Wait(0.2f);
            Check(camera.IsPointerOverUi(centre), "The full dashboard blocks map clicks.");
            Check(ui.Q<Button>("btn-view-toggle")?.text == "[TAB] MAP VIEW", "The Tab button leads back to the map.");

            presenter.IsVisible = wasVisible;
        }

        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void Press(Button button)
        {
            if (button == null)
            {
                _failures.Add("a HUD button is missing");
                return;
            }
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static bool Shown(VisualElement element) => element != null && element.resolvedStyle.display != DisplayStyle.None;

        private void Check(bool condition, string message)
        {
            if (!condition) _failures.Add(message);
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.IsTrue(_failures.Count == 0, "Map HUD failures:\n  " + string.Join("\n  ", _failures));
        }
    }
}
