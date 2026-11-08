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
        /// <summary>Seconds a unit takes; <see cref="PaceDuration"/> (the default) takes the session's pace for the node's item (Epic 10 Part 2, T2.4).</summary>
        public readonly float GatherDuration;
        public const float PaceDuration = -1f;

        /// <summary>Units to harvest in place before completing (salvage gathering for multi-unit bounties).</summary>
        public readonly int TargetUnits;

        // Outcome fields — written only by HarvestResourceIntentHandler on the main simulation thread,
        // read by the consequence service after completion.
        public ItemType YieldedItem { get; set; }
        public int UnitsHarvested { get; set; }

        public HarvestResourceIntent(EntityId agentId, EntityId targetEntityId, PathResponse path, float gatherDuration = PaceDuration, int targetUnits = 1) : base(agentId)
        {
            TargetEntityId = targetEntityId;
            Path = path;
            GatherDuration = gatherDuration;
            TargetUnits = targetUnits < 1 ? 1 : targetUnits;
        }
    }
}
