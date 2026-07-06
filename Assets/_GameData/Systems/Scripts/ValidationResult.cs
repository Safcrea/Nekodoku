using System;
using System.Collections.Generic;

namespace Meowdoku
{
    public sealed class ValidationResult
    {
        public int RevealedCatCount { get; }
        public int HeartsRemaining { get; }
        public int MistakeCount { get; }
        public bool IsSolved { get; }
        public bool IsFailed { get; }
        public bool HasConflict => ConflictCells.Count > 0;
        public string Message { get; }
        public HashSet<Coord> ConflictCells { get; }

        public ValidationResult(
            int revealedCatCount,
            int heartsRemaining,
            int mistakeCount,
            bool isSolved,
            bool isFailed,
            string message,
            HashSet<Coord> conflictCells)
        {
            RevealedCatCount = revealedCatCount;
            HeartsRemaining = heartsRemaining;
            MistakeCount = mistakeCount;
            IsSolved = isSolved;
            IsFailed = isFailed;
            Message = message ?? string.Empty;
            ConflictCells = conflictCells ?? new HashSet<Coord>();
        }
    }
}
