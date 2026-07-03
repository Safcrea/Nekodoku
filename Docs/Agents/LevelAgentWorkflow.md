# Level Agent Workflow

This project uses two Codex custom agents for level content:

- `Meowdoku Level Designer` in `.codex/agents/level-designer.toml`
- `Meowdoku Level QA` in `.codex/agents/level-qa.toml`

## Flow

1. The Level Designer creates original candidate levels in the current project format.
2. The Level Designer hands off each candidate with static validation notes and deduction notes.
3. The Level QA agent validates rule correctness, checks gameplay risks, and reports pass/fail findings.
4. A level is accepted only after QA passes it or all blocking findings are fixed.

## Current Gate

The project now has an EditMode level-data QA test in `Assets/Scripts/Meowdoku/Editor/NekoSampleLevelTests.cs`. It checks static rules and brute-force unique solutions for `NekoSampleLevels`.

This is not a human-style deduction solver or difficulty classifier. No-guessing and difficulty claims must stay marked as unverified until the Milestone 3 solver exists.

## Acceptance Checklist

- Exactly one cat per row.
- Exactly one cat per column.
- Exactly one cat per region.
- No touching cats, including diagonals.
- Region map has `size * size` entries.
- Solution has `size` entries.
- Region IDs use `0..size-1`, and every expected region exists.
- Locked cats, if any, are valid solution cats.
- Level is original and not copied from the reference game.
- QA report has no blocking findings.
