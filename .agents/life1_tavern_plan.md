# Epic LIFE-1 plan 2: Wants, fun and the tavern (L1's wants, and L2)

This is a working document (AGENTS.md, Documentation Protocol 6), the second of Epic LIFE-1's three plans. It was written 2026-11-24 at the producer's "Looks good PR, merge and close it out then draft up plan 2", after plan 1 merged as PR #28.
- **The design** is the epic, [`docs/epics/epic-life-1.md`](../docs/epics/epic-life-1.md): its rules A1–A12, its questions Q1–Q11 (standing by exception), §2.5's sketch of wants, fun, venues and the tavern, and the findings plan 1 left open (F3, F11, F12).
- **The decisions** below (D11–D26) are this plan's defaults; the producer answers by exception. Each moves to the epic's §0 when its step lands.
- **The producer's changes to the draft** (2026-11-24, with the go):
  > It is not only okay but encouraged to expand the map beyond the viewport size.
  > Crawlers can absolutely disobey orders, being a guild member does not grant the player authority to override their autonomy.
  - The first moves the tavern off the hub, with the floor growing north (D16, F14).
  - The second ends three places where orders override a crawler's choice (F15): the orders and autonomy steps, O.1 and O.2, come before fun (D25). It amends the epic's A8, plan 1's D9 and Epic 10's D8.

- **Complexity:** Very Complex as a whole: orders that a crawler may set aside, a new goal family that competes with work on the hottest path of cognition, saved fields, a map room, a new building with its economy, art, poses and Gum. Split below so that every step is Basic or Possibly Complex. No step needs supervision: the tavern's building, which the epic marked supervised unless it split below that, is split into seven steps (T.1–T.7), none above Possibly Complex.
- **Scope:** orders weighed as duty, not obeyed by rule (D25); L1's wants and all of L2: fun, activities, venues as map data, Recreate, variety, curiosity (Q8), commitment's value (F3), the tavern room and the tavern as the player's building (Q10), poses and thought icons at venues; the DoDs `AnEveningAtTheTavern` and `AWantComesTrue`.
- **Branch:** `feat/life1-tavern`, from `main` after PR #28's merge. One PR into `main` at the end, with a commit per step.
- **The go:** 2026-11-24, "Proceed", with the two changes above.
- **Estimate:** 15–22 agent days (orders and autonomy 1–2, L2's fun and venues 4–6, the tavern 5–7, wants 3–4, tuning and the DoDs 2–3).
- **The producer's stops:**
  - the look at L2, "an evening at the tavern", after T.7;
  - the soak's bands with the tavern (T.6), and the DoD's amended reading (D24), by exception;
  - the merge.
- **The checks:**
  - golden unchanged through the refactor and every step that only adds data or mechanism (P.1, L2.2–L2.5, T.1, W.1);
  - Epic 10's and 11's live scenarios after O.1 and O.2, which let members set orders aside;
  - each behaviour change re-records golden in its own commit, with the gate seeds' day held;
  - `AnEveningAtTheTavern` and `AWantComesTrue`, live on the real outpost with nothing injected (the tavern player acts only through commands).

## 0. What plan 1 hands over (checked at P.1)
- **The motive model** (`MotiveModel`, `LifeTuning`): priority = base × weights + commitment. Motives 0–9 (None to Leave), room for 16; this plan adds Fun (10) and Want (11). Mood weighs income only (low 0.9, breaking 0.75), never under orders (until O.1). Commitment is built and 0 (F3). The fallback plans the next two goals when the best can't be planned; nothing goes unfound today (F9), so plan 2's goals are its first users.
- **The life record** (`CrawlerLife`, 512 bytes, format 15): thoughts at 0, subjects at 128, and 320 spare bytes from 192, laid out by D6 for wants (192), fun, weariness, the windfall, the intention and curiosity (264) and variety (280). A field reads 0 as "none", so a plan 1 save loads into plan 2 unchanged. No format raise in this plan.
- **Thoughts and mood:** 21 thought rows thought at 15 sites; mood is the need terms plus live thoughts; the dossier's Mind and Today read them. `ThoughtNamesTests` lists every site.
- **The day as measured** (`docs/life-model.md` §3.1): rest 9.8 h, work 8.4 h (walks included), the hearth 3.1 h, wandering and idling 0.1 h. Every pair of residents meets every day, most at the hearth.
  - Leisure must win hours from `EarnIncome`, not from idling (F11). `EarnIncome` = max(5, 25 + the purse's need (up to 20) − hunger × 0.2) × greed × diligence, valid below the comfortable purse (80g): about 25–45 for a neutral crawler. `GatherAtAnchor` = (max(0, 75 − social) + 15, +5 with a friend) × sociability. `Rest` = 100 − energy.
  - A hurt crawler heals in its bunk; a low mood that pulled company sent it to the hearth instead (F12). Recreate must never outbid eating, sleep or healing.
- **The hearth** is company's: `GatherAtAnchor` walks there (`TravelToAnchor`, `RelaxAtAnchor`), and the relax's completion gives +20 social and a little energy (`AgentConsequenceService`).
- **The stations** (`WorkshopService`, `WorkshopRules`, `StationState`): three kinds on the map's sites (forge, still, drill yard), built by `BuildStationCommand` (the `Construction` sink), worked by an artisan hired by `HireArtisanCommand` on its shifts (`ShiftBlocks`: six blocks of four hours, block 3 is 18:00–22:00, block 4 22:00–02:00). The drill yard has no recipes and no artisan. Six places switch on a station's kind: `WorkshopTuning.CostOf`, `WorkshopRules` (the artisan check, the kind's word), `StationModel` (takes work, name, description) and `ArtKeys.Station` (F13).
- **The stall:** 29 goods in a table of 32 (`TradeStallData.GoodsCapacity`), so two more fit inside the format. Prices by `UpdateStallPricesCommand`; the caravan's supplies by `SupplyTuning`'s caravan terms, paid into wholesale sinks (manuals' `ManualWholesale` is the latest).
- **The map** (`Floor1Layout`): 14 sectors on 64 × 42 units, kept to what a 720-row window shows whole, which the producer lifted on 2026-11-24 (F14). Every room beside the hub is taken: the Garden north, the Ore Quarry west, the Herb Grove east, the Bunkhouse south. The Garden (x −8 to 8, y 6 to 18) opens only onto the hub, between the Deep Seam and the Overgrowth; the beetles' reach ends at x −15 and the rats' at x 15.
- **The drill** (Epic 11, S3.4) is the precedent for a spell of free time at a place: `DrillGoal`, the fact `Drilled` (never true in a built state), `DrillAction` and `DrillIntent`, which walks there and spends a game hour.
- **Orders override a crawler's choice in three places** (checked while revising, F15):
  - **An expedition's orders are judged once,** at dispatch (Epic 10's D8, `AgentCognitionService.EvaluateGroupDirective`, keyed on the directive's issued tick). After that `ComplyDirectiveGoal` is valid whatever the member's compliance, at 120 (`ExpeditionPriority`), above everything but the reflexes, rescue and hunger from 70.
  - **A shift's orders** are judged at each block (the crew's directive is issued per block) and again by compliance at every choice; complied with, they stand at 120 too.
  - **The motive model** leaves crawlers under orders out of mood's weights (`MotiveModel.UnderOrders`; plan 1's D9, the epic's A8).
  - **What already copes:** a member who refuses stays behind (allegiance −5) or deserts (leaving the guild); the muster waits a bounded time and goes without who isn't there; a party with no one left ends its trip (`GuildService.Expeditions`). An artisan who deserts its shift's order quits the station.
- **The camera** already shows a map larger than the window: it frames at 1:1 on the map's centre and pans (`CameraRig`; the isometric outpost is wider than a 1280 × 720 window).
- **The planner:** 48 of 64 facts.

## 1. Decisions proposed (defaults; the producer answers by exception)
- **D11, fun** (the epic's §2.5; A3, A11):
  - **Held as boredom** in the life record at 264 (a float, 0 to 100); fun is 100 minus it. Stored the other way round, a plan 1 save would load every crawler bored to the bone; this way it loads entertained, as A3 asks ("0 is none").
  - **Its pace:** boredom rises 3 a game hour awake (`DayTuning.BoredomPerHour`), never asleep, and falls by what activities fill. The hearth fills fun too while a crawler gathers there (the fireside's rate, D12), so evenings at the hearth count.
  - **Its urgency:** `Urgency.Of(Fun)` = 100 − fun.
  - **Its mood term:** Bored (fun 25 or less, −5), in `MoodRules`' terms; Comfortable needs not bored.
- **D12, activities** (A4, A6): `ActivityTable`, a row per `Activity` (a byte, never reused): the player's words, fun filled a game hour (and social or hunger, if any), how long a spell lasts in game minutes, its cost (an item served, or none), its variety group, the company it needs (none, or 2–4 at the venue), the personality trait that leans toward it, the pose it is drawn in, and the thought it leaves. Starting rows (tuned at L2.6):

  | Activity | Venue | Fun / h | Also | Lasts | Cost | Group | Company | Leans | Thought |
  | :--- | :--- | ---: | :--- | ---: | :--- | :--- | :--- | :--- | :--- |
  | By the fire | the hearth | +6 | (`GatherAtAnchor`'s own +20 social) | — | — | Fireside | — | sociability | (today's good talk) |
  | A walk | the Garden | +15 | | 45 min | — | Outdoors | — | low sociability | A walk in the Garden +2 |
  | Browsing | the stall | +10 | | 30 min | — | Market | — | greed | Browsed the market +1 |
  | Sparring | the guildhall yard (members) | +20 | social +5 | 45 min | — | Games | 2 | risk tolerance | A good bout +3 |
  | An ale | the tavern | +20 | social +5 | 45 min | an ale | Drink | — | sociability | An ale at the tavern +3 |
  | A supper | the tavern | +10 | hunger −40 | 45 min | a stew | Meal | — | — | A supper at the tavern +4 |
  | Dice | the tavern | +30 | social +10 | 60 min | — | Games | 2–4 | risk tolerance | A game of dice +4 |

  - The fireside is `GatherAtAnchor`'s, not Recreate's: the hearth stays company's goal (precedent kept), and its relax fills fun at the row's rate. Recreate offers the other venues.
  - Dice and sparring need another crawler at the activity: alone at the venue, a crawler takes the venue's solo activity or none.
  - Dice carries no stakes: wagers wait for LIFE-2's social facts.
- **D13, venues as map data** (A6): `VenueDefinition` in `MapDefinition`, as stations and lairs are: a key, its sector, its position and radius, its seats (positions), its hours (a `ShiftBlocks` mask), the activities it offers, who may come (anyone, or guild members), and the station it needs built and kept, if any.
  - **Floor 1's:** the hearth, the stall, the Garden (at all hours: D26), the guildhall yard (members; it needs the drill yard built), and the tavern (T.2).
  - **Open:** a venue is open in its hours, its station built, and its keeper at the counter on shift. The main thread computes which venues are open each tick, and the snapshot carries the mask (`OpenVenueMask`), as it carries `BuiltMask`.
  - **No leisure in danger:** `VenueValidator` fails a venue in a hazard sector or within a lair's reach (`LairReach`), as the epic requires.
  - **Seats aren't claimed:** a seat is free when no crawler stands within 0.4 of it in the snapshot. No contention state, nothing saved; a full venue scores 0.
- **D14, Recreate** (A1, A6, A12; F11, F12):
  - **The goal:** `RecreateGoal` (Fun, Leisure). Base = `Urgency.Of(Fun)` × the best activity's opportunity (`Leisure.Score`, D15).
  - **Its validity:** invalid when hungry (60 or more), tired (under `Considerations.TiredEnergy`) or hurt (under `Vitality.TreatWoundsBelow`). So Recreate never outbids eating, sleep or healing (F12). It stays valid under orders and competes with duty through the motive model (D25): a content member keeps to its orders, which stand at 120, and a bored, unhappy one may not.
  - **Planning:** one fact, `Recreated` (48: never true in a built state, as `Drilled`); one action, `RecreateAction`, whose intent carries the venue, the activity, the seat and the path (49 of 64 facts).
  - **The handler:** walks to the seat, pays the activity's cost as it begins (D19), fills fun (and social or hunger) over its duration on the day's pace, and thinks the activity's thought at its end. Struck, hungry or ordered away, it stops as any intent does.
- **D15, the leisure score** (A6; Q8): `Leisure.Score(context, venue, activity)` = the activity's fill share × the trait's lean × variety × distance × friends present × (open, a seat free, its cost affordable and in stock), in [0, 1]. Pure, allocation-free, and the same on both threads.
  - **Variety** (life record, 280): a byte a variety group, "had enough", raised 64 by each spell and eased 64 a game day. The score is multiplied by 1 − 0.6 × had-enough / 255.
  - **Curiosity** (Q8; life record, 277): a sixth trait, drawn at spawn on its own salt (`SimRandomSalt.Curiosity`), so no earlier draw moves; 0 reads as unset, a neutral 50. A curious crawler weighs variety more and leans toward venues it hasn't tried (life record, 278: a bit a venue).
  - **Friends present:** ×1.25 when a warm or trusted friend is at the venue (the affinity map, as `GatherAtAnchor` reads it).
- **D16, the tavern room** (Q10; F14, the producer's change): the tavern takes the Garden's room, directly off the hub (x −8 to 8, y 6 to 18, the hub's size), and the Garden moves one room north, to a new room at y 18 to 30. The floor grows north by 12 units, past what a 720-row window shows whole, as the producer encourages.
  - **Doors:** the hub's door north (x −3 to 3) now opens into the tavern; the tavern's door to the Garden sits at its east end, so the way to the Garden skirts the tables. The Garden still opens onto nothing else.
  - **Inside:** the counter (the tavern's station site, `StationKind.Tavern`) and tables with eight seats (its venue). A new sector, the Tavern (id 15, a facility); the Garden keeps its id and moves with its four plots, 12 units further from the stall.
  - **Unmoved:** the frontier row, its lairs and nodes, the Old Stair. The tavern is at least seven units outside the beetles' and the rats' reach.
  - **The walk** from the hearth is about 16 units, some 8 game minutes.
  - **Alternatives:** past the Ore Quarry, in the rock under the Old Stair (the first draft: no room moves, but a 28-unit walk); or a band inserted south of the hub (the whole lower wing moves, with the bunks, the workshops and the guildhall).
- **D17, station kinds as a table** (A4; the epic's "every new system is a table"; F13): `StationTable`, a row per `StationKind`: its words, description, whether it takes an artisan, whether it has recipes, whether it is a venue, and its art key. It replaces the six kind switches, behaviour-neutral, so the tavern is a row. This is the cleanup the project's "refactor and reevaluate" rule asks for: without it the tavern adds a seventh switch in each of six files.
- **D18, the tavern as the player's building** (Q10):
  - **Built** by `BuildStationCommand` at the tavern's site, for `WorkshopTuning.TavernCost` (200g, the drill yard's price; the forge and the still cost 300g), into the `Construction` sink.
  - **Kept** by a keeper hired by `HireArtisanCommand` on an artisan's contract and wage. The keeper's shifts are the tavern's opening hours, set by the player in the station window (D26: while the outpost has no night, the hours that suit are where residents are free). The keeper's shift directive takes it to the counter, as an artisan to its station (Epic 8's compliance).
  - **No recipes and no practice:** a keeper's skill waits (a new skill would move every seed's draw).
- **D19, the menu: ale and stew** (Q10):
  - **Two items** by "Adding or removing an item": ale (a consume effect on fun) and stew (hunger −40, fun), in a new group, `ItemCategory.Served`: served at a venue, never bought to carry, never sold at the hub.
  - **Supplied** by the caravan into the stall's stock (12 ale, 8 stew a visit, at 2g and 4g wholesale, into a `TavernWholesale` sink), and **priced** through the stall's goods and `UpdateStallPricesCommand` (ale 4g, stew 10g to start; rations are 15g).
  - **Served** at the tavern: a crawler starting an ale or a supper there buys it from the stall's stock at the stall's price, its gold to the treasury through the ledger as any sale, and consumes it on the spot.
  - **Its economy** (to `docs/economy-model.md`): build (Construction), the keeper's wage (as an artisan's), wholesale (`TavernWholesale`), takings (sales). The soak gains a `tavern` profile (T.6).
- **D20, wants** (L1; the epic's §2.5):
  - **The table:** `WantTable`, a row per `Want` (a `ushort`, never reused): its trigger, its target kind (an activity, a venue or a friend), its lifetime in game days, its strength, the thought it leaves fulfilled and the one it leaves expired. Starting rows:

    | Want | Arises when | Target | Lasts | Strength | Fulfilled | Expired |
    | :--- | :--- | :--- | ---: | ---: | :--- | :--- |
    | A tavern supper | waking with half the comfortable purse or more, the tavern built | a supper | 1 d | 30 | Had my tavern supper +5 | Missed my supper −2 |
    | A night of dice | waking with fun under 50, sociable | dice | 1 d | 25 | A night of dice +5 | No dice tonight −2 |
    | A quiet walk | waking with fun under 50, not sociable | a walk | 1 d | 20 | A quiet walk +3 | No time to myself −1 |
    | To see {0} | a warm friend not met for a game day | the friend | 2 d | 25 | Saw {0} +4 | Missed {0} −3 |
    | Somewhere new | curious, a venue not yet tried | the venue | 3 d | 20 | Somewhere new +4 | (none) |

  - **Held:** three slots of 24 bytes in the life record at 192: the want (2), flags (1), spare (1), expires at (4), its target (16: a friend's `EntityId`, or an activity and a venue in its first bytes).
  - **Arising:** on waking (one roll a day on its own salt, `SimRandomSalt.Wants`) and at its events, on the main thread through one entry (`AgentState.Want`), as thoughts are thought.
  - **Pursued** by `FulfilWantGoal` (Want; Leisure, or Social for a friend). Base = the want's strength × `Leisure.Score` for its target (or a friend's reach), so a want pulls toward one venue or one crawler. One fact, `WantMet` (49, never true in a built state; 50 of 64), and `FulfilWantAction`, which builds a `RecreateIntent` for an activity or venue, or a walk to the friend for one.
  - **Ending:** fulfilled, its thought and the slot freed; past its lifetime, expired lazily when the record is written, with its small bad thought.
  - **A good** as a want's target (§2.5) waits for LIFE-2's personal goods and taste: today's goods are gear, which residents already buy by their stats.
- **D21, mood's pull on leisure** (F12, resolved here): a low mood weighs Fun 1.25 and a breaking one 1.5; company stays 1. Safe now, because Recreate is invalid while hurt, hungry or tired (D14). It reaches crawlers under orders as anyone (D25).
- **D22, commitment** (F3): measured with Recreate in (flips among leisure goals at plans' ends). If they pass 2 a resident-day, commitment is set (5 to start), with golden re-recorded in its own commit; otherwise it stays 0, and F3 closes with the measurement.
- **D23, what the player sees** (A10):
  - **The tavern on the map:** the room, its counter and tables; a plot until built (`prop/tavern`, `prop/station-plot`).
  - **In the dashboard:** the tavern in the station window (build, hire a keeper, the evening shift); ale and stew in the stall window's prices.
  - **At venues:** a seated pose (`AgentVisualState.Seated`), and a thought icon over a crawler at an activity (a mug, a bowl, dice, a leaf, a purse), carried by the frames (`EntityView.Activity`).
  - **The dossier:** Mind lists wants with their time left ("A tavern supper · 9 h left"); Today tells evenings through the activities' and wants' thoughts.
- **D24, the DoDs' readings** (F11): `AnEveningAtTheTavern` keeps the epic's outcomes but one. "Ambient wandering falls by half" can't be read: wandering is already 0.1 h a resident-day (F11). In its place: residents' leisure at venues other than the hearth rises from 0.2 h to at least an hour a resident-day, with work's hours measured beside it (D25's band is plan 3's).

- **D26, plan 2 doesn't keep the clock** (F16, found at P.1): residents sleep when tired and wake when rested, so the outpost sleeps from mid-afternoon to before midnight and works through the small hours. An evening tavern would open while half of them sleep.
  - **Venues' hours** stay only where a place has them for its own reasons: the Garden is open at all hours (D13's 06:00–22:00 dropped), the yard with the drill yard, the tavern while its keeper is on shift.
  - **The tavern's hours** are its keeper's shifts, which the player sets; the station window offers any two blocks, and the tavern player picks the blocks when most residents are awake and off work (measured at T.6).
  - **The DoD** reads leisure at venues across the day, not the evening (D24).
  - **Plan 3's routine** (the epic's bedtime pull and chronotypes) brings sleep to the night; then the evening shift is the evening, and `AnEveningAtTheTavern` is re-run at LG1 by the clock.
  - **Alternative:** bring the bedtime pull forward into plan 2, ahead of plan 3's chronotypes. It would move every crawler's day before fun is tuned against it.
- **D25, orders are a motive, not a rule** (the producer's change; F15):
  - **Mood reaches crawlers under orders** as anyone: `MotiveModel.UnderOrders` goes, and duty gains mood's weights (low 0.85, breaking 0.6 to start, measured at O.1). Plan 1's D9 ("nothing under orders") and the epic's A8 are amended.
  - **An expedition's orders are judged at every stage,** as a shift's are at every block: the guild issues its directive afresh when the stage changes (muster, march, work or fight, return), so each member judges it again by compliance. A member who no longer complies stays behind or deserts, by the path dispatch uses. `ComplyDirectiveGoal` no longer holds a member to a trip it judged once. Epic 10's D8 is amended.
  - **Duty keeps its weight:** orders stand at 120 when complied with, so a content, loyal member keeps to them; frustration, low trust or loyalty (compliance) and a low mood (its weight) are what set them aside. Leisure and wants stay valid under orders and compete through the model.
  - **What the guild does about it** is today's: a member who stays behind loses allegiance; one who deserts leaves. The muster's and the return's time limits keep a trip from waiting for someone who isn't coming.
  - **Plan 3's routine and intention** (the epic's A1 keeps orders out of their weights): whether a day off can weigh against a shift is plan 3's to decide under this rule.

## 2. The milestones, phased

### P. The hand-over (half a day)
**Complexity:** Basic.
- [x] **P.1 The hand-over checked, and the evening measured** (done 2026-11-24: §0 matches the code but for F15, recorded at the revision; `docs/life-model.md` §5.1; **F16: the outpost has no night**, so D26) [Basic: docs and a measurement's extra columns; golden unchanged]
  - Check §0 against the code, and record any difference as a finding (the epic's §7, F13 onward).
  - `MeasureACrawlersDay` gains hours by the hour of day (where residents are from 18:00 to 02:00) and who meets whom at the hearth, as the baseline for L2.6 and the DoDs.
  - `docs/life-model.md` gains §5, "Fun and the evening", with the baseline.

### O. Orders and autonomy (1–2 days)
**Complexity:** Possibly Complex: two behaviour changes in cognition and the guild, each with golden re-recorded in its own commit. Neither generalises the expedition's machine: it already handles a member who stays behind or deserts, and only *when* a member judges changes.
- [x] **O.1 Mood reaches crawlers under orders** (D25) (done 2026-11-24: `docs/life-model.md` §5.2; almost nothing moves, golden unchanged, core 350/350. **Deviation:** no golden re-record was needed) [Possibly Complex: a behaviour change on the hot path; golden re-recorded in its own commit]
  - `MotiveModel.UnderOrders` removed; duty's mood weights in `LifeTuning.Default`; `MotiveModelValidator` gains a crawler under orders whose low mood weighs its duty.
  - **The checks:** F12's four DoDs (`ADraughtKeepsAPartyWorking`, `EveryScenarioPlaysTogether`, `PassionBurnsBright`, `ACallingEmerges`) and every Epic 10 and 11 live scenario on their seeds; the gate seeds' day holds; orders refused a day measured before and after (`docs/life-model.md` §3.3).
- [x] **O.2 An expedition's orders judged at every stage** (D25) (done 2026-11-24: `docs/life-model.md` §5.3; nothing moves on the gate seeds, golden unchanged, core 351/351. **Deviation:** no golden re-record was needed; the validator is a live test in `ExpeditionTests`, `AMemberWhoSoursOnTheWayStaysBehindAtTheNextStage`) [Possibly Complex: a behaviour change in the guild's stages and cognition's directive evaluation; golden re-recorded in its own commit]
  - The guild re-issues the party's directive at each stage change; `ComplyDirectiveGoal` judges an expedition by compliance as it judges a shift; the "judged once" short-circuit goes.
  - **The checks:** a validator that a frustrated member stays behind at the march and the party goes on without it; the guild's and the clears' live scenarios (`GuildScenarioTests`, `ClearScenarioTests`, `GateScenarioTests`, `PartThreeTogetherTests`, `EpicElevenTogetherTests`) on their seeds; members who stay behind mid-trip counted on the gate seeds with the guild player.
  - AGENTS.md: the guild's paragraph says orders are judged at each stage.

### L2a. Fun and its venues (4–6 days)
**Complexity:** Very Complex as a phase by bundling (saved fields, map data, a new goal on the hot path, a behaviour change). Split into seven steps, each Basic or Possibly Complex. Golden is unchanged through L2.2–L2.5; L2.1 and L2.6 re-record it in their own commits.
- [x] **L2.1 Fun** (D11) (done 2026-11-24: `docs/life-model.md` §5.4; behaviour unchanged, golden re-recorded for coverage only in its own commit. **Deviation:** the Bored mood term moves to L2.6, with Recreate, so mood never pays for boredom before a crawler can answer it) [Possibly Complex: a saved field, a mood term cognition reads; golden re-recorded in its own commit if mood's term moves a choice]
  - Boredom in the life record, its pace in `DayTuning`, fun filled at the hearth's relax, `Urgency.Of(Fun)`; the Bored term at L2.6.
  - **The checks:** the round trip in the save validators; `MoodValidator` with the new term; a plan 1 save loads entertained; the gate seeds' day holds.
- [x] **L2.2 The activity table** (D12) (done 2026-11-24: `docs/life-model.md` §5.5; golden unchanged, core 354/354. The fireside's fill moved from `DayTuning` to its row; the costs of ale and supper wait for T.3's items) [Basic: a table with its validator and names test, as `ThoughtTable`; nothing reads it yet]
  - `Activity`, `ActivityDefinition`, `ActivityTable`; the activities' thoughts as `ThoughtTable` rows (thought from L2.5).
  - `ActivityValidator` (fills, durations, company, groups within bounds); `ActivityNamesTests` (code names an activity only in the table and the venues' data).
  - AGENTS.md: "Adding an activity".
- [x] **L2.3 Venues as map data** (D13) (done 2026-11-24: `docs/life-model.md` §5.6; golden unchanged, core 355/355) [Possibly Complex: map data and a mask the snapshot carries, both read by cognition; golden unchanged]
  - `VenueDefinition` and the builder's `Venue`; Floor 1's hearth, stall, Garden and guildhall yard (the tavern's comes with T.2).
  - `OpenVenueMask` computed on the main thread and carried by the snapshot.
  - `VenueValidator`: in no hazard sector and no lair's reach; seats inside the sector; every activity offered in the table.
  - AGENTS.md: "Adding a venue".
- [x] **L2.4 The leisure score** (D15, without variety's and curiosity's fields: L2.7) (done 2026-11-24: `docs/life-model.md` §5.7; golden unchanged. **Deviation:** the validator is `LeisureTests` in `Simpiens.Core.Tests`, on the live outpost's snapshot, not a `LeisureValidator`) [Possibly Complex: cognition; pure, allocation-free]
  - `Leisure.Score` and `Leisure.TryChoose` (the best venue and activity a crawler knows of).
  - `LeisureValidator`: each factor moves the score its way; a closed, full or unaffordable venue scores 0; the same inputs score the same; nothing allocates.
- [x] **L2.5 The Recreate intent** (D14's handler) (done 2026-11-24: golden unchanged; a walk strolls among the venue's seats at half pace. **Deviation:** "struck, it stops" is the runtime's pre-emption of any intent, already proved, so `RecreateValidator` checks the handler's refusals instead) [Possibly Complex: an intent handler on the main thread; a mechanism with no goal yet, so golden is unchanged]
  - `RecreateIntent` and `RecreateIntentHandler`: walk to the seat, fill over the duration on the day's pace, think the thought at the end; the cost waits for T.5.
  - **The check:** `RecreateValidator`, with a forced intent: a crawler walks to the Garden, walks 45 game minutes, comes back with its fun up and the thought held; struck, it stops.
- [x] **L2.6 Recreate** (D14) (done 2026-11-24: `docs/life-model.md` §5.8. Boredom set to 2 a game hour (D11 amended: at 3 leisure took 1.5 h a day); Recreate 0.7 h a resident-day; about 14% less fighting on 60 seeds. 13 live scenarios re-picked in their own commit, with `ScenarioSeed` to search; `PassionBurnsBright` keeps its passion and gives up its harder-foes half (F17); golden re-recorded in its own commit) [Possibly Complex: a GOAP goal, action and fact; a behaviour change, golden re-recorded in its own commit]
  - `RecreateGoal`, `RecreateAction`, the fact `Recreated` (49 of 64), and their places in `OutpostGoapDomain`; `MotiveValidator`'s and `docs/life-model.md` §2.1's rows; the Bored mood term (D11, moved from L2.1) in `MoodRules` and `MoodValidator`.
  - **Tuning:** boredom's pace and the fills, from the evening's measurement, so that free evenings go to venues and work keeps most of its hours. The numbers go to `docs/life-model.md` §5.
  - **The checks:** the gate seeds' day holds; every DoD keeps its seed or is re-picked with the reason (F12's four first); the soak's bands measured, any move to the producer by exception.
- [ ] **L2.7 Variety and curiosity** (D15) [Possibly Complex: two saved fields and a draw on its own salt; golden re-recorded in its own commit]
  - Variety's eight bytes and their easing; curiosity's draw at spawn and its tried venues; both read by `Leisure.Score`.
  - **The checks:** the round trip; `LeisureValidator` gains variety and curiosity; a seed's earlier draws (looks, skills, techniques) are unchanged by the new salt.

### L2b. The tavern (5–7 days)
**Complexity:** Very Complex as a phase by bundling (a refactor, a map room, a building, items, the economy, art and Gum). Split into seven steps, each Basic or Possibly Complex.
- [x] **T.1 Station kinds as a table** (D17; F13) (done 2026-11-24: `StationTable` in `Simulation/Items/`, its row class `StationKindDefinition` since the map's sites are `StationDefinition`; costs as a `StationCost` line priced in `WorkshopTuning`; the art keys built once from the rows; golden, core and the client in the pinned image unchanged) [Possibly Complex: across core and presentation; behaviour-neutral, golden and renders unchanged]
  - `StationTable` replaces the six switches; `StationsValidator` checks the table.
  - AGENTS.md: "Adding a station kind".
- [x] **T.2 The tavern room** (D16) (done 2026-11-24: the floor 64 × 54; the Garden a room north, the tavern's room sector 15, a facility. **Deviations:** the tavern's counter and venue moved to T.4, with its station kind, so no venue serves before a tavern stands; the framing tests restated for a floor taller than the window; renders recorded once after T.4. Golden re-recorded; 8 scenarios re-picked, 2 still searching) [Possibly Complex: the map grows and a room moves, with the nav grid; golden re-recorded in its own commit; renders re-recorded in the pinned image after a look]
  - The Garden moved north with its plots and label; the tavern in its room, with its walls, doors, station site, venue (seats at tables) and label; the floor's bounds to y 30; `Floor1Layout`'s comment on the window's limit replaced with the producer's rule.
  - **The checks:** `MapDefinitionValidator`'s Floor 1 checks (doors leave four walkable cells, sectors don't overlap, the Garden reached only through the tavern); `VenueValidator`; `AtlasLayoutTests` and the fog's sheet for the larger floor; the gate seeds' day holds (nobody starves with the Garden further off); look at the outpost renders in both view modes, then re-record them in the pinned image.
- [x] **T.3 Ale and stew** (D19's items) (done 2026-11-24: `ItemCategory.Served`; the tavern's `StationKind` and row (200g, `StationCost.Tavern`) moved here from T.4 so the caravan can bring the menu only while a tavern stands (`CaravanTerms.ForStation`, `ISupplyHost.StationStands`); a mug and a bowl, new shapes; `prop/tavern` in both view modes. Golden unchanged; art pins re-recorded in their own commit) [Possibly Complex: a new item group that residents' purchases must skip; otherwise "Adding or removing an item"]
  - Their `ItemType` values, catalogue rows (`ItemCategory.Served`), terms in the stall's goods and the caravan's supplies (`TavernWholesale`), and their looks in `ItemArt`.
  - **The checks:** `ItemSystemProofTests`' pattern; a validator that no resident buys a served good to carry or eats one from the pack; the art sheet looked at and the art pins re-recorded in their own commit; golden re-recorded if the caravan's supplies move the ledger.
- [x] **T.4 The tavern as a building** (D18) (done 2026-11-24: the counter's site and the tavern's venue on the map (two tables of four; ale, supper, dice); `WorkshopService.KeptMask`, and `VenueRules` open a keeper's station's venue only while kept; a station with no recipes keeps its artisan on shift without an order, standing, not hammering. Golden unchanged; the station kind and row came with T.3) [Possibly Complex: a station kind with a nearby precedent; the commands' rules]
  - Its `StationTable` row (an artisan, no recipes, a venue); `TavernCost`; the keeper's evening shift; the venue open while the keeper is at the counter on shift.
  - **The checks:** `WorkshopValidator` gains the tavern (built, hired, refused with its reasons); a headless check that the venue opens and closes with the keeper's shift.
- [ ] **T.5 Serving and the takings** (D19) [Possibly Complex: the ledger and the stall's stock; a behaviour change once the tavern is built]
  - The handler pays an activity's cost at the tavern: a sale from the stall's stock at its price, through the ledger; the item consumed on the spot.
  - **The checks:** the ledger balances (`OutpostWatch`'s conservation); an out-of-stock or unaffordable activity is never chosen (`LeisureValidator`); the economy validators.
- [ ] **T.6 The tavern player and the soak** (D18, D19) [Possibly Complex: measuring and setting a soak band]
  - `TavernPlayer` in `Simpiens.Hosting`: builds the tavern, hires the best-liked free resident as its keeper, gives it the evening shift and sets the menu's prices, acting only through commands (`WorkshopPlayer`'s pattern).
  - The soak's `tavern` profile: takings, wholesale, the keeper's wage, residents' work hours and purses. Its band to the producer by exception; the numbers to `docs/economy-model.md`.
- [ ] **T.7 The tavern on screen** (D23) [Possibly Complex: art, a pose, the scene and Gum; the art pins and renders re-recorded after a look]
  - The tavern's art (`prop/tavern`: the counter and tables, both view modes), its row in the station window, ale and stew in the stall window.
  - The seated pose (`BodyKit`, `HeadAnchors`, `CrawlerAppearance`); thought icons over crawlers at an activity (`EntityView.Activity`, a badge drawn by the scene).
  - **The checks:** `ArtCompletenessTests`; `ThrashingTests` (no flicker between activities); the window-size tests; look at the art sheet and the renders (a new one of an evening at the tavern), then re-record the art pins and the renders in the pinned image, each in its own commit.
  - **The producer's look** at L2 follows this step.

### L1w. Wants (3–4 days)
**Complexity:** Very Complex as a phase by bundling (saved fields, a draw, a goal, the client). Split into four steps, each Basic or Possibly Complex.
- [ ] **W.1 The want table** (D20) [Basic: a table with its validator and names test; nothing reads it yet]
  - `Want`, `WantDefinition`, `WantTable`; the wants' thoughts as `ThoughtTable` rows.
  - `WantsValidator`; `WantNamesTests`. AGENTS.md: "Adding a want".
- [ ] **W.2 Wants arise and end** (D20) [Possibly Complex: saved fields, a draw on its own salt, events on the main thread; golden re-recorded in its own commit]
  - The three slots, `AgentState.Want`, the waking roll and the event triggers, lazy expiry with its thought; `OutpostWatch` tallies wants arisen, fulfilled and expired.
  - **The checks:** the round trip; a validator of each trigger and of expiry.
- [ ] **W.3 Fulfilling a want** (D20) [Possibly Complex: a GOAP goal, action and fact; a behaviour change, golden re-recorded in its own commit]
  - `FulfilWantGoal`, `FulfilWantAction`, the fact `WantMet` (50 of 64); fulfilment at the activity's end, or at meeting the friend.
  - **The checks:** `MotiveValidator`'s row; a validator that a want pulls its crawler to its venue over a nearer one; the gate seeds' day holds.
- [ ] **W.4 Wants on screen** (D23) [Possibly Complex: presentation models and Gum; renders re-recorded]
  - The Mind tab lists wants with their time left; `MindModel` and `LifeTexts` gain them.
  - **The checks:** `LifeModelsTests`; the Mind tab's render re-recorded in the pinned image.

### L2c. Tuning and the DoDs (2–3 days)
**Complexity:** Possibly Complex: behaviour changes and live scenarios.
- [ ] **L2.8 Mood's pull on leisure, and commitment** (D21, D22; F3, F12) [Possibly Complex: two behaviour changes, each with golden re-recorded in its own commit]
  - Fun's mood weights; then the flips measured and commitment set, or F3 closed at 0.
  - **The checks:** F12's four DoDs pass on their seeds; the gate seeds' day holds; `ThrashingTests`.
- [ ] **D.1 The DoD: `AnEveningAtTheTavern`** (D24) [Possibly Complex: a live scenario]
  - On the gate seeds' day, with the tavern player and nothing injected:
    - at least half the residents spend an hour at a venue other than the hearth, and leisure at venues reaches an hour a resident-day;
    - the tavern serves customers every evening its keeper keeps;
    - nobody starves;
    - the tavern's takings, wholesale and the keeper's wage are in the ledger, which balances.
  - A save in the evening loads with crawlers at the tavern carrying on.
- [ ] **D.2 The DoD: `AWantComesTrue`** [Possibly Complex: a live scenario]
  - On the gate seeds, with the tavern player and nothing injected: wants arise, are pursued at their venue or friend and fulfilled, and leave their thoughts; most wants that arise are fulfilled before they expire.
  - A want held across a save is fulfilled after the load.

### Plan 2's gate and close-out (1 day)
**Complexity:** Possibly Complex: measuring.
- [ ] **C.1 The gate** [Possibly Complex: measuring]
  - **The tests:** every test, the golden check, the gate seeds' day, and the client's tests in the pinned image.
  - **The soak:** every profile's band on the final build, the tavern's beside them.
  - **Measurements:** P.1's evening and L1.3's moods, re-run.
  - **Cost:** `PopulationScalingProbe` at 12, 24 and 48 crawlers, beside `main` at the branch's start, in a quiet sitting (leisure scoring runs for every crawler on every choice: F10's 600 choices a resident-day).
- [ ] **C.2 Close-out** [Basic: written from lists this plan holds]
  - **The docs, in order:**
    1. the roadmap first, then the epic's status and §4;
    2. decisions to the epic's §0, findings to its §7, numbers to `docs/life-model.md` and `docs/economy-model.md`;
    3. the focus file, and a summary in `.agents/architecture_history.md`.
  - **Delete this plan.**
  - **Open the PR** into `main`.

## 3. What plan 3 takes
Drafted when this plan merges, from the epic's §2.4 and §2.5.
- **L3, rhythm:** routines and chronotypes, the weights by family and hour, satiation (the purse against its target, plus what wants save for), weariness, the day's intention, rest days in safe zones, Q11's switch, and the DoD `ADayOffAfterAWindfall`.
- **Duty under the routine:** whether the day's intention or a routine's hours weigh against a shift or a trip, under D25.
- **LG1, the gate:** `LifeOneTogether`, the soak's bands with days off and the tavern, scaling, and the producer's play session.

## 4. Risks (this plan's; the epic's §6 holds the rest)
- **Leisure eats the economy.** Evenings at venues take hours from `EarnIncome` (F11), and the tavern takes gold. Crawlers earn what they spend, so they may simply work less and eat less well. L2.6 tunes boredom and fills against the evening's baseline; T.6 measures purses and work hours with the tavern, and the soak's bands go to the producer. Plan 3's satiation is the epic's lever on work hours; this plan doesn't pull it.
- **Recreate outbids what keeps a crawler alive.** D14's validity keeps it out of hunger, tiredness and hurt, and L2.6 re-runs F12's four DoDs first. Against orders it competes by design (D25).
- **Members walk off their orders** (D25). Epic 10's and 11's scenarios assume a party that stays together. O.1 and O.2 re-run them first; if one fails because a member set its orders aside, its scripted player is checked for a seed or a moment that made the member unhappy, and the measurement goes to the producer before any weight is tuned to keep members obedient.
- **The Garden is further from the stall** (D16): 12 more units each way, through the tavern. It is the outpost's fallback food, eaten 0.2 times a resident-day (§3.2), so starvation is watched on the gate seeds at T.2.
- **Stew undercuts rations** (10g against 15g), and the stall's ration sales fall. The soak's `tavern` profile reads it; stew's price is the player's lever, and its starting price can rise.
- **Crawlers flicker between activities.** Commitment (L2.8), variety (L2.7) and `ThrashingTests`.
- **Leisure scoring costs every choice.** It scores a handful of venues, allocation-free; the gate measures it beside `main`.
- **Planner facts:** this plan takes two (`Recreated`, `WantMet`), to 50 of 64; A12's third (a venue's seat) isn't needed, since seats aren't claimed (D13).
