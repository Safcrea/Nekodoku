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
import { generateLevelWithRegionSizes, defaultRegionSizes, defaultLockedCatCount } from "./core/generator.js";

// Region colors mirror NekoGameController.RegionColors so painted boards read
// the same here and in the game. Which of these a given region actually
// displays as is a separate, purely-cosmetic layer - see
// state.regionColorIndices/colorForRegion.
const REGION_COLORS = [
    "#ffb7c2", "#abe0c4", "#9eccf5", "#ffd194", "#c9b5ed",
    "#f5e88c", "#ed9e80", "#87d1d9", "#bde08f",
];

/** Identity mapping (region r -> palette color r) - the starting point every
 *  time the board size changes, same as regionSizes/lockedCats resetting. */
function defaultRegionColorIndices(size) {
    return Array.from({ length: size }, (_, i) => i % REGION_COLORS.length);
}

function colorForRegion(region) {
    return REGION_COLORS[state.regionColorIndices[region] ?? region % REGION_COLORS.length];
}

const INTRO_DIAGONAL_DELAY_MS = 45; // Matches the game's diagonal pop-in sweep.

const state = {
    /** The level's title is no longer a directly-editable field - it comes from
     *  whichever of the two title sources last ran: the generator's own nice
     *  adjective+noun title after a Generate, or titleFromId() derived from the
     *  Id field for hand-painted levels (see titleFromId). */
    title: "New Level",
    id: "",
    size: 5,
    regions: [],
    /** @type {({row: number, column: number, locked: boolean} | null)[]} one entry per cat */
    placements: [],
    /** One cell-count target per color, edited in the Generate panel; must sum to size*size. */
    regionSizes: [],
    /** Pre-generate config: which cats (by index) should start revealed. Fully
     *  explicit - an unchecked box means "not locked", generate is never told
     *  to silently pick its own default the way it does when this option is
     *  omitted entirely (see generateLevelWithRegionSizes's lockedRows). */
    lockedCats: [],
    /** The count shown in the "Randomize" field - only consulted when that
     *  button is clicked, never implicitly. */
    lockRandomizeCount: 0,
    /** Which REGION_COLORS index each region displays as - a permutation of
     *  `size` distinct palette entries, purely cosmetic (never part of the
     *  exported JSON). Defaults to identity (region r -> palette color r);
     *  reassigned by clicking a swatch in the Colors & cell counts list. */
    regionColorIndices: [],
    mode: "regions",
    selectedRegion: 0,
    selectedCat: 0,
};

const el = {
    themeSwitch: document.getElementById("theme-switch"),
    id: document.getElementById("id-input"),
    size: document.getElementById("size-input"),
    sizeValue: document.getElementById("size-value"),
    seed: document.getElementById("seed-input"),
    randomSeedBtn: document.getElementById("random-seed-btn"),
    colorCountList: document.getElementById("color-count-list"),
    cellTotalRow: document.getElementById("cell-total-row"),
    evenSplitBtn: document.getElementById("even-split-btn"),
    randomizeCountsBtn: document.getElementById("randomize-counts-btn"),
    lockCatList: document.getElementById("lock-cat-list"),
    lockCountInput: document.getElementById("lock-count-input"),
    randomizeLocksBtn: document.getElementById("randomize-locks-btn"),
    randomizerToggle: document.getElementById("randomizer-toggle"),
    generateInfoBtn: document.getElementById("generate-info-btn"),
    generateInfoPopover: document.getElementById("generate-info-popover"),
    palettePopover: document.getElementById("palette-popover"),
    palettePopoverGrid: document.getElementById("palette-popover-grid"),
    generateBtn: document.getElementById("generate-btn"),
    modeToolbar: document.getElementById("mode-toolbar"),
    regionsControls: document.getElementById("regions-controls"),
    catsControls: document.getElementById("cats-controls"),
    swatchRow: document.getElementById("swatch-row"),
    catRow: document.getElementById("cat-row"),
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
    state.regions = Array.from({ length: size }, () => "0".repeat(size));
    state.placements = new Array(size).fill(null);
    state.regionSizes = defaultRegionSizes(size);
    state.lockedCats = new Array(size).fill(false);
    state.lockRandomizeCount = defaultLockedCatCount(size);
    state.regionColorIndices = defaultRegionColorIndices(size);
    state.selectedRegion = 0;
    state.selectedCat = 0;
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
    // A different board size invalidates any custom split, so start fresh
    // from an even one rather than trying to rescale it.
    state.regionSizes = defaultRegionSizes(newSize);
    // Same for the locked-cat picker - old picks may no longer be valid indices.
    state.lockedCats = new Array(newSize).fill(false);
    state.lockRandomizeCount = defaultLockedCatCount(newSize);
    // And for the color assignment - a custom swap for e.g. 5 colors doesn't
    // carry any obvious meaning at 9.
    state.regionColorIndices = defaultRegionColorIndices(newSize);
    state.selectedRegion = Math.min(state.selectedRegion, newSize - 1);
    state.selectedCat = Math.min(state.selectedCat, newSize - 1);
}

/** Derives a human-readable title from the Id field - the only title source
 *  for hand-painted levels now that there's no separate Title input (see
 *  state.title's comment). "porch-patrol" -> "Porch Patrol". */
function titleFromId(id) {
    const words = id.trim().split(/[-_\s]+/).filter(Boolean);
    return words.length === 0 ? "New Level" : words.map((word) => word[0].toUpperCase() + word.slice(1)).join(" ");
}

function setRegion(row, column, region) {
    const chars = state.regions[row].split("");
    chars[column] = String(region);
    state.regions[row] = chars.join("");
}

function placementIndexAt(row, column) {
    return state.placements.findIndex((p) => p !== null && p.row === row && p.column === column);
}

/** Actual per-region cell counts on the current board (used to seed the Generate panel after a load). */
function regionSizesFromState() {
    const counts = new Array(state.size).fill(0);
    for (const row of state.regions) {
        for (const digit of row) {
            const region = Number(digit);
            if (region >= 0 && region < state.size) {
                counts[region]++;
            }
        }
    }

    return counts;
}

/** Builds a game-format LevelData, or reports why one can't be built yet. */
function tryBuildLevel() {
    if (state.title.trim() === "") {
        return { error: "Title is required." };
    }

    const cats = [];
    for (let i = 0; i < state.size; i++) {
        const p = state.placements[i];
        if (p === null) {
            return { error: `Cat ${i + 1} has no location yet.` };
        }

        cats.push({ row: p.row, column: p.column, locked: p.locked });
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
    state.placements = Array.from({ length: level.size }, (_, i) => {
        const cat = level.cats[i];
        return cat ? { row: cat.row, column: cat.column, locked: cat.locked } : null;
    });
    state.regionSizes = regionSizesFromState();
    // Echoes back whichever cats actually ended up locked (from Generate's
    // explicit lockedRows, or whatever an imported file already had) so the
    // picker always reflects the board that's actually loaded.
    state.lockedCats = Array.from({ length: level.size }, (_, i) => level.cats[i]?.locked ?? false);
    state.lockRandomizeCount = state.lockedCats.filter(Boolean).length || defaultLockedCatCount(level.size);
    state.regionColorIndices = defaultRegionColorIndices(level.size);
    state.selectedRegion = 0;
    state.selectedCat = 0;
    pendingIntro = true;
    renderAll();
}

// ---------- rendering ----------

function renderAll() {
    el.id.value = state.id;
    el.size.value = String(state.size);
    el.sizeValue.textContent = `${state.size}×${state.size}`;

    for (const button of el.modeToolbar.querySelectorAll("button")) {
        button.classList.toggle("active", button.dataset.mode === state.mode);
    }

    el.regionsControls.hidden = state.mode !== "regions";
    el.catsControls.hidden = state.mode !== "cats";

    renderSwatches();
    renderCatRow();
    renderColorCounts();
    renderLockCatList();
    renderBoard();
    renderValidation();
}

function renderSwatches() {
    el.swatchRow.replaceChildren();
    for (let region = 0; region < state.size; region++) {
        const swatch = document.createElement("button");
        swatch.className = "swatch" + (state.selectedRegion === region ? " active" : "");
        swatch.style.background = colorForRegion(region);
        swatch.textContent = String(region);
        swatch.title = `Region ${region}`;
        swatch.addEventListener("click", () => {
            state.selectedRegion = region;
            renderAll();
        });
        el.swatchRow.append(swatch);
    }
}

function renderCatRow() {
    el.catRow.replaceChildren();
    for (let i = 0; i < state.size; i++) {
        const slot = document.createElement("button");
        slot.className = "cat-slot"
            + (state.selectedCat === i ? " active" : "")
            + (state.placements[i] !== null ? " placed" : "");
        slot.textContent = String(i + 1);
        slot.title = `Cat ${i + 1}`;
        if (state.placements[i]?.locked) {
            const dot = document.createElement("span");
            dot.className = "lock-dot";
            slot.append(dot);
        }

        slot.addEventListener("click", () => {
            state.selectedCat = i;
            renderAll();
        });
        el.catRow.append(slot);
    }

    el.lockedToggle.checked = state.placements[state.selectedCat]?.locked ?? false;
    el.lockedToggle.disabled = state.placements[state.selectedCat] === null;
}

/** The Generate panel's per-color cell-count editor - defaults to an even split, user-editable.
 *  Each row also gets a Normalize button: press it after hand-editing some
 *  other row throws the total off, and it dumps the entire current gap onto
 *  THAT row, leaving every other row (including whichever one you just
 *  edited) untouched. */
function renderColorCounts() {
    el.colorCountList.replaceChildren();
    for (let region = 0; region < state.size; region++) {
        const row = document.createElement("div");
        row.className = "color-count-row";

        const swatch = document.createElement("button");
        swatch.type = "button";
        swatch.className = "swatch small";
        swatch.style.background = colorForRegion(region);
        swatch.title = "Click to use a different palette color for this region";
        swatch.addEventListener("click", (event) => {
            event.stopPropagation();
            openPalettePicker(region, swatch);
        });

        const input = document.createElement("input");
        input.type = "number";
        input.min = "1";
        input.max = String(state.size * state.size);
        input.value = String(state.regionSizes[region] ?? 1);
        input.addEventListener("input", () => {
            const parsed = Math.floor(Number(input.value));
            state.regionSizes[region] = Number.isFinite(parsed) && parsed >= 1 ? parsed : 1;
            renderCellTotal();
        });

        const normalizeBtn = document.createElement("button");
        normalizeBtn.type = "button";
        normalizeBtn.className = "btn small normalize-btn";
        normalizeBtn.textContent = "Normalize";
        normalizeBtn.title = "Add the current gap to this color's count so the total matches the board again";
        normalizeBtn.addEventListener("click", () => {
            const target = state.size * state.size;
            const total = state.regionSizes.reduce((sum, count) => sum + count, 0);
            const deficit = target - total;
            state.regionSizes[region] = Math.max(1, state.regionSizes[region] + deficit);
            renderColorCounts();
        });

        row.append(swatch, input, normalizeBtn);
        el.colorCountList.append(row);
    }

    renderCellTotal();
}

/** Opens the shared palette popover next to whichever swatch was clicked,
 *  populated with every palette color so the user can pick which one this
 *  region should display as - out of the full 9-color palette, not just the
 *  size-many colors the app happens to assign by default. */
function openPalettePicker(region, anchorEl) {
    el.palettePopoverGrid.replaceChildren();
    REGION_COLORS.forEach((hex, colorIndex) => {
        const option = document.createElement("button");
        option.type = "button";
        option.className = "palette-swatch-option" + (state.regionColorIndices[region] === colorIndex ? " active" : "");
        option.style.background = hex;
        option.title = `Use this color for region ${region}`;
        option.addEventListener("click", (event) => {
            event.stopPropagation();
            assignRegionColor(region, colorIndex);
            el.palettePopover.hidden = true;
        });
        el.palettePopoverGrid.append(option);
    });

    const rect = anchorEl.getBoundingClientRect();
    el.palettePopover.style.top = `${rect.bottom + 6}px`;
    el.palettePopover.style.left = `${rect.left}px`;
    el.palettePopover.hidden = false;
}

/** Assigns colorIndex to region. If another region already displays that
 *  color, the two regions swap colors instead of creating a duplicate - the
 *  palette stays a permutation (every region a distinct color) no matter
 *  what order colors get reassigned in. */
function assignRegionColor(region, colorIndex) {
    const previousIndex = state.regionColorIndices[region];
    const otherRegion = state.regionColorIndices.findIndex((idx, r) => idx === colorIndex && r !== region);
    if (otherRegion >= 0) {
        state.regionColorIndices[otherRegion] = previousIndex;
    }

    state.regionColorIndices[region] = colorIndex;
    renderSwatches();
    renderColorCounts();
    renderBoard();
}

function renderCellTotal() {
    const total = state.regionSizes.reduce((sum, count) => sum + count, 0);
    const target = state.size * state.size;
    const mismatched = total !== target;
    el.cellTotalRow.textContent = `${total} / ${target} cells`;
    el.cellTotalRow.classList.toggle("ok", !mismatched);
    el.cellTotalRow.classList.toggle("danger", mismatched);
    // The Randomizer toggle overwrites regionSizes with a valid split right
    // before generating (see el.generateBtn's click handler), so a mismatch
    // shouldn't block the button while it's checked.
    const blocked = mismatched && !el.randomizerToggle.checked;
    el.generateBtn.disabled = blocked;
    el.generateBtn.title = blocked
        ? `Color cell counts must add up to exactly ${target} (currently ${total}) before you can generate - edit a row, press its Normalize button, or turn on "randomize counts & locked cats before generating".`
        : "";
}

/** Pre-generate locked-cat picker: one checkbox per cat (explicit, fully
 *  user-controlled) plus a count + Randomize button that checks that many
 *  random boxes for you. Nothing here is implicit - whatever's checked when
 *  Generate is pressed is exactly what gets passed as lockedRows. */
function renderLockCatList() {
    el.lockCatList.replaceChildren();
    for (let i = 0; i < state.size; i++) {
        const label = document.createElement("label");
        label.className = "lock-cat-item";

        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.checked = state.lockedCats[i] ?? false;
        checkbox.addEventListener("change", () => {
            state.lockedCats[i] = checkbox.checked;
        });

        const number = document.createElement("span");
        number.textContent = String(i + 1);

        label.append(checkbox, number);
        el.lockCatList.append(label);
    }

    el.lockCountInput.value = String(state.lockRandomizeCount);
    el.lockCountInput.max = String(state.size);
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

// The board's own box size is driven entirely by CSS (.board { width: min(...,
// 100%); aspect-ratio: 1/1 }), independent of grid-template-columns - growing
// the grid to 9x9 shrinks the cells to fit that same box rather than growing
// the box. updateBoardSizing() reads the box's resolved width and divides it
// into state.size cells (accounting for the fixed gap), so the container
// truly never grows with N, on any viewport.
const BOARD_GAP_PX = 5;
const MIN_CELL_PX = 18;

function updateBoardSizing() {
    const containerWidth = el.board.clientWidth || 480;
    const cellSize = Math.max(MIN_CELL_PX, Math.floor((containerWidth - (state.size - 1) * BOARD_GAP_PX) / state.size));
    el.board.style.gridTemplateColumns = `repeat(${state.size}, ${cellSize}px)`;
    el.board.style.setProperty("--cell-size", `${cellSize}px`);
}

let resizeFrame = null;
window.addEventListener("resize", () => {
    if (resizeFrame !== null) {
        return;
    }

    resizeFrame = requestAnimationFrame(() => {
        resizeFrame = null;
        // A resize never changes cell content/count, just their size - re-measure
        // and re-apply the grid math without rebuilding cells (that would replay
        // the intro pop-in and drop any in-flight juice animations).
        updateBoardSizing();
    });
});

function rebuildBoard() {
    builtBoardSize = state.size;
    updateBoardSizing();
    el.board.replaceChildren();
    boardCells = [];

    for (let row = 0; row < state.size; row++) {
        const rowCells = [];
        // Pushed before the column loop (not after) - applyCellVisual below reads
        // boardCells[row][column] via boardCells, so it needs to already resolve
        // to this row's (still-filling) array, not find the row missing and no-op.
        boardCells.push(rowCells);
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
    }
}

function applyCellVisual(row, column) {
    const cell = boardCells[row]?.[column];
    if (!cell) {
        return;
    }

    const region = Number(state.regions[row][column]);
    cell.style.background = colorForRegion(region);

    const catIndex = placementIndexAt(row, column);
    cell.textContent = catIndex >= 0 ? String(catIndex + 1) : "";
    cell.classList.toggle(
        "selected-cat",
        state.mode === "cats" && catIndex >= 0 && catIndex === state.selectedCat
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

    el.autoPlaceBtn.disabled = solutions !== 1;
    el.autoPlaceBtn.title = solutions === 1
        ? "Place all cats at the unique solution"
        : solutions === 0
            ? "Auto-place needs a region layout with a legal solution first - this one has none yet."
            : "Auto-place needs exactly one legal solution first - this layout currently has 2+; keep painting until only one remains.";
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

// Painting re-validates (a backtracking uniqueness solver plus per-region
// connectivity flood fills) after every cell, which is imperceptible at 5x5
// but scales badly with size - dragging fast across a 9x9 board fires many
// pointermove events per second, and running full validation synchronously
// on each one backs up the main thread faster than the browser can paint,
// reading as a hang/crash. Coalescing to one validation pass per animation
// frame (same rAF-throttle idiom as updateBoardSizing's resizeFrame below)
// keeps the same live feedback while capping the worst-case cost regardless
// of how many cells got painted between frames.
let validationFrame = null;
function scheduleValidation() {
    if (validationFrame !== null) {
        return;
    }

    validationFrame = requestAnimationFrame(() => {
        validationFrame = null;
        renderValidation();
    });
}

function paintCell(row, column) {
    setRegion(row, column, state.selectedRegion);
    applyCellVisual(row, column);
    pulseCell(row, column, "painted");
    scheduleValidation();
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
    if (existing >= 0 && existing !== state.selectedCat) {
        // Clicking someone else's cat selects that cat instead of stacking.
        state.selectedCat = existing;
    } else if (existing === state.selectedCat) {
        state.placements[state.selectedCat] = null;
    } else {
        const locked = state.placements[state.selectedCat]?.locked ?? false;
        state.placements[state.selectedCat] = { row, column, locked };
        placed = true;
        const next = state.placements.findIndex((p) => p === null);
        if (next >= 0) {
            state.selectedCat = next;
        }
    }

    renderAll();
    if (placed) {
        pulseCell(row, column, "cat-pop");
    }
}

// ---------- form events ----------

el.id.addEventListener("input", () => {
    state.id = el.id.value;
    // The only title source for a hand-painted level - see state.title's comment.
    state.title = titleFromId(state.id);
    renderValidation();
});

el.size.addEventListener("input", () => {
    setSize(Number(el.size.value));
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
    const placement = state.placements[state.selectedCat];
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

/** Fisher-Yates shuffle of [0, count) - used by the Randomize locked-cats button. */
function shuffledIndices(count) {
    const indices = Array.from({ length: count }, (_, i) => i);
    for (let i = indices.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [indices[i], indices[j]] = [indices[j], indices[i]];
    }

    return indices;
}

/** Random per-color split summing to size*size: every color starts at its
 *  required minimum of 1 cell, then the remaining cells are thrown one at a
 *  time at a random color - always sums exactly right, unlike naive
 *  independent-random-then-normalize approaches. */
function randomRegionSizes(size) {
    const total = size * size;
    const counts = new Array(size).fill(1);
    for (let i = 0; i < total - size; i++) {
        counts[Math.floor(Math.random() * size)]++;
    }

    return counts;
}

/** Same pick used by the Randomize locked-cats button, pulled out so the
 *  Randomizer toggle (see el.generateBtn's click handler) can trigger the
 *  same thing right before generating. */
function randomizeLockedCats() {
    const count = state.lockRandomizeCount;
    const indices = shuffledIndices(state.size);
    const chosen = new Set(indices.slice(0, count));
    state.lockedCats = Array.from({ length: state.size }, (_, i) => chosen.has(i));
}

el.randomSeedBtn.addEventListener("click", () => {
    el.seed.value = randomSeed();
});

el.evenSplitBtn.addEventListener("click", () => {
    state.regionSizes = defaultRegionSizes(state.size);
    renderColorCounts();
});

el.randomizeCountsBtn.addEventListener("click", () => {
    state.regionSizes = randomRegionSizes(state.size);
    renderColorCounts();
});

el.randomizerToggle.addEventListener("change", () => {
    renderCellTotal();
});

// Info popover: the Generate panel's hint text lives here instead of as
// permanent on-page paragraphs, toggled by the "i" button next to the
// Generate button. Closes on a second click, an outside click, or Escape.
el.generateInfoBtn.addEventListener("click", (event) => {
    event.stopPropagation();
    el.generateInfoPopover.hidden = !el.generateInfoPopover.hidden;
});

document.addEventListener("click", (event) => {
    if (!el.generateInfoPopover.hidden && !el.generateInfoPopover.contains(event.target) && event.target !== el.generateInfoBtn) {
        el.generateInfoPopover.hidden = true;
    }

    if (!el.palettePopover.hidden && !el.palettePopover.contains(event.target)) {
        el.palettePopover.hidden = true;
    }
});

document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") {
        return;
    }

    el.generateInfoPopover.hidden = true;
    el.palettePopover.hidden = true;
});

el.lockCountInput.addEventListener("input", () => {
    const parsed = Math.floor(Number(el.lockCountInput.value));
    state.lockRandomizeCount = Number.isFinite(parsed) ? Math.max(0, Math.min(state.size, parsed)) : 0;
});

el.randomizeLocksBtn.addEventListener("click", () => {
    randomizeLockedCats();
    renderLockCatList();
});

el.generateBtn.addEventListener("click", () => {
    // The Randomizer toggle re-rolls both the color split and locked cats
    // right before generating, instead of using whatever's currently set -
    // and re-renders so the panel reflects what's about to be generated
    // rather than changing silently underneath the player.
    if (el.randomizerToggle.checked) {
        state.regionSizes = randomRegionSizes(state.size);
        randomizeLockedCats();
        renderColorCounts();
        renderLockCatList();
    }

    let seed = el.seed.value.trim();
    if (seed === "") {
        seed = randomSeed();
        el.seed.value = seed;
    }

    const lockedRows = state.lockedCats
        .map((locked, index) => (locked ? index : -1))
        .filter((index) => index >= 0);

    try {
        applyLevelData(generateLevelWithRegionSizes(seed, state.size, state.regionSizes.slice(), { lockedRows }));
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
