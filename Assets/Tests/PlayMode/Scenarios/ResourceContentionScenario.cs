using System.Collections;
using NUnit.Framework;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Sessions;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Scenario testing agents competing for resources and interacting around gathering anchors.
    /// Verifies zero pawn stacking/clumping and zero sprite visual state thrashing.
    /// </summary>
    public class ResourceContentionScenario : ISimulationScenario
    {
        public string Name => "ResourceContentionScenario";
        public string SessionId => CanaryPenSession.SessionId; // 24 pen agents crowd the narrow pens
        public float DurationSeconds => 4.0f;
        public float TimeMultiplier => 2.5f;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            yield return new WaitForSeconds(0.5f);
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.AreEqual(0, detector.StackingCount, 
                $"Pawn stacking/clumping detected during resource contention! Stacking incidents: {detector.StackingCount}");

            Assert.AreEqual(0, detector.ThrashingCount, 
                $"High-frequency visual state thrashing detected! Thrashing incidents: {detector.ThrashingCount}");
        }
    }
}
