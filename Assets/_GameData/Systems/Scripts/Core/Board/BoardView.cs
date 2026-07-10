using System.Collections.Generic;
using AllIn1SpringsToolkit;
using DG.Tweening;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Spawns one <see cref="GridCell"/> per board cell under <see cref="boardRoot"/> and
    /// keeps their static appearance in sync with board state each refresh. Layout itself
    /// is not this class's job: boardRoot carries a GridLayoutGroup + <see cref="BoardGridSizer"/>
    /// that arrange and resize the cells to fit whatever size boardRoot is - drag-resizing
    /// boardRoot in the Inspector/Scene view resizes the whole grid automatically. Per-cell
    /// juice (punch/jelly/pop) lives on GridCell; this class only decides which cell to tell.
    /// The board shake is spring-driven (<see cref="boardShakeSpring"/> on boardRoot) so
    /// consecutive wrong guesses compound the shake instead of restarting it.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float BoardIntroCellSeconds = 0.34f;
        private const float BoardIntroDiagonalDelaySeconds = 0.045f;
        private const float ShakeHorizontalImpulse = 14f;
        private const float ShakeVerticalImpulse = 4f;
        private const float CompleteBoardScale = 0.94f;
        private const float CompleteBoardAlpha = 0.82f;
        private const float CompleteBoardPoseSeconds = 0.45f;
        private const float CompleteCellMaxDelaySeconds = 0.35f;
        private const float FailureBoardScale = 0.98f;
        private const float FailureBoardAlpha = 0.72f;
        private const float FailureBoardPoseSeconds = 0.24f;
        private const float FailureCellMaxDelaySeconds = 0.16f;
        private const float ReviveCellMaxDelaySeconds = 0.22f;
        private const float RetryCellMaxDelaySeconds = 0.16f;

        [SerializeField]
        private GameObject cellPrefab;

        [SerializeField]
        private RectTransform boardRoot;

        [SerializeField]
        private BoardGridSizer boardGridSizer;

        [SerializeField]
        private TransformSpringComponent boardShakeSpring;

        [SerializeField]
        private CanvasGroup boardCanvasGroup;

        private readonly List<GridCell> cells = new List<GridCell>();

        private GridCell[,] cellGrid;
        private HashSet<Coord> tutorialAllowedCells;
        private HashSet<Coord> tutorialRequiredCells;
        private Tween resultPoseTween;
        private Vector3 boardRootRestScale = Vector3.one;
        private bool boardRootRestScaleCaptured;
        private bool tutorialRestrictionActive;
        private bool tutorialInteractionEnabled = true;

        private void Awake()
        {
            CaptureBoardRestScale();
            ResolveBoardCanvasGroup();
        }

        public void RebuildCells(PuzzleBoard board, BoardInputHandler inputHandler)
        {
            if (cellPrefab == null)
            {
                return;
            }

            ResetResultPose();

            for (int i = boardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(boardRoot.GetChild(i).gameObject);
            }

            cells.Clear();
            cellGrid = new GridCell[board.Size, board.Size];
            boardGridSizer.SetColumns(board.Size);

            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    GridCell cell = CreateCell(board, inputHandler, row, column);
                    cells.Add(cell);
                    cellGrid[row, column] = cell;
                }
            }
        }

        private GridCell CreateCell(PuzzleBoard board, BoardInputHandler inputHandler, int row, int column)
        {
            GameObject cellObject = Instantiate(cellPrefab, boardRoot, false);
            cellObject.name = $"Cell {row},{column}";
            GridCell cell = cellObject.GetComponent<GridCell>();
            cell.Bind(inputHandler, row, column);
            cell.SetRegion(board.Level.RegionAt(row, column));
            return cell;
        }

        public GridCell GetCellView(int row, int column)
        {
            if (cellGrid == null || row < 0 || column < 0 || row >= cellGrid.GetLength(0) || column >= cellGrid.GetLength(1))
            {
                return null;
            }

            return cellGrid[row, column];
        }

        public Tween PlayLevelCompleteTransition(PuzzleBoard board, Coord? origin)
        {
            if (board == null || boardRoot == null)
            {
                return null;
            }

            CanvasGroup group = ResolveBoardCanvasGroup();
            KillResultPoseTween();
            ResetBoardShakeSpringPosition();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(boardRoot.DOScale(boardRootRestScale * CompleteBoardScale, CompleteBoardPoseSeconds).SetEase(Ease.OutSine));
            if (group != null)
            {
                sequence.Join(group.DOFade(CompleteBoardAlpha, CompleteBoardPoseSeconds).SetEase(Ease.OutSine));
            }

            float maxDistance = MaxDistanceFrom(board, origin);
            foreach (GridCell cell in cells)
            {
                if (!board.IsActiveCell(cell.Row, cell.Column))
                {
                    continue;
                }

                float delay = StaggerDelay(cell.Row, cell.Column, origin, maxDistance, CompleteCellMaxDelaySeconds);
                Tween pulse = cell.PlayCompletePulse(board.HasRevealedCat(cell.Row, cell.Column));
                if (pulse != null)
                {
                    sequence.Insert(delay, pulse);
                }
            }

            resultPoseTween = sequence;
            sequence.OnComplete(() => resultPoseTween = null);
            return sequence;
        }

        public Tween PlayLevelFailedTransition(PuzzleBoard board)
        {
            if (board == null || boardRoot == null)
            {
                return null;
            }

            CanvasGroup group = ResolveBoardCanvasGroup();
            KillResultPoseTween();
            ResetBoardShakeSpringPosition();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(boardRoot.DOScale(boardRootRestScale * FailureBoardScale, FailureBoardPoseSeconds).SetEase(Ease.OutSine));
            if (group != null)
            {
                sequence.Join(group.DOFade(FailureBoardAlpha, FailureBoardPoseSeconds).SetEase(Ease.OutSine));
            }

            foreach (GridCell cell in cells)
            {
                if (!board.HasRevealedMiss(cell.Row, cell.Column))
                {
                    continue;
                }

                Tween pulse = cell.PlayFailurePulse();
                if (pulse != null)
                {
                    sequence.Insert(Random.Range(0f, FailureCellMaxDelaySeconds), pulse);
                }
            }

            resultPoseTween = sequence;
            sequence.OnComplete(() => resultPoseTween = null);
            return sequence;
        }

        public Tween PlayExtraLifeReviveTransition(PuzzleBoard board)
        {
            if (board == null || boardRoot == null)
            {
                return null;
            }

            CanvasGroup group = ResolveBoardCanvasGroup();
            KillResultPoseTween();
            ResetBoardShakeSpringPosition();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(boardRoot.DOScale(boardRootRestScale, CompleteBoardPoseSeconds).SetEase(Ease.OutSine));
            if (group != null)
            {
                sequence.Join(group.DOFade(1f, CompleteBoardPoseSeconds).SetEase(Ease.OutSine));
            }

            Coord center = new Coord(board.Size / 2, board.Size / 2);
            float maxDistance = MaxDistanceFrom(board, center);
            foreach (GridCell cell in cells)
            {
                if (!board.IsRevealed(cell.Row, cell.Column))
                {
                    continue;
                }

                float delay = StaggerDelay(cell.Row, cell.Column, center, maxDistance, ReviveCellMaxDelaySeconds);
                Tween pulse = cell.PlayRevivePulse();
                if (pulse != null)
                {
                    sequence.Insert(delay, pulse);
                }
            }

            resultPoseTween = sequence;
            sequence.OnComplete(() => resultPoseTween = null);
            return sequence;
        }

        public Tween PlayRetryClearTransition(PuzzleBoard board)
        {
            if (board == null)
            {
                return null;
            }

            KillResultPoseTween();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            foreach (GridCell cell in cells)
            {
                if (!board.IsActiveCell(cell.Row, cell.Column))
                {
                    continue;
                }

                float normalizedRow = board.Size > 1 ? (float)cell.Row / (board.Size - 1) : 0f;
                Tween clear = cell.PlayRetryClear();
                if (clear != null)
                {
                    sequence.Insert(normalizedRow * RetryCellMaxDelaySeconds, clear);
                }
            }

            resultPoseTween = sequence;
            sequence.OnComplete(() => resultPoseTween = null);
            return sequence;
        }

        public void ResetResultPose()
        {
            CaptureBoardRestScale();
            KillResultPoseTween();
            ResetBoardShakeSpringPosition();

            if (boardRoot != null)
            {
                boardRoot.DOKill();
                boardRoot.localScale = boardRootRestScale;
            }

            CanvasGroup group = ResolveBoardCanvasGroup();
            if (group != null)
            {
                group.DOKill();
                group.alpha = 1f;
                group.blocksRaycasts = true;
            }
        }

        // ---- powerup hint (fade-pulse highlight, same GridCell hint API the tutorial's guide uses) ----

        public void ShowHintCross(IEnumerable<Coord> cellsToHighlight)
        {
            foreach (Coord coord in cellsToHighlight)
            {
                GridCell cell = GetCellView(coord.Row, coord.Column);
                if (cell != null)
                {
                    cell.SetCrossHint(true);
                }
            }
        }

        public void ShowHintCat(Coord cat)
        {
            GridCell cell = GetCellView(cat.Row, cat.Column);
            if (cell != null)
            {
                cell.SetCatHint(true);
            }
        }

        /// <summary>Unconditionally clears both hint kinds on every cell. Safe/idempotent even when no
        /// hint is active (GridCell no-ops if its hint flag is already off), so callers don't need to
        /// track which cells were highlighted.</summary>
        public void ClearHint()
        {
            foreach (GridCell cell in cells)
            {
                cell.SetCatHint(false);
                cell.SetCrossHint(false);
            }
        }

        public void PlayIntro(bool inputLocked)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            foreach (GridCell cell in cells)
            {
                float delay = (cell.Row + cell.Column) * BoardIntroDiagonalDelaySeconds;
                cell.PlayIntroPop(delay, BoardIntroCellSeconds, !inputLocked);
            }
        }

        public void StopIntro(bool inputLocked)
        {
            foreach (GridCell cell in cells)
            {
                cell.SkipIntro(!inputLocked);
            }
        }

        public void RefreshVisuals(PuzzleBoard board, bool inputLocked)
        {
            foreach (GridCell cell in cells)
            {
                CellMark mark = board.GetMark(cell.Row, cell.Column);
                bool revealedCat = board.HasRevealedCat(cell.Row, cell.Column);
                bool revealedMiss = board.HasRevealedMiss(cell.Row, cell.Column);

                cell.Refresh(mark, revealedCat, revealedMiss);

                Coord coord = new Coord(cell.Row, cell.Column);
                bool allowed = !tutorialRestrictionActive || tutorialAllowedCells.Contains(coord);
                bool completedRequired = tutorialRequiredCells != null
                    && tutorialRequiredCells.Contains(coord)
                    && mark == CellMark.Cross;
                // A revealed cat's cell is always shown undimmed (see GetTutorialDimMode below) even
                // when it's outside the current sub-guide's allowed set - e.g. the row/column teach step
                // deliberately excludes the cat's own cell from `allowed` since it can't be crossed. It
                // must stay interactable to match that undimmed look: otherwise its raycasts are blocked
                // and a drag gesture *starting* on that cell (a very natural place to start dragging down
                // a column) never fires OnBeginDrag at all, silently swallowing the whole drag.
                bool interactive = (allowed || revealedCat) && tutorialInteractionEnabled;
                cell.SetInteractable(!inputLocked && interactive);
                cell.SetTutorialDimMode(GetTutorialDimMode(tutorialRestrictionActive, allowed, completedRequired, revealedCat));
            }
        }

        /// <summary>
        /// Restricts interaction to exactly the given cells (dims and disables everything else) so a
        /// scripted tutorial step can't be broken by poking around the rest of the board. Persists
        /// across <see cref="RefreshVisuals"/> calls until <see cref="ClearTutorialRestriction"/>.
        /// </summary>
        public void SetTutorialRestriction(IEnumerable<Coord> allowedCells, IEnumerable<Coord> requiredCells = null)
        {
            tutorialAllowedCells = ToHashSet(allowedCells);
            tutorialRequiredCells = ToHashSetOrNull(requiredCells);
            tutorialRestrictionActive = true;
        }

        /// <summary>
        /// Independent of the allowed/dimmed cell set above - lets a caller keep the *next* target cell
        /// looking correct (highlighted/undimmed) while still blocking all clicks until it's actually
        /// ready to be tapped (e.g. until a hand guide has finished appearing/gliding onto it). Defaults
        /// to enabled so callers that never touch this behave exactly as before.
        /// </summary>
        public void SetTutorialInteractionEnabled(bool enabled)
        {
            tutorialInteractionEnabled = enabled;
        }

        public void ClearTutorialRestriction()
        {
            tutorialRestrictionActive = false;
            tutorialAllowedCells = null;
            tutorialRequiredCells = null;
            tutorialInteractionEnabled = true;
        }

        /// <summary>
        /// Mirrors the interactable check <see cref="RefreshVisuals"/> applies per cell, for
        /// <see cref="BoardInputHandler"/> to gate an in-progress cross-drag. A drag's *start* cell is
        /// naturally gated by that cell's own raycast target (blocked when non-interactable), but once a
        /// drag is under way its hit-testing (<see cref="TryPointerToCell"/>) is a pure geometric lookup
        /// with no idea about the tutorial restriction - so without this, a drag begun on a legitimately
        /// allowed cell could glide onto a dimmed/blocked cell and cross it anyway.
        /// </summary>
        public bool IsTutorialCrossAllowed(int row, int column)
        {
            if (!tutorialRestrictionActive)
            {
                return true;
            }

            return tutorialInteractionEnabled && tutorialAllowedCells.Contains(new Coord(row, column));
        }

        /// <summary>
        /// Cells the lesson previously required (e.g. an earlier sub-guide's cells, now crossed) stay
        /// undimmed once done - only cells that are neither the current sub-guide's target nor already
        /// completed get dimmed. Interactability for those already-done cells is turned off separately
        /// via the `allowed` check in <see cref="RefreshVisuals"/>, so this method only ever governs opacity.
        /// </summary>
        private static TutorialDimMode GetTutorialDimMode(bool restrictionActive, bool allowed, bool completedRequired, bool revealedCat)
        {
            if (!restrictionActive || revealedCat || completedRequired || allowed)
            {
                return TutorialDimMode.None;
            }

            return TutorialDimMode.NotRequired;
        }

        private static HashSet<Coord> ToHashSet(IEnumerable<Coord> cells)
        {
            return cells != null ? new HashSet<Coord>(cells) : new HashSet<Coord>();
        }

        private static HashSet<Coord> ToHashSetOrNull(IEnumerable<Coord> cells)
        {
            return cells != null ? new HashSet<Coord>(cells) : null;
        }

        public bool TryPointerToCell(Vector2 screenPosition, Camera pressEventCamera, out int row, out int column)
        {
            row = -1;
            column = -1;
            foreach (GridCell cell in cells)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(cell.Rect, screenPosition, pressEventCamera))
                {
                    row = cell.Row;
                    column = cell.Column;
                    return true;
                }
            }

            return false;
        }

        public Camera GetPointerEventCamera()
        {
            if (boardRoot == null)
            {
                return null;
            }

            Canvas canvas = boardRoot.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        public bool ContainsBoardTransform(Transform target)
        {
            return boardRoot != null && target != null && (target == boardRoot || target.IsChildOf(boardRoot));
        }

        public bool TryGetCellCenterIn(RectTransform relativeTo, Coord coord, out Vector2 center, out float cellSize)
        {
            center = Vector2.zero;
            cellSize = 0f;

            GridCell cell = GetCellView(coord.Row, coord.Column);
            if (relativeTo == null || cell == null)
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

        /// <summary>Plays the heart-lost reaction on every cell whose cat is already revealed.</summary>
        public void PlayHeartLostReactionOnRevealedCats(PuzzleBoard board)
        {
            foreach (GridCell cell in cells)
            {
                if (board.HasRevealedCat(cell.Row, cell.Column))
                {
                    cell.PlayHeartLostReaction();
                }
            }
        }

        public void PlayCrossResetAnimations(PuzzleBoard board, float maxDelaySeconds)
        {
            if (board == null)
            {
                return;
            }

            float delayLimit = Mathf.Max(0f, maxDelaySeconds);
            foreach (GridCell cell in cells)
            {
                if (!board.IsRevealed(cell.Row, cell.Column) && board.GetMark(cell.Row, cell.Column) == CellMark.Cross)
                {
                    cell.PlayCrossResetJelly(Random.Range(0f, delayLimit));
                }
            }
        }

        // ---- board-level juice: shake on a wrong guess ----

        public void ShakeBoard()
        {
            if (!isActiveAndEnabled || boardShakeSpring == null)
            {
                return;
            }

            Vector3 impulse = new Vector3(
                Random.Range(-1f, 1f) * ShakeHorizontalImpulse,
                Random.Range(-1f, 1f) * ShakeVerticalImpulse,
                0f);
            boardShakeSpring.AddVelocityPosition(impulse);
        }

        /// <summary>Cancels all board-owned animations and snaps everything back to rest. Call around level load/restart/undo.</summary>
        public void StopAllJuice()
        {
            ResetResultPose();

            if (boardShakeSpring != null)
            {
                boardShakeSpring.ReachEquilibriumPosition();
            }

            foreach (GridCell cell in cells)
            {
                cell.StopJuice();
            }
        }

        private CanvasGroup ResolveBoardCanvasGroup()
        {
            if (boardCanvasGroup != null || boardRoot == null)
            {
                return boardCanvasGroup;
            }

            if (!boardRoot.TryGetComponent(out boardCanvasGroup))
            {
                boardCanvasGroup = boardRoot.gameObject.AddComponent<CanvasGroup>();
            }

            return boardCanvasGroup;
        }

        private void CaptureBoardRestScale()
        {
            if (boardRoot == null || boardRootRestScaleCaptured)
            {
                return;
            }

            boardRootRestScale = boardRoot.localScale;
            boardRootRestScaleCaptured = true;
        }

        private void KillResultPoseTween()
        {
            resultPoseTween?.Kill();
            resultPoseTween = null;

            if (boardRoot != null)
            {
                boardRoot.DOKill();
            }

            if (boardCanvasGroup != null)
            {
                boardCanvasGroup.DOKill();
            }
        }

        private void ResetBoardShakeSpringPosition()
        {
            if (boardShakeSpring == null)
            {
                return;
            }

            boardShakeSpring.ReachEquilibriumPosition();
        }

        private static float MaxDistanceFrom(PuzzleBoard board, Coord? origin)
        {
            if (board == null || !origin.HasValue)
            {
                return 0f;
            }

            float maxDistance = 0f;
            Coord pivot = origin.Value;
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    if (!board.IsActiveCell(row, column))
                    {
                        continue;
                    }

                    maxDistance = Mathf.Max(maxDistance, Distance(row, column, pivot));
                }
            }

            return maxDistance;
        }

        private static float StaggerDelay(int row, int column, Coord? origin, float maxDistance, float maxDelay)
        {
            if (!origin.HasValue || maxDistance <= 0f || maxDelay <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Distance(row, column, origin.Value) / maxDistance) * maxDelay;
        }

        private static float Distance(int row, int column, Coord pivot)
        {
            return Mathf.Abs(row - pivot.Row) + Mathf.Abs(column - pivot.Column);
        }
    }
}
