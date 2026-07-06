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
- **Rebuild the cell prefab**: menu `Meowdoku > Scene > Build Cell Prefab` — regenerates `NekoCell.prefab` from code; run after changing the cell's visual structure in the builder.
- **Rebuild the scene UI**: menu `Meowdoku > Scene > Build Game UI` — regenerates the "Neko Canvas" hierarchy (title, rules strip, board panel, buttons, tutorial/win panels) and re-wires it onto whatever `NekoGameController` is in the open scene. Safe to re-run (destroys and rebuilds "Neko Canvas" first). Save the scene afterward.

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

`LevelData` (plain JSON DTO: `formatVersion`, `id`, `title`, `size`, `regions` as row-strings, `cats[]` with `row`/`column`/`letter`/`locked`) → `.ToLevel()` → `NekoLevel` (the runtime model: pure C#, no `UnityEngine` dependency) → consumed by `NekoPuzzleBoard`/`NekoPuzzleValidator` for actual gameplay.

`NekoGameController` loads levels from a **serialized `LevelDatabase` field**, not `Resources.Load` — nothing in this pipeline uses the `Resources` folder. `NekoLevelValidator` (Editor-only: structural checks, region-connectivity BFS, backtracking uniqueness solver) validates levels at author time, not at runtime load.

`NekoSampleLevels.cs` (Editor-only) is the legacy hardcoded-C# generator that originally produced all 100 levels; it now exists solely so the exporter can regenerate/re-export that baked set. New levels should be authored via the in-Unity Level Editor or the web editor, not by extending that generator.

### `NekoGameController` — scene-wired, not self-bootstrapping

`NekoGameController` must be a real GameObject placed in `Assets/_GameData/Systems/Scenes/Game Scene.unity`, with ~20 serialized fields wired in the Inspector (Level Database, Cell Prefab, Tutorial Hand Texture, board/UI root RectTransforms, HUD texts, the 4 nav buttons). It does **not** self-bootstrap via `RuntimeInitializeOnLoadMethod` — `Awake()` validates every serialized reference and logs exactly which one is missing by name before doing anything else. If you add a new UI element the controller needs, it must become a serialized field wired through the scene (or through `Meowdoku > Scene > Build Game UI`), never a `new GameObject(...)` built at runtime.

Board cells are `NekoCell.prefab` instances (`Assets/_GameData/Systems/Prefabs/`), not hand-built GameObjects — `NekoCellView` exposes the prefab's child components as serialized fields.

The three procedural pixel-drawing sprites (cat face, focus ring, juice dot in `NekoGameController.Assets.cs`) are a known exception — still generated at runtime via hand-rolled triangle-fill/Bresenham code rather than baked to real sprite assets. Left alone deliberately; low risk, cosmetic-only.

### `NekoGameController` is a large partial class — split intentionally, not fully

The controller is split across many files by concern (`.Board`, `.Effects`, `.Tutorial`, `.Input`, `.Flow`, `.Layout`, `.UiFactory`, `.Assets`, `.Letters`, `.Haptics`, `.Undo`, `.Types`), but they are still one class and one `MonoBehaviour`. Two pieces have been fully extracted into standalone, non-MonoBehaviour classes: `NekoUndoStack` (push/pop/apply snapshot mechanics) and `NekoHaptics`/`HapticStrength` (top-level, no longer nested). `Board.cs`/`Effects.cs`/`Tutorial.cs`/`Input.cs` remain intentionally un-split: they call into each other constantly (crossing a cell → board refresh → tutorial-guide update → possible effect trigger → board layout read), so separating them means redesigning the call graph, not just moving code. Don't attempt a mechanical split of these without a way to actually playtest the result — a half-finished split is worse than the current coupling.

### Editor tooling pattern

Every Editor-only tool that needs to touch the scene/assets (`LevelExporter`, `CellPrefabBuilder`, `GameSceneUIBuilder`, `LevelEditorWindow`) is a `[MenuItem]`-driven tool run from inside the Editor, because Unity refuses a second batchmode instance while the Editor has the project open. These tools duplicate a handful of small theme constants (colors, `BoardPixels`, etc.) from the runtime controller rather than reaching into its private fields — noted in each file's header comment; keep them in sync by hand until a shared theme asset exists.

### Web level editor internals

`web level editor/js/core/model.js` and `validator.js` are hand-ports of `LevelData.cs` and `NekoLevelValidator.cs` — same field order, same structural checks, same backtracking solver. `js/core/generator.js` is web-only (no C# equivalent): seeded (`seed:size` → mulberry32 PRNG) legal-cat-placement → snaky Eden-growth regions → hill-climbing boundary-flip repair until the solver confirms a unique solution — plain retry-until-unique does not scale past ~7×7 (thousands of solutions on random layouts), the repair walk is what makes 8×8/9×9 generation reliable. 3×3 has no legal layout under the rules (the middle row's cat always conflicts) and is rejected with a clear error rather than looping forever.

`tests/validate-goldens.mjs` is the parity guard: it runs the JS validator against every JSON file the game ships and additionally requires the JS serializer's output to be **byte-identical** to what Unity's `JsonUtility` wrote. If this test fails after a C# change, the JS port needs the same change, not the other way around.

### Docs/ and .codex/agents/ are earlier-milestone planning docs, not current status

`Docs/GameDesignDocument.md`, `DevelopmentPipeline.md`, `CurrentBacklog.md`, `DeveloperImplementationBrief.md`, and `Docs/Agents/LevelAgentWorkflow.md` describe an earlier project milestone (levels still hardcoded in C#, UI still runtime-built, no solver yet) and reference stale file paths (`Assets/Scripts/Meowdoku/...` — everything moved to `Assets/_GameData/Systems/Scripts/Meowdoku/...` in a later reorg). The `.codex/agents/level-designer.toml` / `level-qa.toml` configs have the same stale paths. Treat the **game rules and IP constraints** in these docs as current (they haven't changed), but verify file paths and "not yet implemented" claims against the actual code before trusting them — the level data system, solver, and JSON pipeline these docs describe as future milestones already exist.

### Third-party packages present but not yet wired in

DOTween (`Assets/Resources/DOTweenSettings.asset`) and AllIn1SpringsToolkit (`Assets/Plugins/AllIn1SpringsToolkit/`) are installed but no Meowdoku script references them yet — the hand-rolled coroutine animations in `Effects.cs`/`Tutorial.cs`/`Board.cs` haven't been migrated. AudioTools/FastUISounds, FolderIcons, and Hierarchy Designer are editor/audio conveniences, unrelated to gameplay code.
