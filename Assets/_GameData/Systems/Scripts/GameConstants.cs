namespace Meowdoku
{
    public static class GameConstants
    {
        public const string CurrentLevelNumberPlayerPrefsKey = "Nekodoku.CurrentLevelNumber";
        public const int FirstLevelNumber = 1;
        public const int AnalyticsTutorialLevelCount = 2;

        /// <summary>When enabled, each failure reveals one additional starting cat on retry,
        /// up to <see cref="MaximumDynamicDifficultyRevealedCats"/> total starting cats.</summary>
        public const bool DynamicDifficultyAdjustment = true;
        public const int MaximumDynamicDifficultyRevealedCats = 3;

        /// <summary>The 0-based index of the level currently loaded, kept in sync by
        /// GameManager.LoadLevel. Exists as a static so code with no GameManager reference (e.g. the ads
        /// plugin's GameAdsCheck.cs, which needs "what level is the player on" for interstitial/rate-us/
        /// notification gating) can read it - add <see cref="FirstLevelNumber"/> for the 1-based number,
        /// same convention GameManager itself uses everywhere else.</summary>
        public static int CurrentLevelIndex { get; set; }
    }
}
