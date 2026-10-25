using System.Threading;
using System.Threading.Tasks;

namespace Simpiens.Narrative
{
    /// <summary>
    /// Stage 2 extensible asynchronous narrative enrichment contract.
    /// Provides the architectural integration bridge for Track A Epic 11 (MCP / LLM inference)
    /// to enrich high-intensity story beats without modifying the UI presentation layer.
    /// </summary>
    public interface ILlmNarrativeEnricher
    {
        /// <summary>
        /// Indicates whether an active LLM provider or local inference pipeline is connected.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Asynchronously enriches a chronicle event into rich, evocative contextual prose.
        /// </summary>
        ValueTask<string> EnrichStoryBeatAsync(ChronicleEvent evt, CancellationToken ct = default);
    }

    /// <summary>
    /// Default zero-cost passthrough enricher used during MVP.
    /// Returns the deterministic template synthesis when no LLM provider is active.
    /// </summary>
    public class PassthroughNarrativeEnricher : ILlmNarrativeEnricher
    {
        private readonly INarrativeFormatter _formatter;

        public bool IsAvailable => false;

        public PassthroughNarrativeEnricher() : this(null)
        {
        }

        public PassthroughNarrativeEnricher(INarrativeFormatter formatter = null)
        {
            _formatter = formatter ?? new TemplateNarrativeFormatter();
        }

        public ValueTask<string> EnrichStoryBeatAsync(ChronicleEvent evt, CancellationToken ct = default)
        {
            return new ValueTask<string>(_formatter.FormatEvent(evt));
        }
    }
}
