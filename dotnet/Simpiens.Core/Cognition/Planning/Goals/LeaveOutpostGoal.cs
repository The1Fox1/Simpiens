using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning.Actions;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: a drifter whose stay is over leaves the outpost by the Road Gate (Epic 10 G1).
    /// DesiredState: HasLeftOutpost = true.
    /// Priority: above every need short of starvation, so a drifter leaves soon after its stay ends; a starving one
    /// eats first (hunger's priority is the hunger itself).
    /// </summary>
    public class LeaveOutpostGoal : GoapGoal
    {
        public const float Priority = 95f;

        public LeaveOutpostGoal() : base("LeaveOutpost", WorldState.Empty.With(StateFact.HasLeftOutpost, true))
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context) => context.StayIsOver && LeaveOutpostAction.TryGetGate(context, out _);
    }
}
