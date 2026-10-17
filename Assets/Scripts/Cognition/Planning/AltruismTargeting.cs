using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Shared selection of a distressed companion to aid, used by the Tier-1 AltruismEvaluator and by the
    /// GOAP AidCompanionGoal / ShareFoodAction so reflex and plan agree on who (if anyone) needs help.
    /// Allocation-free (thread-static scratch list); ties break by EntityId for determinism.
    /// </summary>
    public static class AltruismTargeting
    {
        public const float AltruismThreshold = 25f;
        public const float SatedHungerThreshold = 50f;
        public const float DistressHungerThreshold = 85f;
        public const float PerceptionRadius = 6.0f;

        [System.ThreadStatic]
        private static List<EntitySnapshot> _peerResults;

        /// <summary>
        /// Selects the perceived peer (within PerceptionRadius, in line of sight) who is starving (hunger ≥ 85)
        /// or collapsed and whose altruism score clears the threshold. The helper itself must be sated (&lt; 50).
        /// </summary>
        public static bool TrySelectDistressedPeer(in AgentContext context, out EntityId peerId, out Vector2 peerPosition, out float utility)
        {
            peerId = default;
            peerPosition = default;
            utility = -1f;

            if (context.Hunger >= SatedHungerThreshold) return false;
            if (context.Snapshot == null || context.Memory == null) return false;

            var results = _peerResults ??= new List<EntitySnapshot>(16);
            results.Clear();
            context.Snapshot.GetEntitiesInRange(context.Position, PerceptionRadius, results);

            bool found = false;
            int count = results.Count;
            for (int i = 0; i < count; i++)
            {
                var peer = results[i];
                if (peer.Id == context.AgentId || peer.Type != EntityType.Pawn) continue;

                // Agents cannot perceive distressed peers through walls
                if (!context.Snapshot.HasLineOfSight(context.Position, peer.Position)) continue;

                bool isDistressed = peer.Hunger >= DistressHungerThreshold || peer.IsCollapsed;
                if (!isDistressed) continue;

                float score = context.Memory.CalculateAltruismScore(peer.Id, context.CurrentTick, context.Hunger, peer.IsCollapsed);
                if (score < AltruismThreshold) continue;

                if (!found || score > utility || (score == utility && peer.Id.CompareTo(peerId) < 0))
                {
                    found = true;
                    utility = score;
                    peerId = peer.Id;
                    peerPosition = peer.Position;
                }
            }

            return found;
        }
    }
}
