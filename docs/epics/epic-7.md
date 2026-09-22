# Epic 7: The Sociopolitical & Relationship System

- **Status**: IN PROGRESS (Phase 1 Completed, Phase 2 Pending)
- **Preceding Epic**: Epic Alpha: Biological Needs, Mental Breaks & Autonomous Motor Watchdog
- **Following Epic**: Epic 8: Inventory, Economy & Construction
- **Architectural Layer**: Cognition (`AgentMemory`, `AffinityMatrix`, GOAP Planning) & Core Simulation (`SimulationManager`)

---

## 1. Executive Summary & Problem Statement

In **Epic Alpha**, agents gained robust individual autonomy: biological metabolism (Hunger, Energy, Social), autonomous motor fail-safes (the 3-tiered unstick watchdog), and critical mental breaks (starvation panic and exhaustion collapse).

However, agents still possess **zero interpersonal distinction**:
1. **The Homogeneous Gossip Flaw**: When two agents wander into proximity ($\le 2.5\text{m}$), they blindly exchange their entire spatial Fog of War memory, regardless of whether the other agent is an ally, stranger, or mortal enemy.
2. **One-Dimensional Socialization**: `Social` drive currently functions purely as an internal hunger-like meter. Agents do not care *who* they speak to or *how* they feel about them.
3. **No Social Memory**: An agent attacked by another agent remembers the event in their `MemoryLedger`, but holds no persistent grudge, fear, or loyalty toward the attacker.

Before agents can organize into cohesive tribes, designate chieftains, build collective stockpiles (Epic 8), or wage wars (Epic 9), they must possess a **multidimensional, persistent Relationship System** capable of distinguishing friend from foe.

---

## 2. Architectural Overview: The Affinity Matrix

```mermaid
flowchart TD
    subgraph Layer 1: Event Ledger & Interactions
        E1["MemoryEvent.GossipShared / GossipReceived"]
        E2["MemoryEvent.Attacked (Epic 9)"]
        E3["Direct Proximity & Sustained Contact"]
    end

    subgraph Layer 2: Relational State (AgentMemory)
        AM["AffinityMatrix (Sparse Dictionary per Agent)\nDictionary<GUID, AffinityRecord>"]
        AR["AffinityRecord (16-byte stack struct)\nsbyte Warmth (-100 to +100)\nsbyte Trust (-100 to +100)\nsbyte Fear (0 to +100)\nsbyte Respect (-100 to +100)\nbyte InteractionCount\nuint LastInteractionTick"]
        AM --> AR
    end

    subgraph Layer 3: Cognitive Integration & Planning
        WB["WorldStateBuilder"]
        WS["WorldState Facts:\n- HasWarmCompanion\n- HasTrustedPeer\n- HasFearedThreat\n- HasRespectedLeader"]
        GOAP["GoapPlanner & Goals:\n- SatiateHungerGoal\n- RestGoal\n- SocializeGoal"]
    end

    subgraph Layer 4: Selective Simulation Execution
        SIM["SimulationManager.CheckGossipOpportunity"]
        FILT{"Filter:\nTrust >= -20 && Fear <= 60\nPeer Not An Enemy?"}
        YES["Execute Mutual Gossip & Spatial Knowledge Share\nWarmth (+5), Trust (+10), Fear (-5), Respect (+5)"]
        NO["Suppress Gossip\nIncrease Suspicion / Fear"]
    end

    E1 --> AM
    E2 --> AM
    E3 --> AM
    AM --> WB
    WB --> WS
    WS --> GOAP
    AM --> SIM
    SIM --> FILT
    FILT -->|Pass| YES
    FILT -->|Fail| NO
```

---

## 3. The 4-Axis Relational Dynamics

Rather than a simplistic 0–100 "friendship" slider, relationships are modeled on four orthogonal psychological axes:

| Axis | Range | Default | Dynamics & Meaning |
| :--- | :--- | :--- | :--- |
| **Warmth** *(Affection / Heart)* | $-100 \dots +100$ | $0$ (Neutral) | Altruism, love, camaraderie, and tenderness vs hatred, animosity, and disgust. Drives sharing scarce food, rescuing wounded, cohabitating, and mourning losses. |
| **Trust** *(Reliability / Mind)* | $-100 \dots +100$ | $0$ (Neutral) | Reliability, honesty, and predictability vs treachery, suspicion, and deceit. Unlocks sensitive Fog of War map sharing, trade on credit, and unmonitored sleep/safety. |
| **Fear** *(Intimidation / Gut)* | $0 \dots 100$ | $0$ (Unafraid) | Physical dread, terror, and perceived vulnerability. Increases when cornered, attacked, or facing a dominant warrior ($+25$). Triggers yielding right-of-way, surrendering items, or fleeing combat. |
| **Respect** *(Competence / Ego)* | $-100 \dots +100$ | $0$ (Neutral) | Admiration, prestige, and acknowledging skill/authority vs contempt, derision, and pity. Drives voting for Tribe Chieftain, following build/work orders, and skill imitation. |

---

## 4. Epic 7 Phases Overview

### Phase 1: The Social Affinity Matrix & Relational Dynamics [COMPLETED]
- **Zero-Allocation Data Layer**:
  - `AffinityRecord`: 16-byte unmanaged struct (`Warmth`, `Trust`, `Fear`, `Respect`, `InteractionCount`, `LastInteractionTick`).
  - `AffinityMatrix`: Sparse, on-demand dictionary in [`AgentMemory.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Memory/AgentMemory.cs). Unseen agents consume 0 memory.
- **Relational Updates & Ledger Binding**:
  - Mutual gossip updates: Successful knowledge exchange awards $+5\text{ Warmth}$, $+10\text{ Trust}$, $-5\text{ Fear}$, $+5\text{ Respect}$.
  - Temporal regression: Extreme scores slowly regress toward neutral over long time horizons ($1,000\text{ ticks}$).
- **Selective Socialization in Simulation**:
  - In `SimulationManager.CheckGossipOpportunity`, gossip is **suppressed** if `Trust < -20` (distrusted) or `Fear > 60` (intimidated).
- **GOAP WorldState & Goal Integration**:
  - Extend `StateFact` with `HasWarmCompanion`, `HasTrustedPeer`, `HasFearedThreat`, and `HasRespectedLeader`.
  - Update `SocializeGoal` to prioritize companions with positive `Warmth` and `Trust`.
- **Automated Domain Validation Suite**:
  - `AffinityMatrixValidator.cs` in `Assets/Scripts/Testing/Sociopolitical/` verifying all 5 core dynamics with strictly 0 GC allocations.

### Phase 2: Tribal Tagging, Hierarchy & Collective Influence [PENDING]
- **Identity & Tribal Metadata**:
  - `TribeId` (GUID) and `TribalRole` (`Chieftain`, `Elder`, `Warrior`, `Gatherer`, `Outcast`).
- **Chieftain Authority & Tribal Command Dispatch**:
  - Chieftain orders propagate via `IPlayerInterventionService` / `InfluenceQueue` as external cognitive signals (e.g. `TribalRallyEvent`), ingested by members off-thread into `ObeyChieftainGoal` weighted by personal `Allegiance` and `Fear`.
- **Inter-Tribe Diplomacy**:
  - Tribal default affinities (e.g. Tribe A vs Tribe B relations) seed initial baseline affinities for member agents.

---

## 5. Technical Constraints & Design Principles

1. **Strict Zero-Allocation Hot Path**:
   - `AffinityRecord` is an unmanaged stack struct (16 bytes).
   - Reading relationships during `ManualUpdate()` or `Tick()` uses zero-allocation struct copies or `in` parameters.
2. **Dual-Loop Compliance**:
   - Relationships and tribal loyalties are evaluated off-thread in `CognitiveEngine` / GOAP.
   - Neither `SimulationManager` nor MonoBehaviours mutate relational states directly outside of deterministic interaction events (such as gossip completion or combat resolution).
3. **MCP-Ready Context Aggregation**:
   - The sparse `AffinityMatrix` serializes cleanly into a lightweight JSON dictionary (`{"agentId": {"trust": 35, "fear": 0, "allegiance": 50}}`), directly preparing the groundwork for Epic 10 narrative prompts.
4. **Defensive Null-Safety for Test Isolation**:
   - All relationship queries handle null contexts, missing peer IDs, and isolated headless test runs benignly (defaulting to neutral `Trust = 0`, `Fear = 0`, `Allegiance = 0`).
