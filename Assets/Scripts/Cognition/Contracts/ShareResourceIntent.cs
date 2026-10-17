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
        public readonly float TransferDuration;
        public readonly float FoodAmount;

        public ShareResourceIntent(EntityId agentId, EntityId targetPawnId, PathResponse path, float transferDuration = 1.0f, float foodAmount = 40f) : base(agentId)
        {
            TargetPawnId = targetPawnId;
            Path = path;
            TransferDuration = transferDuration;
            FoodAmount = foodAmount;
        }
    }
}
