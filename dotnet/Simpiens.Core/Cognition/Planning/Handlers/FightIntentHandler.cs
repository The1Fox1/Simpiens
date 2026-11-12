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
            if (agent == null || agent.IsDowned || agent.Health < Vitality.FightLine(agent.Expedition)) return IntentResult.Failed;
            if (!context.TryGetCreature(fight.Target, out var creature)) return IntentResult.Success; // slain, or gone
            if (Melee.TrySwing(node, creature, context)) return IntentResult.InProgress;
            float distance = Vec2.Distance(node.Position, creature.Position);
            if (creature.Mode == CreatureMode.Flee && distance > Vitality.FightPursuit) return IntentResult.Success; // it ran: let it go
            return Melee.StepToward(node, creature, context, besideOthers: agent.Expedition.IsActive); // a party fights side by side (K7.2a)
        }
    }

    /// <summary>
    /// A crawler's melee against a creature (Epic 10 Part 3, K3.3, shared by the fight and the hunt, K6.2): in reach, a swing
    /// asked of the creatures' pass; out of it, a step closer, stopping half a reach inside it.
    /// </summary>
    internal static class Melee
    {
        /// <summary>In reach: posed defiant, it asks for a swing (true). Out of reach: posed defiant, nothing done (false).</summary>
        public static bool TrySwing(WorldEntity node, CreatureState creature, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var me = Combatant.ForCrawler(agent, node.Radius);
            var them = Combatant.ForCreature(creature.Definition, creature.Id, creature.Position, creature.Health);
            if (agent != null) agent.VisualState = AgentVisualState.Defiant;
            if (!CombatRules.InReach(me, them)) return false;
            context.SwingAtCreature(node, creature.Id);
            return true;
        }

        /// <summary>
        /// A step toward the creature, as far as half a reach inside it; failed when the step is blocked. With
        /// <paramref name="besideOthers"/> (a hunt's, K6.4) it keeps clear of other crawlers and waits where one is in the way,
        /// so two hunters on one rat don't stand in each other.
        /// </summary>
        public static IntentResult StepToward(WorldEntity node, CreatureState creature, IIntentExecutionContext context, bool besideOthers = false)
        {
            var me = Combatant.ForCrawler(node.Agent, node.Radius);
            var them = Combatant.ForCreature(creature.Definition, creature.Id, creature.Position, creature.Health);
            float step = SimMath.Min((float)(node.BaseSpeed * context.FixedTickDeltaTime), SimMath.Max(0f, (float)(CombatRules.Gap(me, them) - me.Reach * 0.5f)));
            if (step <= 0f) return IntentResult.InProgress;
            var toward = (creature.Position - node.Position).Normalized;
            if (besideOthers)
            {
                context.TryDisplacePawn(node, node.Position + toward * step, respectPawns: true); // blocked by another: it waits
                return IntentResult.InProgress;
            }
            return context.TryDisplacePawn(node, node.Position + toward * step, respectPawns: false) ? IntentResult.InProgress : IntentResult.Failed;
        }
    }

    /// <summary>
    /// Executes a HuntIntent (Epic 10 Part 3, K6.2, Q7): the hunter walks toward its lair's heart on its path, and fights the
    /// lair's nearest living creature as soon as one is within its sight (or its path is walked), one after another, as a
    /// fight does (<see cref="Melee"/>); a member at a clear's fight fights its party's focus instead (K7.2a). Done when the
    /// lair is empty (the hunt pays from the simulation's pass) or after a game hour, to decide afresh; failed under the retreat
    /// line, or downed.
    /// </summary>
    public class HuntIntentHandler : IntentHandlerBase<HuntIntent>
    {
        /// <summary>Within this of one of the lair's creatures, the hunter goes for it.</summary>
        public const float EngageDistance = AgentRuntime.DefaultVisionRadius;

        protected override IntentResult Execute(WorldEntity node, HuntIntent hunt, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent == null || agent.IsDowned || agent.Health < Vitality.FightLine(agent.Expedition)) return IntentResult.Failed;
            if (!context.TryGetNearestLairCreature(hunt.LairIndex, node.Position, out var creature)) return IntentResult.Success; // the lair is empty
            // Under a clear's orders, the party's focus while it lives (Epic 10 Part 3, K7.2a)
            var order = agent.Expedition;
            if (order.IsFighting && order.LairIndex == hunt.LairIndex && order.Focus != default && context.TryGetCreature(order.Focus, out var focus) && focus.IsAlive) creature = focus;
            state.ElapsedTime += context.FixedTickDeltaTime;
            if (state.ElapsedTime >= DayClock.SecondsPerHour) return IntentResult.Success; // a game hour on: it decides afresh
            bool walked = hunt.Path == null || state.CurrentWaypointIndex >= hunt.Path.Length;
            if (walked || (creature.Position - node.Position).SqrMagnitude <= EngageDistance * EngageDistance)
            {
                if (Melee.TrySwing(node, creature, context)) return IntentResult.InProgress;
                return Melee.StepToward(node, creature, context, besideOthers: true);
            }
            var result = context.ExecutePathMovement(node, hunt.Path, state);
            return result == IntentResult.Success ? IntentResult.InProgress : result;
        }
    }
}
