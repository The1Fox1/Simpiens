using UnityEngine;

namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Unmanaged struct recording the outcome of a leadership succession evaluation (Epic 8 Phase 3).
    /// </summary>
    public readonly struct SuccessionResult
    {
        public readonly byte GroupId;
        public readonly SuccessionMode Mode;
        public readonly bool Succeeded;
        public readonly bool LoserDeparted;
        public readonly EntityId NewLeaderId;
        public readonly EntityId PreviousLeaderId;
        public readonly EntityId ContenderBId;
        public readonly byte SplinterGroupId;
        public readonly byte NewCohesion;

        public SuccessionResult(
            byte groupId,
            SuccessionMode mode,
            bool succeeded,
            bool loserDeparted,
            EntityId newLeaderId,
            EntityId previousLeaderId,
            EntityId contenderBId,
            byte splinterGroupId,
            byte newCohesion)
        {
            GroupId = groupId;
            Mode = mode;
            Succeeded = succeeded;
            LoserDeparted = loserDeparted;
            NewLeaderId = newLeaderId;
            PreviousLeaderId = previousLeaderId;
            ContenderBId = contenderBId;
            SplinterGroupId = splinterGroupId;
            NewCohesion = newCohesion;
        }

        /// <summary>
        /// True when the group's leadership actually changed: a new leader, or a schism (which always founds a
        /// splinter group). A succession check that re-confirms the sitting leader changes nothing worth chronicling.
        /// </summary>
        public bool ChangesLeadership => Succeeded && (Mode == SuccessionMode.FactionalSchism || NewLeaderId != PreviousLeaderId);

        public static readonly SuccessionResult Failed = new SuccessionResult(
            0, SuccessionMode.Peaceful, false, false, default, default, default, 0, 0);

        public static SuccessionResult Peaceful(byte groupId, EntityId newLeaderId, EntityId previousLeaderId, byte newCohesion)
        {
            return new SuccessionResult(groupId, SuccessionMode.Peaceful, true, false, newLeaderId, previousLeaderId, default, 0, newCohesion);
        }

        public static SuccessionResult Dominance(byte groupId, EntityId winnerId, EntityId loserId, bool loserDeparted, EntityId previousLeaderId, byte newCohesion)
        {
            return new SuccessionResult(groupId, SuccessionMode.DominanceChallenge, true, loserDeparted, winnerId, previousLeaderId, loserId, 0, newCohesion);
        }

        public static SuccessionResult Schism(byte groupId, byte splinterGroupId, EntityId leaderAId, EntityId leaderBId, EntityId previousLeaderId, byte newCohesion)
        {
            return new SuccessionResult(groupId, SuccessionMode.FactionalSchism, true, false, leaderAId, previousLeaderId, leaderBId, splinterGroupId, newCohesion);
        }
    }
}
