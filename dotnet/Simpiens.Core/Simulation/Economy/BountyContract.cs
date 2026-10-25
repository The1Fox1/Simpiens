using System;
using System.Runtime.InteropServices;
namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Unmanaged, blittable representation of a public bounty contract.
    /// Exactly 64 bytes (1 CPU cache line), zero heap allocations.
    /// Compatible with SharedWorldSnapshot fixed buffers and direct MemoryPack memory copies.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct BountyContract : IEquatable<BountyContract>
    {
        public readonly ushort BountyId;         // 2 bytes (offset 0)
        public readonly BountyType Type;          // 1 byte  (offset 2)
        public readonly BountyStatus Status;      // 1 byte  (offset 3)
        public readonly byte TargetSectorId;      // 1 byte  (offset 4)
        public readonly ItemType TargetItemType;  // 1 byte  (offset 5)
        public readonly byte TargetQuantity;      // 1 byte  (offset 6)
        private readonly byte _pad0;              // 1 byte  (offset 7)
        public readonly ushort RewardGold;        // 2 bytes (offset 8)
        private readonly ushort _pad1;            // 2 bytes (offset 10)
        public readonly uint IssuedTick;          // 4 bytes (offset 12)
        public readonly uint ExpirationTick;      // 4 bytes (offset 16)
        public readonly Vec2 TargetLocation;   // 8 bytes (offset 20)
        private readonly uint _pad2;              // 4 bytes (offset 28)
        public readonly EntityId ClaimantId;          // 16 bytes (offset 32)
        public readonly EntityId CreatorStallId;      // 16 bytes (offset 48)
                                                  // Total: 64 bytes

        public static readonly BountyContract Empty = default;

        public bool IsActive(uint currentTick) =>
            (Status == BountyStatus.Open || Status == BountyStatus.Claimed) && currentTick <= ExpirationTick;

        public bool IsExpired(uint currentTick) =>
            (Status == BountyStatus.Open || Status == BountyStatus.Claimed) && currentTick > ExpirationTick;

        public bool IsClaimed => Status == BountyStatus.Claimed && ClaimantId != default;

        public BountyContract(
            ushort bountyId,
            BountyType type,
            byte targetSectorId,
            ItemType targetItemType,
            byte targetQuantity,
            ushort rewardGold,
            uint issuedTick,
            uint expirationTick,
            Vec2 targetLocation = default,
            EntityId claimantId = default,
            EntityId creatorStallId = default,
            BountyStatus status = BountyStatus.Open)
        {
            BountyId = bountyId;
            Type = type;
            Status = status;
            TargetSectorId = targetSectorId;
            TargetItemType = targetItemType;
            TargetQuantity = targetQuantity;
            _pad0 = 0;
            RewardGold = rewardGold;
            _pad1 = 0;
            IssuedTick = issuedTick;
            ExpirationTick = expirationTick;
            TargetLocation = targetLocation;
            _pad2 = 0;
            ClaimantId = claimantId;
            CreatorStallId = creatorStallId;
        }

        public BountyContract WithClaimant(EntityId claimantId, BountyStatus status = BountyStatus.Claimed)
        {
            return new BountyContract(
                BountyId,
                Type,
                TargetSectorId,
                TargetItemType,
                TargetQuantity,
                RewardGold,
                IssuedTick,
                ExpirationTick,
                TargetLocation,
                claimantId,
                CreatorStallId,
                status);
        }

        public BountyContract WithStatus(BountyStatus status)
        {
            return new BountyContract(
                BountyId,
                Type,
                TargetSectorId,
                TargetItemType,
                TargetQuantity,
                RewardGold,
                IssuedTick,
                ExpirationTick,
                TargetLocation,
                ClaimantId,
                CreatorStallId,
                status);
        }

        public BountyContract WithTargetLocation(Vec2 targetLocation)
        {
            return new BountyContract(
                BountyId,
                Type,
                TargetSectorId,
                TargetItemType,
                TargetQuantity,
                RewardGold,
                IssuedTick,
                ExpirationTick,
                targetLocation,
                ClaimantId,
                CreatorStallId,
                Status);
        }

        public bool Equals(BountyContract other)
        {
            return BountyId == other.BountyId &&
                   Type == other.Type &&
                   Status == other.Status &&
                   TargetSectorId == other.TargetSectorId &&
                   TargetItemType == other.TargetItemType &&
                   TargetQuantity == other.TargetQuantity &&
                   RewardGold == other.RewardGold &&
                   IssuedTick == other.IssuedTick &&
                   ExpirationTick == other.ExpirationTick &&
                   TargetLocation.Equals(other.TargetLocation) &&
                   ClaimantId.Equals(other.ClaimantId) &&
                   CreatorStallId.Equals(other.CreatorStallId);
        }

        public override bool Equals(object obj) => obj is BountyContract other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BountyId.GetHashCode();
                hash = (hash * 397) ^ ((byte)Type).GetHashCode();
                hash = (hash * 397) ^ ((byte)Status).GetHashCode();
                hash = (hash * 397) ^ TargetSectorId.GetHashCode();
                hash = (hash * 397) ^ ((byte)TargetItemType).GetHashCode();
                hash = (hash * 397) ^ RewardGold.GetHashCode();
                hash = (hash * 397) ^ ClaimantId.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(in BountyContract left, in BountyContract right) => left.Equals(right);
        public static bool operator !=(in BountyContract left, in BountyContract right) => !left.Equals(right);

        public override string ToString() =>
            $"[Bounty #{BountyId} {Type} ({Status}) Item={TargetItemType}x{TargetQuantity} Reward={RewardGold}g Claimant={ClaimantId}]";
    }
}
