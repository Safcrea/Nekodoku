# AVN Level Editor: PawDoku (web)

Browser-based editor for the game's JSON level format. Same rules, same files:
anything saved here drops straight into
`Assets/_GameData/Systems/Data/Levels/Default Levels/`. This is the only way
to author or validate a level's content — there's no in-Unity authoring
tooling anymore. Level *order* is a shared manifest
(`levels-manifest.json`, in that same folder) editable from either side — see
the Level Library section below, and CLAUDE.md's "Level data pipeline" for
the Unity-side sync tool that reads it (`LevelManifestSync.cs`) — no more
hand-dragging `TextAsset`s into `LevelDatabase`'s Inspector list.

No build step, no dependencies — plain ES modules.

## Run locally

Browsers block ES modules from `file://`, so serve the folder:

```sh
cd "web level editor"
python3 -m http.server 8377     # or: npm run serve
# open http://localhost:8377
```

Deploying is just static hosting: point GitHub Pages / Netlify / Vercel at this
folder as-is.

## Test (validator parity)

There's no C# structural/uniqueness validator anymore — this JS validator is
the only place a level's solvability is checked before it ships (see
CLAUDE.md's "Level data pipeline"). The guard is the golden-corpus test, which
checks every level the game ships for structural validity, solution
uniqueness, and byte-identical round-trip serialization:

```sh
npm test
# = node tests/validate-goldens.mjs  (golden corpus parity)
# + node tests/generate-test.mjs    (generator validity + determinism)
```

**Run this after any change to the level format or rules — in either codebase.**
If you change `LevelData.cs` or the game's puzzle rules, mirror the change in
`js/core/` so the two never silently drift apart.

## Layout

- `js/core/model.js` — level parse/serialize (mirrors `LevelData.cs`)
- `js/core/validator.js` — structural checks (mirroring `LevelData.ToLevel()`'s shape checks) + the solver
- `js/core/generator.js` — seeded procedural generator (web-only)
- `js/app.js` — editor UI (state, grid painting, animations, import/export, theming)
- `tests/validate-goldens.mjs` — parity test against the game's exported levels
- `tests/generate-test.mjs` — generator validity/determinism test
- `tools/build-preview.mjs` — bundles everything into one self-contained
  `preview.html` for sharing (`node tools/build-preview.mjs`)

## Using the editor

- **Level** card only has an Id field now (plus Size) — there's no separate
  Title input. A hand-painted level's title is derived from the Id
  (`porch-patrol` → "Porch Patrol"); a generated level keeps the generator's
  own nicer adjective+noun title (only until you edit the Id afterward, which
  re-derives the title from it same as a hand-painted level).
- **Size** decides the board and, with it, how many colors (regions) there
  are — one region per row, always `size` of them, each holding exactly one
  cat.
- **Generate** — type any seed (or hit the dice), set how many cells each
  color should get in the "Colors & cell counts" list (defaults to an even
  split; "Even split" resets it; each row's **Normalize** button dumps the
  current gap onto that row, so after hand-editing one row away from even you
  can fix the total by pressing Normalize on any *other* row without touching
  the one you just set), and pick which cats start revealed in "Locked cats"
  (check exactly which ones, or set a count and press Randomize) — then press
  Generate. The counts must add up to exactly `size × size` — the Generate
  button stays disabled until they do (hover it for why). Deterministic — the
  same seed + size + counts + locks always produces the same level, so a
  seed+split+locks combo is a shareable level. Sizes 4-9 (3×3 boards have no
  legal layout under the rules). Under the hood: place a legal cat solution,
  grow snaky regions from the cats toward the requested split, then
  hill-climb boundary-cell flips until the solver confirms the solution is
  unique *and* every region matches its requested cell count exactly. Very
  even splits on large boards (8×8/9×9) are the hardest case — they're highly
  symmetric, which admits far more alternate solutions — so those can take a
  few seconds or occasionally fail with a "try different counts or another
  seed" error; skewed splits (a couple of big colors, the rest small) are far
  faster even at 9×9.
- **Regions mode** — pick a color swatch, click or drag across the board.
- **Cats mode** — pick a numbered cat slot, click its cell; click a placed cat
  to remove it; "starts revealed" toggles that one cat as a free clue (same
  effect as the Generate panel's Locked cats picker, just after the fact).
  There is no target word anymore — the old word-spelling feature was removed
  from the game (see CLAUDE.md's "Runtime architecture" section on
  `GameplayScreen` / `CatCounter`), and `LevelCatData` no longer carries a
  `letter` field to match.
- **Auto-place cats** — when the region layout has exactly one legal solution,
  places every cat on it (row order); disabled (hover for why) otherwise.
- **Validation** — live: solution count from the region layout, plus the same
  structural checks the game's `LevelData.ToLevel()` shape checks run.
- **File** — a menu in the top-right of the header (no more inline button row
  or visible JSON textarea): Download, Copy, Open file…, Save to Library, New.
- **Level Library** — needs a Chromium-based browser (Chrome/Edge; feature-detected,
  hidden with a note otherwise). "Open Level Library Folder…" picks
  `Default Levels/` via the File System Access API; the panel lists every
  level there, in `levels-manifest.json`'s order (falling back to alphabetical
  the very first time, before a manifest exists). Drag rows to reorder, then
  "Save Library Order" writes the manifest back. The panel itself lives in a
  collapsed-by-default drawer on the right edge of the screen — click the
  "Level Library" tab to open/close it. Click a row to load that level into
  the editor; "Save to Library" (in the File menu) writes your edit back to
  that exact file — it never creates a
  second file even if you change the Id while editing. With no level loaded
  from the library, "Save to Library" instead adds the current level as a new
  file. "Remove" only drops a level from the manifest order, it never deletes
  the file — an unlisted file is exactly what the Unity-side sync warns about
  as "orphaned," which is the intended safety net, not a bug.
