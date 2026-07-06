using System.Collections.Generic;
using System.Linq;

namespace Meowdoku
{
    internal readonly struct CellSnapshot
    {
        public readonly int Row;
        public readonly int Column;
        public readonly NekoCellMark Mark;
        public readonly bool Revealed;

        public CellSnapshot(int row, int column, NekoCellMark mark, bool revealed)
        {
            Row = row;
            Column = column;
            Mark = mark;
            Revealed = revealed;
        }
    }

    internal readonly struct BoardSnapshot
    {
        public readonly CellSnapshot[] Cells;
        public readonly NekoCoord[] VisibleLetters;

        public BoardSnapshot(CellSnapshot[] cells, NekoCoord[] visibleLetters)
        {
            Cells = cells;
            VisibleLetters = visibleLetters;
        }
    }

    /// <summary>
    /// Bounded undo history for board mark/reveal state and which cat letters are visible.
    /// Pure board-state bookkeeping - the caller is responsible for stopping animations and
    /// refreshing the UI around a call to Save/TryPop.
    /// </summary>
    public sealed class NekoUndoStack
    {
        private const int MaxEntries = 60;
        private readonly Stack<BoardSnapshot> entries = new Stack<BoardSnapshot>();

        public void Clear()
        {
            entries.Clear();
        }

        public void Save(NekoPuzzleBoard board, IEnumerable<NekoCoord> visibleLetters)
        {
            CellSnapshot[] cells = new CellSnapshot[board.Size * board.Size];
            int i = 0;
            for (int row = 0; row < board.Size; row++)
            {
                for (int column = 0; column < board.Size; column++)
                {
                    cells[i++] = new CellSnapshot(row, column, board.GetMark(row, column), board.IsRevealed(row, column));
                }
            }

            entries.Push(new BoardSnapshot(cells, visibleLetters.ToArray()));
            while (entries.Count > MaxEntries)
            {
                TrimOldest();
            }
        }

        public void DiscardMostRecent()
        {
            if (entries.Count > 0)
            {
                entries.Pop();
            }
        }

        internal bool TryPop(out BoardSnapshot snapshot)
        {
            if (entries.Count == 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = entries.Pop();
            return true;
        }

        internal static void Apply(NekoPuzzleBoard board, BoardSnapshot snapshot)
        {
            board.ClearCellsForUndo();
            foreach (CellSnapshot cell in snapshot.Cells)
            {
                board.SetCellStateForUndo(cell.Row, cell.Column, cell.Mark, cell.Revealed);
            }
        }

        private void TrimOldest()
        {
            BoardSnapshot[] items = entries.ToArray();
            entries.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
            {
                entries.Push(items[i]);
            }
        }
    }
}
