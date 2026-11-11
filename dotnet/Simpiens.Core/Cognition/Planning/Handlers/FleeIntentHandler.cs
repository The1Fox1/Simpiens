using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Runs a struck crawler along its flight (Epic 10 Part 3, K2.3, D19), shown panicking; it is safe, and stops, once it
    /// stands clear of every lair's reach and nothing has struck it for <see cref="Vitality.FlightSeconds"/>, or at the end
    /// of its way.
    /// </summary>
    public class FleeIntentHandler : IntentHandlerBase<FleeIntent>
    {
        protected override IntentResult Execute(WorldEntity node, FleeIntent flee, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent != null && !Vitality.StruckRecently(agent.StruckAtTick, context.CurrentTick) && Vitality.ClearOfLairs(context.Map, node.Position))
                return IntentResult.Success;
            if (flee.Path == null || !flee.Path.IsValid) return IntentResult.TargetMissing;
            var result = context.ExecutePathMovement(node, flee.Path, state);
            if (result == IntentResult.InProgress && agent != null) agent.VisualState = AgentVisualState.Panicking;
            return result;
        }
    }
}
