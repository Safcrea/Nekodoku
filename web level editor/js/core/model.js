// Mirror of Assets/_GameData/Systems/Scripts/Meowdoku/Levels/LevelData.cs.
// The JSON produced here must stay byte-compatible with Unity's JsonUtility
// output (4-space indent, field order: formatVersion, id, title, size,
// regions, cats / row, column, letter, locked).

export const CURRENT_FORMAT_VERSION = 1;
export const MIN_SIZE = 3;
export const MAX_SIZE = 9;

/**
 * @typedef {{ row: number, column: number, letter: string, locked: boolean }} Cat
 * @typedef {{ formatVersion: number, id: string, title: string, size: number, regions: string[], cats: Cat[] }} LevelData
 */

/** Creates a blank level of the given size (all cells region 0, no cats placed). */
export function createEmptyLevel(size) {
    return {
        formatVersion: CURRENT_FORMAT_VERSION,
        id: "",
        title: "New Level",
        size,
        regions: Array.from({ length: size }, () => "0".repeat(size)),
        cats: [],
    };
}

/**
 * Parses and shape-checks raw JSON text. Throws with a readable message on
 * malformed input; puzzle-rule problems are the validator's job, not this one.
 * @returns {LevelData}
 */
export function parseLevelJson(text) {
    let raw;
    try {
        raw = JSON.parse(text);
    } catch (error) {
        throw new Error(`Not valid JSON: ${error.message}`);
    }

    if (typeof raw !== "object" || raw === null) {
        throw new Error("Level JSON must be an object.");
    }

    const size = Number(raw.size);
    if (!Number.isInteger(size) || size < MIN_SIZE) {
        throw new Error(`Level size must be an integer of at least ${MIN_SIZE}.`);
    }

    if (!Array.isArray(raw.regions) || raw.regions.some((row) => typeof row !== "string")) {
        throw new Error("Level regions must be an array of row strings.");
    }

    if (!Array.isArray(raw.cats)) {
        throw new Error("Level cats must be an array.");
    }

    const cats = raw.cats.map((cat, index) => {
        if (typeof cat !== "object" || cat === null) {
            throw new Error(`Cat ${index} is not an object.`);
        }

        return {
            row: Number(cat.row),
            column: Number(cat.column),
            letter: String(cat.letter ?? "").toUpperCase(),
            locked: Boolean(cat.locked),
        };
    });

    return {
        formatVersion: Number(raw.formatVersion ?? CURRENT_FORMAT_VERSION),
        id: String(raw.id ?? ""),
        title: String(raw.title ?? ""),
        size,
        regions: raw.regions.slice(),
        cats,
    };
}

/** Serializes to the exact shape Unity's JsonUtility writes. */
export function serializeLevel(level) {
    const ordered = {
        formatVersion: level.formatVersion,
        id: level.id,
        title: level.title,
        size: level.size,
        regions: level.regions,
        cats: level.cats.map((cat) => ({
            row: cat.row,
            column: cat.column,
            letter: cat.letter,
            locked: cat.locked,
        })),
    };
    return JSON.stringify(ordered, null, 4);
}

/** The word the level spells, derived from cat order (may contain gaps while editing). */
export function targetWord(level) {
    return level.cats.map((cat) => cat.letter || "?").join("");
}

export function regionAt(level, row, column) {
    return level.regions[row].charCodeAt(column) - 48;
}

export function slugify(value) {
    const slug = (value ?? "")
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "");
    return slug === "" ? "level" : slug;
}
