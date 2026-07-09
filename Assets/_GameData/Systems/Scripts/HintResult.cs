using System;

namespace Meowdoku
{
    /// <summary>
    /// The outcome of <see cref="PuzzleBoard.FindHint"/>: either a set of cells that can be deduced
    /// to cross from one of the puzzle's three rules, or (when nothing is deducible) a still-hidden
    /// cat cell to point at instead. Never both, never neither unless <see cref="HasHint"/> is false.
    /// </summary>
    public readonly struct HintResult
    {
        public static readonly HintResult None = new HintResult(Array.Empty<Coord>(), null);

        public readonly Coord[] CrossCells;
        public readonly Coord? RevealCat;

        private HintResult(Coord[] crossCells, Coord? revealCat)
        {
            CrossCells = crossCells;
            RevealCat = revealCat;
        }

        public static HintResult Cross(Coord[] cells) => new HintResult(cells, null);

        public static HintResult Reveal(Coord cat) => new HintResult(Array.Empty<Coord>(), cat);

        public bool HasHint => CrossCells.Length > 0 || RevealCat.HasValue;
    }
}
