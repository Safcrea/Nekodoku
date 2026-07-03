using System;

namespace Meowdoku
{
    public enum NekoCellMark
    {
        Empty,
        Cat,
        Cross
    }

    public enum NekoCommitResult
    {
        NoChange,
        Correct,
        Wrong
    }

    public readonly struct NekoCoord : IEquatable<NekoCoord>
    {
        public readonly int Row;
        public readonly int Column;

        public NekoCoord(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public bool Equals(NekoCoord other)
        {
            return Row == other.Row && Column == other.Column;
        }

        public override bool Equals(object obj)
        {
            return obj is NekoCoord other && Equals(other);
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
