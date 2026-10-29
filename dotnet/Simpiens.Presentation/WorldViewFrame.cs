using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Core.Session;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
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
        /// <summary>A crawler's sector at the end of the tick (for scenes started by entering a zone); -1 for props or with no sectors captured.</summary>
        public int SectorId;
        /// <summary>What a crawler wears in its protection slot (graphics V5); None for none, and for props.</summary>
        public ItemType Worn;
        /// <summary>The first salvage a crawler holds, in id order (graphics V5): what it is seen carrying; None for none.</summary>
        public ItemType Carried;
    }

    /// <summary>
    /// The world as the view draws it, at the end of one tick (Epic CLIENT, review A3): every entity in the world, in
    /// registration order, and which sectors are charted. The views read frames, never the live simulation; a network
    /// client would fill the same structure from replicated state. Its buffers grow once and are reused.
    /// </summary>
    public sealed class WorldViewFrame
    {
        private EntityView[] _entities = new EntityView[128];
        private TradeStallData[] _stalls = new TradeStallData[4];
        private readonly Dictionary<EntityId, int> _index = new Dictionary<EntityId, int>(128);
        private readonly bool[] _charted = new bool[256];
        private readonly List<ChronicleEvent> _events = new List<ChronicleEvent>(8);
        private bool _knowsCharting;

        /// <summary>The tick the frame was captured at.</summary>
        public long Tick { get; private set; }
        /// <summary>Which world it shows: a respawn, a switch or a load starts a new epoch (entity ids repeat between them).</summary>
        public uint Epoch { get; private set; }
        /// <summary>Bit i set when sector i (ids below 64) is charted, as <see cref="ISectorRegistry.ExploredMask"/>.</summary>
        public ulong ChartedMask { get; private set; }
        public int Count { get; private set; }

        /// <summary>The stalls at the end of the tick (graphics V5): plain copies, goods and stock included.</summary>
        public int StallCount { get; private set; }

        public ref readonly TradeStallData Stall(int index) => ref _stalls[index];

        /// <summary>
        /// What the chronicle recorded during this tick (oldest first): crawlers' interactions with each other among them,
        /// which start dynamic scenes (M3 design D9). Empty right after a rebuilt world.
        /// </summary>
        public IReadOnlyList<ChronicleEvent> Events => _events;

        public ref readonly EntityView this[int index] => ref _entities[index];

        public bool TryGetIndex(EntityId id, out int index) => _index.TryGetValue(id, out index);

        /// <summary>Whether the sector was charted; with no charting captured, its default (<see cref="SectorDefinition.StartsExplored"/>).</summary>
        public bool IsCharted(in SectorDefinition sector) => _knowsCharting ? _charted[sector.Id] : sector.StartsExplored;

        /// <summary>Whether the sector with this id was charted (false when no charting was captured).</summary>
        public bool IsCharted(int sectorId) => _knowsCharting && sectorId >= 0 && sectorId < _charted.Length && _charted[sectorId];

        /// <summary>
        /// Copies <paramref name="entities"/>, the charting of <paramref name="sectors"/> (null: unknown) and the tick's
        /// chronicle <paramref name="events"/> at <paramref name="tick"/>.
        /// </summary>
        public void Capture(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick, uint epoch, IReadOnlyList<ChronicleEvent> events = null, IReadOnlyList<TradeStallData> stalls = null)
        {
            StallCount = 0;
            if (stalls != null)
            {
                if (_stalls.Length < stalls.Count) Array.Resize(ref _stalls, Math.Max(stalls.Count, _stalls.Length * 2));
                for (int i = 0; i < stalls.Count; i++) _stalls[StallCount++] = stalls[i];
            }

            Tick = tick;
            Epoch = epoch;
            _events.Clear();
            if (events != null) for (int i = 0; i < events.Count; i++) _events.Add(events[i]);
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
                    SectorId = agent != null && sectors != null ? sectors.GetSectorAt(entity.Position).Id : -1,
                    Worn = agent?.Inventory != null ? Worn(agent.Inventory) : ItemType.None,
                    Carried = agent?.Inventory != null ? agent.Inventory.FirstHeld(ItemCategory.Resource) : ItemType.None,
                };
                _index[entity.Id] = Count;
                Count++;
            }
        }

        private static ItemType Worn(AgentInventory inventory)
        {
            var worn = inventory.GetEquipped(EquipmentSlot.Protection);
            return worn.IsEmpty ? ItemType.None : worn.Type;
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
        private INarrativeChronicleService _chronicle;
        private ISimulationManager _simulation;
        private ulong _lastEventId;
        private readonly List<ChronicleEvent> _recent = new List<ChronicleEvent>(64);
        private readonly List<ChronicleEvent> _newEvents = new List<ChronicleEvent>(8);

        /// <summary>
        /// Raised after each frame is captured, and once after a rebuilt world's: on the simulation's thread, once per
        /// tick, so what reacts to it (dynamic scenes' triggers) sees every tick, whatever the frame rate.
        /// </summary>
        public event Action<WorldViewFrames> Captured;

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
        /// <param name="simulation">Where the stalls come from (graphics V5); none without it.</param>
        public void Follow(SessionHost host, IWorldRegistry registry, ISectorRegistry sectors, ISimulationClock clock, INarrativeChronicleService chronicle = null, ISimulationManager simulation = null)
        {
            Dispose();
            _simulation = simulation;
            _host = host;
            _registry = registry;
            _sectors = sectors;
            _clock = clock;
            _chronicle = chronicle;
            if (_host != null)
            {
                _host.TickCompleted += OnTickCompleted;
                _host.SessionChanged += OnSessionChanged;
            }
            Restart(_registry?.ActiveEntities, _sectors, _clock != null ? _clock.CurrentTick : 0, _simulation?.ActiveStalls);
        }

        /// <summary>The next frame: <see cref="Current"/> becomes <see cref="Previous"/>, with the chronicle's new events.</summary>
        public void Capture(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick, IReadOnlyList<TradeStallData> stalls = null)
        {
            CaptureFrame(entities, sectors, tick, TakeNewEvents(), stalls);
            Captured?.Invoke(this);
        }

        /// <summary>A rebuilt world: a new epoch, and both frames show it, so nothing carries over from the old world (its chronicle's past included).</summary>
        public void Restart(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick, IReadOnlyList<TradeStallData> stalls = null)
        {
            _epoch = unchecked((uint)System.Threading.Interlocked.Increment(ref s_lastEpoch));
            _lastEventId = NewestEventId();
            CaptureFrame(entities, sectors, tick, null, stalls);
            CaptureFrame(entities, sectors, tick, null, stalls);
            Captured?.Invoke(this);
        }

        private void CaptureFrame(IReadOnlyList<WorldEntity> entities, ISectorRegistry sectors, long tick, IReadOnlyList<ChronicleEvent> events, IReadOnlyList<TradeStallData> stalls)
        {
            var next = _previous;
            _previous = _current;
            _current = next;
            _current.Capture(entities, sectors, tick, _epoch, events, stalls);
        }

        /// <summary>The events the chronicle recorded since the last capture, oldest first (it holds the newest first).</summary>
        private List<ChronicleEvent> TakeNewEvents()
        {
            _newEvents.Clear();
            if (_chronicle == null) return _newEvents;
            _chronicle.GetAllRecentEvents(_recent, _recent.Capacity);
            for (int i = _recent.Count - 1; i >= 0; i--)
            {
                if (_recent[i].EventId <= _lastEventId) continue;
                _newEvents.Add(_recent[i]);
                _lastEventId = _recent[i].EventId;
            }
            return _newEvents;
        }

        private ulong NewestEventId()
        {
            if (_chronicle == null) return 0UL;
            _chronicle.GetAllRecentEvents(_recent, 1);
            return _recent.Count > 0 ? _recent[0].EventId : 0UL;
        }

        private void OnTickCompleted(long tick) => Capture(_registry?.ActiveEntities, _sectors, tick, _simulation?.ActiveStalls);

        private void OnSessionChanged(ISessionDefinition session) => Restart(_registry?.ActiveEntities, _sectors, _clock != null ? _clock.CurrentTick : 0, _simulation?.ActiveStalls);

        public void Dispose()
        {
            if (_host == null) return;
            _host.TickCompleted -= OnTickCompleted;
            _host.SessionChanged -= OnSessionChanged;
            _host = null;
        }
    }
}
