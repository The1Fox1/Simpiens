namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Eat a ration the agent carries. Its effect (hunger relief, the ration used up) is applied by the consequence
    /// service on success, whoever chose it: a GOAP plan (<c>EatCarriedFoodAction</c>) or a hunger reflex. Before
    /// Epic MAP Phase 5 eating was an idle intent that fed the agent only as a plan step, so a starving crawler with
    /// a ration in its pocket panicked instead of eating.
    /// </summary>
    public class EatCarriedFoodIntent : AgentIntent
    {
        public const float DefaultDuration = 1.0f;

        public readonly float Duration;

        public EatCarriedFoodIntent(EntityId agentId, float duration = DefaultDuration) : base(agentId)
        {
            Duration = duration;
        }
    }
}
