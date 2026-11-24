using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 11, S3.4, Q10, D10): a guild member with no orders, fed and rested, drills at the guildhall's yard in its free
    /// hours, a game hour at a time (<see cref="Drill.WouldDrill"/>). DesiredState: Drilled = true, reached through Drill.
    /// </summary>
    public class DrillGoal : GoapGoal
    {
        /// <summary>Above earning's floor (25) and reading (27), under selling salvage (30): a member's free hours, once its purse allows.</summary>
        public const float Priority = 28f;

        public DrillGoal() : base("Drill", WorldState.Empty.With(StateFact.Drilled, true), Motive.Growth, ActivityFamily.Leisure)
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context) => Drill.WouldDrill(context);
    }
}
