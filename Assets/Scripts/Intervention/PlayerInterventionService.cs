using System.Collections.Concurrent;

namespace Simpiens.Intervention
{
    /// <summary>
    /// Implements a thread-safe Command pattern queue for player interventions.
    /// Agents or the SimulationManager can ingest these events during their polling cycles.
    /// </summary>
    public class PlayerInterventionService : IPlayerInterventionService
    {
        private readonly ConcurrentQueue<InfluenceEvent> _influenceQueue = new ConcurrentQueue<InfluenceEvent>();

        public void QueueInfluence(InfluenceEvent influence)
        {
            _influenceQueue.Enqueue(influence);
        }

        public bool TryDequeueInfluence(out InfluenceEvent influence)
        {
            return _influenceQueue.TryDequeue(out influence);
        }
    }
}
