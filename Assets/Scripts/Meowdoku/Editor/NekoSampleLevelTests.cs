using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Meowdoku.Tests
{
    public sealed class NekoSampleLevelTests
    {
        private static readonly HashSet<string> BlockedTargetWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SEX",
            "SEXY",
            "NUDE",
            "NAKED",
            "DRUGS",
            "HATE",
            "KILL",
            "DEATH",
            "BLOOD"
        };

        [Test]
        public void SampleLevelsContainExpectedPrototypeCount()
        {
            Assert.AreEqual(100, NekoSampleLevels.Levels.Length);
        }

        [Test]
        public void SampleLevelsIncreaseDifficultyEveryThreeLevels()
        {
            for (int i = 0; i < NekoSampleLevels.Levels.Length; i++)
            {
                NekoLevel level = NekoSampleLevels.Levels[i];
                int expectedTier = (i / 3) + 1;
                Assert.AreEqual(expectedTier, level.DifficultyTier, $"{level.Title} has the wrong difficulty tier.");
                Assert.AreEqual(ExpectedLockedCatCount(i, expectedTier), level.LockedCats.Length, $"{level.Title} has the wrong starter clue count.");
                Assert.AreEqual(ExpectedBoardSize(i), level.Size, $"{level.Title} has the wrong board size.");
            }
        }

        [Test]
        public void SampleLevelsHaveValidDataAndUniqueSolutions()
        {
            foreach (NekoLevel level in NekoSampleLevels.Levels)
            {
                AssertLevelDataIsValid(level);
                Assert.AreEqual(1, CountLegalSolutions(level, 2), $"{level.Title} must have exactly one legal solution.");
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

        private static void AssertLevelDataIsValid(NekoLevel level)
        {
            int size = level.Size;
            Assert.GreaterOrEqual(size, 3, $"{level.Title} size is too small.");
            Assert.AreEqual(size * size, level.Regions.Length, $"{level.Title} region map length is wrong.");
            Assert.AreEqual(size, level.Solution.Length, $"{level.Title} solution length is wrong.");
            AssertTargetWordIsValid(level);

            bool[] regionMapContainsId = new bool[size];
            foreach (int region in level.Regions)
            {
                Assert.GreaterOrEqual(region, 0, $"{level.Title} has a negative region id.");
                Assert.Less(region, size, $"{level.Title} has a region id outside 0..{size - 1}.");
                regionMapContainsId[region] = true;
            }

            for (int region = 0; region < size; region++)
            {
                Assert.IsTrue(regionMapContainsId[region], $"{level.Title} is missing region {region}.");
                Assert.IsTrue(IsRegionConnected(level, region), $"{level.Title} region {region} is split across the board.");
            }

            bool[] rows = new bool[size];
            bool[] columns = new bool[size];
            bool[] solutionRegions = new bool[size];

            for (int i = 0; i < level.Solution.Length; i++)
            {
                NekoCoord cat = level.Solution[i];
                Assert.IsTrue(level.Contains(cat.Row, cat.Column), $"{level.Title} has a solution cat outside the board.");
                Assert.IsFalse(rows[cat.Row], $"{level.Title} has more than one cat in row {cat.Row}.");
                Assert.IsFalse(columns[cat.Column], $"{level.Title} has more than one cat in column {cat.Column}.");

                int region = level.RegionAt(cat.Row, cat.Column);
                Assert.IsFalse(solutionRegions[region], $"{level.Title} has more than one cat in region {region}.");

                rows[cat.Row] = true;
                columns[cat.Column] = true;
                solutionRegions[region] = true;

                for (int j = i + 1; j < level.Solution.Length; j++)
                {
                    NekoCoord other = level.Solution[j];
                    bool touches = Math.Abs(cat.Row - other.Row) <= 1 && Math.Abs(cat.Column - other.Column) <= 1;
                    Assert.IsFalse(touches, $"{level.Title} has touching cats at {cat.Row},{cat.Column} and {other.Row},{other.Column}.");
                }
            }

            foreach (NekoCoord lockedCat in level.LockedCats)
            {
                Assert.IsTrue(ContainsCoord(level.Solution, lockedCat), $"{level.Title} has a locked cat that is not in the solution.");
            }
        }

        private static void AssertTargetWordIsValid(NekoLevel level)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(level.TargetWord), $"{level.Title} is missing a target word.");
            Assert.AreEqual(level.Size, level.TargetWord.Length, $"{level.Title} target word length must match board size.");
            Assert.Less(level.TargetWord.Length, 13, $"{level.Title} target word must be shorter than 13 letters.");
            Assert.IsFalse(BlockedTargetWords.Contains(level.TargetWord), $"{level.Title} target word is blocked.");

            for (int i = 0; i < level.TargetWord.Length; i++)
            {
                char letter = level.TargetWord[i];
                Assert.IsTrue(letter >= 'A' && letter <= 'Z', $"{level.Title} target word must contain A-Z letters only.");
            }
        }

        private static int CountLegalSolutions(NekoLevel level, int limit)
        {
            int size = level.Size;
            int[] columnsByRow = new int[size];
            bool[] usedColumns = new bool[size];
            bool[] usedRegions = new bool[size];
            int solutionCount = 0;

            void Search(int row)
            {
                if (solutionCount >= limit)
                {
                    return;
                }

                if (row == size)
                {
                    solutionCount++;
                    return;
                }

                for (int column = 0; column < size; column++)
                {
                    if (usedColumns[column])
                    {
                        continue;
                    }

                    if (row > 0 && Math.Abs(columnsByRow[row - 1] - column) <= 1)
                    {
                        continue;
                    }

                    int region = level.RegionAt(row, column);
                    if (usedRegions[region])
                    {
                        continue;
                    }

                    columnsByRow[row] = column;
                    usedColumns[column] = true;
                    usedRegions[region] = true;

                    Search(row + 1);

                    usedColumns[column] = false;
                    usedRegions[region] = false;
                }
            }

            Search(0);
            return solutionCount;
        }

        private static bool IsRegionConnected(NekoLevel level, int region)
        {
            int size = level.Size;
            bool[] visited = new bool[size * size];
            Queue<NekoCoord> queue = new Queue<NekoCoord>();
            int regionCellCount = 0;

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    if (level.RegionAt(row, column) != region)
                    {
                        continue;
                    }

                    regionCellCount++;
                    if (queue.Count == 0)
                    {
                        visited[(row * size) + column] = true;
                        queue.Enqueue(new NekoCoord(row, column));
                    }
                }
            }

            int connectedCellCount = 0;
            while (queue.Count > 0)
            {
                NekoCoord coord = queue.Dequeue();
                connectedCellCount++;
                TryVisit(coord.Row - 1, coord.Column);
                TryVisit(coord.Row + 1, coord.Column);
                TryVisit(coord.Row, coord.Column - 1);
                TryVisit(coord.Row, coord.Column + 1);
            }

            return connectedCellCount == regionCellCount;

            void TryVisit(int row, int column)
            {
                if (!level.Contains(row, column))
                {
                    return;
                }

                int index = (row * size) + column;
                if (visited[index] || level.RegionAt(row, column) != region)
                {
                    return;
                }

                visited[index] = true;
                queue.Enqueue(new NekoCoord(row, column));
            }
        }

        private static bool ContainsCoord(NekoCoord[] coords, NekoCoord target)
        {
            foreach (NekoCoord coord in coords)
            {
                if (coord.Equals(target))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ExpectedLockedCatCount(int levelIndex, int difficultyTier)
        {
            if (levelIndex == 0)
            {
                return 1;
            }

            if (difficultyTier <= 4)
            {
                return 2;
            }

            return difficultyTier <= 18 ? 1 : 0;
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
