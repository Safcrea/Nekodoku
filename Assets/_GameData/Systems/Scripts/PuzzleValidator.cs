using System;
using System.Collections.Generic;

namespace Meowdoku
{
    public static class PuzzleValidator
    {
        public static ValidationResult Validate(PuzzleBoard board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            HashSet<Coord> conflicts = new HashSet<Coord>();
            int revealedCats = board.RevealedCatCount();
            bool failed = board.IsFailed;
            bool solved = !failed && revealedCats == board.Size;
            string message = BuildMessage(board, revealedCats, solved, failed);

            return new ValidationResult(
                revealedCats,
                board.HeartsRemaining,
                board.MistakeCount,
                solved,
                failed,
                message,
                conflicts);
        }

        private static string BuildMessage(PuzzleBoard board, int revealedCats, bool solved, bool failed)
        {
            if (solved)
            {
                return "Solved";
            }

            if (failed)
            {
                return "No hearts left. Restart or undo.";
            }

            if (revealedCats == 0)
            {
                return "Find one hidden cat in every row, column, and color.";
            }

            return "Keep finding the hidden cats.";
        }
    }
}
