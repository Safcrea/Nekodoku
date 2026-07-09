// Generator guard: every generated level must satisfy the shared validator
// (structural + unique solution), and the same seed must always reproduce the
// same level. Run with:  node Website/tests/generate-test.mjs

import {
    generateLevel,
    generateLevelWithRegionSizes,
    defaultRegionSizes,
    GENERATOR_MIN_SIZE,
    GENERATOR_MAX_SIZE,
} from "../js/core/generator.js";
import { serializeLevel } from "../js/core/model.js";
import { findStructuralIssues, countLegalSolutions } from "../js/core/validator.js";

const SEEDS_PER_SIZE = 25;
let failures = 0;
let generated = 0;

for (let size = GENERATOR_MIN_SIZE; size <= GENERATOR_MAX_SIZE; size++) {
    for (let i = 0; i < SEEDS_PER_SIZE; i++) {
        const seed = `test-seed-${i}`;
        let level;
        try {
            level = generateLevel(seed, size);
        } catch (error) {
            failures++;
            console.error(`FAIL ${size}x${size} "${seed}": ${error.message}`);
            continue;
        }

        generated++;
        const problems = findStructuralIssues(level);
        const solutions = countLegalSolutions(level, 2);
        if (solutions !== 1) {
            problems.push(`expected exactly 1 legal solution, found ${solutions === 2 ? "2+" : solutions}`);
        }

        const again = generateLevel(seed, size);
        if (serializeLevel(again) !== serializeLevel(level)) {
            problems.push("not deterministic: same seed produced a different level");
        }

        if (problems.length > 0) {
            failures++;
            console.error(`FAIL ${size}x${size} "${seed}"`);
            for (const problem of problems) {
                console.error(`  - ${problem}`);
            }
        }
    }
}

// The documented failure mode must stay a clear error, not a hang or bad level.
try {
    generateLevel("any", 3);
    failures++;
    console.error("FAIL: size 3 should be rejected (no legal layout exists).");
} catch {
    // expected
}

// ---- generateLevelWithRegionSizes: the editor's guided per-color-count flow ----

function regionCounts(level) {
    const counts = new Array(level.size).fill(0);
    for (const row of level.regions) {
        for (const digit of row) {
            counts[Number(digit)]++;
        }
    }

    return counts;
}

function checkRegionSizedLevel(label, size, regionSizes, level) {
    const problems = findStructuralIssues(level);
    const solutions = countLegalSolutions(level, 2);
    if (solutions !== 1) {
        problems.push(`expected exactly 1 legal solution, found ${solutions === 2 ? "2+" : solutions}`);
    }

    const counts = regionCounts(level);
    if (JSON.stringify(counts) !== JSON.stringify(regionSizes)) {
        problems.push(`region cell counts ${JSON.stringify(counts)} don't match requested ${JSON.stringify(regionSizes)}`);
    }

    if (problems.length > 0) {
        failures++;
        console.error(`FAIL ${label}`);
        for (const problem of problems) {
            console.error(`  - ${problem}`);
        }
    } else {
        generated++;
    }
}

// defaultRegionSizes itself: always size entries, always summing to size*size.
for (let size = GENERATOR_MIN_SIZE; size <= GENERATOR_MAX_SIZE; size++) {
    const sizes = defaultRegionSizes(size);
    if (sizes.length !== size || sizes.reduce((sum, n) => sum + n, 0) !== size * size) {
        failures++;
        console.error(`FAIL defaultRegionSizes(${size}): ${JSON.stringify(sizes)}`);
    }
}

// Even splits at small/medium sizes - this is the common case (a fresh editor
// session defaults to an even split) and must stay fast and reliable.
const EVEN_SPLIT_SEEDS = 6;
for (let size = GENERATOR_MIN_SIZE; size <= 6; size++) {
    const sizes = defaultRegionSizes(size);
    for (let i = 0; i < EVEN_SPLIT_SEEDS; i++) {
        const seed = `region-seed-${i}`;
        const label = `${size}x${size} even split, seed "${seed}"`;
        try {
            const level = generateLevelWithRegionSizes(seed, size, sizes);
            checkRegionSizedLevel(label, size, sizes, level);

            const again = generateLevelWithRegionSizes(seed, size, sizes);
            if (serializeLevel(again) !== serializeLevel(level)) {
                failures++;
                console.error(`FAIL ${label}\n  - not deterministic: same seed produced a different level`);
            }
        } catch (error) {
            failures++;
            console.error(`FAIL ${label}: ${error.message}`);
        }
    }
}

// Skewed splits at every supported size, including the largest boards - an
// even split gets combinatorially harder as boards grow (more symmetric
// layouts admit far more alternate solutions), but a skewed one - most cells
// in one or two colors, the rest thin slivers - stays fast even at 9x9, so
// this is what actually exercises the large-board path in reasonable time.
function skewedRegionSizes(size) {
    const sizes = new Array(size).fill(1);
    sizes[size - 1] = size * size - (size - 1);
    return sizes;
}

for (let size = GENERATOR_MIN_SIZE; size <= GENERATOR_MAX_SIZE; size++) {
    const sizes = skewedRegionSizes(size);
    const seed = "skewed-region-seed";
    const label = `${size}x${size} skewed split ${JSON.stringify(sizes)}, seed "${seed}"`;
    try {
        const level = generateLevelWithRegionSizes(seed, size, sizes);
        checkRegionSizedLevel(label, size, sizes, level);
    } catch (error) {
        failures++;
        console.error(`FAIL ${label}: ${error.message}`);
    }
}

// Rejects a split that doesn't add up to size*size, with a clear message
// rather than a bad level or a hang.
try {
    generateLevelWithRegionSizes("bad-split", 5, [1, 1, 1, 1, 1]);
    failures++;
    console.error("FAIL: a region-size split that doesn't sum to size*size should be rejected.");
} catch {
    // expected
}

if (failures > 0) {
    console.error(`\n${failures} failure(s).`);
    process.exit(1);
}

console.log(`OK: generated ${generated} levels across sizes ${GENERATOR_MIN_SIZE}-${GENERATOR_MAX_SIZE} (plain + region-sized); all valid, uniquely solvable, and deterministic.`);
