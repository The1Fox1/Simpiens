using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        public IntentResult ExecutePanicMovement(WorldEntity node, PathResponse path, ref int currentIndex)
            => _locomotion.ExecutePanicMovement(node, path, ref currentIndex);

        public IntentResult ExecutePathMovement(WorldEntity node, PathResponse path, ActiveIntentState state)
            => _locomotion.ExecutePathMovement(node, path, state);

        internal IntentResult ApplyProgressiveUnstuckReflex(WorldEntity node, Vec2 targetPos, ActiveIntentState state)
            => _locomotion.ApplyProgressiveUnstuckReflex(node, targetPos, state);

        public bool TryDisplacePawn(WorldEntity node, Vec2 candidate, bool respectPawns = true)
            => _locomotion.TryDisplacePawn(node, candidate, respectPawns);

        internal bool IsCrowdedMove(WorldEntity node, Vec2 candidate)
            => _locomotion.IsCrowdedMove(node, candidate);

        internal bool IsPositionBlocked(Vec2 targetPos, EntityId ignoreAgentId, float pawnClearanceRadius = 0.4f)
            => _locomotion.IsPositionBlocked(targetPos, ignoreAgentId, pawnClearanceRadius);
    }
}
