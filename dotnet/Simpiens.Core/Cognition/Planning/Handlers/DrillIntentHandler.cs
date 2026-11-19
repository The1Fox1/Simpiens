using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Skills;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a DrillIntent (Epic 11, S3.4, Q10, D10): the member walks to the drill yard on its path, then drills there a
    /// game hour, posed at a swing, practising the fight work of what it wields at source Yard, to the yard's ceiling (5):
    /// 30 experience a game hour, in six lots, each multiplied by its passion. A station is never pulled down, so a yard once
    /// built stands. Main thread.
    /// </summary>
    public class DrillIntentHandler : IntentHandlerBase<DrillIntent>
    {
        protected override IntentResult Execute(WorldEntity node, DrillIntent drill, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var stations = context.Map?.Stations;
            if (agent == null || stations == null || (uint)drill.StationIndex >= (uint)stations.Count) return IntentResult.TargetMissing;
            var yard = stations[drill.StationIndex];

            // The walk there first (nothing drilled yet), and only as far as the yard's reach
            float reach = yard.Radius * yard.Radius;
            if (state.ElapsedTime <= 0f && (yard.Position - node.Position).SqrMagnitude > reach)
            {
                if (drill.Path == null) return IntentResult.PathBlocked;
                var walked = context.ExecutePathMovement(node, drill.Path, state);
                if (walked != IntentResult.Success) return walked;
                if ((yard.Position - node.Position).SqrMagnitude > reach) return IntentResult.PathBlocked;
            }

            agent.VisualState = AgentVisualState.Harvesting; // the harvest's swing reads as a blow at the post
            agent.LastDrilledAt = context.CurrentTick;
            state.ElapsedTime += context.FixedTickDeltaTime;
            int due = (int)(state.ElapsedTime * SkillRules.YardLotsPerHour / DayClock.SecondsPerHour);
            while (drill.Lots < due && drill.Lots < SkillRules.YardLotsPerHour)
            {
                drill.Lots++;
                context.Practise(agent, Drill.WorkOf(agent.Inventory), SkillRules.YardExperiencePerHour / SkillRules.YardLotsPerHour,
                    new PracticeSource(PracticeKind.Yard, SkillRules.YardCeiling));
            }
            if (state.ElapsedTime < DayClock.SecondsPerHour) return IntentResult.InProgress;
            agent.VisualState = AgentVisualState.Idle;
            return IntentResult.Success;
        }
    }
}
