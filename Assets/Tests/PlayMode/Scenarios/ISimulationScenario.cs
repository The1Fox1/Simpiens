using System.Collections;
using Simpiens.Testing.Diagnostics;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Contract for parameterized simulation scenarios executed within the PlayMode test harness.
    /// </summary>
    public interface ISimulationScenario
    {
        string Name { get; }

        /// <summary>Id of the session the harness respawns before the scenario (OutpostSession, CanaryPenSession).</summary>
        string SessionId { get; }
        float DurationSeconds { get; }
        float TimeMultiplier { get; }

        IEnumerator SetupAsync(IObjectResolver resolver);
        void AssertResults(IVisualAnomalyDetector detector);
    }
}
