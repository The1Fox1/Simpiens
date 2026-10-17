namespace Simpiens.Narrative
{
    /// <summary>
    /// Strategy interface for translating structured ChronicleEvent records into humanized narrative prose.
    /// </summary>
    public interface INarrativeFormatter
    {
        /// <summary>
        /// Translates a single chronicle event into player-facing narrative story prose.
        /// </summary>
        string FormatEvent(in ChronicleEvent evt);
    }
}
