using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Utility considerations: allocation-free scoring helpers that fold an agent's personality into goal
    /// priorities, prices and target choice (review §3.2, §2.7). GOAP keeps sequencing; these decide how much an
    /// agent wants something and which target it prefers.
    /// Invariant: every trait enters as <see cref="TraitFactor"/>, which is exactly 1 for a neutral trait, so a
    /// neutral personality leaves pre-personality priorities unchanged.
    /// </summary>
    public static class Considerations
    {
        /// <summary>The session's economy tuning as seen by cognition (published on the world snapshot).</summary>
        public static EconomyTuning Economy(in AgentContext context)
        {
            return context.Snapshot?.Economy ?? EconomyTuning.Default;
        }

        // Trait strengths (minor design decisions, 2026-10-11; tune with the economy decisions)
        public const float GreedOnPrice = 0.5f;        // greedy pays up to 50% less / asks up to 50% more
        public const float GreedOnReward = 0.5f;       // bounty reward weight ±50%
        public const float DiligenceOnWork = 0.3f;     // income/bounty goal priority ±30%
        public const float DiligenceOnDistance = 0.5f; // distance cost ∓50%
        public const float GreedOnIncome = 0.3f;       // income/liquidation goal priority ±30%
        public const float SociabilityOnSocial = 0.4f; // socialize/hearth priority ±40%
        public const float LoyaltyOnCompliance = 0.4f; // directive compliance priority ±40%

        /// <summary>1 + trait × strength. Trait in [-1, 1]; neutral (0) always yields exactly 1.</summary>
        public static float TraitFactor(float trait, float strength)
        {
            return 1f + trait * strength;
        }

        /// <summary>
        /// Highest price per ration this agent will pay right now: the session's reference price (EconomyTuning, 20g by
        /// default), raised by hunger (×0.5 when sated up to ×1.5 when starving) and lowered by greed. Neutral at hunger
        /// 50 with the default tuning: 20g; range across personalities and hunger: 5g–45g.
        /// </summary>
        public static ushort RationReservationPrice(in AgentContext context)
        {
            float urgency = 0.5f + Mathf.Clamp(context.Hunger, 0f, 100f) / 100f;
            float price = Economy(context).RationReferencePrice * urgency * TraitFactor(context.Personality.Greed, -GreedOnPrice);
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(price), 0, ushort.MaxValue);
        }

        /// <summary>
        /// Lowest price per unit this agent accepts when selling salvage: the item's base value, raised by greed.
        /// Neutral: ore 8g, herb 12g (seeded stall buy prices are 20g / 25g, so neutral behaviour is unchanged).
        /// </summary>
        // --- Rest anchor (Epic MAP Phase 3) ---
        /// <summary>Energy per second while asleep in a bunk (resting in place: 15).</summary>
        public const float BunkRecoveryPerSecond = 30f;

        /// <summary>Energy added when a bunk sleep completes (resting in place: 30).</summary>
        public const float BunkSleepBonus = 40f;

        /// <summary>Below this energy a crawler rests where it stands rather than walking to a bunk.</summary>
        public const float TooTiredToWalkEnergy = 15f;

        /// <summary>Extra cost of resting in place while a free bunk is known and the agent can still walk.</summary>
        public const float FieldRestPenalty = 6f;

        /// <summary>Distance (units) that adds one to the cost of walking to a bunk.</summary>
        public const float BunkDistancePerCost = 25f;

        /// <summary>Walking to a bunk: cheap nearby, dearer far away, but under resting in place across one floor.</summary>
        public static float BunkTravelCost(float baseCost, float distance)
        {
            return baseCost * 0.5f + distance / BunkDistancePerCost;
        }

        /// <summary>Below this energy a crawler is tired and wants rest (RestGoal).</summary>
        public const float TiredEnergy = 40f;

        public static float FieldRestCost(in AgentContext context, float baseCost)
        {
            if (context.Energy <= TooTiredToWalkEnergy) return baseCost;
            return BunkTargeting.TryGetNearestFreeBunk(context, out _, out _) ? baseCost + FieldRestPenalty : baseCost;
        }

        /// <summary>Relaxing at the hearth: priced like resting in place while tired, at base cost otherwise.</summary>
        public static float HearthRestCost(in AgentContext context, float baseCost)
        {
            return context.Energy < TiredEnergy ? FieldRestCost(context, baseCost) : baseCost;
        }

        public static ushort MinimumSalePrice(in AgentContext context, ItemType item)
        {
            float price = Economy(context).BaseValue(item) * TraitFactor(context.Personality.Greed, GreedOnPrice);
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(price), 0, ushort.MaxValue);
        }

        /// <summary>
        /// Desirability of a bounty: greed-weighted reward over a distance cost (agent → target → redemption stall)
        /// that diligent agents mind less. Higher is better; only compared between bounties for one agent.
        /// </summary>
        public static float BountyScore(in AgentContext context, in BountyContract bounty, Vector2 stallPosition)
        {
            float reward = bounty.RewardGold * TraitFactor(context.Personality.Greed, GreedOnReward);
            Vector2 target = bounty.TargetLocation != default ? bounty.TargetLocation : stallPosition;
            float distance = Vector2.Distance(context.Position, target) + Vector2.Distance(target, stallPosition);
            float distanceCost = TraitFactor(context.Personality.Diligence, -DiligenceOnDistance);
            return reward / (10f + distance * distanceCost);
        }
    }
}
