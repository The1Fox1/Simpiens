using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Intervention;
using Simpiens.Simulation.Economy;
using ZLogger;

namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        public IReadOnlyList<TradeStallData> ActiveStalls => _market.ActiveStalls;
        public IReadOnlyList<BountyContract> ActiveBounties => _market.ActiveBounties;

        public bool TryGetStall(EntityId stallId, out TradeStallData stall) => _market.TryGetStall(stallId, out stall);

        public bool TryGetTradeStall(EntityId stallId, out TradeStallData stallData) => _market.TryGetStall(stallId, out stallData);

        public void UpdateStall(TradeStallData stall) => _market.UpdateStall(stall);

        public void UpdateBounty(BountyContract bounty) => _market.UpdateBounty(bounty);

        public void RegisterTradeStall(TradeStallData stallData) => _market.RegisterTradeStall(stallData);

        public void RegisterBounty(BountyContract bounty) => _market.RegisterBounty(bounty);

        public bool TryRegisterBounty(BountyContract bounty) => _market.TryRegisterBounty(bounty);

        public bool TryGetBounty(ushort bountyId, out BountyContract bounty) => _market.TryGetBounty(bountyId, out bounty);

        private void ExecutePostBountyCommand(in PostBountyCommand cmd, uint currentTick)
        {
            if (cmd.Type != BountyType.Hunt)
            {
                _market.PostBounty(in cmd, currentTick);
                return;
            }
            // A hunt (Epic 10 Part 3, K6.1, Q7): on a charted lair that holds a creature; its sector and heart are the lair's
            var lairs = Creatures.Lairs;
            if (cmd.LairIndex >= lairs.Count || !Creatures.IsCharted(cmd.LairIndex) || lairs[cmd.LairIndex].Alive == 0)
            {
                SimLog.Intervention.ZLogWarning($"Hunt rejected: lair {cmd.LairIndex} isn't a charted lair with a creature in it");
                return;
            }
            var heart = lairs[cmd.LairIndex].Definition.Position;
            var hunt = new PostBountyCommand(cmd.BountyId, BountyType.Hunt, (byte)GetSectorForPosition(heart), ItemType.None, 1, cmd.RewardGold,
                cmd.DurationTicks, heart, cmd.CreatorStallId, cmd.LairIndex);
            _market.PostBounty(in hunt, currentTick);
        }

        /// <summary>The open hunts' parties (Epic 10 Part 3, K6.1), by bounty id.</summary>
        public IReadOnlyList<HuntRecord> Hunts => _market.Hunts;

        /// <summary>A claim on a hunt joins its party while it has room (K6.1); true when the crawler is in it.</summary>
        public bool TryJoinHunt(ushort bountyId, EntityId crawler) => _market.TryJoinHunt(bountyId, crawler, CurrentTick);

        private void ExecuteCancelBountyCommand(in CancelBountyCommand cmd, uint currentTick) => _market.CancelBounty(cmd.BountyId, currentTick);

        private void ExecuteUpdateStallPricesCommand(in UpdateStallPricesCommand cmd) => _market.UpdateStallPrices(in cmd);
    }
}
