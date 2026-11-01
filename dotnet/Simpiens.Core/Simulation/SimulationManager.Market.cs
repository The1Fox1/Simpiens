using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        private readonly Dictionary<EntityId, TradeStallData> _stalls = new Dictionary<EntityId, TradeStallData>();
        private readonly List<TradeStallData> _activeStallsBuffer = new List<TradeStallData>(8);
        private readonly Dictionary<ushort, BountyContract> _bounties = new Dictionary<ushort, BountyContract>();
        // The next generated bounty id. Not saved: a load derives it as the highest saved id + 1, which is exact because
        // nothing removes a bounty from _bounties (finished ones stay, and are saved) and every id taken raises it (until
        // the 65,535 ids run out and generation wraps to fill gaps)
        private ushort _nextBountyId = 1;
        private readonly List<BountyContract> _activeBountiesBuffer = new List<BountyContract>(16);
        private readonly List<ushort> _expiredBountiesBuffer = new List<ushort>(16);

        public IReadOnlyList<TradeStallData> ActiveStalls => _activeStallsBuffer;
        public IReadOnlyList<BountyContract> ActiveBounties => _activeBountiesBuffer;

        public bool TryGetStall(EntityId stallId, out TradeStallData stall) => _stalls.TryGetValue(stallId, out stall);

        public void UpdateStall(TradeStallData stall)
        {
            _stalls[stall.StallEntityId] = stall;
            UpdateActiveStallsBuffer();
        }

        public void UpdateBounty(BountyContract bounty)
        {
            _bounties[bounty.BountyId] = bounty;
            UpdateActiveBountiesBuffer();
        }

        public void RegisterTradeStall(TradeStallData stallData)
        {
            _stalls[stallData.StallEntityId] = stallData;
            UpdateActiveStallsBuffer();
        }

        public bool TryGetTradeStall(EntityId stallId, out TradeStallData stallData)
        {
            return _stalls.TryGetValue(stallId, out stallData);
        }

        private void UpdateActiveStallsBuffer()
        {
            _activeStallsBuffer.Clear();
            foreach (var kvp in _stalls)
            {
                _activeStallsBuffer.Add(kvp.Value);
            }
            SortStable(_activeStallsBuffer, (a, b) => a.StallEntityId.CompareTo(b.StallEntityId));
        }

        /// <summary>
        /// Registers a bounty, locking its reward in escrow from the creator stall first.
        /// Bounties whose reward cannot be escrowed are rejected so delivery can never mint gold.
        /// </summary>
        public void RegisterBounty(BountyContract bounty)
        {
            TryRegisterBounty(bounty);
        }

        public bool TryRegisterBounty(BountyContract bounty)
        {
            if (bounty.BountyId == 0)
            {
                SimLog.Intervention.ZLogWarning($"Bounty rejected: BountyId cannot be 0");
                return false;
            }

            // A live bounty's id is never taken over (ROADMAP §1A.2 #19): its escrow and claimant would be lost. A finished
            // bounty's record may be replaced by an explicit id (scripted sessions and validators reuse fixed ids);
            // generated ids never reuse one
            if (_bounties.TryGetValue(bounty.BountyId, out var existing) &&
                (existing.Status == BountyStatus.Open || existing.Status == BountyStatus.Claimed))
            {
                SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: the id is held by a live bounty (status {existing.Status})");
                return false;
            }

            bool isLive = bounty.Status == BountyStatus.Open || bounty.Status == BountyStatus.Claimed;

            // The target must make sense on the loaded map (unknown sectors, scouting a charted one), before any escrow
            var target = isLive ? BountyRules.CheckTarget(bounty.Type, bounty.TargetSectorId, _sectors) : BountyRules.TargetCheck.Ok;
            if (target != BountyRules.TargetCheck.Ok)
            {
                SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: {BountyRules.Describe(target, bounty.TargetSectorId, _sectors)}");
                return false;
            }
            if (isLive && bounty.RewardGold > 0 && _economyLedger != null && !_economyLedger.HasEscrow(bounty.BountyId))
            {
                var stallId = bounty.CreatorStallId;
                if (stallId == default && _activeStallsBuffer.Count > 0)
                {
                    stallId = _activeStallsBuffer[0].StallEntityId;
                }

                if (stallId == default || !_stalls.TryGetValue(stallId, out var stall))
                {
                    SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: no creator stall to escrow its {bounty.RewardGold}g reward");
                    return false;
                }

                if (!_economyLedger.TryLockEscrow(stallId, bounty.BountyId, bounty.RewardGold, ref stall))
                {
                    SimLog.Intervention.ZLogWarning($"Bounty #{bounty.BountyId} rejected: stall treasury {stall.TreasuryGold}g cannot escrow {bounty.RewardGold}g reward");
                    return false;
                }

                _stalls[stallId] = stall;
                UpdateActiveStallsBuffer();

                if (bounty.CreatorStallId != stallId)
                {
                    bounty = new BountyContract(
                        bounty.BountyId, bounty.Type, bounty.TargetSectorId, bounty.TargetItemType, bounty.TargetQuantity,
                        bounty.RewardGold, bounty.IssuedTick, bounty.ExpirationTick, bounty.TargetLocation,
                        bounty.ClaimantId, stallId, bounty.Status);
                }
            }

            if (bounty.TargetLocation == default)
            {
                bounty = bounty.WithTargetLocation(ResolveSectorTarget(bounty.TargetSectorId));
            }

            _bounties[bounty.BountyId] = bounty;
            if (bounty.BountyId >= _nextBountyId)
            {
                _nextBountyId = (ushort)(bounty.BountyId + 1);
            }
            UpdateActiveBountiesBuffer();
            SimLog.Intervention.ZLogInformation($"Bounty #{bounty.BountyId} registered: {bounty.Type} {bounty.TargetItemType}x{bounty.TargetQuantity} sector {bounty.TargetSectorId} at {bounty.TargetLocation}, reward {bounty.RewardGold}g, status {bounty.Status}, expires t{bounty.ExpirationTick}");
            return true;
        }

        /// <summary>
        /// Walkable target point for a sector: its anchor when defined, otherwise its bounds centre.
        /// The A* pathfinder falls back to the nearest walkable cell if the point is obstructed.
        /// </summary>
        private Vec2 ResolveSectorTarget(byte sectorId)
        {
            if (_sectors != null && sectorId != 0 && _sectors.TryGetSector(sectorId, out var sector))
            {
                return sector.TargetPoint;
            }
            return default;
        }

        public bool TryGetBounty(ushort bountyId, out BountyContract bounty)
        {
            return _bounties.TryGetValue(bountyId, out bounty);
        }

        private void UpdateActiveBountiesBuffer()
        {
            _activeBountiesBuffer.Clear();
            foreach (var kvp in _bounties)
            {
                if (kvp.Value.Status == BountyStatus.Open || kvp.Value.Status == BountyStatus.Claimed)
                {
                    _activeBountiesBuffer.Add(kvp.Value);
                }
            }
            SortStable(_activeBountiesBuffer, (a, b) => a.BountyId.CompareTo(b.BountyId));
        }

        private void ExecutePostBountyCommand(in PostBountyCommand cmd, uint currentTick)
        {
            ushort bountyId = cmd.BountyId;
            if (bountyId == 0 && !TryTakeNextFreeBountyId(out bountyId))
            {
                SimLog.Intervention.ZLogWarning($"Bounty rejected: every bounty id is taken");
                return;
            }
            PostBountyInternal(
                bountyId,
                cmd.Type,
                cmd.SectorId,
                cmd.ItemType,
                cmd.Quantity,
                cmd.RewardGold,
                cmd.DurationTicks,
                cmd.TargetLocation,
                cmd.CreatorStallId,
                currentTick);
        }

        // The next free id from _nextBountyId on, skipping 0 and every id on the board; false when all 65,535 are taken
        private bool TryTakeNextFreeBountyId(out ushort id)
        {
            for (int tried = 0; tried <= ushort.MaxValue; tried++)
            {
                ushort candidate = _nextBountyId++;
                if (candidate != 0 && !_bounties.ContainsKey(candidate))
                {
                    id = candidate;
                    return true;
                }
            }
            id = 0;
            return false;
        }

        private void ExecuteCancelBountyCommand(in CancelBountyCommand cmd, uint currentTick)
        {
            CancelBountyInternal(cmd.BountyId, currentTick);
        }

        private void ExecuteUpdateStallPricesCommand(in UpdateStallPricesCommand cmd)
        {
            EntityId targetStallId = cmd.StallId;
            if (targetStallId == default && _activeStallsBuffer.Count > 0)
            {
                targetStallId = _activeStallsBuffer[0].StallEntityId;
            }

            if (targetStallId != default && _stalls.TryGetValue(targetStallId, out var stall))
            {
                // Each pair in order; 0 keeps the current price, and an item the stall doesn't trade is ignored
                var updated = stall;
                for (int i = 0; i < cmd.Count; i++)
                {
                    var price = cmd[i];
                    if (price.Price > 0) updated = updated.WithPrice(price.Item, price.Price);
                }
                _stalls[targetStallId] = updated;
                UpdateActiveStallsBuffer();
                SimLog.Intervention.ZLogInformation($"Stall {SimLog.Agent(targetStallId)} prices updated: {cmd.Count} prices");
            }
        }

        private void PostBountyInternal(
            ushort bountyId,
            BountyType type,
            byte sectorId,
            ItemType itemType,
            byte qty,
            ushort rewardGold,
            uint durationTicks,
            Vec2 targetLocation,
            EntityId creatorStallId,
            uint currentTick)
        {
            if (creatorStallId == default && _activeStallsBuffer.Count > 0)
            {
                creatorStallId = _activeStallsBuffer[0].StallEntityId;
            }

            var resolvedType = type == BountyType.None ? BountyType.GatherResource : type;
            var contract = new BountyContract(
                bountyId,
                resolvedType,
                sectorId,
                // A Gather bounty without an item asks for the first resource (iron ore)
                itemType == ItemType.None && resolvedType == BountyType.GatherResource ? Simpiens.Simulation.Items.ItemCatalog.InCategory(Simpiens.Simulation.Items.ItemCategory.Resource)[0] : itemType,
                qty == 0 ? (byte)1 : qty,
                rewardGold,
                currentTick,
                currentTick + durationTicks,
                targetLocation,
                default,
                creatorStallId,
                BountyStatus.Open);

            // Escrow is locked (or the bounty rejected) inside TryRegisterBounty
            if (!TryRegisterBounty(contract))
            {
                return;
            }

            _chronicleService?.RecordEvent(new ChronicleEvent(
                ChronicleEventType.BountyPosted,
                PlayerEntityId,
                default,
                sectorId,
                currentTick,
                ChronicleIntensity.Notable,
                rewardGold));
        }

        private void CancelBountyInternal(ushort bountyId, uint currentTick)
        {
            if (_bounties.TryGetValue(bountyId, out var bounty))
            {
                if (bounty.Status == BountyStatus.Open || bounty.Status == BountyStatus.Claimed)
                {
                    // Refund escrow gold to stall if recorded in ledger or legacy fallback
                    if (bounty.CreatorStallId != default && _stalls.TryGetValue(bounty.CreatorStallId, out var stall))
                    {
                        if (_economyLedger != null)
                        {
                            if (_economyLedger.TryRefundEscrow(bountyId, bounty.CreatorStallId, ref stall, out _))
                            {
                                _stalls[bounty.CreatorStallId] = stall;
                                UpdateActiveStallsBuffer();
                            }
                        }
                        else
                        {
                            _stalls[bounty.CreatorStallId] = stall.WithTreasury(stall.TreasuryGold + bounty.RewardGold);
                            UpdateActiveStallsBuffer();
                        }
                    }

                    _bounties[bountyId] = bounty.WithStatus(BountyStatus.Cancelled);
                    UpdateActiveBountiesBuffer();
                    SimLog.Intervention.ZLogInformation($"Bounty #{bountyId} cancelled (was {bounty.Status}, claimant {SimLog.Agent(bounty.ClaimantId)}); escrow refunded");

                    _chronicleService?.RecordEvent(new ChronicleEvent(
                        ChronicleEventType.BountyExpired,
                        PlayerEntityId,
                        bounty.ClaimantId,
                        bounty.TargetSectorId,
                        currentTick,
                        ChronicleIntensity.Trivial));
                }
            }
        }
    }
}
