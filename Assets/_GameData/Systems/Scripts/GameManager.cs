using System.Collections;
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
    [RequireComponent(typeof(BoardView))]
    [RequireComponent(typeof(GameplayScreen))]
    [RequireComponent(typeof(TutorialController))]
    [RequireComponent(typeof(BoardInputHandler))]
    [RequireComponent(typeof(LifeHearts))]
    public sealed class GameManager : MonoBehaviour
    {
        private const float LevelCompleteAdvanceSeconds = 1.2f;

        [SerializeField]
        private LevelDatabase levelDatabase;

        private readonly NekoUndoStack undoStack = new NekoUndoStack();

        [Header("Script References")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private GameplayScreen hudView;
        [SerializeField] private TutorialController tutorialController;
        [SerializeField] private BoardInputHandler inputHandler;
        [SerializeField] private LifeHearts lifeHearts;
        [SerializeField] private WinScreen winScreen;

        private Level[] levels;
        private PuzzleBoard board;
        private int levelIndex;
        private Coroutine levelAdvanceRoutine;

        public PuzzleBoard Board => board;

        private void Start()
        {
            tutorialController.Initialize(boardView);
            inputHandler.Initialize(this, boardView, tutorialController, lifeHearts);

            boardView.CacheRestPosition();

            LevelLoader levelLoader = new LevelLoader(levelDatabase);
            levels = levelLoader.LoadLevels();
            LoadLevel(0);
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

            StopLevelAdvance();
            inputHandler.CancelCatReveal();
            boardView.StopAllJuice(board); tutorialController.StopGuide();
            NekoUndoStack.Apply(board, snapshot);
            Refresh();
            tutorialController.UpdateGuide(levelIndex, board, true);
        }

        /// <summary>The big per-action sync: HUD, hearts, board visuals, tutorial, win screen, level-advance check.</summary>
        public void Refresh()
        {
            ValidationResult validation = board.Validate();
            hudView.Refresh(levelIndex, board.Level, validation);
            lifeHearts.Refresh(validation.HeartsRemaining, validation.IsFailed);
            boardView.RefreshVisuals(board, validation, inputHandler.InputLocked);

            tutorialController.UpdatePanel(levelIndex, board, false);
            if (validation.IsSolved)
            {
                tutorialController.StopGuide();
            }
            else
            {
                tutorialController.UpdateGuide(levelIndex, board, false);
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

            UpdateSolvedLevelAdvance(validation.IsSolved);
        }

        private void LoadLevel(int nextLevelIndex)
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            inputHandler.CancelCatReveal();
            boardView.StopIntro(board, inputHandler.InputLocked);
            StopLevelAdvance();
            boardView.StopAllJuice(board);
            tutorialController.StopPulse();
            tutorialController.StopGuide();
            winScreen.Hide();

            levelIndex = Mathf.Clamp(nextLevelIndex, 0, levels.Length - 1);
            board = new PuzzleBoard(levels[levelIndex]);
            undoStack.Clear();
            boardView.RebuildCells(board, inputHandler);
            Refresh();
            tutorialController.UpdatePanel(levelIndex, board, true);
            boardView.PlayIntro(board, inputHandler.InputLocked);
            tutorialController.UpdateGuide(levelIndex, board, true);
            lifeHearts.PlayIntro();
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
            StopLevelAdvance();
            boardView.StopAllJuice(board);
            tutorialController.StopPulse();
            tutorialController.StopGuide();
            winScreen.Hide();

            board.Clear();
            undoStack.Clear();
            Refresh();
            tutorialController.UpdatePanel(levelIndex, board, true);
            boardView.PlayIntro(board, inputHandler.InputLocked);
            tutorialController.UpdateGuide(levelIndex, board, true);
            lifeHearts.PlayIntro();
        }

        private void StopLevelAdvance()
        {
            if (levelAdvanceRoutine == null)
            {
                return;
            }

            StopCoroutine(levelAdvanceRoutine);
            levelAdvanceRoutine = null;
        }

        private void UpdateSolvedLevelAdvance(bool isSolved)
        {
            if (!isSolved || inputHandler.InputLocked)
            {
                StopLevelAdvance();
                return;
            }

            if (levelAdvanceRoutine == null && isActiveAndEnabled)
            {
                levelAdvanceRoutine = StartCoroutine(AdvanceToNextLevelAfterDelay());
            }
        }

        private IEnumerator AdvanceToNextLevelAfterDelay()
        {
            yield return new WaitForSecondsRealtime(LevelCompleteAdvanceSeconds);
            levelAdvanceRoutine = null;
            NextLevel();
        }
    }
}
