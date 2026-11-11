using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Creatures;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Fights a creature (Epic 10 Part 3, K3.3): out of reach, the crawler steps toward it; in reach, it asks the creatures'
    /// pass for a swing (decided there with every creature's, D6). Done when the creature is slain or gone, or runs past
    /// <see cref="Vitality.FightPursuit"/>; failed when the crawler's health falls under the retreat line or it is downed
    /// (it flees, or limps out), or it can't step closer.
    /// </summary>
    public class FightIntentHandler : IntentHandlerBase<FightIntent>
    {
        protected override IntentResult Execute(WorldEntity node, FightIntent fight, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent == null || agent.IsDowned || agent.Health < Vitality.RetreatBelow) return IntentResult.Failed;
            if (!context.TryGetCreature(fight.Target, out var creature)) return IntentResult.Success; // slain, or gone
            var kind = creature.Definition;
            var me = Combatant.ForCrawler(agent, node.Radius);
            var them = Combatant.ForCreature(kind, creature.Id, creature.Position, creature.Health);
            agent.VisualState = AgentVisualState.Defiant;
            if (CombatRules.InReach(me, them))
            {
                context.SwingAtCreature(node, creature.Id);
                return IntentResult.InProgress;
            }
            float distance = Vec2.Distance(node.Position, creature.Position);
            if (creature.Mode == CreatureMode.Flee && distance > Vitality.FightPursuit) return IntentResult.Success; // it ran: let it go
            float step = SimMath.Min((float)(node.BaseSpeed * context.FixedTickDeltaTime), SimMath.Max(0f, (float)(CombatRules.Gap(me, them) - me.Reach * 0.5f)));
            if (step <= 0f) return IntentResult.InProgress;
            var toward = (creature.Position - node.Position).Normalized;
            return context.TryDisplacePawn(node, node.Position + toward * step, respectPawns: false) ? IntentResult.InProgress : IntentResult.Failed;
        }
    }
}
