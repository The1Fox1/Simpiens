using Simpiens.Cognition.Pathfinding;
namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to move towards a distressed companion and share food resources with them.
    /// </summary>
    public class ShareResourceIntent : AgentIntent
    {
        public readonly EntityId TargetPawnId;
        public readonly PathResponse Path;
        /// <summary>Seconds the hand-over takes; <see cref="PaceDuration"/> (the default) takes the session's pace (Epic 10 Part 2, T2.4).</summary>
        public readonly float TransferDuration;
        public readonly float FoodAmount;
        public const float PaceDuration = -1f;

        public ShareResourceIntent(EntityId agentId, EntityId targetPawnId, PathResponse path, float transferDuration = PaceDuration, float foodAmount = 40f) : base(agentId)
        {
            TargetPawnId = targetPawnId;
            Path = path;
            TransferDuration = transferDuration;
            FoodAmount = foodAmount;
        }
    }
}
