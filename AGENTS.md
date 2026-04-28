# AGENTS.md

Welcome to the **Simpiens** project! This document outlines the Unity simulation's architectural layout, guiding automated agents on where to find, modify, or extend code.

## Current Project Structure
The `Assets/Scripts/` directory contains the core simulation logic, strongly emphasizing a code-first, decoupled approach where heavy cognitive logic runs asynchronously off the main Unity loop.

### Directories

- **`Cognition/`**
  Houses the asynchronous cognitive reasoning engine for agents.
  - `Contracts/`: Interfaces and data structures for Agent Intents (`AgentIntent`, `WanderIntent`) and Context (`AgentContext`).
  - `Evaluators/`: Implementations that assess utility or determine the next best action (`SurvivalUtilityEvaluator`).
  - `Pathfinding/`: Zero-allocation asynchronous pathfinding over the spatial grid (`AsyncPathfinder`).
  - **Key Files**: `CognitiveEngine.cs`, `AutonomousAgent.cs`.

- **`Core/`**
  Handles bootstrapping and Unity-specific initialization.
  - `Bootstrapping/`: Contains the absolute entry point for the application (`AppBootstrapper.cs`, `SimulationStarter.cs`).
  - **Key Files**: `ScreenBoundsGenerator.cs`, `IRoutine.cs`.

- **`Entities/`**
  Contains strictly `MonoBehaviour` views that hold state or rendering instructions.
  - **Rules**: Zero logic! Only basic view management, motor controls, and configurations.
  - **Key Files**: `AgentMotor.cs`, `NodeController.cs`.

- **`Intervention/`**
  Player intervention system acting as an influence queue.
  - **Key Files**: `PlayerInterventionService.cs`, `InfluenceEvent.cs`.

- **`Simulation/`**
  The deterministic simulation environment, registries, and spatial representations.
  - `Spatial/`: Handles high-performance memory-managed grids (`SpatialHashGrid`, `EntitySnapshot`).
  - **Key Files**: `WorldRegistry.cs`, `SimulationClock.cs`, `ISimulationManager.cs`, `SimulationManager.cs`.

- **`Testing/`**
  Isolated debug and validation tools.
  - **Key Files**: `EdgeCaseInjector.cs`, `SwarmSpawner.cs`, `GridVisualizer.cs`.

---

## Architecture Constraints Reminder
- **Composition over Inheritance**: Utilize C# Interfaces and VContainer DI.
- **No MonoBehaviours for Logic**: Game objects are dumb views.
- **Zero Allocations in Hot Paths**: Use Structs, object pooling, and standard `for` loops in tick-critical areas.
- **Dual-Loop System**: Use `SimulationManager` for Unity's tick, and `CognitiveEngine` for asynchronous background tasks.
