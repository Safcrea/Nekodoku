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

// Positionally mirrors the C# side's RegionPalette/RegionColorId (Assets/_GameData/Systems/Scripts/
// Core/RegionPalette.cs) so painted boards read the same here and in the game - index 0 here is
// "Color A" there, index 1 is "Color B", etc. Only the array *position* is shared; these hex swatches
// are this editor's own stand-in art and can be swapped for actual per-region images independently of
// Unity's sprites, same as RegionColorId's names are independent of its sprites. Which of these a given
// region actually displays as is a separate, purely-cosmetic layer - see state.regionColorIndices/colorForRegion.
const REGION_COLORS = [
    "#ffb7c2", // Color A
    "#abe0c4", // Color B
    "#9eccf5", // Color C
    "#ffd194", // Color D
    "#c9b5ed", // Color E
    "#f5e88c", // Color F
    "#ed9e80", // Color G
    "#87d1d9", // Color H
    "#bde08f", // Color I
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
    /** Optional random-count recipe rows: "colors" regions should each get "cells" cells.
     *  Randomize counts applies these to random colors, then distributes the remaining cells. */
    countRules: [],
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

/** The Level Library panel's own state - deliberately separate from `state`
 *  above, which represents only "the one level currently being edited".
 *  The library's lifecycle (which folder is open, what order its levels are
 *  in) is independent of whatever gets loaded into the editor at any moment. */
const library = {
    /** @type {FileSystemDirectoryHandle | null} */
    dirHandle: null,
    /** @type {{fileStem: string, fileName: string, fileHandle: FileSystemFileHandle, level: object}[]} */
    entries: [],
    /** Object reference into `entries` (not an index - stays correct across
     *  reorders without any index-shifting math), or null if nothing loaded
     *  into the editor right now came from the library. */
    activeEntry: null,
    /** True whenever entries[] no longer matches the on-disk manifest's order
     *  (a reorder, add, or remove happened since the last Save Library Order). */
    manifestDirty: false,
};

const el = {
    themeSwitch: document.getElementById("theme-switch"),
    workspaceTitle: document.getElementById("workspace-title"),
    workspaceMeta: document.getElementById("workspace-meta"),
    workspaceSizeStat: document.getElementById("workspace-size-stat"),
    workspaceLayoutStep: document.getElementById("workspace-layout-step"),
    workspaceSolutionStep: document.getElementById("workspace-solution-step"),
    workspaceCatsStep: document.getElementById("workspace-cats-step"),
    workspaceReadiness: document.getElementById("workspace-readiness"),
    id: document.getElementById("id-input"),
    size: document.getElementById("size-input"),
    sizeValue: document.getElementById("size-value"),
    seed: document.getElementById("seed-input"),
    randomSeedBtn: document.getElementById("random-seed-btn"),
    colorCountList: document.getElementById("color-count-list"),
    countRuleList: document.getElementById("count-rule-list"),
    countRuleStatus: document.getElementById("count-rule-status"),
    addCountRuleBtn: document.getElementById("add-count-rule-btn"),
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
    toastStack: document.getElementById("toast-stack"),
    generateBtn: document.getElementById("generate-btn"),
    modeToolbar: document.getElementById("mode-toolbar"),
    regionsControls: document.getElementById("regions-controls"),
    catsControls: document.getElementById("cats-controls"),
    swatchRow: document.getElementById("swatch-row"),
    catRow: document.getElementById("cat-row"),
    lockedToggle: document.getElementById("locked-toggle"),
    board: document.getElementById("board"),
    boardCard: document.querySelector(".board-card"),
    boardModePill: document.getElementById("board-mode-pill"),
    boardHelp: document.getElementById("board-help"),
    boardCelebration: document.getElementById("board-celebration"),
    statusRow: document.getElementById("status-row"),
    issueList: document.getElementById("issue-list"),
    autoPlaceBtn: document.getElementById("auto-place-btn"),
    downloadBtn: document.getElementById("download-btn"),
    copyBtn: document.getElementById("copy-btn"),
    openFileBtn: document.getElementById("open-file-btn"),
    fileInput: document.getElementById("file-input"),
    saveToLibraryBtn: document.getElementById("save-to-library-btn"),
    newBtn: document.getElementById("new-btn"),
    libraryDrawer: document.getElementById("library-drawer"),
    libraryTab: document.getElementById("library-tab"),
    libraryPanel: document.getElementById("library-panel"),
    libraryStatus: document.getElementById("library-status"),
    libraryReconnectBtn: document.getElementById("library-reconnect-btn"),
    libraryOpenBtn: document.getElementById("library-open-btn"),
    librarySaveOrderBtn: document.getElementById("library-save-order-btn"),
    libraryList: document.getElementById("library-list"),
    libraryUnsupported: document.getElementById("library-unsupported"),
};

const TOAST_TIMEOUT_MS = 3600;

// ---------- state helpers ----------

function resetLevel(size) {
    state.title = "New Level";
    state.id = "";
    state.size = size;
    state.regions = Array.from({ length: size }, () => "0".repeat(size));
    state.placements = new Array(size).fill(null);
    state.regionSizes = defaultRegionSizes(size);
    state.countRules = [];
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
    state.countRules = [];
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

/** Loads a parsed/generated level into the editor and replays the board intro. Deliberately does NOT
 *  touch library.activeEntry itself - Generate re-rolling a library-loaded level's content (which also
 *  assigns it a brand-new seed-derived id) is still "editing the same level slot", so it must keep the
 *  link to the originating file alive or "Save to Library" has no file left to overwrite and silently
 *  forks a second one. Callers that load something genuinely unrelated (Open file, New) are responsible
 *  for clearing library.activeEntry themselves before/after calling this; loadLibraryEntry instead SETS
 *  it right after, since that's the one call site that's establishing the link in the first place. */
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
    // A newly loaded/generated level deserves its own completion burst even if
    // the previous board was already valid and ready to ship.
    validationWasReady = false;
    renderAll();
}

// ---------- rendering ----------

function renderAll() {
    el.id.value = state.id;
    el.size.value = String(state.size);
    el.sizeValue.textContent = `${state.size}×${state.size}`;
    renderWorkspaceIdentity();

    for (const button of el.modeToolbar.querySelectorAll("button")) {
        button.classList.toggle("active", button.dataset.mode === state.mode);
    }

    el.regionsControls.hidden = state.mode !== "regions";
    el.catsControls.hidden = state.mode !== "cats";

    renderSwatches();
    renderCatRow();
    renderColorCounts();
    renderCountRules();
    renderLockCatList();
    renderBoard();
    renderValidation();
}

function renderWorkspaceIdentity() {
    const displayTitle = state.title.trim() || titleFromId(state.id);
    const idLabel = state.id.trim() || "Untitled";
    const modeLabel = state.mode === "regions" ? "Regions" : "Cats";

    el.workspaceTitle.textContent = displayTitle;
    el.workspaceMeta.textContent = `${idLabel} · ${state.size}×${state.size} board · ${modeLabel} mode`;
    el.workspaceSizeStat.textContent = `${state.size}×${state.size}`;
    el.boardModePill.textContent = state.mode === "regions" ? "Region brush" : "Cat placement";
    el.boardHelp.textContent = state.mode === "regions"
        ? "Drag across cells to paint the selected region."
        : "Select a numbered cat, then choose its home on the board.";
    document.body.dataset.editorMode = state.mode;
}

function showToast(message, tone = "ok") {
    const toast = document.createElement("div");
    toast.className = `toast ${tone}`;
    toast.setAttribute("role", tone === "danger" ? "alert" : "status");

    const dot = document.createElement("span");
    dot.className = "toast-dot";

    const text = document.createElement("span");
    text.className = "toast-message";
    text.textContent = message;

    const close = document.createElement("button");
    close.type = "button";
    close.className = "toast-close";
    close.setAttribute("aria-label", "Dismiss notification");
    close.textContent = "x";

    let dismissed = false;
    const dismiss = () => {
        if (dismissed) {
            return;
        }

        dismissed = true;
        toast.classList.add("leaving");
        window.setTimeout(() => toast.remove(), 180);
    };

    close.addEventListener("click", dismiss);
    toast.append(dot, text, close);
    el.toastStack.append(toast);
    window.setTimeout(dismiss, TOAST_TIMEOUT_MS);
}

function renderSwatches() {
    el.swatchRow.replaceChildren();
    for (let region = 0; region < state.size; region++) {
        const swatch = document.createElement("button");
        swatch.className = "swatch swatch-enter" + (state.selectedRegion === region ? " active" : "");
        swatch.style.animationDelay = `${region * 24}ms`;
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
        slot.className = "cat-slot swatch-enter"
            + (state.selectedCat === i ? " active" : "")
            + (state.placements[i] !== null ? " placed" : "");
        slot.style.animationDelay = `${i * 24}ms`;
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

function renderCountRules() {
    el.countRuleList.replaceChildren();
    state.countRules.forEach((rule, index) => {
        const row = document.createElement("div");
        row.className = "count-rule-row";
        row.style.animationDelay = `${index * 28}ms`;

        const colorsInput = document.createElement("input");
        colorsInput.type = "number";
        colorsInput.min = "1";
        colorsInput.max = String(state.size);
        colorsInput.value = String(rule.colors);
        colorsInput.title = "How many colors";
        colorsInput.addEventListener("input", () => {
            rule.colors = clampInt(colorsInput.value, 1, state.size);
            updateCountRuleStatus();
        });

        const colorsLabel = document.createElement("span");
        colorsLabel.textContent = "colors";

        const cellsInput = document.createElement("input");
        cellsInput.type = "number";
        cellsInput.min = "1";
        cellsInput.max = String(state.size * state.size);
        cellsInput.value = String(rule.cells);
        cellsInput.title = "Cells per color";
        cellsInput.addEventListener("input", () => {
            rule.cells = clampInt(cellsInput.value, 1, state.size * state.size);
            updateCountRuleStatus();
        });

        const cellsLabel = document.createElement("span");
        cellsLabel.textContent = "cells each";

        const removeBtn = document.createElement("button");
        removeBtn.type = "button";
        removeBtn.className = "btn small count-rule-remove";
        removeBtn.textContent = "Remove";
        removeBtn.addEventListener("click", () => {
            state.countRules.splice(index, 1);
            renderCountRules();
        });

        row.append(colorsInput, colorsLabel, cellsInput, cellsLabel, removeBtn);
        el.countRuleList.append(row);
    });

    updateCountRuleStatus();
}

function updateCountRuleStatus() {
    const plan = buildCountRulePlan(state.size);
    el.countRuleStatus.textContent = countRuleStatusText(plan);
    el.countRuleStatus.classList.toggle("danger", plan.errors.length > 0);
    renderCellTotal();
}

function countRuleStatusText(plan) {
    if (plan.errors.length > 0) {
        return plan.errors[0];
    }

    if (plan.rules.length === 0) {
        return "No constraints: Randomize counts will freely split all colors.";
    }

    return `${plan.fixedColors} fixed color${plan.fixedColors === 1 ? "" : "s"}, ${plan.freeColors} free color${plan.freeColors === 1 ? "" : "s"}, ${plan.freeCells} free cell${plan.freeCells === 1 ? "" : "s"}.`;
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
    const rulePlan = buildCountRulePlan(state.size);
    const ruleBlocked = el.randomizerToggle.checked && rulePlan.errors.length > 0;
    const blocked = (mismatched && !el.randomizerToggle.checked) || ruleBlocked;
    el.generateBtn.disabled = blocked;
    if (ruleBlocked) {
        el.generateBtn.title = rulePlan.errors[0];
    } else {
        el.generateBtn.title = blocked
            ? `Color cell counts must add up to exactly ${target} (currently ${total}) before you can generate - edit a row, press its Normalize button, or turn on "randomize counts & locked cats before generating".`
            : "";
    }
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
    cell.classList.toggle("has-cat", catIndex >= 0);
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

let validationWasReady = false;

function renderValidation() {
    renderWorkspaceIdentity();
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
    const regionCounts = regionSizesFromState();
    const layoutReady = regionsAreInRange() && regionCounts.every((count) => count > 0);
    const solutionReady = solutions === 1;
    const catsReady = Boolean(built.level) && issues.length === 0;
    const readyToShip = layoutReady && solutionReady && catsReady;

    updateWorkflow(layoutReady, solutionReady, readyToShip, built, issues);

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

    if (readyToShip && !validationWasReady) {
        requestAnimationFrame(celebrateBoard);
    }
    validationWasReady = readyToShip;
}

function updateWorkflow(layoutReady, solutionReady, readyToShip, built, issues) {
    setWorkflowStep(el.workspaceLayoutStep, layoutReady, !layoutReady);
    setWorkflowStep(el.workspaceSolutionStep, solutionReady, layoutReady && !solutionReady);
    setWorkflowStep(el.workspaceCatsStep, readyToShip, solutionReady && !readyToShip);

    let message = "Paint every region";
    if (layoutReady && !solutionReady) {
        message = "Refine to one solution";
    } else if (solutionReady && !built.level) {
        const missingCats = state.placements.filter((placement) => placement === null).length;
        message = `Place ${missingCats} cat${missingCats === 1 ? "" : "s"}`;
    } else if (built.level && issues.length > 0) {
        message = `Fix ${issues.length} issue${issues.length === 1 ? "" : "s"}`;
    } else if (readyToShip) {
        message = "Ready to ship";
    }

    const label = el.workspaceReadiness.querySelector("span:last-child");
    label.textContent = message;
    el.workspaceReadiness.classList.toggle("ready", readyToShip);
    el.boardCard.classList.toggle("ready", readyToShip);
}

function setWorkflowStep(step, complete, current) {
    step.classList.toggle("complete", complete);
    step.classList.toggle("current", current);
    const number = step.querySelector("b");
    if (number) {
        number.textContent = complete ? "✓" : step === el.workspaceLayoutStep ? "1" : step === el.workspaceSolutionStep ? "2" : "3";
    }
}

function celebrateBoard() {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        return;
    }

    el.boardCelebration.replaceChildren();
    const colors = [...REGION_COLORS, "#f05d49", "#7d6bc4", "#258b80"];
    const sparkCount = 18;
    for (let i = 0; i < sparkCount; i++) {
        const spark = document.createElement("span");
        spark.className = "board-spark";
        const angle = (Math.PI * 2 * i) / sparkCount + (i % 2) * 0.08;
        const distance = 120 + (i % 5) * 24;
        spark.style.setProperty("--spark-x", `${Math.cos(angle) * distance}px`);
        spark.style.setProperty("--spark-y", `${Math.sin(angle) * distance}px`);
        spark.style.setProperty("--spark-rotation", `${120 + i * 37}deg`);
        spark.style.setProperty("--spark-delay", `${(i % 4) * 24}ms`);
        spark.style.setProperty("--spark-color", colors[i % colors.length]);
        el.boardCelebration.append(spark);
    }

    window.setTimeout(() => el.boardCelebration.replaceChildren(), 1000);
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

function clampInt(value, min, max) {
    const parsed = Math.floor(Number(value));
    if (!Number.isFinite(parsed)) {
        return min;
    }

    return Math.max(min, Math.min(max, parsed));
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

function buildCountRulePlan(size) {
    const totalCells = size * size;
    const rules = state.countRules
        .map((rule) => ({
            colors: clampInt(rule.colors, 1, size),
            cells: clampInt(rule.cells, 1, totalCells),
        }))
        .filter((rule) => rule.colors > 0);

    const fixedColors = rules.reduce((sum, rule) => sum + rule.colors, 0);
    const fixedCells = rules.reduce((sum, rule) => sum + rule.colors * rule.cells, 0);
    const freeColors = size - fixedColors;
    const freeCells = totalCells - fixedCells;
    const errors = [];

    if (fixedColors > size) {
        errors.push(`Count constraints use ${fixedColors} colors, but this board only has ${size}.`);
    } else if (freeCells < freeColors) {
        errors.push(`Count constraints leave ${freeCells} cells for ${freeColors} free colors; each free color needs at least 1.`);
    } else if (freeColors === 0 && freeCells !== 0) {
        errors.push(`Count constraints use all ${size} colors but add up to ${fixedCells} / ${totalCells} cells.`);
    }

    return { rules, fixedColors, fixedCells, freeColors, freeCells, errors };
}

/** Random per-color split summing to size*size. Optional count rules reserve
 *  random colors at fixed sizes first: e.g. "2 colors, 4 cells each", then
 *  the remaining cells are distributed across the remaining colors. */
function randomRegionSizes(size) {
    const plan = buildCountRulePlan(size);
    if (plan.errors.length > 0) {
        throw new Error(plan.errors[0]);
    }

    const counts = new Array(size).fill(null);
    const order = shuffledIndices(size);
    let cursor = 0;

    for (const rule of plan.rules) {
        for (let i = 0; i < rule.colors; i++) {
            counts[order[cursor]] = rule.cells;
            cursor++;
        }
    }

    const freeRegions = order.slice(cursor);
    if (freeRegions.length > 0) {
        for (const region of freeRegions) {
            counts[region] = 1;
        }

        let extras = size * size - counts.reduce((sum, count) => sum + count, 0);
        while (extras > 0) {
            counts[freeRegions[Math.floor(Math.random() * freeRegions.length)]]++;
            extras--;
        }
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
    try {
        state.regionSizes = randomRegionSizes(state.size);
        renderColorCounts();
    } catch (error) {
        alert(error.message);
        renderCountRules();
    }
});

el.addCountRuleBtn.addEventListener("click", () => {
    state.countRules.push({
        colors: 1,
        cells: Math.max(1, Math.floor((state.size * state.size) / state.size)),
    });
    renderCountRules();
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

// Level Library drawer: a plain, persistent toggle (not an auto-closing
// popover like the menus above) - it's a workspace panel you leave open
// while working, not a transient menu.
el.libraryTab.addEventListener("click", () => {
    const opening = el.libraryPanel.hidden;
    el.libraryPanel.hidden = !opening;
    el.libraryTab.setAttribute("aria-expanded", String(opening));
});

el.lockCountInput.addEventListener("input", () => {
    const parsed = Math.floor(Number(el.lockCountInput.value));
    state.lockRandomizeCount = Number.isFinite(parsed) ? Math.max(0, Math.min(state.size, parsed)) : 0;
});

el.randomizeLocksBtn.addEventListener("click", () => {
    randomizeLockedCats();
    renderLockCatList();
});

el.generateBtn.addEventListener("click", async () => {
    el.generateBtn.classList.add("working");
    await new Promise((resolve) => requestAnimationFrame(resolve));

    // The Randomizer toggle re-rolls both the color split and locked cats
    // right before generating, instead of using whatever's currently set -
    // and re-renders so the panel reflects what's about to be generated
    // rather than changing silently underneath the player.
    try {
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

        const level = generateLevelWithRegionSizes(seed, state.size, state.regionSizes.slice(), { lockedRows });
        applyLevelData(level);
        showToast(`Generated ${level.title}.`);
    } catch (error) {
        alert(error.message);
    } finally {
        el.generateBtn.classList.remove("working");
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
    showToast(`Downloaded ${built.level.id}.json.`);
});

el.copyBtn.addEventListener("click", async () => {
    const built = tryBuildLevel();
    if (!built.level) {
        alert(built.error);
        return;
    }

    await navigator.clipboard.writeText(serializeLevel(built.level));
    showToast("Copied level JSON to clipboard.");
});

el.openFileBtn.addEventListener("click", () => el.fileInput.click());

el.fileInput.addEventListener("change", async () => {
    const file = el.fileInput.files?.[0];
    if (file) {
        const level = importJson(await file.text());
        if (level) {
            showToast(`Opened ${level.title}.`);
        }

        el.fileInput.value = "";
    }
});

el.newBtn.addEventListener("click", () => {
    if (confirm("Discard the current level and start fresh?")) {
        resetLevel(5);
        library.activeEntry = null;
        pendingIntro = true;
        renderAll();
        renderLibrary();
        showToast("Started a new level.");
    }
});

function importJson(text) {
    if (text.trim() === "") {
        alert("That file is empty.");
        return null;
    }

    let level;
    try {
        level = parseLevelJson(text);
    } catch (error) {
        alert(error.message);
        return null;
    }

    if (level.size < MIN_SIZE || level.size > MAX_SIZE) {
        alert(`This editor supports sizes ${MIN_SIZE}-${MAX_SIZE}; the file has size ${level.size}.`);
        return null;
    }

    // Opening a file from disk is a genuinely different level from whatever the library drawer had
    // active, even if it happens to be the same JSON by coincidence - clear the link explicitly rather
    // than relying on applyLevelData to do it (Generate deliberately does not, see its comment).
    library.activeEntry = null;
    applyLevelData(level);
    return level;
}

// ---------- level library ----------
// Lets the editor open a whole folder of levels (via the File System Access
// API - Chromium only), drag-reorder them, and edit one in place - the web
// side of the shared levels-manifest.json that a Unity Editor tool
// (LevelManifestSync.cs) also reads to keep LevelDatabase.levelFiles in sync,
// so reordering/adding/editing here needs no more manual Inspector dragging.

const LIBRARY_MANIFEST_NAME = "levels-manifest.json";
const LIBRARY_DB_NAME = "meowdoku-level-editor";
const LIBRARY_STORE_NAME = "handles";
const LIBRARY_HANDLE_KEY = "levelLibraryDir";

function librarySupported() {
    return "showDirectoryPicker" in window;
}

function openLibraryDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(LIBRARY_DB_NAME, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(LIBRARY_STORE_NAME);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function idbGetHandle() {
    const db = await openLibraryDb();
    return new Promise((resolve, reject) => {
        const request = db.transaction(LIBRARY_STORE_NAME, "readonly").objectStore(LIBRARY_STORE_NAME).get(LIBRARY_HANDLE_KEY);
        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

async function idbSetHandle(handle) {
    const db = await openLibraryDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(LIBRARY_STORE_NAME, "readwrite");
        tx.objectStore(LIBRARY_STORE_NAME).put(handle, LIBRARY_HANDLE_KEY);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
    });
}

async function openLibraryFolder() {
    const handle = await window.showDirectoryPicker({ mode: "readwrite" });
    library.dirHandle = handle;
    idbSetHandle(handle).catch(() => {}); // best-effort persistence, not required to work
    el.libraryReconnectBtn.hidden = true;
    await loadLibraryFromHandle();
    showToast(`Opened library "${handle.name}".`);
}

/** (Re)reads every level in library.dirHandle plus levels-manifest.json (if present) and
 *  rebuilds library.entries in manifest order - files on disk but missing from the manifest
 *  are appended at the end and flag manifestDirty, same "don't silently drop it" spirit as
 *  LevelManifestSync.cs's orphan warning on the Unity side. */
async function loadLibraryFromHandle() {
    const dirHandle = library.dirHandle;
    const found = [];
    for await (const [name, handle] of dirHandle.entries()) {
        if (handle.kind !== "file" || !name.endsWith(".json") || name === LIBRARY_MANIFEST_NAME) {
            continue;
        }

        let level;
        try {
            level = parseLevelJson(await (await handle.getFile()).text());
        } catch (error) {
            console.warn(`Level Library: skipping unreadable file "${name}": ${error.message}`);
            continue;
        }

        found.push({ fileStem: name.slice(0, -".json".length), fileName: name, fileHandle: handle, level });
    }

    let order = null;
    try {
        const manifestHandle = await dirHandle.getFileHandle(LIBRARY_MANIFEST_NAME, { create: false });
        order = JSON.parse(await (await manifestHandle.getFile()).text())?.order ?? null;
    } catch {
        // No manifest yet (first-ever open of this folder) - fall back to
        // alphabetical and let the next Save Library Order create one.
    }

    if (order) {
        const byStem = new Map(found.map((entry) => [entry.fileStem, entry]));
        const ordered = [];
        for (const stem of order) {
            const entry = byStem.get(stem);
            if (entry) {
                ordered.push(entry);
                byStem.delete(stem);
            }
        }

        for (const orphan of byStem.values()) {
            ordered.push(orphan);
        }

        library.entries = ordered;
        library.manifestDirty = byStem.size > 0;
    } else {
        found.sort((a, b) => a.fileStem.localeCompare(b.fileStem));
        library.entries = found;
        library.manifestDirty = found.length > 0;
    }

    library.activeEntry = null;
    renderLibrary();
}

async function saveLibraryOrder() {
    if (!library.dirHandle) {
        return;
    }

    const manifest = { formatVersion: 1, order: library.entries.map((entry) => entry.fileStem) };
    const handle = await library.dirHandle.getFileHandle(LIBRARY_MANIFEST_NAME, { create: true });
    const writable = await handle.createWritable();
    await writable.write(JSON.stringify(manifest, null, 2) + "\n");
    await writable.close();

    library.manifestDirty = false;
    renderLibrary();
    showToast("Saved library order.");
}

/** The other half of "edit levels in the level editor and it reflects in Unity": writes the
 *  currently-edited level back to its own file (if loaded from the library) or creates a new
 *  file and appends it to the library (if not). Always writes back to the ORIGINATING file
 *  handle, never to whatever the id field currently says - renaming id while editing must not
 *  silently fork into a second file. */
async function saveToLibrary() {
    if (!library.dirHandle) {
        return;
    }

    const built = tryBuildLevel();
    if (!built.level) {
        alert(built.error);
        return;
    }

    const text = serializeLevel(built.level);

    if (library.activeEntry) {
        const writable = await library.activeEntry.fileHandle.createWritable();
        await writable.write(text);
        await writable.close();
        library.activeEntry.level = built.level;
        const fileName = library.activeEntry.fileName;
        renderLibrary();
        showToast(`Saved ${fileName} to library.`);
        return;
    }

    const fileStem = built.level.id || slugify(built.level.title);
    const fileName = `${fileStem}.json`;
    // If this id/slug collides with an already-listed entry (e.g. "New" then
    // typing the same id by mistake), overwrite that entry in place instead
    // of creating a second list entry pointing at the same file.
    const existing = library.entries.find((entry) => entry.fileStem === fileStem);

    const fileHandle = await library.dirHandle.getFileHandle(fileName, { create: true });
    const writable = await fileHandle.createWritable();
    await writable.write(text);
    await writable.close();

    if (existing) {
        existing.fileHandle = fileHandle;
        existing.level = built.level;
        library.activeEntry = existing;
        renderLibrary();
        showToast(`Saved ${fileName} to library.`);
    } else {
        const entry = { fileStem, fileName, fileHandle, level: built.level };
        library.entries.push(entry);
        library.activeEntry = entry;
        library.manifestDirty = true;
        renderLibrary();
        showToast(`Added ${fileName} to library.`);
    }
}

function removeLibraryEntry(index) {
    const [removed] = library.entries.splice(index, 1);
    if (library.activeEntry === removed) {
        library.activeEntry = null;
    }

    library.manifestDirty = true;
    renderLibrary();
    if (removed) {
        showToast(`Removed ${removed.fileName} from library order.`, "warn");
    }
}

function loadLibraryEntry(index) {
    const entry = library.entries[index];
    if (!entry) {
        return;
    }

    applyLevelData(entry.level);
    library.activeEntry = entry;
    renderLibrary();
    showToast(`Loaded ${entry.fileName} from library.`);
}

function updateSaveToLibraryButton() {
    el.saveToLibraryBtn.disabled = !library.dirHandle;
    // Icon-only button now - textContent would wipe out the svg, so the overwrite-vs-create
    // distinction lives in the tooltip/label instead.
    const label = library.activeEntry ? "Save to Library" : "Add to Library";
    el.saveToLibraryBtn.title = label;
    el.saveToLibraryBtn.setAttribute("aria-label", label);
}

let libraryDragIndex = null;

/** Native HTML5 drag-and-drop - desktop-only local tool, no touch support needed. Reordering
 *  splices library.entries directly and always re-renders from it afterward (never trusts a
 *  stale dataset.index post-splice); library.activeEntry is an object reference rather than an
 *  index specifically so it never needs adjusting when entries move around it. */
function attachLibraryDragHandlers(li, index) {
    li.addEventListener("dragstart", (event) => {
        libraryDragIndex = index;
        li.classList.add("dragging");
        event.dataTransfer.effectAllowed = "move";
        event.dataTransfer.setData("text/plain", String(index));
    });

    li.addEventListener("dragend", () => {
        li.classList.remove("dragging");
        for (const row of el.libraryList.children) {
            row.classList.remove("drag-over-before", "drag-over-after");
        }
    });

    li.addEventListener("dragover", (event) => {
        event.preventDefault();
        const rect = li.getBoundingClientRect();
        const before = event.clientY < rect.top + rect.height / 2;
        li.classList.toggle("drag-over-before", before);
        li.classList.toggle("drag-over-after", !before);
    });

    li.addEventListener("dragleave", () => {
        li.classList.remove("drag-over-before", "drag-over-after");
    });

    li.addEventListener("drop", (event) => {
        event.preventDefault();
        const from = libraryDragIndex;
        const before = li.classList.contains("drag-over-before");
        li.classList.remove("drag-over-before", "drag-over-after");
        if (from === null || from === index) {
            return;
        }

        const [moved] = library.entries.splice(from, 1);
        let to = index;
        if (from < index) {
            to -= 1;
        }
        if (!before) {
            to += 1;
        }

        library.entries.splice(to, 0, moved);
        library.manifestDirty = true;
        renderLibrary();
    });
}

function renderLibrary() {
    if (library.dirHandle) {
        const dirtyNote = library.manifestDirty ? " (unsaved order)" : "";
        el.libraryStatus.textContent = `${library.entries.length} level${library.entries.length === 1 ? "" : "s"} in "${library.dirHandle.name}"${dirtyNote}`;
    } else {
        el.libraryStatus.textContent = "No folder open.";
    }

    el.librarySaveOrderBtn.disabled = !library.dirHandle;

    el.libraryList.replaceChildren();
    library.entries.forEach((entry, index) => {
        const li = document.createElement("li");
        li.className = "library-item" + (entry === library.activeEntry ? " active" : "");
        li.draggable = true;
        // The filename used to sit in its own row-cropping column fighting the number for space;
        // it's still available on hover for anyone who needs to confirm which file this is.
        li.title = entry.fileName;

        const handleSpan = document.createElement("span");
        handleSpan.className = "library-drag-handle";
        handleSpan.textContent = "☰";

        // Shows the level's position in this order (what the player will
        // actually see it as, e.g. GameManager's levelIndex + 1) rather than
        // its title/name - that's what the library is for managing here.
        const number = document.createElement("span");
        number.className = "library-item-number";
        number.textContent = `Level ${index + 1}`;

        const removeBtn = document.createElement("button");
        removeBtn.type = "button";
        removeBtn.className = "btn small";
        removeBtn.textContent = "Remove";
        removeBtn.title = "Remove from the library order - does not delete the file";
        removeBtn.addEventListener("click", (event) => {
            event.stopPropagation();
            removeLibraryEntry(index);
        });

        li.append(handleSpan, number, removeBtn);
        li.addEventListener("click", () => loadLibraryEntry(index));
        attachLibraryDragHandlers(li, index);
        el.libraryList.append(li);
    });

    updateSaveToLibraryButton();
}

async function restoreLibraryHandle() {
    let handle;
    try {
        handle = await idbGetHandle();
    } catch {
        return;
    }

    if (!handle) {
        return;
    }

    library.dirHandle = handle;
    const permission = await handle.queryPermission({ mode: "readwrite" }).catch(() => "prompt");
    if (permission === "granted") {
        try {
            await loadLibraryFromHandle();
        } catch (error) {
            console.warn("Level Library: failed to auto-load the persisted folder:", error);
            library.dirHandle = null;
        }
    } else {
        el.libraryReconnectBtn.hidden = false;
        el.libraryStatus.textContent = `Folder "${handle.name}" needs permission - click Reconnect.`;
    }
}

el.libraryOpenBtn.addEventListener("click", async () => {
    try {
        await openLibraryFolder();
    } catch (error) {
        if (error.name !== "AbortError") {
            showToast(`Couldn't open library: ${error.message}`, "danger");
            alert(`Couldn't open that folder: ${error.message}`);
        }
    }
});

el.libraryReconnectBtn.addEventListener("click", async () => {
    if (!library.dirHandle) {
        return;
    }

    try {
        const permission = await library.dirHandle.requestPermission({ mode: "readwrite" });
        if (permission === "granted") {
            el.libraryReconnectBtn.hidden = true;
            await loadLibraryFromHandle();
            showToast(`Reconnected library "${library.dirHandle.name}".`);
        }
    } catch (error) {
        showToast(`Couldn't reconnect library: ${error.message}`, "danger");
        alert(`Couldn't reconnect: ${error.message}`);
    }
});

el.librarySaveOrderBtn.addEventListener("click", () => {
    saveLibraryOrder().catch((error) => {
        showToast(`Couldn't save library order: ${error.message}`, "danger");
        alert(`Couldn't save library order: ${error.message}`);
    });
});

el.saveToLibraryBtn.addEventListener("click", () => {
    saveToLibrary().catch((error) => {
        showToast(`Couldn't save to library: ${error.message}`, "danger");
        alert(`Couldn't save to library: ${error.message}`);
    });
});

// ---------- theme ----------

function applyTheme(choice) {
    if (!window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        document.documentElement.classList.add("theme-changing");
        window.setTimeout(() => document.documentElement.classList.remove("theme-changing"), 320);
    }

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

if (librarySupported()) {
    renderLibrary();
    restoreLibraryHandle();
} else {
    el.libraryOpenBtn.disabled = true;
    el.libraryList.hidden = true;
    el.libraryUnsupported.hidden = false;
    el.libraryStatus.textContent = "Not supported in this browser.";
}
