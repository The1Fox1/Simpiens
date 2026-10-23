# Epic 10 G1: drifters (working plan)

Working document (AGENTS.md, Documentation Protocol 6): deleted when G1 closes. Epic: `docs/epics/epic-10.md` §2.2, §3;
producer decision Q7 (2026-10-23): the 12 residents stay; a few drifters at a time arrive with their own gold and leave
after a stay; the rate and the cap are tuned here. Branch `feat/epic10-g1`, stacked on `feat/epic10-g0` (PR #7) until it
merges; one pull request into `main`.

## Goal
Strangers walk in through the Road Gate with gold of their own, eat, trade and move on. Their gold is new money from
outside the outpost, named in the ledger, so the economy is no longer closed: a slightly greedy player now does better
than in a closed economy (economy model §7, "why greed doesn't pay here").

## Design (defaults; the numbers are tuning)
- **Numbers in `DrifterTuning`**, nested in `EconomyTuning` like `SupplyTuning`: first arrival at t1200, then one every
  1,200 ticks ± 300 (seeded), at most 3 at once, a stay of 1,800–3,000 ticks, a purse of 40–80g, arrival hunger 40–70
  (they come off the road hungry: outside buyers of rations). `DrifterTuning.None` turns them off (pens, measurements).
- **Arrival** (`SessionHost`, on the tick, main thread): a free Road Gate spawn point; a seeded personality; the purse
  recorded as the faucet `DrifterPurses`. A drifter is a crawler like any other (same cognition), with `AgentState.IsDrifter`
  and `DepartsAtTick`. Label `Drifter{n}`; the identity service names it like everyone else.
- **Departure is a decision, not a teleport:** past `DepartsAtTick` a drifter's `LeaveOutpostGoal` (high priority, and
  valid only for a drifter whose stay is over) plans `TravelToGate -> LeaveOutpost`. On leaving: its gold goes out as the
  sink `DrifterDepartures`, its pack leaves with it, a claimed bounty is released (escrow stays), a bunk claim dropped,
  the entity unregistered, the runtime disposed, `SessionHost.CrawlerDeparted` raised, the chronicle told.
- **Saves:** `AgentRecord` gains `IsDrifter`, `DepartsAtTick`; a new `PopulationSave` block (the host's: next arrival tick,
  drifters so far). The save format version goes up; the round-trip validators check both.
- **Determinism:** arrival times, purses, hunger and stays from `SimRandom.For(seed, n, salt)`; ids from the allocator.
- **Views:** the KNI client draws whatever the registry holds (crawlers leaving are forgotten since review S1); the
  dossier shows "Drifter, leaves in N min" or "Resident"; the chronicle tells arrivals and departures. Unity's views are
  frozen, but a departed crawler's GameObject must go: `SwarmSpawner` destroys it on `CrawlerDeparted` (an adaptation,
  not new view work).
- **The day's checks** (`OutpostWatch`) judge the residents; drifters are counted (arrived, bought, left) and their gold
  is in the conservation check while they are present.

## Steps
- [ ] 1. `DrifterTuning`, `AgentState` fields, `PopulationSave`, format bump, validators.
- [ ] 2. Arrival on the tick; the faucet; chronicle.
- [ ] 3. `LeaveOutpostGoal` and its actions; departure with the sink, claims released, `CrawlerDeparted`.
- [ ] 4. `OutpostWatch`: residents judged, drifters counted; the live scenario (G1's DoD) in `OutpostScenarioTests`.
- [ ] 5. Soak: bands re-measured with drifters; the greedy player's treasury at 30 min beats the same player's without
      drifters (closed), on each soak seed.
- [ ] 6. Client: dossier tag, chronicle text; Unity: `SwarmSpawner` removes departed crawlers.
- [ ] 7. Golden hashes, references (pinned image), Unity PlayMode and soak; docs; PR.

## Definition of Done (epic §3, G1)
Headless, on the real outpost, nothing injected: within the 15-minute day at least two drifters arrive, buy at the stall
and leave by the Road Gate; `DrifterPurses` and `DrifterDepartures` show in the ledger and conservation holds; the
residents' day still passes on the 23 gate seeds; the fair soak stays in its band; the greedy player ends with more gold
than without drifters.
