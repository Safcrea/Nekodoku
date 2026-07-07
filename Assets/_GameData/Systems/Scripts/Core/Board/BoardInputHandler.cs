using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

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
        private TutorialController tutorialController;
        private GameplayScreen gameplayScreen;

        private bool inputLocked;
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

        public bool InputLocked => inputLocked;

        public void Initialize(GameManager manager, BoardView board, TutorialController tutorial, GameplayScreen screen)
        {
            gameManager = manager;
            boardView = board;
            tutorialController = tutorial;
            gameplayScreen = screen;
        }

        private void Update()
        {
            UpdateOffTileCrossDrag();
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
        }

        public void ResetCrosses()
        {
            if (inputLocked || gameManager?.Board == null)
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
            tutorialController?.StopGuide();
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

        public void BeginCrossDrag(PointerEventData eventData)
        {
            if (inputLocked)
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
            crossDragPlacesCrosses = true;

            PuzzleBoard board = gameManager.Board;
            if (boardView.TryPointerToCell(screenPosition, eventCamera, out int row, out int column))
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
            if (inputLocked || gameManager?.Board == null || boardView == null)
            {
                StopOffTilePointerTracking();
                return;
            }

            if (Input.touchCount > 0)
            {
                UpdateOffTileTouchDrag();
                return;
            }

            UpdateOffTileMouseDrag();
        }

        private void UpdateOffTileMouseDrag()
        {
            Vector2 pointerPosition = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                BeginOffTilePointerTracking(pointerPosition, MousePointerId);
            }

            if (!offTilePointerTracking || offTilePointerId != MousePointerId)
            {
                return;
            }

            if (Input.GetMouseButton(0))
            {
                TryContinueOffTileCrossDrag(pointerPosition);
            }

            if (Input.GetMouseButtonUp(0))
            {
                EndOffTileCrossDrag();
            }
        }

        private void UpdateOffTileTouchDrag()
        {
            Touch? trackedTouch = null;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (offTilePointerTracking)
                {
                    if (touch.fingerId == offTilePointerId)
                    {
                        trackedTouch = touch;
                        break;
                    }
                }
                else if (touch.phase == TouchPhase.Began)
                {
                    BeginOffTilePointerTracking(touch.position, touch.fingerId);
                    trackedTouch = touch;
                    break;
                }
            }

            if (!offTilePointerTracking)
            {
                return;
            }

            if (!trackedTouch.HasValue)
            {
                EndOffTileCrossDrag();
                return;
            }

            Touch touchValue = trackedTouch.Value;
            if (touchValue.phase == TouchPhase.Ended || touchValue.phase == TouchPhase.Canceled)
            {
                EndOffTileCrossDrag();
                return;
            }

            TryContinueOffTileCrossDrag(touchValue.position);
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
            if (inputLocked || !crossDragging)
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
