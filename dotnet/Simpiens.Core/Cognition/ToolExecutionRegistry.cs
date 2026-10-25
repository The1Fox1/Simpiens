using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Model Context Protocol (MCP) ready execution registry bridging cognitive tool calls
    /// to typed simulation intents dispatched to ISimulationManager.
    /// </summary>
    public class ToolExecutionRegistry : IToolExecutionRegistry
    {
        private readonly ISimulationManager _simulationManager;
        private readonly Dictionary<string, Action<EntityId, string>> _customActions = new Dictionary<string, Action<EntityId, string>>(StringComparer.OrdinalIgnoreCase);

        public ToolExecutionRegistry(ISimulationManager simulationManager = null)
        {
            _simulationManager = simulationManager;
        }

        public void RegisterActionHandler(string actionType, Action<EntityId, string> handler)
        {
            if (string.IsNullOrEmpty(actionType)) throw new ArgumentNullException(nameof(actionType));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _customActions[actionType] = handler;
        }

        public void ExecuteAction(string agentIdStr, string actionType, string parameters)
        {
            if (string.IsNullOrEmpty(agentIdStr) || string.IsNullOrEmpty(actionType)) return;

            EntityId agentId;
            if (!EntityId.TryParse(agentIdStr, out agentId))
            {
                // Fallback for non-hex test identifiers
                agentId = default;
            }

            if (_customActions.TryGetValue(actionType, out var customHandler))
            {
                customHandler.Invoke(agentId, parameters);
                return;
            }

            if (_simulationManager == null) return;

            switch (actionType.ToLowerInvariant())
            {
                case "wander":
                case "move_to":
                    _simulationManager.EnqueueIntent(new WanderIntent(agentId, new PathResponse()));
                    break;

                case "panic":
                    _simulationManager.EnqueueIntent(new PanicIntent(agentId, new PathResponse()));
                    break;

                case "rest":
                case "idle":
                    float duration = 1.0f;
                    if (!string.IsNullOrEmpty(parameters)) float.TryParse(parameters, out duration);
                    _simulationManager.EnqueueIntent(new IdleIntent(agentId, duration > 0 ? duration : 1.0f));
                    break;

                case "gather":
                case "harvest":
                    if (!string.IsNullOrEmpty(parameters) && EntityId.TryParse(parameters.Trim(), out var targetResId))
                    {
                        _simulationManager.EnqueueIntent(new HarvestResourceIntent(agentId, targetResId, new PathResponse()));
                    }
                    break;

                case "share_food":
                    if (!string.IsNullOrEmpty(parameters) && EntityId.TryParse(parameters.Trim(), out var targetPawnId))
                    {
                        _simulationManager.EnqueueIntent(new ShareResourceIntent(agentId, targetPawnId, new PathResponse()));
                    }
                    break;

                case "claim_bounty":
                    if (!string.IsNullOrEmpty(parameters) && ushort.TryParse(parameters.Trim(), out ushort bountyId))
                    {
                        _simulationManager.EnqueueIntent(new ClaimBountyIntent(agentId, bountyId));
                    }
                    break;

                default:
                    SimLog.Cognition.ZLogWarning($"ToolExecutionRegistry: unknown or unhandled actionType '{actionType}'");
                    break;
            }
        }
    }
}
