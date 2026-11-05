using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// An artisan on shift goes to its station and works it (Epic 10 Part 2, W1.3, D13): the walk on its path, then a tick
    /// of the station's work each tick it stands there, for up to a game hour before it decides afresh.
    /// </summary>
    public class WorkStationIntent : AgentIntent
    {
        /// <summary>The station: its index among the map's stations.</summary>
        public readonly byte StationIndex;
        public readonly PathResponse Path;

        public WorkStationIntent(EntityId agentId, byte stationIndex, PathResponse path) : base(agentId)
        {
            StationIndex = stationIndex;
            Path = path;
        }
    }
}
