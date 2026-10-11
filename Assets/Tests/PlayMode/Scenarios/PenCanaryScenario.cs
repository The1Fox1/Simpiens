using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Canary for the ten scripted demonstration pens (Epics 7, Beta, 8). Each pen's relationships, hunger and groups
    /// are seeded by its session script: that setup IS the precondition, so this scenario proves the social mechanisms
    /// still work; it is not Definition-of-Done evidence that they emerge in the live game.
    ///
    /// Runs the pens at 10x, observes every frame (chronicle events by id, authoritative group and resource state,
    /// agent positions) and records the first tick at which each pen's intended outcome is seen. All ten checks are
    /// reported together.
    /// </summary>
    public class PenCanaryScenario : ISimulationScenario
    {
        /// <summary>
        /// Bound for every pen outcome, in ticks from the session start. Observed 2026-10-11 (seed default): pen 2 at
        /// t4, 3 at t21, 1 at t22, 7 at t24, 9 and 10 at t50, 8 at t100, 6 at t123, 4 at t200. Campfire companionship
        /// (pen 4) is only recorded every 200 ticks, so the bound allows three of those windows.
        /// </summary>
        public const long DeadlineTicks = 600;
        private const float WallClockTimeoutSeconds = 60f;

        public string Name => "PenCanaryScenario";
        public float DurationSeconds => 0f; // SetupAsync observes until the deadline
        public float TimeMultiplier => 10f;

        // Pen agent indices in spawn order (see the pen session script)
        private const int P1Dominant = 0, P1Submissive = 1;
        private const int P2Respected = 2, P2Deferent = 3;
        private const int P3RivalA = 4, P3RivalB = 5;
        private const int P4Donor = 6, P4Companion = 7;
        private const int P5Selfish = 8, P5Stranger = 9;
        private const int P6Follower = 11;
        private const int P7Defiant = 13;
        private const int P8Candidate = 14;
        private const int P9Collapsed = 17, P9Challenger = 18;
        private const int P10Leader = 20, P10DissidentB = 23;

        private static readonly Vector2 P6RallyPoint = new Vector2(-8f, -7.5f);
        private static readonly Vector2 P7RallyPoint = new Vector2(-4f, -7.2f);
        private const float RallyRadius = 2.0f;
        private const uint DirectiveExpiryTick = 5000;

        private readonly long[] _outcomeTick = new long[11];
        private readonly string[] _outcomeName =
        {
            "", "1 Fear Yield", "2 Respect Defer", "3 Rivalry", "4 Altruism & Hearth", "5 Selfish Peer",
            "6 Directive Rally", "7 Passive Defiance", "8 Hearth Emergence", "9 Dominance Trial", "10 Schism"
        };
        private readonly StringBuilder _timeline = new StringBuilder();
        private readonly List<ChronicleEvent> _events = new List<ChronicleEvent>(1024);

        private IReadOnlyList<AutonomousAgent> _agents;
        private ulong _lastEventId;
        private long _p4ShareTick = -1;
        private bool _p3Yielded;
        private long _p5ShareTick = -1;
        private bool _p8GroupFormed;
        private bool _p10SchismEvent;
        private int _p7BushYield = -1;
        private int _p1BushYield = -1;
        private float _p1DominantHunger;
        private long _p1SubmissiveWonTick = -1;
        private Vector2 _p7BushPosition;
        private long _observedTicks;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(sim, "ISimulationManager not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");

            _agents = spawner.Agents;
            Assert.GreaterOrEqual(_agents.Count, 24, "The pen session must spawn the 24 scripted pen agents.");
            for (int i = 0; i < _outcomeTick.Length; i++) _outcomeTick[i] = -1;

            var simManager = sim as SimulationManager;
            var pen1Bush = spawner.World.GetEntityId(DevMapLayout.Pen1ContestedBush);
            var pen7Bush = spawner.World.GetEntityId(DevMapLayout.Pen7Bush);
            Assert.IsTrue(spawner.World.Map.TryGetResourceNode(DevMapLayout.Pen7Bush, out var pen7Node), "Pen 7 bush missing from the map.");
            _p7BushPosition = pen7Node.Position;
            if (simManager != null && simManager.TryGetResource(pen7Bush, out var bush)) _p7BushYield = bush.RemainingYield;
            if (simManager != null && simManager.TryGetResource(pen1Bush, out var contested)) _p1BushYield = contested.RemainingYield;
            _p1DominantHunger = _agents[P1Dominant].Hunger;

            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            long startTick = clock.CurrentTick;
            float elapsed = 0f;
            while (elapsed < WallClockTimeoutSeconds)
            {
                long rel = clock.CurrentTick - startTick;
                _observedTicks = rel;
                ReadNewEvents(sim.Chronicle, rel);
                ObserveState(simManager, pen1Bush, pen7Bush, rel, (uint)clock.CurrentTick);

                if (rel >= DeadlineTicks) break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.Log($"[PenCanaryScenario] observed {_observedTicks} ticks; outcomes: {Summary()}\n{_timeline}");
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.GreaterOrEqual(_observedTicks, DeadlineTicks, $"Wall-clock timeout before the deadline: observed {_observedTicks}/{DeadlineTicks} ticks.");

            var failures = new StringBuilder();
            for (int pen = 1; pen <= 10; pen++)
            {
                if (pen == 5) continue; // Negative outcome, checked below
                if (_outcomeTick[pen] < 0) failures.AppendLine($"  Pen {_outcomeName[pen]}: not observed within {DeadlineTicks} ticks");
            }
            if (_p5ShareTick >= 0) failures.AppendLine($"  Pen {_outcomeName[5]}: the hostile peer fed the stranger at t{_p5ShareTick}");
            if (_p1SubmissiveWonTick >= 0) failures.AppendLine($"  Pen {_outcomeName[1]}: the fearful agent won the contested bush from the feared agent at t{_p1SubmissiveWonTick}");
            if (_p3Yielded) failures.AppendLine($"  Pen {_outcomeName[3]}: a rival yielded instead of contesting");

            Assert.IsTrue(failures.Length == 0, $"Pen canary failures:\n{failures}Outcomes: {Summary()}");
        }

        private string Summary()
        {
            var sb = new StringBuilder();
            for (int pen = 1; pen <= 10; pen++)
            {
                if (pen > 1) sb.Append(", ");
                sb.Append(pen).Append('@');
                if (pen == 5) sb.Append(_p5ShareTick < 0 ? "ok" : $"FED t{_p5ShareTick}");
                else sb.Append(_outcomeTick[pen] < 0 ? "-" : _outcomeTick[pen].ToString());
            }
            return sb.ToString();
        }

        private void Mark(int pen, long rel, string what)
        {
            if (_outcomeTick[pen] >= 0) return;
            _outcomeTick[pen] = rel;
            _timeline.AppendLine($"  t{rel}: pen {_outcomeName[pen]}: {what}");
        }

        private UnityEngine.GUID Id(int index) => _agents[index].AgentId;

        private bool Is(UnityEngine.GUID id, int index) => id == Id(index);

        private void ReadNewEvents(INarrativeChronicleService chronicle, long rel)
        {
            if (chronicle == null) return;
            _events.Clear();
            chronicle.GetAllRecentEvents(_events, NarrativeChronicleService.DefaultGlobalCapacity);

            // Newest first: walk backwards so events are handled in recording order
            ulong newest = _lastEventId;
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                var e = _events[i];
                if (e.EventId <= _lastEventId) continue;
                if (e.EventId > newest) newest = e.EventId;
                OnEvent(e, rel);
            }
            _lastEventId = newest;
        }

        private void OnEvent(in ChronicleEvent e, long rel)
        {
            switch (e.EventType)
            {
                case ChronicleEventType.ResourceYielded: // A yielded to dominant B
                    if (Is(e.ActorAId, P1Submissive) && Is(e.ActorBId, P1Dominant)) Mark(1, rel, "submissive agent yielded the contested bush to the feared agent");
                    if (Is(e.ActorAId, P2Deferent) && Is(e.ActorBId, P2Respected)) Mark(2, rel, "deferent agent yielded the contested bush to the respected agent");
                    if ((Is(e.ActorAId, P3RivalA) && Is(e.ActorBId, P3RivalB)) || (Is(e.ActorAId, P3RivalB) && Is(e.ActorBId, P3RivalA)))
                    {
                        _p3Yielded = true;
                        _timeline.AppendLine($"  t{rel}: pen 3: a rival YIELDED");
                    }
                    break;
                case ChronicleEventType.ResourceClaimed: // A won a contested harvest against B
                    if (Is(e.ActorAId, P1Submissive) && Is(e.ActorBId, P1Dominant) && _p1SubmissiveWonTick < 0)
                    {
                        _p1SubmissiveWonTick = rel;
                        _timeline.AppendLine($"  t{rel}: pen 1: the fearful agent WON a contest against the feared agent");
                    }
                    if ((Is(e.ActorAId, P3RivalA) && Is(e.ActorBId, P3RivalB)) || (Is(e.ActorAId, P3RivalB) && Is(e.ActorBId, P3RivalA)))
                    {
                        Mark(3, rel, "rivals contested the bush; one won it");
                    }
                    break;
                case ChronicleEventType.AltruisticFoodShare: // A fed B
                    if (Is(e.ActorAId, P4Donor) && Is(e.ActorBId, P4Companion) && _p4ShareTick < 0)
                    {
                        _p4ShareTick = rel;
                        _timeline.AppendLine($"  t{rel}: pen 4: donor fed the starving companion");
                    }
                    if (Is(e.ActorAId, P5Selfish) && Is(e.ActorBId, P5Stranger) && _p5ShareTick < 0)
                    {
                        _p5ShareTick = rel;
                        _timeline.AppendLine($"  t{rel}: pen 5: hostile peer FED the stranger");
                    }
                    break;
                case ChronicleEventType.CampfireCompanionship:
                    if ((Is(e.ActorAId, P4Donor) && Is(e.ActorBId, P4Companion)) || (Is(e.ActorAId, P4Companion) && Is(e.ActorBId, P4Donor)))
                    {
                        if (_p4ShareTick >= 0) Mark(4, rel, "donor fed the companion, then both kept company at the campfire");
                        else _timeline.AppendLine($"  t{rel}: pen 4: campfire companionship before any feeding");
                    }
                    break;
                case ChronicleEventType.GroupFormed:
                    _p8GroupFormed = true;
                    _timeline.AppendLine($"  t{rel}: group formed at a hearth");
                    break;
                case ChronicleEventType.GroupSchism:
                    _p10SchismEvent = true;
                    _timeline.AppendLine($"  t{rel}: group schism");
                    break;
                case ChronicleEventType.LeadershipAssumed:
                    _timeline.AppendLine($"  t{rel}: leadership assumed");
                    break;
            }
        }

        private void ObserveState(SimulationManager simManager, UnityEngine.GUID pen1Bush, UnityEngine.GUID pen7Bush, long rel, uint absoluteTick)
        {
            // Pen 1: the contested bush goes to the feared agent. The fearful agent either yields in a contest
            // (chronicled above) or avoids the bush altogether, which leaves no event: then the first harvest of the
            // contested bush is the feared agent's (its hunger falls in the same frame the bush is depleted)
            float dominantHunger = _agents[P1Dominant].Hunger;
            if (simManager != null && simManager.TryGetResource(pen1Bush, out var contested))
            {
                if (_p1BushYield >= 0 && contested.RemainingYield < _p1BushYield)
                {
                    if (_p1DominantHunger - dominantHunger >= 20f) Mark(1, rel, "feared agent harvested the contested bush; the fearful agent kept away");
                    else _timeline.AppendLine($"  t{rel}: pen 1 contested bush harvested by someone other than the feared agent");
                }
                _p1BushYield = contested.RemainingYield;
            }
            _p1DominantHunger = dominantHunger;

            // Pen 6: the compliant follower reaches the rally point
            if (Near(P6Follower, P6RallyPoint, RallyRadius) && _agents[P6Follower].Membership.HasGroup)
            {
                Mark(6, rel, "compliant follower reached the rally point");
            }

            // Pen 7: the starving follower harvests the bush outside the rally radius while the directive is active
            if (simManager != null && simManager.TryGetResource(pen7Bush, out var bush))
            {
                if (_p7BushYield >= 0 && bush.RemainingYield < _p7BushYield)
                {
                    bool byDefiant = Near(P7Defiant, _p7BushPosition, 1.5f);
                    bool outsideRally = !Near(P7Defiant, P7RallyPoint, RallyRadius);
                    if (byDefiant && outsideRally && absoluteTick <= DirectiveExpiryTick)
                    {
                        Mark(7, rel, "starving follower foraged outside the rally point under an active directive");
                    }
                    else
                    {
                        _timeline.AppendLine($"  t{rel}: pen 7 bush harvested (defiant nearby: {byDefiant}, outside rally: {outsideRally})");
                    }
                }
                _p7BushYield = bush.RemainingYield;
            }

            // Pen 8: a group formed with the respected candidate as leader
            var candidate = _agents[P8Candidate].Membership;
            if (_p8GroupFormed && candidate.HasGroup && candidate.Role == RoleRank.Leader)
            {
                Mark(8, rel, "hearth group formed with the respected candidate as leader");
            }

            // Pen 9: the challenger leads; the collapsed incumbent no longer does
            var challenger = _agents[P9Challenger].Membership;
            var incumbent = _agents[P9Collapsed].Membership;
            if (challenger.HasGroup && challenger.Role == RoleRank.Leader && !(incumbent.HasGroup && incumbent.Role == RoleRank.Leader && incumbent.GroupId == challenger.GroupId))
            {
                Mark(9, rel, "challenger displaced the collapsed leader");
            }

            // Pen 10: after the schism the discredited leader's band spans two groups (original + splinter)
            if (_p10SchismEvent && _outcomeTick[10] < 0)
            {
                byte first = 0;
                bool split = false;
                for (int i = P10Leader; i <= P10DissidentB; i++)
                {
                    var m = _agents[i].Membership;
                    if (!m.HasGroup) continue;
                    if (first == 0) first = m.GroupId;
                    else if (m.GroupId != first) split = true;
                }
                if (split)
                {
                    Mark(10, rel, $"band split into two groups ({Memberships(P10Leader, P10DissidentB)})");
                }
            }
        }

        private string Memberships(int from, int to)
        {
            var sb = new StringBuilder();
            for (int i = from; i <= to; i++)
            {
                var m = _agents[i].Membership;
                if (i > from) sb.Append(", ");
                sb.Append(i).Append(':').Append(m.HasGroup ? $"g{m.GroupId}/{m.Role}" : "none");
            }
            return sb.ToString();
        }

        private bool Near(int index, Vector2 point, float radius)
        {
            var agent = _agents[index];
            if (agent == null) return false;
            return Vector2.Distance(agent.transform.position, point) <= radius;
        }
    }
}
