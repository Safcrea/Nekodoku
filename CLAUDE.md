# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Meowdoku ("Kawaii Neko Puzzle") is a Unity mobile logic puzzle: an original game inspired by the Meowdoku-style mechanic (one hidden cat per row/column/color-region, no two cats touching). It is two codebases in one repo:

- **The Unity game** — `Assets/_GameData/` (C#, Unity 6000.3.13f1).
- **A standalone static web level editor** — `web level editor/` (plain JS, no build step). It edits the exact same JSON level format the game loads, with a hand-ported validator and a seeded procedural generator, both kept in lockstep with the C# implementation via a parity test.

**IP constraint that applies to all level/art/UI work:** original game, inspired by the reference mechanic. Never copy the reference game's name, art, UI layout, level maps, screenshots, logo, sounds, or text/branding.

## Commands

### Unity (game)

No CLI build/lint — work happens in the Editor. There's no interactive `Update`/`build` step to run for iteration; changes are verified by letting the Editor recompile and reading the Console.

- **Run EditMode tests**: Window > General > Test Runner > EditMode > Run All. (Batchmode is also possible via `Unity -batchmode -nographics -runTests -testPlatform EditMode -projectPath . -testResults <path>`, but only when no other Editor instance has the project open — Unity refuses a second instance on the same project.)
- **Regenerate the baked level set**: menu `Meowdoku > Levels > Export Sample Levels To JSON` — re-exports the 100 legacy-generated levels to `Assets/_GameData/Systems/Data/Levels/*.json` and rebuilds `LevelDatabase.asset`. Re-run after touching `NekoSampleLevels.cs` (Editor-only) or the level JSON schema.
- **Hand-author/paint a level visually**: menu `Meowdoku > Levels > Level Editor` — paint regions, place cats, get live solver feedback, save straight to the JSON format and register it with the database.
- **Cell prefab / scene UI regeneration tools no longer exist.** `CellPrefabBuilder` and `GameSceneUIBuilder` (the `Meowdoku > Scene > Build Cell Prefab` / `Build Game UI` menu tools) were deleted after the prefab and canvas hierarchy were already baked into `NekoCell.prefab` and the scene. If the cell's or canvas's visual structure ever needs to change again, these tools need to be rebuilt from scratch (or the prefab/scene hand-edited directly in the Editor) — there's no code-driven regeneration path anymore.

### Web level editor (`web level editor/`)

```sh
cd "web level editor"
npm run serve   # http://localhost:8377 — ES modules need a real server, not file://
npm test        # validate-goldens.mjs (parity vs. all shipped levels) + generate-test.mjs (generator determinism/validity)
node tools/build-preview.mjs   # bundles into one dependency-free preview.html
```

Run `npm test` after touching anything under `web level editor/js/core/` **or** the C# level format/validator — the two must never silently drift apart.

## Architecture

### Level data pipeline

Levels are JSON files under `Assets/_GameData/Systems/Data/Levels/`, indexed in order by `LevelDatabase` (a ScriptableObject holding an ordered `List<TextAsset>`, at `Assets/_GameData/Systems/Scriptables/Level Database/LevelDatabase.asset`). The conversion chain:

`LevelData` (plain JSON DTO: `formatVersion`, `id`, `title`, `size`, `regions` as row-strings, `cats[]` with `row`/`column`/`letter`/`locked`) → `.ToLevel()` → `Level` (the runtime model: pure C#, no `UnityEngine` dependency) → consumed by `PuzzleBoard`/`PuzzleValidator` for actual gameplay.

`GameManager` loads levels via `LevelLoader`, which wraps a **serialized `LevelDatabase` field** — not `Resources.Load`; nothing in this pipeline uses the `Resources` folder. `NekoLevelValidator` (Editor-only: structural checks, region-connectivity BFS, backtracking uniqueness solver) validates levels at author time, not at runtime load.

`NekoSampleLevels.cs` (Editor-only) is the legacy hardcoded-C# generator that originally produced all 100 levels; it now exists solely so the exporter can regenerate/re-export that baked set. New levels should be authored via the in-Unity Level Editor or the web editor, not by extending that generator.

### Runtime architecture: one `GameManager` orchestrating six focused components

The game used to be a single ~2600-line `NekoGameController` `MonoBehaviour` split into partial-class files by concern. It's now genuinely split into separate components, all living as sibling `MonoBehaviour`s on one "Neko Game Manager" GameObject in `Assets/_GameData/Systems/Scenes/Game Scene.unity`:

- **`GameManager`** — the conductor. Owns `board`, `levels`, `levelIndex`, the undo stack, and the level-advance coroutine. Exposes `Board`, `SaveUndo()`, `DiscardLastUndo()`, `Refresh()` for the other components to call back into. `[RequireComponent]`-declares the other five, so adding it to a GameObject auto-adds them.
- **`BoardView`** — cell prefab instantiation, board layout math, intro animation, per-refresh visual sync, cell-level juice (punch/cross-jelly), board shake, and hit-testing (`TryPointerToCell`). Owns `cellPrefab`/`boardRoot`.
- **`WordSlotsView`** — the word bar: per-level slot rebuild, which cat letters are currently revealed (`letterVisibleCats`), the reveal/undo/restore API around that. Owns `wordRoot`.
- **`HudView`** — the "gameplay screen" chrome: title/count/status, win panel + its pop animation, the 4 nav buttons.
- **`TutorialController`** — the level-1 "How to Play" panel and pointing-hand guide.
- **`EffectsPlayer`** — the juice layer: floating -1 labels, sparkle bursts, the cat-found reveal sequence (pop → burst → flying letter). Owns `juiceRoot`.
- **`BoardInputHandler`** — tap/drag-to-cross, double-tap-to-commit, and (deliberately) owns `InputLocked` and the cat-reveal-sequence lock, since gating input is fundamentally what that state is for. `CellView` calls into this, not into `GameManager` directly.

**Wiring happens only in `GameManager.Start()`**, via `GetComponent<T>()` and explicit `Initialize(...)` calls — never in any component's `Awake()`. Unity only guarantees every `Awake()` across the scene has run before any `Start()` runs, and component order within one GameObject's Awake phase is otherwise unspecified; centralizing the cross-component wiring in one `Start()` sidesteps that entirely. If you add a component that needs another one, give it an `Initialize(...)` method and call it from `GameManager.Start()` — don't reach for `Awake()`-time `GetComponent`.

Each view validates its own serialized fields via a `Validate()` method (using the shared `SceneValidation.LogIfMissing` helper), and `GameManager.ValidateSceneReferences()` calls all of them before doing anything else, so a misconfigured scene fails loudly naming the exact field on the exact component.

Small standalone helpers used across components: `UiFactory` (the handful of UI elements still built at runtime — word slots, floating juice labels), `SpriteFactory` (procedural cat/focus-ring/juice-dot pixel art — a known deliberate exception to "no runtime asset generation," low-risk and cosmetic-only), `NekoEasing` (shared easing curves), `NekoUndoStack` (push/pop/apply snapshot mechanics), `NekoHaptics`/`HapticStrength` (Android vibration).

Board cells are `NekoCell.prefab` instances (`Assets/_GameData/Systems/Prefabs/`), not hand-built GameObjects — `CellView` exposes the prefab's child components as serialized fields and forwards taps/drags to `BoardInputHandler`.

### Editor tooling pattern

Editor-only tools that need to touch the scene/assets (`LevelExporter`, `LevelEditorWindow`) are `[MenuItem]`-driven, run from inside the Editor, because Unity refuses a second batchmode instance while the Editor has the project open.

### Web level editor internals

`web level editor/js/core/model.js` and `validator.js` are hand-ports of `LevelData.cs` and `NekoLevelValidator.cs` — same field order, same structural checks, same backtracking solver. `js/core/generator.js` is web-only (no C# equivalent): seeded (`seed:size` → mulberry32 PRNG) legal-cat-placement → snaky Eden-growth regions → hill-climbing boundary-flip repair until the solver confirms a unique solution — plain retry-until-unique does not scale past ~7×7 (thousands of solutions on random layouts), the repair walk is what makes 8×8/9×9 generation reliable. 3×3 has no legal layout under the rules (the middle row's cat always conflicts) and is rejected with a clear error rather than looping forever.

`tests/validate-goldens.mjs` is the parity guard: it runs the JS validator against every JSON file the game ships and additionally requires the JS serializer's output to be **byte-identical** to what Unity's `JsonUtility` wrote. If this test fails after a C# change, the JS port needs the same change, not the other way around.

### Docs/ and .codex/agents/ are earlier-milestone planning docs, not current status

`Docs/GameDesignDocument.md`, `DevelopmentPipeline.md`, `CurrentBacklog.md`, `DeveloperImplementationBrief.md`, and `Docs/Agents/LevelAgentWorkflow.md` describe an earlier project milestone (levels still hardcoded in C#, UI still runtime-built, no solver yet, one big `NekoGameController`) and reference stale file paths (`Assets/Scripts/Meowdoku/...` — everything moved to `Assets/_GameData/Systems/Scripts/` in a later reorg, and the class-per-concern paths no longer exist at all post-split). The `.codex/agents/level-designer.toml` / `level-qa.toml` configs have the same stale paths. Treat the **game rules and IP constraints** in these docs as current (they haven't changed), but verify file paths and "not yet implemented" claims against the actual code before trusting them — the level data system, solver, JSON pipeline, and component split these docs describe as future milestones already exist.

### Third-party packages present but not yet wired in

DOTween (`Assets/Resources/DOTweenSettings.asset`) and AllIn1SpringsToolkit (`Assets/Plugins/AllIn1SpringsToolkit/`) are installed but no Meowdoku script references them yet — the hand-rolled coroutine animations in `BoardView`/`EffectsPlayer`/`TutorialController`/`HudView` haven't been migrated. AudioTools/FastUISounds, FolderIcons, and Hierarchy Designer are editor/audio conveniences, unrelated to gameplay code.
