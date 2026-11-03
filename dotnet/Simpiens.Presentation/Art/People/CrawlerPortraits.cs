using System;
using System.Collections.Generic;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// Which portrait is in which slot of a portrait atlas (Epic UI-Beta B3, D7), for one texture. A slot holds a portrait
    /// (<see cref="PortraitKey"/>: a look with its mood, standing and helm); showing one that isn't seated takes a free
    /// slot, or the one shown least recently, and queues its bake. The client bakes the queue into the texture
    /// (<see cref="TryTakePending"/>, <see cref="BakeSlot"/>) as soon as it asks. Showing a seated portrait allocates nothing.
    /// </summary>
    public sealed class CrawlerPortraits
    {
        /// <summary>Portraits an atlas holds at once.</summary>
        public const int Slots = 48;

        private readonly Dictionary<PortraitKey, int> _slotOf = new Dictionary<PortraitKey, int>(Slots);
        private readonly PortraitKey[] _keyIn = new PortraitKey[Slots];
        private readonly bool[] _held = new bool[Slots];
        private readonly long[] _shownAt = new long[Slots];
        private readonly Queue<int> _pending = new Queue<int>(Slots);
        private readonly bool[] _queued = new bool[Slots];
        private long _shows;

        /// <summary>Slots holding a portrait.</summary>
        public int InUse { get; private set; }

        /// <summary>Bakes made since the table was made: one per portrait seated.</summary>
        public int Bakes { get; private set; }

        /// <summary>The slot showing <paramref name="key"/>: seated already, or seated now and queued for baking.</summary>
        public int Show(in PortraitKey key)
        {
            _shows++;
            if (_slotOf.TryGetValue(key, out int slot))
            {
                _shownAt[slot] = _shows;
                return slot;
            }
            slot = Array.IndexOf(_held, false);
            if (slot < 0)
            {
                slot = 0; // the one shown least recently
                for (int i = 1; i < Slots; i++) if (_shownAt[i] < _shownAt[slot]) slot = i;
                _slotOf.Remove(_keyIn[slot]);
            }
            else
            {
                _held[slot] = true;
                InUse++;
            }
            _keyIn[slot] = key;
            _slotOf[key] = slot;
            _shownAt[slot] = _shows;
            Bakes++;
            if (!_queued[slot])
            {
                _queued[slot] = true;
                _pending.Enqueue(slot);
            }
            return slot;
        }

        /// <summary>The slot holding <paramref name="key"/>, if it is seated.</summary>
        public bool TrySlotOf(in PortraitKey key, out int slot) => _slotOf.TryGetValue(key, out slot);

        /// <summary>The portrait in <paramref name="slot"/>, if one is.</summary>
        public bool TryKeyIn(int slot, out PortraitKey key)
        {
            key = _keyIn[slot];
            return _held[slot];
        }

        /// <summary>The next slot whose bake the client must write.</summary>
        public bool TryTakePending(out int slot)
        {
            if (!_pending.TryDequeue(out slot)) return false;
            _queued[slot] = false;
            return true;
        }

        /// <summary>Bakes the framed portrait in <paramref name="slot"/> into <paramref name="into"/> (<see cref="PortraitKit.FramedSize"/> square).</summary>
        public void BakeSlot(int slot, Span<Rgba32> into) => PortraitKit.BakeFramed(_keyIn[slot], into);
    }
}
