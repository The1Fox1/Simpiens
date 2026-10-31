using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Decoupled interface representing an agent's sociopolitical state and memory (Epic 8).
    /// Enables pure C# domain services and isolated headless test suites without Unity GameObject dependencies.
    /// </summary>
    public interface ISocialAgent
    {
        EntityId AgentId { get; }
        AgentMemory Memory { get; }
        /// <summary>The agent's copy of its memberships (Epic 10 G4: many-to-many, one per kind); the group registry owns them.</summary>
        MembershipSet Memberships { get; set; }
        bool IsExhaustionCollapsed { get; }
        bool IsStarvationPanicking { get; }
    }
}
