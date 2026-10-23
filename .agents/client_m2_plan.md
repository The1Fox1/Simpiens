# Epic CLIENT, M2 (the dashboard in Gum, then Unity retired): implementation plan

Working document (AGENTS.md, documentation protocol): deleted when M2 closes, after its findings move to `docs/epics/epic-client.md` and its decisions to `.agents/architecture_history.md`.

- **Goal:** the KNI client plays the whole game Unity plays today. On top of M1's map, it gets every dashboard panel, each working the way Unity's does:
  - the header over the map;
  - the merchant terminal;
  - the bounty board;
  - the Tab dashboard: sectors, residents, events and the crawler dossier;
  - quick save and load;
  - the Dev Map switch, in development builds.

  Then Unity is removed from the repo, in chunks of their own.
- **Branch:** `feat/kni-client`, as the producer asked (2026-10-18): parity comes before the pull request.
- **Authority:** the producer said go on 2026-10-18 (see the end of this plan). The agent carries out M2.0 to M2.7, then stops for the producer's side-by-side with Unity. M2.8 and M2.9 follow only after that sign-off, in a second pull request.
- **Gate (proposal §Roadmap):**
  1. Every panel works in the client, and Unity's view and UI scenarios pass as client tests.
  2. Render references are reviewed for every panel.
  3. The producer signs off a side-by-side look against Unity.
  4. Unity's removal then leaves `dotnet test`, the golden check, and the MonoGame and Android builds green, with every DoD scenario still covered.
- **Baseline:** `feat/kni-client` at `c619de4` (M1 complete). There:
  - `dotnet test` passes 111 of 111 (client 49, core 62);
  - the golden check passes 3 of 3;
  - the MonoGame and Android builds compile.
- **Source of truth for behaviour:** Unity's frozen UI.
  - `FacilityDashboardPresenter`: 1,624 lines, refreshing at 10 Hz.
  - `FacilityDashboard.uxml` and `.uss`: the layout, and the palette (Tailwind slate, sky, amber, emerald, red).
  - `FacilityZoneService`.
  - `ViewToggleController`: Tab.
  - `SwarmSpawner`'s session API.

  The Unity scenarios and validators to port are listed per chunk.
- **Not in M2:**
  - the portrait phone layout and touch (M4);
  - other screen sizes and DPI scaling, beyond a resizable desktop window (M6);
  - the reskin by a designer (the UI style is still to be decided);
  - icons and real art (M7);
  - isometric (M3);
  - the map designer (after M2);
  - speed controls: Unity only shows the speed, and so will the client.

## Research (2026-10-18, a scratch spike outside the repo)
- **Gum.KNI 2026.10.1.1** is the latest stable release (MIT, published 2026-10-01). It works on KNI 4.3.9001:
  - Forms controls are drawn offscreen with SDL's offscreen driver on llvmpipe (button, label, slider, combo box, list box, check box);
  - two captures in a row were byte-identical.
- **The stock Meadow theme has a cost:**
  - it needs the HiDef graphics profile (shader model 3), where KNI defaults to Reach;
  - its shapes come from Apos.Shapes.KNI. NuGet resolves 0.8.1, which works but logs "effect for an older version of KNI". 0.8.6 breaks Gum.Shapes at start-up (`MissingMethodException` on `ShapeBatch..ctor`);
  - it rasterizes its fonts at run time with FreeType, through KernSmith and FreeTypeSharp, which bundles its own natives.
- **Gum's built-in visuals** (V3) need none of that: Reach is enough, the font is a built-in bitmap, and there are no extra packages. Their palette is set through `Styling.ActiveStyle.Colors`.
- **For tests and the map:**
  - `ICursor.WindowOver` names the control under the pointer, which gives the map's "over UI" flag.
  - `FormsUtilities.SetCursor` and `SetKeyboard` take scripted input, so tests can click controls without devices.
- **Packaging:**
  - `GumService` is `MonoGameGum.GumService` in both Gum.KNI and Gum.MonoGame (same version numbers), so the UI code compiles against the MonoGame fallback unchanged.
  - Gum.KNI ships `net8.0` and `net9.0-android35.0` assets, and nothing for iOS (an M5 risk).
- **Start-up crashes:** the bare scratch exe crashed at start-up in 2 of 20 offscreen runs, with or without Gum: a call through a null native function pointer, before the first frame. The client suite ran 10 of 10 clean, on top of dozens of runs during M0 and M1. The suite's stability is measured again in M2.0, with Gum in it.

## Design
- **D1: Gum's built-in visuals in Unity's palette, not the Meadow theme.**
  - This deviates from the proposal, which started on Meadow.
  - Meadow adds HiDef, a pinned Apos.Shapes with a shader warning, and FreeType natives on every platform, and its cottage look is further from Unity's "Facility OS" panels than the plain visuals are.
  - The designer's reskin replaces the colours and visuals later; the panels' code doesn't change.
- **D2: an engine-free dashboard model.**
  - `Simpiens.Presentation` gets the dashboard's logic, ported from the presenter without UI types: header metrics, sector rows, residents, events, the dossier, the merchant view, and the bounty board with its form rules.
  - It turns the player's choices into typed commands. Commands go through `IPlayerInterventionService` only, which keeps the multiplayer rule; save and load go through `ISaveGameService`, between ticks.
  - Gum views in `Simpiens.Client` bind to the model and hold no game logic.
  - The model is tested without a GPU.
- **D3: Unity's layout first.**
  - The header and the merchant and bounty windows sit over the map. The map takes clicks except where UI is.
  - Tab shows the three panels over the map, and they block map clicks, as in Unity.
  - The proposal's docked desktop layout and the portrait phone layout come with M4 and M6, after the side-by-side has passed.
- **D4: "Calm frustration" becomes a typed command.**
  - Unity's dossier writes `AgentState` directly from the UI, a view writing state.
  - The client sends a new `CalmCrawlerCommand` (`InfluenceType.CalmCrawler`), applied by `SimulationManager` on the tick, like the other commands.
  - This is a core change, so it is checked in both runtimes: Unity's PlayMode suite and golden check run too. Unity's presenter keeps its direct write: Unity is frozen.
  - If Unity can't be run here, D4 waits for M2.9, and the client's button stays disabled until then.
- **D5: text.**
  - Gum's built-in font is used.
  - Unity's emoji (💰 🍞 ⛏️ 🌿 📜 ⚖️ 💾 📂) become words, because the font has no emoji. Icons come with art (M7).
  - The map's labels stay the M1 pixel font.
- **D6: saves.**
  - The quick-save slot is `Saves/quicksave.simsave` under the per-user application data folder (`~/.local/share/Simpiens` on Linux, `%LOCALAPPDATA%\Simpiens` on Windows, the app's files folder on Android).
  - Tests use their own temporary folder.
  - The asynchronous save is polled from the game loop, so its outcome is shown on the game thread: KNI has no synchronization context to resume on.
- **D7: the Dev Map only in development builds.** `Simpiens.Desktop` references `Simpiens.Testing.Core` in Debug only, and registers `CanaryPenSession` through a `ClientOptions.ConfigureServices` hook. Release builds never reference the harness, as Unity's player builds didn't.
- **D8: refresh.**
  - The dashboard refreshes at 10 Hz of view time, as Unity did, and only what is visible.
  - Rows are reused, not rebuilt; Unity pooled them too.
  - The frame time is measured again with the UI open.
- **D9: input.**
  - `PointerInput.OverUi` comes from `ICursor.WindowOver` (hit-testing only the HUD's children, as Unity's `picking-mode="Ignore"` did).
  - Keys:
    - Tab toggles the panels;
    - Esc closes an open window first, otherwise it stops following;
    - while a control has focus, the camera ignores the keyboard.

## Chunks (one commit each, only when the gate commands pass)
1. **M2.0 Gum in the client.**
   - Packages through the `XnaFramework` switch: Gum.KNI or Gum.MonoGame 2026.10.1.1, pinned, with no theme.
   - The UI layer:
     - Gum is initialized after the world;
     - each frame draws the world, then the UI;
     - the canvas follows the window.
   - Unity's palette goes into `Styling`.
   - `OverUi` comes from the cursor, and tests get scripted cursor and keyboard input.
   - Checks:
     - the Android and MonoGame builds still compile;
     - the client suite runs 20 times in a row, clean;
     - the frame time is measured again.
   - Tests: a label and a button drawn over the map (a render reference); a scripted click on a button; a map click under a button doesn't select a crawler.
2. **M2.1 The dashboard model (engine-free).**
   - `FacilityZoneService` ported to Presentation.
   - The header metrics: treasury, rations, ore and herb stock, active bounties, tick and speed.
   - Sector rows with occupancy.
   - The residents of a sector, with their state badges and activity text.
   - The sector's last 100 chronicle events, through `TemplateNarrativeFormatter`.
   - The dossier: drives, goal and action, affinities with names.
   - The merchant view, and the bounty board: active contracts, and the form's rules. Scout lists only uncharted rooms. `BountyRules` checks the target. The treasury must cover the reward. Slider ranges are Unity's: prices 1–50, quantity 1–10, reward 10–100, duration 500–3,000 ticks.
   - Tests: Unity's `BountyTerminalValidator` and `FacilityZoneServiceValidator` checks ported; the model on the live outpost.
3. **M2.2 The calm command (core) (D4).**
   - `CalmCrawlerCommand` in the `InfluenceEvent` union, applied on the tick.
   - Tests:
     - a validator for the command;
     - golden hashes unchanged in .NET and in Unity;
     - Unity's PlayMode suite green.
4. **M2.3 The header over the map.**
   - The title and status pill, the metrics, the Merchant, Bounties, Save and Load buttons, the save status, the tick, the speed and the Tab button.
   - It refreshes at 10 Hz while the map shows.
   - Tests: `MapHudScenario` ported (the header updates over the map; the buttons open their windows on the first press; the map stays clickable except under UI); a render reference.
5. **M2.4 The merchant terminal.**
   - Stall status, treasury and stock; three price sliders; Apply sends `UpdateStallPricesCommand`, with feedback; the close button.
   - Tests: the new prices are in the stall on the next tick (live outpost); a render reference.
6. **M2.5 The bounty board.**
   - The active contracts, with a Cancel on each.
   - The post form: type, sector (by name), item and quantity (hidden for Scout), reward and duration.
   - The checks, with the reasons as Unity's (all charted, a bad target, short treasury).
   - Post and Cancel send their commands.
   - Tests:
     - `PlayerScoutScenario` ported: the Definition of Done for the player's view. On the real outpost, through the UI only, with nothing injected, the player posts a Scout for the Overgrowth. A crawler claims it and reports back within the bound. The Overgrowth is charted, its fog lifts, and Scout has nothing left to offer.
     - A Gather bounty posted and cancelled, with its refund.
     - A render reference.
7. **M2.6 The Tab dashboard.**
   - Sectors with occupancy.
   - The selected sector's residents and events.
   - The dossier: drive bars, intent, affinities, and the calm button (D4).
   - Selection syncs both ways with the map's `WorldSelection`: a map click shows the crawler and room here, and a resident clicked here selects it on the map.
   - Tests:
     - selection both ways;
     - calm through the button;
     - the panels block map clicks;
     - a render reference with the dossier open.
8. **M2.7 Saving, the session switch and the parity gate.**
   - Quick save and load (D6), with Unity's messages.
   - A load or respawn closes the windows, drops the selections and rebuilds the lists.
   - The Dev Map switch and its banner in Debug builds (D7).
   - Tests:
     - `QuickSaveButtonsScenario` ported (an empty slot says so; Save writes the tick; Load brings it back), which also stands in for the Unity player's persistence smoke: a save file written and loaded by the client;
     - `SessionSwitchScenario` ported (the outpost comes back structurally identical);
     - `ShroudViewScenario`'s camera part (a followed crawler stays within tolerance at 10x).
   - Gate:
     - the review set (every panel open over the outpost, the Dev Map banner);
     - frame time with the UI open;
     - docs, roadmap first.
   - **Then stop, for the producer's side-by-side with Unity** (person-gated).
9. **M2.8 Unity-only coverage moves to .NET** (only after the producer's sign-off).
   - Every Unity PlayMode scenario without a .NET twin gets one:
     - `BunkRestScenario`, `PriceResponseScenario`, `PenCanaryScenario`;
     - `SessionCaptureScenario` and `SessionRestoreScenario` (live);
     - `ResourceContentionScenario`, `SwarmNavigationScenario` and `DynamicChaosScenario`. For these, the `VisualAnomalyDetector` and the `EdgeCaseInjector` are ported to engine-free code over `WorldEntity`, with the client's `CrawlerAppearance` in place of `AgentView` for state thrashing, together with the detector's validator;
     - `PopulationScalingProbe`, as an explicit test or a `simpiens` command.
   - Dropped, with the reason recorded: `EntityIdCompatibilityValidator`, which proves bit-compatibility with `UnityEngine.GUID` and means nothing without Unity.
   - Proposed new Definition of Done wording (ROADMAP §1B), for the producer: "a scenario in `dotnet/` (headless, or through the client) on the real session, with nothing injected".
10. **M2.9 Unity removed** (its own commit; the producer's sign-off; maybe its own pull request).
    - The core and the test harness move from `Assets/` into `dotnet/` (`git mv`, without `.meta` and `.asmdef` files), and the projects stop globbing from `Assets/`.
    - Deleted:
      - `Assets/`, `ProjectSettings/` and `Packages/`;
      - the Unity scripts (`run-playmode-tests.sh`, `golden-hashes.sh`, `run-economy-soak.sh`, `run-persistence-smoke.sh`, and `update-memorypack.sh`, since .NET gets MemoryPack from NuGet);
      - Unity's `.gitignore` entries.
    - The golden baselines stay: .NET reproduces them.
    - Finding F1 ends, because there is only one runtime.
    - The C# 10 and netstandard2.1 cap stays for this commit, so it is a pure move. Lifting it is a follow-up.
    - Docs:
      - `AGENTS.md` rewritten for a .NET-only repo;
      - the roadmap's rules that name Unity (§1A, §1B), Track B (moved to the client) and Epic CORE's notes;
      - `curentfocus`;
      - the history log.

## Gate commands (before every commit)
```
dotnet build dotnet/Simpiens.slnx -c Release                               # 0 errors
dotnet test  dotnet/Simpiens.slnx -c Release                               # all pass (111 at M2 start, plus new tests)
dotnet run --project dotnet/Simpiens.Host -c Release -- golden check       # 3/3 seeds
dotnet build dotnet/Simpiens.Desktop -c Release -p:XnaFramework=MonoGame   # 0 errors
scripts/build-android.sh                                                   # 0 errors
git diff --stat main -- Assets/ ProjectSettings/ Packages/                 # empty, except M2.2's core change
```
M2.2 also runs Unity's suite and golden check (`scripts/run-playmode-tests.sh`, `scripts/golden-hashes.sh check`). From M2.9 the Unity lines go away.

Render references change only on purpose, in the chunk that changes the drawing. Each re-recorded image is looked at before it's committed.

## Risks
| Risk | Plan |
| :--- | :--- |
| The offscreen start-up crash seen in the bare spike reaches the suite once Gum is in | M2.0 runs the suite 20 times. If it shows, find the null entry point (the core dumps are on disk) before going on |
| Gum lacks a control or behaves unlike UI Toolkit (dropdown popups, scroll lists, sliders with steps) | Gum is MIT: write the missing piece on its primitives; the first two panels show the cost early |
| Gum's text differs between drivers and breaks render references | The built-in bitmap font avoids rasterizer differences. The UI's pixels can get a looser tolerance if needed |
| Gum's packages move fast (a preview build most days) | Pin 2026.10.1.1 and bump only on purpose, in its own commit |
| The calm command can't be checked in Unity here | D4 waits for M2.9; the button stays disabled until then |
| A UI scenario needs real-time behaviour (Unity's scenarios ran at 10x) | Drive the client frame by frame with the tick-based view clock, as the M0 and M1 tests do |
| Removing Unity loses coverage that no one noticed | M2.8 inventories every Unity test first; M2.9 starts only when each has a .NET twin or a recorded reason |

## Decided by the producer (2026-10-18)
"Go ahead with M2, two PRs, your defaults are fine":
1. **Two pull requests:** the first at parity (after the M2.7 sign-off), the second removing Unity (M2.8 and M2.9), both from this branch.
2. **D1 and D3** as written.
3. **The Definition of Done wording** for a repo without Unity, as proposed in M2.8.
