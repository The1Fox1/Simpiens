namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// What one agent has done this session (Epic MAP Phase 5): plain counters recorded by the simulation where each
    /// act is applied, on the main thread. Diagnostic only: nothing in cognition reads it. The lifecycle scenario
    /// proves the outpost's day from it; a crawler card can show it later.
    /// A tick field holds -1 until the act first happens.
    /// </summary>
    public struct AgentActivity
    {
        public int SalvageHarvests;
        public int SalvageSales;
        public int BountyDeliveries;
        public int RationPurchases;
        public int Meals;
        public int BunkSleeps;
        public int HearthGatherings;

        /// <summary>Harvest → sell cycles: a salvage sale or Gather delivery after at least one salvage harvest.</summary>
        public int Cycles;

        public long FirstHarvestTick;
        public long FirstCycleTick;
        public long FirstMealTick;
        public long FirstSleepTick;
        public long FirstGatheringTick;

        private bool _harvestedSinceSale;

        /// <summary>Danger (Epic 10 G2): ticks spent in a hazard sector, times downed, herbs eaten for health.</summary>
        public int HazardTicks;
        public int TimesDowned;
        public int HerbsEaten;
        public int Retreats;

        public static AgentActivity Empty => new AgentActivity
        {
            FirstHarvestTick = -1,
            FirstCycleTick = -1,
            FirstMealTick = -1,
            FirstSleepTick = -1,
            FirstGatheringTick = -1
        };

        public void RecordSalvageHarvest(uint tick)
        {
            SalvageHarvests++;
            _harvestedSinceSale = true;
            if (FirstHarvestTick < 0) FirstHarvestTick = tick;
        }

        public void RecordSalvageSale(uint tick)
        {
            SalvageSales++;
            CloseCycle(tick);
        }

        public void RecordBountyDelivery(uint tick)
        {
            BountyDeliveries++;
            CloseCycle(tick);
        }

        public void RecordRationPurchase() => RationPurchases++;

        public void RecordMeal(uint tick)
        {
            Meals++;
            if (FirstMealTick < 0) FirstMealTick = tick;
        }

        public void RecordBunkSleep(uint tick)
        {
            BunkSleeps++;
            if (FirstSleepTick < 0) FirstSleepTick = tick;
        }

        public void RecordHearthGathering(uint tick)
        {
            HearthGatherings++;
            if (FirstGatheringTick < 0) FirstGatheringTick = tick;
        }

        private void CloseCycle(uint tick)
        {
            if (!_harvestedSinceSale) return;
            _harvestedSinceSale = false;
            Cycles++;
            if (FirstCycleTick < 0) FirstCycleTick = tick;
        }

        public override string ToString() =>
            $"harvests {SalvageHarvests}, sales {SalvageSales}, deliveries {BountyDeliveries}, cycles {Cycles} (first t{FirstCycleTick}), " +
            $"purchases {RationPurchases}, meals {Meals} (first t{FirstMealTick}), sleeps {BunkSleeps}, hearth {HearthGatherings}" +
            (HazardTicks > 0 || TimesDowned > 0 || HerbsEaten > 0 ? $", hazard {HazardTicks / 10}s, retreats {Retreats}, downed {TimesDowned}, herbs {HerbsEaten}" : string.Empty);
    }
}
