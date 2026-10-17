using UnityEngine;

namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Unmanaged, zero-allocation struct representing an active group command.
    /// Broadcast to subordinates via SharedWorldSnapshot without heap allocations.
    /// </summary>
    public readonly struct GroupDirective
    {
        public readonly DirectiveType Type;
        public readonly byte GroupId;
        public readonly EntityId LeaderId;
        public readonly Vector2 TargetLocation;
        public readonly EntityId TargetEntityId;
        public readonly uint IssuedTick;
        public readonly uint ExpirationTick;
        public readonly float TargetRadius;

        public GroupDirective(DirectiveType type, byte groupId, EntityId leaderId,
                              Vector2 targetLocation, EntityId targetEntityId,
                              uint issuedTick, uint expirationTick, float targetRadius = 2.5f)
        {
            Type = type;
            GroupId = groupId;
            LeaderId = leaderId;
            TargetLocation = targetLocation;
            TargetEntityId = targetEntityId;
            IssuedTick = issuedTick;
            ExpirationTick = expirationTick;
            TargetRadius = targetRadius;
        }

        public static readonly GroupDirective None = new GroupDirective(DirectiveType.None, 0, default, Vector2.zero, default, 0, 0, 0f);

        public bool IsActive(uint currentTick) => Type != DirectiveType.None && currentTick <= ExpirationTick;
    }
}
