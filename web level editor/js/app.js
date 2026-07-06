import {
    MIN_SIZE,
    MAX_SIZE,
    CURRENT_FORMAT_VERSION,
    parseLevelJson,
    serializeLevel,
    slugify,
} from "./core/model.js";
import {
    findStructuralIssues,
    countLegalSolutions,
    findUniqueSolutionColumns,
} from "./core/validator.js";
import { generateLevel } from "./core/generator.js";

// Region colors mirror NekoGameController.RegionColors so painted boards read
// the same here and in the game.
const REGION_COLORS = [
    "#ffb7c2", "#abe0c4", "#9eccf5", "#ffd194", "#c9b5ed",
    "#f5e88c", "#ed9e80", "#87d1d9", "#bde08f",
];

const INTRO_DIAGONAL_DELAY_MS = 45; // Matches the game's diagonal pop-in sweep.

const state = {
    title: "New Level",
    id: "",
    size: 5,
    word: "",
    regions: [],
    /** @type {({row: number, column: number, locked: boolean} | null)[]} one entry per letter */
    placements: [],
    mode: "regions",
    selectedRegion: 0,
    selectedLetter: 0,
};

const el = {
    themeSwitch: document.getElementById("theme-switch"),
    title: document.getElementById("title-input"),
    id: document.getElementById("id-input"),
    size: document.getElementById("size-input"),
    sizeValue: document.getElementById("size-value"),
    word: document.getElementById("word-input"),
    seed: document.getElementById("seed-input"),
    randomSeedBtn: document.getElementById("random-seed-btn"),
    generateBtn: document.getElementById("generate-btn"),
    modeToolbar: document.getElementById("mode-toolbar"),
    regionsControls: document.getElementById("regions-controls"),
    catsControls: document.getElementById("cats-controls"),
    swatchRow: document.getElementById("swatch-row"),
    letterRow: document.getElementById("letter-row"),
    lockedToggle: document.getElementById("locked-toggle"),
    board: document.getElementById("board"),
    statusRow: document.getElementById("status-row"),
    issueList: document.getElementById("issue-list"),
    autoPlaceBtn: document.getElementById("auto-place-btn"),
    jsonArea: document.getElementById("json-area"),
    downloadBtn: document.getElementById("download-btn"),
    copyBtn: document.getElementById("copy-btn"),
    loadJsonBtn: document.getElementById("load-json-btn"),
    openFileBtn: document.getElementById("open-file-btn"),
    fileInput: document.getElementById("file-input"),
    newBtn: document.getElementById("new-btn"),
};

// ---------- state helpers ----------

function resetLevel(size) {
    state.title = "New Level";
    state.id = "";
    state.size = size;
    state.word = "";
    state.regions = Array.from({ length: size }, () => "0".repeat(size));
    state.placements = new Array(size).fill(null);
    state.selectedRegion = 0;
    state.selectedLetter = 0;
}

function setSize(newSize) {
    newSize = Math.max(MIN_SIZE, Math.min(MAX_SIZE, newSize));
    const old = state.size;
    state.regions = Array.from({ length: newSize }, (_, row) => {
        const oldRow = row < old ? state.regions[row] : "";
        return (oldRow + "0".repeat(newSize)).slice(0, newSize);
    });

    const placements = new Array(newSize).fill(null);
    for (let i = 0; i < Math.min(old, newSize); i++) {
        const p = state.placements[i];
        if (p && p.row < newSize && p.column < newSize) {
            placements[i] = p;
        }
    }

    state.placements = placements;
    state.size = newSize;
    state.word = state.word.slice(0, newSize);
    state.selectedRegion = Math.min(state.selectedRegion, newSize - 1);
    state.selectedLetter = Math.min(state.selectedLetter, newSize - 1);
}

function setRegion(row, column, region) {
    const chars = state.regions[row].split("");
    chars[column] = String(region);
    state.regions[row] = chars.join("");
}

function placementIndexAt(row, column) {
    return state.placements.findIndex((p) => p !== null && p.row === row && p.column === column);
}

/** Builds a game-format LevelData, or reports why one can't be built yet. */
function tryBuildLevel() {
    if (state.title.trim() === "") {
        return { error: "Title is required." };
    }

    if (state.word.length !== state.size) {
        return { error: `Target word must be exactly ${state.size} letters.` };
    }

    const cats = [];
    for (let i = 0; i < state.size; i++) {
        const p = state.placements[i];
        if (p === null) {
            return { error: `Letter ${i + 1} ('${state.word[i]}') has no cat placed yet.` };
        }

        cats.push({ row: p.row, column: p.column, letter: state.word[i], locked: p.locked });
    }

    return {
        level: {
            formatVersion: CURRENT_FORMAT_VERSION,
            id: state.id.trim() === "" ? slugify(state.title) : state.id.trim(),
            title: state.title.trim(),
            size: state.size,
            regions: state.regions.slice(),
            cats,
        },
    };
}

function regionsAreInRange() {
    return state.regions.every((row) => [...row].every((digit) => Number(digit) < state.size));
}

/** Loads a parsed/generated level into the editor and replays the board intro. */
function applyLevelData(level) {
    state.title = level.title;
    state.id = level.id;
    state.size = level.size;
    state.regions = level.regions.map((row) => (row + "0".repeat(level.size)).slice(0, level.size));
    state.word = level.cats.map((cat) => cat.letter).join("").slice(0, level.size);
    state.placements = Array.from({ length: level.size }, (_, i) => {
        const cat = level.cats[i];
        return cat ? { row: cat.row, column: cat.column, locked: cat.locked } : null;
    });
    state.selectedRegion = 0;
    state.selectedLetter = 0;
    pendingIntro = true;
    renderAll();
}

// ---------- rendering ----------

function renderAll() {
    el.title.value = state.title;
    el.id.value = state.id;
    el.size.value = String(state.size);
    el.sizeValue.textContent = `${state.size}×${state.size}`;
    el.word.value = state.word;
    el.word.maxLength = state.size;

    for (const button of el.modeToolbar.querySelectorAll("button")) {
        button.classList.toggle("active", button.dataset.mode === state.mode);
    }

    el.regionsControls.hidden = state.mode !== "regions";
    el.catsControls.hidden = state.mode !== "cats";

    renderSwatches();
    renderLetterRow();
    renderBoard();
    renderValidation();
}

function renderSwatches() {
    el.swatchRow.replaceChildren();
    for (let region = 0; region < state.size; region++) {
        const swatch = document.createElement("button");
        swatch.className = "swatch" + (state.selectedRegion === region ? " active" : "");
        swatch.style.background = REGION_COLORS[region % REGION_COLORS.length];
        swatch.textContent = String(region);
        swatch.title = `Region ${region}`;
        swatch.addEventListener("click", () => {
            state.selectedRegion = region;
            renderAll();
        });
        el.swatchRow.append(swatch);
    }
}

function renderLetterRow() {
    el.letterRow.replaceChildren();
    for (let i = 0; i < state.size; i++) {
        const slot = document.createElement("button");
        slot.className = "letter-slot"
            + (state.selectedLetter === i ? " active" : "")
            + (state.placements[i] !== null ? " placed" : "");
        slot.textContent = state.word[i] ?? "?";
        slot.title = `Letter ${i + 1}`;
        if (state.placements[i]?.locked) {
            const dot = document.createElement("span");
            dot.className = "lock-dot";
            slot.append(dot);
        }

        slot.addEventListener("click", () => {
            state.selectedLetter = i;
            renderAll();
        });
        el.letterRow.append(slot);
    }

    el.lockedToggle.checked = state.placements[state.selectedLetter]?.locked ?? false;
    el.lockedToggle.disabled = state.placements[state.selectedLetter] === null;
}

// The board rebuilds (with the pop-in sweep) only on structural changes -
// size change, new, import, generate. Everything else updates cells in place,
// which keeps drag-painting smooth and lets pulse animations play out.

let boardCells = [];
let builtBoardSize = 0;
let pendingIntro = true;

const ANIMATION_CLASS_BY_NAME = {
    "cell-pop": "intro",
    "cell-jelly": "painted",
    "cat-pop": "cat-pop",
};

function renderBoard() {
    if (builtBoardSize !== state.size || pendingIntro) {
        rebuildBoard();
        pendingIntro = false;
        return;
    }

    for (let row = 0; row < state.size; row++) {
        for (let column = 0; column < state.size; column++) {
            applyCellVisual(row, column);
        }
    }
}

function rebuildBoard() {
    builtBoardSize = state.size;
    const cellSize = Math.min(56, Math.floor(470 / state.size));
    el.board.style.gridTemplateColumns = `repeat(${state.size}, ${cellSize}px)`;
    el.board.style.setProperty("--cell-size", `${cellSize}px`);
    el.board.replaceChildren();
    boardCells = [];

    for (let row = 0; row < state.size; row++) {
        const rowCells = [];
        for (let column = 0; column < state.size; column++) {
            const cell = document.createElement("button");
            cell.className = "cell intro";
            cell.dataset.row = String(row);
            cell.dataset.column = String(column);
            cell.style.animationDelay = `${(row + column) * INTRO_DIAGONAL_DELAY_MS}ms`;
            cell.addEventListener("animationend", (event) => {
                const className = ANIMATION_CLASS_BY_NAME[event.animationName];
                if (className) {
                    cell.classList.remove(className);
                    if (className === "intro") {
                        cell.style.animationDelay = "";
                    }
                }
            });
            el.board.append(cell);
            rowCells.push(cell);
            applyCellVisual(row, column);
        }

        boardCells.push(rowCells);
    }
}

function applyCellVisual(row, column) {
    const cell = boardCells[row]?.[column];
    if (!cell) {
        return;
    }

    const region = Number(state.regions[row][column]);
    cell.style.background = REGION_COLORS[region % REGION_COLORS.length];

    const catIndex = placementIndexAt(row, column);
    cell.textContent = catIndex >= 0 ? state.word[catIndex] ?? "?" : "";
    cell.classList.toggle(
        "selected-letter",
        state.mode === "cats" && catIndex >= 0 && catIndex === state.selectedLetter
    );

    if (catIndex >= 0 && state.placements[catIndex].locked) {
        const dot = document.createElement("span");
        dot.className = "lock-dot";
        dot.title = "starts revealed";
        cell.append(dot);
    }
}

function pulseCell(row, column, className) {
    const cell = boardCells[row]?.[column];
    if (cell) {
        cell.classList.remove(className);
        void cell.offsetWidth; // Restart the animation if it's mid-flight.
        cell.classList.add(className);
    }
}

function renderValidation() {
    el.statusRow.replaceChildren();
    el.issueList.replaceChildren();

    const solutions = regionsAreInRange()
        ? countLegalSolutions({ size: state.size, regions: state.regions }, 2)
        : null;

    if (solutions !== null) {
        const chip = document.createElement("span");
        if (solutions === 1) {
            chip.className = "chip ok";
            chip.innerHTML = `<span class="dot"></span> unique solution`;
        } else if (solutions === 0) {
            chip.className = "chip danger";
            chip.innerHTML = `<span class="dot"></span> no solution`;
        } else {
            chip.className = "chip warn";
            chip.innerHTML = `<span class="dot"></span> 2+ solutions`;
        }

        el.statusRow.append(chip);
    }

    const built = tryBuildLevel();
    const issues = built.level ? findStructuralIssues(built.level) : [];

    const readyChip = document.createElement("span");
    if (built.level && issues.length === 0 && solutions === 1) {
        readyChip.className = "chip ok";
        readyChip.innerHTML = `<span class="dot"></span> valid — ready to ship`;
    } else {
        readyChip.className = "chip";
        readyChip.innerHTML = `<span class="dot"></span> in progress`;
    }

    el.statusRow.append(readyChip);

    if (built.error) {
        const item = document.createElement("li");
        item.className = "info";
        item.textContent = built.error;
        el.issueList.append(item);
    }

    for (const issue of issues) {
        const item = document.createElement("li");
        item.textContent = issue;
        el.issueList.append(item);
    }

    el.autoPlaceBtn.disabled = solutions !== 1 || state.word.length !== state.size;
    if (document.activeElement !== el.jsonArea) {
        el.jsonArea.value = built.level && issues.length === 0 ? serializeLevel(built.level) : "";
    }
}

// ---------- board interaction ----------

let painting = false;

function cellFromEvent(event) {
    const target = document.elementFromPoint(event.clientX, event.clientY);
    const cell = target instanceof Element ? target.closest(".cell") : null;
    if (!cell) {
        return null;
    }

    return { row: Number(cell.dataset.row), column: Number(cell.dataset.column) };
}

function paintCell(row, column) {
    setRegion(row, column, state.selectedRegion);
    applyCellVisual(row, column);
    pulseCell(row, column, "painted");
    renderValidation();
}

el.board.addEventListener("pointerdown", (event) => {
    const cell = cellFromEvent(event);
    if (!cell) {
        return;
    }

    event.preventDefault();
    if (state.mode === "regions") {
        painting = true;
        el.board.setPointerCapture(event.pointerId);
        paintCell(cell.row, cell.column);
    } else {
        onCatCellClicked(cell.row, cell.column);
    }
});

el.board.addEventListener("pointermove", (event) => {
    if (!painting) {
        return;
    }

    const cell = cellFromEvent(event);
    if (cell && Number(state.regions[cell.row][cell.column]) !== state.selectedRegion) {
        paintCell(cell.row, cell.column);
    }
});

el.board.addEventListener("pointerup", () => {
    painting = false;
});

el.board.addEventListener("pointercancel", () => {
    painting = false;
});

function onCatCellClicked(row, column) {
    const existing = placementIndexAt(row, column);
    let placed = false;
    if (existing >= 0 && existing !== state.selectedLetter) {
        // Clicking someone else's cat selects that letter instead of stacking.
        state.selectedLetter = existing;
    } else if (existing === state.selectedLetter) {
        state.placements[state.selectedLetter] = null;
    } else {
        const locked = state.placements[state.selectedLetter]?.locked ?? false;
        state.placements[state.selectedLetter] = { row, column, locked };
        placed = true;
        const next = state.placements.findIndex((p) => p === null);
        if (next >= 0) {
            state.selectedLetter = next;
        }
    }

    renderAll();
    if (placed) {
        pulseCell(row, column, "cat-pop");
    }
}

// ---------- form events ----------

el.title.addEventListener("input", () => {
    state.title = el.title.value;
    renderValidation();
});

el.id.addEventListener("input", () => {
    state.id = el.id.value;
    renderValidation();
});

el.size.addEventListener("input", () => {
    setSize(Number(el.size.value));
    renderAll();
});

el.word.addEventListener("input", () => {
    const clean = el.word.value.toUpperCase().replace(/[^A-Z]/g, "").slice(0, state.size);
    state.word = clean;
    renderAll();
});

el.modeToolbar.addEventListener("click", (event) => {
    const button = event.target instanceof Element ? event.target.closest("button[data-mode]") : null;
    if (button) {
        state.mode = button.dataset.mode;
        renderAll();
    }
});

el.lockedToggle.addEventListener("change", () => {
    const placement = state.placements[state.selectedLetter];
    if (placement) {
        placement.locked = el.lockedToggle.checked;
        renderAll();
    }
});

el.autoPlaceBtn.addEventListener("click", () => {
    const columns = findUniqueSolutionColumns({ size: state.size, regions: state.regions });
    if (!columns) {
        return;
    }

    state.placements = columns.map((column, row) => ({
        row,
        column,
        locked: state.placements[row]?.locked ?? false,
    }));
    renderAll();
    for (let row = 0; row < state.size; row++) {
        pulseCell(row, columns[row], "cat-pop");
    }
});

// ---------- generator ----------

function randomSeed() {
    return Math.random().toString(36).slice(2, 8);
}

el.randomSeedBtn.addEventListener("click", () => {
    el.seed.value = randomSeed();
});

el.generateBtn.addEventListener("click", () => {
    let seed = el.seed.value.trim();
    if (seed === "") {
        seed = randomSeed();
        el.seed.value = seed;
    }

    try {
        applyLevelData(generateLevel(seed, state.size));
    } catch (error) {
        alert(error.message);
    }
});

// ---------- file actions ----------

el.downloadBtn.addEventListener("click", () => {
    const built = tryBuildLevel();
    if (!built.level) {
        alert(built.error);
        return;
    }

    const blob = new Blob([serializeLevel(built.level)], { type: "application/json" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = `${built.level.id}.json`;
    link.click();
    URL.revokeObjectURL(link.href);
});

el.copyBtn.addEventListener("click", async () => {
    const built = tryBuildLevel();
    if (!built.level) {
        alert(built.error);
        return;
    }

    await navigator.clipboard.writeText(serializeLevel(built.level));
});

el.loadJsonBtn.addEventListener("click", () => {
    importJson(el.jsonArea.value);
});

el.openFileBtn.addEventListener("click", () => el.fileInput.click());

el.fileInput.addEventListener("change", async () => {
    const file = el.fileInput.files?.[0];
    if (file) {
        importJson(await file.text());
        el.fileInput.value = "";
    }
});

el.newBtn.addEventListener("click", () => {
    if (confirm("Discard the current level and start fresh?")) {
        resetLevel(5);
        pendingIntro = true;
        renderAll();
    }
});

function importJson(text) {
    if (text.trim() === "") {
        alert("Paste level JSON into the text area first.");
        return;
    }

    let level;
    try {
        level = parseLevelJson(text);
    } catch (error) {
        alert(error.message);
        return;
    }

    if (level.size < MIN_SIZE || level.size > MAX_SIZE) {
        alert(`This editor supports sizes ${MIN_SIZE}-${MAX_SIZE}; the file has size ${level.size}.`);
        return;
    }

    applyLevelData(level);
}

// ---------- theme ----------

function applyTheme(choice) {
    if (choice === "light" || choice === "dark") {
        document.documentElement.dataset.theme = choice;
        localStorage.setItem("meowdoku-theme", choice);
    } else {
        delete document.documentElement.dataset.theme;
        localStorage.removeItem("meowdoku-theme");
    }

    const current = localStorage.getItem("meowdoku-theme") ?? "auto";
    for (const button of el.themeSwitch.querySelectorAll("button")) {
        button.classList.toggle("active", button.dataset.themeChoice === current);
    }
}

el.themeSwitch.addEventListener("click", (event) => {
    const button = event.target instanceof Element ? event.target.closest("button[data-theme-choice]") : null;
    if (button) {
        applyTheme(button.dataset.themeChoice);
    }
});

// ---------- boot ----------

resetLevel(5);
applyTheme(localStorage.getItem("meowdoku-theme") ?? "auto");
renderAll();
