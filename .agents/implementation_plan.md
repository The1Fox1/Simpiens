# Section 3.1 — Close the Milestone 1 Supply Side

Review §3.1 says the player loop (*post bounty → agents harvest salvage → deliver/sell at stall → trust grows*) does not run in the live simulation. I checked this against the code. The review is right, and the gap is **wider than the review describes**. Two of the blockers below are not in the review at all.

## Findings (verified against the code, 2026-10-09)

| # | Finding | Evidence | In review? |
|---|---|---|---|
| F1 | **GOAP (Tier 2) never runs in the live session.** `SurvivalUtilityEvaluator` is the last Tier-1 evaluator, and *every* code path in it returns a non-null intent (Idle, Wander or Harvest). `AgentCognitionService` only reaches Tier 2 when all evaluators return `null`, so no goal is ever selected: not `FulfillBountyGoal`, not `LiquidateSalvageGoal`, not even `RestGoal`. | [SurvivalUtilityEvaluator.cs:39-118](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Evaluators/SurvivalUtilityEvaluator.cs#L39-L118), [AgentCognitionService.cs:136-150](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs#L136-L150) | ❌ New |
| F2 | **Resources are untyped.** `ResourceData` has only `EntityId` + `RemainingYield`. `EntitySnapshot` and `SpatialMemoryRecord` carry no resource kind either. | [ResourceData.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/ResourceData.cs) | ✅ |
| F3 | **Harvesting never produces an item.** On success, `AgentConsequenceService` subtracts 50 Hunger and adds a Ration, whatever the node is. No code path adds `IronOre`/`MedicinalHerb`. | [AgentConsequenceService.cs:56-60, 136-140](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentConsequenceService.cs#L56-L140) | ✅ |
| F4 | **`HarvestResourceAction` (GOAP) is a stub.** It emits an `IdleIntent(1.5s)`, and the consequence service then hands out food for idling. | [HarvestResourceAction.cs:21-24](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Actions/HarvestResourceAction.cs#L21-L24) | ❌ New |
| F5 | **Scout bounties can be claimed but never fulfilled.** `ClaimBountyAction` takes the first open bounty of any type. `DeliverBountyIntentHandler` requires `TargetItemType` items, which is `None` for Scout bounties. | [ClaimBountyAction.cs:29-38](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Actions/ClaimBountyAction.cs#L29-L38) | ✅ |
| F6 | **Ration stock only goes down**, and depleted nodes are destroyed (`UnregisterNode` + `SetActive(false)`), so the world runs out of supply for good. | [HarvestResourceIntentHandler.cs:189-198](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/HarvestResourceIntentHandler.cs#L189-L198) | ✅ (stock) / ❌ (nodes) |
| F7 | **No population could run the loop anyway.** `SpawnCount = 24`, and all 24 agents are scripted into walled demo pens. The stall sits at `(0,0)`, inside **Pen 3**, on top of the rivalry bush. The seeded Scout bounty targets `(-4,-9)`, inside walled Pen 7. | [SwarmSpawner.cs:26, 383-421](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Testing/Environment/SwarmSpawner.cs#L383-L421) | ❌ New |
| F8 | **Seeded bounties bypass escrow.** `RegisterBounty` doesn't lock escrow, so delivery calls `AddGold(RewardGold)` with no matching debit and mints gold. | [DeliverBountyIntentHandler.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/DeliverBountyIntentHandler.cs), [EconomyLedger.cs:43-60](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/Economy/EconomyLedger.cs#L43-L60) | ❌ New |

> [!IMPORTANT]
> F1 is the root cause. Fixing F2–F6 alone would still produce **zero** live bounty completions, because the planner never runs. This also means every GOAP goal validated in Epics 6–9 only works under validator injection. That is a status-accuracy issue for the roadmap.

## User Review Required

> [!WARNING]
> **Behaviour change in the demo pens (F1 fix).** After the fix, Tier-1 survival only fires for genuine hunger. Ambient idle/wander moves to a Tier-3 fallback, so well-fed agents start pursuing GOAP goals (Rest, Socialize, GatherAtAnchor, Comply…). The pens were tuned while GOAP was dead, so some demos may look different. The existing PlayMode anomaly scenarios (stacking/thrashing) are the regression gate, and I will re-tune pen seeds if they fail.

> [!WARNING]
> **`SwarmSpawner.SpawnCount` is a serialized field.** I'll raise the code default from 24 to 30 (adding 6 free "crawlers"), but if the scene overrides it in the Inspector, **check the value on the `SwarmSpawner` binding in the Unity Editor**.

## Open Questions

> [!IMPORTANT]
> **Q1 – Ration restock model.** I propose a minimal **supply caravan**: every 600 ticks (60 s), the stall restocks up to a cap of 50 and pays a wholesale price (2g/unit) from its treasury through the ledger as a named sink `"SupplyCaravan"`. The alternative is to have agents harvest food bushes and sell Rations to the stall, which is more emergent but adds a second production chain. Section 3.3 (faucets/sinks) will retune either way. **Caravan OK?**

> [!IMPORTANT]
> **Q2 – What counts as "scouted"?** Proposal: the claimant physically enters the target sector's `FacilityZone` bounds, then reports back at the stall. On report, the sector is marked explored in `FacilityZoneService`, which drives the existing UI shroud.

> [!IMPORTANT]
> **Q3 – Regrowth in the demo pens.** Proposal: regrowth is **off** for pen bushes (`RegrowTicks = 0`) so the scripted contention demos stay deterministic, and **on** for Outpost Commons nodes.

> [!IMPORTANT]
> **Q4 – Epic 9 status after this lands.** If the new no-injection PlayMode scenario passes, §1B allows Epic 9 to move to `COMPLETED`. MemoryPack and the two economy validators are already listed under *Deferred*. **Promote it, or keep `MECHANISM COMPLETE` until §3.2/§3.3 land?**

---

## Proposed Changes

### Phase 1 — Typed resources (data layer)

#### [MODIFY] [ResourceData.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/ResourceData.cs)
- Add `ItemType YieldItem` (`Ration` = food bush, `IronOre`, `MedicinalHerb`), `int MaxYield`, `uint RegrowTicks` (0 = never), and `uint DepletedAtTick`.
- Stays a plain C# class owned by `SimulationManager` (the authoritative single writer).

#### [MODIFY] [EntitySnapshot.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/Spatial/EntitySnapshot.cs), [SpatialMemoryRecord.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Memory/SpatialMemoryRecord.cs)
- Add `ItemType ResourceKind` (1 byte). Default `None` keeps existing constructors source-compatible.
- `AgentMemory.UpdateMemory` copies the kind from snapshot to memory record, so off-thread planners can tell food from ore with no extra lookup.

#### [MODIFY] [NodeController.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Entities/NodeController.cs), [SpatialHashGrid.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/Spatial/SpatialHashGrid.cs)
- `NodeController` gets an immutable `ResourceKind` that is set once at spawn, the same way `Type` is. `SpatialHashGrid.UpdateFromRegistry` writes it into `EntitySnapshot`.
- *Unity note:* this is spawn-time configuration, not mutable state. The authoritative value stays in `ResourceData`.

#### [MODIFY] [AgentSpriteLibrary.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Entities/AgentSpriteLibrary.cs)
- Procedural `OreVeinSprite` and `HerbPatchSprite` (tinted variants of the existing resource sprite), so the player can see salvage nodes.

---

### Phase 2 — Unblock Tier-2 cognition (F1, F4)

#### [MODIFY] [SurvivalUtilityEvaluator.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Evaluators/SurvivalUtilityEvaluator.cs)
- Becomes purely **reflexive**: it returns a food `HarvestResourceIntent` only when `Hunger ≥ 60` (the existing high threshold) and a known `ResourceKind == Ration` node is reachable. Otherwise it returns **`null`**, so control passes to GOAP.
- Only considers food nodes, so starving agents never path to an ore vein to "eat" it.

#### [NEW] `Cognition/Evaluators/AmbientWanderEvaluator.cs`
- The idle/wander logic moved out of `SurvivalUtilityEvaluator`, unchanged. It runs as the **Tier-3 fallback**.

#### [MODIFY] [AgentCognitionService.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentCognitionService.cs), [AutonomousAgent.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AutonomousAgent.cs)
- Accept an optional `ICognitiveEvaluator fallbackEvaluator`. Tier 3 calls it before the bare `IdleIntent`. Null-safe for headless validators.

#### [MODIFY] [HarvestResourceAction.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Actions/HarvestResourceAction.cs)
- Replace the `IdleIntent` stub with a real `CreateIntentAsync`: pick the nearest known food node, path to it, and emit a `HarvestResourceIntent`. Same reachability check as the evaluator.

---

### Phase 3 — Salvage production and GOAP domain

#### [MODIFY] [StateFact.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/StateFact.cs)
Five new facts (30 → 35 of the 64-bit budget):

| Fact | Bit | Built from |
|---|---|---|
| `KnowsSalvageLocation` | 30 | Memory has a non-blacklisted record with `ResourceKind ∈ {IronOre, MedicinalHerb}` |
| `HasGatherBounty` | 31 | Agent's claimed bounty is `GatherResource` |
| `HasScoutBounty` | 32 | Agent's claimed bounty is `ScoutSector` |
| `HasBountyItem` | 33 | Inventory holds the claimed bounty's `TargetItemType × TargetQuantity` |
| `HasScoutReport` | 34 | `AgentState.ScoutReportBountyId == claimed bounty id` |

`KnowsResourceLocation`/`AtResourceLocation` are narrowed to **food** nodes in [WorldStateBuilder.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/WorldStateBuilder.cs).

#### New/changed GOAP actions (`Cognition/Planning/Actions/`)

| Action | Preconditions | Effects | Intent |
|---|---|---|---|
| **[NEW]** `SearchSalvageAction` | — | `KnowsSalvageLocation` | Wander toward unexplored salvage zones (cost 3) |
| **[NEW]** `GatherSalvageAction` | `KnowsSalvageLocation` | `HasSalvageInInventory`, `HasBountyItem` | `HarvestResourceIntent` to the best salvage node. If a Gather bounty is claimed, prefer its `TargetItemType`; otherwise pick by stall buy price ÷ distance. Ties break by GUID, so selection is deterministic. |
| **[MODIFY]** `ClaimBountyAction` → split into `ClaimGatherBountyAction` / `ClaimScoutBountyAction` | `!HasActiveBounty` | `HasActiveBounty` + `HasGatherBounty` / `HasScoutBounty` | `ClaimBountyIntent` filtered by `BountyType`. `IsValid` only when an open bounty of that type exists. |
| **[MODIFY]** `DeliverBountyAction` | `HasGatherBounty`, `HasBountyItem`, `AtTradeStall` | `BountyDelivered`, `!HasActiveBounty`, `!HasGatherBounty` | unchanged |
| **[NEW]** `ScoutSectorAction` | `HasScoutBounty` | `HasScoutReport` | `ScoutIntent` (path to target sector) |
| **[NEW]** `ReportScoutBountyAction` | `HasScoutBounty`, `HasScoutReport`, `AtTradeStall` | `BountyDelivered`, `!HasActiveBounty`, `!HasScoutBounty` | `DeliverBountyIntent` (handler branches on type) |

`LiquidateSalvageGoal` now becomes reachable through `SearchSalvage → GatherSalvage → TravelToStall → SellSalvage`. Each goal's `CalculatePriority` keeps its current curve; per-agent variation is left to §3.2 (personality).

#### [MODIFY] [HarvestResourceIntentHandler.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/HarvestResourceIntentHandler.cs)
- On completion, if `YieldItem != Ration`, add `ItemInstance(YieldItem, 1, baseValue)` to the agent's inventory (main thread, single writer). If the inventory is full, return `Failed` without decrementing yield.
- Record the yielded type on the intent (`HarvestResourceIntent.YieldedItem`, written on the main thread only) so the consequence service can branch on it.
- On depletion with `RegrowTicks > 0`: mark the node **dormant** and hand it to the regrowth service instead of destroying it.

#### [MODIFY] [AgentConsequenceService.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AgentConsequenceService.cs)
- Hunger relief (−50) applies only when `YieldedItem == Ration`. Salvage harvests leave hunger alone.

---

### Phase 4 — Scout bounty fulfilment (F5)

#### [NEW] `Cognition/Contracts/ScoutIntent.cs` + `Cognition/Planning/Handlers/ScoutIntentHandler.cs`
- Follows the path. When `GetSectorForPosition(node.Position) == bounty.TargetSectorId`, set `AgentState.ScoutReportBountyId`/`ScoutReportTick` and return `Success`. Registered in `IntentHandlerRegistry.CreateDefault()`.

#### [MODIFY] [DeliverBountyIntentHandler.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/Handlers/DeliverBountyIntentHandler.cs)
- Branch on `BountyType`: Scout requires a matching scout report (no items), clears the report, and calls `IFacilityZoneService.MarkZoneExplored(TargetSectorId)`. The Gather path is unchanged.
- Payout comes **only** through `IEconomyLedger.TryReleaseEscrowToClaimant`. The direct `AddGold` is removed; un-escrowed bounties are rejected at registration instead (F8).

#### [MODIFY] [SimulationManager.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs) (bounty posting)
- If a posted bounty's `TargetLocation` is `default`, derive it from the target zone's walkable centre (`IFacilityZoneService` + `StaticNavGrid` nearest-walkable).
- `RegisterBounty` (seeding path) routes through the same escrow lock as `PostBountyCommand`.

---

### Phase 5 — Supply faucets (F6)

#### [NEW] `Simulation/Economy/ISupplyService.cs` + `SupplyService.cs`
Plain C# with no Unity API. `SimulationManager.Tick()` calls it once per logical tick, in a fixed order after intent execution.
- **Resource regrowth:** dormant nodes whose `DepletedAtTick + RegrowTicks ≤ currentTick` reset to `MaxYield` and are re-registered. Dormant nodes are iterated in a **sorted list**, not dictionary order, so results are deterministic.
- **Supply caravan (Q1):** every `RestockIntervalTicks`, each stall restocks rations up to `RationStockCap`. Cost is `WholesaleRationCost × units`, paid from treasury via `IEconomyLedger.RecordSink("SupplyCaravan", …)`. Restock is skipped if the treasury can't pay. Emits a chronicle event.
- All tunables live in a `SupplyTuning` struct (durations in **ticks**), injected through VContainer and overridable from a ScriptableObject later (§3.3).

#### [MODIFY] [IIntentExecutionContext.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Contracts/IIntentExecutionContext.cs)
- Add `MarkResourceDormant(GUID)` so handlers never touch `GameObject` activation directly. The view toggles visibility from the registry.

#### [MODIFY] [AppBootstrapper.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/AppBootstrapper.cs)
- Register `ISupplyService`, `AmbientWanderEvaluator`, and the new validator.

---

### Phase 6 — Live "Outpost Commons" arena (F7)

#### [MODIFY] [SwarmSpawner.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Testing/Environment/SwarmSpawner.cs)
- Use the open yard **north of the pens** (y ≈ 5…15, currently empty) as the *Outpost Commons*:
  - Move the stall from `(0,0)` (inside Pen 3) to `(0, 9)`.
  - **Ore Ridge** (west): 3 iron veins, yield 3, regrow 900 ticks. **Herb Hollow** (east): 3 herb patches, yield 3, regrow 600 ticks. Plus 2 food bushes near the stall, regrow 450 ticks.
  - `SpawnCount` 24 → **30**: agents 24–29 are free "crawlers" with no seeded relationships or memory (no precondition injection).
  - The seeded Gather bounty targets IronOre. The seeded Scout bounty targets the new Herb Hollow zone. Both are posted through the escrow path.
- Register the new GOAP actions/goals and the fallback evaluator.

> [!NOTE]
> The production domain still lives in `SwarmSpawner` under `Testing/` (Deviation #9, review §2.8). This plan **does not extend** that deviation beyond the additions needed to wire the loop, and it does not attempt the move. That stays with §2.8.

#### [MODIFY] [FacilityZoneService.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/UI/Zones/FacilityZoneService.cs)
- Add zones 11 *Outpost Market*, 12 *Ore Ridge*, 13 *Herb Hollow* (12 and 13 start **unexplored**). Add `MarkZoneExplored(int)`.

---

### Phase 7 — Verification assets

#### [NEW] `Testing/Economy/SupplyLoopValidator.cs` (mechanism tests, headless)
1. Typed harvest: an ore node yields `IronOre` into inventory and leaves hunger unchanged. A food node still relieves hunger.
2. A full inventory causes `Failed` with no yield lost.
3. Regrowth: a depleted node with `RegrowTicks = 50` is harvestable again at tick +50, not at +49.
4. Caravan: restock respects the cap and treasury, and the ledger conservation invariant holds.
5. Scout: entering the target zone sets the report, delivery pays out through escrow, and the zone is marked explored.
6. Planner: from a *clean* world state with an open Gather bounty, `GoapPlanner` returns `ClaimGather → SearchSalvage → GatherSalvage → TravelToStall → DeliverBounty`.
7. F1 regression: a well-fed agent with an open bounty gets a **GOAP-produced** intent, not Idle/Wander.
8. Zero GC bytes over 1,000 supply ticks.

#### [NEW] `Assets/Tests/PlayMode/Scenarios/OutpostSupplyLoopScenario.cs` (Definition-of-Done scenario)
Runs the **real session** at 10× speed and injects **nothing**: no inventory, teleports, forced intents, or pre-claims. It asserts, within a bounded tick count:
- ≤ 3,000 ticks: at least one Gather bounty is `Completed` and the claimant's affinity toward `PlayerEntityId` has Trust > 0.
- ≤ 3,000 ticks: at least one salvage `SellItem` trade succeeds (stall ore/herb stock increases).
- ≤ 4,000 ticks: the Scout bounty is `Completed` and zone 13 is explored.
- ≤ 6,000 ticks: at least one caravan restock happens and at least one regrowth event fires.
- Ledger conservation holds throughout, and there are 0 stacking/thrashing anomalies.

Wired into [SwarmPlayModeTests.cs](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Tests/PlayMode/SwarmPlayModeTests.cs) as `Scenario_OutpostSupplyLoop_EmergesWithoutInjection`.

---

### Phase 8 — Documentation (roadmap-first, per AGENTS.md)
1. [ROADMAP.md](file:///home/bfox/Code/Repos/Unity/Simpiens/docs/ROADMAP.md): update *Milestone 1 Status* gaps (salvage supply, Scout, restock) and add F1/F7 to §1A.2 as resolved deviations. Epic 9 status follows the answer to Q4.
2. [epic-9.md](file:///home/bfox/Code/Repos/Unity/Simpiens/docs/epics/epic-9.md): update the status header and acceptance checklist, delivered items only.
3. `.agents/architecture_history.md`: append a §3.1 decision entry. `.agents/curentfocus.txt`: mirror the roadmap.
4. `AGENTS.md`: list `SupplyService`, `ScoutIntentHandler`, `AmbientWanderEvaluator`.

## Out of Scope (owned elsewhere)
- Personality-driven variation in bounty acceptance → **§3.2**.
- Economy model, faucet/sink tuning, market soak runner → **§3.3** / Epic Test-2. This plan adds only the minimum faucets the loop needs to keep running.
- Moving the production domain out of `Testing/` → **§2.8**.
- Seeded RNG for existing evaluators (Deviation #5). New code adds **no** randomness: all selection is deterministic.

## Verification Plan

### Automated Tests
- Roslyn build of `Simpiens.dll` and `Simpiens.Tests.PlayMode.dll`: 0 errors.
- Headless runner: `SupplyLoopValidator` (8 tests) plus regression runs of `GridAStarValidator`, `SingleWriterMemoryValidator`, `ResourceContentionValidator`, `AltruisticSharingValidator`, `BountyFulfillmentValidator`, `TradeTransactionValidator`, `GoapPlannerValidator`.
- `scripts/run-playmode-tests.sh`: the new `OutpostSupplyLoopScenario` plus the three existing anomaly scenarios, which are the gate for the F1 behaviour change.

### Manual Verification
- In the Editor: watch the Outpost Commons at 5–10× speed. Crawlers should claim bounties, walk to Ore Ridge, return to the stall, and show up in the chronicle feed. Post a new Gather bounty from the Bounty Board UI and confirm it gets picked up.
- Check the `SwarmSpawner` Inspector `SpawnCount` binding (see warning above).
