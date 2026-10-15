# Milestone 1 Completion Plan: MemoryPack and Save/Load

- **Owner:** Milestone 1 (ROADMAP §3, *Milestone 1 Status*). Status is owned by `docs/ROADMAP.md`.
- **Written:** 2026-10-15, after §3.3 shipped (PR #3) and both producer checks passed.
- **Status:** approved 2026-10-15 by the producer, with D1–D6 as recommended.
- **Progress:** step 0 (the MemoryPack spike) in progress.
- **Branch:** `feat/save-load`, from `main`.
- **Goal:** close Milestone 1.
  - Everything in it is delivered except one clause of the ROADMAP's milestone definition: "Integrates MemoryPack serialization for unmanaged inventory/contracts".
  - Epic 9 also lists two validators as deferred: `EconomySerializationValidator` and `EconomyZeroAllocValidator`.
- **Recommended reading of that clause (D1 = B):** the player can save the outpost and load it back. MemoryPack serializes the whole session, and the economy's data is the first part of it.
- **What the producer sees after this plan (D1 = B, D2 = A):**
  - Save and Load buttons in the dashboard header, with one quick-save slot.
  - Loading puts everything back as it was at the saved tick:
    - every crawler: needs, purse, pack, memories and relationships;
    - the stall, and the bounties with their escrow;
    - the fog, the Garden's regrowth and the chronicle.
  - On the first tick after a load, every crawler decides afresh what to do (D3). Someone who was walking somewhere may turn around.
  - A save made with a different save format, or on another map, is refused with a message rather than loaded wrongly (D4).

---

## 1. Where Milestone 1 stands

| Part | Status |
| :--- | :--- |
| Epic 9 (economy and bounties, with §3.1 supply, §3.2 personality and §3.3 the economy model) | `COMPLETED` 2026-10-15 |
| Epic UI-2 (merchant terminal, bounty board) | `COMPLETED` |
| Epic Test-2 (economy harness, soak with pace bands) | `COMPLETED` 2026-10-15 |
| Epic MAP (the outpost floor, fog of war, the player's view) | `COMPLETED` 2026-10-13 |
| Zero-allocation Grid A* | Delivered 2026-10-09 |
| **MemoryPack serialization** | **Not started:** the package isn't installed and no code uses it |
| `EconomySerializationValidator` and `EconomyZeroAllocValidator` (Epic 9 §6 items 3–4) | Deferred, not implemented |

**Not Milestone 1.** Each of these already has an owner:
- cognition scaling and a profiled soak (§3A #8, before Epic 10);
- fixed-latency cognition, which causes `DeterminismScenario`'s cold-start divergence (§1A.2 #6, the multiplayer work);
- drifters (Milestone 2);
- the "Why?" inspector (§3A #10b, Track B);
- tick replays and floor relocation (Milestone 4);
- `Unity.Collections` and Burst (approved, not needed yet).

---

## 2. Findings from the code

| # | Finding | Where | Consequence |
| :-: | :--- | :--- | :--- |
| F1 | **MemoryPack isn't installed, and the project vendors its own NuGet packages.**<br>• ZLogger's DLLs are copied into `Assets/Plugins/ZLogger/` by a script. That set already includes `System.Runtime.CompilerServices.Unsafe.dll`, which MemoryPack also needs.<br>• MemoryPack needs Unity 2022.3.12f1 or later (this project uses 6000.4.3f1).<br>• Its source generator runs as a Roslyn analyzer. | `Packages/manifest.json`; `scripts/update-zlogger.sh`; `Assets/Scripts/csc.rsp` (C# 10) | Install it the same way, without shipping two copies of one assembly (step 0). |
| F2 | **Most of the data to save holds no references** (it is "unmanaged"): `ItemInstance`, `BountyContract`, `TradeStallData`, `AgentNeeds`, `AgentActivity`, `AgentPersonality`, `AffinityRecord`, `MemoryEvent`, `SpatialMemoryRecord`, `GroupData` and `ChronicleEvent`. Ids (`UnityEngine.GUID`) and positions (`Vector2`) are unmanaged too. | `Simulation/Economy/`, `Cognition/Contracts/`, `Cognition/Memory/`, `Narrative/` | MemoryPack copies these as raw memory. That's fast, but the save's format is their exact layout: changing a field in any of them makes older saves unreadable (D4). |
| F3 | **The containers that hold them are managed objects:**<br>• each crawler's memory: four dictionaries, an event ring buffer, survey keys and sector visit ticks;<br>• the ledger's named totals;<br>• group member sets;<br>• the simulation manager's dictionaries of stalls, bounties, resources and bunks. | `AgentMemory.cs:15-106`; `EconomyLedger.cs:16-35`; `GroupRegistry.cs:20-31`; `SimulationManager.cs:79-90, 212-215` | Capture each one as a flat array sorted by id, so a save is deterministic (§1A.1 #3). Restore rebuilds the dictionaries. |
| F4 | **Randomness keeps no state.** Every draw comes from `SimRandom.For(seed, entity, tick, salt)`. Entity ids come from `EntityIdAllocator`, which is a seed plus a counter. | `SimRandom.cs:20`; `EntityIdAllocator.cs:23-44` | A save needs the seed, the tick and the allocator's counter, and no random-number state. |
| F5 | **A session is rebuilt, never patched.**<br>• `SwarmSpawner.Respawn` resets everything, loads the map and spawns nodes, bunks and hearths with reproducible ids ("same seed + same spawn order => same entity ids").<br>• `Populate` then adds crawlers, stalls and bounties, and the ledger records its baseline.<br>• The clock and the id allocator can only be reset to zero. | `SwarmSpawner.cs:141-221`; `SimulationClock.cs:48`; `EntityIdAllocator.cs:44` | A load runs the first half of a respawn, then restores the saved content in place of `Populate`. The clock, allocator, ledger, supply service, sector registry, chronicle and group registry each need a way to restore their state. |
| F6 | **Work in progress is hard to save.**<br>• A crawler's current intent and plan come in a dozen types, and the cognition thread writes part of them (§1A.2 #15).<br>• Active intents carry waypoints and timers.<br>• Bunk claims and harvest contention depend on them. | `AgentState.cs:92-100`; `SimulationManager.cs:22-33, 87-90` | Don't save them. Crawlers decide afresh after a load (D3), and claims tied to that work are released. |
| F7 | **Some state the player sees lives only in views.**<br>• Crawler names exist only in the GameObject's name and the log's name table, not in `AgentState`.<br>• A dormant node's hidden sprite is view state, though its regrowth is plain data. | `SwarmSpawner.cs:248`; `SimulationManager.cs:80` | The save carries the names, and a load re-syncs the views. Step 2 audits for the rest. |
| F8 | **`WorldStateHash` covers only part of the world:** positions, needs, gold, inventory, intent and goal; stalls; bounties; the fog; ledger totals. | `WorldStateHash.cs:11-16` | The round-trip test compares the saves themselves: capture, load, capture again, and the bytes must match. The hash is a second check. |
| F9 | **Session-level actions already bypass the command queue.** The Dev Map button calls the session switcher directly. | `FacilityDashboardPresenter.cs:266` | Save and Load work at session level too, so they take the same path. A save is taken on the simulation thread, between ticks. |
| F10 | **The zero-allocation check has a house pattern:** `GC.GetAllocatedBytesForCurrentThread()` around a loop, already used by several validators. | `DeterminismValidator.cs:224-236` | `EconomyZeroAllocValidator` uses the same pattern. |

---

## 3. The design (recommended)

### 3.1 The save
- **`SessionSave`** lives in a new `Simulation/Persistence/` folder (`Simpiens.Simulation.Persistence`).
- **A header** holds:
  - the format version and the build version;
  - the session id and a map fingerprint;
  - the seed, the tick and the id allocator's counter.
- **One block per part,** each an array of unmanaged records sorted by id:
  - crawlers: names, needs, purse, pack, personality, timers, memory and relationships;
  - stalls, bounties and the ledger;
  - resource nodes and their regrowth;
  - groups, the fog mask and the chronicle.
- **MemoryPack attributes:** only the container types are `[MemoryPackable]`. Unmanaged records need no attribute.
- **MemoryPack sits behind one class,** so the rest of the code never calls it directly.

### 3.2 Saving
- A save is taken on the simulation thread after a tick has finished. Each owner writes its part.
- The save reads nothing that the cognition thread writes (F6).
- Writing the bytes to a file happens off the main thread. The quick-save slot is `Saves/quicksave.simsave` under Unity's persistent data path.

### 3.3 Loading
- **Rebuild:** `SwarmSpawner.Restore(save)` runs the first half of a respawn (reset, map, and static entities with their reproducible ids). Then each owner restores its part.
- **Crawler views** are spawned with their saved ids and names.
- **The clock** resumes at the saved tick. The session epoch moves on, so late cognition results from before the load are dropped.
- **The ledger** takes its saved totals as they are. There is no second `InitialSeed`.
- **Player commands** queued before the load are discarded.
- **Refusal:** a save with another format version, session or map fingerprint is refused (D4).

### 3.4 Not saved
- Plans, intents and the claims that depend on them (D3).
- Cognition in flight.
- Derived state, which is rebuilt on load: snapshots, the spatial grid and the nav grid.
- The map and the economy tuning. Both come from the session definition.

---

## 4. Steps

Each step is its own commit, and the full PlayMode suite gates every step.

0. **Install MemoryPack (spike).**
   - Vendor MemoryPack and its generator the way ZLogger is vendored, from one dependency closure, so no assembly ships twice.
   - Load the generator as a Roslyn analyzer.
   - Round-trip a trivial type in both `Simpiens` and `Simpiens.Testing`, in the Editor and in a Linux player build.
   - Pin the version and record it in ROADMAP §2.1.
   - If it won't install cleanly, stop and bring the producer the options. The fallback is a small hand-written writer for the flat arrays.
1. **The economy's data.** This is the ROADMAP's literal Milestone 1 item.
   - `EconomySerializationValidator`: `ItemInstance`, `BountyContract`, `TradeStallData` and `AgentInventory` round-trip with 0 bytes of garbage, into a reused buffer.
   - `EconomyZeroAllocValidator` (D5): 1,000 ticks of trading and bounty evaluation with no steady-state allocation.
2. **Capture.**
   - `SessionSave`, and a capture method for each owner.
   - Audit for player-visible state held only in views (F7).
   - Validator: capturing the same state twice gives the same bytes, and every part is present.
3. **Restore.**
   - `SwarmSpawner.Restore`, and a restore method for each owner.
   - Validator (headless): capture → bytes → restore → capture gives identical bytes.
4. **Save files.**
   - The service, the slot, the header checks and the asynchronous write.
   - Validator: a mismatched header and a truncated file are both refused with a message.
5. **Live Definition of Done: `SaveLoadScenario`** on the outpost, with nothing injected.
   - Play to t3,000, save, and load into a fresh session.
   - The re-captured save must equal the file, and `WorldStateHash` must match.
   - Play 3,000 more ticks and check:
     - 0 anomalies;
     - the ledger balances;
     - nobody starves and every crawler eats;
     - at least one harvest → sell cycle and one bounty event happen after the load.
   - Gate: full suite ×3.
6. **Save and Load in the dashboard header** (D2 = A).
   - A scenario asserts view state: Save writes the slot, and Load restores its tick.
   - Then the producer's check at 1×.
7. **Docs close-out.**
   - ROADMAP first: Milestone 1 `COMPLETED`, the §2.1 install status, and Epic 9's deferred items.
   - Then Epic 9 §6–7, and Epic MAP's deferred "map save/load".
   - Then curentfocus, architecture_history, and AGENTS.md (the Persistence folder and its rules).
   - Delete this plan.

**Size:** about the same as §3.3, 8–10 commits. Step 0 is the main unknown.

---

## 5. Risks

| Risk | Mitigation |
| :--- | :--- |
| MemoryPack won't install cleanly next to the vendored ZLogger DLLs: duplicate assemblies, assembly version validation, or the generator in an asmdef project. | Step 0 is a spike that stops for a decision. Vendoring from one closure avoids duplicates. The serializer sits behind one class, so a fallback is cheap. |
| Player-visible state hides outside plain data (F7). | Step 2 audits for it. The live scenario plays 3,000 ticks after a load, so behaviour that breaks shows up there. |
| A loaded game doesn't replay the original future exactly, because of cognition timing (§1A.2 #6) and rebuilt dictionaries (§1A.2 #14). | Assert identical state at the moment of loading, not identical futures. |
| The save format is the struct layout (F2), so changing a field breaks old saves. | The header carries a format version, and Milestone 1 refuses old saves (D4). A validator pins the saved structs' sizes, so a layout change can't slip through unnoticed. |
| A save is taken mid-tick, or while cognition writes off-thread (§1A.2 #15). | Saves are taken on the simulation thread between ticks, and skip the plan and intent fields. |

---

## 6. Decisions for the producer

| # | Question | Options | Recommendation |
| :-: | :--- | :--- | :--- |
| D1 | How much does "MemoryPack serialization" cover? | **A:** the economy's data only: steps 0–1, a live check that round-trips the running economy, and step 7. About 3–4 commits.<br>**B:** a whole-session save and load: all steps. About 8–10 commits. | **B.** Nothing in the game would use A, so its live check would be thin. B proves "state is plain data" (§1A.1 #5) end to end, and both Milestone 4's floor relocation and multiplayer join-in-progress build on it. |
| D2 | Do players get save and load in Milestone 1? | **A:** one quick-save slot, with Save and Load in the dashboard header.<br>**B:** tests only for now; the UI comes later in Track B. | **A.** It's one step, and it means a long outpost session isn't lost when the game closes. |
| D3 | What happens to work in progress? | **A:** it isn't saved; crawlers decide afresh on the first tick after a load.<br>**B:** save intents and plans. | **A.** B needs a hand-registered MemoryPack union over a dozen intent types, plus state the cognition thread writes, for little visible gain. |
| D4 | What happens to saves from an older build? | **A:** refuse a save with another format version, session or map. No migration in Milestone 1.<br>**B:** migrate old saves. | **A.** B needs version-tolerant types, which mean a slower, larger format and more code. Revisit when saves must survive game updates. |
| D5 | Does `EconomyZeroAllocValidator` belong in this slice? | **A:** yes.<br>**B:** defer it to the scaling work (§3A #8). | **A.** It closes Epic 9's deferred list, and the Definition of Done's hot-path rule (§1B.1) asks for it. |
| D6 | Close-out cleanups? | **(a)** The dossier's *Feed ration* button writes hunger straight from the UI (Epic MAP §6 #9), which breaks §1A.1 #7. Make it a command, or remove it.<br>**(b)** Refresh stale docs:<br>• ROADMAP's "Closure plan: proposed … (pending producer approval)" line;<br>• the old Milestone 1 status record;<br>• the design proposal's "12 interconnected sectors" MVP line, superseded 2026-10-11. | **(a)** Remove the button, unless the producer uses it.<br>**(b)** Yes, in step 7. |

---

## 7. Milestone 1 is `COMPLETED` when
- MemoryPack is installed, pinned and recorded (ROADMAP §2.1).
- The validators pass: `EconomySerializationValidator`, `EconomyZeroAllocValidator` (D5), and the capture and round-trip validators.
- `SaveLoadScenario` passes in 3 consecutive full-suite runs, with nothing injected.
- With D2 = A, the producer's check passes: save and load at 1×.
- The docs are reconciled (step 7), and this plan is deleted.
