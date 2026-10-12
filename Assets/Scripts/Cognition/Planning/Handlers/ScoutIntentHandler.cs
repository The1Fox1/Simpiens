using UnityEngine;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a ScoutIntent: follows the path toward the bounty's target sector and succeeds once the agent stands
    /// inside it at its survey point (or at the end of its route inside it), recording a scout report on the agent
    /// (main thread, single writer). The report is redeemed at a stall by the DeliverBountyIntentHandler, which also
    /// publishes what the scout saw there (fog of war).
    /// </summary>
    public class ScoutIntentHandler : IntentHandlerBase<ScoutIntent>
    {
        /// <summary>
        /// The scout has visited once this close to the sector's target location (its anchor or centre). Tight enough
        /// that vision (6) from there covers a Floor 1 room: at 2.0 a scout counted its visit near the doorway side and
        /// charted only 2 of the Deep Seam's 3 veins.
        /// </summary>
        public const float SurveyRadius = 1.25f;

        protected override IntentResult Execute(NodeController node, ScoutIntent scout, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (!context.TryGetBounty(scout.BountyId, out var bounty))
            {
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} scout of bounty #{scout.BountyId} rejected: bounty not found");
                return IntentResult.TargetMissing;
            }

            if (bounty.Type != BountyType.ScoutSector || bounty.Status != BountyStatus.Claimed || bounty.ClaimantId != node.Id)
            {
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} scout of bounty #{scout.BountyId} rejected: {bounty.Type}/{bounty.Status}, claimant {SimLog.Agent(bounty.ClaimantId)}");
                return IntentResult.Failed;
            }

            var agent = node.AgentState;

            // A visit means reaching the sector's survey point, from where the scout can see the room, not just
            // crossing its threshold (fog of war: what it sees there is the survey it reports, Epic MAP Phase 3)
            bool inSector = context.GetSectorForPosition(node.Position) == scout.TargetSectorId;
            bool hasRoute = scout.Path != null && scout.Path.IsValid;
            bool atSurveyPoint = bounty.TargetLocation == default || Vector2.Distance(node.Position, bounty.TargetLocation) <= SurveyRadius;
            if (inSector && (!hasRoute || atSurveyPoint))
            {
                if (agent != null)
                {
                    agent.ScoutReportBountyId = scout.BountyId;
                    agent.ScoutReportTick = context.CurrentTick;
                    agent.VisualState = AgentVisualState.Idle;
                }
                SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} scouted sector {scout.TargetSectorId} for bounty #{scout.BountyId} at {node.Position}");
                return IntentResult.Success;
            }

            var moveResult = context.ExecutePathMovement(node, scout.Path, state);
            if (moveResult == IntentResult.InProgress)
            {
                if (agent != null) agent.VisualState = AgentVisualState.Walking;
                return IntentResult.InProgress;
            }

            if (moveResult == IntentResult.Success)
            {
                // Path exhausted: one final sector check, otherwise the route never reached the sector
                if (context.GetSectorForPosition(node.Position) == scout.TargetSectorId)
                {
                    if (agent != null)
                    {
                        agent.ScoutReportBountyId = scout.BountyId;
                        agent.ScoutReportTick = context.CurrentTick;
                    }
                    SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} scouted sector {scout.TargetSectorId} for bounty #{scout.BountyId} at {node.Position}");
                    return IntentResult.Success;
                }
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} scout path for bounty #{scout.BountyId} ended at {node.Position} (sector {context.GetSectorForPosition(node.Position)}) outside target sector {scout.TargetSectorId}");
                return IntentResult.PathBlocked;
            }

            return moveResult;
        }
    }
}
