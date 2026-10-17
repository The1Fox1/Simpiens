using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Share food with a distressed companion to alleviate their starvation or collapse.
    /// DesiredState: HasAidedPeer = true.
    /// Only valid while a perceived companion actually needs aid (see <see cref="AltruismTargeting"/>);
    /// priority is that companion's altruism score (Warmth, Trust and distress).
    /// </summary>
    public class AidCompanionGoal : GoapGoal
    {
        public AidCompanionGoal() : base("AidCompanion", WorldState.Empty.With(StateFact.HasAidedPeer, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            return AltruismTargeting.TrySelectDistressedPeer(context, out _, out _, out float utility) ? utility : -1f;
        }

        public override bool IsValid(AgentContext context)
        {
            // Sated helper (< 50 hunger) and a companion in distress within view
            return AltruismTargeting.TrySelectDistressedPeer(context, out _, out _, out _);
        }
    }
}
