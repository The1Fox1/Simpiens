# Epic MAP Phase 4: The Player's View (Implementation Plan)

- **Epic:** [Epic MAP Revision 2](../docs/epics/epic-map.md), Phase 4. Status is owned by `docs/ROADMAP.md`.
- **Branch:** `feat/epic-map-revamp` (continues from Phase 3, `bfb6793`; not yet merged).
- **Written:** 2026-10-12, after the producer's Phase 3 visual check passed (including bunk sleeping, `847e432`). **Approved** by the producer 2026-10-12 with D1 = A (contents hidden under the fog, crawlers stay visible) and D2 = yes (the HUD works over the map).
- **Implementation status (2026-10-12): steps 1–6 done** (`e352964`..`e2f5b01`). PlayMode **52/52** in 3 consecutive full runs, identical milestones each time (54 total; soak and probe explicit).
  - `ShroudViewScenario`: both frontier rooms covered at start; the Deep Seam charted at t356 and its fog gone 1.50 s later; the Overgrowth stays covered; the camera holds a followed crawler within 0.171 units at 10×.
  - `PlayerScoutScenario`: the player's Scout Overgrowth (default 25g) is claimed at t60 and charted at t344 (bound 520); its fog is gone 1.50 s later.
  - `OutpostSupplyLoopScenario` and `BunkRestScenario` milestones are unchanged from Phase 3 (sale t233–234, scout t356–357, gather t568–569; third sleeper t1010).
  - **Producer visual check passed** 2026-10-12, including the mouse and keyboard controls (§3 step 7).
- **Goal:** the 2D view becomes the place the game is played. The player sees the floor as rooms, sees the fog where the crawlers know nothing, moves the camera freely, follows a crawler with a click, and posts bounties over the map by room name, without switching to the full-screen dashboard.
- **What the producer sees after Phase 4:**
  - Each room has its own floor. Walls and rock are continuous tiles instead of hundreds of separate squares.
  - The **Deep Seam** and **Overgrowth** sit under fog, labelled *Uncharted*. Their veins and herb patches can't be seen (D1).
  - When the scout reports back, the Deep Seam's fog fades out over about a second and its veins appear.
  - **Scroll** zooms toward the cursor. **Right- or middle-drag** or **WASD/arrows** pans. **Home** reframes the whole floor.
  - **Click a crawler** to select and follow it: a ring and its name appear, and the dashboard's dossier shows it. **Click a room's floor** to select that room in the dashboard. **Esc** or panning stops following.
  - A slim header over the map shows the tick, speed, treasury and stock, with **Bounties** and **Merchant** buttons that open their windows over the map (D2). **Tab** still opens the full dashboard.
  - Posting a Scout bounty offers **only the uncharted rooms, by name**. Nothing on Floor 1 is "Sector 7" any more.
  - The Dev Map toggle still works. The pens have no fog (all charted since Phase 3) and use the same camera.

---

## 1. Findings from the code (why each step exists)

| # | Finding | Where | Consequence for Phase 4 |
| :-: | :--- | :--- | :--- |
| V1 | **Walls are one GameObject per cell.** `MapView.DrawWall` creates a `SpriteRenderer` GameObject per wall cell: about 270 on Floor 1. Rock is one wall sprite stretched and tinted. There is no floor: rooms are the camera's background colour, so the rooms can only be told apart by their labels. | `MapView.cs:62-95` | Walls, rock and floors move to Unity's Tilemap (the module is already installed). Floors are tinted per sector. |
| V2 | **The map gives the fog of war away.** Since Phase 3 the simulation hides uncharted sectors from crawlers, but the map view draws everything: the player sees the Deep Seam's veins before any crawler knows them. Charted and uncharted rooms look the same; only the dashboard's sector cards show a shroud. | `MapView`, `NodeController` views, `FacilityDashboardPresenter.cs:705-750` | A fog overlay per uncharted sector, driven by `ISectorRegistry.ExploredMask` (D1). |
| V3 | **The camera is fixed.** `MapView.FrameCamera` frames the whole 48 × 36 floor at load. There is no pan or zoom, and a crawler (0.7 units) is a few pixels tall at that framing. | `MapView.cs:47-55` | A camera rig: pan, zoom toward the cursor, clamped to the map, and follow. |
| V4 | **Nothing in the world is clickable.** A crawler can be selected only from the dashboard's agent cards (`SelectAgent`). | `FacilityDashboardPresenter.cs:610` | World picking for crawlers and rooms, feeding the existing `SelectAgent` / `SelectZone`. |
| V5 | **The 2D view has no HUD.** The header bar and the Merchant and Bounty windows sit inside `dashboard-root`, so they are hidden whenever the map shows. Posting a bounty means switching to the full-screen dashboard, which covers the map. | `FacilityDashboard.uxml:3-255` | Header and windows move out of `dashboard-root` (D2). |
| V6 | **The bounty sector picker is a pen-era slider.** It is a `SliderInt` over ids 1–10 with a default of 2, which on Floor 1 is the Bunkhouse. Posting a Scout bounty for the Overgrowth means knowing its id (7). | `FacilityDashboard.uxml:209`, `FacilityDashboardPresenter.cs:409-419, 1350` | A dropdown of real sector names: Scout lists only uncharted sectors. |
| V7 | **The simulation accepts any bounty sector.** `PostBountyInternal` takes gold into escrow for a Scout bounty on an already-charted sector, or for a sector id that doesn't exist. A claimant then "scouts" a charted room, or nobody can ever fulfil it. | `SimulationManager.cs:628-720` | Validated in the authoritative layer (the multiplayer server will need this too), not only in the picker. |
| V8 | **Sorting orders are ad hoc**: rock and bunks −1, walls and crawlers 0, the hearth 1, labels 10, all set in four places. Floors, fog and a selection ring need a defined stack. | `MapView`, `SwarmSpawner`, `CampfireView` | One `RenderOrder` table in `Entities/`. |

---

## 2. Design

### 2.1 Floors and walls on a Tilemap (V1, V8)
- `MapView` builds one `Grid` with three `Tilemap`s, all generated from the `MapDefinition`:
  - **Floor:** every walkable cell inside a sector gets a floor tile tinted by its sector. The tint comes from the sector's label colour, darkened, so no new data is needed.
  - **Rock:** solid fills (`WallRect`s wider than one cell).
  - **Walls:** wall runs.
- Tiles are made at runtime (`Tile` instances) from procedural sprites in `AgentSpriteLibrary`, as all current art is. No asset files.
- **The tiles come from the same rasterisation as the nav grid** (`WallRasterizer`), so what the player sees and what the pathfinder treats as walls cannot disagree. A validator checks this cell for cell.
- **`RenderOrder`**, one table: Floor −30, Rock −20, Walls −10, Props 0 (bunks, hearth, stall, resource nodes), Fog 10, Crawlers 20, Selection 25, Labels 30.
- Labels stay `TextMesh`. Uncharted sectors add an *Uncharted* line under the name.

### 2.2 Fog of war on the map (V2, decision D1)
- **`ShroudView`** (a view in `Entities/`): one overlay per sector that is uncharted at load. Each overlay is a quad over the sector's bounds with a procedural cloud texture, at `RenderOrder.Fog`.
- It reads `ISectorRegistry.ExploredMask` once per frame. That is one `ulong` comparison, and it does no work unless a bit changes.
- **When a sector is charted,** its overlay fades out over 1.5 s of real time (unscaled, so the fade is visible at 10× too), then is destroyed.
- Rebuilt with the map on every session load. The Dev Map has no uncharted sectors, so it shows no fog.
- The view owns no state: a respawn or a future save/load gives the right fog from the registry alone.
- **D1 = A:** the fog is opaque over the room's contents (props sort below it) and crawlers walking inside are drawn above it.

### 2.3 Camera rig (V3)
- **`CameraRig`**: plain C# camera maths, so it can be tested headless:
  - clamp the view to `MapDefinition.CameraFrame` plus a 2-unit margin;
  - zoom keeps the world point under the cursor fixed;
  - zoom range: orthographic size 3 (about one room) up to the size that frames the whole map;
  - smooth follow of a target position;
  - reframe.
- **`CameraController`** (MonoBehaviour, view only): reads the Input System and drives `CameraRig`.
  - Controls: scroll zoom; right- or middle-drag pan; WASD and arrows pan, scaled by zoom; Home reframes.
  - It runs on unscaled time, so the camera works while the simulation is paused or at 10×.
  - It ignores input while the full dashboard is open, or while the pointer is over a UI Toolkit element (`panel.Pick`).
- `MapView.FrameCamera` becomes `CameraRig.Reframe` on map load. The fixed framing is no longer the camera's only state.

### 2.4 Picking, selection and follow (V4)
- **`WorldPicker`** (plain C#): given a world point, it returns the nearest crawler within 0.6 units, with ties going to the lower entity id. It reads `IWorldRegistry` pawn positions and needs no physics raycast.
- **A left click (no drag):**
  - on a crawler: `SelectAgent(id)`, and the camera follows it;
  - on empty floor: `SelectZone(sector at the point)`, and the camera stops following;
  - in fog: crawlers are visible (D1 = A), so a click on one selects it like anywhere else.
- **Selection view:** a ring under the selected crawler and its name above, at `RenderOrder.Selection`.
- **Follow stops** on Esc, on a pan input, or when the crawler is removed (session switch).
- Selection is view state in the presenter, as now. Nothing enters the simulation.

### 2.5 The view's HUD (V5, decision D2)
- The header bar (tick, speed, treasury, stock, the Bounties and Merchant buttons) and the two modal windows move out of `dashboard-root` into an always-on layer. The full dashboard panels (sectors, sector detail, dossier) stay behind Tab.
- The presenter's refresh loop runs while the header is visible, not only while the dashboard is. Today `Update` returns early when the dashboard is hidden, so the header would freeze.
- **No new panels.** The selected crawler's details stay in the dossier (Tab). A compact in-view crawler card is UI-3 territory and is not part of this phase.

### 2.6 Sector-aware bounties (V6, V7)
- **Picker:** the sector slider becomes a `DropdownField` of sector names from `IFacilityZoneService`, rebuilt when the map or the explored mask changes.
  - **Scout:** only uncharted sectors. If there are none, the Post button is disabled with *Every sector is charted*.
  - **Gather:** every sector. Uncharted ones are marked *(claimable once charted)*. This matches the seeded Deep Seam Gather bounty, which waits for the scout.
- **Authoritative validation** in `PostBountyInternal` (V7):
  - an unknown sector is rejected;
  - a Scout bounty on a charted sector is rejected.
  - A rejected post takes no escrow. It logs a Warning (`SimLog.Intervention`) and leaves a feedback reason that the presenter shows.
  - Seeded bounties pass the same check.
- **Wording:** "Uncharted" replaces "UNEXPLORED / SHROUDED" in the dashboard, and the bounty cards already show sector names.

### 2.7 Out of the way of Phase 5
- Nothing here changes simulation behaviour, except the bounty validation (§2.6). Crawler milestones must not move: the supply loop and the bunk scenario are re-run, and their numbers must match Phase 3's within run-to-run noise.
- The open findings (jitter, starvation margin, bunk-claim churn) stay with Phase 5.

---

## 3. Steps and commits

Each step must compile and keep PlayMode green (`scripts/run-playmode-tests.sh`, Editor closed). Each commit body states the real test status.

| Step | Work | Gate | Commit |
| :-: | :--- | :--- | :--- |
| 1 | **`RenderOrder` and the Tilemap map** (§2.1). Floors per sector, rock, walls; uncharted label line. | New `MapViewValidator` (PlayMode wrapper; it needs GameObjects): every static-blocked nav cell has a wall or rock tile, every walkable sector cell has a floor tile, and no tile sits on a cell the nav grid disagrees with; the map root has under 20 GameObjects (about 270 today); the Dev Map passes too. Full suite. | `feat(view): the map is drawn on a tilemap with floors per room` |
| 2 | **`ShroudView`** (§2.2), per D1. | New `ShroudViewScenario` (live, no injection): at start both frontier sectors are covered; the Deep Seam overlay is gone within 2 s (real time) of the seeded scout's report, while the Overgrowth's stays; the Dev Map shows no overlay. Full suite. | `feat(view): fog of war over uncharted rooms on the map` |
| 3 | **`CameraRig` and `CameraController`** (§2.3). | New `CameraRigValidator` (headless): clamping at every edge and zoom level; zoom keeps the cursor point fixed; reframe fits the map; follow converges. Full suite. | `feat(view): pan, zoom and reframe the camera` |
| 4 | **Picking, selection and follow** (§2.4). | `CameraRigValidator` adds `WorldPicker` checks: nearest within the radius, ties by id, nothing beyond the radius. `ShroudViewScenario` adds: selecting a crawler makes the camera centre stay within 0.25 units of it for 200 frames. | `feat(view): click a crawler to follow it, click a room to select it` |
| 5 | **HUD over the map** (§2.5), per D2. | The existing UI validators; the presenter refreshes the header with the dashboard hidden (new check in `BountyTerminalValidator`); full suite. | `feat(ui): header, bounty board and merchant work over the map` |
| 6 | **Sector-aware bounties** (§2.6). Picker plus authoritative validation. | `BountyTerminalValidator`: the Scout picker lists exactly the uncharted sectors; rejected posts take no escrow and log a reason. New **`PlayerScoutScenario`** (live): post *Scout Overgrowth* through the presenter's picker at the default reward, with no other help. Within its bound the bounty is claimed and reported, the Overgrowth is charted, and its fog lifts. The bound comes from observation, slowest of 3 × 1.5. Full suite ×3. | `feat(ui): bounties target rooms by name; the simulation rejects impossible ones` |
| 7 | **Docs and the producer's visual check.** ROADMAP, the epic checklist, `curentfocus.txt`, `architecture_history.md` (map on a tilemap, view-side fog, HUD over the map, authoritative bounty validation), AGENTS.md (`Entities/` view files). **Producer check in the Editor:** §0's list. | Docs agree with each other; producer confirms | `docs: Epic MAP Phase 4 complete` |

---

## 4. Tests added or changed

- **New:**
  - `MapViewValidator`;
  - `CameraRigValidator` (headless, includes `WorldPicker`);
  - `ShroudViewScenario` (live);
  - `PlayerScoutScenario` (live).
- **Changed:** `BountyTerminalValidator` (the picker, rejected posts, refresh with the dashboard hidden).
- **Re-checked, unchanged:** `OutpostSupplyLoopScenario` and `BunkRestScenario` milestones match Phase 3 (§2.7).
- **Expected count:** 47 + 4 = **51** PlayMode tests, plus the two explicit runs (soak, probe).
- **Not automated:** the Input System bindings (a key or a drag reaching `CameraRig`). The rig and the picker are tested through their APIs. The bindings are a thin mapping, covered by the producer's visual check.

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| Clicks on the HUD also hit the world (select a crawler behind a button) | `CameraController` checks `panel.Pick` before handling a click or a drag; a validator check where possible, and the visual check |
| Batch-mode PlayMode tests have no real display | Tests assert view state (tiles, overlay alpha, camera position), never pixels |
| The fog overlay drifts from the simulation's charted state | `ShroudView` reads the same `ExploredMask` cognition reads; `ShroudViewScenario` asserts the overlay follows the report |
| Moving the header and windows out of `dashboard-root` breaks USS selectors or the Tab toggle | Step 5 is on its own; the UI validators and the visual check gate it |
| Bounty validation changes seeded or scenario bounties | Seeded bounties are valid by construction (Scout targets the uncharted Deep Seam). The full suite ×3 at step 6. |
| Zoomed-in `TextMesh` labels look blurry | Accepted for now (§7 #7); switching to TextMeshPro is a contained follow-up |

---

## 6. Out of scope
- **Phase 5:** `OutpostLifecycleScenario`, the determinism check and the zero-anomaly bar, including the open jitter finding.
- **Art:** the art stays procedural pixel art. No artist assets, no animation system (`847e432` set the pattern for new looks).
- **UI-3 (Milestone 2):** an in-view crawler card, roster and expedition panels.
- **Time controls** (pause and speed keys) unless the producer adds them (§7 #8).
- **Phase 2 §8 #1** (Pen 7) and the Phase 3 open findings (§8 of the Phase 3 plan): unchanged.

## 6A. Deviations from this plan during implementation
- **Tile alignment per map:** the Dev Map's walls are centred on half-number rows, so `MapView` takes its cell alignment from each map's walls instead of assuming whole numbers. Floor is drawn on every non-wall cell in the playable bounds (neutral where no room is), because the Dev Map has walkable cells outside its pens.
- **Follow moves with the target**, then closes the remaining gap: plain exponential follow trailed a crawler walking at 10× by about 3 units. The follow test settles for a second of real time first (batch-mode frames are very short).
- **`MapHudScenario` is a fifth new test** (the plan expected 4): checking the HUD needs the real dashboard, so it is a live scenario rather than a `BountyTerminalValidator` check. Scenarios that use the dashboard create it, as `SessionSwitchScenario` does.
- **The "pointer over UI" rule** counts any pickable element except the document's own root and template container, which fill the screen without drawing anything.
- **Extra fixes, each found on the way** (one commit each, or folded into the step that found it):
  - `629f3f9` (before this phase): bunks had a dynamic physics body (`NodeController` requires one) and were shoved around by crawlers.
  - step 5: the Bounties and Merchant buttons needed two presses the first time (a UXML `display: none` reads as Flex through `style.display`).
- **`PlayerScoutScenario` bound:** t344 in the full suite (t284 when run alone), × 1.5 = 520.
- **Not automated, as planned:** the Input System bindings themselves (a key or a drag reaching `CameraRig`).

## 7. Decisions

**D1 (decided by the producer 2026-10-12: A): what the player sees inside an uncharted room.** The crawlers' rule is settled (Phase 3 D1). This is the player's.

| Option | The player sees | Trade-off |
| :--- | :--- | :--- |
| **A (recommended)** | Walls, the room's name and *Uncharted*. Its contents (veins, patches) are hidden under fog. Crawlers walking inside, such as the scout, stay visible above the fog. | The player knows no more about the room than the crawlers do, and can still watch their scout work. |
| B | Walls and the name only. Crawlers inside are hidden too, so the scout vanishes into the fog until it comes back. | The most honest; it adds suspense but loses sight of the scout. Clicking into the fog selects the room, never a crawler. |
| C | Everything, dimmed. | The player knows more than any crawler, which undercuts the Phase 3 decision. |

**D2 (decided by the producer 2026-10-12: yes, "control over map would be nice"): HUD over the map.** The recommendation was **yes** (§2.5): the header and the Bounty and Merchant windows work over the map, and the full dashboard stays behind Tab. The alternative is the current flow: Tab to the full-screen dashboard to post anything.

**Minor decisions (producer may override):**
3. **Unity Tilemap**, not a hand-built mesh. It is already installed, batches the drawing, and is the standard 2D grid renderer.
4. **Controls** as in §0. Mouse buttons and keys are one table in `CameraController`.
5. **The fog fades in real time** (1.5 s unscaled), so it is visible at any speed.
6. **The simulation validates bounty sectors** (§2.6). The picker alone isn't enough: a future multiplayer server must reject the same commands.
7. **Labels stay `TextMesh`** unless zoomed-in blur is visible in the check.
8. **No time controls in this phase.** Space to pause and 1/2/3 for speed would be about an hour of work on the existing `SimulationManager.Pause` and clock multiplier; say so and they go into step 5.
