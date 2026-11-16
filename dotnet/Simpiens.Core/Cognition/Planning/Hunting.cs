using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Who hunts (Epic 10 Part 3, K6.2, Q7), read off the main thread from the snapshot's bounties and hunts' parties
    /// (<see cref="HuntRecord"/>). A hunter is a resident on its feet and fit (health over the recovery line, not hungry or
    /// worn out), bold and armed, who braves the hunted lair (D21: it expects to clear its smallest pack before its health
    /// falls to the retreat line). It joins the open hunt with room that pays most, then the lowest id. Allocates nothing.
    /// </summary>
    public static class Hunting
    {
        /// <summary>The hunt it is in, if any.</summary>
        public static bool TryGetJoined(in AgentContext context, out HuntRecord hunt)
        {
            hunt = default;
            var snapshot = context.Snapshot;
            if (snapshot == null || snapshot.ActiveHuntCount == 0) return false;
            var hunts = snapshot.Hunts;
            for (int i = 0; i < hunts.Length; i++)
            {
                if (!hunts[i].HasClaimant(context.AgentId)) continue;
                hunt = hunts[i];
                return true;
            }
            return false;
        }

        /// <summary>The open hunt it would join, and its bounty.</summary>
        public static bool TryChoose(in AgentContext context, out HuntRecord chosen, out BountyContract bounty)
        {
            chosen = default;
            bounty = default;
            var snapshot = context.Snapshot;
            if (snapshot == null || snapshot.ActiveHuntCount == 0 || !IsHunter(context)) return false;
            var hunts = snapshot.Hunts;
            bool found = false;
            for (int i = 0; i < hunts.Length; i++)
            {
                var hunt = hunts[i];
                if (hunt.IsFull || hunt.HasClaimant(context.AgentId) || !snapshot.TryGetBounty(hunt.BountyId, out var b)) continue;
                if (b.Status != BountyStatus.Open || !b.IsActive(context.CurrentTick) || !Braves(context, hunt.LairIndex)) continue;
                if (found && (b.RewardGold < bounty.RewardGold || (b.RewardGold == bounty.RewardGold && b.BountyId > bounty.BountyId))) continue;
                chosen = hunt;
                bounty = b;
                found = true;
            }
            return found;
        }

        /// <summary>A resident fit to hunt: on its feet over the recovery line, not hungry or worn out, bold and armed.</summary>
        public static bool IsHunter(in AgentContext context) =>
            !context.IsDrifter && !context.IsDowned && context.Health >= Vitality.RecoveredAt && context.Hunger < 75f && context.Energy > 25f
            && context.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent && Vitality.IsArmed(context.Inventory);

        /// <summary>It braves lair <paramref name="lairIndex"/> (D21).</summary>
        public static bool Braves(in AgentContext context, int lairIndex)
        {
            var map = context.Snapshot?.Map;
            if (map == null || lairIndex < 0 || lairIndex >= map.Lairs.Count) return false;
            return Vitality.BravesLair(map.Lairs[lairIndex], context.Snapshot.IsBossAlive(lairIndex), context.Skills, context.Inventory, context.Health, context.Personality.RiskTolerancePercent, context.IsDowned);
        }
    }
}
