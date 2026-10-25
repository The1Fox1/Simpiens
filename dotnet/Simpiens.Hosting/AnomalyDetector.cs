using System;
using System.Collections.Generic;
using System.Text;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Hosting
{
    /// <summary>What <see cref="AnomalyDetector"/> flags.</summary>
    public enum AnomalyType
    {
        None = 0,
        /// <summary>A pawn's heading reverses by more than 120° several times within half a second: it vibrates.</summary>
        OscillationJitter = 1,
        /// <summary>Two pawns that aren't gossiping or sharing stand on top of each other for a second.</summary>
        PawnStacking = 2,
        /// <summary>A pawn's body overlaps a map wall or an injected wall.</summary>
        ObstaclePenetration = 3,
        /// <summary>A pawn's visual state flips four times within half a second: its sprite flickers.</summary>
        VisualStateThrashing = 4,
        /// <summary>A pawn leaves the map's playable bounds.</summary>
        BoundaryBreach = 5,
    }

    /// <summary>One anomaly: when, what, who (and with whom), where, and why.</summary>
    public readonly struct AnomalyRecord
    {
        public AnomalyRecord(long tick, AnomalyType type, EntityId primary, EntityId secondary, Vec2 position, string description)
        {
            Tick = tick;
            Type = type;
            PrimaryAgentId = primary;
            SecondaryAgentId = secondary;
            Position = position;
            Description = description;
        }

        public long Tick { get; }
        public AnomalyType Type { get; }
        public EntityId PrimaryAgentId { get; }
        public EntityId SecondaryAgentId { get; }
        public Vec2 Position { get; }
        public string Description { get; }

        public override string ToString()
        {
            string partner = !SecondaryAgentId.IsEmpty ? $" <=> {SecondaryAgentId}" : string.Empty;
            return $"[{Type}] t{Tick} {PrimaryAgentId}{partner} at ({Position.X:F2}, {Position.Y:F2}): {Description}";
        }
    }

    /// <summary>
    /// The motion and body checks Unity's <c>VisualAnomalyDetector</c> made over its views, moved to plain data (Epic
    /// CLIENT M2.8): jitter, pawn stacking, wall penetration, boundary breaches and visual-state thrashing, judged once
    /// per simulation tick from the authoritative positions and states, so a run is judged the same at any speed. The
    /// thresholds are Unity's, in simulation time. It only reads. One per world; call <see cref="Observe"/> after each
    /// tick. Nothing allocates once warm, except the text of an anomaly when one is found.
    /// <para>
    /// Thrashing is judged on the state a source gives: the simulation's own by default, or what a view shows. The
    /// simulation's state flashes for single ticks between decisions (Idle, Thinking, Idle), which the client holds
    /// back (<c>CrawlerAppearance</c> keeps a state at least 3 ticks); Unity's detector judged the displayed state, so the
    /// client's tests judge thrashing through the client's appearance, and headless runs only report it.
    /// </para>
    /// </summary>
    public sealed class AnomalyDetector
    {
        /// <summary>Jitter is this many heading reversals of more than 120°...</summary>
        public const int JitterReversals = 3;
        /// <summary>...within this many ticks (0.5 s).</summary>
        public const int JitterWindowTicks = 5;
        /// <summary>At most one jitter alert per pawn per this many ticks (1 s).</summary>
        public const int JitterAlertCooldownTicks = 10;
        /// <summary>Logical positions kept per pawn for the jitter alert's description.</summary>
        public const int JitterTrailLength = 10;

        /// <summary>Two pawns closer than this, for <see cref="StackingSeconds"/>, are stacked.</summary>
        public const float StackingDistance = 0.35f;
        public const float StackingSeconds = 1f;

        /// <summary>Thrashing is <see cref="ThrashingSwitches"/> state changes within <see cref="ThrashingSeconds"/>.</summary>
        public const int ThrashingSwitches = 4;
        public const float ThrashingSeconds = 0.5f;

        /// <summary>A body overlaps a wall when a circle of this share of its radius does.</summary>
        public const float BodyProbe = 0.7f;

        /// <summary>The same kind of alert repeats for a pawn at most once a second.</summary>
        public const float AlertCooldownSeconds = 1f;

        /// <summary>Anomalies kept for <see cref="Recent"/>.</summary>
        public const int RecentCapacity = 128;

        private sealed class Pawn
        {
            public Vec2 LastPosition;
            public bool HasPosition;
            public Vec2 LastHeading;
            public bool HasHeading;
            public readonly long[] Reversals = new long[4];
            public int ReversalIndex, ReversalCount;
            public long LastJitterAlert = long.MinValue / 2;
            public readonly Vec2[] Trail = new Vec2[JitterTrailLength];
            public int TrailIndex, TrailCount;

            public AgentVisualState LastState;
            public bool HasState;
            public readonly float[] SwitchTimes = new float[5];
            public readonly AgentVisualState[] SwitchStates = new AgentVisualState[5];
            public int SwitchIndex, SwitchCount;

            public float StackedFor;
            public float LastStackingAlert = -10f, LastPenetrationAlert = -10f, LastThrashingAlert = -10f, LastBreachAlert = -10f;
            public long SeenTick;

            public void RecordReversal(long tick)
            {
                Reversals[ReversalIndex] = tick;
                ReversalIndex = (ReversalIndex + 1) % Reversals.Length;
                if (ReversalCount < Reversals.Length) ReversalCount++;
            }

            public void RecordTrail(Vec2 position)
            {
                Trail[TrailIndex] = position;
                TrailIndex = (TrailIndex + 1) % Trail.Length;
                if (TrailCount < Trail.Length) TrailCount++;
            }

            public void RecordSwitch(float seconds, AgentVisualState state)
            {
                SwitchStates[SwitchIndex] = state;
                SwitchTimes[SwitchIndex] = seconds;
                SwitchIndex = (SwitchIndex + 1) % SwitchTimes.Length;
                if (SwitchCount < SwitchTimes.Length) SwitchCount++;
            }
        }

        private readonly IWorldRegistry _registry;
        private readonly IWorldMap _worldMap;
        private readonly ISpatialPartition _partition;
        private readonly float _tickSeconds;
        private readonly Func<WorldEntity, long, AgentVisualState> _shownState;
        private readonly Dictionary<EntityId, Pawn> _pawns = new Dictionary<EntityId, Pawn>(64);
        private readonly List<EntityId> _gone = new List<EntityId>(8);
        private readonly List<AnomalyRecord> _recent = new List<AnomalyRecord>(RecentCapacity);

        /// <param name="worldMap">The loaded map, for wall penetration and the playable bounds; without one, only injected walls and the partition's bounds are checked.</param>
        /// <param name="tickSeconds">Simulated seconds per tick.</param>
        /// <param name="shownState">The state a crawler shows at a tick, for thrashing; the simulation's own when null. Called once per crawler per tick.</param>
        public AnomalyDetector(IWorldRegistry registry, IWorldMap worldMap = null, ISpatialPartition partition = null, float tickSeconds = 0.1f,
            Func<WorldEntity, long, AgentVisualState> shownState = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _worldMap = worldMap;
            _partition = partition;
            _tickSeconds = tickSeconds;
            _shownState = shownState;
        }

        /// <summary>The detector for a running simulation.</summary>
        public static AnomalyDetector For(LocalSimulation sim, Func<WorldEntity, long, AgentVisualState> shownState = null) =>
            new AnomalyDetector(sim.Registry, (IWorldMap)sim.Services.GetService(typeof(IWorldMap)), (ISpatialPartition)sim.Services.GetService(typeof(ISpatialPartition)),
                shownState: shownState);

        public int TotalAnomalies { get; private set; }
        public int JitterCount { get; private set; }
        public int StackingCount { get; private set; }
        public int PenetrationCount { get; private set; }
        public int ThrashingCount { get; private set; }
        public int BoundaryBreachCount { get; private set; }

        /// <summary>The first anomaly of each run, kept for reports.</summary>
        public AnomalyRecord? First { get; private set; }

        /// <summary>The last <see cref="RecentCapacity"/> anomalies, oldest first.</summary>
        public IReadOnlyList<AnomalyRecord> Recent => _recent;

        public event Action<AnomalyRecord> AnomalyFound;

        /// <summary>Counts and histories start again (a new run, or a respawn: entity ids repeat per seed).</summary>
        public void Reset()
        {
            TotalAnomalies = JitterCount = StackingCount = PenetrationCount = ThrashingCount = BoundaryBreachCount = 0;
            First = null;
            _recent.Clear();
            _pawns.Clear();
        }

        /// <summary>"jitter 0, stacking 1, ..." for reports.</summary>
        public string Summary() =>
            $"{TotalAnomalies} anomalies (jitter {JitterCount}, stacking {StackingCount}, penetration {PenetrationCount}, thrashing {ThrashingCount}, breaches {BoundaryBreachCount})";

        /// <summary>Judges the world as it stands after <paramref name="tick"/>.</summary>
        public void Observe(long tick)
        {
            var entities = _registry.ActiveEntities;
            if (entities == null) return;
            float seconds = tick * _tickSeconds;

            for (int i = 0; i < entities.Count; i++)
            {
                var node = entities[i];
                if (node == null || node.Type != EntityType.Pawn) continue;
                var pawn = PawnOf(node.Id);
                pawn.SeenTick = tick;
                CheckJitter(node, pawn, tick);
                CheckThrashing(node, pawn, tick, seconds);
            }
            CheckStacking(entities, tick, seconds);
            CheckBodies(entities, tick, seconds);
            ForgetGone(tick);
        }

        private Pawn PawnOf(EntityId id)
        {
            if (!_pawns.TryGetValue(id, out var pawn)) _pawns[id] = pawn = new Pawn();
            return pawn;
        }

        private void CheckJitter(WorldEntity node, Pawn pawn, long tick)
        {
            Vec2 position = node.Position;
            pawn.RecordTrail(position);
            if (!pawn.HasPosition)
            {
                pawn.LastPosition = position;
                pawn.HasPosition = true;
                return;
            }

            Vec2 delta = position - pawn.LastPosition;
            if (delta.SqrMagnitude <= 0.0001f) return; // headings only from real movement
            Vec2 heading = delta.Normalized;
            pawn.LastPosition = position;

            if (pawn.HasHeading && Vec2.Dot(heading, pawn.LastHeading) < -0.5f)
            {
                pawn.RecordReversal(tick);
                if (pawn.ReversalCount >= JitterReversals)
                {
                    int oldest = (pawn.ReversalIndex - JitterReversals + pawn.Reversals.Length) % pawn.Reversals.Length;
                    long window = tick - pawn.Reversals[oldest];
                    if (window <= JitterWindowTicks && tick - pawn.LastJitterAlert >= JitterAlertCooldownTicks)
                    {
                        pawn.LastJitterAlert = tick;
                        JitterCount++;
                        Emit(new AnomalyRecord(tick, AnomalyType.OscillationJitter, node.Id, default, position,
                            $"{JitterReversals} reversals in {window} ticks; last positions {DescribeTrail(pawn)}"));
                    }
                }
            }
            pawn.LastHeading = heading;
            pawn.HasHeading = true;
        }

        private void CheckThrashing(WorldEntity node, Pawn pawn, long tick, float seconds)
        {
            var agent = node.Agent;
            if (agent == null) return;
            var state = _shownState != null ? _shownState(node, tick) : agent.VisualState;
            if (!pawn.HasState)
            {
                pawn.LastState = state;
                pawn.HasState = true;
                return;
            }
            if (state == pawn.LastState) return;

            pawn.RecordSwitch(seconds, state);
            pawn.LastState = state;
            if (pawn.SwitchCount < ThrashingSwitches) return;
            int oldest = (pawn.SwitchIndex - ThrashingSwitches + pawn.SwitchTimes.Length) % pawn.SwitchTimes.Length;
            float window = seconds - pawn.SwitchTimes[oldest];
            if (window > ThrashingSeconds || seconds - pawn.LastThrashingAlert < AlertCooldownSeconds) return;
            pawn.LastThrashingAlert = seconds;
            ThrashingCount++;
            Emit(new AnomalyRecord(tick, AnomalyType.VisualStateThrashing, node.Id, default, node.Position,
                $"{ThrashingSwitches} state changes in {window:F2} s: {DescribeSwitches(pawn)}"));
        }

        // A pawn accrues stacked time while closer than StackingDistance to a later pawn in the registry (gossip and
        // sharing exempt), loses it twice as fast otherwise, and is flagged once a second while past StackingSeconds
        private void CheckStacking(IReadOnlyList<WorldEntity> entities, long tick, float seconds)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var a = entities[i];
                if (a == null || a.Type != EntityType.Pawn) continue;
                var pawn = PawnOf(a.Id);
                bool stacked = false;
                for (int j = i + 1; j < entities.Count; j++)
                {
                    var b = entities[j];
                    if (b == null || b.Type != EntityType.Pawn) continue;
                    if (IsSocial(a) || IsSocial(b)) continue;
                    float distance = Vec2.Distance(a.Position, b.Position);
                    if (distance >= StackingDistance) continue;
                    stacked = true;
                    pawn.StackedFor += _tickSeconds;
                    if (pawn.StackedFor >= StackingSeconds && seconds - pawn.LastStackingAlert >= AlertCooldownSeconds)
                    {
                        pawn.LastStackingAlert = seconds;
                        StackingCount++;
                        Emit(new AnomalyRecord(tick, AnomalyType.PawnStacking, a.Id, b.Id, a.Position,
                            $"{a.Name} on {b.Name} ({distance:F2} apart for {pawn.StackedFor:F1} s)"));
                    }
                    break;
                }
                if (!stacked) pawn.StackedFor = SimMath.Max(0f, pawn.StackedFor - _tickSeconds * 2f);
            }
        }

        private static bool IsSocial(WorldEntity pawn) => pawn.Agent != null && (pawn.Agent.IsGossiping || pawn.Agent.IsSharing);

        private void CheckBodies(IReadOnlyList<WorldEntity> entities, long tick, float seconds)
        {
            var map = _worldMap?.Current;
            for (int i = 0; i < entities.Count; i++)
            {
                var node = entities[i];
                if (node == null || node.Type != EntityType.Pawn) continue;
                var pawn = PawnOf(node.Id);
                Vec2 position = node.Position;

                if (IsOutsidePlayArea(map, position) && seconds - pawn.LastBreachAlert >= AlertCooldownSeconds)
                {
                    pawn.LastBreachAlert = seconds;
                    BoundaryBreachCount++;
                    Emit(new AnomalyRecord(tick, AnomalyType.BoundaryBreach, node.Id, default, position, $"{node.Name} left the playable bounds"));
                }

                float probe = node.Radius * BodyProbe;
                string obstacle = null;
                if (map != null && WallRasterizer.CircleOverlapsWall(map, position, probe)) obstacle = "a map wall";
                else
                {
                    for (int w = 0; w < entities.Count; w++)
                    {
                        var wall = entities[w];
                        if (wall == null || wall.Type != EntityType.Wall) continue;
                        float reach = probe + wall.Radius;
                        if ((wall.Position - position).SqrMagnitude < reach * reach) { obstacle = wall.Name; break; }
                    }
                }
                if (obstacle != null && seconds - pawn.LastPenetrationAlert >= AlertCooldownSeconds)
                {
                    pawn.LastPenetrationAlert = seconds;
                    PenetrationCount++;
                    Emit(new AnomalyRecord(tick, AnomalyType.ObstaclePenetration, node.Id, default, position, $"{node.Name} inside {obstacle}"));
                }
            }
        }

        private bool IsOutsidePlayArea(MapDefinition map, Vec2 p)
        {
            var grid = _partition?.StaticGrid;
            if (grid != null && grid.HasPlayableBounds) return Outside(grid.PlayableBounds, p);
            if (map != null) return Outside(map.PlayableBounds, p);
            return SimMath.Abs(p.X) > NavConstants.PlayAreaHalfExtent || SimMath.Abs(p.Y) > NavConstants.PlayAreaHalfExtent;
        }

        private static bool Outside(RectF b, Vec2 p) => p.X < b.XMin || p.X > b.XMax || p.Y < b.YMin || p.Y > b.YMax;

        private void ForgetGone(long tick)
        {
            if (_pawns.Count == 0) return;
            _gone.Clear();
            foreach (var pair in _pawns)
            {
                if (pair.Value.SeenTick != tick) _gone.Add(pair.Key);
            }
            for (int i = 0; i < _gone.Count; i++) _pawns.Remove(_gone[i]);
        }

        private void Emit(AnomalyRecord record)
        {
            TotalAnomalies++;
            First ??= record;
            if (_recent.Count >= RecentCapacity) _recent.RemoveAt(0);
            _recent.Add(record);
            AnomalyFound?.Invoke(record);
        }

        private static string DescribeTrail(Pawn pawn)
        {
            var sb = new StringBuilder();
            int length = pawn.Trail.Length;
            for (int k = pawn.TrailCount; k >= 1; k--)
            {
                var p = pawn.Trail[(pawn.TrailIndex - k + length) % length];
                if (sb.Length > 0) sb.Append(' ');
                sb.Append('(').Append(p.X.ToString("F2")).Append(',').Append(p.Y.ToString("F2")).Append(')');
            }
            return sb.ToString();
        }

        private static string DescribeSwitches(Pawn pawn)
        {
            int length = pawn.SwitchStates.Length;
            var sb = new StringBuilder();
            for (int k = ThrashingSwitches; k >= 1; k--)
            {
                if (sb.Length > 0) sb.Append(" > ");
                sb.Append(pawn.SwitchStates[(pawn.SwitchIndex - k + length) % length]);
            }
            return sb.ToString();
        }
    }
}
