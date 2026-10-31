using Simpiens.Cognition.Groups;
using Simpiens.Simulation;

namespace Simpiens.Testing.Sociopolitical
{
    /// <summary>
    /// For validators written when a crawler held one membership (Epic 8): its first membership, in kind order. Groups are
    /// many-to-many since Epic 10 G4 (D1); a validator about one group asks for that group or kind instead.
    /// </summary>
    public static class MembershipTestExtensions
    {
        public static bool TryGetOnlyMembership(this IGroupRegistry registry, EntityId agentId, out GroupMembership membership)
        {
            var memberships = registry.GetMemberships(agentId);
            membership = memberships.First;
            return memberships.HasAny;
        }
    }
}
