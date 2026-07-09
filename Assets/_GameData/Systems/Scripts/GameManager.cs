using UnityEngine;
#if USE_AVNADS_PLUGIN
using AVN.AdsPlugin.Controllers;
using AVN.AdsPlugin.Services;
#endif

namespace Meowdoku
{
    /// <summary>
    /// The conductor: owns level progression and the puzzle-board instance, and
    /// coordinates the view/effect/input components (all sibling components on
    /// this GameObject) around loading, refreshing, and undoing a level. It never
    /// builds UI itself - everything it touches is scene-authored.
    ///
    /// All cross-component wiring happens in Start(), not Awake(), so it never
    /// depends on sibling component initialization order (Unity only guarantees
    /// every Awake() has run before any Start() runs). The sibling components
    /// have no Awake() logic of their own for the same reason.
    /// </summary>
    [RequireComponent(typeof(BoardInputHandler))]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField]
        private LevelDatabase levelDatabase;

        private readonly NekoUndoStack undoStack = new NekoUndoStack();

        [Header("Script References")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private GameplayScreen gameplayScreen;
        [SerializeField] private BoardInputHandler inputHandler;
        [SerializeField] private LevelCompleteScreen levelCompleteScreen;
        [SerializeField] private LevelFailedScreen levelFailedScreen;
        [SerializeField] private PowerupBar powerupBar;
        [SerializeField] private PowerupManager powerupManager;
        [SerializeField] private TutorialLessonController tutorialLessonController;

        private Level[] levels;
        private PuzzleBoard board;
        private int levelIndex;
        private bool isLessonBoardLoaded;
        private bool hintActive;
#if USE_AVNADS_PLUGIN
        private bool levelCompleteReported;
        private bool levelFailedReported;
#endif

        public PuzzleBoard Board => board;
        public bool IsLessonActive { get; private set; }

        /// <summary>True while a double-tap should be blocked - only during the lesson's rule-crossing
        /// steps, never during its cat-reveal steps (where a double-tap is exactly what's asked for).</summary>
        public bool IsLessonCommitBlocked => IsLessonActive && tutorialLessonController != null && !tutorialLessonController.AllowsCommit;

        /// <summary>Player-facing level number (1-based), matching the HUD's "Level {levelIndex + 1}"
        /// and SaveCurrentLevelNumber's own PlayerPrefs convention - the single source of truth for what
        /// "this level" means to analytics.</summary>
        private int AnalyticsLevelNumber => levelIndex + GameConstants.FirstLevelNumber;

        private void Start()
        {
            inputHandler.Initialize(this, boardView, gameplayScreen);
            levelCompleteScreen.NextRequested += NextLevel;
            levelFailedScreen.RetryRequested += RestartLevel;
            levelFailedScreen.ExtraLifeRequested += OnExtraLifeRequested;
            powerupBar.RevealCatRequested += OnRevealCatRequested;
            powerupBar.HintRequested += OnHintRequested;

            LevelLoader levelLoader = new LevelLoader(levelDatabase);
            levels = levelLoader.LoadLevels();

            if (tutorialLessonController != null && tutorialLessonController.ShouldPlay)
            {
                BeginTutorialLesson();
            }
            else
            {
                LoadLevel(GetSavedLevelIndex());
            }
        }

        private void BeginTutorialLesson()
        {
            inputHandler.CancelCatReveal();
            levelCompleteScreen.Hide();
            levelFailedScreen.Hide();
            gameplayScreen.SetHudVisible(false);
            powerupBar.SetVisible(false);

            isLessonBoardLoaded = true;
            IsLessonActive = true;

            board = tutorialLessonController.PrepareBoard();
            if (board == null)
            {
                IsLessonActive = false;
                isLessonBoardLoaded = false;
                LoadLevel(GetSavedLevelIndex());
                return;
            }

            undoStack.Clear();
            boardView.RebuildCells(board, inputHandler);
            boardView.RefreshVisuals(board, inputHandler.InputLocked);
            boardView.PlayIntro(inputHandler.InputLocked);

            // Only now, with the board's cells actually built, start the scripted phases - the hand
            // guide it kicks off queries BoardView for cell positions on its very first tick.
            tutorialLessonController.Begin(OnLessonTeachingFinished);
        }

        /// <summary>
        /// The three scripted rule cards are done, but the lesson board itself still has four more
        /// hidden cats to find. Drop back into normal per-action gameplay (HUD, win/fail all apply)
        /// on this same board instead of jumping straight to Level 1 - the player should get to
        /// actually finish what they just learned, not have it yanked away mid-solve.
        /// </summary>
        private void OnLessonTeachingFinished()
        {
            IsLessonActive = false;
            Refresh();
        }

        public void SaveUndo()
        {
            undoStack.Save(board);
        }

        public void DiscardLastUndo()
        {
            undoStack.DiscardMostRecent();
        }

        private void Undo()
        {
            if (!undoStack.TryPop(out BoardSnapshot snapshot))
            {
                return;
            }

            inputHandler.CancelCatReveal();
            boardView.StopAllJuice();
            NekoUndoStack.Apply(board, snapshot);
            Refresh();
        }

        /// <summary>The big per-action sync: HUD, hearts, board visuals, tutorial, win/fail screens.</summary>
        public void Refresh()
        {
            if (IsLessonActive)
            {
                boardView.RefreshVisuals(board, inputHandler.InputLocked);
                tutorialLessonController.OnBoardChanged();
                return;
            }

            // Any real board resync (a tap/drag/commit, undo, retry, level load...) clears a stale hint -
            // every board-mutating input path funnels through this method, so hooking it here covers all
            // of them in one place instead of instrumenting each input method separately.
            if (hintActive)
            {
                boardView.ClearHint();
                hintActive = false;
            }

            ValidationResult validation = board.Validate();
            gameplayScreen.Refresh(levelIndex, board.Level, validation);
            boardView.RefreshVisuals(board, inputHandler.InputLocked);

            // Mirrors gameplayScreen's HUD visibility: stays hidden for the lesson board's whole
            // lifetime (isLessonBoardLoaded), not just while IsLessonActive - otherwise it fades back
            // in once the scripted Teach* cards finish and the player is still finding the last,
            // unassisted cat on that same board.
            bool powerupBarActive = !isLessonBoardLoaded && !validation.IsFailed && !validation.IsSolved;
            powerupBar.Refresh(powerupManager.RevealCatUsesRemaining, powerupManager.HintUsesRemaining, powerupBarActive);

            bool shouldShowWin = validation.IsSolved && !inputHandler.InputLocked;
            if (shouldShowWin)
            {
#if USE_AVNADS_PLUGIN
                if (!levelCompleteReported)
                {
                    levelCompleteReported = true;
                    GameAnalyticsEvents.CustomLevelAnalysis(AnalyticsLevelNumber, LevelState.Completed, LevelMode.DEFAULT);
                }
#endif
                levelCompleteScreen.ShowWinAnimation(board.Level, validation);
            }
            else
            {
                levelCompleteScreen.Hide();
            }

            bool shouldShowFailed = validation.IsFailed && !inputHandler.InputLocked;
            if (shouldShowFailed)
            {
#if USE_AVNADS_PLUGIN
                if (!levelFailedReported)
                {
                    levelFailedReported = true;
                    GameAnalyticsEvents.CustomLevelAnalysis(AnalyticsLevelNumber, LevelState.Failed, LevelMode.DEFAULT);
                }
#endif
                levelFailedScreen.ShowFailAnimation(!board.HasClaimedExtraLife);
            }
            else
            {
#if USE_AVNADS_PLUGIN
                levelFailedReported = false;
#endif
                levelFailedScreen.Hide();
            }
        }

        public void ResetCrosses()
        {
            inputHandler.ResetCrosses();
        }

        private void LoadLevel(int nextLevelIndex)
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            inputHandler.CancelCatReveal();
            boardView.StopIntro(inputHandler.InputLocked);
            boardView.StopAllJuice();
            levelCompleteScreen.Hide();
            levelFailedScreen.Hide();
            gameplayScreen.SetHudVisible(true);
            powerupManager.ResetForLevel();

            levelIndex = Mathf.Clamp(nextLevelIndex, 0, levels.Length - 1);
            GameConstants.CurrentLevelIndex = levelIndex;
            SaveCurrentLevelNumber();
#if USE_AVNADS_PLUGIN
            levelCompleteReported = false;
            levelFailedReported = false;
            GameAnalyticsEvents.CustomLevelAnalysis(AnalyticsLevelNumber, LevelState.Started, LevelMode.DEFAULT);
#endif
            board = new PuzzleBoard(levels[levelIndex]);
            undoStack.Clear();
            boardView.RebuildCells(board, inputHandler);
            Refresh();
            boardView.PlayIntro(inputHandler.InputLocked);
            gameplayScreen.PlayIntro();
        }

        private void PreviousLevel()
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            LoadLevel(levelIndex <= 0 ? levels.Length - 1 : levelIndex - 1);
        }

        private void NextLevel()
        {
            if (isLessonBoardLoaded)
            {
                isLessonBoardLoaded = false;
                LoadLevel(GetSavedLevelIndex());
                return;
            }

            if (levels == null || levels.Length == 0)
            {
                return;
            }

            LoadLevel((levelIndex + 1) % levels.Length);
        }

        private void RestartLevel()
        {
            if (board == null)
            {
                return;
            }

            inputHandler.CancelCatReveal();
            boardView.StopAllJuice();
            levelCompleteScreen.Hide();
            levelFailedScreen.Hide();

            board.Clear();
            powerupManager.ResetForLevel();
#if USE_AVNADS_PLUGIN
            levelCompleteReported = false;
            levelFailedReported = false;
            GameAnalyticsEvents.CustomLevelAnalysis(AnalyticsLevelNumber, LevelState.Restarted, LevelMode.DEFAULT);
#endif
            undoStack.Clear();
            Refresh();
            boardView.PlayIntro(inputHandler.InputLocked);
            gameplayScreen.PlayIntro();
        }

        /// <summary>On Android, the extra life is gated behind an actual completed rewarded ad -
        /// <see cref="GrantExtraLife"/> only runs from AVNPlugin's onRewarded callback, never on a bare
        /// click. On every other platform (no ads plugin present at all), it grants immediately, same as
        /// this feature's original free-on-click behavior.</summary>
        private void OnExtraLifeRequested()
        {
            if (board == null || board.HasClaimedExtraLife)
            {
                return;
            }

#if USE_AVNADS_PLUGIN
            AVNPlugin.DTInstance?.ShowRewardedAd(GrantExtraLife);
#else
            GrantExtraLife();
#endif
        }

        /// <summary>Continues the same attempt instead of restarting it - unlike <see cref="RestartLevel"/>,
        /// the board's marks/reveals/mistakes are left untouched; only the one-per-attempt extra life
        /// (see <see cref="PuzzleBoard.AddLife"/>) is granted, and Refresh() re-evaluates fail/win state
        /// from the now-higher heart count, which hides the fail screen on its own.</summary>
        private void GrantExtraLife()
        {
            if (board == null || !board.AddLife())
            {
                return;
            }

#if USE_AVNADS_PLUGIN
            GameAnalyticsEvents.ExtraHeartUsed(AnalyticsLevelNumber);
#endif

            Refresh();
        }

        /// <summary>Auto-reveals one still-hidden cat, reusing the exact same flip/pop animation and
        /// InputLocked gating a manual double-tap gets - <see cref="BoardInputHandler.CommitCat"/> has
        /// no gesture-specific guards beyond that, so this just calls it directly.</summary>
        private void OnRevealCatRequested()
        {
            if (IsLessonActive || board == null || inputHandler.InputLocked)
            {
                return;
            }

            if (!board.TryFindHiddenCat(out Coord coord))
            {
                return;
            }

            if (!powerupManager.TryConsumeRevealCat())
            {
                return;
            }

            inputHandler.CommitCat(coord.Row, coord.Column);
#if USE_AVNADS_PLUGIN
            GameAnalyticsEvents.PowerupUsage(PowerupType.RevealCat, false, AnalyticsLevelNumber);
#endif
        }

        /// <summary>Computes and checks for a hint before consuming a free use, so requesting a hint
        /// when there's genuinely nothing to show (e.g. already solved) never burns the budget.</summary>
        private void OnHintRequested()
        {
            if (IsLessonActive || board == null || inputHandler.InputLocked)
            {
                return;
            }

            HintResult hint = board.FindHint();
            if (!hint.HasHint)
            {
                return;
            }

            if (!powerupManager.TryConsumeHint())
            {
                return;
            }

            if (hint.RevealCat.HasValue)
            {
                boardView.ShowHintCat(hint.RevealCat.Value);
            }
            else
            {
                boardView.ShowHintCross(hint.CrossCells);
            }

            hintActive = true;
#if USE_AVNADS_PLUGIN
            GameAnalyticsEvents.PowerupUsage(PowerupType.Hint, false, AnalyticsLevelNumber);
#endif
        }

        private static int GetSavedLevelIndex()
        {
            int savedLevelNumber = PlayerPrefs.GetInt(
                GameConstants.CurrentLevelNumberPlayerPrefsKey,
                GameConstants.FirstLevelNumber);

            return Mathf.Max(GameConstants.FirstLevelNumber, savedLevelNumber) - GameConstants.FirstLevelNumber;
        }

        private void SaveCurrentLevelNumber()
        {
            int currentLevelNumber = levelIndex + GameConstants.FirstLevelNumber;
            PlayerPrefs.SetInt(GameConstants.CurrentLevelNumberPlayerPrefsKey, currentLevelNumber);
            PlayerPrefs.Save();
        }
    }
}
