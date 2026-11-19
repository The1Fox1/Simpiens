using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 11, S3.3, D11): reads the manual it carries by the hearth, two game hours, at practice's source Manual; the
    /// manual is used up. Preconditions: HasManual = true, AtAnchorLocation = true. Effects: HasManual = false.
    /// </summary>
    public class ReadManualAction : GoapAction
    {
        public ReadManualAction(float cost = 1.0f) : base("ReadManual", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.HasManual, true).With(StateFact.AtAnchorLocation, true);
            Effects = WorldState.Empty.With(StateFact.HasManual, false);
        }

        public override AgentIntent CreateIntent(AgentContext context) => new ReadManualIntent(context.AgentId);
    }
}
