// Generator guard: every generated level must satisfy the shared validator
// (structural + unique solution), and the same seed must always reproduce the
// same level. Run with:  node Website/tests/generate-test.mjs

import { generateLevel, GENERATOR_MIN_SIZE, GENERATOR_MAX_SIZE } from "../js/core/generator.js";
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

if (failures > 0) {
    console.error(`\n${failures} failure(s).`);
    process.exit(1);
}

console.log(`OK: generated ${generated} levels across sizes ${GENERATOR_MIN_SIZE}-${GENERATOR_MAX_SIZE}; all valid, uniquely solvable, and deterministic.`);
