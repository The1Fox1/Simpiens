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
        public readonly GUID NewLeaderId;
        public readonly GUID PreviousLeaderId;
        public readonly GUID ContenderBId;
        public readonly byte SplinterGroupId;
        public readonly byte NewCohesion;

        public SuccessionResult(
            byte groupId,
            SuccessionMode mode,
            bool succeeded,
            bool loserDeparted,
            GUID newLeaderId,
            GUID previousLeaderId,
            GUID contenderBId,
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

        public static readonly SuccessionResult Failed = new SuccessionResult(
            0, SuccessionMode.Peaceful, false, false, default, default, default, 0, 0);

        public static SuccessionResult Peaceful(byte groupId, GUID newLeaderId, GUID previousLeaderId, byte newCohesion)
        {
            return new SuccessionResult(groupId, SuccessionMode.Peaceful, true, false, newLeaderId, previousLeaderId, default, 0, newCohesion);
        }

        public static SuccessionResult Dominance(byte groupId, GUID winnerId, GUID loserId, bool loserDeparted, GUID previousLeaderId, byte newCohesion)
        {
            return new SuccessionResult(groupId, SuccessionMode.DominanceChallenge, true, loserDeparted, winnerId, previousLeaderId, loserId, 0, newCohesion);
        }

        public static SuccessionResult Schism(byte groupId, byte splinterGroupId, GUID leaderAId, GUID leaderBId, GUID previousLeaderId, byte newCohesion)
        {
            return new SuccessionResult(groupId, SuccessionMode.FactionalSchism, true, false, leaderAId, previousLeaderId, leaderBId, splinterGroupId, newCohesion);
        }
    }
}
