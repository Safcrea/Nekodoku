// Seeded procedural level generator (web-only; no C# counterpart yet).
//
// Recipe:
//   1. Place a legal cat solution (one per row/column, no touching) by
//      seeded backtracking.
//   2. Grow the N regions outward from the N cat cells (Eden growth, biased
//      toward snaky shapes), guaranteeing every region is connected and holds
//      exactly one cat.
//   3. Repair by hill climbing: flip random boundary cells between regions
//      (never a cat cell, never disconnecting the donor region), accepting
//      flips that do not increase the number of legal solutions, until the
//      solver confirms the solution is unique. Random layouts on large boards
//      admit thousands of solutions, so pure retry is hopeless there - the
//      repair walk is what makes 8x8/9x9 generation reliable.
//
// The same seed + size always produces the same level.

import { CURRENT_FORMAT_VERSION, slugify } from "./model.js";

export const GENERATOR_MIN_SIZE = 4; // 3x3 has no legal layout under the rules.
export const GENERATOR_MAX_SIZE = 9;

const SOLUTION_COUNT_CAP = 48;
const REPAIR_STEP_CAP = 2200;
const ATTEMPT_CAP = 8;
const SNAKY_GROWTH_BIAS = 0.7;

const WORDS = {
    4: [
        "STAR", "FISH", "MOON", "LEAF", "MILK", "YARN", "NEST", "PLUM",
        "SOFA", "LAMP", "BIRD", "SNOW", "RAIN", "WIND", "CAKE", "MINT",
    ],
    5: [
        "APPLE", "RIVER", "LIGHT", "BREAD", "HOUSE", "PLANT",
        "STONE", "MUSIC", "WATER", "CLOUD", "FIELD", "TRAIN",
        "SMILE", "CHAIR", "GRASS", "BRAVE", "SHARP", "WORLD",
    ],
    6: [
        "PLANET", "BRIDGE", "GARDEN", "MARKET", "BUTTON", "POCKET",
        "CASTLE", "FOREST", "SILVER", "SUMMER", "WINTER", "ORANGE",
        "CANDLE", "ISLAND", "STREAM", "ROCKET", "PUZZLE", "BREEZE",
        "SCREEN", "TRAVEL", "SPRING", "FLOWER", "DESERT", "ANCHOR",
    ],
    7: [
        "JOURNEY", "BALANCE", "HARVEST", "MORNING", "PICTURE", "COUNTRY",
        "FREEDOM", "DIAMOND", "LIBRARY", "KITCHEN", "WEATHER", "THOUGHT",
        "LANTERN", "COMPASS", "VILLAGE", "CRYSTAL", "MACHINE", "KINGDOM",
        "HORIZON", "DYNAMIC", "VICTORY", "MYSTERY", "RESOLVE", "PATTERN",
    ],
    8: [
        "NOTEBOOK", "MOUNTAIN", "TREASURE", "DISTANCE", "FESTIVAL", "LANGUAGE",
        "QUESTION", "SUNLIGHT", "BASEMENT", "KEYBOARD", "AIRPLANE", "PAINTING",
        "MARATHON", "HOSPITAL", "CALENDAR", "BUILDING", "PASSPORT", "SANDWICH",
    ],
    9: [
        "ADVENTURE", "BLUEPRINT", "CHALLENGE", "DISCOVERY", "EDUCATION", "FIRELIGHT",
        "HAPPINESS", "IMPORTANT", "LANDSCAPE", "NOTEBOOKS", "OPERATION", "PLANETARY",
        "REFERENCE", "SOMETHING", "TELEPHONE", "UNDERLINE",
    ],
};

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

        const word = pick(rng, WORDS[size]);
        const title = `${pick(rng, TITLE_ADJECTIVES)} ${pick(rng, TITLE_NOUNS)}`;
        const lockedRows = pickLockedRows(rng, size);
        const cats = solutionColumns.map((column, row) => ({
            row,
            column,
            letter: word[row],
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
    const count = size <= 5 ? 2 : size <= 8 ? 1 : 0;
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
