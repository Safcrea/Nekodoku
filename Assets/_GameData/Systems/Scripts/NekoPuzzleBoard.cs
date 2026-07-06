using System;
namespace Meowdoku
{
    public sealed class NekoPuzzleBoard
    {
        private const int StartingHearts = 3;

        private readonly NekoCellMark[,] marks;
        private readonly bool[,] revealed;
        private readonly bool[,] solutionCats;
        private readonly bool[,] lockedCats;

        public NekoLevel Level { get; }
        public int Size => Level.Size;
        public int MistakeCount { get; private set; }
        public int HeartsRemaining => Math.Max(0, StartingHearts - MistakeCount);
        public bool IsFailed => HeartsRemaining <= 0;

        public NekoPuzzleBoard(NekoLevel level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            marks = new NekoCellMark[Size, Size];
            revealed = new bool[Size, Size];
            solutionCats = new bool[Size, Size];
            lockedCats = new bool[Size, Size];
            foreach (NekoCoord coord in Level.Solution)
            {
                if (Level.Contains(coord.Row, coord.Column))
                {
                    solutionCats[coord.Row, coord.Column] = true;
                }
            }

            foreach (NekoCoord coord in Level.LockedCats)
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
                        marks[row, column] = NekoCellMark.Cat;
                    }
                }
            }

            RecalculateMistakes();
        }

        public NekoCellMark GetMark(int row, int column)
        {
            return Contains(row, column) ? marks[row, column] : NekoCellMark.Empty;
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

            NekoCellMark target = crossed ? NekoCellMark.Cross : NekoCellMark.Empty;
            return marks[row, column] != target;
        }

        public bool SetCross(int row, int column, bool crossed)
        {
            if (!CanSetCross(row, column, crossed))
            {
                return false;
            }

            marks[row, column] = crossed ? NekoCellMark.Cross : NekoCellMark.Empty;
            return true;
        }

        public bool ToggleCross(int row, int column)
        {
            if (!Contains(row, column) || IsFailed || revealed[row, column])
            {
                return false;
            }

            return SetCross(row, column, marks[row, column] != NekoCellMark.Cross);
        }

        public bool MarkCross(int row, int column)
        {
            return SetCross(row, column, true);
        }

        public bool ClearCross(int row, int column)
        {
            return SetCross(row, column, false);
        }

        public NekoCommitResult CommitCat(int row, int column)
        {
            if (!IsActiveCell(row, column) || IsFailed || revealed[row, column])
            {
                return NekoCommitResult.NoChange;
            }

            revealed[row, column] = true;
            if (solutionCats[row, column])
            {
                marks[row, column] = NekoCellMark.Cat;
                return NekoCommitResult.Correct;
            }

            marks[row, column] = NekoCellMark.Empty;
            RecalculateMistakes();
            return NekoCommitResult.Wrong;
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
                    marks[row, column] = NekoCellMark.Empty;
                    revealed[row, column] = false;
                }
            }

            MistakeCount = 0;
        }

        public void SetCellStateForUndo(int row, int column, NekoCellMark mark, bool isRevealed)
        {
            if (!Contains(row, column))
            {
                return;
            }

            marks[row, column] = mark;
            revealed[row, column] = isRevealed;
            RecalculateMistakes();
        }

        public NekoValidationResult Validate()
        {
            return NekoPuzzleValidator.Validate(this);
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
