namespace Simpiens.Cognition.Memory
{
    public class MemoryLedger
    {
        private readonly MemoryEvent[] _events;
        private int _head;
        private int _count;

        public MemoryLedger(int capacity = 50)
        {
            _events = new MemoryEvent[capacity];
            _head = 0;
            _count = 0;
        }

        public void AddEvent(in MemoryEvent memoryEvent)
        {
            _events[_head] = memoryEvent;
            _head = (_head + 1) % _events.Length;
            if (_count < _events.Length)
            {
                _count++;
            }
        }
        
        public int Count => _count;
        public int Capacity => _events.Length;

        /// <summary>Replaces the events with saved ones, oldest first; beyond the capacity, the newest are kept.</summary>
        public void Restore(MemoryEvent[] events)
        {
            System.Array.Clear(_events, 0, _events.Length);
            _head = 0;
            _count = 0;
            if (events == null) return;
            for (int i = 0; i < events.Length; i++) AddEvent(events[i]);
        }

        /// <summary>The events oldest first, as a new array (a save).</summary>
        public MemoryEvent[] ToArray()
        {
            var events = new MemoryEvent[_count];
            for (int i = 0; i < _count; i++)
            {
                events[i] = _events[(_head - _count + i + _events.Length) % _events.Length];
            }
            return events;
        }

        public MemoryEvent GetEvent(int index)
        {
            if (index < 0 || index >= _count)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }
            int actualIndex = (_head - _count + index + _events.Length) % _events.Length;
            return _events[actualIndex];
        }

        public void GetRecentEvents(uint currentTick, uint maxAgeTicks, System.Collections.Generic.List<MemoryEvent> results)
        {
            for (int i = 0; i < _count; i++)
            {
                int actualIndex = (_head - _count + i + _events.Length) % _events.Length;
                var evt = _events[actualIndex];
                if (currentTick >= evt.SimulationTick && (currentTick - evt.SimulationTick) <= maxAgeTicks)
                {
                    results.Add(evt);
                }
            }
        }
    }
}
