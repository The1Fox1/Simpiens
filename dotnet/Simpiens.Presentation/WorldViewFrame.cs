using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>A crawler's passing moments that the view shows over its state (<see cref="CrawlerAppearance.Desired"/>).</summary>
    [Flags]
    public enum CrawlerFlags : byte
    {
        None = 0,
        Tantrum = 1,
        StarvationPanic = 2,
        Yielding = 4,
        Sharing = 8,
        Gossiping = 16,
    }

    /// <summary>One entity as the view draws it at the end of a tick: plain data, with no reference into the simulation.</summary>
    public struct EntityView
    {
        public EntityId Id;
        public EntityType Type;
        public ItemType ResourceKind;
        public Vec2 Position;
        /// <summary>A crawler (it has an <see cref="AgentState"/>); the rest are props.</summary>
        public bool IsCrawler;
        public AgentVisualState VisualState;
        public CrawlerFlags Flags;
    }

    /// <summary>
    /// The world as the view draws it, at the end of one tick (Epic CLIENT, review A3): every entity in the world, in
    /// registration order, and which sectors are charted. The views read frames, never the live simulation; a network
    /// client would fill the same structure from replicated state. Its buffers grow once and are reused.
    /// </summary>
    public sealed class WorldViewFrame
    {
        private EntityView[] _entities = new EntityView[128];
        private readonly Dictionary<EntityId, int> _index = new Dictionary<EntityId, int>(128);
        private readonly bool[] _charted = new bool[256];
        private bool _knowsCharting;

        /// <summary>The tick the frame was captured at.</summary>
        public long Tick { get; private set; }
        /// <summary>Which world it shows: a respawn, a switch or a load starts a new epoch (entity ids repeat between them).</summary>
        public uint Epoch { get; private set; }
        /// <summary>Bit i set when sector i (ids below 64) is charted, as <see cref="ISectorRegistry.ExploredMask"/>.</summary>
        public ulong ChartedMask { get; private set; }
        public int Count { get; private set; }

        public ref readonly EntityView this[int index] => ref _entities[index];

        public bool TryGetIndex(EntityId id, out int index) => _index.TryGetValue(id, out index);

        /// <summary>Whether the sector was charted; with no charting captured, its default (<see cref="SectorDefinition.StartsExplored"/>).</summary>
        public bool IsCharted(in SectorDefinition sector) => _knowsCharting ? _charted[sector.Id] : sector.StartsExplored;

        /// <summary>Whether the sector with this id was charted (false when no charting was captured).</summary>
        public bool IsCharted(int sectorId) => _knowsCharting && sectorId >= 0 && sectorId < _charted.Length && _charted[sectorId];

        /// <summary>Copies <paramref name="entities"/> and the charting of <paramref name="sectors"/> (null: unknown) at <paramref name="tick"/>.</summary>
        public void Capture(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick, uint epoch)
        {
            Tick = tick;
            Epoch = epoch;
            _knowsCharting = sectors != null;
            ChartedMask = sectors != null ? sectors.ExploredMask : 0UL;
            Array.Clear(_charted, 0, _charted.Length);
            if (sectors != null)
            {
                // Only the map's sectors and the fallback can be charted
                var all = sectors.All;
                for (int i = 0; i < all.Count; i++) _charted[all[i].Id] = sectors.IsExplored(all[i].Id);
                _charted[sectors.Fallback.Id] = sectors.IsExplored(sectors.Fallback.Id);
            }

            _index.Clear();
            Count = 0;
            if (entities == null) return;
            if (_entities.Length < entities.Count) Array.Resize(ref _entities, Math.Max(entities.Count, _entities.Length * 2));
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity == null) continue;
                var agent = entity.Agent;
                _entities[Count] = new EntityView
                {
                    Id = entity.Id,
                    Type = entity.Type,
                    ResourceKind = entity.ResourceKind,
                    Position = entity.Position,
                    IsCrawler = agent != null,
                    VisualState = agent != null ? agent.VisualState : AgentVisualState.Idle,
                    Flags = agent != null ? FlagsOf(agent) : CrawlerFlags.None,
                };
                _index[entity.Id] = Count;
                Count++;
            }
        }

        /// <summary>A crawler's passing moments, as a frame captures them.</summary>
        public static CrawlerFlags FlagsOf(AgentState agent)
        {
            var flags = CrawlerFlags.None;
            if (agent.IsInTantrum) flags |= CrawlerFlags.Tantrum;
            if (agent.IsStarvationPanicking) flags |= CrawlerFlags.StarvationPanic;
            if (agent.IsYielding) flags |= CrawlerFlags.Yielding;
            if (agent.IsSharing) flags |= CrawlerFlags.Sharing;
            if (agent.IsGossiping) flags |= CrawlerFlags.Gossiping;
            return flags;
        }
    }

    /// <summary>
    /// The view's last two frames (review A3): captured when a tick completes, restarted when the world is rebuilt.
    /// Crawlers are drawn between them at <see cref="Alpha"/>, so they move smoothly between 10 Hz ticks; the picture
    /// runs one tick behind the simulation, the usual price of interpolation. Captures happen on the simulation's
    /// thread; if the simulation moves to its own thread, two buffers become three behind one swap and no reader changes.
    /// </summary>
    public sealed class WorldViewFrames : IDisposable
    {
        private WorldViewFrame _previous = new WorldViewFrame();
        private WorldViewFrame _current = new WorldViewFrame();
        private uint _epoch;
        private SessionHost _host;
        private IWorldRegistry _registry;
        private ISectorRegistry _sectors;
        private ISimulationClock _clock;

        /// <summary>The frame of the tick before <see cref="Current"/> (the same frame right after a rebuild).</summary>
        public WorldViewFrame Previous => _previous;
        /// <summary>The frame of the last completed tick.</summary>
        public WorldViewFrame Current => _current;

        /// <summary>
        /// Which world the frames show (review A5): every respawn, switch or load starts a new one. Unique in the process,
        /// so a view cache keyed on it never mistakes another world (or another frames object) for its own; 0 before
        /// the first capture. View caches compare it instead of guessing from the tick or the map.
        /// </summary>
        public uint Epoch => _epoch;

        private static int s_lastEpoch;

        /// <summary>A move longer than this (world units) in one tick isn't drawn as a glide: a teleport, a load.</summary>
        public const float SnapDistance = 2f;

        private float _alpha = 1f;

        /// <summary>How far the clock is from <see cref="Previous"/> to <see cref="Current"/>: 0 draws the one, 1 the other.</summary>
        public float Alpha
        {
            get => _alpha;
            set => _alpha = SimMath.Clamp01(value);
        }

        /// <summary>
        /// Where the entity at <paramref name="currentIndex"/> in <see cref="Current"/> is drawn this frame: between its
        /// two last positions at <see cref="Alpha"/> when both frames show it in the same world and it moved less than
        /// <see cref="SnapDistance"/>; otherwise where it is now.
        /// </summary>
        public Vec2 DrawnPosition(int currentIndex)
        {
            ref readonly var now = ref _current[currentIndex];
            if (_alpha >= 1f || _previous.Epoch != _current.Epoch || !_previous.TryGetIndex(now.Id, out int before)) return now.Position;
            var from = _previous[before].Position;
            if ((now.Position - from).SqrMagnitude > SnapDistance * SnapDistance) return now.Position;
            return Vec2.Lerp(from, now.Position, _alpha);
        }

        /// <summary>Where an entity is drawn this frame; false when the world doesn't have it.</summary>
        public bool TryGetDrawnPosition(EntityId id, out Vec2 position)
        {
            if (_current.TryGetIndex(id, out int index))
            {
                position = DrawnPosition(index);
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>
        /// Captures the session's world from now on: after every completed tick, and afresh (a new epoch) on every
        /// respawn, switch or load. Captures once at once.
        /// </summary>
        public void Follow(SessionHost host, IWorldRegistry registry, ISectorRegistry sectors, ISimulationClock clock)
        {
            Dispose();
            _host = host;
            _registry = registry;
            _sectors = sectors;
            _clock = clock;
            if (_host != null)
            {
                _host.TickCompleted += OnTickCompleted;
                _host.SessionChanged += OnSessionChanged;
            }
            Restart(_registry?.ActiveEntities, _sectors, _clock != null ? _clock.CurrentTick : 0);
        }

        /// <summary>The next frame: <see cref="Current"/> becomes <see cref="Previous"/>.</summary>
        public void Capture(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick)
        {
            var next = _previous;
            _previous = _current;
            _current = next;
            _current.Capture(entities, sectors, tick, _epoch);
        }

        /// <summary>A rebuilt world: a new epoch, and both frames show it, so nothing carries over from the old world.</summary>
        public void Restart(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick)
        {
            _epoch = unchecked((uint)System.Threading.Interlocked.Increment(ref s_lastEpoch));
            Capture(entities, sectors, tick);
            Capture(entities, sectors, tick);
        }

        private void OnTickCompleted(long tick) => Capture(_registry?.ActiveEntities, _sectors, tick);

        private void OnSessionChanged(ISessionDefinition session) => Restart(_registry?.ActiveEntities, _sectors, _clock != null ? _clock.CurrentTick : 0);

        public void Dispose()
        {
            if (_host == null) return;
            _host.TickCompleted -= OnTickCompleted;
            _host.SessionChanged -= OnSessionChanged;
            _host = null;
        }
    }
}
