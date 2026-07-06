using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Meowdoku
{
    /// <summary>
    /// The front door for board input: tap/drag to cross, double-tap to commit.
    /// Owns "is input currently gated" (drag gesture state, and the lock that
    /// holds while the cat-found reveal sequence plays) since that's fundamentally
    /// an input concern, even though the reveal sequence itself is mostly visual.
    /// </summary>
    public sealed class BoardInputHandler : MonoBehaviour
    {
        private const float LightHapticCooldownSeconds = 0.045f;

        private static readonly Color ConflictColor = new Color(0.94f, 0.24f, 0.2f, 1f);

        private GameManager gameManager;
        private BoardView boardView;
        private TutorialController tutorialController;
        private LifeHearts lifeHearts;

        private bool inputLocked;
        private bool crossDragging;
        private bool crossDragPlacesCrosses;
        private bool crossDragHasUndoSnapshot;
        private int lastCrossDragRow = -1;
        private int lastCrossDragColumn = -1;
        private float lastLightHapticTime = -1f;
        private Coroutine catRevealRoutine;

        public bool InputLocked => inputLocked;

        public void Initialize(GameManager manager, BoardView board, TutorialController tutorial, LifeHearts hearts)
        {
            gameManager = manager;
            boardView = board;
            tutorialController = tutorial;
            lifeHearts = hearts;
        }

        public void ToggleCross(int row, int column)
        {
            if (inputLocked)
            {
                return;
            }

            PuzzleBoard board = gameManager.Board;
            bool placeCross = board.GetMark(row, column) != CellMark.Cross;
            if (!board.CanSetCross(row, column, placeCross))
            {
                return;
            }

            gameManager.SaveUndo();
            if (!board.SetCross(row, column, placeCross))
            {
                gameManager.DiscardLastUndo();
                return;
            }

            if (placeCross)
            {
                PlayLightCrossHaptic();
            }

            gameManager.Refresh();
            if (placeCross)
            {
                boardView.PlayCrossJelly(board, row, column);
            }
        }

        public void CommitCat(int row, int column)
        {
            if (inputLocked)
            {
                return;
            }

            PuzzleBoard board = gameManager.Board;
            int heartsBefore = board.HeartsRemaining;
            gameManager.SaveUndo();
            CommitResult result = board.CommitCat(row, column);
            if (result == CommitResult.NoChange)
            {
                gameManager.DiscardLastUndo();
                return;
            }

            bool foundCat = result == CommitResult.Correct;
            bool lostHeart = board.HeartsRemaining < heartsBefore;
            if (result == CommitResult.Correct)
            {
                //?Haptics.Play(HapticStrength.Medium);
            }
            else if (lostHeart)
            {
                //?Haptics.Play(HapticStrength.Strong);
            }

            gameManager.Refresh();
            if (foundCat)
            {
                PlayCatRevealForCat(row, column);
            }
            else if (lostHeart)
            {
                boardView.ShakeBoard();
                boardView.PunchCell(row, column, 1.08f);
                lifeHearts.PlayHeartLostEffect(board.HeartsRemaining);
            }
        }

        private void PlayCatRevealForCat(int row, int column)
        {
            CancelCatReveal();
            inputLocked = true;
            crossDragging = false;
            tutorialController.StopGuide();
            catRevealRoutine = StartCoroutine(RunCatRevealSequence(new Coord(row, column)));
        }

        private IEnumerator RunCatRevealSequence(Coord coord)
        {
            yield return boardView.PlayCatFoundPop(coord);
            inputLocked = false;
            catRevealRoutine = null;
            //?Haptics.Play(HapticStrength.Light);
            gameManager.Refresh();
        }

        /// <summary>Cancels any in-flight cat-reveal sequence and unlocks input. Call around level load/restart/undo.</summary>
        public void CancelCatReveal()
        {
            if (catRevealRoutine != null)
            {
                StopCoroutine(catRevealRoutine);
                catRevealRoutine = null;
            }

            inputLocked = false;
            crossDragging = false;
        }

        public void BeginCrossDrag(PointerEventData eventData)
        {
            if (inputLocked)
            {
                return;
            }

            crossDragging = true;
            crossDragHasUndoSnapshot = false;
            lastCrossDragRow = -1;
            lastCrossDragColumn = -1;
            crossDragPlacesCrosses = true;

            PuzzleBoard board = gameManager.Board;
            if (boardView.TryPointerToCell(board, eventData.position, eventData.pressEventCamera, out int row, out int column))
            {
                crossDragPlacesCrosses = board.GetMark(row, column) != CellMark.Cross;
                ApplyCrossDrag(row, column);
            }
        }

        public void CrossAtPointer(PointerEventData eventData)
        {
            if (inputLocked || !crossDragging)
            {
                return;
            }

            PuzzleBoard board = gameManager.Board;
            if (boardView.TryPointerToCell(board, eventData.position, eventData.pressEventCamera, out int row, out int column))
            {
                ApplyCrossDrag(row, column);
            }
        }

        public void EndCrossDrag()
        {
            crossDragging = false;
            crossDragHasUndoSnapshot = false;
            lastCrossDragRow = -1;
            lastCrossDragColumn = -1;
        }

        private void ApplyCrossDrag(int row, int column)
        {
            if (row == lastCrossDragRow && column == lastCrossDragColumn)
            {
                return;
            }

            lastCrossDragRow = row;
            lastCrossDragColumn = column;

            PuzzleBoard board = gameManager.Board;
            if (!board.CanSetCross(row, column, crossDragPlacesCrosses))
            {
                return;
            }

            if (!crossDragHasUndoSnapshot)
            {
                gameManager.SaveUndo();
                crossDragHasUndoSnapshot = true;
            }

            if (!board.SetCross(row, column, crossDragPlacesCrosses))
            {
                return;
            }

            if (crossDragPlacesCrosses)
            {
                PlayLightCrossHaptic();
            }

            gameManager.Refresh();
            if (crossDragPlacesCrosses)
            {
                boardView.PlayCrossJelly(board, row, column);
            }
        }

        private void PlayLightCrossHaptic()
        {
            if (Time.unscaledTime - lastLightHapticTime < LightHapticCooldownSeconds)
            {
                return;
            }

            lastLightHapticTime = Time.unscaledTime;
            //?Haptics.Play(HapticStrength.Light);
        }
    }
}
