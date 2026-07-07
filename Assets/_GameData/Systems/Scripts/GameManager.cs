using UnityEngine;

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
        [SerializeField] private TutorialController tutorialController;
        [SerializeField] private BoardInputHandler inputHandler;
        [SerializeField] private LevelCompleteScreen winScreen;
        [SerializeField] private LevelFailedScreen levelFailedScreen;

        private Level[] levels;
        private PuzzleBoard board;
        private int levelIndex;

        public PuzzleBoard Board => board;

        private void Start()
        {
            tutorialController?.Initialize(boardView);
            inputHandler.Initialize(this, boardView, tutorialController, gameplayScreen);
            winScreen.NextRequested += NextLevel;
            levelFailedScreen.RetryRequested += RestartLevel;

            LevelLoader levelLoader = new LevelLoader(levelDatabase);
            levels = levelLoader.LoadLevels();
            LoadLevel(GetSavedLevelIndex());
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
            tutorialController?.StopGuide();
            NekoUndoStack.Apply(board, snapshot);
            Refresh();
            tutorialController?.UpdateGuide(levelIndex, board, true);
        }

        /// <summary>The big per-action sync: HUD, hearts, board visuals, tutorial, win/fail screens.</summary>
        public void Refresh()
        {
            ValidationResult validation = board.Validate();
            gameplayScreen.Refresh(levelIndex, board.Level, validation);
            boardView.RefreshVisuals(board, inputHandler.InputLocked);

            tutorialController?.UpdatePanel(levelIndex, board, false);
            if (validation.IsSolved)
            {
                tutorialController?.StopGuide();
            }
            else
            {
                tutorialController?.UpdateGuide(levelIndex, board, false);
            }

            bool shouldShowWin = validation.IsSolved && !inputHandler.InputLocked;
            if (shouldShowWin)
            {
                winScreen.ShowWinAnimation(board.Level, validation);
            }
            else
            {
                winScreen.Hide();
            }

            bool shouldShowFailed = validation.IsFailed && !inputHandler.InputLocked;
            if (shouldShowFailed)
            {
                levelFailedScreen.ShowFailAnimation();
            }
            else
            {
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
            tutorialController?.StopPulse();
            tutorialController?.StopGuide();
            winScreen.Hide();
            levelFailedScreen.Hide();

            levelIndex = Mathf.Clamp(nextLevelIndex, 0, levels.Length - 1);
            SaveCurrentLevelNumber();
            board = new PuzzleBoard(levels[levelIndex]);
            undoStack.Clear();
            boardView.RebuildCells(board, inputHandler);
            Refresh();
            tutorialController?.UpdatePanel(levelIndex, board, true);
            boardView.PlayIntro(inputHandler.InputLocked);
            tutorialController?.UpdateGuide(levelIndex, board, true);
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
            tutorialController?.StopPulse();
            tutorialController?.StopGuide();
            winScreen.Hide();
            levelFailedScreen.Hide();

            board.Clear();
            undoStack.Clear();
            Refresh();
            tutorialController?.UpdatePanel(levelIndex, board, true);
            boardView.PlayIntro(inputHandler.InputLocked);
            tutorialController?.UpdateGuide(levelIndex, board, true);
            gameplayScreen.PlayIntro();
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
