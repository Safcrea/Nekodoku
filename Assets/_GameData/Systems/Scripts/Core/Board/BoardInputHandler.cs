using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using InputSystemMouse = UnityEngine.InputSystem.Mouse;
using InputSystemTouchControl = UnityEngine.InputSystem.Controls.TouchControl;
using InputSystemTouchPhase = UnityEngine.InputSystem.TouchPhase;
using InputSystemTouchscreen = UnityEngine.InputSystem.Touchscreen;

namespace Meowdoku
{
    /// <summary>
    /// The front door for board input: tap/drag to cross, double-tap to commit. Only
    /// decides what an input means and whether it's allowed - the resulting animation
    /// is always played by the specific GridCell involved, never implemented here. Owns
    /// "is input currently gated" (drag gesture state, and the lock that holds while the
    /// cat-found reveal sequence plays) since that's fundamentally an input concern, even
    /// though the reveal sequence's visuals are GridCell's.
    /// </summary>
    public sealed class BoardInputHandler : MonoBehaviour
    {
        private const int MousePointerId = -1;
        private const float LightHapticCooldownSeconds = 0.045f;
        private const float ResetCrossMaxRandomDelaySeconds = 0.5f;

        private GameManager gameManager;
        private BoardView boardView;
        private GameplayScreen gameplayScreen;

        private bool inputLocked;
        private bool resultLocked;
        private bool crossDragging;
        private bool crossDragPlacesCrosses;
        private bool crossDragHasUndoSnapshot;
        private int lastCrossDragRow = -1;
        private int lastCrossDragColumn = -1;
        private float lastLightHapticTime = -1f;
        private Coroutine catRevealRoutine;
        private bool offTilePointerTracking;
        private bool offTilePointerStartedDrag;
        private int offTilePointerId = MousePointerId;
        private readonly List<RaycastResult> pointerRaycastResults = new List<RaycastResult>();
        private PointerEventData sharedPointerEventData;

        public bool InputLocked => inputLocked || resultLocked;

        public bool CanCommitCat(int row, int column)
        {
            return !InputLocked && gameManager != null && gameManager.CanCommitCatAt(row, column);
        }

        public void Initialize(GameManager manager, BoardView board, GameplayScreen screen)
        {
            gameManager = manager;
            boardView = board;
            gameplayScreen = screen;
        }

        private void Update()
        {
            UpdateOffTileCrossDrag();
        }

        public void ToggleCross(int row, int column)
        {
            if (InputLocked)
            {
                return;
            }

            // A cell an earlier sub-guide already required and crossed stays raycast-enabled during
            // the tutorial (see BoardView.RefreshVisuals) purely so a drag can *start* on it - it isn't
            // meant to accept a lone tap toggling it back off.
            if (!boardView.IsTutorialCrossAllowed(row, column))
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

            PlayLightCrossFeedback(placeCross);

            gameManager.Refresh();
            GridCell crossedCell = boardView.GetCellView(row, column);
            if (crossedCell != null)
            {
                crossedCell.PlayCrossJelly();
            }
        }

        public void CommitCat(int row, int column)
        {
            if (!CanCommitCat(row, column))
            {
                return;
            }

            PuzzleBoard board = gameManager.Board;
            int heartsBefore = board.HeartsRemaining;
            gameManager.SaveUndo();
            bool penalizeWrongGuess = !gameManager.IsPenaltyFreeTutorialGuess(row, column);
            CommitResult result = board.CommitCat(row, column, penalizeWrongGuess);
            if (result == CommitResult.NoChange)
            {
                gameManager.DiscardLastUndo();
                return;
            }

            bool foundCat = result == CommitResult.Correct;
            bool lostHeart = board.HeartsRemaining < heartsBefore;

            gameManager.Refresh();
            if (foundCat)
            {
                PlayCatRevealForCat(row, column);
            }
            else if (lostHeart)
            {
                GameHaptics.Failure();
                boardView.ShakeBoard();
                GridCell missedCell = boardView.GetCellView(row, column);
                if (missedCell != null)
                {
                    missedCell.PlayWrongPunch();
                }
                boardView.PlayHeartLostReactionOnRevealedCats(board);
                gameplayScreen.PlayHeartLost(board.HeartsRemaining);
                SoundManager.PlaySound(SFX.HeartLost);
            }
            else if (result == CommitResult.Wrong)
            {
                // The tutorial's final search still shows the normal red miss, but without the
                // shake, heart animation, sound, or failure progress of a penalized guess.
                GridCell missedCell = boardView.GetCellView(row, column);
                missedCell?.PlayWrongPunch();
            }
        }

        public void ResetCrosses()
        {
            if (InputLocked || gameManager?.Board == null)
            {
                return;
            }

            gameManager.SaveUndo();
            boardView.PlayCrossResetAnimations(gameManager.Board, ResetCrossMaxRandomDelaySeconds);
            if (!gameManager.Board.ClearPlayerCrosses())
            {
                gameManager.DiscardLastUndo();
                return;
            }

            crossDragging = false;
            crossDragHasUndoSnapshot = false;
            lastCrossDragRow = -1;
            lastCrossDragColumn = -1;
            StopOffTilePointerTracking();
            gameManager.Refresh();
        }

        private void PlayCatRevealForCat(int row, int column)
        {
            CancelCatReveal();
            inputLocked = true;
            crossDragging = false;
            GameHaptics.Success();
            SoundManager.PlaySound(SFX.CatFound);
            catRevealRoutine = StartCoroutine(RunCatRevealSequence(row, column));
        }

        private IEnumerator RunCatRevealSequence(int row, int column)
        {
            GridCell cell = boardView.GetCellView(row, column);
            if (cell != null)
            {
                gameplayScreen.PlayCatCollected();
                yield return cell.PlayCatFoundPop().WaitForCompletion(true);
            }

            inputLocked = false;
            catRevealRoutine = null;
            gameManager.NotifyCatRevealCompleted(new Coord(row, column));
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
            StopOffTilePointerTracking();
        }

        public void SetResultLocked(bool locked)
        {
            resultLocked = locked;
            if (!locked)
            {
                return;
            }

            crossDragging = false;
            crossDragHasUndoSnapshot = false;
            lastCrossDragRow = -1;
            lastCrossDragColumn = -1;
            StopOffTilePointerTracking();
        }

        public void BeginCrossDrag(PointerEventData eventData)
        {
            if (InputLocked)
            {
                return;
            }

            BeginCrossDragAtPointer(eventData.position, eventData.pressEventCamera);
        }

        private void BeginCrossDragAtPointer(Vector2 screenPosition, Camera eventCamera)
        {
            crossDragging = true;
            crossDragHasUndoSnapshot = false;
            lastCrossDragRow = -1;
            lastCrossDragColumn = -1;
            // Always places crosses, regardless of the starting cell's own mark - a drag that
            // happens to start on an already-crossed cell (e.g. re-dragging across a row that's
            // only partially marked) must still cross the remaining blank cells it passes over,
            // not flip into "erase" mode for the whole gesture.
            crossDragPlacesCrosses = true;

            if (boardView.TryPointerToCell(screenPosition, eventCamera, out int row, out int column))
            {
                ApplyCrossDrag(row, column);
            }
        }

        public void CrossAtPointer(PointerEventData eventData)
        {
            if (InputLocked || !crossDragging)
            {
                return;
            }

            if (boardView.TryPointerToCell(eventData.position, eventData.pressEventCamera, out int row, out int column))
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
            StopOffTilePointerTracking();
        }

        private void ApplyCrossDrag(int row, int column)
        {
            if (row == lastCrossDragRow && column == lastCrossDragColumn)
            {
                return;
            }

            lastCrossDragRow = row;
            lastCrossDragColumn = column;

            if (!boardView.IsTutorialCrossAllowed(row, column))
            {
                return;
            }

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

            PlayLightCrossFeedback(crossDragPlacesCrosses);

            gameManager.Refresh();
            GridCell crossedCell = boardView.GetCellView(row, column);
            if (crossedCell != null)
            {
                crossedCell.PlayCrossJelly();
            }
        }

        /// <summary>Throttled haptic + SFX for placing/removing a cross - shared cooldown so a fast drag across many cells doesn't machine-gun the sound.</summary>
        private void PlayLightCrossFeedback(bool placing)
        {
            if (Time.unscaledTime - lastLightHapticTime < LightHapticCooldownSeconds)
            {
                return;
            }

            lastLightHapticTime = Time.unscaledTime;
            GameHaptics.LightImpact();
            SoundManager.PlaySound(placing ? SFX.CrossMarked : SFX.CrossUnmarked);
        }

        private void UpdateOffTileCrossDrag()
        {
            if (InputLocked || gameManager?.Board == null || boardView == null)
            {
                StopOffTilePointerTracking();
                return;
            }

            UpdateOffTileCrossDragInputSystem();
        }

        private void UpdateOffTileCrossDragInputSystem()
        {
            if (UpdateOffTileTouchDragInputSystem())
            {
                return;
            }

            UpdateOffTileMouseDragInputSystem();
        }

        private void UpdateOffTileMouseDragInputSystem()
        {
            InputSystemMouse mouse = InputSystemMouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointerPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                BeginOffTilePointerTracking(pointerPosition, MousePointerId);
            }

            if (!offTilePointerTracking || offTilePointerId != MousePointerId)
            {
                return;
            }

            if (mouse.leftButton.isPressed)
            {
                TryContinueOffTileCrossDrag(pointerPosition);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndOffTileCrossDrag();
            }
        }

        private bool UpdateOffTileTouchDragInputSystem()
        {
            InputSystemTouchscreen touchscreen = InputSystemTouchscreen.current;
            if (touchscreen == null)
            {
                return false;
            }

            bool sawActiveTouch = false;
            bool trackedTouchFound = false;
            InputSystemTouchControl trackedTouch = null;

            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                InputSystemTouchControl touch = touchscreen.touches[i];
                InputSystemTouchPhase phase = touch.phase.ReadValue();
                int touchId = touch.touchId.ReadValue();

                if (IsInputSystemTouchActive(phase))
                {
                    sawActiveTouch = true;
                }

                if (offTilePointerTracking && touchId == offTilePointerId)
                {
                    trackedTouch = touch;
                    trackedTouchFound = true;
                    break;
                }

                if (!offTilePointerTracking && phase == InputSystemTouchPhase.Began)
                {
                    BeginOffTilePointerTracking(touch.position.ReadValue(), touchId);
                    trackedTouch = touch;
                    trackedTouchFound = offTilePointerTracking;
                    break;
                }
            }

            if (!sawActiveTouch && !offTilePointerTracking)
            {
                return false;
            }

            if (!offTilePointerTracking)
            {
                return true;
            }

            if (!trackedTouchFound || trackedTouch == null)
            {
                EndOffTileCrossDrag();
                return true;
            }

            InputSystemTouchPhase trackedPhase = trackedTouch.phase.ReadValue();
            if (trackedPhase == InputSystemTouchPhase.Ended || trackedPhase == InputSystemTouchPhase.Canceled)
            {
                EndOffTileCrossDrag();
                return true;
            }

            TryContinueOffTileCrossDrag(trackedTouch.position.ReadValue());
            return true;
        }

        private static bool IsInputSystemTouchActive(InputSystemTouchPhase phase)
        {
            return phase == InputSystemTouchPhase.Began
                || phase == InputSystemTouchPhase.Moved
                || phase == InputSystemTouchPhase.Stationary;
        }

        private void BeginOffTilePointerTracking(Vector2 screenPosition, int pointerId)
        {
            Camera eventCamera = boardView.GetPointerEventCamera();
            if (boardView.TryPointerToCell(screenPosition, eventCamera, out _, out _) || PointerOverBlockingUi(screenPosition))
            {
                StopOffTilePointerTracking();
                return;
            }

            offTilePointerTracking = true;
            offTilePointerStartedDrag = false;
            offTilePointerId = pointerId;
        }

        private void TryContinueOffTileCrossDrag(Vector2 screenPosition)
        {
            Camera eventCamera = boardView.GetPointerEventCamera();
            if (PointerOverBlockingUi(screenPosition) || !boardView.TryPointerToCell(screenPosition, eventCamera, out _, out _))
            {
                return;
            }

            if (!offTilePointerStartedDrag)
            {
                offTilePointerStartedDrag = true;
                BeginCrossDragAtPointer(screenPosition, eventCamera);
                return;
            }

            CrossAtPointer(screenPosition, eventCamera);
        }

        private void CrossAtPointer(Vector2 screenPosition, Camera eventCamera)
        {
            if (InputLocked || !crossDragging)
            {
                return;
            }

            if (boardView.TryPointerToCell(screenPosition, eventCamera, out int row, out int column))
            {
                ApplyCrossDrag(row, column);
            }
        }

        private void EndOffTileCrossDrag()
        {
            if (offTilePointerStartedDrag)
            {
                EndCrossDrag();
                return;
            }

            StopOffTilePointerTracking();
        }

        private void StopOffTilePointerTracking()
        {
            offTilePointerTracking = false;
            offTilePointerStartedDrag = false;
            offTilePointerId = MousePointerId;
        }

        private bool PointerOverBlockingUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            sharedPointerEventData ??= new PointerEventData(eventSystem);
            sharedPointerEventData.Reset();
            sharedPointerEventData.position = screenPosition;

            pointerRaycastResults.Clear();
            eventSystem.RaycastAll(sharedPointerEventData, pointerRaycastResults);
            foreach (RaycastResult result in pointerRaycastResults)
            {
                Transform hitTransform = result.gameObject != null ? result.gameObject.transform : null;
                if (boardView.ContainsBoardTransform(hitTransform))
                {
                    return false;
                }

                if (hitTransform != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
