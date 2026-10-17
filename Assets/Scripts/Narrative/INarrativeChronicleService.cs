using System.Collections.Generic;
using UnityEngine;

namespace Simpiens.Narrative
{
    /// <summary>
    /// Event bus interface for publishing story-grade narrative events across the simulation.
    /// Ingested by the narrative chronicle for presentation and player observation.
    /// </summary>
    public interface IChronicleEventBus
    {
        /// <summary>
        /// Records a story beat into the authoritative chronicle buffer.
        /// Thread-safe and zero-allocation.
        /// </summary>
        void RecordEvent(in ChronicleEvent chronicleEvent);
    }

    /// <summary>
    /// Authoritative repository and query service for humanized narrative story beats.
    /// Provides zero-allocation, pre-buffered query APIs ordered newest-first.
    /// </summary>
    public interface INarrativeChronicleService : IChronicleEventBus
    {
        /// <summary>
        /// Total number of events currently retained in the chronicle buffer.
        /// </summary>
        int TotalEventCount { get; }

        /// <summary>
        /// Queries recent events filtered by facility sector, ordered newest-to-oldest.
        /// Each sector maintains an isolated circular buffer (up to 100 events).
        /// Populates the passed resultsBuffer without heap allocations.
        /// </summary>
        void GetSectorEvents(int sectorId, List<ChronicleEvent> resultsBuffer, int maxCount = 100);

        /// <summary>
        /// Queries recent events involving a specific agent as Actor A or Actor B.
        /// Populates the passed resultsBuffer without heap allocations.
        /// </summary>
        void GetActorEvents(EntityId actorId, List<ChronicleEvent> resultsBuffer, int maxCount = 16);

        /// <summary>
        /// Queries the most recent events across the entire facility, ordered newest-to-oldest.
        /// Populates the passed resultsBuffer without heap allocations.
        /// </summary>
        void GetAllRecentEvents(List<ChronicleEvent> resultsBuffer, int maxCount = 32);

        /// <summary>
        /// Clears all retained chronicle events (used for test isolation and resets).
        /// </summary>
        void Clear();

        /// <summary>Writes the event sequence, the facility's history and each sector's into a save (Milestone 1 save/load).</summary>
        void CaptureTo(Simpiens.Simulation.Persistence.ChronicleSave save);

        /// <summary>Replaces the event sequence and every history with a save's (a load).</summary>
        void RestoreFrom(Simpiens.Simulation.Persistence.ChronicleSave save);
    }
}
