using AllIn1SpringsToolkit;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meowdoku
{
    public enum TutorialDimMode
    {
        None,
        NotRequired
    }

    /// <summary>
    /// Lives on the root of the Cell prefab (Assets/_GameData/Systems/Prefabs/Grid Cell.prefab).
    /// Four visuals - base (region color), cross (live mark), cat, and NoCatImg (revealed
    /// miss) - all sized/positioned/tinted via the prefab's own Inspector setup, not from
    /// code. Owns its own per-cell juice (jelly, wrong-punch, cat-found pop) since it already
    /// owns the visuals those animate. The jelly/wrong-punch reactions are spring-driven
    /// (<see cref="TransformSpringComponent"/> on the cross/NoCatImg transforms specifically,
    /// never the cell root) so they can compound naturally under rapid input instead of
    /// resetting cold like a DOTween punch would.
    /// </summary>
    public sealed class GridCell : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Constants

        private const string RevealedAnimationName = "Revealed";
        private const string SadAnimationName = "Sad";
        private const float CatRevealFlipWaitSeconds = 0.45f;
        private const float CatRevealFlipRotationForce = 150f;
        private const float CatRevealFlipRotationDrag = 10f;

        #endregion

        #region Serialized Fields

        [Header("Data")]
        [SerializeField] private RegionPalette regionPalette;

        [Header("Visual References")]
        [SerializeField] private Image baseImage;
        [SerializeField] private Image crossImage;

        [SerializeField] private Image catImage;

        [SerializeField] private Image noCatImage;

        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private SpriteSheetAnimationPlayer catAnimator;

        [Header("TRS References")]
        [SerializeField] private TransformSpringComponent gridCellSpring;

        [SerializeField] private TransformSpringComponent noCatSpring;

        [Header("Tuning")]
        [SerializeField] private float DoubleTapSeconds = 0.45f;
        [SerializeField] private float CrossJellySeconds = 0.34f;
        [SerializeField] private float CatFoundPopSeconds = 0.24f;
        [SerializeField] private float CrossJellyScaleImpulse = 6f;
        [SerializeField] private float WrongPunchScaleImpulse = 5f;
        [SerializeField] private float TutorialDimmedAlpha = 0.28f;
        [SerializeField] private float TutorialDimFadeSeconds = 0.18f;
        [SerializeField] private float BaseShadeFadeSeconds = 0.16f;
        [SerializeField] private Color CrossToggleHighlightColor = new Color(1f, 0.92f, 0.55f, 1f);
        [SerializeField] private float CrossToggleHighlightStrength = 0.75f;
        [SerializeField] private float CrossToggleHighlightInSeconds = 0.06f;
        [SerializeField] private float CrossToggleHighlightOutSeconds = 0.18f;
        [SerializeField] private float HintMinAlpha = 0.2f;
        [SerializeField] private float HintMaxAlpha = 0.55f;
        [SerializeField] private float HintPulseSeconds = 0.55f;

        #endregion

        #region Public Properties

        /// <summary>
        /// Computed rather than cached from Awake() - a cell freshly Instantiate()'d into an
        /// inactive-at-the-time hierarchy (e.g. under a HUD panel that's hidden while the tutorial's
        /// board is being rebuilt) defers Awake() until it becomes active, which left this null for
        /// anything that queried it (e.g. the tutorial's hand-guide) before then. transform itself
        /// needs no lifecycle to be valid.
        /// </summary>
        public RectTransform Rect => (RectTransform)transform;

        public int Row { get; private set; }
        public int Column { get; private set; }

        #endregion

        #region Private State

        private BoardInputHandler inputHandler;
        private CellMark lastMark;
        private bool lastRevealedCat;
        private bool lastRevealedMiss;

        private bool dragged;
        private bool hasRecentTap;
        private float lastTapTime;
        private bool catReactionPlaying;
        private bool crossResetPreviewVisible;
        private Tween crossResetTween;
        private Tween baseColorTween;
        private bool catRevealFlipInProgress;
        private bool catRevealFrontShown;
        private TutorialDimMode tutorialDimMode;
        private Color targetBaseColor = Color.white;
        private bool baseColorInitialized;
        private Tween catHintTween;
        private bool catHintActive;
        private Tween crossHintTween;
        private bool crossHintActive;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            crossImage.raycastTarget = false;
            catImage.raycastTarget = false;
            noCatImage.raycastTarget = false;
        }

        private void OnEnable()
        {
            if (catAnimator != null)
            {
                catAnimator.AnimationCompleted += HandleCatAnimationCompleted;
            }

            if (lastRevealedCat)
            {
                PlayRevealedCatAnimation(false);
            }
        }

        private void Update()
        {
            UpdateCatRevealFrontFace();
        }

        private void OnDisable()
        {
            if (catAnimator != null)
            {
                catAnimator.AnimationCompleted -= HandleCatAnimationCompleted;
            }

            catReactionPlaying = false;
            CancelCrossResetPreview();
            baseColorTween?.Kill();
            baseColorTween = null;
            catHintTween?.Kill();
            catHintTween = null;
            catHintActive = false;
            crossHintTween?.Kill();
            crossHintTween = null;
            crossHintActive = false;
            catRevealFlipInProgress = false;
            catRevealFrontShown = false;
        }

        #endregion

        #region Setup & Binding

        public void Bind(BoardInputHandler owner, int row, int column)
        {
            inputHandler = owner;
            Row = row;
            Column = column;
            hasRecentTap = false;
        }

        public void SetRegion(int regionIndex)
        {
            if (baseImage != null)
            {
                baseImage.sprite = regionPalette != null ? regionPalette.SpriteForRegion(regionIndex) : null;
            }

            SetBaseColorImmediate(Color.white);
        }

        public void SetInteractable(bool interactable)
        {
            canvasGroup.blocksRaycasts = interactable;
        }

        #endregion

        #region Tutorial Dimming

        /// <summary>
        /// Applies tutorial dimming without rewriting alpha when the mode is unchanged. RefreshVisuals
        /// runs constantly, so this avoids fighting intro-pop's own fade tween outside real dim changes.
        /// </summary>
        public void SetTutorialDimMode(TutorialDimMode dimMode)
        {
            if (dimMode == tutorialDimMode)
            {
                return;
            }

            tutorialDimMode = dimMode;
            canvasGroup.DOKill();
            float targetAlpha = AlphaForTutorialDimMode(dimMode);
            if (!isActiveAndEnabled || TutorialDimFadeSeconds <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
                return;
            }

            canvasGroup.DOFade(targetAlpha, TutorialDimFadeSeconds)
                .SetEase(Ease.OutSine)
                .SetUpdate(true);
        }

        private float AlphaForTutorialDimMode(TutorialDimMode dimMode)
        {
            return dimMode switch
            {
                TutorialDimMode.NotRequired => TutorialDimmedAlpha,
                _ => 1f,
            };
        }

        #endregion

        #region Tutorial Hints (fade in/out loop pointing at an unrevealed cat or an uncrossed cell)

        /// <summary>
        /// Loops the cat visual's opacity to hint "there is a cat here" without actually revealing it
        /// (board state/mark are untouched). While active, <see cref="ApplyVisualState"/> leaves the cat
        /// image alone so it doesn't fight the loop; turning the hint off re-applies real state immediately.
        /// </summary>
        public void SetCatHint(bool active)
        {
            if (catHintActive == active)
            {
                return;
            }

            catHintActive = active;
            catHintTween?.Kill();
            catHintTween = null;
            catImage.DOKill();

            if (!active)
            {
                catImage.color = Color.white;
                ApplyVisualState(lastMark, lastRevealedCat, lastRevealedMiss);
                return;
            }

            catImage.enabled = true;
            Color color = Color.white;
            color.a = HintMinAlpha;
            catImage.color = color;

            if (catAnimator != null && !catReactionPlaying)
            {
                catAnimator.Play(RevealedAnimationName, false);
            }

            catHintTween = catImage.DOFade(HintMaxAlpha, HintPulseSeconds)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        /// <summary>Loops the cross visual's opacity to hint "cross this cell" without actually marking it.</summary>
        public void SetCrossHint(bool active)
        {
            if (crossHintActive == active)
            {
                return;
            }

            crossHintActive = active;
            crossHintTween?.Kill();
            crossHintTween = null;
            crossImage.DOKill();

            if (!active)
            {
                crossImage.color = Color.white;
                ApplyVisualState(lastMark, lastRevealedCat, lastRevealedMiss);
                return;
            }

            crossImage.enabled = true;
            Color color = Color.white;
            color.a = HintMinAlpha;
            crossImage.color = color;

            crossHintTween = crossImage.DOFade(HintMaxAlpha, HintPulseSeconds)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        /// <summary>Stops both hint loops if running. Safe to call unconditionally.</summary>
        public void ClearHint()
        {
            SetCatHint(false);
            SetCrossHint(false);
        }

        #endregion

        #region Visual State Sync

        /// <summary>Syncs static appearance to the current board state. Called every gameplay refresh.</summary>
        public void Refresh(CellMark mark, bool revealedCat, bool revealedMiss)
        {
            lastMark = mark;
            lastRevealedCat = revealedCat;
            lastRevealedMiss = revealedMiss;
            ApplyVisualState(mark, revealedCat, revealedMiss);
        }

        private void ApplyVisualState(CellMark mark, bool revealedCat, bool revealedMiss)
        {
            if (mark == CellMark.Cross || revealedCat || revealedMiss)
            {
                CancelCrossResetPreview();
            }

            if (!catHintActive)
            {
                catImage.enabled = revealedCat && (!catRevealFlipInProgress || catRevealFrontShown);
                catImage.color = Color.white;
            }

            if (!crossHintActive)
            {
                crossImage.enabled = (!revealedCat && mark == CellMark.Cross) || crossResetPreviewVisible;
                crossImage.color = Color.white;
            }
            noCatImage.enabled = !revealedCat && revealedMiss;
            noCatImage.color = Color.red;
            FadeBaseColorTo(Color.white, BaseShadeFadeSeconds);

            if (revealedCat)
            {
                PlayRevealedCatAnimation(false);
            }
            else
            {
                catReactionPlaying = false;
                catAnimator?.Stop();
            }
        }

        public void PlayCrossResetJelly(float delaySeconds)
        {
            if (crossImage == null)
            {
                return;
            }

            crossResetTween?.Kill();
            crossResetTween = null;
            crossResetPreviewVisible = true;
            crossImage.enabled = true;
            crossImage.color = Color.white;

            float startDelay = Mathf.Max(0f, delaySeconds);
            crossResetTween = DOVirtual.DelayedCall(startDelay, () =>
            {
                PlayCrossJelly();
                crossResetTween = DOVirtual.DelayedCall(Mathf.Max(0.01f, CrossJellySeconds), () =>
                {
                    crossResetPreviewVisible = false;
                    crossResetTween = null;
                    ApplyVisualState(lastMark, lastRevealedCat, lastRevealedMiss);
                }).SetUpdate(true);
            }).SetUpdate(true);
        }

        private void FadeBaseColorTo(Color color, float seconds)
        {
            targetBaseColor = color;
            if (baseImage == null)
            {
                return;
            }

            if (!baseColorInitialized || !isActiveAndEnabled || seconds <= 0f)
            {
                SetBaseColorImmediate(color);
                return;
            }

            if (ColorsApproximately(baseImage.color, color))
            {
                return;
            }

            baseColorTween?.Kill();
            baseImage.DOKill();
            baseColorTween = baseImage.DOColor(color, seconds)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .OnComplete(() => baseColorTween = null);
        }

        private void SetBaseColorImmediate(Color color)
        {
            targetBaseColor = color;
            baseColorInitialized = true;
            baseColorTween?.Kill();
            baseColorTween = null;

            if (baseImage != null)
            {
                baseImage.DOKill();
                baseImage.color = color;
            }
        }

        /// <summary>
        /// Flashes toward <see cref="CrossToggleHighlightColor"/> rather than <see cref="Color.white"/> -
        /// now that region identity lives in <see cref="baseImage"/>'s sprite (not a saturated tint),
        /// <see cref="targetBaseColor"/> sits near-white already, so lerping toward white would be a no-op.
        /// </summary>
        private void PlayCrossToggleHighlight()
        {
            if (baseImage == null)
            {
                return;
            }

            baseColorTween?.Kill();
            baseImage.DOKill();

            Color flashColor = Color.Lerp(targetBaseColor, CrossToggleHighlightColor, Mathf.Clamp01(CrossToggleHighlightStrength));
            flashColor.a = targetBaseColor.a;

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(baseImage.DOColor(flashColor, CrossToggleHighlightInSeconds).SetEase(Ease.OutSine));
            sequence.Append(baseImage.DOColor(targetBaseColor, CrossToggleHighlightOutSeconds).SetEase(Ease.InOutSine));
            sequence.OnComplete(() => baseColorTween = null);
            baseColorTween = sequence;
        }

        private static bool ColorsApproximately(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r)
                && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b)
                && Mathf.Approximately(a.a, b.a);
        }

        #endregion

        #region Intro Animation

        public void PlayIntroPop(float delaySeconds, float durationSeconds, bool interactableAfter)
        {
            Rect.DOKill();
            canvasGroup.DOKill();
            Rect.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            Rect.DOScale(1f, durationSeconds).SetEase(Ease.OutBack).SetDelay(delaySeconds).SetUpdate(true);
            canvasGroup.DOFade(1f, durationSeconds * 0.7f).SetDelay(delaySeconds).SetUpdate(true)
                .OnComplete(() => canvasGroup.blocksRaycasts = interactableAfter);
        }

        public void SkipIntro(bool interactable)
        {
            Rect.DOKill();
            canvasGroup.DOKill();
            Rect.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = interactable;
        }

        #endregion

        #region Juice & Reactions

        public void PlayCrossJelly()
        {
            PlayCrossToggleHighlight();

            if (gridCellSpring != null)
            {
                gridCellSpring.AddVelocityScale(Vector3.one * CrossJellyScaleImpulse);
            }
        }

        public void PlayWrongPunch()
        {
            if (noCatSpring != null)
            {
                noCatSpring.AddVelocityScale(Vector3.one * WrongPunchScaleImpulse);
            }
        }

        /// <summary>Played on every already-revealed cat when a heart is lost elsewhere on the board.</summary>
        public void PlayHeartLostReaction()
        {
            if (!lastRevealedCat || catAnimator == null)
            {
                return;
            }

            catReactionPlaying = true;
            if (!catAnimator.PlayOnceThen(SadAnimationName, RevealedAnimationName))
            {
                catReactionPlaying = false;
                PlayRevealedCatAnimation(false);
            }
        }

        /// <summary>Pop + gold flash played when a cat is correctly revealed. Returns the tween so callers can await it.</summary>
        public Tween PlayCatFoundPop()
        {
            Rect.DOKill();
            catImage.DOKill();

            catReactionPlaying = false;
            catRevealFlipInProgress = true;
            catRevealFrontShown = false;
            catImage.enabled = false;
            catAnimator?.Stop();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.AppendCallback(PlayMemoryTileRevealFlip);
            sequence.AppendInterval(Mathf.Max(CatFoundPopSeconds, CatRevealFlipWaitSeconds));
            sequence.OnComplete(() =>
            {
                catRevealFlipInProgress = false;
                ShowCatRevealFront();
                SnapGridCellSpringRotationToFaceUp();
                catImage.color = Color.white;
            });
            return sequence;
        }

        /// <summary>Cancels any in-flight per-cell animation and snaps back to the last known state. Call around level load/restart/undo.</summary>
        public void StopJuice()
        {
            Rect.DOKill();
            baseColorTween?.Kill();
            baseColorTween = null;
            baseImage.DOKill();
            catImage.DOKill();
            catHintTween?.Kill();
            catHintTween = null;
            catHintActive = false;
            crossHintTween?.Kill();
            crossHintTween = null;
            crossHintActive = false;
            Rect.localScale = Vector3.one;
            CancelCrossResetPreview();
            catRevealFlipInProgress = false;
            catRevealFrontShown = false;

            if (gridCellSpring != null)
            {
                gridCellSpring.ReachEquilibriumScale();
                SnapGridCellSpringRotationToFaceUp();
            }

            if (noCatSpring != null)
            {
                noCatSpring.ReachEquilibriumScale();
            }

            ApplyVisualState(lastMark, lastRevealedCat, lastRevealedMiss);
            SetBaseColorImmediate(Color.white);
        }

        #endregion

        #region Input Handling

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragged)
            {
                dragged = false;
                hasRecentTap = false;
                return;
            }

            float now = Time.unscaledTime;
            bool isDoubleTap = eventData.clickCount >= 2 || (hasRecentTap && now - lastTapTime <= DoubleTapSeconds);
            hasRecentTap = false;

            if (isDoubleTap)
            {
                inputHandler.CommitCat(Row, Column);
                return;
            }

            hasRecentTap = true;
            lastTapTime = now;
            inputHandler.ToggleCross(Row, Column);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragged = true;
            hasRecentTap = false;
            inputHandler.BeginCrossDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            dragged = true;
            inputHandler.CrossAtPointer(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            inputHandler.EndCrossDrag();
        }

        #endregion

        #region Cat Reveal Flip Animation

        private void PlayRevealedCatAnimation(bool restart)
        {
            if (catAnimator == null || catReactionPlaying)
            {
                return;
            }

            if (catRevealFlipInProgress && !catRevealFrontShown)
            {
                return;
            }

            if (!restart && catAnimator.CurrentAnimationName == RevealedAnimationName && catAnimator.IsPlaying)
            {
                return;
            }

            catAnimator.Play(RevealedAnimationName, restart);
        }

        private void HandleCatAnimationCompleted(string animationName)
        {
            if (animationName == SadAnimationName)
            {
                catReactionPlaying = false;
            }
        }

        private void CancelCrossResetPreview()
        {
            crossResetTween?.Kill();
            crossResetTween = null;
            crossResetPreviewVisible = false;
        }

        private void PlayMemoryTileRevealFlip()
        {
            if (gridCellSpring == null)
            {
                ShowCatRevealFront();
                return;
            }

            Rect.localScale = Vector3.one;
            gridCellSpring.SpringRotationEnabled = true;
            gridCellSpring.SetUnifiedForceAndDragRotation(CatRevealFlipRotationForce, CatRevealFlipRotationDrag);
            gridCellSpring.AddVelocityScale(Vector3.one * -5f);
            gridCellSpring.SetCurrentValueRotation(new Vector3(0f, 180f, 0f));
            gridCellSpring.SetTargetRotation(new Vector3(0f, 180f, 0f));
            gridCellSpring.SetVelocityRotation(Vector3.zero);
            Rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            gridCellSpring.SetTargetRotation(Vector3.zero);
        }

        private void UpdateCatRevealFrontFace()
        {
            if (!catRevealFlipInProgress || catRevealFrontShown)
            {
                return;
            }

            bool isFrontFace = Vector3.Dot(Rect.forward, Vector3.forward) >= 0f;
            if (isFrontFace)
            {
                ShowCatRevealFront();
            }
        }

        private void ShowCatRevealFront()
        {
            catRevealFrontShown = true;
            catImage.enabled = lastRevealedCat;
            PlayRevealedCatAnimation(true);
        }

        private void SnapGridCellSpringRotationToFaceUp()
        {
            Rect.localRotation = Quaternion.identity;
            if (gridCellSpring == null)
            {
                return;
            }

            gridCellSpring.SetCurrentValueRotation(Quaternion.identity);
            gridCellSpring.SetTargetRotation(Quaternion.identity);
            gridCellSpring.SetVelocityRotation(Vector3.zero);
        }

        #endregion
    }
}
