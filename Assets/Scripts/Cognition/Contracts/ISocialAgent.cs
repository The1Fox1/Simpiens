using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using UnityEngine;

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
        GroupMembership Membership { get; set; }
        bool IsExhaustionCollapsed { get; }
        bool IsStarvationPanicking { get; }
    }
}
