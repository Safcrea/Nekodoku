using UnityEngine;
using UnityEngine.EventSystems;

namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        public void ToggleCross(int row, int column)
        {
            if (inputLocked)
            {
                return;
            }

            bool placeCross = board.GetMark(row, column) != NekoCellMark.Cross;
            if (!board.CanSetCross(row, column, placeCross))
            {
                return;
            }

            SaveUndo();
            if (!board.SetCross(row, column, placeCross))
            {
                undoStack.Pop();
                return;
            }

            if (placeCross)
            {
                PlayLightCrossHaptic();
            }

            RefreshBoard();
            if (placeCross)
            {
                PlayCrossJelly(row, column);
            }
        }

        public void CommitCat(int row, int column)
        {
            if (inputLocked)
            {
                return;
            }

            int heartsBefore = board.HeartsRemaining;
            SaveUndo();
            NekoCommitResult result = board.CommitCat(row, column);
            if (result != NekoCommitResult.NoChange)
            {
                bool foundCat = result == NekoCommitResult.Correct;
                bool lostHeart = board.HeartsRemaining < heartsBefore;
                if (result == NekoCommitResult.Correct)
                {
                    NekoHaptics.Play(HapticStrength.Medium);
                }
                else if (lostHeart)
                {
                    NekoHaptics.Play(HapticStrength.Strong);
                }

                RefreshBoard();
                if (foundCat)
                {
                    PlayCatRevealForCat(row, column);
                }
                else if (lostHeart)
                {
                    ShakeBoard();
                    PunchCell(row, column, 1.08f);
                    SpawnFloatingLabel(row, column, "-1", ConflictColor);
                }
            }
            else
            {
                undoStack.Pop();
            }
        }

        public void BeginCrossDrag(PointerEventData eventData)
        {
            if (inputLocked)
            {
                return;
            }

            crossDragging = true;
            crossDragHasUndoSnapshot = false;
            lastCrossDragRow = -1;
            lastCrossDragColumn = -1;
            crossDragPlacesCrosses = true;

            if (TryPointerToCell(eventData, out int row, out int column))
            {
                crossDragPlacesCrosses = board.GetMark(row, column) != NekoCellMark.Cross;
                ApplyCrossDrag(row, column);
            }
        }

        public void CrossAtPointer(PointerEventData eventData)
        {
            if (inputLocked || !crossDragging)
            {
                return;
            }

            if (TryPointerToCell(eventData, out int row, out int column))
            {
                ApplyCrossDrag(row, column);
            }
        }

        public void EndCrossDrag()
        {
            crossDragging = false;
            crossDragHasUndoSnapshot = false;
            lastCrossDragRow = -1;
            lastCrossDragColumn = -1;
        }

        private void ApplyCrossDrag(int row, int column)
        {
            if (row == lastCrossDragRow && column == lastCrossDragColumn)
            {
                return;
            }

            lastCrossDragRow = row;
            lastCrossDragColumn = column;
            if (!board.CanSetCross(row, column, crossDragPlacesCrosses))
            {
                return;
            }

            if (!crossDragHasUndoSnapshot)
            {
                SaveUndo();
                crossDragHasUndoSnapshot = true;
            }

            if (!board.SetCross(row, column, crossDragPlacesCrosses))
            {
                return;
            }

            if (crossDragPlacesCrosses)
            {
                PlayLightCrossHaptic();
            }

            RefreshBoard();
            if (crossDragPlacesCrosses)
            {
                PlayCrossJelly(row, column);
            }
        }

        private bool TryPointerToCell(PointerEventData eventData, out int row, out int column)
        {
            row = -1;
            column = -1;

            if (inputLocked)
            {
                return false;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                CellUi cell = cells[i];
                if (!board.IsActiveCell(cell.Row, cell.Column) || !cell.Rect.gameObject.activeSelf)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(cell.Rect, eventData.position, eventData.pressEventCamera))
                {
                    row = cell.Row;
                    column = cell.Column;
                    return true;
                }
            }

            return false;
        }
    }
}
