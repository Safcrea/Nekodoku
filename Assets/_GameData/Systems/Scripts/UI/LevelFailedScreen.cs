using System;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The out-of-hearts popup: panel pop-in, a non-typewriter Text Animator title reveal,
    /// broken-heart accents, then a retry button fade/pop. Raises <see cref="RetryRequested"/>
    /// when the retry button is clicked -
    /// GameManager subscribes to that in Start() rather than this class knowing anything
    /// about level restarting.
    /// </summary>
    public sealed class LevelFailedScreen : MonoBehaviour
    {
        private const float PopSeconds = 0.34f;
        private const float StaggerSeconds = 0.1f;
        private const float LabelWobbleDegrees = 8f;
        private const float HeartPopSeconds = 0.36f;
        private const float HeartIdleRotateSeconds = 1.4f;
        private const float HeartIdleRotateDegrees = 5f;
        private const float RetryButtonDelaySeconds = 0.36f;
        private const float RetryAfterExtraLifeDelaySeconds = 0.5f;

        [SerializeField] private RectTransform failedPanel;
        [SerializeField] private CanvasGroup failedPanelCanvasGroup;
        [SerializeField] private RectTransform failedLabel;
        [SerializeField] private CanvasGroup failedLabelCanvasGroup;
        [Tooltip("Preferred non-typewriter Text Animator component for the 'Level Failed' title. If empty, the script falls back to failedTypewriter.TextAnimator or a Text Animator on failedLabel.")]
        [SerializeField] private TextAnimatorComponentBase failedTextAnimator;
        [SerializeField] private TypewriterComponent failedTypewriter;
        [SerializeField] private TMP_Text failedLabelText;

        [Header("Broken hearts")]
        [SerializeField] private RectTransform brokenHeartLeft;
        [SerializeField] private CanvasGroup brokenHeartLeftCanvasGroup;
        [SerializeField] private RectTransform brokenHeartRight;
        [SerializeField] private CanvasGroup brokenHeartRightCanvasGroup;

        [SerializeField] private RectTransform retryButton;
        [SerializeField] private CanvasGroup retryButtonCanvasGroup;

        [Tooltip("Grants one extra life and continues the same attempt instead of restarting it. Only offered once per level-load/retry (see ShowFailAnimation's extraLifeAvailable parameter).")]
        [SerializeField] private RectTransform extraLifeButton;
        [SerializeField] private CanvasGroup extraLifeButtonCanvasGroup;

        private Sequence failSequence;
        private Tween leftHeartIdleTween;
        private Tween rightHeartIdleTween;

        public event Action RetryRequested;
        public event Action ExtraLifeRequested;

        private void Awake()
        {
            ResolveReferences();

            if (retryButton != null && retryButton.TryGetComponent(out Button button))
            {
                button.onClick.AddListener(() =>
                {
                    GameHaptics.Selection();
                    SoundManager.PlaySound(SFX.ButtonClick);
                    RetryRequested?.Invoke();
                });
            }

            if (extraLifeButton != null && extraLifeButton.TryGetComponent(out Button extraLifeBtn))
            {
                extraLifeBtn.onClick.AddListener(() =>
                {
                    GameHaptics.Selection();
                    SoundManager.PlaySound(SFX.ButtonClick);
                    ExtraLifeRequested?.Invoke();
                });
            }
        }

        /// <summary>
        /// <paramref name="extraLifeAvailable"/> gates whether the "Get Extra Life" button is offered
        /// this time - false once it's already been claimed for the current attempt (see
        /// <see cref="PuzzleBoard.HasClaimedExtraLife"/>), true again after a fresh level load or retry.
        /// When offered, it reveals first; the retry button then follows after a further delay so the
        /// player sees the free option before the fallback. When not offered, retry reveals at the same
        /// slot the extra-life button would have used, so the pacing doesn't leave an empty gap.
        /// </summary>
        public void ShowFailAnimation(bool extraLifeAvailable)
        {
            bool wasVisible = failedPanel.gameObject.activeSelf;
            failedPanel.gameObject.SetActive(true);
            if (wasVisible)
            {
                return;
            }

            KillTweens();
            PrepareTextAnimation();

            SoundManager.PlaySound(SFX.LevelFailed);
            GameHaptics.Failure();

            failedPanel.localScale = Vector3.one * 0.84f;
            failedPanelCanvasGroup.alpha = 0f;
            failedLabel.localScale = Vector3.one * 0.84f;
            failedLabel.localRotation = Quaternion.identity;
            failedLabelCanvasGroup.alpha = 0f;
            PrepareHeart(brokenHeartLeft, brokenHeartLeftCanvasGroup, -14f);
            PrepareHeart(brokenHeartRight, brokenHeartRightCanvasGroup, 14f);
            retryButton.localScale = Vector3.one * 0.84f;
            retryButtonCanvasGroup.alpha = 0f;
            retryButtonCanvasGroup.blocksRaycasts = false;
            PrepareExtraLifeButton(extraLifeAvailable);

            failSequence = DOTween.Sequence().SetUpdate(true);
            failSequence.Join(failedPanelCanvasGroup.DOFade(1f, PopSeconds));
            failSequence.Join(failedPanel.DOScale(1f, PopSeconds).SetEase(Ease.OutBack));

            failSequence.Insert(StaggerSeconds, failedLabelCanvasGroup.DOFade(1f, PopSeconds * 0.6f));
            failSequence.Insert(StaggerSeconds, failedLabel.DOScale(1f, PopSeconds * 0.6f).SetEase(Ease.OutBack));
            failSequence.Insert(StaggerSeconds, failedLabel.DOPunchRotation(
                new Vector3(0f, 0f, LabelWobbleDegrees),
                PopSeconds * 1.4f,
                6,
                0.7f));
            failSequence.InsertCallback(StaggerSeconds, PlayTextAnimation);

            float heartsDelay = StaggerSeconds * 2f;
            InsertHeartAnimation(failSequence, brokenHeartLeft, brokenHeartLeftCanvasGroup, heartsDelay, -1);
            InsertHeartAnimation(failSequence, brokenHeartRight, brokenHeartRightCanvasGroup, heartsDelay + 0.06f, 1);

            float firstButtonDelay = heartsDelay + RetryButtonDelaySeconds;
            float retryDelay = firstButtonDelay;

            if (extraLifeAvailable && extraLifeButton != null)
            {
                InsertButtonReveal(failSequence, extraLifeButton, extraLifeButtonCanvasGroup, firstButtonDelay);
                retryDelay = firstButtonDelay + RetryAfterExtraLifeDelaySeconds;
            }

            InsertButtonReveal(failSequence, retryButton, retryButtonCanvasGroup, retryDelay);
        }

        private void InsertButtonReveal(Sequence sequence, RectTransform button, CanvasGroup group, float delay)
        {
            if (sequence == null || button == null || group == null)
            {
                return;
            }

            sequence.Insert(delay, group.DOFade(1f, PopSeconds * 0.55f));
            sequence.Insert(delay, button.DOScale(1f, PopSeconds * 0.7f).SetEase(Ease.OutBack));
            sequence.InsertCallback(delay + (PopSeconds * 0.55f), () => group.blocksRaycasts = true);
        }

        private void PrepareExtraLifeButton(bool available)
        {
            if (extraLifeButton == null)
            {
                return;
            }

            extraLifeButton.gameObject.SetActive(available);
            if (!available)
            {
                return;
            }

            extraLifeButton.localScale = Vector3.one * 0.84f;
            if (extraLifeButtonCanvasGroup != null)
            {
                extraLifeButtonCanvasGroup.alpha = 0f;
                extraLifeButtonCanvasGroup.blocksRaycasts = false;
            }
        }

        public void Hide()
        {
            KillTweens();

            failedPanel.gameObject.SetActive(false);

            failedPanel.localScale = Vector3.one;
            failedPanelCanvasGroup.alpha = 1f;
            failedLabel.localScale = Vector3.one;
            failedLabel.localRotation = Quaternion.identity;
            failedLabelCanvasGroup.alpha = 1f;
            ResetHeart(brokenHeartLeft, brokenHeartLeftCanvasGroup);
            ResetHeart(brokenHeartRight, brokenHeartRightCanvasGroup);
            retryButton.localScale = Vector3.one;
            retryButtonCanvasGroup.alpha = 1f;
            retryButtonCanvasGroup.blocksRaycasts = true;
            ResetExtraLifeButtonVisual();

            if (failedTypewriter != null)
            {
                failedTypewriter.StopShowingText();
            }
        }

        private void ResetExtraLifeButtonVisual()
        {
            if (extraLifeButton == null)
            {
                return;
            }

            extraLifeButton.localScale = Vector3.one;
            if (extraLifeButtonCanvasGroup != null)
            {
                extraLifeButtonCanvasGroup.alpha = 1f;
                extraLifeButtonCanvasGroup.blocksRaycasts = true;
            }

            extraLifeButton.gameObject.SetActive(false);
        }

        private string BuildFailMessage()
        {
            return "<bounce><slidev>Cats Lost</slidev></bounce>";
        }

        private void ResolveReferences()
        {
            if (failedLabelText == null && failedLabel != null)
            {
                failedLabel.TryGetComponent(out failedLabelText);
            }

            if (failedTextAnimator == null)
            {
                if (failedTypewriter != null)
                {
                    failedTextAnimator = failedTypewriter.TextAnimator;
                }
                else if (failedLabel != null)
                {
                    failedLabel.TryGetComponent(out failedTextAnimator);
                }
            }

            brokenHeartLeftCanvasGroup = ResolveCanvasGroup(brokenHeartLeft, brokenHeartLeftCanvasGroup);
            brokenHeartRightCanvasGroup = ResolveCanvasGroup(brokenHeartRight, brokenHeartRightCanvasGroup);
        }

        private static CanvasGroup ResolveCanvasGroup(RectTransform rect, CanvasGroup existing)
        {
            if (existing != null || rect == null)
            {
                return existing;
            }

            if (!rect.TryGetComponent(out CanvasGroup group))
            {
                group = rect.gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private void PrepareTextAnimation()
        {
            if (failedTypewriter != null)
            {
                failedTypewriter.StopShowingText();
            }

            if (failedTextAnimator != null)
            {
                failedTextAnimator.TryInitializingOnce();
                failedTextAnimator.time.RestartTime();
                failedTextAnimator.SetText(BuildFailMessage(), true);
                return;
            }

            if (failedLabelText != null)
            {
                failedLabelText.text = "Level Failed";
            }
        }

        private void PlayTextAnimation()
        {
            if (failedTextAnimator == null)
            {
                return;
            }

            failedTextAnimator.TryInitializingOnce();
            failedTextAnimator.time.RestartTime();
            failedTextAnimator.SetVisibilityEntireText(true, true);
        }

        private static void PrepareHeart(RectTransform heart, CanvasGroup group, float startRotation)
        {
            if (heart == null)
            {
                return;
            }

            heart.gameObject.SetActive(true);
            heart.localScale = Vector3.one * 0.6f;
            heart.localRotation = Quaternion.Euler(0f, 0f, startRotation);
            if (group != null)
            {
                group.alpha = 0f;
            }
        }

        private void InsertHeartAnimation(Sequence sequence, RectTransform heart, CanvasGroup group, float delay, int direction)
        {
            if (sequence == null || heart == null)
            {
                return;
            }

            if (group != null)
            {
                sequence.Insert(delay, group.DOFade(1f, HeartPopSeconds * 0.65f));
            }

            sequence.Insert(delay, heart.DOScale(1f, HeartPopSeconds).SetEase(Ease.OutBack));
            sequence.Insert(delay, heart.DOPunchRotation(new Vector3(0f, 0f, direction * 12f), HeartPopSeconds, 6, 0.55f));
            sequence.InsertCallback(delay + HeartPopSeconds, () => StartHeartIdleRotation(heart, direction));
        }

        private void StartHeartIdleRotation(RectTransform heart, int direction)
        {
            if (heart == null)
            {
                return;
            }

            Tween tween = heart.DORotate(
                    new Vector3(0f, 0f, direction * HeartIdleRotateDegrees),
                    HeartIdleRotateSeconds)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);

            if (direction < 0)
            {
                leftHeartIdleTween = tween;
            }
            else
            {
                rightHeartIdleTween = tween;
            }
        }

        private void ResetHeart(RectTransform heart, CanvasGroup group)
        {
            if (heart == null)
            {
                return;
            }

            heart.DOKill();
            heart.localScale = Vector3.one;
            heart.localRotation = Quaternion.identity;
            if (group != null)
            {
                group.alpha = 1f;
            }

            heart.gameObject.SetActive(false);
        }

        private void KillTweens()
        {
            failSequence?.Kill();
            failSequence = null;
            leftHeartIdleTween?.Kill();
            leftHeartIdleTween = null;
            rightHeartIdleTween?.Kill();
            rightHeartIdleTween = null;

            failedPanel.DOKill();
            failedPanelCanvasGroup.DOKill();
            failedLabel.DOKill();
            failedLabelCanvasGroup.DOKill();
            brokenHeartLeft?.DOKill();
            brokenHeartLeftCanvasGroup?.DOKill();
            brokenHeartRight?.DOKill();
            brokenHeartRightCanvasGroup?.DOKill();
            retryButton.DOKill();
            retryButtonCanvasGroup.DOKill();
            extraLifeButton?.DOKill();
            extraLifeButtonCanvasGroup?.DOKill();
        }
    }
}
