namespace Simpiens.Intervention
{
    /// <summary>
    /// A data object representing a player input or external intervention.
    /// Used in the Command pattern to queue state changes rather than mutating directly.
    /// </summary>
    public readonly struct InfluenceEvent
    {
        public readonly string SourceId;
        public readonly string CommandType;
        public readonly string Payload;

        public InfluenceEvent(string sourceId, string commandType, string payload)
        {
            SourceId = sourceId;
            CommandType = commandType;
            Payload = payload;
        }
    }
}
