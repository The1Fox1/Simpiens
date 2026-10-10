# Epic UI-1: The Flat "Facility Dashboard" Menu UI

- **Status**: COMPLETED (Branching UI Track)
- **Track**: Track B: UI / UX & Player Interface
- **Preceding Epic**: None (Origin of Track B)
- **Following Epic**: [Epic UI-Alpha: The Humanized Narrative Chronicle & Story Feed](epic-ui-alpha.md)
- **Architectural Layer**: Presentation (`Simpiens.UI`, UI Toolkit UXML/USS, `FacilityDashboardPresenter`) & Spatial Querying (`FacilityZoneService`)

---

## 1. Executive Summary & Problem Statement

Navigating, selecting, and inspecting autonomous agents across a continuous 2D spatial canvas with camera controls, sprite occlusion, and dynamic movement creates significant UX friction for an early MVP. 

Rather than spending weeks tuning 2D selection colliders, camera bounds, and micro-animation readability, **Epic UI-1** introduces a **static, web-like management interface (Facility Dashboard)** built using **Unity UI Toolkit**.

### Key Architectural Tenet: The Dual-View Model
- **No Engine Rewrites**: The authoritative simulation continues running on the continuous 2D spatial engine (`SimulationManager`, `SpatialHashGrid`, `AutonomousAgent`, `GoapPlanner`).
- **Zero Allocations in UI Binding**: Leveraging UI Toolkit's virtualized `ListView` recycling, streaming simulation updates will produce **0 bytes of GC allocation per tick**.
- **Side-by-Side Coexistence**: A simple toggle key (`Tab`) switches seamlessly between the 2D Viewport and the Fullscreen Facility Dashboard, preserving all automated PlayMode tests and visual debugging tools.

---

## 2. Technical Architecture & Wireframe

The interface organizes the facility into a 3-column command dashboard:

```
┌───────────────────────────────────────────────────────────────────────────────────────────────────┐
│ SIMPIENS FACILITY OS v0.9                        [Tick: 01420] [Sim Speed: 1x 2x 4x] [View: Map] │
├──────────────────────────┬────────────────────────────────────────────┬───────────────────────────┤
│ SECTORS / ROOMS          │ ACTIVE SECTOR: Campfire Hearth (Pen 4)     │ AGENT DOSSIER             │
├──────────────────────────┼────────────────────────────────────────────┼───────────────────────────┤
│ [●] Common Hearth   (3)  │ AGENTS PRESENT (3):                        │ SwarmAgent_6 (Companion)  │
│ [ ] Berry Orchard   (2)  │ ┌────────────────────────────────────────┐ │ ───────────────────────── │
│ [ ] Isolation Ward  (1)  │ │ SwarmAgent_6  [Status: Relaxing]       │ │ Needs:                    │
│ [ ] Quarters B      (0)  │ │ Target: Campfire Anchor | Comfort: High│ │ • Hunger:   [██░░░░░░] 20 │
│ [ ] Perimeter Gates (4)  │ └────────────────────────────────────────┘ │ • Energy:   [████████] 85 │
│                          │ ┌────────────────────────────────────────┐ │ • Social:   [██████░░] 70 │
│                          │ │ SwarmAgent_7  [Status: Gossiping]      │ │ • Frustr:   [░░░░░░░░]  0 │
│                          │ │ Conversing with: SwarmAgent_6          │ │                           │
│                          │ └────────────────────────────────────────┘ │ Active Goal:              │
│                          │                                            │ > GatherAtAnchorGoal      │
│                          │ RECENT SECTOR EVENTS:                      │                           │
│                          │ • [1410] SwarmAgent_6 shared food with _7  │ Social Affinities:        │
│                          │ • [1395] SwarmAgent_7 recovered from panic │ • Agent_7: W:+85 T:+80    │
│                          │ • [1350] Hearth gossip exchange completed  │ • Agent_8: W:-20 T:-10    │
├──────────────────────────┴────────────────────────────────────────────┴───────────────────────────┤
│ PLAYER INTERVENTIONS: [Drop Ration Crate] [Trigger Alarm / Rally] [Quarantine Sector]            │
└───────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Epic UI-1 Implementation Phases

### Phase 1: Facility Zone & Sector Mapping
- Create pure C# `FacilityZone` struct and `FacilityZoneService`.
- Map world coordinates to human-readable sectors based on proximity to registered `AnchorData` or bounding rectangles (`Rect`).
- Provide zero-allocation query APIs: `GetAgentsInZone(zoneId, buffer)` and `GetZoneForPosition(Vector2 pos)`.

### Phase 2: UI Toolkit Layout & CSS Design (UXML / USS)
- Create `FacilityDashboard.uxml` with standard web-like flexbox architecture.
- Create `FacilityDashboard.uss` with high-contrast, dark-mode terminal styling (`#0f172a` slate palette, `#10b981` emerald accents, `#f59e0b` amber warnings).
- Configure virtualized `ListView` templates for Sector Rows and Agent Cards to prevent GC allocation spikes.

### Phase 3: Telemetry & Dossier Data Binding
- Implement `FacilityDashboardPresenter : MonoBehaviour` injected via VContainer.
- Bind reactive data feeds:
  - Sector occupancy counts.
  - Agent status cards (Name, Visual State badge, current activity).
  - Selected agent dossier: Hunger / Energy / Social progress bars, active GOAP goal, current action, 4-axis relational affinities (`Warmth`, `Trust`, `Fear`, `Respect`), and recent `MemoryLedger` entries.

### Phase 4: Dual-View Controller & Intervention Hookup
- Implement `ViewToggleController` listening to `Tab` / `ESC` inputs to toggle the UI Document overlay and main camera rendering.
- Wire intervention buttons to enqueue events in `PlayerInterventionService`.

---

## 4. Acceptance Criteria

1. **Functional Acceptance**:
   - Player can press `Tab` to bring up the full-screen Facility Dashboard.
   - Clicking a sector (e.g. "Campfire Hearth") lists all agents currently located within that area in real-time.
   - Clicking an agent displays their full physiological dossier, active GOAP goal, relational matrix, and recent memory events.
   - Pressing `Tab` returns immediately to the 2D spatial view with zero loss of simulation state.
2. **Performance Acceptance**:
   - Ticking the dashboard produces **zero heap allocations** per frame during real-time data streaming.
   - Frame rate remains stable at 60+ FPS with 20+ active agents simulated.
