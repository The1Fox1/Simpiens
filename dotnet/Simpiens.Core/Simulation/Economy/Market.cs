using System;
using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation.Economy
{
    internal sealed class Market
    {
        private readonly IEconomyLedger _economyLedger;
        private readonly ISectorRegistry _sectors;
        private readonly INarrativeChronicleService _chronicleService;

        private readonly Dictionary<EntityId, TradeStallData> _stalls = new Dictionary<EntityId, TradeStallData>();
        private readonly List<TradeStallData> _activeStallsBuffer = new List<TradeStallData>(8);
        private readonly Dictionary<ushort, BountyContract> _bounties = new Dictionary<ushort, BountyContract>();
        // The next generated bounty id. Not saved: a load derives it as the highest saved id + 1, which is exact because
        // nothing removes a bounty from _bounties (finished ones stay, and are saved) and every id taken raises it (until
        // the 65,535 ids run out and generation wraps to fill gaps)
        private ushort _nextBountyId = 1;
        private readonly List<BountyContract> _activeBountiesBuffer = new List<BountyContract>(16);
        private readonly List<ushort> _expiredBountiesBuffer = new List<ushort>(16);
        private readonly List<ushort> _departingClaims = new List<ushort>(2);

        // Hunts' parties (Epic 10 Part 3, K6.1), by bounty id; a record lives while its hunt is open
        private readonly List<HuntRecord> _hunts = new List<HuntRecord>(4);
        private readonly EntityId[] _shareClaimants = new EntityId[HuntRecord.MaxClaimants];
        private readonly uint[] _shares = new uint[HuntRecord.MaxClaimants];

        /// <summary>The open hunts' parties, by bounty id (K6.1).</summary>
        public IReadOnlyList<HuntRecord> Hunts => _hunts;

        public IReadOnlyList<TradeStallData> ActiveStalls => _activeStallsBuffer;
        public IReadOnlyList<BountyContract> ActiveBounties => _activeBountiesBuffer;

        public Market(
            IEconomyLedger economyLedger = null,
            ISectorRegistry sectors = null,
            INarrativeChronicleService chronicleService = null)
        {
            _economyLedger = economyLedger;
            _sectors = sectors;
            _chronicleService = chronicleService;
        }

        public bool TryGetStall(EntityId stallId, out TradeStallData stall) => _stalls.TryGetValue(stallId, out stall);

        public void UpdateStall(TradeStallData stall)
        {
            _stalls[stall.StallEntityId] = stall;
            UpdateActiveStallsBuffer();
        }

        public void RegisterTradeStall(TradeStallData stallData)
        {
            _stalls[stallData.StallEntityId] = stallData;
            UpdateActiveStallsBuffer();
        }

        public void UpdateStallPrices(in UpdateStallPricesCommand cmd)
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

        private void UpdateActiveStallsBuffer()
        {
            _activeStallsBuffer.Clear();
            foreach (var kvp in _stalls)
            {
                _activeStallsBuffer.Add(kvp.Value);
            }
            StableSort.Sort(_activeStallsBuffer, (a, b) => a.StallEntityId.CompareTo(b.StallEntityId));
        }

        public void UpdateBounty(BountyContract bounty)
        {
            _bounties[bounty.BountyId] = bounty;
            UpdateActiveBountiesBuffer();
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
            StableSort.Sort(_activeBountiesBuffer, (a, b) => a.BountyId.CompareTo(b.BountyId));
        }

        /// <summary>Posts a bounty; its id, or 0 when it was refused. A hunt (K6.1) gets its party's record.</summary>
        public ushort PostBounty(in PostBountyCommand cmd, uint currentTick)
        {
            ushort bountyId = cmd.BountyId;
            if (bountyId == 0 && !TryTakeNextFreeBountyId(out bountyId))
            {
                SimLog.Intervention.ZLogWarning($"Bounty rejected: every bounty id is taken");
                return 0;
            }
            bool posted = PostBountyInternal(
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
            if (!posted) return 0;
            if (cmd.Type == BountyType.Hunt) InsertHunt(new HuntRecord(bountyId, cmd.LairIndex));
            return bountyId;
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

        public void CancelBounty(ushort bountyId, uint currentTick)
        {
            CancelBountyInternal(bountyId, currentTick);
        }

        private bool PostBountyInternal(
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
                itemType == ItemType.None && resolvedType == BountyType.GatherResource ? ItemCatalog.InCategory(ItemCategory.Resource)[0] : itemType,
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
                return false;
            }

            _chronicleService?.RecordEvent(new ChronicleEvent(
                ChronicleEventType.BountyPosted,
                SimulationManager.PlayerEntityId,
                default,
                sectorId,
                currentTick,
                ChronicleIntensity.Notable,
                rewardGold));
            return true;
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
                    RemoveHunt(bountyId);
                    UpdateActiveBountiesBuffer();
                    SimLog.Intervention.ZLogInformation($"Bounty #{bountyId} cancelled (was {bounty.Status}, claimant {SimLog.Agent(bounty.ClaimantId)}); escrow refunded");

                    _chronicleService?.RecordEvent(new ChronicleEvent(
                        ChronicleEventType.BountyExpired,
                        SimulationManager.PlayerEntityId,
                        bounty.ClaimantId,
                        bounty.TargetSectorId,
                        currentTick,
                        ChronicleIntensity.Trivial));
                }
            }
        }

        public void ExpireBounties(uint currentTick)
        {
            if (_bounties.Count == 0) return;

            _expiredBountiesBuffer.Clear();
            foreach (var kvp in _bounties)
            {
                if (kvp.Value.IsExpired(currentTick))
                {
                    _expiredBountiesBuffer.Add(kvp.Key);
                }
            }

            if (_expiredBountiesBuffer.Count > 0)
            {
                StableSort.Sort(_expiredBountiesBuffer, (a, b) => a.CompareTo(b));
                for (int i = 0; i < _expiredBountiesBuffer.Count; i++)
                {
                    ushort expiredId = _expiredBountiesBuffer[i];
                    var b = _bounties[expiredId];

                    if (b.CreatorStallId != default && _stalls.TryGetValue(b.CreatorStallId, out var stall))
                    {
                        if (_economyLedger != null)
                        {
                            if (_economyLedger.TryRefundEscrow(expiredId, b.CreatorStallId, ref stall, out _))
                            {
                                _stalls[b.CreatorStallId] = stall;
                            }
                        }
                        else
                        {
                            _stalls[b.CreatorStallId] = stall.WithTreasury(stall.TreasuryGold + b.RewardGold);
                        }
                    }

                    _bounties[expiredId] = b.WithStatus(BountyStatus.Expired);
                    RemoveHunt(expiredId);
                    SimLog.Intervention.ZLogInformation($"Bounty #{expiredId} expired unfulfilled (was {b.Status}, claimant {SimLog.Agent(b.ClaimantId)}); escrow refunded");

                    _chronicleService?.RecordEvent(new ChronicleEvent(
                        ChronicleEventType.BountyExpired,
                        SimulationManager.PlayerEntityId,
                        b.ClaimantId,
                        b.TargetSectorId,
                        currentTick,
                        ChronicleIntensity.Trivial));
                }

                UpdateActiveBountiesBuffer();
                UpdateActiveStallsBuffer();
            }
        }

        public int ReleaseClaimsForAgent(EntityId agentId)
        {
            _departingClaims.Clear();
            for (int i = 0; i < _activeBountiesBuffer.Count; i++)
            {
                var bounty = _activeBountiesBuffer[i];
                if (bounty.Status == BountyStatus.Claimed && bounty.ClaimantId == agentId)
                {
                    _departingClaims.Add(bounty.BountyId);
                }
            }
            for (int i = 0; i < _departingClaims.Count; i++)
            {
                if (_bounties.TryGetValue(_departingClaims[i], out var claimed))
                {
                    UpdateBounty(claimed.WithClaimant(default, BountyStatus.Open));
                }
            }
            int released = _departingClaims.Count;
            // And every hunt it joined, its share with it (K6.1)
            for (int i = 0; i < _hunts.Count; i++)
            {
                if (!_hunts[i].HasClaimant(agentId)) continue;
                _hunts[i] = _hunts[i].WithoutClaimant(agentId);
                released++;
            }
            return released;
        }

        public EconomySave Capture()
        {
            var stalls = new TradeStallData[_stalls.Count];
            int n = 0;
            foreach (var kvp in _stalls) stalls[n++] = kvp.Value;
            Array.Sort(stalls, (a, b) => a.StallEntityId.CompareTo(b.StallEntityId));

            var bounties = new BountyContract[_bounties.Count];
            n = 0;
            foreach (var kvp in _bounties) bounties[n++] = kvp.Value;
            Array.Sort(bounties, (a, b) => a.BountyId.CompareTo(b.BountyId));

            var ledger = new LedgerSave();
            _economyLedger?.CaptureTo(ledger);
            return new EconomySave { Stalls = stalls, Bounties = bounties, Ledger = ledger, Hunts = _hunts.ToArray() };
        }

        public void RestoreFrom(EconomySave economy)
        {
            _stalls.Clear();
            if (economy?.Stalls != null)
            {
                for (int i = 0; i < economy.Stalls.Length; i++) _stalls[economy.Stalls[i].StallEntityId] = economy.Stalls[i];
            }
            UpdateActiveStallsBuffer();

            _bounties.Clear();
            _nextBountyId = 1;
            if (economy?.Bounties != null)
            {
                for (int i = 0; i < economy.Bounties.Length; i++)
                {
                    var b = economy.Bounties[i];
                    _bounties[b.BountyId] = b;
                    if (b.BountyId >= _nextBountyId)
                    {
                        _nextBountyId = (ushort)(b.BountyId + 1);
                    }
                }
            }
            UpdateActiveBountiesBuffer();
            _hunts.Clear();
            if (economy?.Hunts != null) _hunts.AddRange(economy.Hunts); // saved in id order (K6.1)
            if (economy?.Ledger != null) _economyLedger?.RestoreFrom(economy.Ledger);
        }

        public void Reset()
        {
            _stalls.Clear();
            _activeStallsBuffer.Clear();
            _bounties.Clear();
            _nextBountyId = 1;
            _activeBountiesBuffer.Clear();
            _expiredBountiesBuffer.Clear();
            _departingClaims.Clear();
            _hunts.Clear();
        }

        // Hunts (Epic 10 Part 3, K6.1)

        /// <summary>The open hunt with <paramref name="bountyId"/>'s party.</summary>
        public bool TryGetHunt(ushort bountyId, out HuntRecord hunt)
        {
            int index = HuntIndex(bountyId);
            hunt = index >= 0 ? _hunts[index] : default;
            return index >= 0;
        }

        /// <summary>
        /// <paramref name="crawler"/> joins the hunt (a claim, K6.1): while it is open and active and has room. True when it
        /// is in the party (already, or now).
        /// </summary>
        public bool TryJoinHunt(ushort bountyId, EntityId crawler, uint currentTick)
        {
            int index = HuntIndex(bountyId);
            if (index < 0 || !_bounties.TryGetValue(bountyId, out var bounty) || bounty.Status != BountyStatus.Open || !bounty.IsActive(currentTick)) return false;
            var hunt = _hunts[index];
            if (hunt.HasClaimant(crawler)) return true;
            if (hunt.IsFull) return false;
            _hunts[index] = hunt.WithClaimant(crawler);
            return true;
        }

        /// <summary>A crawler's blow on a creature of lair <paramref name="lairIndex"/> adds to its share of every hunt on it it joined (K6.1).</summary>
        public void RecordHuntDamage(EntityId crawler, int lairIndex, float damage)
        {
            for (int i = 0; i < _hunts.Count; i++)
            {
                if (_hunts[i].LairIndex != lairIndex || !_hunts[i].HasClaimant(crawler)) continue;
                _hunts[i] = _hunts[i].WithDamage(crawler, damage);
            }
        }

        /// <summary>
        /// Pays every joined hunt whose lair is empty (K6.1, Q7): the escrow by damage dealt, the bounty completed, the
        /// chronicle told. A hunt on an emptied lair with nobody in it waits for a claimant, or its expiry. In id order.
        /// </summary>
        public void SettleHunts(IReadOnlyList<Simpiens.Simulation.Creatures.LairState> lairs, IAgentRegistry agents, uint currentTick)
        {
            bool settled = false;
            for (int i = _hunts.Count - 1; i >= 0; i--)
            {
                var hunt = _hunts[i];
                if (hunt.Count == 0 || lairs == null || hunt.LairIndex >= lairs.Count || lairs[hunt.LairIndex].Alive > 0) continue;
                if (!_bounties.TryGetValue(hunt.BountyId, out var bounty) || bounty.Status != BountyStatus.Open) continue;
                uint reward = _economyLedger != null ? _economyLedger.GetEscrowAmount(hunt.BountyId) : bounty.RewardGold;
                var claimants = new Span<EntityId>(_shareClaimants, 0, hunt.Count);
                var shares = new Span<uint>(_shares, 0, hunt.Count);
                for (int c = 0; c < hunt.Count; c++) claimants[c] = hunt.ClaimantAt(c);
                hunt.Split(reward, shares);
                if (_economyLedger != null && !_economyLedger.TryReleaseEscrowSplit(hunt.BountyId, claimants, shares, agents)) continue;
                _bounties[hunt.BountyId] = bounty.WithClaimant(hunt.Claimant0, BountyStatus.Completed);
                _hunts.RemoveAt(i);
                settled = true;
                SimLog.Economy.ZLogInformation($"Hunt #{hunt.BountyId} done: lair {hunt.LairIndex} emptied; {reward}g paid to {hunt.Count} hunter(s) by damage dealt");
                for (int c = 0; c < hunt.Count; c++)
                {
                    if (agents != null && agents.TryGetAgent(hunt.ClaimantAt(c), out var hunter)) hunter.AddDeed(Simpiens.Simulation.Skills.Deed.HuntPaid); // a deed (Epic 11, S0.5)
                    _chronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.BountyFulfilled, hunt.ClaimantAt(c), SimulationManager.PlayerEntityId,
                        bounty.TargetSectorId, currentTick, ChronicleIntensity.Notable, shares[c]));
                }
            }
            if (settled) UpdateActiveBountiesBuffer();
        }

        private void InsertHunt(in HuntRecord hunt)
        {
            int at = 0;
            while (at < _hunts.Count && _hunts[at].BountyId < hunt.BountyId) at++;
            _hunts.Insert(at, hunt);
        }

        private void RemoveHunt(ushort bountyId)
        {
            int index = HuntIndex(bountyId);
            if (index >= 0) _hunts.RemoveAt(index);
        }

        private int HuntIndex(ushort bountyId)
        {
            for (int i = 0; i < _hunts.Count; i++) if (_hunts[i].BountyId == bountyId) return i;
            return -1;
        }
    }
}

