using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Spatial;
using ZLogger;

namespace Simpiens.Simulation.Movement
{
    internal sealed class Locomotion
    {
        // Sidesteps for a panicking pawn whose way is blocked: 45 degrees either side, then 90, as (cos, sin) pairs
        private static readonly Vec2[] SidestepTurns =
        {
            new Vec2(0.70710677f, 0.70710677f), new Vec2(0.70710677f, -0.70710677f), new Vec2(0f, 1f), new Vec2(0f, -1f)
        };

        // A walker's way around a pawn: 45 degrees to the right first (keep right), then to the left; both gain ground
        private static readonly Vec2[] FlowTurns =
        {
            new Vec2(0.70710677f, -0.70710677f), new Vec2(0.70710677f, 0.70710677f)
        };

        // Snapshot positions are one tick old and every pawn moves at most ~0.3m per tick, so widen the candidate search
        private const float CrowdSearchMargin = 0.5f;

        private readonly ISpatialPartition _spatialPartition;
        private readonly IWorldRegistry _worldRegistry;
        private readonly ISimulationClock _clock;

        public Locomotion(ISpatialPartition spatialPartition = null, IWorldRegistry worldRegistry = null, ISimulationClock clock = null)
        {
            _spatialPartition = spatialPartition;
            _worldRegistry = worldRegistry;
            _clock = clock;
        }

        public IntentResult ExecutePanicMovement(WorldEntity node, PathResponse path, ref int currentIndex)
        {
            if (path == null || path.Waypoints == null || currentIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end or empty
            }

            // A waypoint out of sight behind a wall, once the one before it was skipped in a crowd, is skipped too: sliding
            // along the wall toward it turned back and forth every tick (Epic 10 G5, finding F5)
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            if (snapshot != null && snapshot.IsStaticWalkable(node.Position))
            {
                while (currentIndex < path.Length && !snapshot.IsSegmentStaticClear(node.Position, path.Waypoints[currentIndex])) currentIndex++;
                if (currentIndex >= path.Length) return IntentResult.Success;
            }

            Vec2 targetPos = path.Waypoints[currentIndex];

            // Ignore occupied waypoints to physically break free from any deadlock, but never move into walls or onto pawns
            float speed = (node != null ? node.BaseSpeed : 2f) * 1.5f;
            float step = (float)(speed * SimulationManager.FixedTickDeltaTime);
            if (!TryDisplacePawn(node, Vec2.MoveTowards(node.Position, targetPos, step)))
            {
                // Skip a blocked waypoint instead of grinding against the obstacle. One within reach is skipped without a
                // sidestep: stepping round the pawn on it went past it and back (F5)
                if (Vec2.Distance(node.Position, targetPos) <= NavConstants.ArrivalTolerance || !TryStep(node, targetPos, step, SidestepTurns))
                {
                    currentIndex++;
                    return currentIndex >= path.Length ? IntentResult.Success : IntentResult.InProgress;
                }
            }

            // Check if reached the current waypoint
            if (Vec2.Distance(node.Position, targetPos) < 0.05f)
            {
                currentIndex++;
                // If this was the last waypoint, return true indicating completion
                if (currentIndex >= path.Length)
                {
                    return IntentResult.Success;
                }
            }

            return IntentResult.InProgress;
        }

        /// <summary>
        /// Steps a pawn around whatever blocks its straight step toward <paramref name="targetPos"/>, trying the turns
        /// in order, each a (cos, sin) rotation of the heading; walls and pawn spacing are respected. A panicking crawler
        /// uses <see cref="SidestepTurns"/>: without it, in a crowd it skipped every waypoint and panicked again on the
        /// spot, starving there (Epic 10 G0, finding F2). A walker uses <see cref="FlowTurns"/>.
        /// </summary>
        private bool TryStep(WorldEntity node, Vec2 targetPos, float step, Vec2[] turns)
        {
            Vec2 toTarget = targetPos - (Vec2)node.Position;
            float dist = toTarget.Magnitude;
            if (dist < 0.001f) return false;
            Vec2 dir = toTarget / dist;
            for (int i = 0; i < turns.Length; i++)
            {
                Vec2 turn = turns[i];
                // Vec2 operators, not a scalar chain: each rounds when it stores, so Unity's Mono (which keeps a chain's
                // intermediates at double precision) and CoreCLR turn the same way (Epic CORE F1)
                Vec2 turned = dir * turn.X + new Vec2(-dir.Y, dir.X) * turn.Y;
                if (TryDisplacePawn(node, (Vec2)node.Position + turned * step)) return true;
            }
            return false;
        }

        public IntentResult ExecutePathMovement(WorldEntity node, PathResponse path, ActiveIntentState state)
        {
            if (path == null || path.Waypoints == null || state.CurrentWaypointIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end or empty
            }

            Vec2 targetPos = path.Waypoints[state.CurrentWaypointIndex];

            // 0. A waypoint within reach whose next leg turns back counts as passed when the way to the next is clear: a
            // crawler pushed past a corner walked onto it and turned back, which reads as jitter (Epic 10 G5, finding F5)
            if (state.CurrentWaypointIndex < path.Length - 1 && Vec2.Distance(node.Position, targetPos) <= NavConstants.ArrivalTolerance
                && IsTurnBack(node.Position, targetPos, path.Waypoints[state.CurrentWaypointIndex + 1]))
            {
                state.CurrentWaypointIndex++;
                targetPos = path.Waypoints[state.CurrentWaypointIndex];
            }

            // 1. Motor Watchdog Progress Tracking
            float distMoved = Vec2.Distance(node.Position, state.LastSampledPosition);
            if (distMoved >= 0.05f)
            {
                state.LastSampledPosition = node.Position;
                state.TimeSinceLastProgress = 0f;
                state.StallCount = 0;
                state.UseRelaxedClearance = false;
            }
            else
            {
                state.TimeSinceLastProgress += SimulationManager.FixedTickDeltaTime;
                if (state.TimeSinceLastProgress >= 1.0f)
                {
                    state.StallCount++;
                    state.TimeSinceLastProgress = 0f;

                    var reflexResult = ApplyProgressiveUnstuckReflex(node, targetPos, state);
                    if (reflexResult != IntentResult.InProgress)
                    {
                        return reflexResult;
                    }
                }
            }

            bool isFinalWaypoint = state.CurrentWaypointIndex == path.Length - 1;
            bool nearWaypoint = Vec2.Distance(node.Position, targetPos) <= NavConstants.ArrivalTolerance;
            bool nearFinal = isFinalWaypoint && nearWaypoint;

            // 2. Path Invalidation: Check if next step is blocked. Only near the waypoint: a smoothed path's next
            // waypoint is often the destination, and a pawn standing there froze a crawler across the map (Epic 10 G0,
            // finding F2); further away, IsCrowdedMove below keeps pawns apart
            float clearance = state.UseRelaxedClearance ? 0.2f : 0.4f;
            if (nearWaypoint && IsPositionBlocked(targetPos, node.Id, clearance))
            {
                // Someone already stands on our destination and we're close: arrive here rather than stack on them
                if (nearFinal) return IntentResult.Success;

                // Do not advance position into the obstructed target this tick.
                // Stationary state will accumulate TimeSinceLastProgress in Section 1.
                // If blocked continuously for >= 1.0s, the progressive watchdog reflex will handle unsticking cleanly.
                return IntentResult.InProgress;
            }

            // 3. Move the pawn visually and logically, keeping clear of walls and other pawns; a downed crawler limps
            float speed = node != null ? node.BaseSpeed : 2f;
            if (node?.Agent != null && node.Agent.IsDowned) speed = (float)(speed * Vitality.DownedSpeedFactor);
            float step = (float)(speed * SimulationManager.FixedTickDeltaTime);
            Vec2 candidate = Vec2.MoveTowards(node.Position, targetPos, step);
            if (IsCrowdedMove(node, candidate))
            {
                if (nearFinal) return IntentResult.Success;
                // Flow around: a step 45 degrees to the right, else to the left, still gains on the waypoint. Two crawlers
                // meeting head-on in a doorway both keep right and pass instead of standing nose to nose (Epic 10 G1:
                // a jam in the Garden door starved a crawler); the watchdog resolves what this can't
                if (!TryStep(node, targetPos, step, FlowTurns)) return IntentResult.InProgress;
            }
            else if (!TryDisplacePawn(node, candidate, respectPawns: false))
            {
                return IntentResult.InProgress; // static wall ahead; watchdog takes over
            }

            // Check if reached the current waypoint
            if (Vec2.Distance(node.Position, targetPos) < 0.05f)
            {
                state.CurrentWaypointIndex++;
                state.LastSampledPosition = node.Position;
                state.TimeSinceLastProgress = 0f;
                state.StallCount = 0;
                state.UseRelaxedClearance = false;

                // If this was the last waypoint, return true indicating completion
                if (state.CurrentWaypointIndex >= path.Length)
                {
                    return IntentResult.Success;
                }
            }

            return IntentResult.InProgress;
        }

        /// <summary>
        /// True when walking on to <paramref name="waypoint"/> means turning back toward <paramref name="next"/> (more than a
        /// right angle), and the leg from <paramref name="position"/> straight to <paramref name="next"/> passes the path
        /// smoothing's checks: walls, sight and props.
        /// </summary>
        private bool IsTurnBack(Vec2 position, Vec2 waypoint, Vec2 next)
        {
            if (Vec2.Dot(waypoint - position, next - waypoint) >= 0f) return false;
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            return snapshot != null && snapshot.IsStaticWalkable(position) && snapshot.IsSegmentStaticClear(position, next)
                && snapshot.HasLineOfSight(position, next) && snapshot.IsSegmentPropClear(position, next, GridAStarSearcher.PropClearance);
        }

        public IntentResult ApplyProgressiveUnstuckReflex(WorldEntity node, Vec2 targetPos, ActiveIntentState state)
        {
            Vec2 toTarget = targetPos - (Vec2)node.Position;
            float dist = toTarget.Magnitude;
            Vec2 dir = dist > 0.001f ? toTarget / dist : Vec2.Up;

            if (state.StallCount <= 2)
            {
                // Tier 1: Local Clearance Nudge (Orthogonal shift to break symmetrical collider face-offs)
                // StallCount 1: shifts +0.2f along perpendicular.
                // StallCount 2: shifts -0.4f to test the opposite flank (-0.2f relative to original centerline).
                float displacement = (state.StallCount == 1) ? 0.2f : -0.4f;
                Vec2 perpendicular = new Vec2(-dir.Y, dir.X) * displacement;
                if (SimLog.Traces(SimLog.Navigation, Microsoft.Extensions.Logging.LogLevel.Debug, node.Id))
                {
                    SimLog.Navigation.ZLogDebug($"Watchdog tier 1 ({state.StallCount}): {SimLog.Agent(node.Id)} stalled at {node.Position} toward {targetPos}; nudging {perpendicular}");
                }
                // Wall-checked: try the chosen flank, then the mirrored one, else stay put and escalate next stall
                if (!TryDisplacePawn(node, node.Position + perpendicular))
                {
                    TryDisplacePawn(node, (Vec2)node.Position - perpendicular);
                }
                state.LastSampledPosition = node.Position;
                return IntentResult.InProgress;
            }
            else if (state.StallCount == 3)
            {
                // Tier 2: Relaxed Clearance Repath
                state.UseRelaxedClearance = true;
                if (SimLog.Traces(SimLog.Navigation, Microsoft.Extensions.Logging.LogLevel.Debug, node.Id))
                {
                    SimLog.Navigation.ZLogDebug($"Watchdog tier 2: {SimLog.Agent(node.Id)} stalled at {node.Position}; relaxing pawn clearance");
                }
                return IntentResult.InProgress;
            }
            else
            {
                // Tier 3: Target Disengage & Blacklist Back-Off (StallCount >= 4)
                SimLog.Navigation.ZLogDebug($"Watchdog tier 3: {SimLog.Agent(node.Id)} disengaging from {targetPos} at {node.Position} ({state.Intent?.GetType().Name}); backing off 0.5m");
                TryDisplacePawn(node, (Vec2)node.Position - (dir * 0.5f));

                if (state.Intent is HarvestResourceIntent harvest)
                {
                    if (node.Agent != null && node.Agent.Memory != null)
                    {
                        uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;
                        node.Agent.Memory.BlacklistEntity(harvest.TargetEntityId, currentTick + 300);
                    }
                }

                return IntentResult.PathBlocked;
            }
        }

        /// <summary>
        /// Moves a pawn to <paramref name="candidate"/> only if the straight move stays in statically walkable cells
        /// (exact segment traversal, no corner cutting) and, unless <paramref name="respectPawns"/> is false, does not
        /// crowd another pawn (see <see cref="IsCrowdedMove"/>). A pawn already inside a blocked cell may always move,
        /// so it can escape. Every simulation-side pawn displacement (path steps, panic, watchdog, yielding) goes through here.
        /// </summary>
        public bool TryDisplacePawn(WorldEntity node, Vec2 candidate, bool respectPawns = true)
        {
            if (node == null) return false;
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            if (snapshot != null && snapshot.IsStaticWalkable(node.Position) && !snapshot.IsSegmentStaticClear(node.Position, candidate))
            {
                return false;
            }
            if (respectPawns && IsCrowdedMove(node, candidate))
            {
                return false;
            }
            node.SetPosition(candidate);
            return true;
        }

        /// <summary>
        /// True when moving <paramref name="node"/> to <paramref name="candidate"/> would bring it closer than
        /// <see cref="NavConstants.PawnSeparation"/> to another pawn without increasing the distance to it (stepping apart
        /// is always allowed). Nearby pawns come from the snapshot; their positions are read live from the registry,
        /// so pawns already moved earlier this tick are seen where they are now, not where they were.
        /// </summary>
        public bool IsCrowdedMove(WorldEntity node, Vec2 candidate)
        {
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            if (snapshot == null || node == null) return false;

            float search = NavConstants.PawnSeparation + CrowdSearchMargin;
            float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
            float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;
            int minX = SimMath.Max(0, SimMath.FloorToInt((candidate.X - search + offsetX) / snapshot.CellSize));
            int maxX = SimMath.Min(snapshot.Width - 1, SimMath.FloorToInt((candidate.X + search + offsetX) / snapshot.CellSize));
            int minY = SimMath.Max(0, SimMath.FloorToInt((candidate.Y - search + offsetY) / snapshot.CellSize));
            int maxY = SimMath.Min(snapshot.Height - 1, SimMath.FloorToInt((candidate.Y + search + offsetY) / snapshot.CellSize));
            float sepSqr = NavConstants.PawnSeparation * NavConstants.PawnSeparation;
            Vec2 current = node.Position;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * snapshot.Width + x;
                    int count = snapshot.CellCounts[index];
                    var cellArray = snapshot.Grid[index];
                    for (int i = 0; i < count; i++)
                    {
                        var entity = cellArray[i];
                        if (entity.Type != EntityType.Pawn || entity.Id == node.Id) continue;

                        var otherNode = _worldRegistry != null ? _worldRegistry.GetEntity(entity.Id) : null;
                        Vec2 other = otherNode != null ? otherNode.Position : entity.Position;
                        float candSqr = (candidate - other).SqrMagnitude;
                        if (candSqr >= sepSqr) continue;
                        if (candSqr > (current - other).SqrMagnitude) continue; // stepping apart is always allowed
                        return true;
                    }
                }
            }
            return false;
        }

        public bool IsPositionBlocked(Vec2 targetPos, EntityId ignoreAgentId, float pawnClearanceRadius = 0.4f)
        {
            if (_spatialPartition == null) return false;
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return false;

            // Cast at each step: Mono keeps a scalar chain at double precision, CoreCLR rounds each step (Epic CORE F1)
            int cellX = SimMath.FloorToInt((float)((float)(targetPos.X + (float)(snapshot.Width * snapshot.CellSize * 0.5f)) / snapshot.CellSize));
            int cellY = SimMath.FloorToInt((float)((float)(targetPos.Y + (float)(snapshot.Height * snapshot.CellSize * 0.5f)) / snapshot.CellSize));

            if (cellX < 0 || cellX >= snapshot.Width || cellY < 0 || cellY >= snapshot.Height) return true; // Out of bounds

            if (snapshot.StaticGrid != null && !snapshot.StaticGrid.IsCellWalkable(cellX, cellY)) return true; // Static wall collision

            int index = cellY * snapshot.Width + cellX;
            int count = snapshot.CellCounts[index];
            var cellArray = snapshot.Grid[index];

            for (int i = 0; i < count; i++)
            {
                var entity = cellArray[i];
                if (entity.Id != ignoreAgentId)
                {
                    if (entity.Type == EntityType.Wall) return true;
                    if (entity.Type == EntityType.Anchor && Vec2.Distance(entity.Position, targetPos) < 0.45f) return true;
                    if (entity.Type == EntityType.Pawn)
                    {
                        // Dynamic validation: if another pawn is currently standing within clearance radius
                        if (Vec2.Distance(entity.Position, targetPos) < pawnClearanceRadius) return true;
                    }
                }
            }
            return false;
        }
    }
}

