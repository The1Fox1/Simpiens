using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Seek out companions to converse and fulfill social needs.
    /// DesiredState: HasExchangedGossip = true.
    /// Priority: Scales inversely with current agent Social drive (100 - Social).
    /// </summary>
    public class SocializeGoal : GoapGoal
    {
        public SocializeGoal() : base("Socialize", WorldState.Empty.With(StateFact.HasExchangedGossip, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            // The more isolated (lower social score), the higher the priority
            float priority = 100f - context.Social;

            // Relational modifier: If agent knows warm or trusted companions, socializing is more appealing
            if (context.Memory != null && context.Memory.AffinityMap != null)
            {
                foreach (var kvp in context.Memory.AffinityMap)
                {
                    var record = context.Memory.GetAffinity(kvp.Key, context.CurrentTick);
                    if (record.Warmth >= 20 || record.Trust >= 20)
                    {
                        priority += 10f;
                        break;
                    }
                }
            }

            return priority;
        }

        public override bool IsValid(AgentContext context)
        {
            // Only consider socializing when social drive falls below threshold
            return context.Social < 50f;
        }
    }
}
