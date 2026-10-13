using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using Simpiens.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Epic MAP Phase 4 Definition-of-Done scenario for the player's view: on the real outpost session, the player
    /// opens the bounty board over the map, picks Scout, finds exactly the uncharted rooms offered by name, picks the
    /// Overgrowth and posts it at the board's default reward. Nothing else is injected. Within
    /// <see cref="ChartDeadlineTicks"/> a crawler claims it, scouts the room and reports back, the Overgrowth is
    /// charted and its fog lifts on the map. With both frontier rooms charted, Scout has nothing left to offer.
    /// </summary>
    public class PlayerScoutScenario : ISimulationScenario
    {
        /// <summary>Overgrowth charted by now: t344 in the full suite (t284 alone), x 1.5.</summary>
        public const uint ChartDeadlineTicks = 520;
        public const float LiftSeconds = 2f;
        private const float WallClockTimeoutSeconds = 240f;

        public string Name => "PlayerScoutScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private readonly List<string> _failures = new List<string>();
        private long _claimTick = -1, _chartTick = -1;
        private float _liftSeconds = -1f;
        private ushort _reward;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
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
            var sim = resolver.Resolve<ISimulationManager>();
            var sectors = resolver.Resolve<ISectorRegistry>();
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            var presenter = GameObject.Find("[FacilityDashboard]").GetComponent<FacilityDashboardPresenter>();
            var ui = presenter.GetComponent<UIDocument>().rootVisualElement;
            presenter.IsVisible = false; // over the map
            yield return null;

            // 1. The player opens the board, picks Scout: exactly the uncharted rooms are offered
            Press(ui.Q<Button>("btn-bounty-board"));
            yield return null;
            ui.Q<DropdownField>("dropdown-bounty-type").value = "ScoutSector";
            yield return null;
            var offered = new List<int>(presenter.BountySectorChoiceIds);
            var uncharted = new List<int>();
            for (int i = 0; i < sectors.All.Count; i++)
            {
                if (!sectors.IsExplored(sectors.All[i].Id)) uncharted.Add(sectors.All[i].Id);
            }
            Check(SameSet(offered, uncharted), $"Scout offers the uncharted rooms ({string.Join(",", uncharted)}), got ({string.Join(",", offered)}).");
            var sectorPicker = ui.Q<DropdownField>("dropdown-bounty-sector");
            Check(sectorPicker != null && sectorPicker.choices.Contains("Overgrowth"), "The picker lists the Overgrowth by name.");

            // 2. Pick the Overgrowth and post at the default reward
            Check(presenter.SelectBountySector(Floor1Layout.OvergrowthSectorId), "The Overgrowth can be picked.");
            var rewardSlider = ui.Q<SliderInt>("slider-bounty-reward");
            _reward = (ushort)(rewardSlider != null ? rewardSlider.value : 0);
            Press(ui.Q<Button>("btn-post-bounty"));
            Press(ui.Q<Button>("btn-close-bounties"));

            // 3. A crawler claims it, scouts and reports; the room is charted and its fog lifts
            long start = clock.CurrentTick;
            float elapsed = 0f;
            ushort bountyId = 0;
            while (clock.CurrentTick - start < ChartDeadlineTicks && elapsed < WallClockTimeoutSeconds)
            {
                if (bountyId == 0) bountyId = FindBounty(sim, Floor1Layout.OvergrowthSectorId);
                if (bountyId != 0 && _claimTick < 0 && sim.TryGetBounty(bountyId, out var b) && b.Status != BountyStatus.Open) _claimTick = clock.CurrentTick - start;
                if (sectors.IsExplored(Floor1Layout.OvergrowthSectorId)) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Check(bountyId != 0, "The posted Scout Overgrowth bounty reached the simulation.");
            if (sectors.IsExplored(Floor1Layout.OvergrowthSectorId))
            {
                _chartTick = clock.CurrentTick - start;
                var shroud = Object.FindFirstObjectByType<ShroudView>();
                float waited = 0f;
                while (shroud != null && shroud.HasOverlay(Floor1Layout.OvergrowthSectorId) && waited < LiftSeconds * 3f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                _liftSeconds = shroud != null && !shroud.HasOverlay(Floor1Layout.OvergrowthSectorId) ? waited : -1f;
            }

            // 4. With the Deep Seam charted too (the seeded scout), Scout has nothing left to offer
            float more = 0f;
            while (!sectors.IsExplored(Floor1Layout.DeepSeamSectorId) && more < 30f)
            {
                more += Time.unscaledDeltaTime;
                yield return null;
            }
            Press(ui.Q<Button>("btn-bounty-board"));
            yield return null;
            Check(presenter.BountySectorChoiceIds.Count == 0, $"With every room charted Scout offers nothing (got {presenter.BountySectorChoiceIds.Count}).");
            Check(ui.Q<Button>("btn-post-bounty") != null && !ui.Q<Button>("btn-post-bounty").enabledSelf, "Post is disabled when there is nothing to scout.");
            Check(ui.Q<Label>("bounty-feedback-label")?.text == FacilityDashboardPresenter.AllChartedText, $"The board says why (got '{ui.Q<Label>("bounty-feedback-label")?.text}').");
            Press(ui.Q<Button>("btn-close-bounties"));
        }

        private static ushort FindBounty(ISimulationManager sim, byte sectorId)
        {
            var bounties = sim.ActiveBounties;
            for (int i = 0; i < bounties.Count; i++)
            {
                if (bounties[i].Type == BountyType.ScoutSector && bounties[i].TargetSectorId == sectorId) return bounties[i].BountyId;
            }
            return 0;
        }

        private static bool SameSet(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!b.Contains(a[i])) return false;
            }
            return true;
        }

        private void Press(Button button)
        {
            if (button == null)
            {
                _failures.Add("a bounty board button is missing");
                return;
            }
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private void Check(bool condition, string message)
        {
            if (!condition) _failures.Add(message);
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Debug.Log($"[PlayerScoutScenario] posted Scout Overgrowth for {_reward}g; claimed@{_claimTick}, charted@{_chartTick}, fog gone after {_liftSeconds:F2}s");
            Assert.IsTrue(_chartTick >= 0, $"The Overgrowth was not charted within {ChartDeadlineTicks} ticks (claimed at {_claimTick}).");
            Assert.IsTrue(_liftSeconds >= 0f && _liftSeconds <= LiftSeconds, $"The Overgrowth's fog took {_liftSeconds:F2}s to lift (limit {LiftSeconds}s; -1 = never).");
            Assert.IsTrue(_failures.Count == 0, "Player scout failures:\n  " + string.Join("\n  ", _failures));
        }
    }
}
