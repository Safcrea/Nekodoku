// Port of Assets/_GameData/Systems/Scripts/Meowdoku/Editor/NekoLevelValidator.cs
// (plus the shape checks LevelData.ToLevel performs in C#). Both implementations
// must agree on every level; tests/validate-goldens.mjs guards that parity
// against the game's full exported level set.

import { regionAt } from "./model.js";

export const BLOCKED_TARGET_WORDS = new Set([
    "SEX",
    "SEXY",
    "NUDE",
    "NAKED",
    "DRUGS",
    "HATE",
    "KILL",
    "DEATH",
    "BLOOD",
]);

/**
 * Structural checks, mirroring NekoLevelValidator.FindStructuralIssues.
 * Returns an array of human-readable problems; empty means structurally sound
 * (solvability is checked separately by countLegalSolutions).
 */
export function findStructuralIssues(level) {
    const issues = [];
    const { size, title } = level;
    const label = title || "<untitled>";

    if (size < 3) {
        issues.push(`${label}: size ${size} is too small.`);
    }

    if (level.regions.length !== size || level.regions.some((row) => row.length !== size)) {
        issues.push(`${label}: region map is not ${size}x${size}.`);
        return issues; // Everything below indexes into the region grid.
    }

    for (let row = 0; row < size; row++) {
        if (!/^[0-9]+$/.test(level.regions[row])) {
            issues.push(`${label}: region row ${row} has a non-digit character.`);
            return issues;
        }
    }

    if (level.cats.length !== size) {
        issues.push(`${label}: has ${level.cats.length} cats but size is ${size}.`);
    }

    addTargetWordIssues(level, label, issues);
    addRegionIssues(level, label, issues);
    addCatIssues(level, label, issues);
    return issues;
}

function addTargetWordIssues(level, label, issues) {
    const word = level.cats.map((cat) => cat.letter).join("");
    if (word.length === 0) {
        issues.push(`${label}: missing a target word.`);
        return;
    }

    if (word.length !== level.size) {
        issues.push(`${label}: target word length must match board size.`);
    }

    if (word.length >= 13) {
        issues.push(`${label}: target word must be shorter than 13 letters.`);
    }

    if (BLOCKED_TARGET_WORDS.has(word)) {
        issues.push(`${label}: target word is blocked.`);
    }

    if (!/^[A-Z]*$/.test(word)) {
        issues.push(`${label}: target word must contain A-Z letters only.`);
    }
}

function addRegionIssues(level, label, issues) {
    const { size } = level;
    const seen = new Array(size).fill(false);

    for (let row = 0; row < size; row++) {
        for (let column = 0; column < size; column++) {
            const region = regionAt(level, row, column);
            if (region < 0 || region >= size) {
                issues.push(`${label}: has a region id outside 0..${size - 1}.`);
                return;
            }

            seen[region] = true;
        }
    }

    for (let region = 0; region < size; region++) {
        if (!seen[region]) {
            issues.push(`${label}: missing region ${region}.`);
            continue;
        }

        if (!isRegionConnected(level, region)) {
            issues.push(`${label}: region ${region} is split across the board.`);
        }
    }
}

function addCatIssues(level, label, issues) {
    const { size } = level;
    const rows = new Array(size).fill(false);
    const columns = new Array(size).fill(false);
    const regions = new Array(size).fill(false);

    for (let i = 0; i < level.cats.length; i++) {
        const cat = level.cats[i];
        if (cat.row < 0 || cat.row >= size || cat.column < 0 || cat.column >= size) {
            issues.push(`${label}: has a solution cat outside the board.`);
            continue;
        }

        if (rows[cat.row]) {
            issues.push(`${label}: has more than one cat in row ${cat.row}.`);
        }

        if (columns[cat.column]) {
            issues.push(`${label}: has more than one cat in column ${cat.column}.`);
        }

        const region = regionAt(level, cat.row, cat.column);
        if (region >= 0 && region < size) {
            if (regions[region]) {
                issues.push(`${label}: has more than one cat in region ${region}.`);
            }

            regions[region] = true;
        }

        rows[cat.row] = true;
        columns[cat.column] = true;

        for (let j = i + 1; j < level.cats.length; j++) {
            const other = level.cats[j];
            const touches = Math.abs(cat.row - other.row) <= 1 && Math.abs(cat.column - other.column) <= 1;
            if (touches) {
                issues.push(`${label}: has touching cats at ${cat.row},${cat.column} and ${other.row},${other.column}.`);
            }
        }
    }
}

/** 4-neighbour flood fill, mirroring NekoLevelValidator.IsRegionConnected. */
export function isRegionConnected(level, region) {
    const { size } = level;
    const visited = new Array(size * size).fill(false);
    const queue = [];
    let regionCellCount = 0;

    for (let row = 0; row < size; row++) {
        for (let column = 0; column < size; column++) {
            if (regionAt(level, row, column) !== region) {
                continue;
            }

            regionCellCount++;
            if (queue.length === 0) {
                visited[row * size + column] = true;
                queue.push([row, column]);
            }
        }
    }

    let connected = 0;
    while (queue.length > 0) {
        const [row, column] = queue.pop();
        connected++;
        tryVisit(row - 1, column);
        tryVisit(row + 1, column);
        tryVisit(row, column - 1);
        tryVisit(row, column + 1);
    }

    return connected === regionCellCount;

    function tryVisit(row, column) {
        if (row < 0 || row >= size || column < 0 || column >= size) {
            return;
        }

        const index = row * size + column;
        if (visited[index] || regionAt(level, row, column) !== region) {
            return;
        }

        visited[index] = true;
        queue.push([row, column]);
    }
}

/**
 * Web-editor extension (no C# counterpart): when the region layout admits
 * exactly one legal solution, returns its column per row; otherwise null.
 * Powers the "auto-place cats" button.
 */
export function findUniqueSolutionColumns(level) {
    const { size } = level;
    const columnsByRow = new Array(size).fill(0);
    const usedColumns = new Array(size).fill(false);
    const usedRegions = new Array(size).fill(false);
    let found = null;
    let solutionCount = 0;

    search(0);
    return solutionCount === 1 ? found : null;

    function search(row) {
        if (solutionCount >= 2) {
            return;
        }

        if (row === size) {
            solutionCount++;
            found = columnsByRow.slice();
            return;
        }

        for (let column = 0; column < size; column++) {
            if (usedColumns[column]) {
                continue;
            }

            if (row > 0 && Math.abs(columnsByRow[row - 1] - column) <= 1) {
                continue;
            }

            const region = regionAt(level, row, column);
            if (usedRegions[region]) {
                continue;
            }

            columnsByRow[row] = column;
            usedColumns[column] = true;
            usedRegions[region] = true;

            search(row + 1);

            usedColumns[column] = false;
            usedRegions[region] = false;
        }
    }
}

/**
 * Backtracking solution counter, mirroring NekoLevelValidator.CountLegalSolutions.
 * Stops early once `limit` solutions are found (pass 2 to test uniqueness).
 */
export function countLegalSolutions(level, limit) {
    const { size } = level;
    const columnsByRow = new Array(size).fill(0);
    const usedColumns = new Array(size).fill(false);
    const usedRegions = new Array(size).fill(false);
    let solutionCount = 0;

    search(0);
    return solutionCount;

    function search(row) {
        if (solutionCount >= limit) {
            return;
        }

        if (row === size) {
            solutionCount++;
            return;
        }

        for (let column = 0; column < size; column++) {
            if (usedColumns[column]) {
                continue;
            }

            if (row > 0 && Math.abs(columnsByRow[row - 1] - column) <= 1) {
                continue;
            }

            const region = regionAt(level, row, column);
            if (usedRegions[region]) {
                continue;
            }

            columnsByRow[row] = column;
            usedColumns[column] = true;
            usedRegions[region] = true;

            search(row + 1);

            usedColumns[column] = false;
            usedRegions[region] = false;
        }
    }
}
