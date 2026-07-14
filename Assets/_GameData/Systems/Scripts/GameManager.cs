using System.Collections;
using Sych.QuickActionsAssets.Runtime;
using DG.Tweening;
using UnityEngine;
using AVN.AdsPlugin;

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
        private const float LevelFailedPopupDelaySeconds = 0.2f;

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
        private int tutorialLessonIndex;
        private bool isLessonBoardLoaded;
        private bool hintActive;
        private Coord? pendingResultOrigin;
        private bool completeTransitionPlayed;
        private bool failedTransitionPlayed;
        private int currentLevelFailureCount;
        private Coroutine resultTransitionRoutine;
        private string pendingQuickActionId;
#if USE_AVNADS_PLUGIN
        private bool levelCompleteReported;
        private bool levelFailedReported;
#endif

        public PuzzleBoard Board => board;
        public bool IsLessonActive { get; private set; }

        /// <summary>During scripted teaching, only the guided cat can be committed. During the final
        /// find-the-cat step, any unrevealed tile can be tried; wrong guesses are rendered as red misses
        /// but are made penalty-free by <see cref="IsPenaltyFreeTutorialGuess"/>.</summary>
        public bool CanCommitCatAt(int row, int column)
        {
            if (IsLessonActive && tutorialLessonController != null)
            {
                return tutorialLessonController.AllowsCommitAt(row, column);
            }

            if (isLessonBoardLoaded && board != null)
            {
                if (tutorialLessonController != null && tutorialLessonController.IsFinalCatSearchActive)
                {
                    return board.IsActiveCell(row, column) && !board.IsRevealed(row, column);
                }

                return board.HasHiddenCat(row, column);
            }

            return true;
        }

        public bool IsPenaltyFreeTutorialGuess(int row, int column)
        {
            return isLessonBoardLoaded
                && !IsLessonActive
                && tutorialLessonController != null
                && tutorialLessonController.IsFinalCatSearchActive
                && board != null
                && !board.HasHiddenCat(row, column);
        }

        /// <summary>Both tutorial boards belong to analytics level 0. Normal gameplay uses the same
        /// 1-based numbering as the HUD, so the first playable level is analytics level 1.</summary>
        private int AnalyticsLevelNumber => isLessonBoardLoaded
            ? 0
            : levelIndex + GameConstants.FirstLevelNumber;

        private bool ShouldOfferTutorialCompletionChoice =>
            isLessonBoardLoaded
            && tutorialLessonController != null
            && tutorialLessonController.HasNextLesson;

        private void OnEnable()
        {
            LocalizationService.Changed += OnLocalizationChanged;
            QuickActions.Performed += OnQuickActionPerformed;
        }

        private void OnDisable()
        {
            LocalizationService.Changed -= OnLocalizationChanged;
            QuickActions.Performed -= OnQuickActionPerformed;
        }

        private void Start()
        {
#if UNITY_ANDROID
            Application.targetFrameRate = 60;
#endif
            AVNPlugin.DTInstance?.ResetDelayBeforeFirstAdStartTime();
            LocalizationService.InitializeDefaultIfNeeded();

            inputHandler.Initialize(this, boardView, gameplayScreen);
            levelCompleteScreen.NextRequested += NextLevel;
            levelCompleteScreen.PlayGameRequested += PlayGameAfterTutorial;
            levelFailedScreen.RetryRequested += OnRetryRequested;
            levelFailedScreen.ExtraLifeRequested += OnExtraLifeRequested;
            powerupBar.RevealCatRequested += OnRevealCatRequested;
            powerupBar.HintRequested += OnHintRequested;
            powerupBar.RewardedRevealCatRequested += OnRewardedRevealCatRequested;
            powerupBar.RewardedHintRequested += OnRewardedHintRequested;
            powerupManager.InitializeGlobalBudget();

            LevelLoader levelLoader = new LevelLoader(levelDatabase);
            levels = levelLoader.LoadLevels();

            SoundManager.PlayMusic(BGM.MainMenu);

            if (tutorialLessonController != null && tutorialLessonController.ShouldPlay)
            {
                BeginTutorialLesson();
            }
            else
            {
                LoadLevel(GetSavedLevelIndex());
                AVNPlugin.DTInstance?.LoadBanner(true, BannerAdTypes.BANNER);
            }

            ProcessLaunchQuickAction();
        }

        private void OnQuickActionPerformed(string id)
        {
            if (!QuickActions.IsGameAction(id))
            {
                return;
            }

            QuickActions.ResetLastPerformedQuickActionId();
            if (board == null)
            {
                pendingQuickActionId = id;
                return;
            }

            PerformQuickAction(id);
        }

        private void ProcessLaunchQuickAction()
        {
            if (!string.IsNullOrEmpty(pendingQuickActionId))
            {
                string actionId = pendingQuickActionId;
                pendingQuickActionId = null;
                PerformQuickAction(actionId);
                return;
            }

            if (QuickActions.TryConsumeLastPerformedGameAction(out string launchActionId))
            {
                PerformQuickAction(launchActionId);
            }
        }

        private void PerformQuickAction(string id)
        {
            switch (id)
            {
                case QuickActions.ContinueGameId:
                    // Opening or foregrounding the app already continues the saved level.
                    break;
                case QuickActions.RestartLevelId when !isLessonBoardLoaded:
                    CancelResultTransitionState();
                    RestartLevel();
                    break;
                case QuickActions.ShowHintId when !isLessonBoardLoaded:
                    ShowHint(false);
                    break;
            }
        }

        private void OnLocalizationChanged()
        {
            if (board == null || inputHandler == null)
            {
                return;
            }

            Refresh();
        }

        private void BeginTutorialLesson()
        {
            CancelResultTransitionState();
            inputHandler.CancelCatReveal();
            levelCompleteScreen.Hide();
            levelFailedScreen.Hide();
            gameplayScreen.SetHudVisible(false);
            powerupBar.SetVisible(false);

            isLessonBoardLoaded = true;
            IsLessonActive = true;
            currentLevelFailureCount = 0;

            board = tutorialLessonController.PrepareBoard(tutorialLessonIndex);
            if (board == null)
            {
                IsLessonActive = false;
                isLessonBoardLoaded = false;
                LoadLevel(GetSavedLevelIndex());
                return;
            }

#if USE_AVNADS_PLUGIN
            levelCompleteReported = false;
            levelFailedReported = false;
            GameAnalyticsEvents.LevelAnalysis(AnalyticsLevelNumber, LevelState.Started, LevelMode.DEFAULT);
#endif

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
            if (!tutorialLessonController.HasNextLesson)
            {
                AVNPlugin.DTInstance?.LoadBanner(true, BannerAdTypes.BANNER);
            }

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
            CancelResultTransitionState();
            boardView.StopAllJuice();
            NekoUndoStack.Apply(board, snapshot);
            Refresh();
        }

        public void NotifyCatRevealCompleted(Coord coord)
        {
            pendingResultOrigin = coord;
            if (isLessonBoardLoaded && tutorialLessonController != null)
            {
                tutorialLessonController.OnCatRevealCompleted(coord);
            }
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

            if (validation.IsSolved)
            {
                if (!completeTransitionPlayed && resultTransitionRoutine == null)
                {
                    resultTransitionRoutine = StartCoroutine(RunLevelCompleteTransition(validation, pendingResultOrigin));
                }
                else if (completeTransitionPlayed)
                {
                    levelCompleteScreen.ShowWinAnimation(
                        board.Level,
                        validation,
                        ShouldOfferTutorialCompletionChoice);
                }
            }
            else
            {
                completeTransitionPlayed = false;
                pendingResultOrigin = null;
                if (resultTransitionRoutine == null)
                {
                    levelCompleteScreen.Hide();
                }
            }

            if (validation.IsFailed)
            {
                if (!failedTransitionPlayed && resultTransitionRoutine == null)
                {
                    resultTransitionRoutine = StartCoroutine(RunLevelFailedTransition(!board.HasClaimedExtraLife));
                }
                else if (failedTransitionPlayed)
                {
                    levelFailedScreen.ShowFailAnimation(!board.HasClaimedExtraLife, GetCatsRemaining());
                }
            }
            else
            {
                failedTransitionPlayed = false;
                if (resultTransitionRoutine == null)
                {
                    levelFailedScreen.Hide();
                }
#if USE_AVNADS_PLUGIN
                levelFailedReported = false;
#endif
            }
        }

        private IEnumerator RunLevelCompleteTransition(ValidationResult validation, Coord? origin)
        {
            inputHandler.SetResultLocked(true);
            ReportLevelCompleteIfNeeded();

            Tween boardTween = boardView.PlayLevelCompleteTransition(board, origin);
            levelCompleteScreen.ShowWinAnimation(
                board.Level,
                validation,
                ShouldOfferTutorialCompletionChoice);
            if (boardTween != null)
            {
                yield return boardTween.WaitForCompletion(true);
            }

            completeTransitionPlayed = true;
            resultTransitionRoutine = null;
            boardView.RefreshVisuals(board, inputHandler.InputLocked);
        }

        private IEnumerator RunLevelFailedTransition(bool extraLifeAvailable)
        {
            inputHandler.SetResultLocked(true);
            ReportLevelFailedIfNeeded();

            if (GameConstants.DynamicDifficultyAdjustment)
            {
                currentLevelFailureCount++;
            }

            Tween boardTween = boardView.PlayLevelFailedTransition(board);
            yield return new WaitForSecondsRealtime(LevelFailedPopupDelaySeconds);
            levelFailedScreen.ShowFailAnimation(extraLifeAvailable, GetCatsRemaining());
            if (boardTween != null)
            {
                yield return boardTween.WaitForCompletion(true);
            }

            failedTransitionPlayed = true;
            resultTransitionRoutine = null;
            boardView.RefreshVisuals(board, inputHandler.InputLocked);
        }

        private void ReportLevelCompleteIfNeeded()
        {
#if USE_AVNADS_PLUGIN
            if (levelCompleteReported)
            {
                return;
            }

            levelCompleteReported = true;
            GameAnalyticsEvents.CustomLevelAnalysis(AnalyticsLevelNumber, LevelState.Completed);
#endif
        }

        private void ReportLevelFailedIfNeeded()
        {
#if USE_AVNADS_PLUGIN
            if (levelFailedReported)
            {
                return;
            }

            levelFailedReported = true;
            GameAnalyticsEvents.CustomLevelAnalysis(AnalyticsLevelNumber, LevelState.Failed);
#endif
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

            CancelResultTransitionState();
            inputHandler.CancelCatReveal();
            boardView.StopIntro(inputHandler.InputLocked);
            boardView.StopAllJuice();
            levelCompleteScreen.Hide();
            levelFailedScreen.Hide();
            gameplayScreen.SetHudVisible(true);

            levelIndex = Mathf.Clamp(nextLevelIndex, 0, levels.Length - 1);
            currentLevelFailureCount = 0;
            GameConstants.CurrentLevelIndex = levelIndex;
            SaveCurrentLevelNumber();
#if USE_AVNADS_PLUGIN
            levelCompleteReported = false;
            levelFailedReported = false;
            GameAnalyticsEvents.LevelAnalysis(AnalyticsLevelNumber, LevelState.Started, LevelMode.DEFAULT);

#endif
            board = new PuzzleBoard(levels[levelIndex]);
            undoStack.Clear();
            boardView.RebuildCells(board, inputHandler);
            Refresh();
            boardView.PlayIntro(inputHandler.InputLocked);
            gameplayScreen.PlayIntro();
        }

        private void CancelResultTransitionState()
        {
            if (resultTransitionRoutine != null)
            {
                StopCoroutine(resultTransitionRoutine);
                resultTransitionRoutine = null;
            }

            completeTransitionPlayed = false;
            failedTransitionPlayed = false;
            pendingResultOrigin = null;

            if (inputHandler != null)
            {
                inputHandler.SetResultLocked(false);
            }
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
                if (tutorialLessonController != null && tutorialLessonController.HasNextLesson)
                {
                    tutorialLessonIndex++;
                    BeginTutorialLesson();
                    return;
                }

                isLessonBoardLoaded = false;
                LoadLevel(GetSavedLevelIndex());
                return;
            }

            if (levels == null || levels.Length == 0)
            {
                return;
            }

            LoadLevel((levelIndex + 1) % levels.Length);
            AVNPlugin.DTInstance?.ShowInterAd();
            AVNPlugin.DTInstance?.NativeRateUsCall();
            AVNPlugin.DTInstance?.NotificationCall();
        }

        private void PlayGameAfterTutorial()
        {
            if (!isLessonBoardLoaded)
            {
                return;
            }

            tutorialLessonController?.MarkLessonsSeen();
            isLessonBoardLoaded = false;
            IsLessonActive = false;
            LoadLevel(GetSavedLevelIndex());
            AVNPlugin.DTInstance?.LoadBanner(true, BannerAdTypes.BANNER);
        }

        private void OnRetryRequested()
        {
            if (board == null || resultTransitionRoutine != null)
            {
                return;
            }

            resultTransitionRoutine = StartCoroutine(RunRetryTransition());
        }

        private IEnumerator RunRetryTransition()
        {
            inputHandler.CancelCatReveal();
            inputHandler.SetResultLocked(true);
            levelFailedScreen.SetButtonsInteractable(false);

            Tween hideTween = levelFailedScreen.HideForTransition();
            Tween clearTween = boardView.PlayRetryClearTransition(board);

            if (hideTween != null)
            {
                yield return hideTween.WaitForCompletion(true);
            }

            if (clearTween != null)
            {
                yield return clearTween.WaitForCompletion(true);
            }

            resultTransitionRoutine = null;
            RestartLevel(false);
        }

        private void RestartLevel(bool snapBoardJuice = true)
        {
            if (board == null)
            {
                return;
            }

            inputHandler.CancelCatReveal();
            inputHandler.SetResultLocked(false);
            completeTransitionPlayed = false;
            failedTransitionPlayed = false;
            pendingResultOrigin = null;

            if (snapBoardJuice)
            {
                boardView.StopAllJuice();
            }
            else
            {
                boardView.ResetResultPose();
            }

            levelCompleteScreen.Hide();
            levelFailedScreen.Hide();

            board.Clear(GetRetryStartingCatCount());
#if USE_AVNADS_PLUGIN
            levelCompleteReported = false;
            levelFailedReported = false;
            GameAnalyticsEvents.CustomLevelAnalysis(AnalyticsLevelNumber, LevelState.Restarted);
#endif
            undoStack.Clear();
            Refresh();
            boardView.PlayIntro(inputHandler.InputLocked);
            gameplayScreen.PlayIntro();
        }

        private int GetCatsRemaining()
        {
            return board == null ? 0 : Mathf.Max(0, board.Size - board.RevealedCatCount());
        }

        private int GetRetryStartingCatCount()
        {
            if (!GameConstants.DynamicDifficultyAdjustment || board == null)
            {
                return 0;
            }

            int authoredStartingCats = board.Level.LockedCats.Length;
            int adjustedStartingCats = authoredStartingCats + currentLevelFailureCount;
            return Mathf.Min(
                board.Size,
                Mathf.Min(GameConstants.MaximumDynamicDifficultyRevealedCats, adjustedStartingCats));
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

            if (resultTransitionRoutine != null)
            {
                StopCoroutine(resultTransitionRoutine);
                resultTransitionRoutine = null;
            }

#if USE_AVNADS_PLUGIN
            GameAnalyticsEvents.ExtraHeartUsed(AnalyticsLevelNumber);
#endif

            resultTransitionRoutine = StartCoroutine(RunExtraLifeReviveTransition(board.HeartsRemaining));
        }

        private IEnumerator RunExtraLifeReviveTransition(int heartsRemaining)
        {
            inputHandler.SetResultLocked(true);
            failedTransitionPlayed = false;
            levelFailedScreen.SetButtonsInteractable(false);

            Tween hideTween = levelFailedScreen.HideForTransition();
            if (hideTween != null)
            {
                yield return hideTween.WaitForCompletion(true);
            }

            ValidationResult validation = board.Validate();
            gameplayScreen.Refresh(levelIndex, board.Level, validation);
            boardView.RefreshVisuals(board, inputHandler.InputLocked);
            gameplayScreen.PlayHeartGained(heartsRemaining);

            Tween reviveTween = boardView.PlayExtraLifeReviveTransition(board);
            if (reviveTween != null)
            {
                yield return reviveTween.WaitForCompletion(true);
            }

            inputHandler.SetResultLocked(false);
            resultTransitionRoutine = null;
            Refresh();
        }

        /// <summary>Auto-reveals one still-hidden cat, reusing the exact same flip/pop animation and
        /// InputLocked gating a manual double-tap gets - <see cref="BoardInputHandler.CommitCat"/> has
        /// no gesture-specific guards beyond that, so this just calls it directly.</summary>
        private void OnRevealCatRequested()
        {
            RevealHiddenCat(false);
        }

        private void OnRewardedRevealCatRequested()
        {
            RevealHiddenCat(true);
        }

        private void RevealHiddenCat(bool rewarded)
        {
            if (IsLessonActive || board == null || inputHandler.InputLocked)
            {
                return;
            }

            if (!board.TryFindHiddenCat(out Coord coord))
            {
                return;
            }

            if (!rewarded && !powerupManager.TryConsumeRevealCat())
            {
                return;
            }

            inputHandler.CommitCat(coord.Row, coord.Column);
#if USE_AVNADS_PLUGIN
            GameAnalyticsEvents.PowerupUsage(PowerupType.RevealCat, rewarded, AnalyticsLevelNumber);
#endif
        }

        /// <summary>Computes and checks for a hint before consuming a free use, so requesting a hint
        /// when there's genuinely nothing to show (e.g. already solved) never burns the budget.</summary>
        private void OnHintRequested()
        {
            ShowHint(false);
        }

        private void OnRewardedHintRequested()
        {
            ShowHint(true);
        }

        private void ShowHint(bool rewarded)
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

            if (!rewarded && !powerupManager.TryConsumeHint())
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
            GameAnalyticsEvents.PowerupUsage(PowerupType.Hint, rewarded, AnalyticsLevelNumber);
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
