using System;
using AllIn1SpringsToolkit;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The win popup: the bucket reveals its cat slots first (<see cref="LevelCompleteBucket"/>),
    /// then the panel/label/button pop in, a Text Animator-driven "Level Complete" message, a
    /// star rating that pops in after the screen fade completes, on top of its
    /// (always-visible, Editor-authored) black bases -
    /// one star per heart remaining - and a VFX burst (a plain <see cref="ParticleSystem"/>
    /// reference - no procedural VFX here) all play once the bucket sequence finishes. The next
    /// button settles into a subtle idle pulse once it's fully faded in, so it keeps drawing the
    /// eye without being distracting. The first tutorial completion also offers a secondary
    /// "Play Game" action. GameManager subscribes to both actions in Start(), rather than this
    /// class knowing anything about level progression.
    /// </summary>
    public sealed class LevelCompleteScreen : MonoBehaviour
    {
        private const float PopSeconds = 0.34f;
        private const float StaggerSeconds = 0.08f;
        private const float LabelWobbleDegrees = 10f;

        private const float StarPopSeconds = 0.3f;
        private const float StarStaggerSeconds = 0.1f;

        private const float NextButtonPulseSeconds = 0.6f;
        private const float OutroSeconds = 0.28f;

        [SerializeField] private LevelCompleteBucket bucket;

        [Tooltip("Full-screen dim behind the panel - fades 0→1 while the screen shows, fades back to 0 during the outro when it hides. The panel itself only pops in, it does not fade.")]
        [SerializeField] private CanvasGroup bgCanvasGroup;

        [SerializeField] private RectTransform winPanel;
        [SerializeField] private RectTransform winLabel;
        [SerializeField] private CanvasGroup winLabelCanvasGroup;
        [SerializeField] private TypewriterComponent winTypewriter;
        [SerializeField] private RectTransform nextButton;
        [SerializeField] private CanvasGroup nextButtonCanvasGroup;
        [Tooltip("Optional - localized \"Next\" button label. Set from LocalizationService each time the panel shows.")]
        [SerializeField] private TMP_Text nextButtonText;
        [Tooltip("Optional scene-authored secondary action. If omitted, the Next button is cloned once at startup so the tutorial choice is still available.")]
        [SerializeField] private RectTransform secondaryButton;
        [SerializeField] private CanvasGroup secondaryButtonCanvasGroup;
        [SerializeField] private TMP_Text secondaryButtonText;
        [SerializeField] private TransformSpringComponent winPanelSpring;
        [SerializeField] private TransformSpringComponent nextButtonSpring;

        [Tooltip("The star FILL images only - the black star bases are always-visible Editor art behind these and need no code.")]
        [SerializeField] private Image[] starFillImages;

        [Tooltip("Optional - a random encouragement line, re-picked from LocalizationService's comments pool each time the panel shows.")]
        [SerializeField] private TMP_Text commentText;
        [SerializeField] private CanvasGroup commentCanvasGroup;

        [SerializeField] private ParticleSystem winVfx;

        [Header("Spring Tuning")]
        [SerializeField] private float panelPopScaleImpulse = 6.5f;
        [SerializeField] private float nextButtonPopScaleImpulse = 5.5f;
        [SerializeField] private float nextButtonPulseScaleImpulse = 1.8f;

        private bool isShowing;
        private bool showTutorialChoice;
        private int pendingStarsEarned;
        private Sequence panelSequence;
        private Sequence starsSequence;
        private Sequence outroSequence;
        private Tween nextButtonPulseTween;

        public event Action NextRequested;
        public event Action PlayGameRequested;

        private void Awake()
        {
            ResolveSecondaryButton();
            ResolveSprings();

            if (nextButton != null && nextButton.TryGetComponent(out Button button))
            {
                button.onClick.AddListener(() =>
                {
                    if (!isShowing)
                    {
                        return;
                    }

                    GameHaptics.Selection();
                    SoundManager.PlaySound(SFX.ButtonClick);
                    PlayOutroAnimation(() => NextRequested?.Invoke());
                });
            }

            if (secondaryButton != null && secondaryButton.TryGetComponent(out Button secondaryAction))
            {
                secondaryAction.onClick.AddListener(() =>
                {
                    if (!isShowing || !showTutorialChoice)
                    {
                        return;
                    }

                    GameHaptics.Selection();
                    SoundManager.PlaySound(SFX.ButtonClick);
                    PlayOutroAnimation(() => PlayGameRequested?.Invoke());
                });
            }

            if (winTypewriter != null)
            {
                winTypewriter.onTextShowed.AddListener(PlayWinVfx);
            }
        }

        public void ShowWinAnimation(
            Level level,
            ValidationResult validation,
            bool offerTutorialChoice = false)
        {
            if (isShowing)
            {
                return;
            }

            isShowing = true;
            showTutorialChoice = offerTutorialChoice;
            pendingStarsEarned = validation.HeartsRemaining;

            DOVirtual.DelayedCall(0.75f, () =>
            {
                if (bucket != null)
                {
                    bucket.PlayGatherCats(PlayPanelSequence);
                }
                else
                {
                    PlayPanelSequence();
                }
            }, false).SetUpdate(true);
        }

        private void PlayPanelSequence()
        {
            // BG Canvas is Win Panel's parent in the hierarchy, not a sibling - Win Panel staying
            // inactive despite its own SetActive(true) below, with the fade/pop tweens never visibly
            // doing anything, is what an inactive ancestor looks like from here.
            if (bgCanvasGroup != null)
            {
                bgCanvasGroup.gameObject.SetActive(true);
            }

            winPanel.gameObject.SetActive(true);

            KillPanelTweens();

            if (winTypewriter != null)
            {
                winTypewriter.StopShowingText();
            }

            ConfigureActionButtons();

            if (commentText != null)
            {
                commentText.text = LocalizationService.GetRandomFromPool("levelComplete.comments");
            }

            SnapSpring(winPanelSpring, winPanel, winPanel.localPosition, Vector3.one * 0.84f, Quaternion.identity);
            if (bgCanvasGroup != null)
            {
                bgCanvasGroup.alpha = 0f;
            }
            winLabel.localScale = Vector3.one * 0.84f;
            winLabel.localRotation = Quaternion.identity;
            winLabelCanvasGroup.alpha = 0f;
            if (commentCanvasGroup != null)
            {
                commentCanvasGroup.alpha = 0f;
            }
            SnapSpring(nextButtonSpring, nextButton, nextButton.localPosition, Vector3.one * 0.84f, Quaternion.identity);
            nextButtonCanvasGroup.alpha = 0f;
            if (showTutorialChoice && secondaryButton != null)
            {
                secondaryButtonCanvasGroup.alpha = 0f;
            }

            panelSequence = DOTween.Sequence().SetUpdate(true);
            if (bgCanvasGroup != null)
            {
                panelSequence.Insert(0f, bgCanvasGroup.DOFade(1f, PopSeconds));
            }
            panelSequence.InsertCallback(0f, () => PopScale(winPanelSpring, Vector3.one, panelPopScaleImpulse));

            // Let the complete screen become fully visible before the earned fills start popping.
            panelSequence.InsertCallback(
                PopSeconds + StaggerSeconds,
                () => PlayStarsAnimation(pendingStarsEarned));

            panelSequence.Insert(StaggerSeconds, winLabelCanvasGroup.DOFade(1f, PopSeconds * 0.6f));
            panelSequence.Insert(StaggerSeconds, winLabel.DOScale(1f, PopSeconds * 0.6f).SetEase(Ease.OutBack));
            panelSequence.Insert(StaggerSeconds, winLabel.DOPunchRotation(
                new Vector3(0f, 0f, LabelWobbleDegrees),
                PopSeconds * 1.4f,
                6,
                0.7f));
            if (commentCanvasGroup != null)
            {
                panelSequence.Insert(StaggerSeconds, commentCanvasGroup.DOFade(1f, PopSeconds * 0.6f));
            }
            // Start the typewriter exactly when the label becomes visible - calling ShowText()
            // any earlier lets its reveal animation run (and finish) while alpha is still 0,
            // so it looks static once it fades in.
            panelSequence.InsertCallback(StaggerSeconds, () =>
            {
                if (winTypewriter != null)
                {
                    winTypewriter.ShowText(BuildWinMessage());
                }
            });

            float nextDelay = StaggerSeconds * 2f;
            panelSequence.Insert(nextDelay, nextButtonCanvasGroup.DOFade(1f, PopSeconds * 0.5f));
            panelSequence.InsertCallback(nextDelay, () => PopScale(nextButtonSpring, Vector3.one, nextButtonPopScaleImpulse));
            if (showTutorialChoice && secondaryButton != null)
            {
                panelSequence.Insert(nextDelay, secondaryButtonCanvasGroup.DOFade(1f, PopSeconds * 0.5f));
            }
            panelSequence.InsertCallback(nextDelay + (PopSeconds * 0.5f), PlayNextButtonIdlePulse);
        }

        private void ConfigureActionButtons()
        {
            if (nextButtonText != null)
            {
                nextButtonText.text = LocalizationService.Get(
                    showTutorialChoice
                        ? "levelComplete.continueTutorial"
                        : "levelComplete.next");
            }

            if (secondaryButton == null)
            {
                showTutorialChoice = false;
                return;
            }

            secondaryButton.gameObject.SetActive(showTutorialChoice);
            if (!showTutorialChoice)
            {
                return;
            }

            if (secondaryButtonText != null)
            {
                secondaryButtonText.text = LocalizationService.Get("levelComplete.playGame");
            }
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

        /// <summary>
        /// Fades the panel/label/stars/next button out while the bucket drops back below the
        /// screen, then invokes onComplete. GameManager advances to the next level from there,
        /// which calls <see cref="Hide"/> to do the (by-then invisible) instant reset.
        /// </summary>
        private void PlayOutroAnimation(Action onComplete)
        {
            if (!isShowing)
            {
                onComplete?.Invoke();
                return;
            }

            isShowing = false;

            panelSequence?.Kill();
            panelSequence = null;
            nextButtonPulseTween?.Kill();
            nextButtonPulseTween = null;

            if (winTypewriter != null)
            {
                winTypewriter.StopShowingText();
            }

            outroSequence?.Kill();
            outroSequence = DOTween.Sequence().SetUpdate(true);
            outroSequence.Join(winLabelCanvasGroup.DOFade(0f, OutroSeconds));
            outroSequence.Join(nextButtonCanvasGroup.DOFade(0f, OutroSeconds));
            if (secondaryButton != null && secondaryButton.gameObject.activeSelf)
            {
                outroSequence.Join(secondaryButtonCanvasGroup.DOFade(0f, OutroSeconds));
            }

            if (bgCanvasGroup != null)
            {
                outroSequence.Join(bgCanvasGroup.DOFade(0f, OutroSeconds));
            }

            if (commentCanvasGroup != null)
            {
                outroSequence.Join(commentCanvasGroup.DOFade(0f, OutroSeconds));
            }

            if (starFillImages != null)
            {
                foreach (Image fill in starFillImages)
                {
                    if (fill != null && fill.gameObject.activeSelf)
                    {
                        outroSequence.Join(fill.DOFade(0f, OutroSeconds));
                    }
                }
            }

            outroSequence.OnComplete(() =>
            {
                outroSequence = null;
                onComplete?.Invoke();
            });

            bucket?.PlayReturnAnimation();
        }

        public void Hide()
        {
            isShowing = false;

            outroSequence?.Kill();
            outroSequence = null;

            KillPanelTweens();

            winPanel.gameObject.SetActive(false);

            SnapSpring(winPanelSpring, winPanel, winPanel.localPosition, Vector3.one, Quaternion.identity);
            if (bgCanvasGroup != null)
            {
                bgCanvasGroup.alpha = 0f;
                bgCanvasGroup.gameObject.SetActive(false);
            }
            winLabel.localScale = Vector3.one;
            winLabel.localRotation = Quaternion.identity;
            winLabelCanvasGroup.alpha = 1f;
            if (commentCanvasGroup != null)
            {
                commentCanvasGroup.alpha = 1f;
            }
            SnapSpring(nextButtonSpring, nextButton, nextButton.localPosition, Vector3.one, Quaternion.identity);
            nextButtonCanvasGroup.alpha = 1f;
            if (secondaryButton != null)
            {
                secondaryButtonCanvasGroup.alpha = 1f;
                secondaryButton.gameObject.SetActive(false);
            }
            showTutorialChoice = false;

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
            if (bgCanvasGroup != null)
            {
                bgCanvasGroup.DOKill();
            }
            winLabel.DOKill();
            winLabelCanvasGroup.DOKill();
            nextButton.DOKill();
            nextButtonCanvasGroup.DOKill();
            secondaryButton?.DOKill();
            secondaryButtonCanvasGroup?.DOKill();
            commentCanvasGroup?.DOKill();
        }

        private string BuildWinMessage()
        {
            return $"<bounce>{{wave}}{LocalizationService.Get("levelComplete.text")}{{/wave}}</bounce>";
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

        private void ResolveSecondaryButton()
        {
            if (secondaryButton == null && nextButton != null)
            {
                secondaryButton = Instantiate(nextButton, nextButton.parent);
                secondaryButton.name = "Play Game button";
            }

            if (secondaryButton == null)
            {
                return;
            }

            if (secondaryButtonCanvasGroup == null)
            {
                secondaryButtonCanvasGroup = secondaryButton.GetComponent<CanvasGroup>();
                if (secondaryButtonCanvasGroup == null)
                {
                    secondaryButtonCanvasGroup = secondaryButton.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (secondaryButtonText == null)
            {
                secondaryButtonText = secondaryButton.GetComponentInChildren<TMP_Text>(true);
            }

            secondaryButton.gameObject.SetActive(false);
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
