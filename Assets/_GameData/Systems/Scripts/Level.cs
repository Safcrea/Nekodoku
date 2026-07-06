using System;
using System.Collections.Generic;

namespace Meowdoku
{
    public sealed class Level
    {
        public string Title { get; }
        public string TargetWord { get; }
        public int Size { get; }
        public int[] Regions { get; }
        public Coord[] Solution { get; }
        public Coord[] LockedCats { get; }

        public Level(string title, string targetWord, int[] regions, Coord[] solution, Coord[] lockedCats)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Level title is required.", nameof(title));
            }

            if (string.IsNullOrWhiteSpace(targetWord))
            {
                throw new ArgumentException("Target word is required.", nameof(targetWord));
            }

            Title = title;
            TargetWord = targetWord.ToUpperInvariant();
            Size = TargetWord.Length;
            Regions = regions != null ? (int[])regions.Clone() : throw new ArgumentNullException(nameof(regions));
            Solution = solution != null ? (Coord[])solution.Clone() : throw new ArgumentNullException(nameof(solution));
            LockedCats = lockedCats != null ? (Coord[])lockedCats.Clone() : Array.Empty<Coord>();
        }

        public bool Contains(int row, int column)
        {
            return row >= 0 && row < Size && column >= 0 && column < Size;
        }

        public int RegionAt(int row, int column)
        {
            if (!Contains(row, column))
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Cell is outside the board.");
            }

            return Regions[(row * Size) + column];
        }

        public char LetterForCat(int row, int column)
        {
            for (int i = 0; i < Solution.Length; i++)
            {
                Coord coord = Solution[i];
                if (coord.Row == row && coord.Column == column)
                {
                    return TargetWord[i];
                }
            }

            return '\0';
        }

        public int IndexOfCat(int row, int column)
        {
            for (int i = 0; i < Solution.Length; i++)
            {
                Coord coord = Solution[i];
                if (coord.Row == row && coord.Column == column)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
