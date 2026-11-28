using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// One crawler a seat (Epic PLACES plan 1, F.6): a seat's claim is its first arrival's; a second is refused, and its holder
    /// may claim it again; a claim lasts only while its holder is at a spell on that seat, and goes with a crawler leaving the
    /// outpost. Runs as a test (ValidatorTests).
    /// </summary>
    public class SeatingValidator
    {
        private static readonly EntityId Ama = new EntityId(5u, 0u, 0u, 5u), Bram = new EntityId(6u, 0u, 0u, 6u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[SeatingValidator] Beginning seating validation...");
            ValidateClaims();
            ValidateWhileSeated();
            TestLog.Log("<color=green>[SeatingValidator] ALL 2 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateClaims()
        {
            var seating = new Seating();
            Assert(seating.TryClaim(0, 1, Ama), "The first to arrive claims the seat.");
            Assert(!seating.TryClaim(0, 1, Bram), "A second is refused.");
            Assert(seating.TryClaim(0, 1, Ama), "Its holder may claim it again.");
            Assert(seating.TryClaim(0, 2, Bram) && seating.TryClaim(1, 1, Bram), "Another seat, or another venue's, is free.");
            Assert(seating.TryGetHolder(0, 1, out var holder) && holder == Ama && !seating.TryGetHolder(0, 3, out _), "Who holds which.");
            seating.ReleaseAllForAgent(Bram);
            Assert(seating.Count == 1 && !seating.TryGetHolder(0, 2, out _), "A crawler leaving the outpost lets go of its seats.");
            TestLog.Log("[PASS] Test 1: A seat's claim is its first arrival's; a second refused; released on leaving.");
        }

        private void ValidateWhileSeated()
        {
            var seating = new Seating();
            seating.TryClaim(0, 1, Ama);
            seating.TryClaim(0, 2, Bram);
            var active = new Dictionary<EntityId, ActiveIntentState>
            {
                [Ama] = new ActiveIntentState { Intent = new RecreateIntent(Ama, 0, Activity.Ale, 1, null) },
                [Bram] = new ActiveIntentState { Intent = new IdleIntent(Bram, 1f) },
            };
            seating.Validate(active);
            Assert(seating.TryGetHolder(0, 1, out _) && !seating.TryGetHolder(0, 2, out _), "A claim stays while its holder sits there, and goes when it does something else.");
            active[Ama] = new ActiveIntentState { Intent = new RecreateIntent(Ama, 0, Activity.Ale, 3, null) };
            seating.Validate(active);
            Assert(!seating.TryGetHolder(0, 1, out _), "And when it sits at another seat.");
            TestLog.Log("[PASS] Test 2: A claim lasts only while its holder is at a spell on that seat.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[SeatingValidator] FAILED: " + message);
        }
    }
}
