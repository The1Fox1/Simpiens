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
        protected override IntentResult Execute(WorldEntity node, ClaimBountyIntent claim, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (!context.TryGetBounty(claim.BountyId, out var bounty))
            {
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} claim of bounty #{claim.BountyId} rejected: bounty not found");
                return IntentResult.TargetMissing;
            }

            uint currentClaimTick = context.CurrentTick;
            if (bounty.Type == BountyType.Hunt) return JoinHunt(node, bounty, state, context); // a party of up to three (K6.1)
            if (!bounty.IsActive(currentClaimTick) || bounty.Status != BountyStatus.Open)
            {
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} claim of bounty #{claim.BountyId} rejected: status {bounty.Status}, claimant {SimLog.Agent(bounty.ClaimantId)}, expires t{bounty.ExpirationTick}");
                return IntentResult.Failed;
            }

            // Signing for it at the board takes its time (D22); another may claim it first
            if (StillWorking(node, state, context, context.Economy.Day.Actions.BoardSeconds)) return IntentResult.InProgress;

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

        // A hunt's claim (Epic 10 Part 3, K6.1): signing at the board, then joining its party while it has room; the
        // contract stays open, its party in the hunt's record
        private static IntentResult JoinHunt(WorldEntity node, in BountyContract bounty, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (!bounty.IsActive(context.CurrentTick) || bounty.Status != BountyStatus.Open) return IntentResult.Failed;
            if (StillWorking(node, state, context, context.Economy.Day.Actions.BoardSeconds)) return IntentResult.InProgress;
            if (!context.TryJoinHunt(bounty.BountyId, node.Id)) return IntentResult.Failed; // full, or gone
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} joined hunt #{bounty.BountyId} (sector {bounty.TargetSectorId}, {bounty.RewardGold}g)");
            context.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.BountyClaimed, node.Id, context.PlayerEntityId,
                context.GetSectorForPosition(node.Position), context.CurrentTick, ChronicleIntensity.Notable));
            return IntentResult.Success;
        }
    }
}
