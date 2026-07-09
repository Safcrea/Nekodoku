// Seeded procedural level generator (web-only; no C# counterpart yet).
//
// Two entry points share the same first two steps:
//   1. Place a legal cat solution (one per row/column, no touching) by
//      seeded backtracking.
//   2. Grow the N regions outward from the N cat cells (Eden growth, biased
//      toward snaky shapes), guaranteeing every region is connected and holds
//      exactly one cat.
//
// generateLevel() grows regions freely (whatever size the Eden growth happens
// to produce), then repairs by hill climbing: flip random boundary cells
// between regions (never a cat cell, never disconnecting the donor region),
// accepting flips that do not increase the number of legal solutions, until
// the solver confirms the solution is unique.
//
// generateLevelWithRegionSizes() is the editor's guided flow: the caller picks
// exactly how many cells each of the N colors gets (summing to size*size).
// Growth is weighted toward whichever region is furthest below target (never
// hard-capped, so it can't dead-end), which lands close to the split but
// rarely exact; repairPreservingSizes then reaches uniqueness first (ignoring
// sizes) and closes the remaining size gap incrementally, one region-pair
// flip at a time, re-confirming uniqueness after each one. The final level
// always matches the requested split exactly.
//
// Random layouts on large boards admit thousands of solutions, so pure retry
// is hopeless there - the repair walk is what makes 8x8/9x9 generation
// reliable. The same seed (+ size, + region sizes) always produces the same
// level.

import { CURRENT_FORMAT_VERSION, slugify } from "./model.js";

export const GENERATOR_MIN_SIZE = 4; // 3x3 has no legal layout under the rules.
export const GENERATOR_MAX_SIZE = 9;

const SOLUTION_COUNT_CAP = 48;
const REPAIR_STEP_CAP = 2200;
const ATTEMPT_CAP = 8;
const TARGET_SIZE_ATTEMPT_CAP = 40;
const COMBINED_REPAIR_STEP_CAP = 3000;
const SNAKY_GROWTH_BIAS = 0.7;

const TITLE_ADJECTIVES = [
    "Porch", "Window", "Sunny", "Cozy", "Fuzzy", "Ribbon",
    "Shelf", "Laser", "Treat", "Plush", "Whisker", "Felix",
    "Cloud", "Garden", "Velvet", "Calico", "Kitten", "Moonlit",
    "Purring", "Hidden", "Silver", "Golden", "Sleepy", "Bright",
];

const TITLE_NOUNS = [
    "Patrol", "Watch", "Steps", "Trail", "Print", "Route",
    "Scout", "Lane", "Map", "Pile", "Way", "Field",
    "Cloud", "Nap", "Path", "Keys", "Cove", "Nook",
    "Den", "Loft", "Dash", "Drift", "Puzzle", "Grove",
];

/** Deterministic PRNG (xmur3 string hash feeding mulberry32). */
export function createRng(seedString) {
    let h = 1779033703 ^ seedString.length;
    for (let i = 0; i < seedString.length; i++) {
        h = Math.imul(h ^ seedString.charCodeAt(i), 3432918353);
        h = (h << 13) | (h >>> 19);
    }

    let a = (() => {
        h = Math.imul(h ^ (h >>> 16), 2246822507);
        h = Math.imul(h ^ (h >>> 13), 3266489909);
        return (h ^= h >>> 16) >>> 0;
    })();

    return function mulberry32() {
        a = (a + 0x6d2b79f5) | 0;
        let t = Math.imul(a ^ (a >>> 15), 1 | a);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

/**
 * Generates a complete, uniquely-solvable level.
 * @throws when the size is unsupported or no unique layout is found in time.
 */
export function generateLevel(seed, size, attemptCap = ATTEMPT_CAP) {
    if (size < GENERATOR_MIN_SIZE || size > GENERATOR_MAX_SIZE) {
        throw new Error(
            size === 3
                ? "3×3 boards have no legal layout (the middle row's cat always conflicts) — use 4×4 or larger."
                : `Generator supports sizes ${GENERATOR_MIN_SIZE}-${GENERATOR_MAX_SIZE}.`
        );
    }

    const rng = createRng(`${seed}:${size}`);
    for (let attempt = 0; attempt < attemptCap; attempt++) {
        const solutionColumns = placeSolution(rng, size);
        const grid = growRegions(rng, size, solutionColumns);
        if (!repairToUniqueSolution(rng, size, grid, solutionColumns)) {
            continue;
        }

        const title = `${pick(rng, TITLE_ADJECTIVES)} ${pick(rng, TITLE_NOUNS)}`;
        const lockedRows = pickLockedRows(rng, size);
        const cats = solutionColumns.map((column, row) => ({
            row,
            column,
            locked: lockedRows.has(row),
        }));

        return {
            formatVersion: CURRENT_FORMAT_VERSION,
            id: `${slugify(String(seed))}-${size}x${size}`,
            title,
            size,
            regions: grid.map((row) => row.join("")),
            cats,
        };
    }

    throw new Error(`No unique-solution layout found for seed "${seed}" at ${size}×${size} — try another seed.`);
}

/** Even split of size*size cells across the size colors (remainder goes to the first colors). */
export function defaultRegionSizes(size) {
    const total = size * size;
    const base = Math.floor(total / size);
    const remainder = total % size;
    return Array.from({ length: size }, (_, i) => base + (i < remainder ? 1 : 0));
}

/**
 * Generates a complete, uniquely-solvable level whose N regions hold exactly
 * `regionSizes[r]` cells each (region r is always the one seeded on row r's
 * cat - see growRegionsSoftTarget/balanceRegionSizes). regionSizes must have `size` entries,
 * each >= 1, summing to size*size.
 * @param {object} [options]
 * @param {number} [options.attemptCap]
 * @param {Iterable<number>} [options.lockedRows] - explicit 0-based row/cat
 *   indices to mark "starts revealed". Omit to fall back to the default
 *   random starter-clue curve (pickLockedRows).
 * @throws when the size/sizes/lockedRows are invalid, or no unique layout is
 *   found in time.
 */
export function generateLevelWithRegionSizes(seed, size, regionSizes, options = {}) {
    const { attemptCap = TARGET_SIZE_ATTEMPT_CAP, lockedRows } = options;

    if (size < GENERATOR_MIN_SIZE || size > GENERATOR_MAX_SIZE) {
        throw new Error(
            size === 3
                ? "3×3 boards have no legal layout (the middle row's cat always conflicts) — use 4×4 or larger."
                : `Generator supports sizes ${GENERATOR_MIN_SIZE}-${GENERATOR_MAX_SIZE}.`
        );
    }

    if (!Array.isArray(regionSizes) || regionSizes.length !== size) {
        throw new Error(`Need exactly ${size} color cell counts (one per color), got ${regionSizes?.length ?? 0}.`);
    }

    if (regionSizes.some((count) => !Number.isInteger(count) || count < 1)) {
        throw new Error("Each color needs at least 1 cell.");
    }

    const total = regionSizes.reduce((sum, count) => sum + count, 0);
    if (total !== size * size) {
        throw new Error(`Color cell counts add up to ${total}, but a ${size}×${size} board has ${size * size} cells.`);
    }

    let explicitLockedRows = null;
    if (lockedRows !== undefined) {
        explicitLockedRows = new Set(lockedRows);
        for (const row of explicitLockedRows) {
            if (!Number.isInteger(row) || row < 0 || row >= size) {
                throw new Error(`Locked cat index ${row} is outside 0..${size - 1}.`);
            }
        }
    }

    const rng = createRng(`${seed}:${size}:${regionSizes.join(",")}`);
    for (let attempt = 0; attempt < attemptCap; attempt++) {
        const solutionColumns = placeSolution(rng, size);
        const grid = growRegionsSoftTarget(rng, size, solutionColumns, regionSizes);
        if (!repairPreservingSizes(rng, size, grid, solutionColumns, regionSizes)) {
            continue;
        }

        const title = `${pick(rng, TITLE_ADJECTIVES)} ${pick(rng, TITLE_NOUNS)}`;
        const lockedRowSet = explicitLockedRows ?? pickLockedRows(rng, size);
        const cats = solutionColumns.map((column, row) => ({
            row,
            column,
            locked: lockedRowSet.has(row),
        }));

        return {
            formatVersion: CURRENT_FORMAT_VERSION,
            id: `${slugify(String(seed))}-${size}x${size}`,
            title,
            size,
            regions: grid.map((row) => row.join("")),
            cats,
        };
    }

    throw new Error(`Couldn't fit that color split for seed "${seed}" at ${size}×${size} — try different counts or another seed.`);
}

/** Default starter-clue count for a given board size, matching pickLockedRows's
 *  curve - exposed so the UI can pre-fill its own locked-cat count control. */
export function defaultLockedCatCount(size) {
    return size <= 5 ? 2 : size <= 8 ? 1 : 0;
}

/** One cat per row/column, no two in adjacent cells. Always succeeds for size >= 4. */
function placeSolution(rng, size) {
    const columns = new Array(size).fill(-1);
    const used = new Array(size).fill(false);

    const found = search(0);
    if (!found) {
        throw new Error(`Internal error: no legal cat placement exists for size ${size}.`);
    }

    return columns;

    function search(row) {
        if (row === size) {
            return true;
        }

        for (const column of shuffledIndices(rng, size)) {
            if (used[column]) {
                continue;
            }

            if (row > 0 && Math.abs(columns[row - 1] - column) <= 1) {
                continue;
            }

            columns[row] = column;
            used[column] = true;
            if (search(row + 1)) {
                return true;
            }

            used[column] = false;
        }

        columns[row] = -1;
        return false;
    }
}

/**
 * Eden growth: region r starts on row r's cat cell, then unassigned frontier
 * cells join a random adjacent region until the board is covered. Growth is
 * biased toward cells touching exactly one region, which produces snaky
 * region shapes that admit far fewer alternative solutions than round blobs.
 */
function growRegions(rng, size, solutionColumns) {
    const grid = Array.from({ length: size }, () => new Array(size).fill(-1));
    for (let row = 0; row < size; row++) {
        grid[row][solutionColumns[row]] = row;
    }

    let unassigned = size * size - size;
    while (unassigned > 0) {
        const frontier = [];
        const tips = [];
        for (let row = 0; row < size; row++) {
            for (let column = 0; column < size; column++) {
                if (grid[row][column] !== -1) {
                    continue;
                }

                const neighbors = neighborRegions(grid, size, row, column);
                if (neighbors.length === 0) {
                    continue;
                }

                const cell = { row, column, neighbors };
                frontier.push(cell);
                if (neighbors.length === 1) {
                    tips.push(cell);
                }
            }
        }

        const pool = tips.length > 0 && rng() < SNAKY_GROWTH_BIAS ? tips : frontier;
        const cell = pool[Math.floor(rng() * pool.length)];
        grid[cell.row][cell.column] = cell.neighbors[Math.floor(rng() * cell.neighbors.length)];
        unassigned--;
    }

    return grid;
}

/**
 * Same Eden growth as growRegions, but the region a frontier cell joins is
 * weighted toward whichever neighbouring region has the most budget left
 * under regionSizes (never hard-excluded, just unlikely once it's full) -
 * unlike a hard per-region cap, this never dead-ends growth itself, since
 * every frontier cell always has at least one pickable neighbour. The result
 * lands close to the requested split but rarely exact; repairPreservingSizes
 * below closes the remaining gap while also reaching a unique solution.
 */
function growRegionsSoftTarget(rng, size, solutionColumns, regionSizes) {
    const grid = Array.from({ length: size }, () => new Array(size).fill(-1));
    const counts = new Array(size).fill(1); // Each cat cell already counts as 1.
    for (let row = 0; row < size; row++) {
        grid[row][solutionColumns[row]] = row;
    }

    let unassigned = size * size - size;
    while (unassigned > 0) {
        const frontier = [];
        const tips = [];
        for (let row = 0; row < size; row++) {
            for (let column = 0; column < size; column++) {
                if (grid[row][column] !== -1) {
                    continue;
                }

                const neighbors = neighborRegions(grid, size, row, column);
                if (neighbors.length === 0) {
                    continue;
                }

                const cell = { row, column, neighbors };
                frontier.push(cell);
                if (neighbors.length === 1) {
                    tips.push(cell);
                }
            }
        }

        const pool = tips.length > 0 && rng() < SNAKY_GROWTH_BIAS ? tips : frontier;
        const cell = pool[Math.floor(rng() * pool.length)];
        const region = pickRegionWeightedByRemainingBudget(rng, cell.neighbors, counts, regionSizes);
        grid[cell.row][cell.column] = region;
        counts[region]++;
        unassigned--;
    }

    return grid;
}

function pickRegionWeightedByRemainingBudget(rng, regions, counts, regionSizes) {
    const MIN_WEIGHT = 0.15; // Keeps a region pickable (just unlikely) once it's past target.
    const weights = regions.map((region) => Math.max(MIN_WEIGHT, regionSizes[region] - counts[region]));
    const totalWeight = weights.reduce((sum, weight) => sum + weight, 0);
    let x = rng() * totalWeight;
    for (let i = 0; i < regions.length; i++) {
        x -= weights[i];
        if (x <= 0) {
            return regions[i];
        }
    }

    return regions[regions.length - 1];
}

function countRegionSizes(grid, size, regionCount) {
    const counts = new Array(regionCount).fill(0);
    for (let row = 0; row < size; row++) {
        for (let column = 0; column < size; column++) {
            counts[grid[row][column]]++;
        }
    }

    return counts;
}

function totalAbsDeviation(counts, regionSizes) {
    let total = 0;
    for (let i = 0; i < counts.length; i++) {
        total += Math.abs(counts[i] - regionSizes[i]);
    }

    return total;
}

/** How much totalAbsDeviation would change if one cell moved from region
 *  `from` to region `to`. Negative/zero means the move helps or is neutral. */
function flipDeviationDelta(counts, regionSizes, from, to) {
    const before = Math.abs(counts[from] - regionSizes[from]) + Math.abs(counts[to] - regionSizes[to]);
    const after = Math.abs(counts[from] - 1 - regionSizes[from]) + Math.abs(counts[to] + 1 - regionSizes[to]);
    return after - before;
}

/**
 * Reaches a layout that is simultaneously uniquely-solvable AND exactly
 * regionSizes: one hill climb over legal flips using exactly
 * repairToUniqueSolution's own acceptance rule (never let the solution count
 * increase - see the comment inline below for why that rule, not a stricter
 * one, is what actually converges), where the only difference is *which*
 * candidate flip gets tried at each step: randomLegalFlipPreferringDeviation
 * weights the search toward candidates that also close the size gap, without
 * ever excluding the rest. Two stricter variants were tried first and both
 * failed empirically: gating acceptance on deviation as well as solution
 * count gets stuck a couple of solutions short of unique (it blocks the
 * lateral, same-count wandering the plain algorithm relies on); alternating
 * a full uniqueness repair with a separate full size-balancing pass reliably
 * re-multiplied solutions back into the double digits every time the size
 * pass ran, since that pass had no visibility into solution count at all.
 */
function repairPreservingSizes(rng, size, grid, solutionColumns, regionSizes) {
    let count = countSolutionsOnGrid(grid, size, SOLUTION_COUNT_CAP);
    let counts = countRegionSizes(grid, size, regionSizes.length);
    let deviation = totalAbsDeviation(counts, regionSizes);

    for (let step = 0; step < COMBINED_REPAIR_STEP_CAP && (count > 1 || deviation > 0); step++) {
        const flip = randomLegalFlipPreferringDeviation(rng, size, grid, solutionColumns, counts, regionSizes);
        if (flip === null) {
            break;
        }

        const from = grid[flip.row][flip.column];
        const to = flip.toRegion;
        const deviationDelta = flipDeviationDelta(counts, regionSizes, from, to);

        grid[flip.row][flip.column] = to;
        const newCount = countSolutionsOnGrid(grid, size, SOLUTION_COUNT_CAP);

        // While not yet unique, this is exactly repairToUniqueSolution's own
        // acceptance rule (count must not increase, including *lateral*
        // same-count moves - what lets it wander out of a solution-count
        // plateau; gating on deviation too here, tried first, blocked that
        // wandering and got stuck forever a couple of solutions short of
        // unique). Once count has reached 1, lateral moves flip to requiring
        // deviationDelta <= 0 too - otherwise a lateral move (still legal,
        // still keeps count at 1) has no reason to ever prefer a
        // deviation-improving direction over a worsening one, so deviation
        // just randomly walks forever instead of settling at 0. That still
        // gets stuck once every remaining deviation-improving flip would
        // (even briefly) cost uniqueness - a real dead end for a purely
        // greedy climb, needing a multi-flip detour through a worse state to
        // get out. So once count===1, a flip that would cost uniqueness by
        // exactly one extra solution is also accepted, but rarely
        // (EXCURSION_PROBABILITY) and only when it meaningfully helps
        // deviation - simulated-annealing-style noise to escape the plateau;
        // the outer attempt-retry loop covers excursions that don't recover.
        const EXCURSION_PROBABILITY = 0.08;
        const isRecoverableExcursion = count === 1 && newCount > count && deviationDelta < 0 && rng() < EXCURSION_PROBABILITY;
        const accept = newCount < count || (newCount === count && (count > 1 || deviationDelta <= 0)) || isRecoverableExcursion;
        if (accept) {
            count = newCount;
            counts[from]--;
            counts[to]++;
            deviation += deviationDelta;
        } else {
            grid[flip.row][flip.column] = from;
        }
    }

    return count === 1 && deviation === 0;
}

/**
 * Same legal-flip search as randomLegalFlip, but candidates that would reduce
 * (or hold steady) the size deviation are weighted more likely to be offered
 * first - a soft nudge only. Every legal candidate stays in the pool
 * regardless of its size impact, so this never blocks the plain uniqueness
 * search from reaching moves it needs.
 */
function randomLegalFlipPreferringDeviation(rng, size, grid, solutionColumns, counts, regionSizes) {
    const DEVIATION_PREFERRED_WEIGHT = 5;
    const candidates = [];
    for (let row = 0; row < size; row++) {
        for (let column = 0; column < size; column++) {
            if (solutionColumns[row] === column) {
                continue; // Cat cells anchor their region.
            }

            const from = grid[row][column];
            for (const toRegion of neighborRegionsOfAssigned(grid, size, row, column)) {
                if (toRegion !== from) {
                    candidates.push({ row, column, toRegion });
                }
            }
        }
    }

    while (candidates.length > 0) {
        const weights = candidates.map((candidate) =>
            flipDeviationDelta(counts, regionSizes, grid[candidate.row][candidate.column], candidate.toRegion) <= 0
                ? DEVIATION_PREFERRED_WEIGHT
                : 1
        );
        const totalWeight = weights.reduce((sum, weight) => sum + weight, 0);
        let x = rng() * totalWeight;
        let index = 0;
        for (; index < weights.length - 1; index++) {
            x -= weights[index];
            if (x <= 0) {
                break;
            }
        }

        const flip = candidates[index];
        if (donorStaysConnected(grid, size, flip.row, flip.column)) {
            return flip;
        }

        candidates.splice(index, 1);
    }

    return null;
}

/**
 * Hill-climbing repair: random boundary-cell flips between regions, accepted
 * when the (capped) solution count does not increase. Returns true once the
 * layout has exactly one legal solution.
 */
function repairToUniqueSolution(rng, size, grid, solutionColumns) {
    let count = countSolutionsOnGrid(grid, size, SOLUTION_COUNT_CAP);
    for (let step = 0; step < REPAIR_STEP_CAP && count > 1; step++) {
        const flip = randomLegalFlip(rng, size, grid, solutionColumns);
        if (flip === null) {
            break;
        }

        const previousRegion = grid[flip.row][flip.column];
        grid[flip.row][flip.column] = flip.toRegion;
        const newCount = countSolutionsOnGrid(grid, size, SOLUTION_COUNT_CAP);
        if (newCount <= count) {
            count = newCount;
        } else {
            grid[flip.row][flip.column] = previousRegion;
        }
    }

    return count === 1;
}

/** A random flip of one boundary cell to a neighbouring region that keeps the
 *  donor region connected and never moves a cat's own cell. */
function randomLegalFlip(rng, size, grid, solutionColumns) {
    const candidates = [];
    for (let row = 0; row < size; row++) {
        for (let column = 0; column < size; column++) {
            if (solutionColumns[row] === column) {
                continue; // Cat cells anchor their region.
            }

            const from = grid[row][column];
            for (const toRegion of neighborRegionsOfAssigned(grid, size, row, column)) {
                if (toRegion !== from) {
                    candidates.push({ row, column, toRegion });
                }
            }
        }
    }

    while (candidates.length > 0) {
        const index = Math.floor(rng() * candidates.length);
        const flip = candidates[index];
        if (donorStaysConnected(grid, size, flip.row, flip.column)) {
            return flip;
        }

        candidates.splice(index, 1);
    }

    return null;
}

function donorStaysConnected(grid, size, row, column) {
    const region = grid[row][column];
    let start = null;
    let regionCells = 0;
    for (let r = 0; r < size; r++) {
        for (let c = 0; c < size; c++) {
            if (grid[r][c] !== region || (r === row && c === column)) {
                continue;
            }

            regionCells++;
            if (start === null) {
                start = [r, c];
            }
        }
    }

    if (start === null) {
        return false; // Would empty the region entirely.
    }

    const visited = new Set([start[0] * size + start[1]]);
    const queue = [start];
    while (queue.length > 0) {
        const [r, c] = queue.pop();
        for (const [nr, nc] of [[r - 1, c], [r + 1, c], [r, c - 1], [r, c + 1]]) {
            if (nr < 0 || nr >= size || nc < 0 || nc >= size) {
                continue;
            }

            if (nr === row && nc === column) {
                continue;
            }

            const key = nr * size + nc;
            if (grid[nr][nc] === region && !visited.has(key)) {
                visited.add(key);
                queue.push([nr, nc]);
            }
        }
    }

    return visited.size === regionCells;
}

/** Same search as the shared validator's countLegalSolutions, but reading an
 *  int grid directly so the repair loop avoids string conversions. */
function countSolutionsOnGrid(grid, size, limit) {
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

            const region = grid[row][column];
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

function neighborRegions(grid, size, row, column) {
    const regions = [];
    for (const [r, c] of [[row - 1, column], [row + 1, column], [row, column - 1], [row, column + 1]]) {
        if (r >= 0 && r < size && c >= 0 && c < size && grid[r][c] !== -1 && !regions.includes(grid[r][c])) {
            regions.push(grid[r][c]);
        }
    }

    return regions;
}

function neighborRegionsOfAssigned(grid, size, row, column) {
    return neighborRegions(grid, size, row, column);
}

/** Starter-clue curve matching the game's feel: more help on small boards. */
function pickLockedRows(rng, size) {
    const count = defaultLockedCatCount(size);
    const rows = new Set();
    while (rows.size < count) {
        rows.add(Math.floor(rng() * size));
    }

    return rows;
}

function pick(rng, list) {
    return list[Math.floor(rng() * list.length)];
}

function shuffledIndices(rng, count) {
    const indices = Array.from({ length: count }, (_, i) => i);
    for (let i = count - 1; i > 0; i--) {
        const j = Math.floor(rng() * (i + 1));
        [indices[i], indices[j]] = [indices[j], indices[i]];
    }

    return indices;
}
