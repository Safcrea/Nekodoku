using AllIn1SpringsToolkit;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meowdoku
{
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
        private const string RevealedAnimationName = "Revealed";
        private const string SadAnimationName = "Sad";
        private const float CatRevealFlipWaitSeconds = 0.45f;
        private const float CatRevealFlipRotationForce = 150f;
        private const float CatRevealFlipRotationDrag = 10f;

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
        [SerializeField] private float RevealedBaseAlpha = 0.6f;


        public RectTransform Rect { get; private set; }
        public int Row { get; private set; }
        public int Column { get; private set; }

        private BoardInputHandler inputHandler;
        private Color regionColor = Color.white;
        private CellMark lastMark;
        private bool lastRevealedCat;
        private bool lastRevealedMiss;

        private bool dragged;
        private bool hasRecentTap;
        private float lastTapTime;
        private bool catReactionPlaying;
        private bool crossResetPreviewVisible;
        private Tween crossResetTween;
        private bool catRevealFlipInProgress;
        private bool catRevealFrontShown;

        private void Awake()
        {
            Rect = (RectTransform)transform;
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
            catRevealFlipInProgress = false;
            catRevealFrontShown = false;
        }

        public void Bind(BoardInputHandler owner, int row, int column)
        {
            inputHandler = owner;
            Row = row;
            Column = column;
            hasRecentTap = false;
        }

        public void SetRegion(int regionIndex)
        {
            regionColor = regionPalette != null ? regionPalette.ColorForRegion(regionIndex) : Color.white;
            baseImage.color = regionColor;
        }

        public void SetInteractable(bool interactable)
        {
            canvasGroup.blocksRaycasts = interactable;
        }

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

            catImage.enabled = revealedCat && (!catRevealFlipInProgress || catRevealFrontShown);
            catImage.color = Color.white;
            crossImage.enabled = (!revealedCat && mark == CellMark.Cross) || crossResetPreviewVisible;
            crossImage.color = Color.white;
            noCatImage.enabled = !revealedCat && revealedMiss;
            noCatImage.color = Color.red;
            baseImage.color = ComputeBaseColor(revealedCat);

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

        private Color ComputeBaseColor(bool revealedCat)
        {
            Color color = regionColor;
            color.a = revealedCat ? RevealedBaseAlpha : 1f;
            return color;
        }

        // ---- intro ----

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

        // ---- juice ----

        public void PlayCrossJelly()
        {
            baseImage.DOKill();

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
            baseImage.DOKill();
            catImage.DOKill();
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
        }

        // ---- input ----

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
            gridCellSpring.AddVelocityScale(Vector3.one * -20f);
            DOVirtual.DelayedCall(0.01f, () =>
            {
                gridCellSpring.SetCurrentValueRotation(new Vector3(0f, 180f, 0f));
                gridCellSpring.SetTargetRotation(new Vector3(0f, 180f, 0f));
                gridCellSpring.SetVelocityRotation(Vector3.zero);
                Rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
                gridCellSpring.SetTargetRotation(Vector3.zero);
            });
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
    }
}
