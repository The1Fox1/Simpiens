using Simpiens.Diagnostics;
using Simpiens.Simulation.Economy;
using ZLogger;
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
            if (Melee.TryQuaff(node, context)) return IntentResult.InProgress; // a draught in place of a swing (K10.6, D69)
            if (Ranged.IsShooter(agent))
            {
                // A shooter keeps its distance (Epic 10 Part 3, K8.2b); it lets a creature go that runs out of its range
                if (creature.Mode == CreatureMode.Flee && Vec2.Distance(node.Position, creature.Position) > Ranged.RangeOf(agent) + Vitality.FightPursuit) return IntentResult.Success;
                return Ranged.Engage(node, creature, context);
            }
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
        /// <summary>
        /// A draught in a fight (Epic 10 Part 3, K10.6, D69; Q13: "a boss needs a party, draughts and armour"): hurt below the
        /// treat line and holding a made medicine, a crawler whose swing is due drinks it in place of the swing, at once
        /// (physical time, as a swing is; a herb is eaten out of the fight, over its minutes). True when it drank.
        /// </summary>
        public static bool TryQuaff(WorldEntity node, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent?.Inventory == null || agent.Health >= Vitality.TreatWoundsBelow) return false;
            uint tick = context.CurrentTick;
            if (!CombatRules.CanSwing(tick, agent.NextSwingAt)) return false;
            var medicines = Simpiens.Simulation.Items.ItemCatalog.InCategory(Simpiens.Simulation.Items.ItemCategory.Medicine);
            for (int i = 0; i < medicines.Count; i++)
            {
                var medicine = medicines[i];
                if (medicine.IsSalvage() || !agent.Inventory.HasItem(medicine)) continue;
                float before = agent.Health;
                if (!ItemUse.Consume(agent, medicine)) continue;
                agent.Activity.DraughtsDrunk++;
                agent.NextSwingAt = CombatRules.NextSwingAt(tick, Combatant.ForCrawler(agent, node.Radius)); // it costs the swing
                SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} drank {Simpiens.Simulation.Items.ItemCatalog.NameOf(medicine)} in the fight: health {before:F0} -> {agent.Health:F0}");
                return true;
            }
            return false;
        }

        /// <summary>In reach: posed defiant, it asks for a swing (true). Out of reach: posed defiant, nothing done (false).</summary>
        public static bool TrySwing(WorldEntity node, CreatureState creature, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var me = Combatant.ForCrawler(agent, node.Radius);
            var them = Combatant.ForCreature(creature.Definition, creature.Id, creature.Position, creature.Health, creature.Rank);
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
            var them = Combatant.ForCreature(creature.Definition, creature.Id, creature.Position, creature.Health, creature.Rank);
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
    /// A shooter's turn against a creature (Epic 10 Part 3, K8.2b, D46), shared by the fight and the hunt (and so the party's
    /// fight): in range with a clear line it asks the creatures' pass for a shot; it steps back while the creature is inside its
    /// keep-away line (<see cref="CombatRules.KeepAway"/>), toward it while out of range or without a line, and otherwise holds. A step back that
    /// walls or crawlers block turns aside, or it stands and shoots.
    /// </summary>
    internal static class Ranged
    {
        /// <summary>True when what the crawler wields has a range.</summary>
        public static bool IsShooter(AgentState agent) => RangeOf(agent) > 0f;

        public static float RangeOf(AgentState agent) => agent?.Inventory == null ? 0f : Simpiens.Simulation.Items.CrawlerStats.Get(agent.Inventory, Simpiens.Simulation.Items.StatKind.Range);

        public static IntentResult Engage(WorldEntity node, CreatureState creature, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var me = Combatant.ForCrawler(agent, node.Radius);
            var them = Combatant.ForCreature(creature.Definition, creature.Id, creature.Position, creature.Health, creature.Rank);
            agent.VisualState = AgentVisualState.Defiant;
            bool clear = context.HasLineOfSight(node.Position, creature.Position);
            if (clear && CombatRules.InRange(me, them)) context.SwingAtCreature(node, creature.Id);
            float gap = CombatRules.Gap(me, them);
            float keep = CombatRules.KeepAway(me.Range, them.Reach);
            float step = (float)(node.BaseSpeed * context.FixedTickDeltaTime);
            if (gap < keep)
            {
                var away = (node.Position - creature.Position).Normalized;
                if (away == Vec2.Zero) away = Vec2.Up;
                StepAside(node, away, SimMath.Min(step, (float)(keep - gap)), context);
                return IntentResult.InProgress;
            }
            if (!clear || gap > (float)(me.Range * CombatRules.CloseShare))
            {
                float close = clear ? (float)(gap - me.Range * CombatRules.CloseShare) : step;
                var toward = (creature.Position - node.Position).Normalized;
                context.TryDisplacePawn(node, node.Position + toward * SimMath.Min(step, close), respectPawns: true); // blocked: it waits
            }
            return IntentResult.InProgress;
        }

        // Straight back, else turned 45 and then 90 degrees either way; it stays where it is when all are blocked
        private static void StepAside(WorldEntity node, Vec2 away, float step, IIntentExecutionContext context)
        {
            if (context.TryDisplacePawn(node, node.Position + away * step, respectPawns: true)) return;
            for (int turn = 1; turn <= 2; turn++)
            {
                float angle = turn * 0.7853982f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float a = angle * side;
                    float cos = SimMath.Cos(a), sin = SimMath.Sin(a);
                    var turned = new Vec2((float)(away.X * cos - away.Y * sin), (float)(away.X * sin + away.Y * cos));
                    if (context.TryDisplacePawn(node, node.Position + turned * step, respectPawns: true)) return;
                }
            }
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
                if (Melee.TryQuaff(node, context)) return IntentResult.InProgress; // a draught in place of a swing (K10.6, D69)
                if (Ranged.IsShooter(agent)) return Ranged.Engage(node, creature, context); // from range, keeping its distance (K8.2b)
                if (Melee.TrySwing(node, creature, context)) return IntentResult.InProgress;
                return Melee.StepToward(node, creature, context, besideOthers: true);
            }
            var result = context.ExecutePathMovement(node, hunt.Path, state);
            return result == IntentResult.Success ? IntentResult.InProgress : result;
        }
    }
}
