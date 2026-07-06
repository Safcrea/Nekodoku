using System.Collections.Generic;
using NUnit.Framework;

namespace Meowdoku.Tests
{
    public sealed class NekoSampleLevelTests
    {
        [Test]
        public void SampleLevelsContainExpectedPrototypeCount()
        {
            Assert.AreEqual(100, NekoSampleLevels.Levels.Length);
        }

        [Test]
        public void SampleLevelsFollowStarterClueAndBoardSizeCurve()
        {
            for (int i = 0; i < NekoSampleLevels.Levels.Length; i++)
            {
                NekoLevel level = NekoSampleLevels.Levels[i];
                int variantSeed = (i / 3) + 1;
                Assert.AreEqual(ExpectedLockedCatCount(i, variantSeed), level.LockedCats.Length, $"{level.Title} has the wrong starter clue count.");
                Assert.AreEqual(ExpectedBoardSize(i), level.Size, $"{level.Title} has the wrong board size.");
            }
        }

        [Test]
        public void SampleLevelsHaveValidDataAndUniqueSolutions()
        {
            foreach (NekoLevel level in NekoSampleLevels.Levels)
            {
                List<string> issues = NekoLevelValidator.FindStructuralIssues(level);
                Assert.IsEmpty(issues, string.Join("\n", issues));
                Assert.AreEqual(1, NekoLevelValidator.CountLegalSolutions(level, 2), $"{level.Title} must have exactly one legal solution.");
            }
        }

        [Test]
        public void SampleLevelLettersFollowSolutionOrder()
        {
            foreach (NekoLevel level in NekoSampleLevels.Levels)
            {
                for (int i = 0; i < level.Solution.Length; i++)
                {
                    NekoCoord coord = level.Solution[i];
                    Assert.AreEqual(level.TargetWord[i], level.LetterForCat(coord.Row, coord.Column), $"{level.Title} letter {i} is not mapped to its solution cat.");
                }
            }
        }

        [Test]
        public void CorrectCatCommitKeepsEveryCellActive()
        {
            NekoLevel level = NekoSampleLevels.Levels[0];
            NekoPuzzleBoard board = new NekoPuzzleBoard(level);
            NekoCoord cat = level.Solution[1];

            Assert.AreEqual(NekoCommitResult.Correct, board.CommitCat(cat.Row, cat.Column));

            for (int row = 0; row < level.Size; row++)
            {
                for (int column = 0; column < level.Size; column++)
                {
                    Assert.IsTrue(board.IsActiveCell(row, column), $"Cell {row},{column} should stay active after a cat reveal.");
                }
            }
        }

        [Test]
        public void CorrectCatCommitDoesNotAutoCrossRuledOutCells()
        {
            NekoLevel level = NekoSampleLevels.Levels[0];
            NekoPuzzleBoard board = new NekoPuzzleBoard(level);
            NekoCoord cat = level.Solution[1];

            Assert.AreEqual(NekoCommitResult.Correct, board.CommitCat(cat.Row, cat.Column));

            for (int column = 0; column < level.Size; column++)
            {
                if (column == cat.Column)
                {
                    continue;
                }

                Assert.AreEqual(NekoCellMark.Empty, board.GetMark(cat.Row, column), $"Cell {cat.Row},{column} should not be auto-crossed.");
            }

            for (int row = 0; row < level.Size; row++)
            {
                if (row == cat.Row)
                {
                    continue;
                }

                Assert.AreEqual(NekoCellMark.Empty, board.GetMark(row, cat.Column), $"Cell {row},{cat.Column} should not be auto-crossed.");
            }
        }

        [Test]
        public void MultipleCatCommitsKeepTheFixedBoardShape()
        {
            NekoLevel level = NekoSampleLevels.Levels[0];
            NekoPuzzleBoard board = new NekoPuzzleBoard(level);

            foreach (NekoCoord cat in level.Solution)
            {
                if (!board.IsRevealed(cat.Row, cat.Column))
                {
                    Assert.AreEqual(NekoCommitResult.Correct, board.CommitCat(cat.Row, cat.Column));
                }
            }

            Assert.AreEqual(level.Size, board.RevealedCatCount());
            for (int row = 0; row < level.Size; row++)
            {
                for (int column = 0; column < level.Size; column++)
                {
                    Assert.IsTrue(board.IsActiveCell(row, column), $"Cell {row},{column} should still be active on solve.");
                }
            }
        }

        private static int ExpectedLockedCatCount(int levelIndex, int variantSeed)
        {
            if (levelIndex == 0)
            {
                return 1;
            }

            if (variantSeed <= 4)
            {
                return 2;
            }

            return variantSeed <= 18 ? 1 : 0;
        }

        private static int ExpectedBoardSize(int levelIndex)
        {
            if (levelIndex < 18)
            {
                return 5;
            }

            if (levelIndex < 42)
            {
                return 6;
            }

            if (levelIndex < 66)
            {
                return 7;
            }

            if (levelIndex < 84)
            {
                return 8;
            }

            return 9;
        }
    }
}
