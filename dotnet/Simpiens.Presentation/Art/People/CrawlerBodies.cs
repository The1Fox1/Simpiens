using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.People;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// Which body is in which slot of the world sheet's body region (Epic UI-Beta B2, D1–D2), for one texture. A slot
    /// holds a body, not a crawler: the look with what shows of the crawler's gear and standing (<see cref="BodyKey"/>),
    /// so crawlers that look alike share one. Once per update (<see cref="Update"/>) the frame's crawlers keep their
    /// slots, new bodies take free ones and their bakes are queued, and slots no crawler uses are freed. The client then
    /// bakes each queued slot (<see cref="TryTakePending"/>, <see cref="BakeSlot"/>) into its cells of the texture. A crawler
    /// with no look, or past the region's <see cref="BodySlots.Count"/> bodies, is drawn as today's figure. Once its bodies
    /// are baked, an update allocates nothing.
    /// </summary>
    public sealed class CrawlerBodies
    {
        private readonly Dictionary<BodyKey, int> _slotOf = new Dictionary<BodyKey, int>(BodySlots.Count);
        private readonly BodyKey[] _keyIn = new BodyKey[BodySlots.Count];
        private readonly bool[] _held = new bool[BodySlots.Count];
        private readonly bool[] _seen = new bool[BodySlots.Count];
        private readonly int[] _cells = new int[BodySlots.Count * BodyKit.FrameCount]; // world sheet indices, slot by slot
        private readonly Queue<int> _pending = new Queue<int>(BodySlots.Count);
        private readonly bool[] _queued = new bool[BodySlots.Count];

        public CrawlerBodies()
        {
            for (int i = 0; i < _cells.Length; i++) _cells[i] = SpriteSheets.World.IndexOf(BodySlots.All[i]);
        }

        /// <summary>Crawlers with a look that got no slot at the last update, so are drawn as today's figure (D2).</summary>
        public int Overflow { get; private set; }

        /// <summary>Slots holding a body.</summary>
        public int InUse { get; private set; }

        /// <summary>Slots whose bake waits for the client.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>The body <paramref name="crawler"/> is drawn with: its look, its helm if it wears one, its tabard if it's in a guild.</summary>
        public BodyKey KeyOf(in EntityView crawler)
            => new BodyKey(crawler.Look, BodyKit.HeadGearOf(crawler.Worn), crawler.Standing == CrawlerStanding.GuildMember);

        /// <summary>
        /// Moves the table on to <paramref name="frame"/>: bodies its crawlers show keep their slots, new ones take the
        /// lowest free slots in frame order and are queued for baking, and the rest are freed. Null clears nothing.
        /// </summary>
        public void Update(WorldViewFrame frame)
        {
            if (frame == null) return;
            Array.Clear(_seen);
            for (int i = 0; i < frame.Count; i++)
            {
                ref readonly var entity = ref frame[i];
                if (entity.IsCrawler && entity.Look.IsComplete && _slotOf.TryGetValue(KeyOf(entity), out int slot)) _seen[slot] = true;
            }
            // Free what no crawler shows, then seat the new bodies
            for (int slot = 0; slot < BodySlots.Count; slot++)
            {
                if (!_held[slot] || _seen[slot]) continue;
                _slotOf.Remove(_keyIn[slot]);
                _held[slot] = false;
                InUse--;
            }
            int overflow = 0;
            for (int i = 0; i < frame.Count; i++)
            {
                ref readonly var entity = ref frame[i];
                if (!entity.IsCrawler || !entity.Look.IsComplete) continue;
                var key = KeyOf(entity);
                if (_slotOf.ContainsKey(key)) continue;
                int free = Array.IndexOf(_held, false);
                if (free < 0)
                {
                    overflow++;
                    continue;
                }
                _held[free] = true;
                _keyIn[free] = key;
                _slotOf[key] = free;
                InUse++;
                if (!_queued[free])
                {
                    _queued[free] = true;
                    _pending.Enqueue(free);
                }
            }
            Overflow = overflow;
        }

        /// <summary>
        /// The world sheet's sprite for <paramref name="crawler"/> in <paramref name="state"/>'s <paramref name="frame"/>,
        /// when its body has a slot; false draws today's figure.
        /// </summary>
        public bool TryCell(in EntityView crawler, AgentVisualState state, int frame, out int spriteIndex)
        {
            spriteIndex = -1;
            if (!crawler.Look.IsComplete || !_slotOf.TryGetValue(KeyOf(crawler), out int slot)) return false;
            spriteIndex = _cells[slot * BodyKit.FrameCount + BodyKit.FrameIndex(state, frame)];
            return true;
        }

        /// <summary>The slot holding <paramref name="crawler"/>'s body, when it has one.</summary>
        public bool TrySlotOf(in EntityView crawler, out int slot)
        {
            slot = -1;
            return crawler.Look.IsComplete && _slotOf.TryGetValue(KeyOf(crawler), out slot);
        }

        /// <summary>The next slot whose bake the client must write, in the order they were seated.</summary>
        public bool TryTakePending(out int slot)
        {
            while (_pending.TryDequeue(out slot))
            {
                _queued[slot] = false;
                if (_held[slot]) return true; // freed again before the client came: nothing to write
            }
            return false;
        }

        /// <summary>Bakes the body in <paramref name="slot"/> into <paramref name="into"/> (<see cref="BodyKit.PixelsPerBody"/>).</summary>
        public void BakeSlot(int slot, Span<Rgba32> into)
        {
            var key = _keyIn[slot];
            BodyKit.Bake(key.Look, key.HeadGear, key.Tabard, into);
        }

        /// <summary>The world sheet's sprite of <paramref name="slot"/>'s frame <paramref name="frameIndex"/>: where its bake goes.</summary>
        public int Cell(int slot, int frameIndex) => _cells[slot * BodyKit.FrameCount + frameIndex];

        /// <summary>The body in <paramref name="slot"/>, if one is.</summary>
        public bool TryKeyIn(int slot, out BodyKey key)
        {
            key = _keyIn[slot];
            return _held[slot];
        }

        /// <summary>Forgets every body: the texture behind the region is new.</summary>
        public void Clear()
        {
            _slotOf.Clear();
            _pending.Clear();
            Array.Clear(_held);
            Array.Clear(_queued);
            InUse = 0;
            Overflow = 0;
        }
    }

    /// <summary>A body: a look with what shows of a crawler's gear (a helm) and standing (the guild's tabard).</summary>
    public readonly struct BodyKey : IEquatable<BodyKey>
    {
        public BodyKey(in CrawlerLook look, ItemType headGear, bool tabard)
        {
            Look = look;
            HeadGear = headGear;
            Tabard = tabard;
        }

        public CrawlerLook Look { get; }

        /// <summary>What it wears on its head, drawn as a helm; None for nothing.</summary>
        public ItemType HeadGear { get; }

        public bool Tabard { get; }

        public bool Equals(BodyKey other) => Look.Equals(other.Look) && HeadGear == other.HeadGear && Tabard == other.Tabard;

        public override bool Equals(object obj) => obj is BodyKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Look.GetHashCode(), (int)HeadGear, Tabard);
    }
}
