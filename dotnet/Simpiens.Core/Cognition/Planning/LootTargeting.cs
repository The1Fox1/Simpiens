using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Loot;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Which pile a crawler would go and pick over (Epic 10 Part 3, K4.3d), from the snapshot's copies (<see cref="PileView"/>):
    /// one it sees (within its vision, in line of sight, in a charted place), that it may take from (its own, or past its
    /// victor's claim), where it would go alone (<see cref="Vitality.WouldEnterAt(in AgentContext, Vec2)"/>: a known lair it
    /// doesn't brave keeps it out; but a victor's own drop at hand, <see cref="AtHand"/>, it takes fog or lair), holding something it would take
    /// (<see cref="LootRules"/>). The nearest, then the lowest id. Read off the main thread; allocates nothing.
    /// </summary>
    public static class LootTargeting
    {
        /// <summary>How far a crawler sees a pile: its vision, as it sees everything else.</summary>
        public const float Sight = AgentRuntime.DefaultVisionRadius;

        /// <summary>How close it stands to pick a pile over.</summary>
        public const float Reach = 0.8f;

        /// <summary>
        /// Within this of its own drop, a victor takes it even in a known lair or an uncharted sector (D24): it fought there and
        /// stands by it. A victor hurt by its fight no longer braves the lair, and without this walked off from its pile a step
        /// away; a stray who slew a rat in the uncharted Overgrowth didn't see its own drop.
        /// </summary>
        public const float AtHand = 3f;

        /// <summary>
        /// True when its own drop lies at hand (<see cref="AtHand"/>, in line of sight, its claim running) holding something it
        /// would take: a hurt victor picks it up before it goes for a herb (the danger reflex holds off, D24).
        /// </summary>
        public static bool OwnDropAtHand(in AgentContext context)
        {
            var snapshot = context.Snapshot;
            if (snapshot == null || snapshot.ActivePileCount == 0 || context.Inventory == null || context.IsDowned) return false;
            var piles = snapshot.Piles;
            for (int i = 0; i < piles.Length; i++)
            {
                ref readonly var pile = ref piles[i];
                if (pile.ClaimedFor != context.AgentId || context.CurrentTick >= pile.ClaimUntil) continue;
                if ((pile.Position - context.Position).SqrMagnitude > AtHand * AtHand || !snapshot.HasLineOfSight(context.Position, pile.Position)) continue;
                if (LootRules.WouldTakeAny(context.Inventory, pile)) return true;
            }
            return false;
        }

        public static bool TryChoosePile(in AgentContext context, out PileView chosen)
        {
            chosen = default;
            var snapshot = context.Snapshot;
            if (snapshot == null || snapshot.ActivePileCount == 0 || context.Inventory == null || context.IsDowned) return false;
            var piles = snapshot.Piles;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < piles.Length; i++)
            {
                ref readonly var pile = ref piles[i];
                float distance = (pile.Position - context.Position).SqrMagnitude;
                if (distance > Sight * Sight || distance > best) continue;
                if (distance == best && found && pile.Id.CompareTo(chosen.Id) >= 0) continue;
                if (!pile.MayTake(context.AgentId, context.CurrentTick)) continue;
                if (!snapshot.HasLineOfSight(context.Position, pile.Position)) continue;
                // Its own drop at hand it knows of, fog or lair (D24); anything else it must see in a charted place it would go
                bool atHand = pile.ClaimedFor == context.AgentId && context.CurrentTick < pile.ClaimUntil && distance <= AtHand * AtHand;
                if (!atHand && (!snapshot.IsCharted(pile.Position) || !Vitality.WouldEnterAt(context, pile.Position))) continue;
                if (!LootRules.WouldTakeAny(context.Inventory, pile)) continue;
                best = distance;
                chosen = pile;
                found = true;
            }
            return found;
        }
    }
}
