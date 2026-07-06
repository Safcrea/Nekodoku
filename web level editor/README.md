# Meowdoku Level Editor (web)

Browser-based editor for the game's JSON level format. Same rules, same files:
anything saved here drops straight into `Assets/_GameData/Systems/Data/Levels/`
and registers via the Unity `LevelDatabase` (or the in-Unity Level Editor).

No build step, no dependencies — plain ES modules.

## Run locally

Browsers block ES modules from `file://`, so serve the folder:

```sh
cd Website
python3 -m http.server 8377     # or: npm run serve
# open http://localhost:8377
```

Deploying is just static hosting: point GitHub Pages / Netlify / Vercel at this
folder as-is.

## Test (validator parity)

The JS validator is a port of `NekoLevelValidator.cs` and must always agree
with it. The guard is the golden-corpus test, which checks every level the game
ships for structural validity, solution uniqueness, and byte-identical
round-trip serialization against Unity's `JsonUtility` output:

```sh
npm test
# = node tests/validate-goldens.mjs  (golden corpus parity)
# + node tests/generate-test.mjs    (generator validity + determinism)
```

**Run this after any change to the level format or rules — in either codebase.**
If you change `LevelData.cs` / `NekoLevelValidator.cs`, mirror the change in
`js/core/` and re-export the levels from Unity so the corpus reflects the new
format.

## Layout

- `js/core/model.js` — level parse/serialize (mirrors `LevelData.cs`)
- `js/core/validator.js` — structural checks + solver (mirrors `NekoLevelValidator.cs`)
- `js/core/generator.js` — seeded procedural generator (web-only)
- `js/app.js` — editor UI (state, grid painting, animations, import/export, theming)
- `tests/validate-goldens.mjs` — parity test against the game's exported levels
- `tests/generate-test.mjs` — generator validity/determinism test
- `tools/build-preview.mjs` — bundles everything into one self-contained
  `preview.html` for sharing (`node tools/build-preview.mjs`)

## Using the editor

- **Generate** — type any seed (or hit the dice) and press Generate: a complete,
  uniquely-solvable level at the current board size, with word, title, and
  starter clues. Deterministic — the same seed + size always produces the same
  level, so a seed is a shareable level. Sizes 4-9 (3×3 boards have no legal
  layout under the rules). Under the hood: place a legal cat solution, grow
  snaky regions from the cats, then hill-climb boundary-cell flips until the
  solver confirms the solution is unique.
- **Regions mode** — pick a color swatch, click or drag across the board.
- **Cats mode** — pick a letter slot, click its cell; click a placed cat to
  remove it; "starts revealed" marks the selected letter's cat as a free clue.
- **Auto-place cats** — when the region layout has exactly one legal solution,
  places every letter's cat on it (row order).
- **Validation** — live: solution count from the region layout, plus the same
  structural checks the Unity editor and tests run.
- **File** — download/copy the JSON, or paste/open an existing level to edit.
