namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A bold, armed crawler struck by a creature fights back (Epic 10 Part 3, K3.3, Q6): it closes on the creature and swings
    /// at it, every swing decided in the creatures' pass with theirs (the combat rule's D6), until the creature is slain or
    /// runs, or its own health falls under the retreat line, when it flees. A reflex: it pre-empts whatever it was doing.
    /// </summary>
    public class FightIntent : AgentIntent
    {
        public readonly EntityId Target;

        public FightIntent(EntityId agentId, EntityId target) : base(agentId)
        {
            Target = target;
        }
    }
}
