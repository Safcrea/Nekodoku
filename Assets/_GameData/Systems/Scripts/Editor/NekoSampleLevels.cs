using System;
using System.Collections.Generic;

namespace Meowdoku
{
    public static class NekoSampleLevels
    {
        private const int TargetLevelCount = 100;
        private const int FiveByFiveLevelCount = 18;
        private const int SixBySixLevelCount = 24;
        private const int SevenBySevenLevelCount = 24;
        private const int EightByEightLevelCount = 18;
        private const int TransformCount = 8;

        private static readonly string[] FiveLetterWords =
        {
            "APPLE", "RIVER", "LIGHT", "BREAD", "HOUSE", "PLANT",
            "STONE", "MUSIC", "WATER", "CLOUD", "FIELD", "TRAIN",
            "SMILE", "CHAIR", "GRASS", "BRAVE", "SHARP", "WORLD"
        };

        private static readonly string[] SixLetterWords =
        {
            "PLANET", "BRIDGE", "GARDEN", "MARKET", "BUTTON", "POCKET",
            "CASTLE", "FOREST", "SILVER", "SUMMER", "WINTER", "ORANGE",
            "CANDLE", "ISLAND", "STREAM", "ROCKET", "PUZZLE", "BREEZE",
            "SCREEN", "TRAVEL", "SPRING", "FLOWER", "DESERT", "ANCHOR"
        };

        private static readonly string[] SevenLetterWords =
        {
            "JOURNEY", "BALANCE", "HARVEST", "MORNING", "PICTURE", "COUNTRY",
            "FREEDOM", "DIAMOND", "LIBRARY", "KITCHEN", "WEATHER", "THOUGHT",
            "LANTERN", "COMPASS", "VILLAGE", "CRYSTAL", "MACHINE", "KINGDOM",
            "HORIZON", "DYNAMIC", "VICTORY", "MYSTERY", "RESOLVE", "PATTERN"
        };

        private static readonly string[] EightLetterWords =
        {
            "NOTEBOOK", "MOUNTAIN", "TREASURE", "DISTANCE", "FESTIVAL", "LANGUAGE",
            "QUESTION", "SUNLIGHT", "BASEMENT", "KEYBOARD", "AIRPLANE", "PAINTING",
            "MARATHON", "HOSPITAL", "CALENDAR", "BUILDING", "PASSPORT", "SANDWICH"
        };

        private static readonly string[] NineLetterWords =
        {
            "ADVENTURE", "BLUEPRINT", "CHALLENGE", "DISCOVERY", "EDUCATION", "FIRELIGHT",
            "HAPPINESS", "IMPORTANT", "LANDSCAPE", "NOTEBOOKS", "OPERATION", "PLANETARY",
            "REFERENCE", "SOMETHING", "TELEPHONE", "UNDERLINE"
        };

        private static readonly string[] TitleAdjectives =
        {
            "Porch", "Window", "Sunny", "Cozy", "Fuzzy", "Ribbon",
            "Shelf", "Laser", "Treat", "Plush", "Whisker", "Felix",
            "Cloud", "Garden", "Velvet", "Calico", "Kitten", "Moonlit",
            "Purring", "Hidden", "Silver", "Golden", "Sleepy", "Bright"
        };

        private static readonly string[] TitleNouns =
        {
            "Patrol", "Watch", "Steps", "Trail", "Print", "Route",
            "Scout", "Lane", "Map", "Pile", "Way", "Field",
            "Cloud", "Nap", "Path", "Keys", "Cove", "Nook",
            "Den", "Loft", "Dash", "Drift", "Puzzle", "Grove"
        };

        private static readonly LevelSeed[] FiveByFiveSeeds =
        {
            new LevelSeed("Porch Patrol", "APPLE", new[] { 3, 0, 2, 4, 1 }, "11100", "11100", "11243", "44443", "44444"),
            new LevelSeed("Window Watch", "RIVER", new[] { 0, 2, 4, 1, 3 }, "01122", "11112", "13112", "33111", "33144"),
            new LevelSeed("Sunbeam Steps", "LIGHT", new[] { 2, 0, 4, 1, 3 }, "12022", "12222", "11222", "13322", "13342"),
            new LevelSeed("Garden Trail", "BREAD", new[] { 1, 3, 0, 2, 4 }, "00011", "20111", "20111", "22311", "33314"),
            new LevelSeed("Paw Print", "HOUSE", new[] { 2, 0, 3, 1, 4 }, "00000", "10022", "13222", "33222", "33224"),
            new LevelSeed("Ribbon Route", "PLANT", new[] { 1, 4, 2, 0, 3 }, "00111", "22111", "22211", "32221", "22244"),
            new LevelSeed("Shelf Scout", "STONE", new[] { 2, 4, 1, 3, 0 }, "00011", "22211", "22333", "22333", "43333"),
            new LevelSeed("Laser Lane", "MUSIC", new[] { 2, 0, 3, 1, 4 }, "11002", "11222", "33324", "33334", "33334"),
            new LevelSeed("Treat Map", "WATER", new[] { 2, 4, 1, 3, 0 }, "44000", "44111", "42111", "44433", "44333"),
            new LevelSeed("Plush Pile", "CLOUD", new[] { 2, 0, 3, 1, 4 }, "11000", "11122", "11122", "13122", "33324"),
            new LevelSeed("Whisk Way", "FIELD", new[] { 3, 1, 4, 2, 0 }, "11000", "31222", "33322", "33322", "44322"),
            new LevelSeed("Felix Field", "TRAIN", new[] { 0, 3, 1, 4, 2 }, "02111", "02111", "22111", "22433", "24433"),
            new LevelSeed("Cozy Cloud", "SMILE", new[] { 3, 1, 4, 0, 2 }, "00002", "11122", "11422", "34442", "44444"),
            new LevelSeed("Sunny Nap", "CHAIR", new[] { 4, 1, 3, 0, 2 }, "11000", "11120", "11120", "31440", "31400"),
            new LevelSeed("Fuzzy Path", "GRASS", new[] { 2, 4, 0, 3, 1 }, "00001", "00111", "22233", "22333", "24333")
        };

        private static readonly LevelSeed[] SixBySixSeeds =
        {
            new LevelSeed("Kitten Keys", "PLANET", new[] { 1, 3, 5, 0, 2, 4 }, "101222", "111112", "315522", "315555", "314555", "335555"),
            new LevelSeed("Calico Cove", "BRIDGE", new[] { 4, 1, 3, 5, 0, 2 }, "111100", "115100", "115200", "115503", "455533", "555553"),
            new LevelSeed("Ribbon Maze", "GARDEN", new[] { 2, 5, 1, 4, 0, 3 }, "400211", "422221", "425111", "445135", "445555", "445555"),
            new LevelSeed("Pillow Path", "MARKET", new[] { 0, 3, 5, 2, 4, 1 }, "000011", "000112", "000122", "003333", "003343", "055444"),
            new LevelSeed("Tuxedo Trail", "BUTTON", new[] { 3, 0, 4, 1, 5, 2 }, "114000", "144004", "134424", "134444", "555554", "555555"),
            new LevelSeed("Cuddle Loft", "POCKET", new[] { 5, 2, 0, 3, 1, 4 }, "000000", "001300", "211330", "214330", "244333", "233353")
        };

        private static readonly LevelSeed[] SevenBySevenSeeds =
        {
            new LevelSeed("Whisker Grove", "JOURNEY", new[] { 0, 6, 4, 1, 3, 5, 2 }, "0333222", "0332221", "3334255", "3334555", "3344555", "3345555", "3666655"),
            new LevelSeed("Scratch Steps", "BALANCE", new[] { 0, 5, 1, 4, 2, 6, 3 }, "0333333", "3333311", "3233111", "6433355", "6446355", "6466365", "6666665"),
            new LevelSeed("Purring Path", "HARVEST", new[] { 0, 6, 1, 3, 5, 2, 4 }, "0011111", "0004111", "0244411", "2223411", "5553441", "5553441", "5566644")
        };

        private static readonly LevelSeed[] EightByEightSeeds =
        {
            new LevelSeed("Whiskers Drift", "NOTEBOOK", new[] { 5, 7, 4, 6, 2, 0, 3, 1 }, "22222001", "25542011", "55542233", "55544333", "56444433", "56464663", "56666673", "77777777"),
            new LevelSeed("Pawprint Loft", "MOUNTAIN", new[] { 3, 0, 2, 4, 6, 1, 5, 7 }, "00000033", "11000003", "11222233", "51223334", "55253344", "55553777", "55555677", "55557777"),
            new LevelSeed("Sunbeams Cove", "TREASURE", new[] { 4, 7, 5, 3, 1, 6, 2, 0 }, "40000001", "44002221", "44432222", "44333325", "44444225", "44444455", "44645555", "77666555")
        };

        private static readonly LevelSeed[] NineByNineSeeds =
        {
            new LevelSeed("Whiskered Peak", "ADVENTURE", new[] { 1, 5, 8, 3, 6, 4, 2, 0, 7 }, "001111111", "111111111", "111111112", "333333332", "333334444", "666654444", "666666666", "776666666", "777777788")
        };

        public static readonly NekoLevel[] Levels = BuildLevels();

        private static NekoLevel[] BuildLevels()
        {
            List<NekoLevel> levels = new List<NekoLevel>(TargetLevelCount);
            for (int i = 0; i < TargetLevelCount; i++)
            {
                GetLevelPack(i, out LevelSeed[] seeds, out string[] words, out int variantIndex);
                LevelSeed seed = seeds[variantIndex % seeds.Length];
                int variantSeed = (i / 3) + 1;
                int transform = variantIndex % TransformCount;
                int colorShift = ((variantIndex / TransformCount) + variantSeed) % seed.Size;
                string word = WordForLevel(seed, seeds, words, variantIndex);
                string title = TitleForLevel(seed, seeds, i, variantIndex);

                levels.Add(Create(title, word, LockedCatCountForLevel(i, variantSeed), seed, transform, colorShift));
            }

            return levels.ToArray();
        }

        private static void GetLevelPack(int levelIndex, out LevelSeed[] seeds, out string[] words, out int variantIndex)
        {
            if (levelIndex < FiveByFiveLevelCount)
            {
                seeds = FiveByFiveSeeds;
                words = FiveLetterWords;
                variantIndex = levelIndex;
                return;
            }

            levelIndex -= FiveByFiveLevelCount;
            if (levelIndex < SixBySixLevelCount)
            {
                seeds = SixBySixSeeds;
                words = SixLetterWords;
                variantIndex = levelIndex;
                return;
            }

            levelIndex -= SixBySixLevelCount;
            if (levelIndex < SevenBySevenLevelCount)
            {
                seeds = SevenBySevenSeeds;
                words = SevenLetterWords;
                variantIndex = levelIndex;
                return;
            }

            levelIndex -= SevenBySevenLevelCount;
            if (levelIndex < EightByEightLevelCount)
            {
                seeds = EightByEightSeeds;
                words = EightLetterWords;
                variantIndex = levelIndex;
                return;
            }

            seeds = NineByNineSeeds;
            words = NineLetterWords;
            variantIndex = levelIndex - EightByEightLevelCount;
        }

        private static string WordForLevel(LevelSeed seed, LevelSeed[] seeds, string[] words, int variantIndex)
        {
            if (variantIndex < seeds.Length)
            {
                return seed.Word;
            }

            return words[variantIndex % words.Length];
        }

        private static string TitleForLevel(LevelSeed seed, LevelSeed[] seeds, int levelIndex, int variantIndex)
        {
            if (variantIndex < seeds.Length)
            {
                return seed.Title;
            }

            string adjective = TitleAdjectives[levelIndex % TitleAdjectives.Length];
            string noun = TitleNouns[(levelIndex * 7) % TitleNouns.Length];
            return $"{adjective} {noun}";
        }

        private static int LockedCatCountForLevel(int levelIndex, int variantSeed)
        {
            if (levelIndex == 0)
            {
                return 1;
            }

            return LockedCatCountForVariantSeed(variantSeed);
        }

        private static int LockedCatCountForVariantSeed(int variantSeed)
        {
            if (variantSeed <= 4)
            {
                return 2;
            }

            return variantSeed <= 18 ? 1 : 0;
        }

        private static NekoLevel Create(string title, string word, int lockedCatCount, LevelSeed seed, int transform, int colorShift)
        {
            int size = seed.Size;
            int[] regions = new int[size * size];
            NekoCoord[] solution = new NekoCoord[size];

            for (int row = 0; row < size; row++)
            {
                int solutionColumn = seed.SolutionColumns[row];
                TransformCoord(row, solutionColumn, size, transform, out int transformedRow, out int transformedColumn);
                solution[transformedRow] = new NekoCoord(transformedRow, transformedColumn);

                for (int column = 0; column < size; column++)
                {
                    TransformCoord(row, column, size, transform, out int regionRow, out int regionColumn);
                    int region = (seed.RegionAt(row, column) + colorShift) % size;
                    regions[(regionRow * size) + regionColumn] = region;
                }
            }

            return new NekoLevel(title, word, regions, solution, LockedCats(solution, lockedCatCount));
        }

        private static NekoCoord[] LockedCats(NekoCoord[] solution, int lockedCatCount)
        {
            int count = Math.Max(0, Math.Min(lockedCatCount, solution.Length));
            NekoCoord[] lockedCats = new NekoCoord[count];
            for (int i = 0; i < count; i++)
            {
                lockedCats[i] = solution[i];
            }

            return lockedCats;
        }

        private static void TransformCoord(int row, int column, int size, int transform, out int transformedRow, out int transformedColumn)
        {
            switch (transform % TransformCount)
            {
                case 1:
                    transformedRow = row;
                    transformedColumn = size - 1 - column;
                    break;
                case 2:
                    transformedRow = size - 1 - row;
                    transformedColumn = column;
                    break;
                case 3:
                    transformedRow = size - 1 - row;
                    transformedColumn = size - 1 - column;
                    break;
                case 4:
                    transformedRow = column;
                    transformedColumn = row;
                    break;
                case 5:
                    transformedRow = size - 1 - column;
                    transformedColumn = size - 1 - row;
                    break;
                case 6:
                    transformedRow = column;
                    transformedColumn = size - 1 - row;
                    break;
                case 7:
                    transformedRow = size - 1 - column;
                    transformedColumn = row;
                    break;
                default:
                    transformedRow = row;
                    transformedColumn = column;
                    break;
            }
        }

        private readonly struct LevelSeed
        {
            public readonly string Title;
            public readonly string Word;
            public readonly int[] SolutionColumns;
            private readonly string[] regionRows;

            public int Size => Word.Length;

            public LevelSeed(string title, string word, int[] solutionColumns, params string[] regionRows)
            {
                Title = title;
                Word = word;
                SolutionColumns = solutionColumns;
                this.regionRows = regionRows;
            }

            public int RegionAt(int row, int column)
            {
                return regionRows[row][column] - '0';
            }
        }
    }
}
