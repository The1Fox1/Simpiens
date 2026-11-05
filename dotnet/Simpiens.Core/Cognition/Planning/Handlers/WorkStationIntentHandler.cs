using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a WorkStationIntent (Epic 10 Part 2, W1.3, D13): the artisan walks to its station on its path, then each
    /// tick it stands within the station's reach it does a tick of the station's work (<see cref="Simpiens.Simulation.Workshops.Crafting"/>),
    /// posed at work, tiring at the station's rate (the day's <c>EnergyCraftingPerSecond</c>, D15). It ends after a game hour
    /// at the station, so the artisan decides afresh, or as soon as it is off shift: its block over, a break, the order done.
    /// </summary>
    public class WorkStationIntentHandler : IntentHandlerBase<WorkStationIntent>
    {
        protected override IntentResult Execute(WorldEntity node, WorkStationIntent work, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var order = agent != null ? agent.Station : default;
            if (!order.IsActive || order.StationIndex != work.StationIndex)
            {
                if (agent != null && agent.VisualState == AgentVisualState.Harvesting) agent.VisualState = AgentVisualState.Idle;
                return IntentResult.Success; // off shift: its work is done for now
            }

            // The walk there first (nothing worked yet), and only as far as the station's reach
            float reach = order.Radius * order.Radius;
            if (state.ElapsedTime <= 0f && (order.Position - node.Position).SqrMagnitude > reach)
            {
                if (work.Path == null) return IntentResult.PathBlocked;
                var walked = context.ExecutePathMovement(node, work.Path, state);
                if (walked != IntentResult.Success) return walked;
                if ((order.Position - node.Position).SqrMagnitude > reach) return IntentResult.PathBlocked;
            }

            if (agent != null)
            {
                agent.VisualState = AgentVisualState.Harvesting; // the harvest's swing reads as the hammer's
                agent.WorkedStation = true;
            }
            context.WorkStation(work.StationIndex);
            state.ElapsedTime += context.FixedTickDeltaTime;
            if (state.ElapsedTime < DayClock.SecondsPerHour) return IntentResult.InProgress;
            if (agent != null) agent.VisualState = AgentVisualState.Idle;
            return IntentResult.Success;
        }
    }
}
