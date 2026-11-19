namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Drill a game hour at the drill yard <see cref="StationIndex"/> (Epic 11, S3.4, D10), walking there on <see cref="Path"/>
    /// first. <see cref="Lots"/> counts the lots of practice given, work in progress.
    /// </summary>
    public class DrillIntent : AgentIntent
    {
        public DrillIntent(EntityId agentId, int stationIndex, Simpiens.Cognition.Pathfinding.PathResponse path) : base(agentId)
        {
            StationIndex = stationIndex;
            Path = path;
        }

        public int StationIndex { get; }
        public Simpiens.Cognition.Pathfinding.PathResponse Path { get; }
        public int Lots { get; set; }
    }
}
