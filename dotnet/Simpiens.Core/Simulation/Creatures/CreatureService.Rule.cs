using System.Collections.Generic;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// The creature's rule (Epic 10 Part 3, K2.2; Q5; the epic's D9–D15): a short state machine on the main thread, not a
    /// planner. A creature rests at its place and heals; rested, it roams near its lair; it chases a crawler it sees within its
    /// leash and strikes once in reach; hurt past its kind's line it flees home; its quarry lost, it goes home. Movement is
    /// physical time (its speed a second, rests in seconds); healing is the day's (its kind's hours).
    /// </summary>
    public sealed partial class CreatureService
    {
        /// <summary>The waypoints a creature keeps of a path: past them it plans again.</summary>
        public const int MaxPath = 64;

        /// <summary>Seconds a creature rests at its place before it roams: from this to twice it, drawn (D15).</summary>
        public const float RestSeconds = 20f;

        /// <summary>How far from its lair's heart it roams: this share of its leash (D15).</summary>
        public const float RoamShareOfLeash = 0.5f;

        /// <summary>The nearest a roam takes it from the heart.</summary>
        public const float RoamMinimum = 0.5f;

        /// <summary>How near its goal counts as there.</summary>
        public const float Arrived = 0.25f;

        /// <summary>Its goal moved this far from where its path ends: it plans again (a quarry on the move, D11).</summary>
        public const float ReplanDistance = 1f;


        private enum Walk : byte { There, Walking, Stuck }

        private struct Swing
        {
            public CreatureState Attacker;
            public WorldEntity Target;
            public SwingOutcome Outcome;
        }

        private readonly GridAStarSearcher _searcher = new GridAStarSearcher();
        private readonly PathResponse _path = NewPathBuffer();
        private readonly List<Swing> _swings = new List<Swing>(16);

        private static PathResponse NewPathBuffer()
        {
            var response = new PathResponse();
            response.Initialize(200, null);
            return response;
        }

        /// <summary>
        /// D12: every creature in id order decides and moves; then every due swing is decided from where all stand after
        /// moving (the combat rule's D6: the rolls are keyed by attacker and tick); then all the harm lands. A creature swings
        /// at most once a tick.
        /// </summary>
        private void Rule(ICreatureWorld world, uint tick)
        {
            if (world == null || _creatures.Count == 0) return;
            var snapshot = world.Snapshot;
            var entities = world.Entities;
            for (int i = 0; i < _creatures.Count; i++) Step(_creatures[i], snapshot, entities, tick);

            _swings.Clear();
            for (int i = 0; i < _creatures.Count; i++) DecideSwing(_creatures[i], entities, tick);
            for (int i = 0; i < _swings.Count; i++) world.Strike(_swings[i].Target, _swings[i].Attacker.Id, _swings[i].Outcome, tick);

            // Who knows of a lair now (K2.3, D17): a crawler struck by one of its creatures, or one who sees one
            for (int i = 0; i < _swings.Count; i++) Chart(_swings[i].Attacker.LairIndex, _swings[i].Target.Id, struck: true);
            Sightings(snapshot, entities);
        }

        /// <summary>How far a crawler sees a creature (its vision, as it sees everything else).</summary>
        public const float SightingDistance = Simpiens.Cognition.AgentRuntime.DefaultVisionRadius;

        // D17: an uncharted lair is charted when a crawler on its feet sees one of its creatures in a charted sector (nothing
        // inside the fog is seen), within its vision and with nothing walling it off; one sighting a creature, the first crawler
        private void Sightings(SharedWorldSnapshot snapshot, IReadOnlyList<WorldEntity> entities)
        {
            if (entities == null) return;
            for (int i = 0; i < _creatures.Count; i++)
            {
                var c = _creatures[i];
                if (!c.IsAlive || IsCharted(c.LairIndex)) continue;
                if (snapshot != null && !snapshot.IsCharted(c.Position)) continue;
                for (int e = 0; e < entities.Count; e++)
                {
                    var agent = entities[e].Agent;
                    if (agent == null || agent.IsDowned || agent.HasLeft) continue;
                    if ((entities[e].Position - c.Position).SqrMagnitude > SightingDistance * SightingDistance) continue;
                    if (snapshot != null && !snapshot.HasLineOfSight(entities[e].Position, c.Position)) continue;
                    Chart(c.LairIndex, entities[e].Id, struck: false);
                    break;
                }
            }
        }

        private void Chart(int lairIndex, EntityId by, bool struck)
        {
            if (lairIndex < 0 || lairIndex >= 64 || lairIndex >= _lairs.Count || IsCharted(lairIndex)) return;
            _charted |= 1UL << lairIndex;
            SimLog.Simulation.ZLogInformation($"{SimLog.Agent(by)} {(struck ? "was struck from" : "sighted")} {_lairs[lairIndex].Definition.Key}: the lair is charted");
        }

        private void Step(CreatureState c, SharedWorldSnapshot snapshot, IReadOnlyList<WorldEntity> entities, uint tick)
        {
            var kind = c.Definition;
            if (kind == null || !c.IsAlive || c.LairIndex < 0 || c.LairIndex >= _lairs.Count) return;
            var heart = _lairs[c.LairIndex].Definition.Position;
            var at = c.Position;

            // Hurt past its line, a kind that flees runs for its place and heeds nothing (D9)
            if (kind.Flees && Wounded(c, kind) && c.Mode != CreatureMode.Flee && c.Mode != CreatureMode.Rest)
            {
                c.Mode = CreatureMode.Flee;
                c.Target = default;
                c.ForgetPath();
            }

            switch (c.Mode)
            {
                case CreatureMode.Flee:
                    if (WalkTo(c, kind, c.Home, snapshot, tick) != Walk.Walking) Rest(c, tick);
                    break;

                case CreatureMode.Chase:
                    var quarry = FindCrawler(entities, c.Target);
                    if (!IsQuarry(quarry, kind, heart))
                    {
                        GoHome(c);
                        if (WalkTo(c, kind, c.Home, snapshot, tick) != Walk.Walking) Rest(c, tick);
                        break;
                    }
                    var me = Combatant.ForCreature(kind, c.Id, c.Position, c.Health);
                    var them = Combatant.ForCrawler(quarry.Agent, quarry.Radius);
                    if (CombatRules.Gap(me, them) <= kind.Reach) break; // in reach: it stands and strikes
                    if (WalkTo(c, kind, quarry.Position, snapshot, tick) == Walk.Stuck) GoHome(c);
                    break;

                case CreatureMode.Home:
                    if (Notice(c, kind, heart, snapshot, entities)) break;
                    if (WalkTo(c, kind, c.Home, snapshot, tick) != Walk.Walking) Rest(c, tick);
                    break;

                case CreatureMode.Roam:
                    if (Notice(c, kind, heart, snapshot, entities)) break;
                    if (WalkTo(c, kind, c.Goal, snapshot, tick) != Walk.Walking) Rest(c, tick);
                    break;

                default: // Rest: heal at its place; rested and not wounded, it roams
                    if (Vec2.Distance(c.Position, c.Home) <= Arrived && c.Health < kind.MaxHealth)
                        c.Health = SimMath.Min(kind.MaxHealth, (float)(c.Health + kind.HealPerTick));
                    if (Notice(c, kind, heart, snapshot, entities)) break;
                    if (tick >= c.RestUntil && !Wounded(c, kind) && TryRoamPoint(c, kind, heart, snapshot, tick, out var point))
                    {
                        c.Mode = CreatureMode.Roam;
                        c.Goal = point;
                        c.ForgetPath();
                    }
                    break;
            }

            // Every tick, moved or not, through its entity, so a view draws it between ticks from where it was (D11)
            if (c.Entity != null)
            {
                var moved = c.Position;
                c.Position = at;
                c.Entity.SetPosition(moved);
            }
        }

        // D10: a crawler on its feet, in its sight with nothing walling it off, within its leash of the lair's heart; the
        // nearest, the lower id on a tie. Wounded past its line, a fleeing kind notices nothing
        private bool Notice(CreatureState c, CreatureDefinition kind, Vec2 heart, SharedWorldSnapshot snapshot, IReadOnlyList<WorldEntity> entities)
        {
            if ((kind.Flees && Wounded(c, kind)) || entities == null) return false;
            WorldEntity best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (!IsQuarry(e, kind, heart)) continue;
                float d = Vec2.Distance(c.Position, e.Position);
                if (d > kind.Sight) continue;
                if (snapshot != null && !snapshot.HasLineOfSight(c.Position, e.Position)) continue;
                if (d < bestDistance || (d == bestDistance && e.Id.CompareTo(best.Id) < 0))
                {
                    best = e;
                    bestDistance = d;
                }
            }
            if (best == null) return false;
            c.Mode = CreatureMode.Chase;
            c.Target = best.Id;
            c.ForgetPath();
            return true;
        }

        // A crawler worth chasing: on its feet (until K5 a downed crawler limps out, as from a hazard), and within the leash
        private static bool IsQuarry(WorldEntity e, CreatureDefinition kind, Vec2 heart)
        {
            var agent = e?.Agent;
            return agent != null && !agent.IsDowned && agent.Health > 0f && !agent.HasLeft && Vec2.Distance(e.Position, heart) <= kind.Leash;
        }

        private void DecideSwing(CreatureState c, IReadOnlyList<WorldEntity> entities, uint tick)
        {
            if (c.Mode != CreatureMode.Chase || !c.IsAlive) return;
            var kind = c.Definition;
            var quarry = FindCrawler(entities, c.Target);
            if (kind == null || quarry?.Agent == null) return;
            var me = Combatant.ForCreature(kind, c.Id, c.Position, c.Health);
            var them = Combatant.ForCrawler(quarry.Agent, quarry.Radius);
            if (!CombatRules.CanStrike(me, them, tick, c.NextSwingAt)) return;
            _swings.Add(new Swing { Attacker = c, Target = quarry, Outcome = CombatRules.Swing(_seed, tick, me, them) });
            c.NextSwingAt = CombatRules.NextSwingAt(tick, me);
            c.SwungAtTick = tick;
        }

        // Toward the goal along its path (D11): planned through the A* when it has none, when the goal has moved more than
        // a cell from where the path ends, or when the path ran out short of it
        private Walk WalkTo(CreatureState c, CreatureDefinition kind, Vec2 goal, SharedWorldSnapshot snapshot, uint tick)
        {
            if (Vec2.Distance(c.Position, goal) <= Arrived) return Walk.There;
            bool stale = c.PlannedAt < 0 || c.PathCursor >= c.PathLength || Vec2.Distance(goal, c.PlannedFor) > ReplanDistance;
            if (stale && !Plan(c, goal, snapshot, tick)) return Walk.Stuck;

            float step = (float)(kind.Speed * SimulationClock.FixedTickDeltaTime);
            var position = c.Position;
            while (step > 0f && c.PathCursor < c.PathLength)
            {
                var waypoint = c.Path[c.PathCursor];
                float d = Vec2.Distance(position, waypoint);
                if (d <= step)
                {
                    position = waypoint;
                    step = (float)(step - d);
                    c.PathCursor++;
                    continue;
                }
                position = position + (waypoint - position) * (float)(step / d);
                step = 0f;
            }
            c.Position = position;
            return Vec2.Distance(position, goal) <= Arrived ? Walk.There : Walk.Walking;
        }

        private bool Plan(CreatureState c, Vec2 goal, SharedWorldSnapshot snapshot, uint tick)
        {
            c.ForgetPath();
            c.PlannedAt = tick;
            c.PlannedFor = goal;
            if (snapshot == null)
            {
                c.Path[0] = goal;
                c.PathLength = 1;
                return true;
            }
            _path.Clear();
            var request = new PathRequest { StartPosition = c.Position, TargetPosition = goal, RequesterId = c.Id };
            if (!_searcher.Search(request, snapshot, _path) || !_path.IsValid || _path.Length == 0) return false;
            int length = _path.Length < MaxPath ? _path.Length : MaxPath;
            for (int i = 0; i < length; i++) c.Path[i] = _path.Waypoints[i];
            c.PathLength = length;
            c.PathCursor = 0;
            return true;
        }

        // A point near the lair's heart, in its sector and walkable, drawn from (seed, creature, tick); false after four misses
        private bool TryRoamPoint(CreatureState c, CreatureDefinition kind, Vec2 heart, SharedWorldSnapshot snapshot, uint tick, out Vec2 point)
        {
            var rng = SimRandom.For(_seed, c.Id, tick, SimRandomSalt.CreatureRoam);
            float reach = (float)(kind.Leash * RoamShareOfLeash);
            var map = _map;
            byte sector = map != null ? map.FindSector(heart).Id : (byte)0;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = rng.Range(0f, (float)(2f * SimMath.PI));
                float distance = rng.Range(RoamMinimum, reach > RoamMinimum ? reach : RoamMinimum);
                point = new Vec2((float)(heart.X + (float)(SimMath.Cos(angle) * distance)), (float)(heart.Y + (float)(SimMath.Sin(angle) * distance)));
                if (map != null && map.FindSector(point).Id != sector) continue;
                if (snapshot?.StaticGrid != null && (!snapshot.StaticGrid.WorldToCell(point, out int x, out int y) || !snapshot.StaticGrid.IsCellWalkable(x, y))) continue;
                return true;
            }
            point = default;
            return false;
        }

        private void Rest(CreatureState c, uint tick)
        {
            c.Mode = CreatureMode.Rest;
            c.Target = default;
            c.RestUntil = tick + RestTicks(c.Id, tick);
            c.ForgetPath();
        }

        private static void GoHome(CreatureState c)
        {
            c.Mode = CreatureMode.Home;
            c.Target = default;
            c.ForgetPath();
        }

        private static bool Wounded(CreatureState c, CreatureDefinition kind) => kind.Flees && c.Health < (float)(kind.FleeBelow * kind.MaxHealth);

        // How long a rest lasts: RestSeconds to twice it, drawn from (seed, creature, tick)
        private uint RestTicks(EntityId id, uint tick)
        {
            var rng = SimRandom.For(_seed, id, tick, SimRandomSalt.CreatureRest);
            float seconds = rng.Range(RestSeconds, (float)(2f * RestSeconds));
            return (uint)CombatRules.SecondsToTicks(seconds);
        }

        private static WorldEntity FindCrawler(IReadOnlyList<WorldEntity> entities, EntityId id)
        {
            if (entities == null || id == default) return null;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id == id) return entities[i].Agent != null ? entities[i] : null;
            }
            return null;
        }
    }
}
