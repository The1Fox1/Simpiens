using System.Collections;
using NUnit.Framework;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Scenario testing agents navigating around static obstacles and screen bounds.
    /// Verifies zero wall clipping, zero boundary escapes, and zero heading jitter.
    /// </summary>
    public class SwarmNavigationScenario : ISimulationScenario
    {
        public string Name => "SwarmNavigationScenario";
        public float DurationSeconds => 4.0f;
        public float TimeMultiplier => 2.5f;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            // Let agents begin pathfinding and moving
            yield return new WaitForSeconds(0.5f);
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.AreEqual(0, detector.PenetrationCount, 
                $"Obstacle penetration detected during navigation! Penetrations: {detector.PenetrationCount}");

            Assert.AreEqual(0, detector.BoundaryBreachCount, 
                $"Agents breached play arena boundaries! Breaches: {detector.BoundaryBreachCount}");

            Assert.AreEqual(0, detector.JitterCount, 
                $"Agents suffered rapid directional jitter/oscillation! Jitter incidents: {detector.JitterCount}");
        }
    }
}
