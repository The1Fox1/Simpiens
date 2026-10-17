using System.Collections;
using NUnit.Framework;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Scenario testing dynamic obstacle injection directly in front of moving agents.
    /// Verifies that the progressive unstuck reflex resolves cleanly without persistent jitter loops or clipping.
    /// Runs on the outpost floor (Epic MAP Phase 3): walls drop into doors the crawlers stream through on their way
    /// out of the Road Gate, first the Hub - Bunkhouse door, then the Ore Quarry - Hub door. (The gate door itself
    /// sits beside the 12 spawn points and is never clear enough to drop a wall in front of, not onto, a crawler.)
    /// </summary>
    public class DynamicChaosScenario : ISimulationScenario
    {
        public string Name => "DynamicChaosScenario";
        public string SessionId => Simpiens.Core.Session.OutpostSession.SessionId;
        public float DurationSeconds => 4.0f;
        public float TimeMultiplier => 2.5f;

        // Spots across a doorway (offsets along the door from its centre, half a cell either side of the wall line):
        // the door narrows from four cells to three. The wall must drop in front of the crawlers, never onto one: the injector's own
        // clearance check uses physics colliders, which do not see pawns (it shifted the walls off the doors and
        // onto standing pawns), so the scenario picks a spot clear of every pawn from their logical positions.
        private static readonly Vec2[] DoorSpots =
        {
            new Vec2(-0.5f, 0.5f), new Vec2(0.5f, 0.5f), new Vec2(-1.5f, 0.5f), new Vec2(1.5f, 0.5f),
            new Vec2(-0.5f, -0.5f), new Vec2(0.5f, -0.5f), new Vec2(-1.5f, -0.5f), new Vec2(1.5f, -0.5f)
        };

        /// <summary>
        /// An injected wall blocks the 3x3 cells around it (radius 0.5 plus body inflation), reaching up to 1.5 units
        /// from its centre along an axis; a pawn inside those cells is inside the wall the moment it appears (seen
        /// at 1.01 units). Outside them, plus one step of travel, the wall really drops in front of the pawn.
        /// </summary>
        private const float PawnClearance = 2.0f;
        private const int MaxWaitFrames = 300;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            // Allow initial navigation to establish
            yield return new WaitForSeconds(1.0f);

            // Inject an unexpected wall into the Hub - Bunkhouse door while the crawlers file through it
            var map = resolver.Resolve<Simpiens.Core.Session.SwarmSpawner>().World.Map;
            Assert.AreEqual("Floor1", map.Name, "Dynamic chaos runs on the outpost floor.");
            var hubDoor = Simpiens.Core.Session.Layouts.Floor1Layout.HubBunkhouseDoor;
            var quarryDoor = Simpiens.Core.Session.Layouts.Floor1Layout.Doors[3];
            var injector = resolver.Resolve<EdgeCaseInjector>();
            var spawner = resolver.Resolve<Simpiens.Core.Session.SwarmSpawner>();
            yield return InjectClearOfPawns(injector, spawner, hubDoor, verticalWall: false);

            // Allow unstuck reflexes to engage and recover
            yield return new WaitForSeconds(1.0f);

            // Inject a second obstacle into the Ore Quarry - Hub door to test cascading clearance
            yield return InjectClearOfPawns(injector, spawner, quarryDoor, verticalWall: true);
        }

        private static IEnumerator InjectClearOfPawns(EdgeCaseInjector injector, Simpiens.Core.Session.SwarmSpawner spawner, Vec2 door, bool verticalWall)
        {
            if (injector == null) yield break;
            for (int frame = 0; frame < MaxWaitFrames; frame++)
            {
                for (int s = 0; s < DoorSpots.Length; s++)
                {
                    var offset = DoorSpots[s];
                    var spot = door + (verticalWall ? new Vec2(offset.Y, offset.X) : offset);
                    if (IsClearOfPawns(spawner, spot))
                    {
                        injector.InjectWall(spot);
                        Debug.Log($"[DynamicChaosScenario] wall at {spot} (door {door}); nearest pawn {NearestPawnDistance(spawner, spot):F2} away");
                        yield break;
                    }
                }
                yield return null;
            }
            Assert.Fail($"No spot in the door at {door} was clear of pawns for {MaxWaitFrames} frames.");
        }

        private static float NearestPawnDistance(Simpiens.Core.Session.SwarmSpawner spawner, Vec2 spot)
        {
            float best = float.MaxValue;
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state != null) best = SimMath.Min(best, Vec2.Distance(state.Position, spot));
            }
            return best;
        }

        private static bool IsClearOfPawns(Simpiens.Core.Session.SwarmSpawner spawner, Vec2 spot)
        {
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state != null && Vec2.Distance(state.Position, spot) < PawnClearance) return false;
            }
            return true;
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            // Allow up to 2 transient unstuck reflexes during dynamic wall injection, but zero persistent jitter loops
            Assert.LessOrEqual(detector.JitterCount, 2, 
                $"Excessive jitter detected during dynamic obstacle recovery! Jitter incidents: {detector.JitterCount}");

            Assert.AreEqual(0, detector.PenetrationCount, 
                $"Pawn clipped into dynamically injected wall! Penetrations: {detector.PenetrationCount}");

            Assert.AreEqual(0, detector.BoundaryBreachCount, 
                $"Pawn breached boundary during evasion! Breaches: {detector.BoundaryBreachCount}");
        }
    }
}
