# Epic MAP Phase 2: Session Split; Pens Out of the Live Game (Implementation Plan)

- **Epic:** [Epic MAP Revision 2](../docs/epics/epic-map.md), Phase 2. Status is owned by `docs/ROADMAP.md`.
- **Branch:** `feat/epic-map-revamp` (continues from Phase 1, `84164d3`; not yet merged).
- **Written:** 2026-10-11.
- **Goal:** the live game boots into the outpost and never shows the pens. The pens become a test-only **Dev Map** session that a button switches to and back. A new `PenCanaryScenario` turns the pens from visual demos into real assertions.
- **What the producer sees after Phase 2:**
  - Pressing Play shows the **Outpost Commons** yard: 6 crawlers harvesting ore and herbs, selling at the stall, and taking the seeded Scout and Gather bounties.
  - No pens are visible.
  - A **DEV MAP** button switches to the 10-pen arena; **MAIN MAP** switches back.
  - The full outpost floor (`Floor1Layout`) goes live in Phase 3. Phase 2 deliberately keeps the yard the economy scenarios already pass on.

---

## 1. Findings from the code (why each step exists)

| # | Finding | Where | Consequence for Phase 2 |
| :-: | :--- | :--- | :--- |
| G1 | `SwarmSpawner.Respawn` mixes generic world building with session content: map load, node/hearth spawning, 24 hand-placed pen agents, relationship and group scripts, 6 crawlers, the stall and two seeded bounties. | `SwarmSpawner.cs:100-457` | Split it. The spawner keeps the generic part; session content moves into `ISessionDefinition` implementations. |
| G2 | `DevMapLayout` holds the pens **and** the Outpost Commons in one definition. The Commons is the only part the economy loop uses: stall (0, 9), ore/herb/food nodes, sectors 11–13. | `Layouts/DevMapLayout.cs` | Split the layout. `OutpostCommonsLayout` (production) is the interim live map; `DevMapLayout` (pens only) moves to `Simpiens.Testing`. |
| G3 | **The clock is never rewound on respawn.** Seeded bounties expire at absolute tick 6000 (`BountyContract.IsActive`: `currentTick <= ExpirationTick`), and the pen 6/7 rally directives at absolute tick 5000. After about 10 minutes of uptime, a respawn produces dead bounties and expired directives. Wander randomness is keyed on the absolute tick (`SimRandom.For(seed, id, tick, salt)`), so the same seed gives different behaviour depending on when the respawn happens, and PlayMode results depend on test order. | `SimulationClock.cs`, `SwarmSpawner.cs:277,436,447` | Required for the toggle: **respawn rewinds the clock to tick 0**. Sessions express seeded expiries relative to the session start. |
| G4 | The narrative chronicle is not cleared on respawn. The id allocator *is* reset, so old-session events would point at GUIDs that new entities reuse. | `NarrativeChronicleService.Clear` (never called) | Respawn clears the chronicle. |
| G5 | The dashboard defaults to sector 4 ("Common Hearth", a pen sector). It holds the selected agent and zone across a respawn. | `FacilityDashboardPresenter.cs:129-130` | On a session change, the presenter resets its selection to the first sector of the loaded map and clears the agent selection. |
| G6 | The dashboard root is hidden in 2D view (TAB), so a header-only button is unreachable while watching the map. | `FacilityDashboard.uxml`, `IsVisible` | The session button lives in a small always-visible overlay outside `dashboard-root`. |
| G7 | The three anomaly scenarios (navigation, contention, dynamic walls) run on the pens. `DynamicChaosScenario` injects walls at pen coordinates (0.8, 0) and (−3.2, 0). The economy scenarios index crawlers as `agents[ScriptedPenAgentCount + i]`. | `Tests/PlayMode/Scenarios/*` | The harness gains a per-scenario session. The anomaly scenarios opt into the Dev Map and read injection points from its layout. The economy scenarios stay on the outpost and index crawlers from 0. |
| G8 | The pens have no PlayMode assertions. Several intended outcomes are not chronicled: there is no `DirectiveComplied` or `LeadershipChallenged` emitter. The chronicle covers `ResourceYielded`, `ResourceClaimed`, `AltruisticFoodShare`, `CampfireCompanionship`, `GroupFormed`, `LeadershipAssumed` and `GroupSchism`. | `ChronicleEvent.cs`, emitters in `SimulationManager` and the handlers | `PenCanaryScenario` asserts on chronicle events where they exist, and on authoritative state elsewhere: group registry leader and membership, positions against directive radii, harvest records. |
| G9 | In-flight cognition is cancelled when an agent is destroyed (`AutonomousAgent.OnDestroy` cancels its CTS). `SimulationManager.ResetState` drains the intent queue. | `AutonomousAgent.cs:483`, `SimulationManager.cs:605` | The respawn path is already exercised by every PlayMode test. The toggle reuses it, and a switch scenario proves no stale intent reaches the new session. |
| G10 | `BatchPlaymodeController` (non-test batch runs) waits for 3 `[Gossip]` log lines or 25 s. The Commons has no hearth, so the live map produces no gossip until Phase 3. | `Testing/Environment/BatchPlaymodeController.cs` | Harmless (the timeout path). Noted, not changed. |
| G11 | `SetupFacilityUIAsset` (an editor menu) builds a container containing `SwarmSpawner`. | `Core/Editor/SetupFacilityUIAsset.cs:52` | It must register `OutpostSession` too, or resolution fails. |

---

## 2. Design

### 2.1 Session contract (`Assets/Scripts/Core/Session/`, production)

```csharp
public interface ISessionDefinition
{
    string Id { get; }                 // "outpost", "devmap"
    string DisplayName { get; }        // "Main Map", "Dev Map"
    MapDefinition Map { get; }
    void Populate(SessionBuilder session);   // agents, relationships, groups, stalls, bounties
}

public sealed class SessionBuilder
{
    public MapDefinition Map { get; }
    public LoadedWorld World { get; }                  // key -> entity id for nodes and hearths already spawned
    public IGroupRegistry Groups { get; }
    public SimulationSettings Settings { get; }
    public IReadOnlyList<AutonomousAgent> Agents { get; }

    public AutonomousAgent SpawnAgent(Vector2 position, string label, bool seededPersonality);
    public GUID OpenStall(string poiKey, in StallTerms terms);   // position and radius come from the POI
    public void SeedBounty(in SeedBountySpec spec);              // registered after the ledger baseline (below)
    public uint Ticks(uint relative);                            // session-relative tick (start is 0 after the rewind)
}
```

- **`SwarmSpawner` becomes generic.** It keeps its name; renaming is churn with no gain. `Respawn(ISessionDefinition)` runs these steps:
  1. Clean up the scene roots.
  2. Reset the registry, id allocator, simulation manager state, **clock (rewind to 0)** and **chronicle**.
  3. `IWorldMap.Load(session.Map)`, then `MapView.Show`.
  4. Spawn every resource node and every `Hearth` POI from the definition, recording their keys. This is unchanged from Phase 1, in definition order.
  5. `session.Populate(builder)`.
  6. Ledger baseline: `InitialSeed` = stall treasuries + agent gold.
  7. Register the staged seed bounties (escrow), in staging order.
  8. Raise `SessionChanged`.

  Steps 6–7 keep today's order (baseline before escrow), so conservation holds from tick 0 exactly as now.
- **Session selection.** Each session is registered `.As<ISessionDefinition>()`, and the spawner takes `IReadOnlyList<ISessionDefinition>` (VContainer collection injection). The **default is resolved by id (`OutpostSession.SessionId`)**, never by registration order, so the boot path can't pick a test session. `Respawn()` with no argument respawns the current session, which is the default at boot.
- **`ISessionSwitcher`** (implemented by `SwarmSpawner`): `Sessions`, `Current`, `SwitchTo(ISessionDefinition)`, `event Action<ISessionDefinition> SessionChanged`. The UI depends on this, not on the spawner.
- **`SimulationClock.Reset()`** (and on `ISimulationClock`) sets `CurrentTick` to 0 and the accumulator to 0. The time multiplier is kept: a paused game stays paused, and a scenario's speed is untouched.

### 2.2 Sessions and layouts

| Session | Assembly / folder | Map | Content |
| :--- | :--- | :--- | :--- |
| `OutpostSession` (default, live) | `Simpiens`, `Core/Session/` | `OutpostCommonsLayout` (`Core/Session/Layouts/`) | 6 crawlers with seeded personalities on the yard's north edge (as now), labelled `Crawler0..5`; the stall at `outpost.stall` with today's terms; seeded Gather (Ore Ridge, iron ×1, 25g) and Scout (Herb Hollow, 15g) bounties expiring at session tick 6000 |
| `CanaryPenSession` | `Simpiens.Testing`, `Testing/Sessions/` | `DevMapLayout` (pens only, moved to `Testing/Sessions/`) | The 24 scripted pen agents, verbatim: positions, hunger, relationships, spatial memories, groups 6/7/9/10 with directives expiring at session tick 5000, labels `PenN.AgentI`. No stall, no bounties, no crawlers. |

- **`OutpostCommonsLayout`** is the Commons slice of today's `DevMapLayout`, unchanged except for its bounds:
  - same coordinates, node keys, sector ids 11/12/13 with the same names, rects, anchors and explored flags, and the same labels;
  - `PlayableBounds` tightened to the yard plus a margin: x −14..14, y 3..17. Without the pens, the old ±20 bounds would leave the crawlers an empty field to wander into;
  - `CameraFrame` fitted to the yard.
  - It is temporary by design: Phase 3 replaces it with `Floor1Layout`.
- **`DevMapLayout`** becomes pens only, with `PlayableBounds` and `CameraFrame` fitted to the two pen rows. The pens are closed, so the bounds don't affect pen agents. It stays transcribed cell for cell, but the parity proof has served its purpose and **`DevMapParityValidator` is deleted**.
- Production code then contains **no pen coordinates, pen keys, pen sector ids, `ScriptedPenAgentCount` or `AgentLabel` pen logic**. That is the epic's exit criterion, checked by a grep in the step gate.

### 2.3 The Dev Map toggle (UI)

- A small overlay, `session-switch-overlay`, sits in `FacilityDashboard.uxml` **outside `dashboard-root`**. It shows in both the 2D view and the dashboard (G6) and is placed top-right, clear of the header buttons.
  - The button reads **🧪 DEV MAP** on the main map and **🏠 MAIN MAP** on the Dev Map. Its label comes from the *other* session's `DisplayName`.
  - On the Dev Map, a banner reads "DEV MAP: test canaries (not the game)", so a screenshot can never be mistaken for the live game.
- **It is shown only when more than one session is registered.** Shipping player builds have only `OutpostSession`, because `Simpiens.Testing` is `UNITY_INCLUDE_TESTS`-only, so the button is absent there with no `#if`.
- Click → `ISessionSwitcher.SwitchTo(other)`. UI events and ticks both run on the main thread and never nest, so the switch happens between ticks.
- On `SessionChanged`, the presenter:
  - rebuilds the sector list;
  - selects the first non-fallback sector of the new map, replacing the hard-coded 4 (G5);
  - clears the agent selection;
  - closes the Merchant and Bounty overlays;
  - shows "no stall" states when the session has no stall (the Dev Map).
- **Deviation from epic §2.4 wording (minor):** §2.4 says the harness *installs the button*. Here the production UI owns a generic session switch and the harness only installs the second session. The effect is identical: the button exists only when the harness is present. It avoids test code reaching into production UXML by element name.

### 2.4 Test harness

- `ISimulationScenario` gains `string SessionId { get; }`. `ExecuteScenario` calls `spawner.Respawn(sessionById)` instead of `Respawn()`, so no test depends on what the previous test or the button left loaded.
- Scenario placement:
  - **Outpost:** `OutpostSupplyLoopScenario`, `PriceResponseScenario`, `EconomySoakScenario`. Crawlers are now `agents[0..5]`; the economy scenarios' "crawler" filter becomes "all agents".
  - **Dev Map:** `SwarmNavigationScenario`, `ResourceContentionScenario`, `DynamicChaosScenario` (injection points read from `DevMapLayout` keys `pen3.contested` / `pen2.contested`, offset as now), and the new `PenCanaryScenario`.
  - **Both:** the new `SessionSwitchScenario`.
- Phase 3 moves the anomaly scenarios onto `Floor1`, per epic §3. Until then they keep their 24-agent crowding, which is what they exist to stress.

### 2.5 `PenCanaryScenario` (Dev Map, no injection beyond the pens' own scripted setup)

The pens' scripted setup *is* the precondition by design: a canary checks that the mechanism still works, not that it emerges. This is stated in the scenario's doc comment so it is never cited as Definition-of-Done evidence for the live loop.

The scenario runs the Dev Map at 10× and polls the chronicle **every tick**; the chronicle is a bounded buffer, so a check only at the end could miss events. All ten checks are evaluated and reported together rather than stopping at the first failure:

| Pen | Asserted outcome (within the bound) | Observed through |
| :-: | :--- | :--- |
| 1 Fear Yield | Agent 1 yields the contested bush to agent 0; agent 0 harvests it | `ResourceYielded` (A = 1, B = 0); harvest of `pen1.contested` by 0 |
| 2 Respect Defer | Agent 3 yields the contested bush to agent 2 | `ResourceYielded` (A = 3, B = 2) |
| 3 Rivalry | The bush is contested, not yielded: one rival claims it and neither yields | `ResourceClaimed` on `pen3.contested`; no `ResourceYielded` between 4 and 5 |
| 4 Altruism & Hearth | Agent 6 feeds agent 7; afterwards both sit at the Pen 4 campfire | `AltruisticFoodShare` (6 → 7); `CampfireCompanionship` (6, 7) |
| 5 Selfish Peer | Agent 8 never feeds agent 9 | no `AltruisticFoodShare` (8 → 9) over the whole run |
| 6 Directive Rally | The compliant follower (11) reaches the rally point | agent 11 within the directive radius of (−8, −7.5) |
| 7 Passive Defiance | The starving follower (13) leaves the rally point to forage while the directive is still active | harvest of `pen7.bush` (at (−4, −11), outside the 2-unit rally radius around (−4, −7.2)) by agent 13 before the directive expires. Agent 13 *starts* inside the radius, so "never reached the rally point" would be the wrong test. |
| 8 Hearth Emergence | A group forms at the Pen 8 campfire with agent 14 as leader | `GroupFormed` + registry leader = agent 14 |
| 9 Dominance Trial | The challenger (18) displaces the collapsed leader (17) | group 9 leader = agent 18 |
| 10 Schism | Group 10 splits into loyalists (20, 21) and dissidents (22, 23) | `GroupSchism`; final memberships |

- **Bound:** set from the step 3 observation run as the slowest observed outcome × 1.5, capped at 3,000 ticks (5 sim-minutes). Recorded in the scenario with the observed ticks.
- **If a pen does not produce its outcome today**, that is a real regression the demos were hiding. Its check is not loosened to pass. Instead it is (a) fixed when the cause is small and local, or (b) reported to the producer with the evidence, and `[Ignore]`d with a pointer to the finding.

---

## 3. Steps and commits

Each step must compile and keep PlayMode green (`scripts/run-playmode-tests.sh`, Editor closed). Each commit body states the real test status.

| Step | Work | Gate | Commit |
| :-: | :--- | :--- | :--- |
| 1 | **Respawn resets time and history (G3, G4).** `SimulationClock.Reset()`; `Respawn` rewinds the clock and clears the chronicle; seeded bounty and directive expiries stay at 6000/5000, now session-relative because the clock starts at 0. | PlayMode full suite; record the new supply-loop milestones. They shift because every scenario now starts at tick 0, which also makes them independent of test order. | `fix(session): respawn rewinds the clock and clears the chronicle` |
| 2 | **Session contract, behaviour-preserving.** `ISessionDefinition`, `SessionBuilder`, `ISessionSwitcher`, `SwarmSpawner.Respawn(session)`. Today's content moves verbatim into one temporary `LegacySession` (production) with the same spawn order, so the ids are identical. `SetupFacilityUIAsset` is updated (G11). | PlayMode full suite, with supply-loop **milestone ticks identical to step 1**: this proves the extraction changed nothing | `refactor(session): session definitions; spawner made generic` |
| 3 | **Pin the pens before moving them.** `PenCanaryScenario` on `LegacySession`: an observation run first (log the tick of each outcome), then the assertions and bound (§2.5). | `PenCanaryScenario` passes ×3; any pen that fails becomes a finding (§2.5) | `test(session): PenCanaryScenario pins the ten pen outcomes` |
| 4 | **The split.** `OutpostCommonsLayout`; `DevMapLayout` pens-only, moved to `Testing/Sessions/`; `OutpostSession` (default) and `CanaryPenSession`, installed by `TestHarnessInstaller`; `LegacySession` and `DevMapParityValidator` deleted; `ISimulationScenario.SessionId` and scenario placement (§2.4); crawler indexing from 0; the presenter's default sector comes from the map (G5). Validators that used `DevMapLayout` for the Commons (`WorldMapValidator`, `TestWorlds`, `SupplyLoopValidator`) switch to `OutpostCommonsLayout`. | PlayMode full suite **×3**; `PenCanaryScenario` still passes after the move; `OutpostSupplyLoopScenario` passes with 0 anomalies (new milestones recorded: ids shift because the pen nodes and agents no longer precede the crawlers); `MapDefinitionValidator` covers `OutpostCommonsLayout` (every node and POI reachable from the stall's region); grep: no pen references in `Assets/Scripts` outside `Testing/` | `feat(session): live game boots the outpost; pens become the Dev Map canary` |
| 5 | **The Dev Map toggle.** The session-switch overlay, banner, presenter reset on `SessionChanged` (§2.3); `SessionSwitchScenario`. | `SessionSwitchScenario` passes (below); full suite green; **producer visual check in the Editor:** Play shows the yard and no pens, DEV MAP shows the pens, MAIN MAP returns to the yard | `feat(ui): Dev Map toggle between the outpost and the pen canaries` |
| 6 | Docs: epic checklist (Phase 2 delivered items), ROADMAP Epic MAP line (the live session is the Commons until Phase 3, not the pens), `curentfocus.txt`, `architecture_history.md` (session split, clock rewind on respawn, generic session switch), and AGENTS.md (`Core/Session` and `Testing/Sessions` layout, `SessionId` on scenarios) | Docs agree with each other | `docs: Epic MAP Phase 2 complete` |

---

## 4. Tests added or changed

- **New `PenCanaryScenario`** (Dev Map; §2.5).
- **New `SessionSwitchScenario`:**
  1. Boot the outpost and run 200 ticks.
  2. Switch to the Dev Map and assert:
     - the map is `DevMap` and the clock is 0;
     - there are 24 agents and no stall or bounties;
     - the chronicle is empty;
     - no GameObject from the outpost session remains under the scene roots;
     - the ledger baseline equals the Dev Map's seeded gold.
  3. Run 200 ticks.
  4. Switch back and assert the outpost is **structurally identical to a fresh boot** at tick 0:
     - the same entity id set and positions;
     - the same stall terms;
     - both bounties `Open` with expiry 6000;
     - sectors 12/13 shrouded again;
     - ledger conserved.
  5. Run 200 ticks with 0 anomalies, and confirm no intent from the old session was applied (G9).

  It asserts structure, not behaviour: same-seed behavioural equality is limited by the known cognition-arrival deviation (ROADMAP §1A.2 #6). That check belongs to Phase 5.
- **Changed:**
  - `MapDefinitionValidator`: the `OutpostCommonsLayout` checks; the Dev Map checks now use the pens-only layout;
  - `WorldMapValidator`, `TestWorlds`, `SupplyLoopValidator`: they use `OutpostCommonsLayout` for Commons sector ids;
  - the three anomaly scenarios: `SessionId = devmap`, and `DynamicChaosScenario` reads its injection points from the layout;
  - the economy scenarios: crawler indexing.
- **Removed:** `DevMapParityValidator` and its wrapper.
- **Expected count:** 42 − 1 + 2 = **43** PlayMode tests (plus the explicit soak).

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| **Economy milestones move twice** (step 1 rewinds the clock; step 4 shifts ids and tightens the yard bounds) | Expected; this is the same policy as the Phase 1 id shift. Re-verify outcomes ×3 rather than comparing old tick numbers. A real regression (for example the Gather bounty no longer completes) is treated as a finding and fixed at its cause. |
| Tighter yard bounds starve or trap crawlers | The validator proves every node and the stall are reachable; `OutpostSupplyLoopScenario` asserts the loop; the bounds are a single data edit if they need widening |
| A pen outcome doesn't happen today | Step 3 runs *before* the move, so a failure is attributed to existing behaviour, not to Phase 2. Handled per §2.5 (fix or report; never loosen). |
| Pen outcomes change after the split (the 6 Commons crawlers and their nodes no longer exist in the Dev Map, and ids shift) | The canary passing after step 4 is the gate. The pens are walled, so the only coupling was id order and the shared cognition load. |
| Stale async cognition hits a reused GUID after a switch | Agents cancel their CTS on destroy and `ResetState` drains the queue (G9); `SessionSwitchScenario` checks it. If it fails, add a session generation stamp to intents (a small, contained fix). |
| Rewinding the clock breaks a consumer that assumes monotonic ticks across the process (the logger tick stamp, presenter timers, the anomaly detector's per-pawn histories) | The detector already resets per scenario; the tick-stamped log simply restarts at 0 after a respawn and logs the respawn line. Audit `CurrentTick` caches during step 1. |
| The toggle is pressed while the game is paused | Respawn works the same when paused; the multiplier is kept, so the new session starts paused. Covered by a manual check in step 5. |

---

## 6. Out of scope (later phases)
- `Floor1Layout` going live, bunks and the rest anchor, 12 crawlers, migrating the anomaly and economy scenarios to Floor 1, and re-running the soak: Phase 3.
- Tilemap rendering, shroud overlay, pan and zoom, the sector-aware bounty picker: Phase 4.
- The Dev Map in development player builds (a `DEVELOPMENT_BUILD` install path for the harness): a small follow-up, only on request (epic §2.4).
- Renaming `SwarmSpawner`.

## 7. Minor decisions taken in this plan (producer may override)
1. **The interim live map is the Outpost Commons yard, not Floor 1.** Floor 1 needs scenario migration and the bunk/hearth lifecycle, which are Phase 3 scope. Moving it into Phase 2 would merge two phases and lose the "pens out, loop unchanged" checkpoint.
2. **The Dev Map is the pens only.** The Commons and its crawlers are not duplicated there, so the toggle switches between "the game" and "the canaries".
3. **Respawn rewinds the clock to tick 0.** "Switching resets the simulation" (§2.4) then holds literally, and test-order dependence disappears.
4. **The switch is a generic production UI element, shown only when more than one session is registered** (§2.3 deviation note).
5. The Commons keeps sector ids 11–13 and its names: no churn for a map that Phase 3 retires.
6. `SwarmSpawner` keeps its name.
