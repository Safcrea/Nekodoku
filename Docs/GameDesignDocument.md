# Kawaii Neko Puzzle - Game Design Document

## 1. Overview

**Working title:** Kawaii Neko Puzzle  
**Genre:** Mobile logic puzzle  
**Platform:** iOS and Android, portrait orientation  
**Target session length:** 1-4 minutes per level  
**Core fantasy:** Use deduction across colors, rows, columns, and neighboring cells to find the safe home for every kawaii neko.

Each level has a hidden solution. The player studies the colored board, marks impossible cells, then double-taps a cell to commit a cat placement. Correct placements reveal a cat. Wrong placements cost a heart.

## 1.1 Reference Target

The target product is feature-equivalent to the public Meowdoku-style experience:

- One cat per colored region.
- No two cats in the same row or column.
- Cats cannot touch, including diagonally.
- Double-tap to commit a cat placement.
- Wrong placement costs one heart.
- Player has 3 hearts per level.
- X marks help eliminate impossible cells.
- Hint, undo, restart, level list, and rules controls.
- Difficulty categories similar to Normal, Hard, and Ultra.
- Calm mobile-first puzzle flow, offline-friendly play, daily puzzles, and non-intrusive ads.

This project must not copy the exact Meowdoku name, logo, cat art, screenshots, UI layout, wording, sound effects, App Store assets, or level data. We will build an original game with equivalent mechanics and original presentation.

## 2. Design Goals

- Build a simple, readable mobile puzzle that can scale to 100+ levels.
- Keep the rules easy to learn but deep enough for deduction.
- Make every level solvable through logic, not guessing.
- Use an original kawaii neko presentation, not copied UI, art, level layouts, or branding from another game.
- Keep the puzzle engine separate from Unity UI so levels can be generated and tested automatically.
- Match the reference game's expected mobile features closely enough that players understand it immediately.

## 3. Core Rules

Each level has a square board of size `N x N`.

1. Each color region hides exactly 1 cat.
2. Each row hides exactly 1 cat.
3. Each column hides exactly 1 cat.
4. Hidden cats cannot touch each other horizontally, vertically, or diagonally.

The level is solved when all correct cat cells are revealed and every rule is satisfied.

## 4. Player Actions

### Tap / Drag

Marks cells that cannot contain cats.

Current prototype display:

- `X` means the player believes no cat is hidden there.
- Dragging paints multiple X marks.
- Tapping an X again can clear it.
- Revealed cells cannot be changed.

### Double Tap

Commits a cat placement and checks it against the hidden solution.

Reveal outcomes:

- Correct cell: reveal a cat.
- Wrong cell: show a miss state and remove one heart.

Current prototype display:

- A neko cat icon means a hidden cat was revealed.
- Red `X` means a wrong committed placement.

### Undo

Undo restores the previous board state, including marks and committed placements.

Recommended rule:

- Undo should not refund a heart after a wrong committed placement. This keeps hearts meaningful.

### Hint

Hint points the player toward the next useful deduction without automatically solving the board.

## 5. Win And Failure Model

Recommended first release model:

- Player starts each level with 3 hearts.
- A wrong double-tap committed placement removes 1 heart.
- At 0 hearts, show a failed state with restart and optional rewarded-ad continue.
- Star rating rewards clean solves.

Suggested level result:

- 3 stars: solved with 0 mistakes.
- 2 stars: solved with 1-2 mistakes.
- 1 star: solved after continuing or using all hearts.

This keeps the game friendly for casual mobile players while still rewarding careful deduction.

## 6. Board And Level Format

Each level should store:

- Level ID
- Board size
- Color/region map
- Hidden cat solution positions
- Difficulty rating
- Starting locked cats, if any
- Optional target par/mistake limit
- Optional tutorial hint text

Example conceptual structure:

```json
{
  "id": 1,
  "title": "Sprout Garden",
  "size": 5,
  "regions": [
    0, 0, 0, 1, 3,
    0, 2, 1, 1, 3,
    0, 2, 1, 1, 3,
    2, 2, 4, 4, 3,
    4, 4, 4, 3, 3
  ],
  "solution": [
    [0, 0],
    [1, 3],
    [2, 1],
    [3, 4],
    [4, 2]
  ],
  "lockedCats": [
    [0, 0]
  ],
  "difficulty": "normal"
}
```

## 7. Level Progression

Initial full content target: 100 levels.

Recommended split:

- Levels 1-35: Normal, mostly `5x5` and simple `6x6`
- Levels 36-75: Hard, mostly `6x6` and `7x7`
- Levels 76-100: Ultra, harder `7x7` and `8x8`

Difficulty should be based on solver logic, not just board size. A `6x6` with tricky colors can be harder than a simple `7x7`.

## 8. Required Level Validation

Every level must pass automated checks:

- Exactly `N` hidden cats.
- Exactly 1 cat per row.
- Exactly 1 cat per column.
- Exactly 1 cat per color region.
- No cats touch horizontally, vertically, or diagonally.
- The level has one unique solution.
- The level can be solved without guessing.

The first prototype can validate uniqueness. Later, the solver should also classify deduction difficulty.

## 9. Screen Flow

### Launch

For the prototype, launch directly into the current level.

For production:

- Splash/loading
- Home screen
- Level select
- Gameplay
- Win screen

### Gameplay Screen

Required elements:

- Level title/number
- Board
- Found cat counter
- Mistake counter
- Undo button
- Restart button
- Previous/next level buttons for development only

Production buttons:

- Hint
- Undo
- Restart
- Settings

### Level Select

Required elements:

- 100 level buttons
- Locked/unlocked state
- Star rating state
- Current progress marker

## 10. Visual Direction

Theme: kawaii neko, soft, playful, readable.

Recommended style:

- Rounded but clean mobile UI
- Pastel region colors with enough contrast
- Cute cat card reveals
- Soft card flip animation
- Gentle feedback for mistakes
- Small celebratory win animation

Avoid:

- Copying another game's cat art, UI layout, icon, name, colors, screenshots, sounds, or level maps.
- Overly noisy backgrounds that make the puzzle hard to read.
- Text-heavy tutorial screens.

## 11. Audio Direction

Suggested sounds:

- Soft tap for marking
- Brush-like sound for drag crosses
- Happy chime when a cat is revealed
- Muted puff when an empty card is revealed
- Short win jingle

Audio should be optional and controlled from settings.

## 12. Hints

Hints should teach deduction instead of revealing the answer immediately.

Hint stages:

1. Highlight a row, column, or color region with useful information.
2. Highlight a smaller set of candidate cells.
3. Reveal a safe cross or a hidden cat only if the player asks again.

This makes hints feel fair and preserves puzzle value.

## 13. Save Data

Save locally:

- Highest unlocked level
- Stars per level
- Current level board state
- Hearts remaining
- Mistake count
- Settings: sound, music, haptics

Cloud save can be added later if needed.

## 14. Monetization Options

Recommended later, not in the core prototype:

- Rewarded ad to continue after losing all hearts.
- Rewarded ad for an extra hint.
- Optional remove-ads purchase.
- Optional cosmetic cat card packs.

Do not block basic level progress behind ads.

## 15. Prototype Scope

Current target:

- 15 playable prototype levels
- Hidden cats already placed by level data
- Tap/drag cross marks
- Double-tap commit/reveal cat placement
- 3-heart mistake system
- Rule conflict preview from marked/revealed cats
- Found cat counter
- Undo/restart

This is enough to test whether the mechanic feels good before building the 100-level content pipeline.

## 16. Milestones

### Milestone 1 - Core Prototype

- Hidden-cat board model
- 15 prototype levels
- Tap, double-tap, drag controls
- Basic mobile UI
- Rule validation

### Milestone 2 - Level System

- JSON or ScriptableObject level format
- Level loader
- Level select
- Progress save

### Milestone 3 - Generator And Solver

- Generate valid boards
- Validate unique solutions
- Rate difficulty
- Export 100 levels

### Milestone 4 - Mobile Polish

- Final kawaii neko art
- Card reveal animation
- Haptics
- Sound effects
- Better win screen

### Milestone 5 - Store Readiness

- App icon
- App Store screenshots
- Privacy policy
- Android/iOS build settings
- Analytics and crash reporting if needed

## 17. Open Decisions

- Final game name
- Exact cat art style
- Whether color regions must be connected shapes
- Whether empty reveals are mistakes, lives, or only feedback
- Whether levels unlock sequentially or all at once
- Whether to include ads in the first release
