# Epic UI-2: The Merchant Terminal & Bounty Board

- **Status**: COMPLETED
- **Track**: Track B: UI / UX & Player Interface (Branching Track)
- **Production Milestone**: [Milestone 1: The Outpost Loop (MVP)](../ROADMAP.md#3-production-milestones-overview)
- **Preceding Epic**: [Epic UI-Alpha: The Humanized Narrative Chronicle & Story Feed](epic-ui-alpha.md)
- **Following Epic**: Epic UI-3: Guild Roster & Expedition Dispatch
- **Aligned Engine Epic**: [Track A Epic 9: The Outpost Economy & Public Bounties](epic-9.md)
- **Architectural Layer**: 
  - **Player Intervention Pipeline**: `Simpiens.Intervention` (`PlayerInterventionService`, command-pattern `InfluenceEvent` queue)
  - **Presentation Layer**: `Simpiens.UI` (`FacilityDashboardPresenter`, `VisualElementPool<TData, TElement>`, `FacilityZoneService` exploration tracking)
  - **Authoritative Simulation Loop**: `Simpiens.Simulation` (`SimulationManager.Tick()`, `TradeStallData`, `BountyContract`, `NarrativeChronicleService`)

---

## 1. Executive Summary & Design Goals

With the foundational Facility Dashboard ([Epic UI-1](epic-ui-1.md)) and narrative chronicle feeds ([Epic UI-Alpha](epic-ui-alpha.md)) established, **Epic UI-2** delivers the core interactive player loop for **Production Milestone 1 (The Outpost Loop)**.

Instead of passively observing autonomous agents, players can actively direct the outpost's economy and exploration efforts:
1. **The Merchant Terminal**: Inspect the central outpost trade stall, monitor treasury reserves, and configure dynamic commodity prices (provision sales and salvage buy-backs) via interactive sliders.
2. **The Public Bounty Board**: Create public task directives (`GatherResource` and `ScoutSector`) backed by escrowed gold rewards, monitor claimed contracts, and cancel unneeded bounties with full treasury refunds.
3. **Economic HUD & Stockpile Metrics**: Track Outpost Gold, Provision Stocks (Rations), Salvage Reserves (Iron Ore & Medicinal Herbs), and Active Contracts continuously from the top header bar.
4. **Sector Shroud & Exploration Tracking**: Observe unexplored wilderness sectors veiled in fog-of-war shrouds until mapped by scouted agents or completed bounties.
5. **Zero-Allocation High-Frequency UI Pooling**: Standardize continuous 10 Hz element updates via `VisualElementPool<TData, TElement>`, guaranteeing `0 bytes GC` heap allocations during gameplay.

---

## 2. Technical Architecture & Command Pattern Flow

```mermaid
flowchart TD
    subgraph Track B: UI Presentation (Assets/Scripts/UI/)
        TerminalUI["Merchant Terminal & Bounty Board\n(Price Sliders, Bounty Form, Treasury HUD)"]
        Presenter["FacilityDashboardPresenter\n(10 Hz update loop)"]
        Pool["VisualElementPool<TData, TElement>\n(0 bytes GC Element Recycling)"]
        UXML["FacilityDashboard.uxml & .uss"]
    end

    subgraph Intervention Pipeline (Simpiens.Intervention)
        PIS["PlayerInterventionService\n(ConcurrentQueue<InfluenceEvent>)"]
        CmdPrice["InfluenceEvent: 'UpdateStallPrices'"]
        CmdPost["InfluenceEvent: 'PostBounty'"]
        CmdCancel["InfluenceEvent: 'CancelBounty'"]
    end

    subgraph Track A: Authoritative Simulation (Simpiens.Simulation)
        SimMgr["SimulationManager.Tick()\n(Dequeues InfluenceEvents)"]
        Stalls["TradeStallData & Treasury Escrow"]
        Bounties["BountyContract Board\n(Broadcast via SharedWorldSnapshot)"]
        Chronicle["NarrativeChronicleService\n(Emits BountyPosted / BountyExpired)"]
        Agents["Autonomous Agents (GOAP)\n(Claim bounties, deliver salvage, buy rations)"]
    end

    TerminalUI -->|User Interaction| Presenter
    Presenter -->|Enqueue InfluenceEvent| PIS
    PIS --> SimMgr
    SimMgr --> Stalls
    SimMgr --> Bounties
    SimMgr --> Chronicle
    Bounties -.-> Agents
    SimMgr -->|Read Snapshot| Presenter
    Presenter --> Pool
    Pool --> TerminalUI
```

### Strict Command Pattern Adherence
In strict accordance with the project's autonomous architecture guidelines (then `.agents/rules/autonomous-architecture.md`, retired 2026-10-24; today [`AGENTS.md`](../../AGENTS.md)):
- **Zero Direct State Mutation**: UI controls never alter simulation structs or engine state directly.
- **Influence Enqueuing**: All player actions are serialized into immutable `InfluenceEvent` command structs and enqueued onto `IPlayerInterventionService`.
- **Deterministic Tick Processing**: `SimulationManager.Tick()` drains the queue synchronously at the start of each simulation tick, ensuring thread-safe state mutations, escrow accounting, and event logging.

---

## 3. Implemented Components & Deliverables

### Component 1: Zero-Allocation UI Pooling Utility (`Simpiens.UI.Pooling`)
- [`VisualElementPool.cs`](../../Assets/Scripts/UI/Pooling/VisualElementPool.cs):
  - Generic UI Toolkit element recycling utility: `VisualElementPool<TData, TElement> where TElement : VisualElement`.
  - Lazily expands pooled elements when incoming data exceeds capacity.
  - Automatically manages element visibility (`display: Flex` vs `display: None`) and bindings without rebuilding the visual tree.
  - Eliminates garbage collection pressure on high-frequency (10 Hz) refresh loops.

### Component 2: Sector Exploration & Shroud Service (`Simpiens.UI.Zones`)
- [`FacilityZone.cs`](../../Assets/Scripts/UI/Zones/FacilityZone.cs): Added `IsExplored` property and `WithExplored(bool explored)` immutable copy helper.
- [`IFacilityZoneService.cs`](../../Assets/Scripts/UI/Zones/IFacilityZoneService.cs) & [`FacilityZoneService.cs`](../../Assets/Scripts/UI/Zones/FacilityZoneService.cs):
  - Manages dynamic sector exploration state (`HashSet<int> _exploredZoneIds`).
  - Outpost core sectors (0 through 5) initialize as explored; peripheral wilderness sectors (6+) start shrouded.
  - Exposes `IsSectorExplored(int zoneId)`, `SetSectorExplored(int zoneId, bool explored)`, and `SetAllSectorsExplored(bool explored)`.

### Component 3: UXML & USS Layout Updates (`Assets/Resources/UI/`)
- [`FacilityDashboard.uxml`](../../Assets/Resources/UI/FacilityDashboard.uxml):
  - **Top Navigation HUD**: Added `treasury-gold-label`, `provisions-stock-label`, `salvage-stock-label`, `bounties-active-label`, `btn-merchant-terminal`, and `btn-bounty-board`.
  - **Merchant Terminal Modal Overlay** (`merchant-terminal-overlay`): Outpost stall status, treasury balance, inventory readouts, interactive sliders for Ration sell price, Iron Ore buy price, and Herb buy price, live value readouts, and `btn-apply-prices`.
  - **Bounty Board Modal Overlay** (`bounty-board-overlay`): Two-column layout featuring active contract list (`bounties-scroll-list`), count badge, and a contract creation form with type dropdown, sector selector, item dropdown, quantity slider, gold reward slider, duration slider, and `btn-post-bounty`.
- [`FacilityDashboard.uss`](../../Assets/Resources/UI/FacilityDashboard.uss):
  - Styled economic HUD metric pills (`.hud-gold`, `.hud-stock`, `.hud-bounties`), navigation buttons, modal windows, sliders, shrouded sector cards (`.sector-card-shrouded`, `.shroud-badge`), and bounty contract cards (`.bounty-card`, `.bounty-badge-open`, `.bounty-badge-claimed`, `.bounty-badge-cancelled`).

### Component 4: Dashboard Presenter Binding (`Simpiens.UI`)
- [`FacilityDashboardPresenter.cs`](../../Assets/Scripts/UI/FacilityDashboardPresenter.cs):
  - Integrated `IPlayerInterventionService` for command dispatches.
  - Linked top economic HUD labels to `_simulationManager.ActiveStalls` and `_simulationManager.ActiveBounties`.
  - Bound price sliders to `UpdateStallPrices` influence events.
  - Bound bounty form to `PostBounty` influence events with pre-flight treasury gold validation.
  - Bound bounty contract cancellation buttons to `CancelBounty` influence events.
  - Integrated `VisualElementPool<BountyContract, VisualElement>` for active contract list recycling.
  - Enhanced sector list rendering with shroud badges and descriptions for unmapped regions.

### Component 5: Simulation Startup Seeding (`Simpiens.Testing.Environment`)
- [`SwarmSpawner.cs`](../../Assets/Scripts/Core/Session/SwarmSpawner.cs):
  - Automatically seeds an Outpost `TradeStallData` at Central Corridor (`Vector2.zero`) with 250 Treasury Gold, 50 Rations, 10 Iron Ore, and 10 Medicinal Herbs.
  - Seeds initial sample contracts (`GatherResource` for Iron Ore and `ScoutSector` for Wilderness) for immediate interactive verification.

### Component 6: Domain Validation Suite (`Simpiens.Testing.UI`)
- [`BountyTerminalValidator.cs`](../../Assets/Scripts/Testing/UI/BountyTerminalValidator.cs):
  - Automated test suite validating all 6 core domain invariants:
    1. `ValidatePriceUpdateInfluenceDispatch`: Verifies price update command execution through the intervention pipeline.
    2. `ValidateBountyPostingAndEscrow`: Verifies contract registration, gold escrow deduction from stall treasury, and chronicle logging.
    3. `ValidateBountyCancellationAndRefund`: Verifies contract status transition to `Cancelled` and complete gold escrow refund.
    4. `ValidateSectorShroudTracking`: Verifies sector fog of war toggling and bulk exploration updates.
    5. `ValidateZeroAllocationElementPool`: Verifies 1,000 UI sync cycles produce **0 bytes GC heap allocation**.
    6. `ValidateDefensiveNullSafety`: Verifies presentation operations safely no-op when dependencies are null.
- [`SetupFacilityUIAsset.cs`](../../Assets/Scripts/Core/Editor/SetupFacilityUIAsset.cs): Added Editor MenuItem `Simpiens/UI Toolkit/Run Bounty Terminal Validator`.
- [`AppBootstrapper.cs`](../../Assets/Scripts/Core/AppBootstrapper.cs): Registered `BountyTerminalValidator` in VContainer container.
- [`SwarmPlayModeTests.cs`](../../Assets/Tests/PlayMode/SwarmPlayModeTests.cs): Added NUnit test `Scenario_BountyTerminal_AllDomainValidationTestsPass`.

---

## 4. Verification & QA Matrix

| Test Suite / Inspection | Target Invariant | Result |
| :--- | :--- | :--- |
| `BountyTerminalValidator.ValidatePriceUpdateInfluenceDispatch` | Enqueuing `UpdateStallPrices` updates stall prices on next tick | **PASSED** |
| `BountyTerminalValidator.ValidateBountyPostingAndEscrow` | `PostBounty` deducts reward gold into escrow and logs to chronicle | **PASSED** |
| `BountyTerminalValidator.ValidateBountyCancellationAndRefund` | `CancelBounty` marks status cancelled and refunds escrow to stall | **PASSED** |
| `BountyTerminalValidator.ValidateSectorShroudTracking` | Explored vs. shrouded sector states toggle dynamically | **PASSED** |
| `BountyTerminalValidator.ValidateZeroAllocationElementPool` | 1,000 `VisualElementPool` sync iterations allocate 0 bytes GC | **PASSED (0 bytes)** |
| `BountyTerminalValidator.ValidateDefensiveNullSafety` | Null dependency injection in headless mode does not throw NRE | **PASSED** |
| Unity Compilation & Asset Database | C# code and UXML/USS compile cleanly with zero errors/warnings | **PASSED** |
