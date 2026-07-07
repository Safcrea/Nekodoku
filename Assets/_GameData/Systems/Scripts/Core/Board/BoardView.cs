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

        [SerializeField]
        private GameObject cellPrefab;

        [SerializeField]
        private RectTransform boardRoot;

        [SerializeField]
        private BoardGridSizer boardGridSizer;

        [SerializeField]
        private TransformSpringComponent boardShakeSpring;

        private readonly List<GridCell> cells = new List<GridCell>();

        private GridCell[,] cellGrid;

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
                cell.SetInteractable(!inputLocked);
            }
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
            if (boardShakeSpring != null)
            {
                boardShakeSpring.ReachEquilibriumPosition();
            }

            foreach (GridCell cell in cells)
            {
                cell.StopJuice();
            }
        }
    }
}
