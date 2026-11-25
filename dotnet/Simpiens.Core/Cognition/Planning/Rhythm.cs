using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Life;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// What the day's rhythm weighs on work (Epic LIFE-1 plan 3, R.6, D32, D33): a full purse (satiation) and weariness, each a
    /// factor on the Work family's goals in the motive model, exactly 1 for a crawler under its purse target and unwearied, so a
    /// neutral factor leaves every choice as it was. Pure and allocation-free; reads only the context.
    /// </summary>
    public static class Rhythm
    {
        /// <summary>
        /// The purse a crawler works toward (D32): the comfortable purse times its greed's factor, plus what its held wants ask for
        /// (a supper's stew, a round of ale, at the stall's price).
        /// </summary>
        public static int PurseTarget(in AgentContext context) => PurseTarget(Considerations.Economy(context), context.Personality, context.Life, context.Snapshot);

        /// <summary>The purse target from its parts, as the main thread computes it for the day's intention (R.7).</summary>
        public static int PurseTarget(Simpiens.Simulation.Economy.EconomyTuning economy, in AgentPersonality personality, in CrawlerLife life, Simpiens.Simulation.Spatial.SharedWorldSnapshot snapshot)
        {
            economy ??= Simpiens.Simulation.Economy.EconomyTuning.Default;
            float greed = 1f + personality.Greed * economy.Life.Rhythm.PurseGreed;
            int target = (int)(economy.ComfortablePurse * greed);
            if (snapshot == null || snapshot.ActiveStallCount == 0) return target;
            var stall = snapshot.StallBuffer[0];
            for (int i = 0; i < CrawlerLife.WantCapacity; i++)
            {
                var want = life.WantAt(i);
                if (want.Want == 0 || want.Activity == 0) continue;
                var row = ActivityTable.Get((Activity)want.Activity);
                if (row == null || row.Cost == Simpiens.Simulation.Economy.ItemType.None) continue;
                target += stall.SellPriceOf(row.Cost);
            }
            return target;
        }

        /// <summary>
        /// Satiation's weight on work (D32): 1 up to the purse target, falling linearly to the floor at twice the target and
        /// beyond; exactly 1 with no purse, no target or no floor set.
        /// </summary>
        public static float Satiation(in AgentContext context)
        {
            float floor = Considerations.Economy(context).Life.Rhythm.SatiationFloor;
            if (floor <= 0f || context.Inventory == null) return 1f;
            int gold = context.Inventory.Gold;
            int target = PurseTarget(context);
            if (target <= 0 || gold <= target) return 1f;
            float over = System.Math.Min(1f, (gold - target) / (float)target);
            return 1f - (1f - floor) * over;
        }

        /// <summary>Weariness's weight on work (D33): 1 - the pull x weariness / 100; exactly 1 unwearied.</summary>
        public static float Weariness(in AgentContext context)
        {
            float weariness = context.Life.Weariness;
            if (weariness <= 0f) return 1f;
            return 1f - Considerations.Economy(context).Life.Rhythm.WearinessPull * weariness / CrawlerLife.MaxWeariness;
        }

        /// <summary>The rhythm's weight on the Work family's goals: satiation times weariness.</summary>
        public static float OnWork(in AgentContext context) => Satiation(context) * Weariness(context);
    }
}
