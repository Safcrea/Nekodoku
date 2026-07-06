using System;
namespace Meowdoku
{
    public sealed class PuzzleBoard
    {
        private const int StartingHearts = 3;

        private readonly CellMark[,] marks;
        private readonly bool[,] revealed;
        private readonly bool[,] solutionCats;
        private readonly bool[,] lockedCats;

        public Level Level { get; }
        public int Size => Level.Size;
        public int MistakeCount { get; private set; }
        public int HeartsRemaining => Math.Max(0, StartingHearts - MistakeCount);
        public bool IsFailed => HeartsRemaining <= 0;

        public PuzzleBoard(Level level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            marks = new CellMark[Size, Size];
            revealed = new bool[Size, Size];
            solutionCats = new bool[Size, Size];
            lockedCats = new bool[Size, Size];
            foreach (Coord coord in Level.Solution)
            {
                if (Level.Contains(coord.Row, coord.Column))
                {
                    solutionCats[coord.Row, coord.Column] = true;
                }
            }

            foreach (Coord coord in Level.LockedCats)
            {
                if (Level.Contains(coord.Row, coord.Column) && solutionCats[coord.Row, coord.Column])
                {
                    lockedCats[coord.Row, coord.Column] = true;
                }
            }

            Clear();
        }

        public void Clear()
        {
            ClearCellsForUndo();

            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (lockedCats[row, column])
                    {
                        revealed[row, column] = true;
                        marks[row, column] = CellMark.Cat;
                    }
                }
            }

            RecalculateMistakes();
        }

        public CellMark GetMark(int row, int column)
        {
            return Contains(row, column) ? marks[row, column] : CellMark.Empty;
        }

        public bool IsRevealed(int row, int column)
        {
            return Contains(row, column) && revealed[row, column];
        }

        public bool HasRevealedCat(int row, int column)
        {
            return IsRevealed(row, column) && HasHiddenCat(row, column);
        }

        public bool HasRevealedMiss(int row, int column)
        {
            return IsRevealed(row, column) && !HasHiddenCat(row, column);
        }

        public bool HasHiddenCat(int row, int column)
        {
            return Contains(row, column) && solutionCats[row, column];
        }

        public bool IsActiveCell(int row, int column)
        {
            return Contains(row, column);
        }

        public bool IsLocked(int row, int column)
        {
            return Contains(row, column) && lockedCats[row, column];
        }

        public char GetLetterForCat(int row, int column)
        {
            return Level.LetterForCat(row, column);
        }

        public int GetCatIndex(int row, int column)
        {
            return Level.IndexOfCat(row, column);
        }

        public bool CanSetCross(int row, int column, bool crossed)
        {
            if (!IsActiveCell(row, column) || IsFailed || revealed[row, column])
            {
                return false;
            }

            CellMark target = crossed ? CellMark.Cross : CellMark.Empty;
            return marks[row, column] != target;
        }

        public bool SetCross(int row, int column, bool crossed)
        {
            if (!CanSetCross(row, column, crossed))
            {
                return false;
            }

            marks[row, column] = crossed ? CellMark.Cross : CellMark.Empty;
            return true;
        }

        public bool ToggleCross(int row, int column)
        {
            if (!Contains(row, column) || IsFailed || revealed[row, column])
            {
                return false;
            }

            return SetCross(row, column, marks[row, column] != CellMark.Cross);
        }

        public bool MarkCross(int row, int column)
        {
            return SetCross(row, column, true);
        }

        public bool ClearCross(int row, int column)
        {
            return SetCross(row, column, false);
        }

        public CommitResult CommitCat(int row, int column)
        {
            if (!IsActiveCell(row, column) || IsFailed || revealed[row, column])
            {
                return CommitResult.NoChange;
            }

            revealed[row, column] = true;
            if (solutionCats[row, column])
            {
                marks[row, column] = CellMark.Cat;
                return CommitResult.Correct;
            }

            marks[row, column] = CellMark.Empty;
            RecalculateMistakes();
            return CommitResult.Wrong;
        }

        public int RevealedCatCount()
        {
            int count = 0;
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (HasRevealedCat(row, column))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public void ClearCellsForUndo()
        {
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    marks[row, column] = CellMark.Empty;
                    revealed[row, column] = false;
                }
            }

            MistakeCount = 0;
        }

        public void SetCellStateForUndo(int row, int column, CellMark mark, bool isRevealed)
        {
            if (!Contains(row, column))
            {
                return;
            }

            marks[row, column] = mark;
            revealed[row, column] = isRevealed;
            RecalculateMistakes();
        }

        public ValidationResult Validate()
        {
            return PuzzleValidator.Validate(this);
        }

        internal bool Contains(int row, int column)
        {
            return Level.Contains(row, column);
        }

        private void RecalculateMistakes()
        {
            int mistakes = 0;
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (revealed[row, column] && !solutionCats[row, column])
                    {
                        mistakes++;
                    }
                }
            }

            MistakeCount = mistakes;
        }

    }
}
