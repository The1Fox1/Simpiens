# Plan — Milestone 1 Closure (ROADMAP §3A recommended sequence)

> **Status: approved 2026-10-09** (producer). Phase order approved. Economy design decisions are **deferred** until the game is functional (see Phase 3). Pen demo agents stay neutral. Work proceeds on `feat/milestone1-supply-loop-zlogger`.
>
> **Source:** [ROADMAP §3A](../docs/ROADMAP.md#3a-architecture-review-remediation-tracker) proposed sequence, consultant review [`architecture_review.md`](../docs/reviews/architecture_review.md) §2.5, §2.7, §2.8, §3.2, §3.3.
>
> **Branch:** `feat/milestone1-supply-loop-zlogger` (producer decision). Commit after every phase (AGENTS.md *Version Control*).

## Goal

Epic 9 → `COMPLETED`, which (per producer decision 2026-10-09) needs:
1. PlayMode fully green: `OutpostSupplyLoopScenario` meets §1B, and the 3 anomaly scenarios no longer regress.
2. A written, implemented economy model (review §3.3).
3. Agent personality + utility considerations (review §3.2 / §2.7).

Parallel, low-cost work rides alongside: determinism fixes (§1A.2 #5, #6, #14) and test-scope separation (§1A.2 #9, review §2.8).

Out of scope here (later Milestone 1 steps in §3A): resuming Epic MAP, MemoryPack, scaling/soak (§2.5).

## Phase overview

| Phase | Work | Size | Depends on | Unblocks |
| :-: | :--- | :-: | :--- | :--- |
| 1 | Green PlayMode: navigation body clearance, wall-checked watchdog, nav-grid reset, stacking, thrashing | M | — | §1B gate for the supply loop |
| 2 | Determinism: seeded RNG, ordered intent apply, ordered iteration | S | — (do before 3 so tuning runs are reproducible) | Reproducible PlayMode, multiplayer invariants |
| 3 | Economy: **observation-only** soak now; design decisions + model **deferred** until the game is functional | S now / S–M later | 2 (recommended) | Epic 9 promotion (when the deferred part lands) |
| 4 | Personality + considerations | M | 1, 2 (tuned against the live-loop scenarios, not an economy band) | Epic 9 promotion |
| 5 | Test-scope separation | S–M | — (any time; avoid running concurrently with 1 in `SwarmSpawner`) | Clean boot, shippable build |
| 6 | Docs + promotion | S | 1–4 | Epic 9 `COMPLETED` |

---

## Phase 1 — Green PlayMode

> **Status: DONE 2026-10-10.** PlayMode 14/14 across 3 consecutive runs. Commits `c10a57f`, `e74c2d2`, `c12871f`, `cdf6514`, `498bb6c`.
>
> **Differences from the plan below:**
> - **Body clearance:** done by inflating wall rasterization by the pawn radius plus snapped A* endpoints and an exact segment traversal, instead of a per-step circle test (equivalent guarantee, cheaper).
> - **Arrival slots (1E):** replaced by live-position pawn separation and arrival tolerance. Every displacement is now crowd-aware, panic included. The remaining stacking came from panic runs and unchecked watchdog nudges, not from a shared destination.
> - **Thrashing (1F):** the trace showed `Thinking > Idle > Walking`, caused by reflex pre-emption resetting the visual to Idle. Fixed at the source, plus a 2-tick display dwell in `AgentView`. The detector now measures the rendered state.
> - **Found along the way:**
>   - Pawns walked to the edge of the oversized grid, so a ±20 play-area border was added.
>   - A planner bug: salvage/scout actions didn't clear `AtTradeStall`.
>   - A validator broken since §3.1 (`BountyFulfillmentValidator` Test 3).
>   - New known deviation §1A.2 #15 (off-thread plan bookkeeping).
> - **Provisional economy stopgap:** fixed navigation exposed the treasury drain inside the scenario horizon, so `ProvisionalCaravanExport` was added (Phase 3's "only if needed" clause).

### Evidence (latest run, `Logs/playmode-results.xml`, 10/14 pass)

| Scenario | Failure | Representative anomaly |
| :--- | :--- | :--- |
| `OutpostSupplyLoopScenario` | 79 pawn stackings (plus penetrations) | stacking at (2.12, 0.15), 0.21 m for 1.5 s; penetrations at `Wall_3.0_-5.5`, `Wall_10.0_-6.5`, `Wall_6.0_0.5` |
| `DynamicChaosScenario` | 6 penetrations | same two agents repeatedly at `Wall_-8.0_-12.5` and `Wall_6.0_±0.5` (pen walls, **not** the injected wall) |
| `SwarmNavigationScenario` | 2 penetrations | `Wall_6.0_-0.5` / `Wall_6.0_0.5` |
| `ResourceContentionScenario` | 2 thrashing incidents | 4 visual-state switches in 0.12 s, ending `Idle` |

Tick numbers keep rising across scenarios (t6000+), so the 4 scenarios share one session; state leaks between them (see 1C).

### Root causes (verified in code)

1. **Movement checks a point, not a body.** `SimulationManager.ExecutePathMovement` ([SimulationManager.cs:1007](../Assets/Scripts/Core/SimulationManager.cs)) checks only that the *waypoint's* cell is walkable (`IsPositionBlocked`), then `MoveTowards` with no check on the pawn's actual position or radius. Walls are 1 m cells (`SimulationStarter` initialises the grid with `cellSize = 1.0`). A pawn whose centre sits in a free cell next to a wall overlaps it. The detector flags overlap at `Radius * 0.7` ([VisualAnomalyDetector.cs:360](../Assets/Scripts/Testing/Diagnostics/VisualAnomalyDetector.cs)).
2. **Path smoothing grazes corners.** `GridAStarSearcher`'s raycast string-pulling tests a zero-width line, so smoothed segments cut past wall corners (the `Wall_6.0_±0.5` pair is a doorway post).
3. **Watchdog reflexes teleport without checks.** Tier 1 nudges (±0.2 / −0.4 m perpendicular) and the Tier 3 0.5 m back-off call `SetLogicalPosition` directly ([SimulationManager.cs:1075-1125](../Assets/Scripts/Core/SimulationManager.cs)). The SimLog messages already say "unchecked against walls".
4. **Static walls are never cleared.** `SpatialHashGrid.UpdateFromRegistry` calls `StaticNavGrid.SetWall` for every wall each tick, but nothing calls `StaticNavGrid.Clear()`. `SwarmSpawner.Respawn` → `SimulationManager.ResetState` leaves old walls (including `EdgeCaseInjector` chaos walls) in the grid for the next scenario.
5. **Stacking:** pawn clearance applies only to the waypoint, so two pawns heading for the same goal point (hearth slot, stall, bush) both arrive. The detector flags < 0.35 m for ≥ 1 s unless the pair is gossiping/sharing.
6. **Thrashing:** an agent cycles intent → immediate fail/success → Idle → re-think within 0.12 s. Likely a zero-length path or an intent that completes on its first tick. **Confirm from a trace before fixing**; don't guess.

### Changes

**1A. Body-aware movement (`SimulationManager`, `SharedWorldSnapshot`)**
- Add `SharedWorldSnapshot.IsCircleWalkable(Vector2 centre, float radius)`: scan the cells the circle overlaps against `StaticGrid` (allocation-free, ≤ 9 cells at r < 1).
- In `ExecutePathMovement`, compute the candidate step position. If `!IsCircleWalkable(candidate, node.Radius * 0.7f)`, don't move (the watchdog takes over), exactly as a blocked waypoint is handled now.
- Use one shared constant `NavConstants.BodyClearanceFactor = 0.7f` (with `NodeController.Radius`) so the detector and the motor can't drift apart.

**1B. Clearance-aware smoothing (`GridAStarSearcher`)**
- Make the line-of-sight test sweep the agent radius: test the two parallel offset lines at ±r, or treat any cell within r of the segment as blocking. Keep it zero-alloc.
- Add a small static-clearance cost to A* for cells adjacent to walls, so unsmoothed paths keep off walls where there's room. Doorways (2.5 m) must stay passable; cover this in `GridAStarValidator`.

**1C. Nav-grid lifecycle (`ISpatialPartition`, `SimulationManager.ResetState`)**
- Add `ISpatialPartition.ResetStatic()` → `StaticNavGrid.Clear()` (bumps the walkability version so reachability regions rebuild).
- Call it from `SimulationManager.ResetState()`. Walls re-register on the next `UpdateFromRegistry`.
- Unregistered walls need clearing too: if an injected wall is removed mid-run, either rebuild the static grid from registered walls whenever the wall set changes (simplest: a wall-set version counter in `WorldRegistry`), or have `EdgeCaseInjector` call a `RemoveWall` API. **Prefer the rebuild.** It is O(walls), happens rarely, and has no bookkeeping that can drift.

**1D. Wall-checked watchdog (`ApplyProgressiveUnstuckReflex`)**
- Before each nudge or back-off, test `IsCircleWalkable(candidate, r)`. If blocked, try the mirrored direction, then skip the displacement and fall through to the next tier.
- Never displace into a cell in a different static region (`StaticNavGrid.AreConnected`). That stops teleporting through thin walls.
- Update the SimLog messages (drop "unchecked against walls"). Extend `MotorWatchdogValidator` with a "nudge toward a wall is rejected" case.

**1E. Pawn stacking**
- **Arrival slots:** when several agents share a destination (stall interaction point, hearth, bush), the handler that builds the path asks the execution context for a free spot. Add `IIntentExecutionContext.ResolveArrivalPoint(target, agentId, radius)`: a deterministic ring of offsets checked against current pawn positions and `IsCircleWalkable`.
- **Per-step pawn check:** in `ExecutePathMovement`, also reject a step that would bring the pawn within `2 × r × 0.5` (≈ 0.35 m) of another non-interacting pawn. The watchdog resolves the face-off.
- Leave social interactions (gossip/share) exempt, matching the detector.

**1F. Thrashing**
- Run `ResourceContentionScenario` with `SIMPIENS_LOG="Cognition=Debug;Navigation=Debug" SIMPIENS_LOG_FOCUS=<agent>`. Identify the intent that completes within one tick.
- Likely fix: treat zero-waypoint paths as `PathBlocked` rather than `Success`, and add a minimum dwell (in ticks) before the visual state is allowed to change. The dwell belongs in `AgentView`, not the simulation.

### Verification
- `MotorWatchdogValidator`, `GridAStarValidator`, `GoapLiveLoopFixesValidator`: all pass.
- New `NavigationClearanceValidator`: a pawn is never placed overlapping a wall, across move, nudge and back-off; static grid clears on reset; arrival slots don't overlap.
- PlayMode: **14/14**. Record per-scenario anomaly counts in the commit body.
- **Do not loosen the detector thresholds** to get green. If a threshold is genuinely wrong, that's a separate, justified change.

Commits: `fix(navigation): body-aware movement and clearance smoothing` · `fix(navigation): wall-checked watchdog and nav-grid reset` · `fix(simulation): arrival slots to prevent pawn stacking` · `fix(cognition): ...thrashing` (after diagnosis).

---

## Phase 2 — Determinism (§1A.2 #5, #6, #14)

> **Status: DONE 2026-10-10** (`1dc831b`). PlayMode 16/16.
>
> **Differences from the plan below:**
> - **Added `EntityIdAllocator`.** Entity ids came from `GUID.Generate()`, and they feed tie-breaks, exploration hashes and `SimRandom` keys, so a seeded RNG alone could not reproduce a run.
> - **Harvest contention** now picks the lowest-id competitor.
> - **`DeterminismValidator` Test 3** was mutation-checked: restoring insertion-order application makes it fail.
> - **Validator wrappers:** PlayMode wrappers added for `DeterminismValidator` and `NavigationClearanceValidator`. Boot-time `IStartable` failures are only logged.

### Changes

**2A. Seeded, stateless RNG.**
- New `Simulation/SimRandom.cs`: a `readonly struct` built from `(sessionSeed, agentId, tick, salt)` via SplitMix64. It has no shared state, so it's safe on worker threads without `[ThreadStatic]`. Methods: `NextFloat()`, `Range(min, max)`.
- Session seed lives in a new `SimulationSettings` (DI singleton). The seed comes from a command-line arg/env var `SIMPIENS_SEED`, with a fixed default. It's logged at Info on boot.
- **Replace:** `AmbientWanderEvaluator`, `MentalBreakEvaluator` (`[ThreadStatic] System.Random`). `AgentContext` already carries `AgentId` and `CurrentTick`; add the seed to `AgentContext`, or have evaluators take `SimulationSettings` in their constructor.
- **Leave as view-only** (add a one-line comment saying so): `NodeController` `Random.Range` (lines 143, 200) applies only to Dynamic-body bouncing nodes; `NodeFactory` uses `Random.ColorHSV` only for sprite colours.

**2B. Ordered intent application (#6).**
- `SimulationManager.ProcessQueuedIntents`: drain the `ConcurrentQueue` into a reusable `List<AgentIntent>`, sort by `AgentId` (`GUID.CompareTo`, as `ResourceTargeting` does), then apply. Zero-alloc with a preallocated list and a static comparer.
- This fixes order *within* a tick. The tick an intent *arrives* still depends on thread timing. Making that deterministic (apply cognition results at a fixed tick offset, or a barrier) belongs to the multiplayer epic. Record it as remaining in §1A.2 #6 rather than claiming it's resolved.

**2C. Ordered iteration (#14).**
- Keep an insertion-independent sorted key list next to `_activeIntents`, `_stalls`, `_bounties` and `_anchors` (a `SortedList`, or a `List` re-sorted on add/remove, which is rare), and iterate that instead of the `Dictionary`.
- `_bounties` keys are `ushort`; iterate in ascending id.

### Verification
- New `DeterminismValidator`: two headless runs of N ticks with the same seed and identical injected intents give identical agent positions/needs/gold hashes. A different seed gives a different hash.
- Phase 1 PlayMode stays green.

Commit: `fix(simulation): seeded SimRandom and deterministic intent ordering`. Update §1A.2 #5/#14 to RESOLVED and #6 to partially resolved.

---

## Phase 3 — Economy model (review §3.3)

### Current flows (verified)

| Flow | Direction | Numbers (SwarmSpawner / SupplyTuning) |
| :--- | :--- | :--- |
| `InitialSeed` faucet | → stall treasury 250g + agents 10g each | the **only** faucet |
| Agent sells ore / herb | stall → agent | 20g / 25g per unit |
| Agent buys ration | agent → stall | 15g |
| Bounty escrow / payout | stall → escrow → agent | per bounty |
| Supply caravan restock | stall → **sink** | 2g/unit up to cap 50, every 600 ticks |
| Ore / herb stock at stall | — | **accumulates; nothing consumes it** |
| Free food bushes (regrowth) | — | hunger met without spending gold |

**Diagnosis:** the stall pays out for salvage it can never resell, agents can feed themselves for free, and the caravan only removes gold. So the treasury drains to zero, and the loop then stalls (affordability gating stops all salvage sales). This is structural, not a tuning problem.

### Deferred (producer decision 2026-10-09)

The producer wants the game functional before making economy decisions. For now, Phase 3 delivers only:
- **Observation soak:** `EconomySoakScenario` records treasury, agent gold, stock and faucet/sink totals to `Logs/economy-soak.csv`. It asserts only ledger conservation, with **no** pace band. Its data informs the deferred decisions.
- **Provisional stopgap, only if needed:** if the treasury drain stalls the loop inside a PlayMode scenario's horizon, add the smallest reversible fix (e.g. a caravan export at placeholder prices), named `Provisional*` in `SupplyTuning`. It is not a design commitment.

Everything below this heading waits for the producer.

### Producer decisions needed (deferred)

1. **Salvage outlet (faucet):** the caravan *buys* salvage from the stall at an export price (e.g. ore 28g, herb 34g). Proposed default: **yes**. It gives the player a margin to manage, the core trading-post fantasy.
2. **Agent sinks:** which of these? (a) free bushes yield less/regrow slower so rations matter; (b) daily lodging/upkeep fee; (c) gear wear (defer to Epic 10). Proposed default: **(a) only** for Milestone 1.
3. **Scarcity lever:** material scarcity (node regrowth) vs. agent willingness to spend. Proposed: **both**, with personality (Phase 4) driving willingness.
4. **Target pace:** e.g. "a competent player's treasury roughly doubles in 30 sim-minutes at 1x; a careless one hits zero". These targets become soak assertions.

### Changes
- **`docs/economy-model.md`** (new, one page): faucets, sinks, scarcity lever, tuning table, target pace. ROADMAP §1A gains a one-line invariant: *every gold flow is a named ledger faucet/sink or a conserved transfer*.
- **`SupplyService`:** caravan export. Sell stall salvage above a reserve at `SupplyTuning.ExportPrice*`, recorded as a ledger faucet `CaravanExport`. Move tuning into `SupplyTuning` (no magic numbers in `SwarmSpawner`).
- **Food pressure:** apply decision 2(a) via `ResourceData` regrowth/yield tuning for Commons bushes. Leave the pens alone unless the producer says otherwise.
- **Economy soak (Epic Test-2 item):** a headless or PlayMode `EconomySoakScenario` that runs for 30 sim-minutes at 10x with no player input. It samples treasury, agent gold, stock, faucet and sink totals every 600 ticks to `Logs/economy-soak.csv`, and asserts ledger conservation plus the decision-4 band. This also delivers part of Epic Test-2's "market soak runner".

### Verification
- `SupplyLoopValidator` extended: export faucet, conservation over a restock cycle.
- `EconomySoakScenario` passes its band. `OutpostSupplyLoopScenario` still passes.

Commits: `docs(economy): one-page economy model` · `feat(economy): caravan salvage export faucet and food pressure` · `test(economy): economy soak scenario`.

---

## Phase 4 — Personality + considerations (review §3.2, §2.7)

### Design
- **`AgentPersonality`** (`Cognition/Contracts/`): blittable 5-byte struct (`Greed`, `RiskTolerance`, `Sociability`, `Loyalty`, `Diligence`, each `byte` 0–255, accessed as 0..1 floats). Stored on `AgentState`, copied into `AgentContext` (it's immutable, so no single-writer issue).
- **Generation:** deterministic from `SimRandom(seed, agentId, 0, salt: Personality)` (Phase 2). Pen demo agents get **fixed neutral personalities** so the Epics 7/Beta/8 demos stay as tuned.
- **Considerations layer** (`Cognition/Planning/Considerations/`): static, allocation-free scoring helpers that return 0..1.
  - `Reward(gold, personality.Greed)`, `Distance(path or straight-line, Diligence)`, `Risk(...)` (placeholder = 1 until Epic 10 hazards), `Affinity(memory, targetId)`, `Price(stall price vs. agent reference price, Greed)`.
  - A `Score = Π considerations` combiner (Dave Mark-style compensated product) with a fixed-capacity span.

### Changes
- **Target selection:** `BountyQueries.TryGetOpenBounty` currently picks the *reachable, lowest id* bounty. Replace that with the highest consideration score (reward × distance × personality), keeping the GUID/id tie-break. `ResourceTargeting` nearest-node/stall choices get the same treatment where it matters (stall choice once there's more than one stall).
- **Goal priorities:** `FulfillBountyGoal`, `EarnIncomeGoal`, `PurchaseProvisionsGoal`, `LiquidateSalvageGoal`, `SocializeGoal` and `GatherAtAnchorGoal` multiply in their personality consideration. This removes the binary price threshold: agents have personal reservation prices (`Greed` shifts the acceptable ration price and sale price).
- **"Why?" groundwork (review §3.4, not the UI):** record the top 3 goal scores and the winning considerations into `AgentState.LastDecision` (fixed-size struct) on the main thread when the intent is applied. The inspector UI itself stays out of scope; this just makes it cheap later.
- **Fact budget:** add no new `StateFact`s. Personality acts through priorities and selection, not facts (36/64 used).

### Verification
- New `PersonalityValidator`: same seed gives the same personalities; a greedy agent picks the higher-reward farther bounty and a low-diligence agent picks the nearer one; reservation price varies across agents.
- **Live (§1B):** new PlayMode `PriceResponseScenario`. Raise the ration price in steps (via `UpdateStallPricesCommand`, the player's real lever, not injected outcomes) and assert purchase volume falls **gradually** across agents rather than all at once. This is the review's §3.2 "price slider is an on/off switch" check.
- Economy soak: ledger conservation holds (no pace band until the economy decisions are made).

Commits: `feat(cognition): agent personality` · `feat(planning): utility considerations for target selection` · `test(playmode): price response scenario`.

---

## Phase 5 — Test-scope separation (review §2.8, §1A.2 #9, #10 remainder)

Order matters, because `AppBootstrapper` (production assembly) currently references validators, and the production domain lives in `Testing/SwarmSpawner`.

1. **Extract the production domain.** Move the GOAP action graph and goal list out of `SwarmSpawner` into `Cognition/Planning/Domain/OutpostGoapDomain.cs` (a static builder, or a DI-registered `IGoapDomain`). Move session population and world seeding (stall, bounties, Commons nodes) into `Core/Session/SessionBootstrap.cs`. `SwarmSpawner` keeps only the demo-pen scripting and calls into these.
2. **Delete the `CognitiveEngine` stub**, its registration and `ContextValidator`. Their only consumers are each other.
3. **Validator scope.** Move the ~30 validator registrations from `AppBootstrapper` into a `ValidationLifetimeScope` (child scope) in the testing assembly. It's instantiated by the PlayMode harness and by an Editor menu item / `-runValidators` batch arg. It never runs on a normal boot.
4. **asmdef split.** `Assets/Scripts/Testing/Simpiens.Testing.asmdef` references `Simpiens` and is Editor + test builds only (define constraint `UNITY_INCLUDE_TESTS` or Editor-only platform). `Simpiens.Tests.PlayMode` references `Simpiens.Testing`. Add `[assembly: InternalsVisibleTo("Simpiens.Testing")]` for the internals validators use (e.g. `ApplyProgressiveUnstuckReflex`).

### Verification
- Both assemblies compile. A normal Play boot logs **no** validator output. PlayMode 14/14. A player build contains no `Simpiens.Testing` types.
- Update AGENTS.md *Directories* (domain location, `Testing/` no longer hosts production) and remove the "⚠️ SwarmSpawner hosts production" note. Mark §1A.2 #9 and #10 resolved.

Commits: `refactor(planning): extract production GOAP domain from SwarmSpawner` · `refactor(core): session bootstrap and validation scope` · `chore(build): Simpiens.Testing assembly`.

---

## Phase 6 — Docs & promotion

Following the ROADMAP §0 order:
1. **ROADMAP.** Update the §3A rows (#5, #9, #10a; determinism). Update *Milestone 1 Status*. Set Epic 9 → `COMPLETED` only if §1B holds: validators pass, `OutpostSupplyLoopScenario` + `PriceResponseScenario` pass with no injection, and the checklist is reconciled.
2. **`epic-9.md`.** Update the Status header and acceptance checklist. MemoryPack and the two economy validators stay under **Deferred** (owner: Milestone 1 / MemoryPack slice).
3. **`.agents/curentfocus.txt`**, then append decisions to `.agents/architecture_history.md`: nav clearance model, determinism scheme, economy model, personality/considerations.

---

## Risks

| Risk | Mitigation |
| :--- | :--- |
| Phase 1 clearance changes make doorways or pen gaps impassable | Doorway pass-through case in `GridAStarValidator`; clearance cost is soft, only the body check is hard |
| Phase 4 changes demo-pen behaviour | Pen agents get fixed neutral personalities; anomaly scenarios are the regression gate |
| Economy tuning becomes a hand-tweaking loop | Tune only against `EconomySoakScenario` output with fixed seeds (Phase 2 makes runs comparable) |
| Phase 5 touches `SwarmSpawner` at the same time as Phase 1 | Sequence Phase 5 after Phase 1 merges, or keep Phase 1 out of `SwarmSpawner` |
| Arrival time of cognition results stays nondeterministic | Documented as remaining in §1A.2 #6; owned by the future multiplayer epic |

## Open questions for the producer

1. ~~Phase order~~: approved 2026-10-09.
2. Economy decisions 1–4 in Phase 3: **deferred** until the game is functional.
3. ~~Pen personalities~~: **neutral** (decided 2026-10-09).
4. ~~Branch~~: `feat/milestone1-supply-loop-zlogger` (decided 2026-10-09).
