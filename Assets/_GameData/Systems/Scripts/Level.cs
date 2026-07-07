using System;
using System.Collections.Generic;

namespace Meowdoku
{
    public sealed class Level
    {
        public int Size { get; }
        public int[] Regions { get; }
        public Coord[] Solution { get; }
        public Coord[] LockedCats { get; }

        public Level(int[] regions, Coord[] solution, Coord[] lockedCats)
        {
            Regions = regions != null ? (int[])regions.Clone() : throw new ArgumentNullException(nameof(regions));
            Solution = solution != null ? (Coord[])solution.Clone() : throw new ArgumentNullException(nameof(solution));
            LockedCats = lockedCats != null ? (Coord[])lockedCats.Clone() : Array.Empty<Coord>();
            Size = Solution.Length;
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
