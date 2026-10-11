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
    /// Scenario testing dynamic obstacle injection directly in front of moving agents.
    /// Verifies that the progressive unstuck reflex resolves cleanly without persistent jitter loops or clipping.
    /// </summary>
    public class DynamicChaosScenario : ISimulationScenario
    {
        public string Name => "DynamicChaosScenario";
        public float DurationSeconds => 4.0f;
        public float TimeMultiplier => 2.5f;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            // Allow initial navigation to establish
            yield return new WaitForSeconds(1.0f);

            // Inject an unexpected wall along the center corridor pathway
            var injector = resolver.Resolve<EdgeCaseInjector>();
            injector?.InjectWall(new Vector2(0.8f, 0.0f));

            // Allow unstuck reflexes to engage and recover
            yield return new WaitForSeconds(1.0f);

            // Inject a second obstacle in Pen 2 corridor to test cascading clearance
            injector?.InjectWall(new Vector2(-3.2f, 0.0f));
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            // Allow up to 2 transient unstuck reflexes during dynamic wall injection, but zero persistent jitter loops
            Assert.LessOrEqual(detector.JitterCount, 2, 
                $"Excessive jitter detected during dynamic obstacle recovery! Jitter incidents: {detector.JitterCount}");

            Assert.AreEqual(0, detector.PenetrationCount, 
                $"Pawn clipped into dynamically injected wall! Penetrations: {detector.PenetrationCount}");

            Assert.AreEqual(0, detector.BoundaryBreachCount, 
                $"Pawn breached boundary during evasion! Breaches: {detector.BoundaryBreachCount}");
        }
    }
}
