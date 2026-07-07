using System;
using AllIn1SpringsToolkit;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The win popup: the bucket reveals its cat slots first (<see cref="LevelCompleteBucket"/>),
    /// then the panel/label/button pop in, a Text Animator-driven "Level Complete" message, a
    /// star rating that pops in on top of its (always-visible, Editor-authored) black bases -
    /// one star per heart remaining - and a VFX burst (a plain <see cref="ParticleSystem"/>
    /// reference - no procedural VFX here) all play once the bucket sequence finishes. The next
    /// button settles into a subtle idle pulse once it's fully faded in, so it keeps drawing the
    /// eye without being distracting. Raises <see cref="NextRequested"/> when the next button is
    /// clicked - GameManager subscribes to that in Start() rather than this class knowing
    /// anything about level progression.
    /// </summary>
    public sealed class LevelCompleteScreen : MonoBehaviour
    {
        private const float PopSeconds = 0.34f;
        private const float StaggerSeconds = 0.08f;
        private const float LabelWobbleDegrees = 10f;

        private const float StarPopSeconds = 0.3f;
        private const float StarStaggerSeconds = 0.1f;

        private const float NextButtonPulseSeconds = 0.6f;

        [SerializeField] private LevelCompleteBucket bucket;

        [SerializeField] private RectTransform winPanel;
        [SerializeField] private CanvasGroup winPanelCanvasGroup;
        [SerializeField] private RectTransform winLabel;
        [SerializeField] private CanvasGroup winLabelCanvasGroup;
        [SerializeField] private TypewriterComponent winTypewriter;
        [SerializeField] private RectTransform nextButton;
        [SerializeField] private CanvasGroup nextButtonCanvasGroup;
        [SerializeField] private TransformSpringComponent winPanelSpring;
        [SerializeField] private TransformSpringComponent nextButtonSpring;

        [Tooltip("The star FILL images only - the black star bases are always-visible Editor art behind these and need no code.")]
        [SerializeField] private Image[] starFillImages;

        [SerializeField] private ParticleSystem winVfx;

        [Header("Spring Tuning")]
        [SerializeField] private float panelPopScaleImpulse = 6.5f;
        [SerializeField] private float nextButtonPopScaleImpulse = 5.5f;
        [SerializeField] private float nextButtonPulseScaleImpulse = 1.8f;

        private bool isShowing;
        private int pendingStarsEarned;
        private Sequence panelSequence;
        private Sequence starsSequence;
        private Tween nextButtonPulseTween;

        public event Action NextRequested;

        private void Awake()
        {
            ResolveSprings();

            if (nextButton != null && nextButton.TryGetComponent(out Button button))
            {
                button.onClick.AddListener(() =>
                {
                    GameHaptics.Selection();
                    SoundManager.PlaySound(SFX.ButtonClick);
                    NextRequested?.Invoke();
                });
            }

            if (winTypewriter != null)
            {
                winTypewriter.onTextShowed.AddListener(PlayWinVfx);
                winTypewriter.onTextShowed.AddListener(() => PlayStarsAnimation(pendingStarsEarned));
            }
        }

        public void ShowWinAnimation(Level level, ValidationResult validation)
        {
            if (isShowing)
            {
                return;
            }

            isShowing = true;
            pendingStarsEarned = validation.HeartsRemaining;

            if (bucket != null)
            {
                bucket.PlayGatherCats(PlayPanelSequence);
            }
            else
            {
                PlayPanelSequence();
            }
        }

        private void PlayPanelSequence()
        {
            winPanel.gameObject.SetActive(true);

            KillPanelTweens();

            if (winTypewriter != null)
            {
                winTypewriter.ShowText(BuildWinMessage());
            }

            SnapSpring(winPanelSpring, winPanel, winPanel.localPosition, Vector3.one * 0.84f, Quaternion.identity);
            winPanelCanvasGroup.alpha = 0f;
            winLabel.localScale = Vector3.one * 0.84f;
            winLabel.localRotation = Quaternion.identity;
            winLabelCanvasGroup.alpha = 0f;
            SnapSpring(nextButtonSpring, nextButton, nextButton.localPosition, Vector3.one * 0.84f, Quaternion.identity);
            nextButtonCanvasGroup.alpha = 0f;

            panelSequence = DOTween.Sequence().SetUpdate(true);
            panelSequence.Join(winPanelCanvasGroup.DOFade(1f, PopSeconds));
            panelSequence.InsertCallback(0f, () => PopScale(winPanelSpring, Vector3.one, panelPopScaleImpulse));

            panelSequence.Insert(StaggerSeconds, winLabelCanvasGroup.DOFade(1f, PopSeconds * 0.6f));
            panelSequence.Insert(StaggerSeconds, winLabel.DOScale(1f, PopSeconds * 0.6f).SetEase(Ease.OutBack));
            panelSequence.Insert(StaggerSeconds, winLabel.DOPunchRotation(
                new Vector3(0f, 0f, LabelWobbleDegrees),
                PopSeconds * 1.4f,
                6,
                0.7f));

            float nextDelay = StaggerSeconds * 2f;
            panelSequence.Insert(nextDelay, nextButtonCanvasGroup.DOFade(1f, PopSeconds * 0.5f));
            panelSequence.InsertCallback(nextDelay, () => PopScale(nextButtonSpring, Vector3.one, nextButtonPopScaleImpulse));
            panelSequence.InsertCallback(nextDelay + (PopSeconds * 0.5f), PlayNextButtonIdlePulse);
        }

        private void PlayNextButtonIdlePulse()
        {
            nextButtonPulseTween?.Kill();
            nextButtonPulseTween = DOVirtual.DelayedCall(NextButtonPulseSeconds, () =>
            {
                if (!isShowing)
                {
                    return;
                }

                nextButtonSpring?.AddVelocityScale(Vector3.one * nextButtonPulseScaleImpulse);
                PlayNextButtonIdlePulse();
            }, false).SetUpdate(true);
        }

        public void Hide()
        {
            isShowing = false;

            KillPanelTweens();

            winPanel.gameObject.SetActive(false);

            SnapSpring(winPanelSpring, winPanel, winPanel.localPosition, Vector3.one, Quaternion.identity);
            winPanelCanvasGroup.alpha = 1f;
            winLabel.localScale = Vector3.one;
            winLabel.localRotation = Quaternion.identity;
            winLabelCanvasGroup.alpha = 1f;
            SnapSpring(nextButtonSpring, nextButton, nextButton.localPosition, Vector3.one, Quaternion.identity);
            nextButtonCanvasGroup.alpha = 1f;

            if (winTypewriter != null)
            {
                winTypewriter.StopShowingText();
            }

            StopWinVfx();
            ResetStars();

            if (bucket != null)
            {
                bucket.Hide();
            }
        }

        private void KillPanelTweens()
        {
            panelSequence?.Kill();
            panelSequence = null;
            nextButtonPulseTween?.Kill();
            nextButtonPulseTween = null;

            winPanel.DOKill();
            winPanelCanvasGroup.DOKill();
            winLabel.DOKill();
            winLabelCanvasGroup.DOKill();
            nextButton.DOKill();
            nextButtonCanvasGroup.DOKill();
        }

        private string BuildWinMessage()
        {
            return "{bounce}Level Complete!{/bounce}";
        }

        // ---- star rating ----

        private void PlayStarsAnimation(int starsEarned)
        {
            if (starFillImages == null)
            {
                return;
            }

            starsSequence?.Kill();
            starsSequence = DOTween.Sequence().SetUpdate(true);

            for (int i = 0; i < starFillImages.Length; i++)
            {
                Image fill = starFillImages[i];
                if (fill == null)
                {
                    continue;
                }

                RectTransform rect = fill.rectTransform;
                rect.DOKill();
                fill.DOKill();

                bool earned = i < starsEarned;
                fill.gameObject.SetActive(earned);
                if (!earned)
                {
                    continue;
                }

                rect.localScale = Vector3.zero;
                fill.color = new Color(fill.color.r, fill.color.g, fill.color.b, 0f);

                float delay = i * StarStaggerSeconds;
                starsSequence.Insert(delay, rect.DOScale(1f, StarPopSeconds).SetEase(Ease.OutBack));
                starsSequence.Insert(delay, fill.DOFade(1f, StarPopSeconds));
                starsSequence.InsertCallback(delay, () => SoundManager.PlaySound(SFX.StarPop));
            }
        }

        private void ResetStars()
        {
            starsSequence?.Kill();
            starsSequence = null;

            if (starFillImages == null)
            {
                return;
            }

            foreach (Image fill in starFillImages)
            {
                if (fill == null)
                {
                    continue;
                }

                fill.rectTransform.DOKill();
                fill.DOKill();
                fill.rectTransform.localScale = Vector3.zero;
                fill.gameObject.SetActive(false);
            }
        }

        // ---- springs ----

        private void ResolveSprings()
        {
            winPanelSpring = ResolveSpring(winPanel, winPanelSpring, 150f, 11f);
            nextButtonSpring = ResolveSpring(nextButton, nextButtonSpring, 145f, 12f);
        }

        private static TransformSpringComponent ResolveSpring(RectTransform rect, TransformSpringComponent spring, float force, float drag)
        {
            if (rect == null)
            {
                return spring;
            }

            bool created = false;
            if (spring == null && !rect.TryGetComponent(out spring))
            {
                spring = rect.gameObject.AddComponent<TransformSpringComponent>();
                created = true;
            }

            if (spring == null)
            {
                return null;
            }

            spring.followerTransform = rect;
            spring.spaceType = TransformSpringComponent.SpaceType.LocalSpace;
            spring.useScaledTime = false;
            spring.SetUnifiedForceAndDragPosition(force, drag);
            spring.SetUnifiedForceAndDragScale(force, drag);
            spring.SetUnifiedForceAndDragRotation(force, drag);

            if (created || !spring.doesAutoInitialize)
            {
                spring.Initialize();
            }

            return spring;
        }

        private static void SnapSpring(
            TransformSpringComponent spring,
            RectTransform rect,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation)
        {
            if (rect == null)
            {
                return;
            }

            rect.localPosition = localPosition;
            rect.localScale = localScale;
            rect.localRotation = localRotation;

            if (spring == null)
            {
                return;
            }

            spring.SetCurrentValuePosition(localPosition);
            spring.SetTargetPosition(localPosition);
            spring.SetVelocityPosition(Vector3.zero);
            spring.SetCurrentValueScale(localScale);
            spring.SetTargetScale(localScale);
            spring.SetVelocityScale(Vector3.zero);
            spring.SetCurrentValueRotation(localRotation);
            spring.SetTargetRotation(localRotation);
            spring.SetVelocityRotation(Vector3.zero);
        }

        private static void PopScale(TransformSpringComponent spring, Vector3 targetScale, float impulse)
        {
            if (spring == null)
            {
                return;
            }

            spring.SetTargetScale(targetScale);
            spring.AddVelocityScale(Vector3.one * impulse);
        }

        // ---- VFX ----

        private void PlayWinVfx()
        {
            if (winVfx != null)
            {
                winVfx.Play();
            }

            SoundManager.PlaySound(SFX.LevelComplete);
            GameHaptics.Success();
        }

        private void StopWinVfx()
        {
            if (winVfx != null)
            {
                winVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
