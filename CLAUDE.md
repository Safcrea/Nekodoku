# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Meowdoku ("Kawaii Neko Puzzle") is a Unity mobile logic puzzle: an original game inspired by the Meowdoku-style mechanic (one hidden cat per row/column/color-region, no two cats touching). It is two codebases in one repo:

- **The Unity game** — `Assets/_GameData/` (C#, Unity 6000.3.13f1).
- **A standalone static web level editor** — `web level editor/` (plain JS, no build step). It edits the exact same JSON level format the game loads, with a validator (structural checks plus a backtracking uniqueness solver — the only place solvability is checked at all now, see Architecture) and a seeded procedural generator, kept in lockstep with the C# level format via a parity test.

**IP constraint that applies to all level/art/UI work:** original game, inspired by the reference mechanic. Never copy the reference game's name, art, UI layout, level maps, screenshots, logo, sounds, or text/branding.

## Commands

### Unity (game)

No CLI build/lint — work happens in the Editor. There's no interactive `Update`/`build` step to run for iteration; changes are verified by letting the Editor recompile and reading the Console.

- **Run EditMode tests**: Window > General > Test Runner > EditMode > Run All. (Batchmode is also possible via `Unity -batchmode -nographics -runTests -testPlatform EditMode -projectPath . -testResults <path>`, but only when no other Editor instance has the project open — Unity refuses a second instance on the same project.)
- **There is no in-Unity level tooling anymore.** `LevelExporter`, `LevelEditorWindow`, and `NekoLevelValidator` (and `NekoSampleLevels.cs`, the legacy hardcoded generator they supported) have all been deleted — there's no `Meowdoku > Levels > ...` menu at all, and no Editor-only folder for game-specific tooling under `Assets/_GameData/Systems/`. **The web level editor (`web level editor/`) is now the only way to author or validate a level**: hand-paint or seed-generate it there, confirm it with `npm test`, then drop the exported JSON into `Assets/_GameData/Systems/Data/Levels/` and register it in `LevelDatabase.asset` (`Assets/_GameData/Systems/Scriptables/Level Database/`) by hand in the Inspector — dragging the new `TextAsset` into its `levelFiles` list. If this tooling gets rebuilt, update this section.
- **Cell prefab / scene UI regeneration tools no longer exist.** `CellPrefabBuilder` and `GameSceneUIBuilder` (the `Meowdoku > Scene > Build Cell Prefab` / `Build Game UI` menu tools) were deleted after the prefab and canvas hierarchy were already baked into `NekoCell.prefab` and the scene. If the cell's or canvas's visual structure ever needs to change again, these tools need to be rebuilt from scratch (or the prefab/scene hand-edited directly in the Editor) — there's no code-driven regeneration path anymore. New UI panels are hand-built in the Editor and wired to serialized fields the same way.

### Web level editor (`web level editor/`)

```sh
cd "web level editor"
npm run serve   # http://localhost:8377 — ES modules need a real server, not file://
npm test        # validate-goldens.mjs (parity vs. all shipped levels) + generate-test.mjs (generator determinism/validity)
node tools/build-preview.mjs   # bundles into one dependency-free preview.html
```

Run `npm test` after touching anything under `web level editor/js/core/` **or** `LevelData.cs`/the game's puzzle rules — the two must never silently drift apart.

## Architecture

### Level data pipeline

Levels are JSON files under `Assets/_GameData/Systems/Data/Levels/`, indexed in order by `LevelDatabase` (a ScriptableObject holding an ordered `List<TextAsset>`, at `Assets/_GameData/Systems/Scriptables/Level Database/LevelDatabase.asset`). The conversion chain:

`LevelData` (plain JSON DTO: `formatVersion`, `id`, `title`, `size`, `regions` as row-strings, `cats[]` with `row`/`column`/`letter`/`locked`) → `.ToLevel()` → `Level` (the runtime model: pure C#, no `UnityEngine` dependency) → consumed by `PuzzleBoard`/`PuzzleValidator` for actual gameplay.

`GameManager` loads levels via `LevelLoader`, which wraps a **serialized `LevelDatabase` field** — not `Resources.Load`; nothing in this pipeline uses the `Resources` folder. There is **no C# structural/uniqueness validator anymore** (see Commands above) — `LevelDatabase.LoadLevels()` only does the shape checks inside `LevelData.ToLevel()` (does the cat count match `size`, are region rows well-formed, etc.); actual puzzle-rule validity (unique solution, connected regions) is only ever checked by the web level editor's JS validator before a level is exported. Treat `npm test` in `web level editor/` as the only safety net before a hand-authored level ships.

### The tutorial lesson (pre-Level-1, once-ever)

`TutorialLessonController` (sibling of `GameManager`) runs a scripted, once-ever lesson before Level 1 on a bespoke 4×4/4-cat board (`Assets/_GameData/Systems/Data/Tutorial/tutorial-lesson.json`, not registered in `LevelDatabase`, no cats locked/pre-revealed), gated by the `Nekodoku.TutorialLessonSeen` PlayerPrefs key. It's an explicit `Phase` state machine that **interleaves cat reveals with rule teaching** rather than teaching all three rules against one pre-revealed cat:

`RevealCat0 → TeachRowColumn → RevealCat1 → TeachTouching → RevealCat2 → TeachColor` → then the fourth (and last) cat is found by the player entirely unassisted — no more scripted phases, just normal gameplay on the same board (see below). Each `Teach*` phase's target cells are **derived generically from the board/region data** (`ColumnCellsExceptCat`/`RowCellsExceptCat`/`NeighborCellsExceptCovered` — all 8 neighbors, orthogonal and diagonal/`RegionCellsExceptCoveredAndCat`), not hardcoded coordinates, and each rule step only targets cells not already covered by an earlier rule (so the same cell is never asked for twice).

Per-phase interaction control (so a curious player can't poke around and softlock a step):

- **`BoardView.SetTutorialRestriction(IEnumerable<Coord>)`/`ClearTutorialRestriction()`** — baked into `RefreshVisuals` itself (not a one-off call) so the restriction survives every subsequent refresh until explicitly cleared. Cells outside the allowed set are dimmed (`GridCell.SetDimmed`) and click-blocked (`GridCell.SetInteractable`, via `CanvasGroup.blocksRaycasts`).
- During `RevealCat*` phases only the target cat's cell is allowed (double-tap gesture, reusing the two-pulse hand animation); during `Teach*` phases only the current sub-guide's exact cells are allowed (tap or drag, matching the rule).
- **`TutorialLessonController.AllowsCommit`** (true only during `RevealCat*`) + **`GameManager.IsLessonCommitBlocked`** — `BoardInputHandler.CommitCat` is blocked whenever a rule's crossing cells are the only allowed input, so an accidental double-tap can't reveal one as a miss (which would permanently lock it out of ever being crossed, since `PuzzleBoard.CanSetCross` refuses already-revealed cells).

While a rule card is up (pop-in → typewritten body text → hold → dock into its `Rules` tab slot, crossfading in rather than reparenting so it never fights that strip's `HorizontalLayoutGroup`), a full-screen `dimOverlayCanvasGroup` fades in/out to hide the board and HUD so focus stays on the card.

`GameManager.Refresh()` branches into a lightweight lesson-only path (`IsLessonActive`, true only across the `RevealCat*`/`Teach*` phases) that skips HUD/win/fail logic entirely. Once the phases finish, `IsLessonActive` goes false and `Refresh()` drops into completely normal per-action gameplay on the *same* board (real HUD updates, real win/fail) — except the HUD itself (`GameplayScreen.SetHudVisible`) stays hidden for the whole lesson board's lifetime (tracked by `GameManager.isLessonBoardLoaded`, independent of `IsLessonActive`), only reappearing once `LoadLevel` loads the real Level 1. `NextLevel()` checks `isLessonBoardLoaded` first so the win screen's "Next" after finishing the lesson board routes to real Level 1 instead of blindly incrementing `levelIndex`.

There is **no separate per-level tutorial system anymore**. The old `TutorialController` (level-1 "How to Play" panel + a generic hand-guide that derived what to point at from board inspection) was deleted once `TutorialLessonController` existed to teach the rules up front — the lesson's own hand/focus-ring guide (same visual, same coroutine-driven pulse/drag-travel animation) is owned directly by `TutorialLessonController`. After the lesson finishes once, there is no further in-game hinting for any level, including Level 1.

### Runtime architecture: one `GameManager` orchestrating focused sibling components

The game used to be a single ~2600-line `NekoGameController` `MonoBehaviour` split into partial-class files by concern. It's now genuinely split into separate components living on/around one "Neko Game Manager" GameObject in `Assets/_GameData/Systems/Scenes/Game Scene.unity`. The actual current split (verify against `GameManager.cs`'s serialized fields before trusting this list — it has drifted before):

- **`GameManager`** — the conductor. Owns `board`, `levels`, `levelIndex`, the undo stack, and level load/advance/restart. Exposes `Board`, `IsLessonActive`, `SaveUndo()`, `DiscardLastUndo()`, `Refresh()` for the other components to call back into. Only **`BoardInputHandler`** is `[RequireComponent]`-declared (auto-added as a sibling); everything else (`BoardView`, `GameplayScreen`, `TutorialLessonController`, `LevelCompleteScreen`, `LevelFailedScreen`) is a plain serialized reference to whatever GameObject actually hosts it in the scene, not an auto-added sibling.
- **`BoardView`** — cell prefab instantiation, board layout math, intro animation, per-refresh visual sync, board shake, and hit-testing (`TryPointerToCell`/`TryGetCellCenterIn`). Owns `cellPrefab`/`boardRoot`. Also owns the tutorial's cell-restriction mechanism (`SetTutorialRestriction`/`ClearTutorialRestriction`, applied inside `RefreshVisuals`) since it's the one place that already iterates every cell each refresh. Per-cell juice (punch/cross-jelly/cat-found pop) is `GridCell`'s own responsibility, not a separate effects component.
- **`GameplayScreen`** — the HUD: title text plus the two sub-widgets it composes, `LifeHearts` and `CatCounter`. There is no separate word-spelling bar anymore — `letter` still exists on cat data for historical/format reasons but nothing renders a word from it today.
- **`TutorialLessonController`** — the once-ever, pre-Level-1 rule-teaching lesson, and the sole owner of the pointing-hand/focus-ring guide visual. See "The tutorial lesson" above.
- **`LevelCompleteScreen`** / **`LevelCompleteBucket`** — the win popup: bucket rises and gathers cats, panel/label/stars pop in (DOTween + Text Animator), next-button click plays an outro (fade everything, bucket drops back below screen) before advancing.
- **`LevelFailedScreen`** — the lose popup and its retry flow.
- **`BoardInputHandler`** — tap/drag-to-cross, double-tap-to-commit, and (deliberately) owns `InputLocked` and the cat-reveal-sequence lock, since gating input is fundamentally what that state is for. `GridCell` calls into this, not into `GameManager` directly.

**Wiring happens only in `GameManager.Start()`**, via serialized references and explicit `Initialize(...)` calls — never in any component's `Awake()`. Unity only guarantees every `Awake()` across the scene has run before any `Start()` runs, and component order within one GameObject's Awake phase is otherwise unspecified; centralizing the cross-component wiring in one `Start()` sidesteps that entirely. If you add a component that needs another one, give it an `Initialize(...)` method and call it from `GameManager.Start()` — don't reach for `Awake()`-time `GetComponent`.

There is currently **no scene-reference validation convention** (no `Validate()`/`SceneValidation` helper, no `GameManager.ValidateSceneReferences()` — these were removed at some point; if you reintroduce that pattern, update this section). A misconfigured serialized field just fails at runtime with a null reference, not a named startup error.

Small standalone helpers actually present today: `RegionPalette` (color-region palette), `SpriteSheetAnimator`/`SpriteSheetAnimationPlayer` (procedural cat/reveal sprite-sheet animation), `GameHaptics` (haptics via the `Voodoo.Utils` plugin), `UndoStack` (push/pop/apply snapshot mechanics), `SoundManager`/`SoundLibrary`/`SFXBGMData` (audio). **DOTween and `AllIn1SpringsToolkit`'s `TransformSpringComponent` are the standard animation toolkit now** — used throughout `LevelCompleteScreen`, `LevelCompleteBucket`, `LevelFailedScreen`, `LifeHearts`, `CatCounter`, `GridCell`, `BoardView`, and `TutorialLessonController` — not "installed but unused" (see below).

Board cells are `NekoCell.prefab` instances (`Assets/_GameData/Systems/Prefabs/`), not hand-built GameObjects — `GridCell` exposes the prefab's child components as serialized fields and forwards taps/drags to `BoardInputHandler`.

### Web level editor internals

`web level editor/js/core/model.js` is a hand-port of `LevelData.cs` (same field order, same shape checks). `validator.js`'s structural checks mirror what `LevelData.ToLevel()`/the game's rules require, and its backtracking uniqueness solver has **no C# counterpart at all anymore** (the Editor-only `NekoLevelValidator.cs` that it used to mirror has been deleted) — this JS validator is the only place a level's solvability is ever checked before it ships. `js/core/generator.js` is web-only (no C# equivalent): seeded (`seed:size` → mulberry32 PRNG) legal-cat-placement → snaky Eden-growth regions → hill-climbing boundary-flip repair until the solver confirms a unique solution — plain retry-until-unique does not scale past ~7×7 (thousands of solutions on random layouts), the repair walk is what makes 8×8/9×9 generation reliable. 3×3 has no legal layout under the rules (the middle row's cat always conflicts) and is rejected with a clear error rather than looping forever.

`tests/validate-goldens.mjs` is the parity guard: it runs the JS validator against every JSON file the game ships and additionally requires the JS serializer's output to be **byte-identical** to what Unity's `JsonUtility` wrote. If this test fails after a C# change, the JS port needs the same change, not the other way around.

### Docs/ and .codex/agents/ are earlier-milestone planning docs, not current status

`Docs/GameDesignDocument.md`, `DevelopmentPipeline.md`, `CurrentBacklog.md`, `DeveloperImplementationBrief.md`, and `Docs/Agents/LevelAgentWorkflow.md` describe an earlier project milestone (levels still hardcoded in C#, UI still runtime-built, no solver yet, one big `NekoGameController`) and reference stale file paths (`Assets/Scripts/Meowdoku/...` — everything moved to `Assets/_GameData/Systems/Scripts/` in a later reorg, and the class-per-concern paths no longer exist at all post-split). The `.codex/agents/level-designer.toml` / `level-qa.toml` configs have the same stale paths. Treat the **game rules and IP constraints** in these docs as current (they haven't changed), but verify file paths and "not yet implemented" claims against the actual code before trusting them — the level data system, solver, JSON pipeline, and component split these docs describe as future milestones already exist.

### Third-party packages

DOTween (`Assets/Resources/DOTweenSettings.asset`) and AllIn1SpringsToolkit (`Assets/Plugins/AllIn1SpringsToolkit/`) are the standard animation toolkit and are used throughout — see the runtime architecture section above for the current file list. `TutorialLessonController`'s hand/focus-ring guide animation is the one holdout still hand-rolled with `Time.unscaledTime`/a coroutine rather than DOTween; migrate it if you're touching that code anyway, but it's not broken as-is. AudioTools/FastUISounds, FolderIcons, and Hierarchy Designer are editor/audio conveniences, unrelated to gameplay code. Febucci's Text Animator for Unity drives the "Level Complete" text reveal in `LevelCompleteScreen`.
