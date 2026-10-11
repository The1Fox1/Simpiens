namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Why an agent chose its current intent: the producing tier and the three highest-priority GOAP goals considered
    /// (review §3.4 "Why?" inspector groundwork; the UI is out of scope). Built by the cognition task on the intent it
    /// owns, copied to AgentState on the main thread when the intent is applied. Holds only references and numbers.
    /// </summary>
    public readonly struct DecisionSummary
    {
        /// <summary>Which tier produced the intent (e.g. "GOAP new plan", "MentalBreakEvaluator", "Tier-3 fallback").</summary>
        public readonly string Source;
        public readonly uint Tick;

        public readonly string Goal1;
        public readonly float Priority1;
        public readonly string Goal2;
        public readonly float Priority2;
        public readonly string Goal3;
        public readonly float Priority3;

        public DecisionSummary(string source, uint tick, string goal1, float priority1, string goal2, float priority2, string goal3, float priority3)
        {
            Source = source;
            Tick = tick;
            Goal1 = goal1;
            Priority1 = priority1;
            Goal2 = goal2;
            Priority2 = priority2;
            Goal3 = goal3;
            Priority3 = priority3;
        }

        public bool IsEmpty => Source == null;
    }
}
