---
trigger: always_on
---

Project: Unity Simulation Framework for Autonomous Agents.
Role: Full-stack architecture, focusing on separating C# simulation logic from async cognitive processing.

Core Constraints:

Unity Libraries preference: Be aware of type overlap between the standard .NET libraries and Unity specific types (such as Unity.GUID vs System.Guid) and lean toward Unity types unless there's a reason not to.

No Logic in MonoBehaviours: MonoBehaviour scripts must strictly act as dumb views. They only handle rendering, animations, and reading state from a central manager.

Dual-Loop System: Create a central SimulationManager that handles the real-time Unity tick, and a separate CognitiveEngine that processes agent reasoning asynchronously via Tasks/message queues.

MCP-Ready Interface: Define interfaces for the agent reasoning layer that align with the Model Context Protocol. Create a ContextBuilder that aggregates an agent's local environment and memory into a payload, and a ToolExecution registry where the cognitive layer can trigger hard-set game mechanics (e.g., ExecuteAction(AgentId, ActionType, Parameters)).

Influence Queue: Implement a PlayerInterventionService using the Command pattern. Player inputs must be queued as data objects (InfluenceEvent) that agents ingest during their cognitive polling cycle, rather than direct state mutations.

First Output: Generate the interface definitions and structural boilerplate for the SimulationManager, CognitiveEngine, and PlayerInterventionService. Do not implement the Unity rendering logic yet.