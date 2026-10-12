using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using System.Collections;
using NUnit.Framework;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.World;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using Simpiens.Testing.Sessions;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Epic MAP Phase 4: the map's fog follows the simulation's charted state. Real outpost session, no injection: both
    /// frontier sectors start under fog; when the seeded scout's report charts the Deep Seam, its fog is gone within
    /// <see cref="LiftSeconds"/> of real time while the Overgrowth's stays; the Dev Map (all charted) shows no fog.
    /// Crawlers draw above the fog and props below it (producer decision D1 = A).
    /// </summary>
    public class ShroudViewScenario : ISimulationScenario
    {
        /// <summary>The scout reports at t357 (OutpostSupplyLoopScenario's bound is 550).</summary>
        public const uint ChartDeadlineTicks = 900;

        /// <summary>Charting to fog gone: the 1.5 s fade plus frame slack.</summary>
        public const float LiftSeconds = 2f;
        private const float WallClockTimeoutSeconds = 150f;

        public string Name => "ShroudViewScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private bool _deepSeamCoveredAtStart, _overgrowthCoveredAtStart;
        private long _chartTick = -1;
        private float _liftSeconds = -1f;
        private bool _overgrowthCoveredAfter;
        private int _devMapOverlays = -1;
        private string _layering;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sectors = resolver.Resolve<ISectorRegistry>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var switcher = resolver.Resolve<ISessionSwitcher>();
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            yield return null;
            var shroud = Object.FindFirstObjectByType<ShroudView>();
            Assert.IsNotNull(shroud, "The outpost map has no ShroudView.");
            _deepSeamCoveredAtStart = shroud.IsCovered(Floor1Layout.DeepSeamSectorId);
            _overgrowthCoveredAtStart = shroud.IsCovered(Floor1Layout.OvergrowthSectorId);
            _layering = CheckLayering();

            // The seeded scout charts the Deep Seam on its own
            long start = clock.CurrentTick;
            float elapsed = 0f;
            while (!sectors.IsExplored(Floor1Layout.DeepSeamSectorId) && clock.CurrentTick - start < ChartDeadlineTicks && elapsed < WallClockTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (sectors.IsExplored(Floor1Layout.DeepSeamSectorId))
            {
                _chartTick = clock.CurrentTick - start;
                float waited = 0f;
                while (shroud.HasOverlay(Floor1Layout.DeepSeamSectorId) && waited < LiftSeconds * 3f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                _liftSeconds = shroud.HasOverlay(Floor1Layout.DeepSeamSectorId) ? -1f : waited;
                _overgrowthCoveredAfter = shroud.IsCovered(Floor1Layout.OvergrowthSectorId);
            }

            // The Dev Map has no uncharted sectors, so no fog
            switcher.SwitchTo(spawner.Find(CanaryPenSession.SessionId));
            yield return null;
            var devShroud = Object.FindFirstObjectByType<ShroudView>();
            _devMapOverlays = devShroud != null ? devShroud.OverlayCount : -1;
            switcher.SwitchTo(spawner.Find(OutpostSession.SessionId));
            yield return null;
        }

        /// <summary>D1 = A: every crawler draws above the fog, every prop (node, bunk, hearth) below it.</summary>
        private static string CheckLayering()
        {
            var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var node = r.GetComponent<NodeController>();
                if (node == null) continue;
                bool crawler = node.Type == Simpiens.Simulation.Spatial.EntityType.Pawn;
                if (crawler && r.sortingOrder <= RenderOrder.Fog) return $"crawler {r.name} at order {r.sortingOrder}";
                if (!crawler && r.sortingOrder >= RenderOrder.Fog) return $"prop {r.name} at order {r.sortingOrder}";
            }
            return null;
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Debug.Log($"[ShroudViewScenario] covered at start: Deep Seam {_deepSeamCoveredAtStart}, Overgrowth {_overgrowthCoveredAtStart}; " +
                      $"charted@{_chartTick}, fog gone after {_liftSeconds:F2}s, Overgrowth still covered {_overgrowthCoveredAfter}; Dev Map overlays {_devMapOverlays}");

            Assert.IsNull(_layering, $"Fog layering (D1 = A) broken: {_layering}.");
            Assert.IsTrue(_deepSeamCoveredAtStart && _overgrowthCoveredAtStart, "Both frontier sectors start under fog.");
            Assert.IsTrue(_chartTick >= 0, $"The Deep Seam was not charted within {ChartDeadlineTicks} ticks.");
            Assert.IsTrue(_liftSeconds >= 0f && _liftSeconds <= LiftSeconds, $"The Deep Seam's fog took {_liftSeconds:F2}s to lift (limit {LiftSeconds}s; -1 = never).");
            Assert.IsTrue(_overgrowthCoveredAfter, "The Overgrowth stays under fog after the Deep Seam is charted.");
            Assert.AreEqual(0, _devMapOverlays, "The Dev Map shows no fog.");
        }
    }
}
