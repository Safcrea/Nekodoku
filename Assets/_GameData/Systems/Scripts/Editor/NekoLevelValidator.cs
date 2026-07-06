using System;
using System.Collections.Generic;

namespace Meowdoku
{
    /// <summary>
    /// Authoring-time checks for level data: board/solution shape, region connectivity,
    /// and puzzle solvability. Used by the sample level tests, the JSON exporter, and
    /// (eventually) the in-editor level editor. Not referenced at runtime.
    /// </summary>
    public static class NekoLevelValidator
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

        public static List<string> FindStructuralIssues(NekoLevel level)
        {
            List<string> issues = new List<string>();
            if (level == null)
            {
                issues.Add("Level is null.");
                return issues;
            }

            int size = level.Size;
            if (size < 3)
            {
                issues.Add($"{level.Title}: size {size} is too small.");
            }

            if (level.Regions.Length != size * size)
            {
                issues.Add($"{level.Title}: region map length {level.Regions.Length} does not match {size}x{size}.");
            }

            if (level.Solution.Length != size)
            {
                issues.Add($"{level.Title}: solution length {level.Solution.Length} does not match size {size}.");
            }

            AddTargetWordIssues(level, issues);
            AddRegionIssues(level, size, issues);
            AddSolutionIssues(level, size, issues);
            AddLockedCatIssues(level, issues);

            return issues;
        }

        public static int CountLegalSolutions(NekoLevel level, int limit)
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

        public static bool IsRegionConnected(NekoLevel level, int region)
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

        private static void AddTargetWordIssues(NekoLevel level, List<string> issues)
        {
            if (string.IsNullOrWhiteSpace(level.TargetWord))
            {
                issues.Add($"{level.Title}: missing a target word.");
                return;
            }

            if (level.TargetWord.Length != level.Size)
            {
                issues.Add($"{level.Title}: target word length must match board size.");
            }

            if (level.TargetWord.Length >= 13)
            {
                issues.Add($"{level.Title}: target word must be shorter than 13 letters.");
            }

            if (BlockedTargetWords.Contains(level.TargetWord))
            {
                issues.Add($"{level.Title}: target word is blocked.");
            }

            foreach (char letter in level.TargetWord)
            {
                if (letter < 'A' || letter > 'Z')
                {
                    issues.Add($"{level.Title}: target word must contain A-Z letters only.");
                    break;
                }
            }
        }

        private static void AddRegionIssues(NekoLevel level, int size, List<string> issues)
        {
            bool[] regionMapContainsId = new bool[size];
            foreach (int region in level.Regions)
            {
                if (region < 0 || region >= size)
                {
                    issues.Add($"{level.Title}: has a region id outside 0..{size - 1}.");
                    continue;
                }

                regionMapContainsId[region] = true;
            }

            for (int region = 0; region < size; region++)
            {
                if (!regionMapContainsId[region])
                {
                    issues.Add($"{level.Title}: missing region {region}.");
                    continue;
                }

                if (!IsRegionConnected(level, region))
                {
                    issues.Add($"{level.Title}: region {region} is split across the board.");
                }
            }
        }

        private static void AddSolutionIssues(NekoLevel level, int size, List<string> issues)
        {
            bool[] rows = new bool[size];
            bool[] columns = new bool[size];
            bool[] solutionRegions = new bool[size];

            for (int i = 0; i < level.Solution.Length; i++)
            {
                NekoCoord cat = level.Solution[i];
                if (!level.Contains(cat.Row, cat.Column))
                {
                    issues.Add($"{level.Title}: has a solution cat outside the board.");
                    continue;
                }

                if (rows[cat.Row])
                {
                    issues.Add($"{level.Title}: has more than one cat in row {cat.Row}.");
                }

                if (columns[cat.Column])
                {
                    issues.Add($"{level.Title}: has more than one cat in column {cat.Column}.");
                }

                int region = level.RegionAt(cat.Row, cat.Column);
                if (solutionRegions[region])
                {
                    issues.Add($"{level.Title}: has more than one cat in region {region}.");
                }

                rows[cat.Row] = true;
                columns[cat.Column] = true;
                solutionRegions[region] = true;

                for (int j = i + 1; j < level.Solution.Length; j++)
                {
                    NekoCoord other = level.Solution[j];
                    bool touches = Math.Abs(cat.Row - other.Row) <= 1 && Math.Abs(cat.Column - other.Column) <= 1;
                    if (touches)
                    {
                        issues.Add($"{level.Title}: has touching cats at {cat.Row},{cat.Column} and {other.Row},{other.Column}.");
                    }
                }
            }
        }

        private static void AddLockedCatIssues(NekoLevel level, List<string> issues)
        {
            foreach (NekoCoord lockedCat in level.LockedCats)
            {
                if (!ContainsCoord(level.Solution, lockedCat))
                {
                    issues.Add($"{level.Title}: has a locked cat that is not in the solution.");
                }
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
    }
}
