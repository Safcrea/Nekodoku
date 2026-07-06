// Parity guard: the JS validator must agree with the C# one on every level the
// game ships. Run with:  node Website/tests/validate-goldens.mjs
// Fails if any exported level has structural issues, lacks a unique solution,
// or does not round-trip byte-identically through the JS serializer.

import { readdir, readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

import { parseLevelJson, serializeLevel } from "../js/core/model.js";
import { findStructuralIssues, countLegalSolutions } from "../js/core/validator.js";

const levelsDir = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../../Assets/_GameData/Systems/Data/Levels"
);

const files = (await readdir(levelsDir)).filter((name) => name.endsWith(".json")).sort();
if (files.length === 0) {
    console.error(`No level JSON files found in ${levelsDir}. Run the Unity exporter first.`);
    process.exit(1);
}

let failures = 0;
for (const file of files) {
    const text = await readFile(path.join(levelsDir, file), "utf8");
    const problems = [];

    let level;
    try {
        level = parseLevelJson(text);
    } catch (error) {
        problems.push(`parse failed: ${error.message}`);
    }

    if (level) {
        problems.push(...findStructuralIssues(level));

        const solutions = countLegalSolutions(level, 2);
        if (solutions !== 1) {
            problems.push(`expected exactly 1 legal solution, found ${solutions === 2 ? "2+" : solutions}`);
        }

        if (serializeLevel(level) !== text.trim()) {
            problems.push("serializer output differs from Unity's JsonUtility output");
        }
    }

    if (problems.length > 0) {
        failures++;
        console.error(`FAIL ${file}`);
        for (const problem of problems) {
            console.error(`  - ${problem}`);
        }
    }
}

if (failures > 0) {
    console.error(`\n${failures}/${files.length} levels failed.`);
    process.exit(1);
}

console.log(`OK: all ${files.length} levels are structurally valid, uniquely solvable, and round-trip byte-identically.`);
