# Epic UI-Alpha: The Humanized Narrative Chronicle & Story Feed

- **Status**: COMPLETED
- **Track**: Track B: UI / UX & Player Interface (Branching Track)
- **Preceding Epic**: [Epic UI-1: The Flat "Facility Dashboard" Menu UI](epic-ui-1.md)
- **Following Epic**: Epic UI-2: Facility Directives & Player Intervention Terminal
- **Architectural Layer**: 
  - **Core Domain Engine**: `Simpiens.Narrative` (Pure C# `IChronicleEventBus`, `ChronicleEvent` ring buffer, `IAgentIdentityService`, `TemplateNarrativeFormatter`)
  - **Presentation Layer**: `Simpiens.UI` (`RelativeTimeFormatter`, `FacilityDashboardPresenter` chronicle feed binding)
  - **Future Cognitive Bridge**: `INarrativeEnricher` hook (prepares for Track A Epic 11 MCP/LLM integration)

---

## 1. Executive Summary & Problem Statement

In **Epic UI-1**, the Facility Dashboard successfully established sector navigation and pawn dossiers, but its "Sector Events" feed currently surfaces raw cognitive memory logs:
```
[Tick 0] SwarmAgent_6: GossipShared (SwarmAgent_7)
```
This fails to engage the player for three core reasons:
1. **Raw Cognitive Artifacts vs. Player Story**: Internal enum names (`GossipShared`, `ResourceYielded`, `DominanceChallenged`) are decision-making telemetry for GOAP and gossip propagation, not engaging narrative prose.
2. **Procedural Debug Names**: `SwarmAgent_6` breaks immersion. Players bond with recognizable characters (`Burt`, `Bob`, `Alice`).
3. **Engine Ticks vs. Relative Human Time**: `[Tick 1420]` forces the player to calculate simulation math instead of glancing at `[Just now]` or `[1 minute ago]`.

**Epic UI-Alpha** bridges this gap by creating an authoritative **Narrative Chronicle Service** using a **Pipe-and-Filter Event Bus** coupled with **Two-Stage Execution**:
- **Stage 1 (Immediate / Deterministic)**: Instant zero-alloc template synthesis generates readable prose (`"[1 minute ago] Burt shared information with Bob."`).
- **Stage 2 (Async LLM Enrichment Hook)**: Provides the extensible foundation for Track A's **Epic 11 (Agentic Narrative Engine)** to asynchronously upgrade dramatic events into rich, context-aware flavor text without modifying UI code.

---

## 2. Technical Architecture: Pipe-and-Filter with Two-Stage Execution

```mermaid
flowchart TD
    subgraph Simulation & Core Engine (Track A)
        SimEvents["Simulation Events\n(Interactions, Breaks, Gossip, Sharing)"]
    end

    subgraph Core Narrative Backbone (Simpiens.Narrative)
        Bus["IChronicleEventBus\n(Thread-Safe Event Ingestion)"]
        RingBuffer["ChronicleRingBuffer<ChronicleEvent>\n(Fixed Capacity 100, Pre-allocated, Zero-Alloc)"]
        Identity["IAgentIdentityService\n(GUID -> Friendly Name, Title, Pronouns)"]
        Formatter["INarrativeFormatter\n(Strategy Pattern)"]
        TemplateFmt["TemplateNarrativeFormatter\n(Stage 1: Deterministic Zero-Alloc Mad-Libs)"]
        LlmHook["ILlmNarrativeEnricher\n(Stage 2: Async UniTask / MCP Bridge for Epic 11)"]
    end

    subgraph Presentation & UI (Track B: Simpiens.UI)
        TimeFmt["RelativeTimeFormatter\n([Just now], [45s ago], [2m ago])"]
        Presenter["FacilityDashboardPresenter\n(Sector Chronicle Binding via Element Pooling)"]
        UIElements["events-scroll-list\n(Stable UI Toolkit Labels)"]
    end

    SimEvents -->|Emit| Bus
    Bus --> RingBuffer
    RingBuffer --> Formatter
    Identity -.-> Formatter
    Formatter --> TemplateFmt
    Formatter -.->|Async Upgrade| LlmHook
    TemplateFmt --> Presenter
    TimeFmt --> Presenter
    Presenter --> UIElements
```

### Component Breakdown

1. **`ChronicleEvent` (Immutable Struct)**:
   - Lightweight event data: `EventId`, `ActorAId`, `ActorBId`, `SectorId`, `SimulationTick`, `Category` (Social, Survival, Crisis, Group), `Intensity` (Low, Medium, High).
2. **`IAgentIdentityService`**:
   - Deterministic procedural identity generator mapping pawn GUIDs to friendly names (`Burt`, `Bob`, `Alice`, `Charlie`, `Danielle`, `Ezekiel`), nicknames, and social titles based on agent traits and role.
3. **`INarrativeChronicleService`**:
   - Central repository retaining up to **100 events per sector in isolated circular buffers** (plus a 1024-event global buffer), guaranteeing at least 5-minute event persistence without cross-sector evictions. Provides zero-allocation query APIs:
     `GetSectorEvents(int sectorId, List<ChronicleEvent> resultsBuffer, int maxCount = 100)`.
4. **`TemplateNarrativeFormatter` (Stage 1 Deterministic Synthesizer)**:
   - Pre-compiled text pattern matrices that produce natural sentences at 0 bytes GC:
     - `GossipShared`: `"{Actor} shared campfire stories with {Target} in {Sector}."`
     - `FoodShared`: `"{Actor} gave an emergency food ration to {Target}."`
     - `Yielded`: `"{Actor} stepped aside and yielded resources to {Target}."`
     - `Collapse`: `"{Actor} collapsed from exhaustion near {Sector}."`
5. **`RelativeTimeFormatter`**:
   - Translates `(CurrentTick - EventTick) / TicksPerSecond` into humanized relative time strings:
     - $< 10\text{s}$: `"[Just now]"`
     - $< 60\text{s}$: `"[45s ago]"`
     - $\ge 60\text{s}$: `"[1m ago]"`, `"[5m ago]"`, `"[12m ago]"`
6. **`ILlmNarrativeEnricher` (Stage 2 Pluggable Hook)**:
   - Async interface hook designed for clean convergence with **Epic 11**. Fulfillable via a lean, pure-C# UniTask JSON-RPC / SSE client without heavy external ASP.NET MCP dependencies, allowing background workers to enrich dramatic chronicle records with LLM prose without changing the UI presenter.

---

## 3. Implementation Phases

### Phase 1: Core Narrative Contracts & Chronicle Event Bus (`Simpiens.Narrative`)
- Define `ChronicleEvent` struct, `EventCategory`, and `EventIntensity`.
- Implement `IChronicleEventBus` and `NarrativeChronicleService` with a pre-allocated fixed-capacity ring buffer (default: 100 events).
- Provide sector-filtered and actor-filtered zero-allocation query methods.

### Phase 2: Agent Identity & Friendly Persona Service (`IAgentIdentityService`)
- Create `AgentIdentityService` mapping `GUID` to human-readable names (`Burt`, `Bob`, etc.) and titles.
- Guarantee deterministic seed-based name generation so an agent keeps the same identity across scene loads.
- Support fallback to existing `NodeController.name` if unmapped.

### Phase 3: Deterministic Narrative Template Synthesizer (`TemplateNarrativeFormatter`)
- Implement `INarrativeFormatter` and `TemplateNarrativeFormatter` with diverse sentence variations for all existing social, biological, and group events:
  - Campfire gossip, altruistic food sharing, dominance yielding, starvation panics, exhaustion collapses, and group directives.
- Ensure formatting uses string builders or pre-allocated format caches with zero heap allocations on the hot path.

### Phase 4: Dashboard Integration & Relative Time Presentation (`Simpiens.UI`)
- Implement `RelativeTimeFormatter` calculating relative simulation time from `ISimulationClock`.
- Refactor `FacilityDashboardPresenter.PopulateSectorEvents()`:
  - Remove direct reads from private `AgentMemory.EventLedger`.
  - Bind to `INarrativeChronicleService.GetSectorEvents(selectedSectorId, buffer)`.
  - Format output as: `"[1 minute ago] Burt shared information with Bob."`
- Maintain stable element pooling (`_eventItemsPool`) so hover states and scroll position remain smooth and non-allocating.

### Phase 5: Domain Validation Suite & Stage 2 Async Hook
- Implement `NarrativeChronicleValidator` verifying:
  - Ring buffer capacity wrap-around and chronologically sorted retrieval.
  - Friendly name resolution correctness.
  - Zero heap allocation during continuous event emission and querying.
  - Defensive null safety when dependencies are absent.
- Define `ILlmNarrativeEnricher` contract preparing for Track A Epic 11 integration.

---

## 4. Acceptance Criteria

1. **Functional Acceptance**:
   - The "RECENT SECTOR EVENTS" feed displays natural language sentences (e.g. `"[Just now] Burt shared campfire gossip with Bob in Common Hearth."`).
   - Timestamps show humanized relative time (`[Just now]`, `[30s ago]`, `[1m ago]`) based on the simulation clock.
   - All agents are referred to by friendly human names (`Burt`, `Bob`, `Alice`) instead of GameObject debug strings (`SwarmAgent_6`).
   - Switching sectors in the left menu immediately updates the event feed with events that occurred in that specific sector.
2. **Performance & Memory Acceptance**:
   - Generating and querying chronicle entries produces **0 bytes of GC allocation** per frame in the update loop.
   - Event storage operates on a bounded ring buffer that never causes unbounded memory growth.
3. **Architectural Decoupling**:
   - `Simpiens.Narrative` is pure C# and can run 100% headlessly in automated tests without importing `UnityEngine.UIElements`.
   - `FacilityDashboardPresenter` acts purely as a dumb view reading from `INarrativeChronicleService`.
