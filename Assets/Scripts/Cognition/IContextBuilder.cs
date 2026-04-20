namespace Simpiens.Cognition
{
    public interface IContextBuilder
    {
        /// <summary>
        /// Aggregates an agent's local environment and memory into a payload
        /// suitable for Model Context Protocol (MCP) or general LLM ingestion.
        /// </summary>
        string BuildContextPayload(string agentId);
    }
}
