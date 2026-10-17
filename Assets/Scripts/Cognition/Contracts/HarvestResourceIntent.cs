using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to move towards and harvest a specific resource.
    /// </summary>
    public class HarvestResourceIntent : AgentIntent
    {
        public readonly EntityId TargetEntityId;
        public readonly PathResponse Path;
        public readonly float GatherDuration;

        /// <summary>Units to harvest in place before completing (salvage gathering for multi-unit bounties).</summary>
        public readonly int TargetUnits;

        // Outcome fields — written only by HarvestResourceIntentHandler on the main simulation thread,
        // read by the consequence service after completion.
        public ItemType YieldedItem { get; set; }
        public int UnitsHarvested { get; set; }

        public HarvestResourceIntent(EntityId agentId, EntityId targetEntityId, PathResponse path, float gatherDuration = 1.5f, int targetUnits = 1) : base(agentId)
        {
            TargetEntityId = targetEntityId;
            Path = path;
            GatherDuration = gatherDuration;
            TargetUnits = targetUnits < 1 ? 1 : targetUnits;
        }
    }
}
