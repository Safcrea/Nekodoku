using System;
using System.Collections.Generic;
namespace Meowdoku
{
    public sealed class PuzzleBoard
    {
        private const int StartingHearts = 3;

        private readonly CellMark[,] marks;
        private readonly bool[,] revealed;
        private readonly bool[,] solutionCats;
        private readonly bool[,] lockedCats;
        private readonly bool[,] penalizedMisses;

        private int bonusHearts;

        public Level Level { get; }
        public int Size => Level.Size;
        public int MistakeCount { get; private set; }
        public int HeartsRemaining => Math.Max(0, StartingHearts + bonusHearts - MistakeCount);
        public bool IsFailed => HeartsRemaining <= 0;

        /// <summary>True once this attempt has claimed its one-per-attempt extra life from the level-failed
        /// screen. Reset by <see cref="Clear"/> (a fresh level load or an explicit retry), so the offer
        /// comes back on the next attempt but never twice within the same one.</summary>
        public bool HasClaimedExtraLife { get; private set; }

        public PuzzleBoard(Level level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            marks = new CellMark[Size, Size];
            revealed = new bool[Size, Size];
            solutionCats = new bool[Size, Size];
            lockedCats = new bool[Size, Size];
            penalizedMisses = new bool[Size, Size];
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

        /// <summary>Starts a fresh attempt. Level-authored locked cats are always restored, then
        /// additional solution cats are revealed in level order until the requested minimum is met.
        /// This makes retry assistance part of the initial board state without changing level data.</summary>
        public void Clear(int minimumRevealedCatCount = 0)
        {
            ClearCellsForUndo();

            int revealedCatCount = 0;

            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (lockedCats[row, column])
                    {
                        revealed[row, column] = true;
                        marks[row, column] = CellMark.Cat;
                        revealedCatCount++;
                    }
                }
            }

            int targetRevealedCatCount = Math.Min(Size, Math.Max(0, minimumRevealedCatCount));
            for (int i = 0; i < Level.Solution.Length && revealedCatCount < targetRevealedCatCount; i++)
            {
                Coord coord = Level.Solution[i];
                if (!revealed[coord.Row, coord.Column])
                {
                    revealed[coord.Row, coord.Column] = true;
                    marks[coord.Row, coord.Column] = CellMark.Cat;
                    revealedCatCount++;
                }
            }

            bonusHearts = 0;
            HasClaimedExtraLife = false;
            RecalculateMistakes();
        }

        /// <summary>Grants the one-per-attempt extra life. No-ops if already claimed this attempt
        /// (the UI should already prevent this by hiding the offer, but this is the authoritative guard).</summary>
        public bool AddLife()
        {
            if (HasClaimedExtraLife)
            {
                return false;
            }

            HasClaimedExtraLife = true;
            bonusHearts++;
            return true;
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

        public bool IsPenalizedMiss(int row, int column)
        {
            return Contains(row, column) && penalizedMisses[row, column];
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

        public bool ClearPlayerCrosses()
        {
            bool changed = false;
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (!revealed[row, column] && marks[row, column] == CellMark.Cross)
                    {
                        marks[row, column] = CellMark.Empty;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        public CommitResult CommitCat(int row, int column, bool penalizeWrongGuess = true)
        {
            if (!IsActiveCell(row, column) || IsFailed || revealed[row, column])
            {
                return CommitResult.NoChange;
            }

            revealed[row, column] = true;
            if (solutionCats[row, column])
            {
                marks[row, column] = CellMark.Cat;
                penalizedMisses[row, column] = false;
                return CommitResult.Correct;
            }

            marks[row, column] = CellMark.Empty;
            penalizedMisses[row, column] = penalizeWrongGuess;
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

        /// <summary>First still-hidden, unrevealed cat in row-major order - used by the "Reveal A Cat"
        /// powerup. Deterministic rather than random, but trivial to change later.</summary>
        public bool TryFindHiddenCat(out Coord coord)
        {
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (HasHiddenCat(row, column) && !IsRevealed(row, column))
                    {
                        coord = new Coord(row, column);
                        return true;
                    }
                }
            }

            coord = default;
            return false;
        }

        /// <summary>
        /// The "Hint" powerup's deduction scan. Tries each rule tier in order - column, then row, then
        /// touching (8-neighbor), then color region - against every revealed cat (row-major); the first
        /// tier where any revealed cat still has an uncrossed, unrevealed cell wins, and that cat's whole
        /// set of remaining cells for that tier is returned together (not one cell at a time). Falls back
        /// to pointing at a still-hidden cat when nothing is deducible anywhere.
        /// </summary>
        public HintResult FindHint()
        {
            List<Coord> revealedCats = new List<Coord>();
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (HasRevealedCat(row, column))
                    {
                        revealedCats.Add(new Coord(row, column));
                    }
                }
            }

            Coord[] columnHint = FindTierHint(revealedCats, ColumnCellsExceptCat);
            if (columnHint.Length > 0)
            {
                return HintResult.Cross(columnHint);
            }

            Coord[] rowHint = FindTierHint(revealedCats, RowCellsExceptCat);
            if (rowHint.Length > 0)
            {
                return HintResult.Cross(rowHint);
            }

            Coord[] neighborHint = FindTierHint(revealedCats, NeighborCellsExceptCat);
            if (neighborHint.Length > 0)
            {
                return HintResult.Cross(neighborHint);
            }

            Coord[] regionHint = FindTierHint(revealedCats, RegionCellsExceptCat);
            if (regionHint.Length > 0)
            {
                return HintResult.Cross(regionHint);
            }

            return TryFindHiddenCat(out Coord hidden) ? HintResult.Reveal(hidden) : HintResult.None;
        }

        private Coord[] FindTierHint(List<Coord> revealedCats, Func<Coord, List<Coord>> cellsForCat)
        {
            foreach (Coord cat in revealedCats)
            {
                List<Coord> uncrossed = new List<Coord>();
                foreach (Coord cell in cellsForCat(cat))
                {
                    if (!IsRevealed(cell.Row, cell.Column) && GetMark(cell.Row, cell.Column) != CellMark.Cross)
                    {
                        uncrossed.Add(cell);
                    }
                }

                if (uncrossed.Count > 0)
                {
                    return uncrossed.ToArray();
                }
            }

            return Array.Empty<Coord>();
        }

        private List<Coord> ColumnCellsExceptCat(Coord cat)
        {
            List<Coord> cells = new List<Coord>();
            for (int row = 0; row < Size; row++)
            {
                if (row != cat.Row)
                {
                    cells.Add(new Coord(row, cat.Column));
                }
            }

            return cells;
        }

        private List<Coord> RowCellsExceptCat(Coord cat)
        {
            List<Coord> cells = new List<Coord>();
            for (int column = 0; column < Size; column++)
            {
                if (column != cat.Column)
                {
                    cells.Add(new Coord(cat.Row, column));
                }
            }

            return cells;
        }

        private static readonly (int dr, int dc)[] NeighborOffsets =
        {
            (-1, -1), (-1, 0), (-1, 1),
            (0, -1), (0, 1),
            (1, -1), (1, 0), (1, 1)
        };

        private List<Coord> NeighborCellsExceptCat(Coord cat)
        {
            List<Coord> cells = new List<Coord>();
            foreach ((int dr, int dc) in NeighborOffsets)
            {
                int row = cat.Row + dr;
                int column = cat.Column + dc;
                if (Contains(row, column))
                {
                    cells.Add(new Coord(row, column));
                }
            }

            return cells;
        }

        private List<Coord> RegionCellsExceptCat(Coord cat)
        {
            int region = Level.RegionAt(cat.Row, cat.Column);
            List<Coord> cells = new List<Coord>();
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    if (row == cat.Row && column == cat.Column)
                    {
                        continue;
                    }

                    if (Level.RegionAt(row, column) == region)
                    {
                        cells.Add(new Coord(row, column));
                    }
                }
            }

            return cells;
        }

        public void ClearCellsForUndo()
        {
            for (int row = 0; row < Size; row++)
            {
                for (int column = 0; column < Size; column++)
                {
                    marks[row, column] = CellMark.Empty;
                    revealed[row, column] = false;
                    penalizedMisses[row, column] = false;
                }
            }

            MistakeCount = 0;
        }

        public void SetCellStateForUndo(
            int row,
            int column,
            CellMark mark,
            bool isRevealed,
            bool isPenalizedMiss)
        {
            if (!Contains(row, column))
            {
                return;
            }

            marks[row, column] = mark;
            revealed[row, column] = isRevealed;
            penalizedMisses[row, column] = isPenalizedMiss;
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
                    if (revealed[row, column] && !solutionCats[row, column] && penalizedMisses[row, column])
                    {
                        mistakes++;
                    }
                }
            }

            MistakeCount = mistakes;
        }

    }
}
