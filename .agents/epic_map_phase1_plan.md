# Epic MAP Phase 1: The World as Data (Implementation Plan)

- **Epic:** [Epic MAP Revision 2](../docs/epics/epic-map.md), Phase 1. Status is owned by `docs/ROADMAP.md`.
- **Branch:** `feat/epic-map-revamp`
- **Written:** 2026-10-11, against `main` @ `c21e91e` (PR #1 merged).
- **Implementation status (2026-10-11):** steps 1–7 committed (`3cd4a95`..). All assemblies compile; every validator that can run outside Unity passes. **The PlayMode suite has not been run yet** (the Unity Editor held the project lock all session). Running it ×3 is the remaining Phase 1 gate (§5 id-shift risk).
- **Goal:** every piece of static world geography comes from one plain-data `MapDefinition`. Walls are written straight into the nav grid, sectors are owned by the simulation, and world bounds no longer depend on the camera.
- **Behaviour-preserving.** The live game still shows the current pen arena plus Outpost Commons, now expressed as `DevMapLayout`. The new outpost floor (`Floor1Layout`) is authored and validated headlessly but not spawned. It goes live in Phase 3; the Dev Map button arrives in Phase 2.

---

## 1. Findings from the code (why each step exists)

| # | Finding | Where | Consequence for Phase 1 |
| :-: | :--- | :--- | :--- |
| F1 | Walls are `NodeController` GameObjects (Rigidbody2D, CircleCollider r=0.55, BoxCollider 1.02). `SpatialHashGrid.UpdateFromRegistry` re-rasterizes them into `StaticNavGrid` every tick, inflated by `PawnBodyRadius`. They never enter the snapshot's entity cells. | `SwarmSpawner.SpawnPlayPens/SpawnWallNode`, `SpatialHashGrid.cs:172` | Static walls can go straight into the grid. Dynamic walls (`EdgeCaseInjector.InjectWall`, `NavigationClearanceValidator`) must keep the registry path. |
| F2 | When a wall disappears, `StaticNavGrid.Clear()` wipes everything except the playable bounds and relies on re-rasterization. | `SpatialHashGrid.cs:120`, `StaticNavGrid.Clear` | Static map walls need a **base layer** that survives `Clear()`, as the playable bounds already do. |
| F3 | The playable area is a hard-coded ±20 square (`NavConstants.PlayAreaHalfExtent`). The anomaly detector uses the same constant for boundary breaches. | `NavConstants.cs:31`, `VisualAnomalyDetector.cs:366` | Bounds become a per-map rect. Floor 1 (about 48 × 36) would not fit ±20. |
| F4 | Spatial partition size is `max(44×36, camera-derived)`, so **grid dimensions depend on screen aspect**. That makes the grid size, and with it the cell offsets, differ by machine and window: a latent determinism bug. | `SimulationStarter.Start` | Size the partition from the definition only. |
| F5 | `SwarmSpawner` registers before `SimulationStarter`, so entities are spawned before the partition is initialised. This works only because rasterization happens later, on the first tick. | `AppBootstrapper.Configure` | World building (partition init, base walls, sectors) must run first inside `Respawn()`. |
| F6 | Snapshots capture the `StaticNavGrid` reference when created and are pooled. Re-initialising the partition would leave pooled snapshots with a stale grid or wrong dimensions. | `SpatialHashGrid.CreateSnapshot/ReturnSnapshotToPool` | Re-initialisation needs a generation guard. Phase 1 doesn't switch maps, but the Phase 2 Dev Map toggle does. |
| F7 | The anomaly detector's wall-penetration check uses `Physics2D.OverlapCircle` against wall colliders, plus name matching on "Wall"/"Bounds"/"Obstacle". | `VisualAnomalyDetector.EvaluatePenetrationAndBounds` | Without wall GameObjects, the check must test geometry against the definition's wall rects. Physics stays for injected dynamic walls. |
| F8 | `ScreenBoundsGenerator` adds four viewport `BoxCollider2D`s named `*Wall`. The simulation never uses physics for movement; only the detector sees them. | `ScreenBoundsGenerator.cs` | Remove them. Camera framing comes from the definition. |
| F9 | Sectors (`FacilityZoneService`, `Simpiens.UI.Zones`) are read and written by `SimulationManager` (`GetSectorForPosition`, `ResolveSectorTarget`, `SetSectorExplored` on scout fulfilment) and by `TemplateNarrativeFormatter`. Explored state is never reset on respawn. | `SimulationManager.cs:162,231,433` | Add a simulation-owned `ISectorRegistry`, loaded per map and reset on respawn. `FacilityZoneService` becomes a UI adapter. |
| F10 | Pen setup captures node and anchor ids (`out var bushFearId`, `campfirePen8Id`, …) to seed scripted memories and groups. | `SwarmSpawner.Respawn` | Definition entries carry a **key**, and the builder returns key → id so session scripts can find them. |
| F11 | `OutpostMapBuilder` uses `GUID.Generate()` (not deterministic), and only its validator, a PlayMode wrapper and an editor menu use it. | `Environment/`, `SwarmPlayModeTests.cs:237`, `Core/Editor/SetupFacilityUIAsset.cs:19` | Delete it. Replace its validator. |
| F12 | Coordinates are hard-coded in `SwarmSpawner` (`OutpostStallPosition`, nodes, titles), `FacilityZoneService` (rects, ids 11–13) and the scenarios. | — | All of them move into `DevMapLayout` and are read through the definition. |

---

## 2. Design

### 2.1 Types (new namespace `Simpiens.Simulation.World`, folder `Assets/Scripts/Simulation/World/`)

Plain C#, immutable after build, no `UnityEngine.Object`. `Vector2` and `Rect` are allowed; the simulation already uses them.

```csharp
public sealed class MapDefinition
{
    public string Name;                                        // "Floor1", "DevMap"
    public Rect PlayableBounds;                                // cells outside are blocked (replaces ±20)
    public Rect CameraFrame;                                   // what the camera fits on boot (view hint)
    public IReadOnlyList<WallRect> Walls;                      // solid geometry, world units
    public IReadOnlyList<SectorDefinition> Sectors;            // id 0 is reserved for "Transit/unzoned"
    public IReadOnlyList<ResourceNodeDefinition> ResourceNodes;
    public IReadOnlyList<PoiDefinition> Pois;                  // Stall, Hearth, Bunk (Phase 3), SpawnPoint
    public IReadOnlyList<MapLabel> Labels;                     // presentation hint: text, position, colour
}

public readonly struct WallRect { public readonly Rect Solid; }   // axis-aligned, 1-unit-thick runs today
public readonly struct SectorDefinition { byte Id; string Name; string Description; Rect Bounds;
                                          Vector2 Anchor; float AnchorRadius; bool StartsExplored; }
public readonly struct ResourceNodeDefinition { string Key; Vector2 Position; ItemType Item; int Yield; uint RegrowTicks; }
public readonly struct PoiDefinition { string Key; PoiKind Kind; Vector2 Position; float Radius; }
public enum PoiKind : byte { Stall, Hearth, Bunk, SpawnPoint }
```

- **`MapDefinitionBuilder`**: a fluent authoring helper. `WallRun(from, to)` turns an axis-aligned run of unit wall cells into one `WallRect`; it also offers `Sector(...)`, `Node(...)`, `Poi(...)`, `Label(...)` and `Build()`. `Build()` validates cheap invariants: unique keys, unique sector ids, no sector id 0, everything inside `PlayableBounds`.
- **Coordinates stay origin-centred.** `StaticNavGrid`, `SpatialHashGrid` and `SharedWorldSnapshot` all compute `offset = size / 2`. The partition therefore covers the symmetric extent `2 × max(|min|, |max|)` per axis plus a 2-unit margin, and `PlayableBounds` blocks the rest. Generalising the grid origin is **out of scope** (it touches every cell computation).

### 2.2 Services

| Type | Responsibility | Notes |
| :--- | :--- | :--- |
| `WallRasterizer` (static) | `Rasterize(MapDefinition, StaticNavGrid)`: marks every cell overlapping `wall.Solid` inflated by `NavConstants.PawnBodyRadius + WallRasterMargin` | Pure, headless, zero-alloc. Must give the **same cell coverage** as the current node rasterization (r=0.55 + 0.25). Proven cell by cell by the parity validator (§4). |
| `StaticNavGrid` changes | `SetBaseLayer(bool[] blocked)` (or `MarkBaseWall`) kept through `Clear()`; `SetPlayableBounds(Rect)` replaces `SetPlayableHalfExtent(float)`; `PlayableBounds` getter | Dynamic walls still use `SetWall`. `Clear()` restores base layer + bounds and bumps the version. |
| `ISectorRegistry` / `SectorRegistry` | `Load(MapDefinition)` (resets explored to `StartsExplored`), `GetSectorAt(Vector2) → byte`, `TryGetSector(byte, out SectorDefinition)`, `All`, `IsExplored(byte)`, `SetExplored(byte, bool)`, `SetAllExplored(bool)` | Explored state is a `bool[256]` indexed by byte id. Main-thread writer only. Containment logic (anchor circle, then rect) is copied **exactly** from `FacilityZone.Contains`, preserving first-match order. |
| `IWorldMap` / `WorldMap` | `Load(MapDefinition) → LoadedWorld`: initialises or reconfigures the partition from the definition, rasterizes the base walls, loads the sectors, rebuilds regions. Exposes `Current` (the definition) and `WorldVersion` | The one entry point that builds a world. Null-safe dependencies (AGENTS.md test isolation). |
| `LoadedWorld` | `TryGetEntityId(string key, out GUID)`, filled in by the session as it spawns definition entries | Replaces the `out var bushFearId` captures (F10). |
| `ISpatialPartition.Configure(width, height, cellSize)` | Idempotent: does nothing when dimensions match, otherwise builds a new grid and bumps a generation. `ReturnSnapshotToPool` drops snapshots from an old generation. | Readers already holding an old snapshot keep a consistent old grid (F6). `Initialize` stays as a wrapper for existing validators. |
| `FacilityZoneService` (UI) | Becomes an adapter: `IFacilityZoneService` implemented over `ISectorRegistry`, mapping `SectorDefinition` → `FacilityZone`; occupancy helpers stay here | UI types and presenters stay as they are. The id constants (`OreRidgeZoneId`, …) move to `DevMapLayout`. |
| `MapView` (`Entities/`, MonoBehaviour) | Draws walls (one SpriteRenderer per wall cell under `[Map]`, **no colliders, no NodeController**) and labels from the definition | Same look as today. A tilemap or batching is Phase 4. |
| `MapCameraFramer` (replaces `ScreenBoundsGenerator`) | Fits the orthographic camera to `CameraFrame`; no colliders | Pan and zoom are Phase 4. |

### 2.3 Layouts (`Assets/Scripts/Core/Session/Layouts/`)

- **`DevMapLayout`** is an exact transcription of today's live geography:
  - pen walls (both rows), pen titles, Outpost Commons titles;
  - sector ids, names, descriptions, rects, anchors and default explored flags 0–13, as `FacilityZoneService` has them now;
  - the transit zone stays id 0;
  - every resource node with its key, item, yield and regrow time; the campfires (Hearth POIs, r=3); the stall (0, 9);
  - `PlayableBounds = (-20, -20, 40, 40)`;
  - `CameraFrame` matching the current framing (orthographic size 14.5 centred at (0, 0)).
  - Agent placements, relationships and groups are **session** content, so they stay in `SwarmSpawner` until Phase 2.
  - It lives in production code because it is still the live map. Phase 2 moves it to `Simpiens.Testing` as the Dev Map.
- **`Floor1Layout`**: the Revision 2 starter layout from epic §2.2:
  - Hub, Bunkhouse, Garden, Ore Quarry, Herb Grove, two shrouded Frontier sectors, Road Gate;
  - doors at least 3 cells wide, hub doors 4;
  - about 48 × 36.
  - Authored and validated only. Nothing spawns it until Phase 3. The exact coordinates are a Phase 1 minor decision, recorded in the epic doc.

### 2.4 Rewiring

- **`SwarmSpawner.Respawn()`:**
  1. `_worldMap.Load(DevMapLayout.Definition)` (first step; fixes F5).
  2. `MapView.Show(def)`.
  3. Spawn resource nodes, campfires and the stall by iterating the definition and recording their keys.
  4. The pen-script section resolves ids through `LoadedWorld.TryGetEntityId("pen1.contested", …)`.
  5. Delete `SpawnPlayPens`, `SpawnWallNode`, `SpawnPenTitle` and the inline node coordinates.
- **`SimulationStarter`:** remove the partition sizing (F4); keep the dashboard setup.
- **`SimulationManager`:** depend on `ISectorRegistry` instead of `IFacilityZoneService` for `GetSectorForPosition`, `ResolveSectorTarget` and scout `SetExplored`, with a null guard as today. `TemplateNarrativeFormatter` changes the same way.
- **`AppBootstrapper`:**
  - register `SectorRegistry`, `WorldMap` and `MapCameraFramer`;
  - remove `OutpostMapBuilder` and `ScreenBoundsGenerator`;
  - keep `FacilityZoneService` as the adapter.
- **`VisualAnomalyDetector`:**
  - boundary breach checks against `StaticNavGrid.PlayableBounds`, falling back to the ±20 constant when no map is loaded;
  - penetration tests the pawn circle (r × 0.7, as now) against `WallRect.Solid` from `IWorldMap.Current`;
  - the Physics2D path stays only for `EntityType.Wall` nodes (injected dynamic walls), and the name-based matching is removed.
- **Delete:** `Environment/OutpostMapBuilder.cs`, `IOutpostMapBuilder.cs`, `Testing/Environment/OutpostMapBuilderValidator.cs`, its PlayMode wrapper, and the `SetupFacilityUIAsset` menu hook. `Simpiens.Environment` is then empty and is removed.
- **Scenario and validator references:** `SwarmSpawner.OutpostStallPosition` and the `FacilityZoneService.*ZoneId` constants are replaced by `DevMapLayout` lookups in `OutpostSupplyLoopScenario`, `PriceResponseScenario`, `EconomySoakScenario` and `SupplyLoopValidator`.

---

## 3. Steps and commits

Each step must compile and keep PlayMode green (run `scripts/run-playmode-tests.sh`). Each commit body states the real test status.

| Step | Work | Gate | Commit |
| :-: | :--- | :--- | :--- |
| 1 | Add the `Simulation/World` types, `MapDefinitionBuilder`, `WallRasterizer`, the `StaticNavGrid` base layer and `SetPlayableBounds`. Nothing wired in. | `MapDefinitionValidator` (builder invariants, rasterizer inflation cases, base layer survives `Clear`) | `feat(world): plain-data map definition and wall rasterizer` |
| 2 | `DevMapLayout` transcription plus **`DevMapParityValidator`**: rasterize the legacy wall-node positions with today's formula and compare cell by cell with `WallRasterizer(DevMapLayout)`; also compare sector containment for a sample grid of points against today's `FacilityZoneService` | Parity: 0 differing cells, 0 differing sector lookups | `feat(world): DevMapLayout with legacy parity validator` |
| 3 | `ISpatialPartition.Configure` with the generation guard; `WorldMap.Load`; `SectorRegistry`; DI registration. Still not used by the spawner. | Validator: reconfigure while an old snapshot is retained (old reader stays consistent, pool drops stale snapshots); registry resets explored on `Load` | `feat(world): world map loader, sector registry, reconfigurable partition` |
| 4 | **Switch-over:** the spawner loads `DevMapLayout`; walls are no longer entities; `MapView`; key → id lookups; `SimulationStarter` stops sizing the world from the camera; `MapCameraFramer` replaces `ScreenBoundsGenerator`; anomaly detector checks geometry | **PlayMode full suite, 3 runs** (see §5 on id shift); a visual check in Editor that the screen looks the same | `refactor(session): live geography from DevMapLayout; walls are data` |
| 5 | `SimulationManager` / narrative use `ISectorRegistry`; `FacilityZoneService` becomes an adapter; constants move into the scenarios and validators | `FacilityZoneServiceValidator`, `BountyTerminalValidator` and `SupplyLoopValidator` pass unchanged in intent | `refactor(world): sectors owned by the simulation` |
| 6 | `Floor1Layout` plus its validator checks; delete `OutpostMapBuilder` and its validator and wrappers | `MapDefinitionValidator` on Floor1: (a) one connected region contains every sector (doors pass after inflation); (b) every node, POI and spawn point is walkable and in the Hub's region; (c) no sector overlaps, and every walkable cell maps to exactly one non-zero sector; (d) every door is at least 2 walkable cells wide after inflation; (e) building twice gives identical walkability and region labels | `feat(world): Floor1 layout (validated, not yet live); retire OutpostMapBuilder` |
| 7 | Docs: epic checklist (Phase 1 delivered items), ROADMAP Epic MAP line, `curentfocus.txt`, `architecture_history.md` (rasterizer constant, origin-centred decision, generation guard), the AGENTS.md layout section (`Simulation/World`, `Environment/` removed) | Docs agree with each other | `docs: Epic MAP Phase 1 complete` |

`DevMapParityValidator` is temporary. It is deleted in Phase 2, once the Dev Map is the canary session and parity with code that no longer exists means nothing.

---

## 4. Tests added or changed

- **New `MapDefinitionValidator`** (PlayMode wrapper `Scenario_MapDefinition_AllDomainValidationTestsPass`) covers:
  - builder invariants;
  - rasterizer inflation, including half-integer positions (pen rows sit at y = ±3.5, −5.5, −12.5);
  - base layer surviving `Clear()` after a dynamic wall is removed;
  - playable-bounds blocking;
  - the Floor1 checks (a)–(e).
- **New `DevMapParityValidator`** (temporary): wall-cell parity and sector-lookup parity.
- **New `WorldMapValidator`** covers:
  - partition reconfiguration with a retained snapshot;
  - stale snapshots dropped from the pool;
  - sector registry load and reset;
  - null-dependency isolation, which AGENTS.md requires.
- **Changed:**
  - `VisualAnomalyDetectorValidator`: the penetration case now uses a definition wall rect, with one dynamic-wall case kept;
  - `FacilityZoneServiceValidator` / `BountyTerminalValidator`: built through the adapter over a registry loaded with `DevMapLayout`;
  - `NavigationClearanceValidator`: unchanged (dynamic node walls still rasterize).
- **Removed:** `OutpostMapBuilderValidator` and its wrapper.
- **Expected count:** 40 − 1 + 3 = **42** PlayMode tests (plus the explicit soak).

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| **Entity ids shift.** Walls no longer consume allocator ids (about 150 pen wall nodes are spawned before the nodes and agents), so every seeded id changes. Ids feed tie-breaks and RNG keys, so seeded outcomes change. | Expected and accepted (decision log). Re-verify outcomes instead of comparing old runs: full PlayMode ×3. If a live scenario regresses, treat it as a real robustness finding and fix the cause; **don't** pad the allocator to keep the old ids. |
| The rasterizer differs from node rasterization at the edges | The parity validator (step 2) must show 0 differing cells before step 4 starts |
| Anomaly detector sensitivity changes (circle vs rect instead of circle vs collider) | Keep the r × 0.7 probe; validator cases for a pawn touching versus inside a wall; compare anomaly counts on the 3 anomaly scenarios before and after step 4 |
| Partition reconfiguration races background readers | Readers keep their retained snapshot and its old grid; the generation guard only affects pooling; validator case in step 3 |
| Explored state now resets on respawn (it never did before) | Correct behaviour; check whether any scenario relied on state leaking between tests (all scenarios respawn) |
| Camera framing changes the producer's view | `DevMapLayout.CameraFrame` reproduces the current size 14.5 at the origin; visual check in step 4 |

---

## 6. Out of scope (later phases)
- Session definitions, the **Dev Map toggle button** and moving pens to `Simpiens.Testing`: Phase 2.
- Spawning `Floor1Layout`, the bunk rest anchor, 12 crawlers, migrating scenarios to Floor 1: Phase 3.
- Tilemap rendering, shroud overlay, pan and zoom, sector-aware bounty picker: Phase 4.
- A non-centred grid origin, ScriptableObject-authored maps, map serialization.

## 6A. Deviations from this plan during implementation
- **No separate `MapCameraFramer`.** Camera framing lives in `MapView.FrameCamera`, because the camera is part of the map's view. `ScreenBoundsGenerator` was deleted with its viewport colliders.
- **Narrative stays on `IFacilityZoneService`.** The narrative formatter reads sector names through the UI adapter. It is presentation, not authoritative state, and switching it would have meant presenter churn for no gain.
- **`DevMapParityValidator` test 2.** It compared against the legacy zone table until step 5 deleted that table (proven identical at `7b610cb`). It now checks that the adapter reproduces the registry exactly.
- **Resource nodes sit inside inflated wall cells.** Some pen bushes (e.g. `pen1.forage` at y −2.2) lie within body reach of a pen wall, exactly as before. Validators assert snap-reachability (`TryGetRegion`), not cell walkability.
- **Dev Map playable bounds are x ±20, y ±18.** That is the former effective walkable area: the old 44×36 grid already cut y at ±18, inside the ±20 border.
- **No wall raster margin.** The rasterizer inflates by `PawnBodyRadius` only, and `WallHalfExtent = 0.55` reproduces legacy coverage exactly.
- **Solid fill rects.** `MapView` draws non-unit wall rects (Floor1's rock either side of the lower wing) as one stretched, darkened sprite.
- **Verification tooling.** I added a standalone compile check and an outside-Unity validator runner (scratch only, not committed). They run pure validators with a managed `ILogHandler`. Validators that create GameObjects or call native `GUID.Generate()` still need Unity.

## 7. Minor decisions taken in this plan (producer may override)
1. The definition is authored in code (`Floor1Layout`); ScriptableObjects can come later.
2. The grid stays origin-centred; the partition covers the symmetric extent.
3. Walls are drawn as per-cell sprites without physics until Phase 4.
4. `FacilityZoneService` is kept as a UI adapter rather than removed, to limit churn in the presenters.
5. Explored state resets on respawn.
6. The new namespace is `Simpiens.Simulation.World`; `Simpiens.Environment` is removed.
