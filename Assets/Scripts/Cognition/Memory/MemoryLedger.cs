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

        public MemoryEvent GetEvent(int index)
        {
            if (index < 0 || index >= _count)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }
            int actualIndex = (_head - _count + index + _events.Length) % _events.Length;
            return _events[actualIndex];
        }
    }
}
