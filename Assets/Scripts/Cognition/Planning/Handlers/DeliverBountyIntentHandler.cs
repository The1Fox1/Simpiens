using UnityEngine;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Redeems a claimed bounty at an outpost stall.
    /// Gather bounties hand over TargetItemType × TargetQuantity; Scout bounties hand over the scout report
    /// recorded by ScoutIntentHandler and mark the target sector explored.
    /// The reward is paid only by releasing the bounty's escrow through the ledger, so no gold is minted.
    /// </summary>
    public class DeliverBountyIntentHandler : IntentHandlerBase<DeliverBountyIntent>
    {
        protected override IntentResult Execute(NodeController node, DeliverBountyIntent deliver, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (!context.TryGetBounty(deliver.BountyId, out var delBounty))
            {
                return Reject(node, deliver, "bounty not found", IntentResult.TargetMissing);
            }

            if (delBounty.Status != BountyStatus.Claimed || delBounty.ClaimantId != node.Id)
            {
                return Reject(node, deliver, "bounty not claimed by this agent");
            }

            if (!context.TryGetStall(deliver.StallEntityId, out var delStall))
            {
                if (context.ActiveStalls.Count > 0)
                {
                    delStall = context.ActiveStalls[0];
                }
                else
                {
                    return Reject(node, deliver, "no stall available", IntentResult.TargetMissing);
                }
            }

            float distToDelStall = Vector2.Distance(node.Position, delStall.Position);
            if (distToDelStall > delStall.InteractionRadius)
            {
                return Reject(node, deliver, "out of stall interaction range");
            }

            var agent = node.Agent;
            var agentState = node.AgentState;
            if (agent == null || agent.Inventory == null)
            {
                return Reject(node, deliver, "agent has no inventory");
            }

            bool isScout = delBounty.Type == BountyType.ScoutSector;

            // 1. Verify the deliverable before touching any state
            if (isScout)
            {
                if (agentState == null || agentState.ScoutReportBountyId != delBounty.BountyId)
                {
                    return Reject(node, deliver, "no scout report for this bounty");
                }
            }
            else if (!agent.Inventory.HasItem(delBounty.TargetItemType, delBounty.TargetQuantity))
            {
                return Reject(node, deliver, "missing bounty items");
            }

            // Escrow must exist; un-escrowed bounties are rejected at registration (no minting)
            if (delBounty.RewardGold > 0 && context.EconomyLedger != null && !context.EconomyLedger.HasEscrow(delBounty.BountyId))
            {
                SimLog.Economy.ZLogError($"Bounty #{delBounty.BountyId} has no escrow at delivery; refusing to mint {delBounty.RewardGold}g");
                return Reject(node, deliver, "reward not in escrow");
            }

            uint currentDeliverTick = context.CurrentTick;

            // 2. Hand over the deliverable
            if (isScout)
            {
                agentState.ScoutReportBountyId = 0;
                context.MarkSectorExplored(delBounty.TargetSectorId);
                context.PublishSurvey(node.Id, delBounty.TargetSectorId);
            }
            else
            {
                if (!agent.Inventory.TryRemoveItem(delBounty.TargetItemType, delBounty.TargetQuantity))
                {
                    return Reject(node, deliver, "inventory removal failed");
                }

                if (delBounty.TargetItemType == ItemType.IronOre)
                {
                    delStall = delStall.WithOreStock((ushort)(delStall.OreStock + delBounty.TargetQuantity));
                }
                else if (delBounty.TargetItemType == ItemType.MedicinalHerb)
                {
                    delStall = delStall.WithHerbStock((ushort)(delStall.HerbStock + delBounty.TargetQuantity));
                }
                context.UpdateStall(delStall);
            }

            // 3. Authoritative escrow release
            uint reward = delBounty.RewardGold;
            if (context.EconomyLedger != null)
            {
                context.EconomyLedger.TryReleaseEscrowToClaimant(delBounty.BountyId, node.Id, null, out reward);
            }
            agent.Inventory.AddGold((int)reward);

            delBounty = delBounty.WithStatus(BountyStatus.Completed);
            context.UpdateBounty(delBounty);
            if (!isScout && agentState != null) agentState.Activity.RecordBountyDelivery(currentDeliverTick);
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} fulfilled bounty #{delBounty.BountyId} ({delBounty.Type}) for {reward}g at {SimLog.Agent(delStall.StallEntityId)}; player trust +15");

            // Trade-to-Affinity boost toward Player (+15 Trust, +10 Warmth, +10 Respect)
            agent.Memory?.RecordInteraction(context.PlayerEntityId, deltaWarmth: 10, deltaTrust: 15, deltaFear: 0, deltaRespect: 10, currentDeliverTick);

            context.ChronicleService?.RecordEvent(new ChronicleEvent(
                ChronicleEventType.BountyFulfilled,
                node.Id,
                context.PlayerEntityId,
                context.GetSectorForPosition(node.Position),
                currentDeliverTick,
                ChronicleIntensity.Dramatic,
                (int)reward));

            agent.VisualState = AgentVisualState.Idle;
            return IntentResult.Success;
        }

        private static IntentResult Reject(NodeController node, DeliverBountyIntent deliver, string reason, IntentResult result = IntentResult.Failed)
        {
            SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} delivery of bounty #{deliver.BountyId} rejected: {reason}");
            return result;
        }
    }
}
