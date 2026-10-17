using System;
using System.Collections.Generic;
using Simpiens.Simulation.Persistence;
using VContainer;

namespace Simpiens.Narrative
{
    /// <summary>
    /// Lightweight, zero-allocation circular buffer encapsulating bounded FIFO array storage.
    /// </summary>
    public sealed class CircularEventBuffer
    {
        private readonly ChronicleEvent[] _buffer;
        private readonly int _capacity;
        private int _head = 0;
        private int _count = 0;

        public int Count => _count;
        public int Capacity => _capacity;

        public CircularEventBuffer(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 100;
            _buffer = new ChronicleEvent[_capacity];
        }

        public void Push(in ChronicleEvent evt)
        {
            _buffer[_head] = evt;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }

        public void GetRecent(List<ChronicleEvent> resultsBuffer, int maxCount)
        {
            if (resultsBuffer == null || maxCount <= 0) return;
            int itemsToTake = SimMath.Min(maxCount, _count);
            for (int i = 0; i < itemsToTake; i++)
            {
                int index = (_head - 1 - i + _capacity * 2) % _capacity;
                resultsBuffer.Add(_buffer[index]);
            }
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _capacity);
        }

        public ChronicleEvent GetAt(int offsetFromHead)
        {
            int index = (_head - 1 - offsetFromHead + _capacity * 2) % _capacity;
            return _buffer[index];
        }

        /// <summary>The events oldest first, as a new array (a save).</summary>
        public ChronicleEvent[] ToArray()
        {
            var events = new ChronicleEvent[_count];
            for (int i = 0; i < _count; i++)
            {
                events[i] = GetAt(_count - 1 - i);
            }
            return events;
        }
    }

    /// <summary>
    /// Thread-safe, bounded chronicle repository.
    /// Provides constant-memory event storage, isolated per-sector circular buffers (up to 100 events),
    /// and zero-allocation query methods.
    /// </summary>
    public class NarrativeChronicleService : INarrativeChronicleService
    {
        public const int DefaultGlobalCapacity = 1024;
        public const int DefaultSectorCapacity = 100;

        private readonly CircularEventBuffer _globalBuffer;
        private readonly Dictionary<int, CircularEventBuffer> _sectorBuffers;
        private readonly int _sectorCapacity;
        private readonly object _syncLock = new object();
        private ulong _sequence = 0;

        public int TotalEventCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _globalBuffer.Count;
                }
            }
        }

        public int SectorCapacity => _sectorCapacity;

        [Inject]
        public NarrativeChronicleService() : this(DefaultGlobalCapacity, DefaultSectorCapacity)
        {
        }

        public NarrativeChronicleService(int capacity) : this(capacity, capacity < DefaultSectorCapacity ? capacity : DefaultSectorCapacity)
        {
        }

        public NarrativeChronicleService(int globalCapacity, int sectorCapacity)
        {
            int gCap = globalCapacity > 0 ? globalCapacity : DefaultGlobalCapacity;
            _sectorCapacity = sectorCapacity > 0 ? sectorCapacity : DefaultSectorCapacity;
            _globalBuffer = new CircularEventBuffer(gCap);
            _sectorBuffers = new Dictionary<int, CircularEventBuffer>(16);
        }

        public void RecordEvent(in ChronicleEvent chronicleEvent)
        {
            lock (_syncLock)
            {
                _sequence++;
                var eventWithId = chronicleEvent.EventId == 0 
                    ? chronicleEvent.WithEventId(_sequence) 
                    : chronicleEvent;

                _globalBuffer.Push(eventWithId);

                if (!_sectorBuffers.TryGetValue(eventWithId.SectorId, out var sectorBuf))
                {
                    sectorBuf = new CircularEventBuffer(_sectorCapacity);
                    _sectorBuffers[eventWithId.SectorId] = sectorBuf;
                }

                sectorBuf.Push(eventWithId);
            }
        }

        public void GetSectorEvents(int sectorId, List<ChronicleEvent> resultsBuffer, int maxCount = 100)
        {
            if (resultsBuffer == null) return;
            resultsBuffer.Clear();
            if (maxCount <= 0) return;

            lock (_syncLock)
            {
                if (_sectorBuffers.TryGetValue(sectorId, out var sectorBuf))
                {
                    sectorBuf.GetRecent(resultsBuffer, maxCount);
                }
            }
        }

        public void GetActorEvents(EntityId actorId, List<ChronicleEvent> resultsBuffer, int maxCount = 16)
        {
            if (resultsBuffer == null) return;
            resultsBuffer.Clear();
            if (maxCount <= 0 || actorId == default) return;

            lock (_syncLock)
            {
                int matched = 0;
                int count = _globalBuffer.Count;
                for (int i = 0; i < count && matched < maxCount; i++)
                {
                    var evt = _globalBuffer.GetAt(i);
                    if (evt.ActorAId.Equals(actorId) || evt.ActorBId.Equals(actorId))
                    {
                        resultsBuffer.Add(evt);
                        matched++;
                    }
                }
            }
        }

        public void GetAllRecentEvents(List<ChronicleEvent> resultsBuffer, int maxCount = 32)
        {
            if (resultsBuffer == null) return;
            resultsBuffer.Clear();
            if (maxCount <= 0) return;

            lock (_syncLock)
            {
                _globalBuffer.GetRecent(resultsBuffer, maxCount);
            }
        }

        public void Clear()
        {
            lock (_syncLock)
            {
                _globalBuffer.Clear();
                foreach (var buf in _sectorBuffers.Values)
                {
                    buf.Clear();
                }
            }
        }

        public void RestoreFrom(ChronicleSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            lock (_syncLock)
            {
                _globalBuffer.Clear();
                _sectorBuffers.Clear();
                if (save.Events != null)
                {
                    for (int i = 0; i < save.Events.Length; i++) _globalBuffer.Push(save.Events[i]);
                }
                if (save.Sectors != null)
                {
                    for (int s = 0; s < save.Sectors.Length; s++)
                    {
                        var record = save.Sectors[s];
                        var buffer = new CircularEventBuffer(_sectorCapacity);
                        for (int i = 0; i < record.Count; i++) buffer.Push(save.SectorEvents[record.Start + i]);
                        _sectorBuffers[record.SectorId] = buffer;
                    }
                }
                _sequence = save.Sequence;
            }
        }

        public void CaptureTo(ChronicleSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            lock (_syncLock)
            {
                save.Sequence = _sequence;
                save.Events = _globalBuffer.ToArray();

                var sectorIds = new List<int>(_sectorBuffers.Count);
                foreach (var kvp in _sectorBuffers)
                {
                    if (kvp.Value.Count > 0) sectorIds.Add(kvp.Key);
                }
                sectorIds.Sort();

                var sectors = new ChronicleSectorRecord[sectorIds.Count];
                var sectorEvents = new List<ChronicleEvent>(sectorIds.Count * _sectorCapacity);
                for (int i = 0; i < sectorIds.Count; i++)
                {
                    var events = _sectorBuffers[sectorIds[i]].ToArray();
                    sectors[i] = new ChronicleSectorRecord { SectorId = sectorIds[i], Start = sectorEvents.Count, Count = events.Length };
                    sectorEvents.AddRange(events);
                }
                save.Sectors = sectors;
                save.SectorEvents = sectorEvents.ToArray();
            }
        }
    }
}
