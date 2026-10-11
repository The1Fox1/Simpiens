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
        /// <summary>Reference ration value a neutral agent at hunger 50 will pay (seeded stall price is 15g).</summary>
        public const float RationReferencePrice = 20f;

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
        /// Highest price per ration this agent will pay right now. Hunger raises willingness (×0.5 when sated up to
        /// ×1.5 when starving); greed lowers it. Neutral at hunger 50: 20g; range across personalities and hunger: 5g–45g.
        /// </summary>
        public static ushort RationReservationPrice(in AgentContext context)
        {
            float urgency = 0.5f + Mathf.Clamp(context.Hunger, 0f, 100f) / 100f;
            float price = RationReferencePrice * urgency * TraitFactor(context.Personality.Greed, -GreedOnPrice);
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(price), 0, ushort.MaxValue);
        }

        /// <summary>
        /// Lowest price per unit this agent accepts when selling salvage: the item's base value, raised by greed.
        /// Neutral: ore 8g, herb 12g (seeded stall buy prices are 20g / 25g, so neutral behaviour is unchanged).
        /// </summary>
        public static ushort MinimumSalePrice(in AgentContext context, ItemType item)
        {
            float price = item.BaseValue() * TraitFactor(context.Personality.Greed, GreedOnPrice);
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
