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
    ///
    /// The camera part: a click on a crawler selects it (and its sector); after a second to reach it, the camera keeps
    /// it within <see cref="FollowTolerance"/> for <see cref="FollowFrames"/> frames at 10x; a click on empty floor drops the
    /// crawler and selects the room.
    /// </summary>
    public class ShroudViewScenario : ISimulationScenario
    {
        /// <summary>The scout reports at t357 (OutpostSupplyLoopScenario's bound is 550).</summary>
        public const uint ChartDeadlineTicks = 900;

        /// <summary>Charting to fog gone: the 1.5 s fade plus frame slack.</summary>
        public const float LiftSeconds = 2f;
        private const float WallClockTimeoutSeconds = 150f;

        public const float FollowTolerance = 0.25f;
        public const int FollowFrames = 200;
        /// <summary>Real time the camera gets to reach the crawler before the check starts (frames are short in batch mode).</summary>
        private const float FollowSettleSeconds = 1f;

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
        private string _clickProblem;
        private int _followChecked, _followMisses;
        private float _followWorst;
        private string _followDetail;

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

            // Click the first crawler: it is selected with its sector, and followed close up
            var selection = resolver.Resolve<WorldSelection>();
            var cameraController = Object.FindFirstObjectByType<CameraController>();
            Assert.IsNotNull(cameraController, "The main camera has no CameraController.");
            var crawler = spawner.Agents[0];
            Vector2 at = crawler.transform.position;
            selection.ClickAt(at);
            if (selection.SelectedCrawler != crawler.AgentId || !selection.Following) _clickProblem = $"click on {crawler.name} selected {selection.SelectedCrawler} (following {selection.Following})";
            else if (selection.SelectedSector != sectors.GetSectorAt(at.ToSim()).Id) _clickProblem = $"crawler click selected sector {selection.SelectedSector}, not {sectors.GetSectorAt(at.ToSim()).Id}";
            var rig = cameraController.Rig;
            rig.ZoomAt(rig.Position, 0.01f);

            // The seeded scout charts the Deep Seam on its own
            long start = clock.CurrentTick;
            float elapsed = 0f;
            int checkedFrames = 0;
            float settle = 0f;
            while (!sectors.IsExplored(Floor1Layout.DeepSeamSectorId) && clock.CurrentTick - start < ChartDeadlineTicks && elapsed < WallClockTimeoutSeconds)
            {
                settle += Time.unscaledDeltaTime;
                if (settle >= FollowSettleSeconds && checkedFrames++ < FollowFrames) CheckFollow(crawler.transform.position, rig);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // A click on empty floor in the Garden (away from every crawler) drops the crawler and selects the room
            sectors.TryGetSector(Floor1Layout.GardenSectorId, out var garden);
            selection.ClickAt(EmptyFloor(garden.Bounds.ToUnity(), spawner));
            if (_clickProblem == null && (selection.SelectedCrawler != default || selection.Following || selection.SelectedSector != Floor1Layout.GardenSectorId))
            {
                _clickProblem = $"floor click left crawler {selection.SelectedCrawler} (following {selection.Following}), sector {selection.SelectedSector}";
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

        /// <summary>A point inside the bounds more than a unit from every crawler.</summary>
        private static Vector2 EmptyFloor(Rect bounds, SwarmSpawner spawner)
        {
            var c = bounds.center;
            Vector2[] candidates = { c, c + new Vector2(-4f, 3f), c + new Vector2(4f, 3f), c + new Vector2(-4f, -3f), c + new Vector2(4f, -3f) };
            for (int k = 0; k < candidates.Length; k++)
            {
                bool clear = true;
                var agents = spawner.Agents;
                for (int i = 0; i < agents.Count && clear; i++)
                {
                    clear = agents[i] == null || Vector2.Distance(agents[i].transform.position, candidates[k]) > 1f;
                }
                if (clear) return candidates[k];
            }
            return c;
        }

        /// <summary>The camera centre is on the followed crawler, wherever the map's edge clamp allows it to be.</summary>
        private void CheckFollow(Vector2 crawler, CameraRig rig)
        {
            var frame = rig.Frame;
            float halfW = rig.Size * rig.Aspect, halfH = rig.Size;
            bool centrable = crawler.x >= frame.xMin - CameraRig.Margin + halfW && crawler.x <= frame.xMax + CameraRig.Margin - halfW
                && crawler.y >= frame.yMin - CameraRig.Margin + halfH && crawler.y <= frame.yMax + CameraRig.Margin - halfH;
            if (!centrable) return;
            _followChecked++;
            float d = Vector2.Distance((Vector2)Camera.main.transform.position, crawler);
            if (d > _followWorst) { _followWorst = d; _followDetail = $"rig {rig.Position} size {rig.Size:F2}, camera {Camera.main.transform.position} ({Camera.main.name}, ortho {Camera.main.orthographic}, has controller {Camera.main.GetComponent<CameraController>() != null}), crawler {crawler}"; }
            if (d > FollowTolerance) _followMisses++;
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
                      $"charted@{_chartTick}, fog gone after {_liftSeconds:F2}s, Overgrowth still covered {_overgrowthCoveredAfter}; Dev Map overlays {_devMapOverlays}; " +
                      $"follow: {_followChecked} frames checked, {_followMisses} beyond {FollowTolerance} (worst {_followWorst:F3}: {_followDetail})");

            Assert.IsNull(_layering, $"Fog layering (D1 = A) broken: {_layering}.");
            Assert.IsNull(_clickProblem, $"Map clicks: {_clickProblem}.");
            Assert.GreaterOrEqual(_followChecked, FollowFrames / 2, $"Only {_followChecked} follow frames could be checked.");
            Assert.AreEqual(0, _followMisses, $"The camera lost the followed crawler in {_followMisses} of {_followChecked} frames (worst {_followWorst:F3}).");
            Assert.IsTrue(_deepSeamCoveredAtStart && _overgrowthCoveredAtStart, "Both frontier sectors start under fog.");
            Assert.IsTrue(_chartTick >= 0, $"The Deep Seam was not charted within {ChartDeadlineTicks} ticks.");
            Assert.IsTrue(_liftSeconds >= 0f && _liftSeconds <= LiftSeconds, $"The Deep Seam's fog took {_liftSeconds:F2}s to lift (limit {LiftSeconds}s; -1 = never).");
            Assert.IsTrue(_overgrowthCoveredAfter, "The Overgrowth stays under fog after the Deep Seam is charted.");
            Assert.AreEqual(0, _devMapOverlays, "The Dev Map shows no fog.");
        }
    }
}
