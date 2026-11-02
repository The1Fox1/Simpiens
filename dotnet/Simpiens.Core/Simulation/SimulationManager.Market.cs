using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Simulation.Economy;

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

        private void ExecutePostBountyCommand(in PostBountyCommand cmd, uint currentTick) => _market.PostBounty(in cmd, currentTick);

        private void ExecuteCancelBountyCommand(in CancelBountyCommand cmd, uint currentTick) => _market.CancelBounty(cmd.BountyId, currentTick);

        private void ExecuteUpdateStallPricesCommand(in UpdateStallPricesCommand cmd) => _market.UpdateStallPrices(in cmd);
    }
}
