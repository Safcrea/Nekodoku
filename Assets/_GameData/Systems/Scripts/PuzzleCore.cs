using System;

namespace Meowdoku
{
    public enum CellMark
    {
        Empty,
        Cat,
        Cross
    }

    public enum CommitResult
    {
        NoChange,
        Correct,
        Wrong
    }

    public readonly struct Coord : IEquatable<Coord>
    {
        public readonly int Row;
        public readonly int Column;

        public Coord(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public bool Equals(Coord other)
        {
            return Row == other.Row && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is Coord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Row * 397) ^ Column;
            }
        }
    }
}
