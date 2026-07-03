namespace Meowdoku
{
    public sealed partial class NekoGameController
    {
        private void Undo()
        {
            if (undoStack.Count == 0)
            {
                return;
            }

            BoardSnapshot snapshot = undoStack.Pop();
            StopLevelAdvance();
            StopCatReveal();
            StopLetterFlipRoutines();
            StopJuiceEffects();
            StopTutorialGuide();
            board.ClearCellsForUndo();
            foreach (CellSnapshot cell in snapshot.Cells)
            {
                RestoreCell(cell);
            }

            RestoreLetterVisualState(snapshot.VisibleLetters);
            RefreshBoard();
            RefreshWordSlots();
            UpdateTutorialGuide(true);
        }

        private void SaveUndo()
        {
            CellSnapshot[] snapshot = new CellSnapshot[board.Size * board.Size];
            int i = 0;
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    snapshot[i++] = new CellSnapshot(row, column, board.GetMark(row, column), board.IsRevealed(row, column));
                }
            }

            undoStack.Push(new BoardSnapshot(
                snapshot,
                GetLetterVisualSnapshot()));
            while (undoStack.Count > 60)
            {
                TrimOldestUndo();
            }
        }

        private void TrimOldestUndo()
        {
            BoardSnapshot[] items = undoStack.ToArray();
            undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
            {
                undoStack.Push(items[i]);
            }
        }

        private void RestoreCell(CellSnapshot cell)
        {
            board.SetCellStateForUndo(cell.Row, cell.Column, cell.Mark, cell.Revealed);
        }

        private NekoCoord[] GetLetterVisualSnapshot()
        {
            NekoCoord[] snapshot = new NekoCoord[letterVisibleCats.Count];
            letterVisibleCats.CopyTo(snapshot);
            return snapshot;
        }

        private void RestoreLetterVisualState(NekoCoord[] visibleLetters)
        {
            letterVisibleCats.Clear();
            if (visibleLetters == null)
            {
                return;
            }

            for (int i = 0; i < visibleLetters.Length; i++)
            {
                letterVisibleCats.Add(visibleLetters[i]);
            }
        }
    }
}
