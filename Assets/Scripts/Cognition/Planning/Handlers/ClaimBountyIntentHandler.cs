using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Handlers
{
    public class ClaimBountyIntentHandler : IntentHandlerBase<ClaimBountyIntent>
    {
        protected override IntentResult Execute(NodeController node, ClaimBountyIntent claim, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (!context.TryGetBounty(claim.BountyId, out var bounty))
            {
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} claim of bounty #{claim.BountyId} rejected: bounty not found");
                return IntentResult.TargetMissing;
            }

            uint currentClaimTick = context.CurrentTick;
            if (!bounty.IsActive(currentClaimTick) || bounty.Status != BountyStatus.Open)
            {
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} claim of bounty #{claim.BountyId} rejected: status {bounty.Status}, claimant {SimLog.Agent(bounty.ClaimantId)}, expires t{bounty.ExpirationTick}");
                return IntentResult.Failed;
            }

            bounty = bounty.WithClaimant(node.Id, BountyStatus.Claimed);
            context.UpdateBounty(bounty);
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} claimed bounty #{bounty.BountyId} ({bounty.Type} {bounty.TargetItemType}x{bounty.TargetQuantity}, sector {bounty.TargetSectorId}, {bounty.RewardGold}g)");

            context.ChronicleService?.RecordEvent(new ChronicleEvent(
                ChronicleEventType.BountyClaimed,
                node.Id,
                context.PlayerEntityId,
                context.GetSectorForPosition(node.Position),
                currentClaimTick,
                ChronicleIntensity.Notable));

            return IntentResult.Success;
        }
    }
}
