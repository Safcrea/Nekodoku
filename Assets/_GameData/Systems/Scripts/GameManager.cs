using System.Collections;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// The conductor: owns level progression and the puzzle-board instance, and
    /// coordinates the view/effect/input components (all sibling components on
    /// this GameObject) around loading, refreshing, and undoing a level. It never
    /// builds UI itself - everything it touches is scene-authored and wired
    /// through Meowdoku &gt; Scene &gt; Build Game UI.
    ///
    /// All cross-component wiring happens in Start(), not Awake(), so it never
    /// depends on sibling component initialization order (Unity only guarantees
    /// every Awake() has run before any Start() runs). The sibling components
    /// have no Awake() logic of their own for the same reason.
    /// </summary>
    [RequireComponent(typeof(BoardView))]
    [RequireComponent(typeof(WordSlotsView))]
    [RequireComponent(typeof(HudView))]
    [RequireComponent(typeof(TutorialController))]
    [RequireComponent(typeof(EffectsPlayer))]
    [RequireComponent(typeof(BoardInputHandler))]
    public sealed class GameManager : MonoBehaviour
    {
        private const float LevelCompleteAdvanceSeconds = 1.2f;

        [SerializeField]
        private LevelDatabase levelDatabase;

        [SerializeField]
        private Texture2D tutorialHandTexture;

        private readonly NekoUndoStack undoStack = new NekoUndoStack();

        private BoardView boardView;
        private WordSlotsView wordSlotsView;
        private HudView hudView;
        private TutorialController tutorialController;
        private EffectsPlayer effectsPlayer;
        private BoardInputHandler inputHandler;

        private Level[] levels;
        private PuzzleBoard board;
        private int levelIndex;
        private Coroutine levelAdvanceRoutine;

        public PuzzleBoard Board => board;

        private void Start()
        {
            boardView = GetComponent<BoardView>();
            wordSlotsView = GetComponent<WordSlotsView>();
            hudView = GetComponent<HudView>();
            tutorialController = GetComponent<TutorialController>();
            effectsPlayer = GetComponent<EffectsPlayer>();
            inputHandler = GetComponent<BoardInputHandler>();

            Screen.orientation = ScreenOrientation.Portrait;

            if (!ValidateSceneReferences())
            {
                return;
            }

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            Sprite catSprite = SpriteFactory.CreateNekoCatSprite();
            Sprite handSprite = SpriteFactory.LoadTutorialHandSprite(tutorialHandTexture);
            Sprite focusRingSprite = SpriteFactory.CreateFocusRingSprite();
            Sprite juiceDotSprite = SpriteFactory.CreateJuiceDotSprite();

            boardView.SetCatSprite(catSprite);
            tutorialController.SetSprites(focusRingSprite, handSprite);
            tutorialController.Initialize(boardView, wordSlotsView);
            wordSlotsView.SetFont(defaultFont);
            effectsPlayer.Initialize(boardView, wordSlotsView, defaultFont, juiceDotSprite);
            inputHandler.Initialize(this, boardView, effectsPlayer, tutorialController);

            UiFactory.EnsureEventSystem();
            boardView.CacheRestPosition();
            hudView.BindButtons(PreviousLevel, Undo, RestartLevel, NextLevel);

            LevelLoader levelLoader = new LevelLoader(levelDatabase);
            levels = levelLoader.LoadLevels();
            LoadLevel(0);
        }

        private bool ValidateSceneReferences()
        {
            bool valid = true;
            valid &= SceneValidation.LogIfMissing(levelDatabase, "Level Database", this);
            valid &= SceneValidation.LogIfMissing(tutorialHandTexture, "Tutorial Hand Texture", this);
            valid &= boardView.Validate();
            valid &= wordSlotsView.Validate();
            valid &= hudView.Validate();
            valid &= tutorialController.Validate();
            valid &= effectsPlayer.Validate();
            return valid;
        }

        public void SaveUndo()
        {
            undoStack.Save(board, wordSlotsView.VisibleLetters);
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
            boardView.StopAllJuice(board);
            effectsPlayer.StopAll();
            tutorialController.StopGuide();
            NekoUndoStack.Apply(board, snapshot);
            wordSlotsView.RestoreVisibleLetters(snapshot.VisibleLetters);
            Refresh();
            tutorialController.UpdateGuide(levelIndex, board, true);
        }

        /// <summary>The big per-action sync: HUD, word slots, board visuals, tutorial, win panel, level-advance check.</summary>
        public void Refresh()
        {
            ValidationResult validation = board.Validate();
            hudView.Refresh(levelIndex, board.Level, validation);
            wordSlotsView.Refresh(board);
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
            hudView.SetWinPanelVisible(shouldShowWin, board.Level, validation);
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
            effectsPlayer.StopAll();
            tutorialController.StopPulse();
            tutorialController.StopGuide();
            hudView.HideWinPanel();

            levelIndex = Mathf.Clamp(nextLevelIndex, 0, levels.Length - 1);
            board = new PuzzleBoard(levels[levelIndex]);
            wordSlotsView.BuildForLevel(board);
            undoStack.Clear();
            wordSlotsView.ResetToLockedCats(board);
            boardView.RebuildCells(board, inputHandler);
            Refresh();
            tutorialController.UpdatePanel(levelIndex, board, true);
            boardView.PlayIntro(board, inputHandler.InputLocked);
            tutorialController.UpdateGuide(levelIndex, board, true);
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
            effectsPlayer.StopAll();
            tutorialController.StopPulse();
            tutorialController.StopGuide();
            hudView.HideWinPanel();

            board.Clear();
            undoStack.Clear();
            wordSlotsView.ResetToLockedCats(board);
            wordSlotsView.BuildForLevel(board);
            Refresh();
            tutorialController.UpdatePanel(levelIndex, board, true);
            boardView.PlayIntro(board, inputHandler.InputLocked);
            tutorialController.UpdateGuide(levelIndex, board, true);
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
