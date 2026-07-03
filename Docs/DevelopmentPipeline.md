# Development Pipeline

## 1. Product Direction

Build an original mobile kawaii neko logic puzzle with feature parity to the reference mechanic:

- One cat per color region.
- One cat per row and column.
- Cats cannot touch, including diagonally.
- Tap/drag X marks for impossible cells.
- Double-tap commits a cat placement.
- Wrong committed placement costs one heart.
- 3 hearts per level.
- Hint, undo, restart, level select, difficulty tiers, daily puzzle, save progress.

Do not copy the reference game's name, art, UI layout, level maps, store screenshots, logo, sounds, text, or branding.

## 2. Workstreams

### Gameplay Logic

Owner: Gameplay agent  
Output: deterministic, testable C# puzzle model.

Responsibilities:

- Board state
- Hidden solution state
- X marks
- Cat commits
- Locked starter cats
- Hearts and mistake state
- Win/fail state
- Undo rules
- Hint candidate API

Quality gate:

- EditMode tests pass.
- No gameplay rule is implemented only in UI code.

### Level Pipeline

Owner: Level Designer agent + Level QA agent  
Output: validated level files.

Responsibilities:

- Hand-authored level candidates
- Level schema
- Solver
- Unique-solution validation
- Difficulty rating
- 100-level export
- Daily puzzle seed flow

Quality gate:

- Level QA report has no blocking findings.
- Every level has exactly one legal solution.
- Every level passes row, column, color, and no-touch checks.
- Generated levels are not copied from the reference game.

### Unity App

Owner: Unity UI agent  
Output: mobile-ready playable app.

Responsibilities:

- Scene bootstrap
- Board rendering
- Touch input
- Level select
- Game HUD
- Win/fail screens
- Settings
- Local save/load

Quality gate:

- Portrait mobile layout works at common phone aspect ratios.
- No UI text overlap.
- PlayMode smoke test passes.

### Art And Audio

Owner: Art/audio agent  
Output: original assets.

Responsibilities:

- Original cat card style
- Region color palette
- Buttons and icons
- Card reveal animation
- Haptics
- Sound effects
- Win/fail polish

Quality gate:

- Assets are original or licensed.
- Visuals are readable for color-heavy puzzle play.
- Colorblind support has a planned path.

### Monetization And Store

Owner: Release agent  
Output: store-ready build package.

Responsibilities:

- Ads integration
- Optional remove-ads IAP
- Privacy policy inputs
- App icon
- Store screenshots
- App Store / Google Play metadata
- Build signing checklist

Quality gate:

- Ads do not interrupt active board play.
- Privacy disclosures match actual SDK behavior.
- Store assets use only our original art and screenshots.

## 3. Milestone Plan

### Milestone 0 - Project Hygiene

Status: in progress

Deliverables:

- Development pipeline document
- Unity `.gitignore`
- Git LFS rules for large binary assets
- Current backlog document

Exit criteria:

- Project can be safely put under Git without committing `Library`, `Temp`, build outputs, or logs.

### Milestone 1 - Correct Core Prototype

Deliverables:

- 15 playable prototype levels
- Tap/drag X marks
- Double-tap commit cat
- 3-heart mistake system
- Locked starter cat support
- Undo/restart
- Win/fail state

Exit criteria:

- Prototype behavior matches the GDD.
- C# compile succeeds.
- Manual playthrough of all 3 levels succeeds.

### Milestone 2 - Level Data System

Deliverables:

- Level schema
- Level files stored outside hard-coded C# arrays
- Runtime level loader
- Level select
- Save progress

Exit criteria:

- Adding a level does not require editing gameplay code.
- Level select can load all available levels.

### Milestone 3 - Solver And Generator

Deliverables:

- Solver library
- Unique-solution validator
- Difficulty classifier
- Level generator
- Batch validation report

Exit criteria:

- 100 original levels generated and validated.
- Validation report shows zero invalid levels.

### Milestone 4 - Mobile UX Polish

Deliverables:

- Final board layout
- Final HUD
- Card reveal animation
- Haptics
- Original cat/card art
- Sound effects
- Settings screen

Exit criteria:

- Phone screenshots pass visual QA.
- Gameplay is readable on small screens.

### Milestone 5 - Release Candidate

Deliverables:

- Android build
- iOS build settings
- Ad/IAP integration if approved
- Store icon
- Store screenshots
- Privacy policy content
- QA checklist

Exit criteria:

- Release build installs and plays through.
- No known blocking bugs.

## 4. Branch And Source Control Flow

Use Git before the project grows further.

Recommended branches:

- `main`: stable builds only.
- `develop`: current integration branch.
- `feature/<short-name>`: one focused change.
- `content/levels-<range>`: generated or curated level batches.
- `release/<version>`: store candidate stabilization.

Commit rules:

- Keep gameplay, UI, art, and generated-level changes separate when practical.
- Commit Unity `.meta` files with their assets.
- Do not commit `Library`, `Temp`, `Logs`, `Builds`, or generated local caches.
- Do not commit store certificates, signing keys, passwords, or API secrets.

## 5. Folder Structure

Target structure:

```text
Assets/
  Art/
    Cats/
    UI/
    Effects/
  Audio/
    Music/
    Sfx/
  Data/
    Levels/
    Daily/
  Prefabs/
    UI/
    Board/
  Scenes/
  Scripts/
    Meowdoku/
      Core/
      Levels/
      UI/
      Save/
      Tests/
Docs/
  GameDesignDocument.md
  DevelopmentPipeline.md
  CurrentBacklog.md
Builds/
```

The current prototype can stay in `Assets/Scripts/Meowdoku` until Milestone 2, then split into these folders.

## 6. Development Loop

For each change:

1. Read the relevant GDD/pipeline section.
2. Inspect current code and scene state.
3. Make a focused change.
4. Compile C#.
5. Run relevant tests when they exist.
6. Manually play the affected flow.
7. Update docs if behavior changed.

Unity verification:

- After script edits, wait for Unity compile.
- Check console errors.
- Capture screenshots for UI changes.
- Test at phone-like aspect ratios.

## 7. Testing Strategy

### EditMode Tests

Required for:

- Rule validation
- Hearts
- Undo
- Level parsing
- Solver
- Generator
- Save data serialization

### PlayMode Tests

Required for:

- Loading first scene
- Loading a level
- Completing a level
- Failing from 3 wrong commits
- Restarting a level
- Navigating level select

### Manual QA

Required for:

- Touch feel
- Drag crossing
- Double-tap reliability
- Small-screen layout
- Color readability
- Sound/haptic feel

## 8. Level Generation Pipeline

1. Generate a valid hidden cat solution.
2. Generate color regions around the solution.
3. Validate static rules.
4. Run solver.
5. Reject if there is no unique solution.
6. Score difficulty.
7. Export level data.
8. Run batch validation.
9. Add level to level pack.
10. Playtest a sample from each difficulty bucket.

Level packs:

- `normal_001_035`
- `hard_036_075`
- `ultra_076_100`
- `daily_seeded`

## 9. Definition Of Done

A feature is done only when:

- It matches the GDD.
- It compiles without errors.
- Relevant tests pass or a manual QA note exists.
- It works on portrait mobile layout.
- It does not copy protected reference assets or level content.
- Documentation/backlog is updated if behavior or scope changed.

## 10. Current Technical Notes

- Unity version: 6000.2.14f1.
- UI package: uGUI is installed.
- Input System package is installed.
- Current prototype builds its UI at runtime.
- Current project is not a Git repository yet.
- Current levels are hard-coded in C# and should move to data files in Milestone 2.

## 11. Immediate Next Steps

1. Update prototype to the committed-placement model from the GDD.
2. Add 3-heart fail state.
3. Add locked starter cat support.
4. Move levels to data files.
5. Add first EditMode tests for rules and hearts.
6. Start solver/generator after the runtime level schema is stable.
