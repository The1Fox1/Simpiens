using System.Collections;
using NUnit.Framework;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Sessions;
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
        public string SessionId => CanaryPenSession.SessionId; // 24 pen agents crowd the narrow pens
        public float DurationSeconds => 4.0f;
        public float TimeMultiplier => 2.5f;

        private static readonly Vector2 InjectionOffset = new Vector2(0.8f, 0f);

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            // Allow initial navigation to establish
            yield return new WaitForSeconds(1.0f);

            // Inject an unexpected wall beside the contested bush in Pen 3, where the rivals path
            var map = resolver.Resolve<Simpiens.Core.Session.SwarmSpawner>().World.Map;
            Assert.IsTrue(map.TryGetResourceNode(DevMapLayout.Pen3ContestedBush, out var pen3Bush), "Pen 3 contested bush missing.");
            Assert.IsTrue(map.TryGetResourceNode(DevMapLayout.Pen2ContestedBush, out var pen2Bush), "Pen 2 contested bush missing.");
            var injector = resolver.Resolve<EdgeCaseInjector>();
            injector?.InjectWall(pen3Bush.Position + InjectionOffset);

            // Allow unstuck reflexes to engage and recover
            yield return new WaitForSeconds(1.0f);

            // Inject a second obstacle in Pen 2 to test cascading clearance
            injector?.InjectWall(pen2Bush.Position + InjectionOffset);
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
