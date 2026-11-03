# Epic UI-Beta B4: faces everywhere (plan)

Working document (AGENTS.md, Documentation Protocol 6). Written 2026-11-03 under the producer's directive of 2026-11-02 ("continue working through B3-B5 ... an implementation plan for each work chunk before executing"). The design is the epic's §2 stories 5, 6 and 8, Q10, §3.4 and §9's inspector sketch ([`docs/epics/epic-ui-beta.md`](../docs/epics/epic-ui-beta.md)).

- **Branch:** `feat/ui-beta-b4`, cut from `feat/ui-beta-b3` at `0c68fc8`, since nothing can merge overnight. One PR into `main` after B3's, rebased then. Nothing is pushed.
- **Estimate:** 2 agent days, as the epic holds: P1 0.25, P2 0.75, P3 0.5, P4 0.25, P5 0.25.
- **Complexity** ([rubric](rules/complexity-grading.md)): **Possibly Complex**, as the epic stamps B4: Gum UI, renders that change, and the frame-time gate (F21). Nothing touches the core, the save or threading.
- **Gates (every commit):**
  - a build with 0 warnings;
  - `dotnet test`;
  - the pinned image exact, with a commit that changes the picture followed by its re-records;
  - MonoGame and Android at the phases that touch the client.
  - **B4's own gate (the epic's §4):** the frame-time gate holds with faces in the lists and the inspector on the map.

## 0. What exists
- B3's portrait atlas: 48 slots, each a framed 52×52 portrait at 1× and 104×104 at 2×, keyed by look, mood, standing and helm. It is baked on first showing.
- The dashboard's `MoodTracker`, shared by the dossier and the scene speakers.
- On the map, a selected crawler gets a gold ring and its name over it; `WorldSelection.SelectCrawler(id, follow)` makes the camera follow it.
- Lists that name crawlers:
  - the dashboard's residents in a sector (cards with a name, a detail line and a badge; a click selects into the dossier);
  - the guild roster's member cards;
  - the dossier's affinities, as one text line each.
- Overlays on the map: the expedition tracker (top right).
- `FrameTimeGateTests` (isometric outpost, 800×600: a mean of 10 ms, a CPU p99 of 33 ms); `UiDrawProbe` (explicit) measures Gum's draw by cause (F21).

## 1. Decisions (defaults; the producer answers by exception)
| # | Decision | Default |
| :-: | :--- | :--- |
| D1 | **A face** (for lists) | A 32×32 crop of the portrait around the head, inside a 1-pixel frame in the standing's outer colour. It is a third cell of the same atlas slot: a crop of the same bake, so no extra bake. |
| D2 | **Where faces go** (story 6) | The residents in a sector, the guild roster's members, and the dossier's affinities, which become rows (a face, the name, warmth, trust, fear and respect). The chronicle stays text. |
| D3 | **The map inspector** (Q10, §9's sketch) | A card at the bottom left of the map view while a crawler is selected and the panels are hidden. It holds: the portrait at 2×; the name and state badge; the mood line; health and four need bars; the action; Open dossier and Follow / Stop following. It is a view over an engine-free `InspectorModel` and decides nothing. |
| D4 | **Click to find** (story 8) | A click on a face anywhere (a list, the roster, an affinity) closes the panels and windows and selects that crawler on the map with the camera following, so the inspector shows it. A click on a card's body keeps today's behaviour. |
| D5 | **Faces' moods** | The dashboard's shared tracker, so a face, the dossier and a scene speaker agree. |
| D6 | **The gate** | A frame-time gate with the dashboard's faces shown, and one with the inspector over the map, at the existing budgets. `UiDrawProbe`'s numbers before and after go into the epic. Shared render states (F21) only if the probe shows a need. |

## 2. Phases

### P1. Faces in the atlas (0.25 day) [Basic: a crop of an existing bake, with a test]
1. **`PortraitKit.Face`** [Basic]: the crop and its frame.
2. **The atlas's third cell** [Basic]: `PortraitAtlas.Show(key, PortraitAtlas.FaceScale)`.
3. **Tests** [Basic]: the face is the portrait's crop, pixel for pixel, read back from the texture; it takes no extra bake.

### P2. The map inspector (0.75 day) [Possibly Complex: a new Gum overlay with buttons; a new render]
1. **`InspectorModel`** [Possibly Complex: a new model, as the dossier's]: from the selected crawler, live. It holds:
   - the portrait key, the name, the state badge, the mood line and health;
   - the needs and the action;
   - whether it is following.
   - It has two commands: Open dossier (panels on) and Follow (follow on, or off).
   - Model tests.
2. **The inspector in Gum** [Possibly Complex]: D3's card in the play screen, shown on the map while a crawler is selected. Client tests:
   - a click on a crawler shows its portrait and name;
   - Open dossier shows the panels on it;
   - Follow toggles the camera.
   - A new render, `render-iso-inspector-t300`, recorded in the pinned image.

### P3. Faces in lists (0.5 day) [Possibly Complex: Gum; renders change]
1. **Residents and roster** [Possibly Complex]: a face on each card. The dashboard's and the roster's renders are re-recorded.
2. **Affinities as rows** [Possibly Complex: the dossier's model changes shape]: `DossierModel.Affinities` becomes rows with the peer's id, so the view can show a face. The player's row has no face.

### P4. Click to find (0.25 day) [Basic: a command through the dashboard model, with a test]
1. **`DashboardModel.FindOnMap(id)`** and the faces' click handlers (D4). A client test: a click on a resident's face hides the panels, selects and follows it, and the inspector shows it.

### P5. The gate, the look and close-out (0.25 day) [Possibly Complex: measuring]
1. **The frame-time gate** [Possibly Complex: measuring]: D6's two gate tests. `UiDrawProbe` before and after, numbers in the epic.
2. **The producer's look** [stop, in the morning, with B3's]: the inspector, faces in the lists, the affinities' rows.
3. **Close-out** [Basic]:
   - the epic's §10 and §4 row;
   - the roadmap, then the epic, then the focus file;
   - `architecture_history.md`;
   - AGENTS.md;
   - this plan is deleted.

## 3. Risks
| Risk | What we do about it |
| :--- | :--- |
| Faces in every list raise Gum's draw cost (F21) | One texture for all faces (one draw state); P5 measures; shared render states only on the measurement |
| A 32-pixel face doesn't read | The crop is centred on the head; the renders show it; the morning look decides; a face could take the 52-pixel portrait instead |
| The inspector covers what the player is looking at | Bottom left, compact; hidden with no selection; the panels hide it |
| More than 48 portraits shown at once evict each other | Lists show at most about 20 crawlers; the overflow is the oldest shown, re-baked when shown again |
