# Developer Implementation Brief

## Source Documents

- `Docs/GameDesignDocument.md`
- `Docs/DevelopmentPipeline.md`
- `Docs/CurrentBacklog.md`

## Current Implementation Target

Milestone 1: Correct Core Prototype.

The prototype must match this behavior:

- Tap toggles an X mark on unrevealed cells.
- Drag paints X marks across unrevealed cells.
- Double-tap commits a cat placement.
- Correct commit reveals a cat.
- Wrong commit reveals a miss and subtracts 1 heart.
- Player starts with 3 hearts.
- At 0 hearts, level enters failed state.
- Locked starter cats are supported by level data.
- Undo restores board marks/reveals but does not refund hearts.
- Restart resets board, hearts, and mistakes.

## Files In Scope

- `Assets/Scripts/Meowdoku/NekoPuzzleCore.cs`
- `Assets/Scripts/Meowdoku/NekoGameController.cs`
- `Assets/Scripts/Meowdoku/NekoCellView.cs`

## Current Status

Implemented in the prototype:

- Core board state
- Hidden solution data
- Locked starter cat data
- Tap/drag X marking
- Double-tap cat commit
- Correct and wrong commit outcomes
- 3-heart mistake system
- Failed state when hearts reach 0
- Runtime-generated mobile UI
- Fifteen sample levels
- EditMode validation for sample level data

Still needed:

- Proper fail modal/screen instead of status text only
- Proper win modal/screen
- Level files outside hard-coded C# arrays
- Gameplay EditMode tests
- Level select
- Save/load progress
- Solver and generator

## Manual QA Checklist

For each of the 15 prototype levels:

- Tap an empty unrevealed cell: X appears.
- Tap the same cell again: X clears.
- Drag across cells: X marks are painted.
- Double-tap a correct hidden cat: cat appears and found counter increases.
- Double-tap a wrong cell: red X appears and hearts decrease.
- After 3 wrong commits: status shows failed state and no more cells can be changed.
- Restart resets hearts to 3 and clears non-locked cells.
- Locked starter cat is visible from level start and cannot be changed.
- Undo restores board cells but does not refund hearts.

## Engineering Notes

- Gameplay rules should stay in `NekoPuzzleCore.cs`, not in UI code.
- Current UI is runtime-generated for speed. It can be replaced with prefabs once the loop is stable.
- Sample levels are original prototype data and should be moved to level data files in Milestone 2.
- Do not copy reference game art, UI layout, wording, screenshots, or levels.
