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
            // Its quarry, if it names one (Epic 11, S7.4, D34): a named creature living in that lair
            if (cmd.Quarry != default && (!Creatures.TryGet(cmd.Quarry, out var quarry) || !quarry.IsAlive || !quarry.IsNamed || quarry.LairIndex != cmd.LairIndex))
            {
                SimLog.Intervention.ZLogWarning($"Hunt rejected: {cmd.Quarry} isn't a named creature living in lair {cmd.LairIndex}");
                return;
            }
            var heart = lairs[cmd.LairIndex].Definition.Position;
            var hunt = new PostBountyCommand(cmd.BountyId, BountyType.Hunt, (byte)GetSectorForPosition(heart), ItemType.None, 1, cmd.RewardGold,
                cmd.DurationTicks, heart, cmd.CreatorStallId, cmd.LairIndex, cmd.Quarry);
            _market.PostBounty(in hunt, currentTick);
        }

        /// <summary>The open hunts' parties (Epic 10 Part 3, K6.1), by bounty id.</summary>
        public IReadOnlyList<HuntRecord> Hunts => _market.Hunts;

        /// <summary>The open hunt <paramref name="bountyId"/>'s party (K6.1), with its quarry (Epic 11, S7.4).</summary>
        public bool TryGetHunt(ushort bountyId, out HuntRecord hunt) => _market.TryGetHunt(bountyId, out hunt);

        /// <summary>The living creature of lair <paramref name="lairIndex"/> nearest <paramref name="from"/> (K6.2), the lowest id on a tie.</summary>
        public bool TryGetNearestLairCreature(int lairIndex, Vec2 from, out Simpiens.Simulation.Creatures.CreatureState creature)
        {
            creature = null;
            var lairs = Creatures.Lairs;
            if (lairIndex < 0 || lairIndex >= lairs.Count) return false;
            var lair = lairs[lairIndex];
            float best = float.MaxValue;
            for (int slot = 0; slot < lair.Pack; slot++)
            {
                var id = lair.MemberIn(slot);
                if (id == default || !Creatures.TryGet(id, out var c) || !c.IsAlive) continue;
                float d = (c.Position - from).SqrMagnitude;
                if (d > best || (d == best && creature != null && c.Id.CompareTo(creature.Id) >= 0)) continue;
                best = d;
                creature = c;
            }
            return creature != null;
        }

        /// <summary>A claim on a hunt joins its party while it has room (K6.1); true when the crawler is in it.</summary>
        public bool TryJoinHunt(ushort bountyId, EntityId crawler) => _market.TryJoinHunt(bountyId, crawler, CurrentTick);

        private void ExecuteCancelBountyCommand(in CancelBountyCommand cmd, uint currentTick) => _market.CancelBounty(cmd.BountyId, currentTick);

        private void ExecuteUpdateStallPricesCommand(in UpdateStallPricesCommand cmd) => _market.UpdateStallPrices(in cmd);
    }
}
