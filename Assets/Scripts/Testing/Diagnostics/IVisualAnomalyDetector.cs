using System;
using System.Collections.Generic;

namespace Simpiens.Testing.Diagnostics
{
    /// <summary>
    /// Interface for querying runtime visual and physics anomalies and registering observation callbacks.
    /// </summary>
    public interface IVisualAnomalyDetector
    {
        int TotalAnomalies { get; }
        int JitterCount { get; }
        int StackingCount { get; }
        int PenetrationCount { get; }
        int ThrashingCount { get; }
        int BoundaryBreachCount { get; }

        event Action<VisualAnomalyRecord> OnAnomalyDetected;

        void ResetMetrics();
        IReadOnlyList<VisualAnomalyRecord> GetRecentAnomalies(int count);
    }
}
