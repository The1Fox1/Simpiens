# Epic Alpha: Biological Needs, Mental Breaks & Autonomous Motor Watchdog

- **Status**: IN PROGRESS (Phase 1 Planning)
- **Preceding Epic**: Epic 6: Multi-Step Reasoning (GOAP / HTN)
- **Following Epic**: Epic 7: The Sociopolitical & Relationship System (Affinity Matrix & Tribal Hierarchy)
- **Architectural Layer**: Cognition (Tier 1 Reflexes & Tier 2 GOAP Drives) & Core Simulation (`SimulationManager`)

---

## 1. Executive Summary & Problem Statement

Before agents can organize into sociopolitical structures, negotiate allegiances, or wage tribal conflict (Epic 7), their individual autonomy must be resilient, biologically grounded, and self-rescuing.

### 1.1 The Motor vs. Biology Conflation
Previously, `Frustration` was carrying two conflicting responsibilities:
1. **The Navigation Watchdog:** An unstuck failsafe to prevent agents from getting permanently wedged against geometry or map borders.
2. **The Panic / Breakdown Trigger:** An emotional threshold (`Frustration > 80f`) meant to trigger frantic behavior.

Because mundane wandering and passing gossip relieved 50 and 15 frustration points respectively, agents starving in a barren world remained permanently serene (`Frustration = 0.0`), wandering peacefully until they starved without ever displaying distress or attempting wide-area survival strategies.

### 1.2 The Multifaceted Need Imperative
GOAP reasoning requires discrete, targetable facts in `WorldState` (e.g., `IsHungry == false`, `IsExhausted == false`, `IsLonely == false`). A single scalar "deprivation" or "frustration" bucket prevents GOAP from prioritizing *which* plan to execute, leading to bizarre emergent behaviors (e.g., eating to escape a physical obstacle, or chatting while starving).

---

## 2. Architectural Triad: Drives, Breaks, and Watchdogs

```mermaid
flowchart TD
    subgraph Layer 1: Multifaceted Drives (Internal Physiological State)
        H["Hunger (0 - 100)\nIncreases over time\nRelieved by Eating"]
        E["Energy (0 - 100)\nDecreases with work/movement\nRelieved by Sleep/Rest"]
        S["Social (0 - 100)\nDecreases with isolation\nRelieved by Gossip/Proximity"]
    end

    subgraph Layer 2: Tier 2 Deliberative Planning (GOAP)
        H -->|Priority = f(Hunger)| GH["SatiateHungerGoal"]
        E -->|Priority = f(100 - Energy)| GE["RestGoal"]
        S -->|Priority = f(100 - Social)| GS["SocializeGoal"]
        
        GH --> P["GoapPlanner (A* Search)"]
        GE --> P
        GS --> P
        P --> AP["Sequential Plan Execution\n(Search -> Travel -> Harvest -> Eat)"]
    end

    subgraph Layer 3: Tier 1 Mental Breaks & Emergency Reflexes
        H -.->|Hunger >= 85 & No Food in Memory| MB1["Starvation Panic Reflex\n(Red Pose, Clear Outmoded Memory, 1.5x Sprint)"]
        E -.->|Energy <= 5| MB2["Exhaustion Collapse Reflex\n(Immobilized Deep Sleep)"]
        MB1 ==>|Tier 1 Preemption| AB["ISimulationManager.AbortIntent\n(Cancel Active Plan)"]
        MB2 ==>|Tier 1 Preemption| AB
    end

    subgraph Layer 4: Motor & Navigation Watchdog (Physical Failsafe)
        WD["MotorWatchdog (SimulationManager)\nTracks delta-position & consecutive PathBlocked/TargetMissing"]
        WD -->|Stall Count >= 3| US["Unstuck Reflex\n(Local Clearance Nudge / Evasive Repath / Target Disengage)"]
    end
```

---

## 3. Epic Alpha Phases Overview

### Phase 1: Multifaceted Biological Drives & Dynamic GOAP Goal Priorities
- **Scope**:
  - Zero-allocation `AgentNeeds` stack struct (`Hunger`, `Energy`, `Social`).
  - Continuous decay and replenishment loops in `AutonomousAgent.ManualUpdate()`.
  - Integration with `AgentContext` and `WorldStateBuilder`.
  - Dynamic GOAP Goal Priorities: `SatiateHungerGoal`, `RestGoal`, `SocializeGoal`.
  - Introducing `RestAction` (`IdleIntent`) and `StateFact.IsResting`.

### Phase 2: Autonomous Motor Watchdog (Dedicated Navigation Unstuck Failsafe)
- **Scope**:
  - Completely decouple physical collisions and geometry entrapment from biological/emotional states.
  - Zero-allocation `MotorWatchdog` tracking positional delta $\Delta \vec{p}$ and consecutive path blocks in `SimulationManager`.
  - Progressive 3-tiered unstuck reflex:
    1. Local clearance nudge (perpendicular jitter to separate overlapping pawns).
    2. Relaxed clearance repath (reduced pawn-to-pawn distance buffer).
    3. Target disengage and temporary blacklist in memory.

### Phase 3: Mental Breaks & Emergency Reflexes (The Panic System)
- **Scope**:
  - Refactor `FrustrationEvaluator` into `MentalBreakEvaluator` as a Tier 1 Reflexive Evaluator.
  - **Starvation Panic (`Hunger >= 85f` & no known food)**:
    - Preempts in-flight intent via `ISimulationManager.AbortIntent`.
    - Prunes depleted resource entries from `SpatialMemoryMap`.
    - Triggers the **Red Panicking pose** (`AgentVisualState.Panicking`).
    - Emits a high-speed ($1.5\times$) wide-area exploratory spiral sprint (`PanicIntent`) to discover new resources.
  - **Exhaustion Collapse (`Energy <= 5f`)**:
    - Agent collapses into an emergency nap on the spot (`IdleIntent` with `IsResting = true`), forced to recover energy to at least $30f$.

### Phase 4: Visual Poses, Telemetry & Domain Validation Suite
- **Scope**:
  - Complete visual cues in `AgentSpriteLibrary` for `Harvesting` (gold), `Panicking` (red exclamation), `Resting` (blue zzz), `Gossiping` (yellow dialog), `Walking` (cyan arrow), and `Idle` (white).
  - Automated domain validation suite in `Assets/Scripts/Testing/Drives/DriveSystemValidator.cs`.
  - Benchmark confirming strictly 0 GC allocations in `ManualUpdate()` and `SimulationManager.Tick()`.

---

## 4. Technical Constraints & Design Principles
1. **Zero Allocations in Hot Paths**:
   - `AgentNeeds` is a value-type struct (12 bytes: 3 floats).
   - Dynamic priority calculations avoid LINQ or heap-allocated delegates.
2. **Logic Isolation**:
   - Simulation state and drive ticks execute in pure C# classes.
   - GameObjects remain strictly dumb visual views (`AgentView`, `NodeController`).
3. **Preemption Safety**:
   - Tier 1 mental breaks cleanly abort in-flight movement through `ISimulationManager.AbortIntent(agentId)`, returning path pools and resetting active plans without memory leaks or race conditions.
