using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// Renders the puzzle grid: instantiates cells from the prefab, lays them out
    /// for the current board size, plays the intro sweep, syncs cell visuals to
    /// board state each refresh, and plays the small per-cell juice (punch on a
    /// wrong guess, jelly wobble on a cross). Never mutates game state - it's
    /// told what to show and reports hit-tests back to the caller.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly struct CellUi
        {
            public readonly int Row;
            public readonly int Column;
            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;
            public readonly Image Image;
            public readonly Outline Outline;
            public readonly Text MarkText;
            public readonly Text BadgeText;
            public readonly Image NekoImage;

            public CellUi(int row, int column, RectTransform rect, CanvasGroup group, Image image, Outline outline, Text markText, Text badgeText, Image nekoImage)
            {
                Row = row;
                Column = column;
                Rect = rect;
                Group = group;
                Image = image;
                Outline = outline;
                MarkText = markText;
                BadgeText = badgeText;
                NekoImage = nekoImage;
            }
        }

        private readonly struct CellIntroState
        {
            public readonly RectTransform Rect;
            public readonly CanvasGroup Group;
            public readonly float DelaySeconds;

            public CellIntroState(RectTransform rect, CanvasGroup group, float delaySeconds)
            {
                Rect = rect;
                Group = group;
                DelaySeconds = delaySeconds;
            }
        }

        private const float BoardPixels = 900f;
        private const float BoardIntroCellSeconds = 0.34f;
        private const float BoardIntroDiagonalDelaySeconds = 0.045f;
        private const float CellPunchSeconds = 0.22f;
        private const float CrossJellySeconds = 0.34f;
        private const float CatFoundPopSeconds = 0.24f;
        private const float BoardShakeSeconds = 0.28f;

        private static readonly Color TileBorderColor = Color.white;
        private static readonly Color InkColor = new Color(0.18f, 0.17f, 0.16f, 1f);
        private static readonly Color SparkleGoldColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color ConflictColor = new Color(0.94f, 0.24f, 0.2f, 1f);
        private static readonly Color RevealedColor = new Color(0.14f, 0.52f, 0.48f, 1f);
        private static readonly Color CrossFlashColor = new Color(1f, 0.97f, 0.72f, 1f);

        private static readonly Color[] RegionColors =
        {
            new Color(1.00f, 0.72f, 0.76f, 1f),
            new Color(0.67f, 0.88f, 0.77f, 1f),
            new Color(0.62f, 0.80f, 0.96f, 1f),
            new Color(1.00f, 0.82f, 0.58f, 1f),
            new Color(0.79f, 0.71f, 0.93f, 1f),
            new Color(0.96f, 0.91f, 0.55f, 1f),
            new Color(0.93f, 0.62f, 0.50f, 1f),
            new Color(0.53f, 0.82f, 0.85f, 1f),
            new Color(0.74f, 0.88f, 0.56f, 1f)
        };

        [SerializeField]
        private GameObject cellPrefab;

        [SerializeField]
        private RectTransform boardRoot;

        private readonly List<CellUi> cells = new List<CellUi>();
        private readonly Dictionary<Coord, Coroutine> cellPunchRoutines = new Dictionary<Coord, Coroutine>();
        private readonly Dictionary<Coord, Coroutine> markScaleRoutines = new Dictionary<Coord, Coroutine>();

        private CellUi[,] cellGrid;
        private Vector2 boardRestPosition;
        private Coroutine boardIntroRoutine;
        private Coroutine boardShakeRoutine;


        public void CacheRestPosition()
        {
            boardRestPosition = boardRoot.anchoredPosition;
        }

        public void RebuildCells(PuzzleBoard board, BoardInputHandler inputHandler)
        {
            if (cellPrefab == null)
            {
                return;
            }

            for (int i = boardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(boardRoot.GetChild(i).gameObject);
            }

            cells.Clear();
            cellGrid = new CellUi[board.Size, board.Size];

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    CellUi cell = CreateCell(board, inputHandler, row, column);
                    cells.Add(cell);
                    cellGrid[row, column] = cell;
                }
            }

            ApplyLayoutImmediate(board, false, false);
        }

        private CellUi CreateCell(PuzzleBoard board, BoardInputHandler inputHandler, int row, int column)
        {
            GameObject cellObject = Instantiate(cellPrefab, boardRoot, false);
            cellObject.name = $"Cell {row},{column}";
            RectTransform rect = (RectTransform)cellObject.transform;
            CellView view = cellObject.GetComponent<CellView>();
            view.Bind(inputHandler, row, column);
            view.BackgroundImage.color = RegionColors[board.Level.RegionAt(row, column) % RegionColors.Length];
            view.MarkText.fontSize = board.Size >= 7 ? 28 : 35;
            view.MarkText.raycastTarget = false;
            view.BadgeText.fontSize = board.Size >= 7 ? 30 : 38;
            view.BadgeText.raycastTarget = false;
            view.NekoImage.enabled = false;

            return new CellUi(row, column, rect, view.CanvasGroup, view.BackgroundImage, view.BackgroundOutline, view.MarkText, view.BadgeText, view.NekoImage);
        }

        public void ApplyLayoutImmediate(PuzzleBoard board, bool showRemovedCells, bool inputLocked)
        {
            if (board == null || boardRoot == null)
            {
                return;
            }

            boardRoot.localScale = Vector3.one;
            foreach (CellUi cell in cells)
            {
                bool active = board.IsActiveCell(cell.Row, cell.Column);
                cell.Rect.gameObject.SetActive(active || showRemovedCells);
                if (active && TryGetCellLayout(board, cell.Row, cell.Column, out Vector2 position, out float cellSize))
                {
                    cell.Rect.anchoredPosition = position;
                    cell.Rect.sizeDelta = new Vector2(cellSize, cellSize);
                }

                cell.Rect.localRotation = Quaternion.identity;
                cell.Rect.localScale = Vector3.one;
                cell.Group.alpha = active ? 1f : 0f;
                cell.Group.blocksRaycasts = active && !inputLocked;
            }
        }

        private bool TryGetCellLayout(PuzzleBoard board, int row, int column, out Vector2 position, out float cellSize)
        {
            position = Vector2.zero;
            cellSize = 0f;
            if (board == null || !board.IsActiveCell(row, column))
            {
                return false;
            }

            int size = Mathf.Max(1, board.Size);
            float gap = ActiveBoardGap(size);
            cellSize = (BoardPixels - (gap * (size + 1))) / size;
            float startX = (-BoardPixels * 0.5f) + gap + (cellSize * 0.5f);
            float startY = (BoardPixels * 0.5f) - gap - (cellSize * 0.5f);
            position = new Vector2(
                startX + (column * (cellSize + gap)),
                startY - (row * (cellSize + gap)));
            return true;
        }

        private float ActiveBoardGap(int boardSize)
        {
            return boardSize <= 2 ? 12f : 8f;
        }

        public void PlayIntro(PuzzleBoard board, bool inputLocked)
        {
            StopIntro(board, inputLocked);
            if (!isActiveAndEnabled || cells.Count == 0)
            {
                return;
            }

            boardIntroRoutine = StartCoroutine(AnimateIntro(board, inputLocked));
        }

        private IEnumerator AnimateIntro(PuzzleBoard board, bool inputLocked)
        {
            Canvas.ForceUpdateCanvases();

            CellIntroState[] states = new CellIntroState[cells.Count];
            float totalSeconds = BoardIntroCellSeconds + ((board.Size - 1) * 2 * BoardIntroDiagonalDelaySeconds);

            for (int i = 0; i < cells.Count; i++)
            {
                CellUi cell = cells[i];
                states[i] = new CellIntroState(
                    cell.Rect,
                    cell.Group,
                    (cell.Row + cell.Column) * BoardIntroDiagonalDelaySeconds);

                cell.Rect.localScale = Vector3.zero;
                cell.Group.alpha = 0f;
                cell.Group.blocksRaycasts = false;
            }

            // Diagonal pop-in reveal: every cell starts at scale 0 and springs to full
            // size, staggered by row + column so the fill sweeps across the board.
            float elapsed = 0f;
            while (elapsed < totalSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < states.Length; i++)
                {
                    CellIntroState state = states[i];
                    float t = Mathf.Clamp01((elapsed - state.DelaySeconds) / BoardIntroCellSeconds);
                    float easedScale = DOVirtual.EasedValue(0f, 1f, t, Ease.OutBack);

                    state.Rect.localScale = Vector3.one * easedScale;
                    state.Group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.1f));
                }

                yield return null;
            }

            FinishIntroLayout(board, inputLocked);
            boardIntroRoutine = null;
        }

        public void StopIntro(PuzzleBoard board, bool inputLocked)
        {
            if (boardIntroRoutine != null)
            {
                StopCoroutine(boardIntroRoutine);
                boardIntroRoutine = null;
            }

            FinishIntroLayout(board, inputLocked);
        }

        private void FinishIntroLayout(PuzzleBoard board, bool inputLocked)
        {
            if (boardRoot == null || board == null)
            {
                return;
            }

            ApplyLayoutImmediate(board, false, inputLocked);

            foreach (CellUi cell in cells)
            {
                if (!board.IsActiveCell(cell.Row, cell.Column))
                {
                    continue;
                }

                cell.Rect.localScale = Vector3.one;
                cell.Group.alpha = 1f;
                cell.Group.blocksRaycasts = !inputLocked;
            }
        }

        public void RefreshVisuals(PuzzleBoard board, ValidationResult validation, bool inputLocked)
        {
            foreach (CellUi cell in cells)
            {
                bool active = board.IsActiveCell(cell.Row, cell.Column);
                cell.Rect.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                if (TryGetCellLayout(board, cell.Row, cell.Column, out Vector2 position, out float cellSize))
                {
                    cell.Rect.anchoredPosition = position;
                    cell.Rect.sizeDelta = new Vector2(cellSize, cellSize);
                }

                CellMark mark = board.GetMark(cell.Row, cell.Column);
                bool revealed = board.IsRevealed(cell.Row, cell.Column);
                bool revealedCat = board.HasRevealedCat(cell.Row, cell.Column);
                bool revealedMiss = board.HasRevealedMiss(cell.Row, cell.Column);
                Coord coord = new Coord(cell.Row, cell.Column);
                bool conflict = validation.ConflictCells.Contains(coord);
                bool markScaleAnimating = markScaleRoutines.ContainsKey(coord);

                cell.NekoImage.enabled = revealedCat;
                cell.NekoImage.color = Color.white;
                cell.MarkText.text = revealedCat ? string.Empty : MarkLabel(mark, revealedMiss);
                if (!markScaleAnimating)
                {
                    cell.MarkText.rectTransform.localScale = Vector3.one;
                }

                cell.MarkText.color = conflict || revealedMiss ? ConflictColor : revealedCat ? RevealedColor : InkColor;
                cell.BadgeText.text = string.Empty;
                cell.Outline.effectColor = TileBorderColor;
                cell.Outline.effectDistance = conflict || revealed ? new Vector2(5f, -5f) : new Vector2(3f, -3f);
                cell.Image.color = AdjustCellColor(RegionColors[board.Level.RegionAt(cell.Row, cell.Column) % RegionColors.Length], mark, revealedCat, revealedMiss);
                cell.Group.alpha = 1f;
                cell.Group.blocksRaycasts = !inputLocked;
            }
        }

        private bool TryGetCellUi(int row, int column, out CellUi cellUi)
        {
            if (cellGrid != null
                && row >= 0
                && column >= 0
                && row < cellGrid.GetLength(0)
                && column < cellGrid.GetLength(1))
            {
                cellUi = cellGrid[row, column];
                return cellUi.Rect != null;
            }

            cellUi = default;
            return false;
        }

        /// <summary>Outline-color pulse played when a cat is correctly revealed. Yieldable so the caller can keep input locked until it finishes.</summary>
        public IEnumerator PlayCatFoundPop(Coord coord)
        {
            if (!TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                yield break;
            }

            StopCellPunch(coord, true);
            RectTransform rect = cell.Rect;
            Outline outline = cell.Outline;
            Color startOutlineColor = outline.effectColor;
            Vector2 startOutlineDistance = outline.effectDistance;
            float elapsed = 0f;
            while (elapsed < CatFoundPopSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CatFoundPopSeconds);
                float wave = Mathf.Sin(t * Mathf.PI);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, wave);
                outline.effectColor = Color.Lerp(startOutlineColor, SparkleGoldColor, wave);
                outline.effectDistance = Vector2.Lerp(startOutlineDistance, new Vector2(6f, -6f), wave);
                yield return null;
            }

            rect.localScale = Vector3.one;
            outline.effectColor = startOutlineColor;
            outline.effectDistance = startOutlineDistance;
        }

        public bool TryPointerToCell(PuzzleBoard board, Vector2 screenPosition, Camera pressEventCamera, out int row, out int column)
        {
            row = -1;
            column = -1;
            for (int i = 0; i < cells.Count; i++)
            {
                CellUi cell = cells[i];
                if (!board.IsActiveCell(cell.Row, cell.Column) || !cell.Rect.gameObject.activeSelf)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(cell.Rect, screenPosition, pressEventCamera))
                {
                    row = cell.Row;
                    column = cell.Column;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetCellCenterIn(RectTransform relativeTo, Coord coord, out Vector2 center, out float cellSize)
        {
            center = Vector2.zero;
            cellSize = 0f;

            if (relativeTo == null || !TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                return false;
            }

            Vector3 worldCenter = cell.Rect.TransformPoint(cell.Rect.rect.center);
            center = relativeTo.InverseTransformPoint(worldCenter);

            Vector3 worldMin = cell.Rect.TransformPoint(new Vector3(cell.Rect.rect.xMin, cell.Rect.rect.yMin, 0f));
            Vector3 worldMax = cell.Rect.TransformPoint(new Vector3(cell.Rect.rect.xMax, cell.Rect.rect.yMax, 0f));
            Vector3 localMin = relativeTo.InverseTransformPoint(worldMin);
            Vector3 localMax = relativeTo.InverseTransformPoint(worldMax);
            cellSize = Mathf.Min(Mathf.Abs(localMax.x - localMin.x), Mathf.Abs(localMax.y - localMin.y));
            return cellSize > 0f;
        }

        private string MarkLabel(CellMark mark, bool revealedMiss)
        {
            if (revealedMiss)
            {
                return "X";
            }

            switch (mark)
            {
                case CellMark.Cross:
                    return "X";
                default:
                    return string.Empty;
            }
        }

        private Color AdjustCellColor(Color baseColor, CellMark mark, bool revealedCat, bool revealedMiss)
        {
            if (revealedCat)
            {
                return Color.Lerp(baseColor, RevealedColor, 0.14f);
            }

            if (revealedMiss)
            {
                return Color.Lerp(baseColor, ConflictColor, 0.12f);
            }

            return baseColor;
        }

        private Color CellDisplayColor(PuzzleBoard board, int row, int column)
        {
            if (board == null || !board.IsActiveCell(row, column))
            {
                return Color.white;
            }

            return AdjustCellColor(
                RegionColors[board.Level.RegionAt(row, column) % RegionColors.Length],
                board.GetMark(row, column),
                board.HasRevealedCat(row, column),
                board.HasRevealedMiss(row, column));
        }

        // ---- cell-level juice: punch (wrong guess) and cross jelly (mark placed) ----

        public void PunchCell(int row, int column, float peakScale)
        {
            Coord coord = new Coord(row, column);
            StopCellPunch(coord, false);
            if (!isActiveAndEnabled || !TryGetCellUi(row, column, out _))
            {
                return;
            }

            cellPunchRoutines[coord] = StartCoroutine(AnimateCellPunch(coord, Mathf.Max(1f, peakScale)));
        }

        private IEnumerator AnimateCellPunch(Coord coord, float peakScale)
        {
            float elapsed = 0f;
            while (elapsed < CellPunchSeconds)
            {
                if (!TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CellPunchSeconds);
                float wave = Mathf.Sin(t * Mathf.PI);
                cell.Rect.localScale = Vector3.one * Mathf.Lerp(1f, peakScale, wave);
                yield return null;
            }

            if (TryGetCellUi(coord.Row, coord.Column, out CellUi finishedCell))
            {
                finishedCell.Rect.localScale = Vector3.one;
            }

            cellPunchRoutines.Remove(coord);
        }

        private void StopCellPunch(Coord coord, bool resetScale)
        {
            if (cellPunchRoutines.TryGetValue(coord, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            cellPunchRoutines.Remove(coord);
            if (resetScale && TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                cell.Rect.localScale = Vector3.one;
            }
        }

        public void PlayCrossJelly(PuzzleBoard board, int row, int column)
        {
            Coord coord = new Coord(row, column);
            StopMarkScale(board, coord, true);
            if (!isActiveAndEnabled || !TryGetCellUi(row, column, out _))
            {
                return;
            }

            markScaleRoutines[coord] = StartCoroutine(AnimateCrossJelly(board, coord));
        }

        private IEnumerator AnimateCrossJelly(PuzzleBoard board, Coord coord)
        {
            float elapsed = 0f;
            while (elapsed < CrossJellySeconds)
            {
                if (!TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CrossJellySeconds);
                float scale = CrossJellyScale(t);
                float flash = Mathf.Sin(t * Mathf.PI);
                Color baseColor = CellDisplayColor(board, coord.Row, coord.Column);
                cell.Rect.localScale = Vector3.one * scale;
                cell.MarkText.rectTransform.localScale = Vector3.one;
                cell.Image.color = Color.Lerp(baseColor, CrossFlashColor, flash * 0.58f);
                yield return null;
            }

            if (TryGetCellUi(coord.Row, coord.Column, out CellUi finishedCell))
            {
                finishedCell.Rect.localScale = Vector3.one;
                finishedCell.MarkText.rectTransform.localScale = Vector3.one;
                finishedCell.Image.color = CellDisplayColor(board, coord.Row, coord.Column);
            }

            markScaleRoutines.Remove(coord);
        }

        private float CrossJellyScale(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.38f)
            {
                return Mathf.Lerp(1f, 1.15f, Mathf.SmoothStep(0f, 1f, t / 0.38f));
            }

            if (t < 0.68f)
            {
                return Mathf.Lerp(1.15f, 0.95f, Mathf.SmoothStep(0f, 1f, (t - 0.38f) / 0.3f));
            }

            return Mathf.Lerp(0.95f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.68f) / 0.32f));
        }

        private void StopMarkScale(PuzzleBoard board, Coord coord, bool resetScale)
        {
            if (markScaleRoutines.TryGetValue(coord, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            markScaleRoutines.Remove(coord);
            if (resetScale && TryGetCellUi(coord.Row, coord.Column, out CellUi cell))
            {
                cell.Rect.localScale = Vector3.one;
                cell.MarkText.rectTransform.localScale = Vector3.one;
                cell.Image.color = CellDisplayColor(board, coord.Row, coord.Column);
            }
        }

        // ---- board-level juice: shake on a wrong guess ----

        public void ShakeBoard()
        {
            if (!isActiveAndEnabled || boardRoot == null)
            {
                return;
            }

            if (boardShakeRoutine != null)
            {
                StopCoroutine(boardShakeRoutine);
            }

            boardShakeRoutine = StartCoroutine(AnimateBoardShake());
        }

        private IEnumerator AnimateBoardShake()
        {
            float elapsed = 0f;
            boardRoot.anchoredPosition = boardRestPosition;
            while (elapsed < BoardShakeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BoardShakeSeconds);
                float falloff = 1f - t;
                float x = Mathf.Sin(elapsed * 82f) * 18f * falloff;
                float y = Mathf.Sin(elapsed * 47f) * 5f * falloff;
                boardRoot.anchoredPosition = boardRestPosition + new Vector2(x, y);
                yield return null;
            }

            boardRoot.anchoredPosition = boardRestPosition;
            boardShakeRoutine = null;
        }

        /// <summary>Cancels all board-owned animations and snaps everything back to rest. Call around level load/restart/undo.</summary>
        public void StopAllJuice(PuzzleBoard board)
        {
            if (boardShakeRoutine != null)
            {
                StopCoroutine(boardShakeRoutine);
                boardShakeRoutine = null;
            }

            if (boardRoot != null)
            {
                boardRoot.anchoredPosition = boardRestPosition;
            }

            foreach (KeyValuePair<Coord, Coroutine> punch in cellPunchRoutines)
            {
                if (punch.Value != null)
                {
                    StopCoroutine(punch.Value);
                }
            }

            cellPunchRoutines.Clear();

            foreach (KeyValuePair<Coord, Coroutine> markScale in markScaleRoutines)
            {
                if (markScale.Value != null)
                {
                    StopCoroutine(markScale.Value);
                }
            }

            markScaleRoutines.Clear();

            foreach (CellUi cell in cells)
            {
                cell.Rect.localScale = Vector3.one;
                cell.MarkText.rectTransform.localScale = Vector3.one;
                cell.Image.color = CellDisplayColor(board, cell.Row, cell.Column);
            }
        }
    }
}
