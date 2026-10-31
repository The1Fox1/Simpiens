using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The Dev Map's scripted pens (Epics 7, Beta and 8), headless (Epic CLIENT M2.8; Unity's PenCanaryScenario and
    /// ResourceContentionScenario). Each pen's relationships, hunger and groups are seeded by its session script: that
    /// setup is the precondition, so these prove the social mechanisms still work; they are not Definition-of-Done
    /// evidence that the mechanisms emerge in the live game.
    /// </summary>
    public class DevMapScenarioTests
    {
        private static LocalSimulation Pens()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, services => services.AddSingleton<ISessionDefinition>(new CanaryPenSession()));
            sim.Respawn(CanaryPenSession.SessionId);
            return sim;
        }

        /// <summary>The 24 pen agents crowd narrow pens; none stands on another (Unity's ResourceContentionScenario).</summary>
        [Fact]
        public void ThePensDontStack()
        {
            using var sim = Pens();
            var detector = AnomalyDetector.For(sim);
            for (int t = 0; t < 1000; t++)
            {
                sim.Step(1);
                detector.Observe(sim.Clock.CurrentTick);
            }
            Assert.True(detector.StackingCount == 0, $"Pawns stacked in the pens: {detector.First}");
        }

        /// <summary>
        /// Every pen's intended outcome is seen within <see cref="PenCanary.DeadlineTicks"/> (Unity's PenCanaryScenario,
        /// observed every tick instead of every frame).
        /// </summary>
        [Fact]
        public void EveryPenShowsItsOutcome()
        {
            using var sim = Pens();
            var canary = new PenCanary(sim);
            for (long t = 0; t <= PenCanary.DeadlineTicks; t++)
            {
                canary.Observe(t);
                sim.Step(1);
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"outcomes: {canary.Summary()}\n{canary.Timeline}");
            var failures = canary.Failures();
            Assert.True(failures.Length == 0, $"Pen canary failures:\n{failures}Outcomes: {canary.Summary()}");
        }

        private sealed class PenCanary
        {
            /// <summary>
            /// Bound for every pen outcome, in ticks from the session start. Observed 2026-10-11 in Unity (default seed):
            /// pen 2 at t4, 3 at t21, 1 at t22, 7 at t24, 9 and 10 at t50, 8 at t100, 6 at t123, 4 at t200. Campfire
            /// companionship (pen 4) is only recorded every 200 ticks, so the bound allows three of those windows.
            /// </summary>
            public const long DeadlineTicks = 600;

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

            private static readonly Vec2 P6RallyPoint = new Vec2(-8f, -7.5f);
            private static readonly Vec2 P7RallyPoint = new Vec2(-4f, -7.2f);
            private const float RallyRadius = 2.0f;
            private const uint DirectiveExpiryTick = 5000;

            private static readonly string[] OutcomeName =
            {
                "", "1 Fear Yield", "2 Respect Defer", "3 Rivalry", "4 Altruism & Hearth", "5 Selfish Peer",
                "6 Directive Rally", "7 Passive Defiance", "8 Hearth Emergence", "9 Dominance Trial", "10 Schism"
            };

            private readonly LocalSimulation _sim;
            private readonly SimulationManager _manager;
            private readonly IReadOnlyList<AgentState> _agents;
            private readonly long[] _outcomeTick = new long[11];
            private readonly List<ChronicleEvent> _events = new List<ChronicleEvent>(1024);
            private readonly EntityId _pen1Bush, _pen7Bush;
            private readonly Vec2 _p7BushPosition;
            private ulong _lastEventId;
            private long _p4ShareTick = -1, _p5ShareTick = -1, _p1SubmissiveWonTick = -1;
            private bool _p3Yielded, _p8GroupFormed, _p10SchismEvent;
            private int _p7BushYield = -1, _p1BushYield = -1;
            private float _p1DominantHunger;

            public readonly StringBuilder Timeline = new StringBuilder();

            public PenCanary(LocalSimulation sim)
            {
                _sim = sim;
                _manager = (SimulationManager)sim.Simulation;
                _agents = sim.Host.CrawlerStates;
                Assert.Equal(CanaryPenSession.ScriptedPenAgentCount, _agents.Count);
                for (int i = 0; i < _outcomeTick.Length; i++) _outcomeTick[i] = -1;

                var world = sim.Host.World;
                _pen1Bush = world.GetEntityId(DevMapLayout.Pen1ContestedBush);
                _pen7Bush = world.GetEntityId(DevMapLayout.Pen7Bush);
                Assert.True(world.Map.TryGetResourceNode(DevMapLayout.Pen7Bush, out var pen7Node), "Pen 7's bush is missing from the map.");
                _p7BushPosition = pen7Node.Position;
                if (_manager.TryGetResource(_pen7Bush, out var bush)) _p7BushYield = bush.RemainingYield;
                if (_manager.TryGetResource(_pen1Bush, out var contested)) _p1BushYield = contested.RemainingYield;
                _p1DominantHunger = _agents[P1Dominant].Hunger;
            }

            public void Observe(long rel)
            {
                ReadNewEvents(rel);
                ObserveState(rel, (uint)_sim.Clock.CurrentTick);
            }

            public string Failures()
            {
                var failures = new StringBuilder();
                for (int pen = 1; pen <= 10; pen++)
                {
                    if (pen == 5) continue; // a negative outcome, checked below
                    if (_outcomeTick[pen] < 0) failures.AppendLine($"  Pen {OutcomeName[pen]}: not observed within {DeadlineTicks} ticks");
                }
                if (_p5ShareTick >= 0) failures.AppendLine($"  Pen {OutcomeName[5]}: the hostile peer fed the stranger at t{_p5ShareTick}");
                if (_p1SubmissiveWonTick >= 0) failures.AppendLine($"  Pen {OutcomeName[1]}: the fearful agent won the contested bush from the feared agent at t{_p1SubmissiveWonTick}");
                if (_p3Yielded) failures.AppendLine($"  Pen {OutcomeName[3]}: a rival yielded instead of contesting");
                return failures.ToString();
            }

            public string Summary()
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
                Timeline.AppendLine($"  t{rel}: pen {OutcomeName[pen]}: {what}");
            }

            private bool Is(EntityId id, int index) => id == _agents[index].AgentId;

            private void ReadNewEvents(long rel)
            {
                _events.Clear();
                _sim.Chronicle.GetAllRecentEvents(_events, NarrativeChronicleService.DefaultGlobalCapacity);
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
                            Timeline.AppendLine($"  t{rel}: pen 3: a rival YIELDED");
                        }
                        break;
                    case ChronicleEventType.ResourceClaimed: // A won a contested harvest against B
                        if (Is(e.ActorAId, P1Submissive) && Is(e.ActorBId, P1Dominant) && _p1SubmissiveWonTick < 0)
                        {
                            _p1SubmissiveWonTick = rel;
                            Timeline.AppendLine($"  t{rel}: pen 1: the fearful agent WON a contest against the feared agent");
                        }
                        if ((Is(e.ActorAId, P3RivalA) && Is(e.ActorBId, P3RivalB)) || (Is(e.ActorAId, P3RivalB) && Is(e.ActorBId, P3RivalA)))
                            Mark(3, rel, "rivals contested the bush; one won it");
                        break;
                    case ChronicleEventType.AltruisticFoodShare: // A fed B
                        if (Is(e.ActorAId, P4Donor) && Is(e.ActorBId, P4Companion) && _p4ShareTick < 0)
                        {
                            _p4ShareTick = rel;
                            Timeline.AppendLine($"  t{rel}: pen 4: donor fed the starving companion");
                        }
                        if (Is(e.ActorAId, P5Selfish) && Is(e.ActorBId, P5Stranger) && _p5ShareTick < 0)
                        {
                            _p5ShareTick = rel;
                            Timeline.AppendLine($"  t{rel}: pen 5: hostile peer FED the stranger");
                        }
                        break;
                    case ChronicleEventType.CampfireCompanionship:
                        if ((Is(e.ActorAId, P4Donor) && Is(e.ActorBId, P4Companion)) || (Is(e.ActorAId, P4Companion) && Is(e.ActorBId, P4Donor)))
                        {
                            if (_p4ShareTick >= 0) Mark(4, rel, "donor fed the companion, then both kept company at the campfire");
                            else Timeline.AppendLine($"  t{rel}: pen 4: campfire companionship before any feeding");
                        }
                        break;
                    case ChronicleEventType.GroupFormed:
                        _p8GroupFormed = true;
                        Timeline.AppendLine($"  t{rel}: group formed at a hearth");
                        break;
                    case ChronicleEventType.GroupSchism:
                        _p10SchismEvent = true;
                        Timeline.AppendLine($"  t{rel}: group schism");
                        break;
                }
            }

            private void ObserveState(long rel, uint absoluteTick)
            {
                // Pen 1: the contested bush goes to the feared agent: the fearful agent yields in a contest (above) or
                // keeps away, which leaves no event; then the first harvest of the bush is the feared agent's
                float dominantHunger = _agents[P1Dominant].Hunger;
                if (_manager.TryGetResource(_pen1Bush, out var contested))
                {
                    if (_p1BushYield >= 0 && contested.RemainingYield < _p1BushYield)
                    {
                        if (_p1DominantHunger - dominantHunger >= 20f) Mark(1, rel, "feared agent harvested the contested bush; the fearful agent kept away");
                        else Timeline.AppendLine($"  t{rel}: pen 1 contested bush harvested by someone other than the feared agent");
                    }
                    _p1BushYield = contested.RemainingYield;
                }
                _p1DominantHunger = dominantHunger;

                // Pen 6: the compliant follower reaches the rally point
                if (Near(P6Follower, P6RallyPoint, RallyRadius) && _agents[P6Follower].Memberships.First.HasGroup) Mark(6, rel, "compliant follower reached the rally point");

                // Pen 7: the starving follower harvests the bush outside the rally radius while the directive is active
                if (_manager.TryGetResource(_pen7Bush, out var bush))
                {
                    if (_p7BushYield >= 0 && bush.RemainingYield < _p7BushYield)
                    {
                        bool byDefiant = Near(P7Defiant, _p7BushPosition, 1.5f);
                        bool outsideRally = !Near(P7Defiant, P7RallyPoint, RallyRadius);
                        if (byDefiant && outsideRally && absoluteTick <= DirectiveExpiryTick) Mark(7, rel, "starving follower foraged outside the rally point under an active directive");
                        else Timeline.AppendLine($"  t{rel}: pen 7 bush harvested (defiant nearby: {byDefiant}, outside rally: {outsideRally})");
                    }
                    _p7BushYield = bush.RemainingYield;
                }

                // Pen 8: a group formed with the respected candidate as leader
                var candidate = _agents[P8Candidate].Memberships.First;
                if (_p8GroupFormed && candidate.HasGroup && candidate.Role == RoleRank.Leader) Mark(8, rel, "hearth group formed with the respected candidate as leader");

                // Pen 9: the challenger leads; the collapsed incumbent no longer does
                var challenger = _agents[P9Challenger].Memberships.First;
                var incumbent = _agents[P9Collapsed].Memberships.First;
                if (challenger.HasGroup && challenger.Role == RoleRank.Leader && !(incumbent.HasGroup && incumbent.Role == RoleRank.Leader && incumbent.GroupId == challenger.GroupId))
                    Mark(9, rel, "challenger displaced the collapsed leader");

                // Pen 10: after the schism the discredited leader's band spans two groups
                if (_p10SchismEvent && _outcomeTick[10] < 0)
                {
                    byte first = 0;
                    bool split = false;
                    for (int i = P10Leader; i <= P10DissidentB; i++)
                    {
                        var m = _agents[i].Memberships.First;
                        if (!m.HasGroup) continue;
                        if (first == 0) first = m.GroupId;
                        else if (m.GroupId != first) split = true;
                    }
                    if (split) Mark(10, rel, "band split into two groups");
                }
            }

            private bool Near(int index, Vec2 point, float radius) => Vec2.Distance(_agents[index].Position, point) <= radius;
        }
    }
}
