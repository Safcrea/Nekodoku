using System;
using System.Text;

namespace Meowdoku
{
    /// <summary>
    /// One hidden cat: its board position, and whether it starts pre-revealed.
    /// </summary>
    [Serializable]
    public sealed class LevelCatData
    {
        public int row;
        public int column;
        public bool locked;


        public LevelCatData(Coord coord, bool locked)
        {
            row = coord.Row;
            column = coord.Column;
            this.locked = locked;
        }

        public Coord ToCoord()
        {
            return new Coord(row, column);
        }
    }

    /// <summary>
    /// Plain, JSON-serializable level definition. This is the source-of-truth format for
    /// levels: authored by the (future) level editor or website, loaded by <see cref="LevelDatabase"/>,
    /// and converted into the runtime <see cref="Level"/> model.
    ///
    /// Regions are row-strings, one character per column, e.g. ["230", "220", "221"] for a
    /// 3x3 board. Each character is a region id 0-9, so this format supports boards up to
    /// size 10 (the game currently maxes out at 9x9).
    /// </summary>
    [Serializable]
    public sealed class LevelData
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string id;
        public string title;
        public int size;
        public string[] regions;
        public LevelCatData[] cats;

        public static LevelData FromLevel(string id, Level level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            return new LevelData
            {
                id = id,
                size = level.Size,
                regions = ToRegionRows(level.Regions, level.Size),
                cats = ToCatData(level)
            };
        }

        public Level ToLevel()
        {
            string label = string.IsNullOrWhiteSpace(id) ? "<unknown level>" : id;

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidOperationException($"Level '{label}' is missing a title.");
            }

            if (cats == null || cats.Length != size)
            {
                int actual = cats?.Length ?? 0;
                throw new InvalidOperationException($"Level '{label}' has {actual} cats but size is {size}.");
            }

            int[] regionValues = ParseRegionRows(regions, size, label);
            Coord[] solution = new Coord[size];
            int lockedCount = 0;

            for (int i = 0; i < size; i++)
            {
                LevelCatData cat = cats[i];
                if (cat == null)
                {
                    throw new InvalidOperationException($"Level '{label}' has a null cat entry at index {i}.");
                }
                solution[i] = cat.ToCoord();
                if (cat.locked)
                {
                    lockedCount++;
                }
            }

            Coord[] lockedCats = new Coord[lockedCount];
            int lockedIndex = 0;
            for (int i = 0; i < size; i++)
            {
                if (cats[i].locked)
                {
                    lockedCats[lockedIndex++] = solution[i];
                }
            }


            return new Level(regionValues, solution, lockedCats);
        }

        private static string[] ToRegionRows(int[] regionValues, int size)
        {
            string[] rows = new string[size];
            for (int row = 0; row < size; row++)
            {
                StringBuilder rowBuilder = new StringBuilder(size);
                for (int column = 0; column < size; column++)
                {
                    rowBuilder.Append(regionValues[(row * size) + column]);
                }

                rows[row] = rowBuilder.ToString();
            }

            return rows;
        }

        private static int[] ParseRegionRows(string[] rows, int size, string label)
        {
            if (rows == null || rows.Length != size)
            {
                int actual = rows?.Length ?? 0;
                throw new InvalidOperationException($"Level '{label}' has {actual} region rows but size is {size}.");
            }

            int[] values = new int[size * size];
            for (int row = 0; row < size; row++)
            {
                string rowText = rows[row];
                if (rowText == null || rowText.Length != size)
                {
                    int actual = rowText?.Length ?? 0;
                    throw new InvalidOperationException($"Level '{label}' region row {row} has {actual} columns but size is {size}.");
                }

                for (int column = 0; column < size; column++)
                {
                    char digit = rowText[column];
                    if (digit < '0' || digit > '9')
                    {
                        throw new InvalidOperationException($"Level '{label}' region row {row} has a non-digit character at column {column}.");
                    }

                    values[(row * size) + column] = digit - '0';
                }
            }

            return values;
        }

        private static LevelCatData[] ToCatData(Level level)
        {
            Coord[] solution = level.Solution;
            LevelCatData[] result = new LevelCatData[solution.Length];
            for (int i = 0; i < solution.Length; i++)
            {
                Coord coord = solution[i];
                bool locked = ContainsCoord(level.LockedCats, coord);
                result[i] = new LevelCatData(coord, locked);
            }

            return result;
        }

        private static bool ContainsCoord(Coord[] coords, Coord target)
        {
            foreach (Coord coord in coords)
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
