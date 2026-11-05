using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    public class IdleIntentHandler : IntentHandlerBase<IdleIntent>
    {
        protected override IntentResult Execute(WorldEntity node, IdleIntent idle, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            state.ElapsedTime += context.FixedTickDeltaTime;

            // Social interaction: Gossip when idling near other agents
            if (agent == null || !agent.IsExhaustionCollapsed)
            {
                context.CheckGossipOpportunity(node);
            }

            if (agent != null && !agent.IsGossiping)
            {
                if (agent.IsExhaustionCollapsed || (agent.HasActivePlan && agent.ActivePlan?.CurrentAction is Simpiens.Cognition.Planning.Actions.RestAction))
                {
                    agent.VisualState = AgentVisualState.Resting;
                }
                else if (agent.HasActivePlan && agent.ActivePlan?.CurrentAction is Simpiens.Cognition.Planning.Actions.HarvestResourceAction)
                {
                    agent.VisualState = AgentVisualState.Harvesting;
                }
                else
                {
                    agent.VisualState = AgentVisualState.Idle;
                }
            }

            // Resting where it stands is sleeping rough: up at the session's wake lines, as from a bunk (Epic 10 Part 2, T2.2)
            bool resting = agent != null && !agent.IsDowned && agent.HasActivePlan && agent.ActivePlan?.CurrentAction is Simpiens.Cognition.Planning.Actions.RestAction;
            if (state.ElapsedTime >= idle.Duration || (resting && context.Economy.Day.Wakes(agent.Energy, agent.Hunger)))
            {
                return IntentResult.Success;
            }
            return IntentResult.InProgress;
        }
    }
}
