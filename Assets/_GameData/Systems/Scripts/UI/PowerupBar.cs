using System;
#if USE_AVNADS_PLUGIN
using AVN.AdsPlugin.Controllers;
#endif
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The two powerup buttons (Reveal A Cat, Hint), each with a simple available-dot/ad-icon indicator
    /// instead of a use count - the dot shows while a free use remains, the ad icon shows once exhausted
    /// and routes clicks through the rewarded-ad flow.
    /// The whole bar fades in/out via <see cref="canvasGroup"/> rather than snapping - shown only while
    /// gameplay is actually running (hidden during the tutorial lesson and while the level is failed or
    /// already solved). Raises free/rewarded request events when clicked - GameManager subscribes in
    /// Start() and owns the actual reveal/hint logic, same separation as
    /// <see cref="LevelFailedScreen"/>'s RetryRequested/ExtraLifeRequested.
    /// </summary>
    public sealed class PowerupBar : MonoBehaviour
    {
        private const float FadeSeconds = 0.25f;

        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private Button revealCatButton;
        [SerializeField] private GameObject revealCatAvailableDot;
        [SerializeField] private GameObject revealCatAdIcon;

        [SerializeField] private Button hintButton;
        [SerializeField] private GameObject hintAvailableDot;
        [SerializeField] private GameObject hintAdIcon;

        private bool targetVisible = true;
        private bool revealCatHasFreeUse = true;
        private bool hintHasFreeUse = true;
        private Tween visibilityTween;

        public event Action RevealCatRequested;
        public event Action HintRequested;
        public event Action RewardedRevealCatRequested;
        public event Action RewardedHintRequested;

        private void Awake()
        {
            if (revealCatButton != null)
            {
                revealCatButton.onClick.AddListener(() =>
                {
                    HandlePowerupClick(revealCatHasFreeUse, RevealCatRequested, RewardedRevealCatRequested);
                });
            }

            if (hintButton != null)
            {
                hintButton.onClick.AddListener(() =>
                {
                    HandlePowerupClick(hintHasFreeUse, HintRequested, RewardedHintRequested);
                });
            }
        }

        /// <summary><paramref name="active"/> is true only while normal gameplay is running - false
        /// during the tutorial lesson, a fail, or an already-solved level - and drives both the
        /// available-dot/ad-icon per button and whether the whole bar is faded in or out.</summary>
        public void Refresh(int revealCatUsesRemaining, int hintUsesRemaining, bool active)
        {
            revealCatHasFreeUse = revealCatUsesRemaining > 0;
            hintHasFreeUse = hintUsesRemaining > 0;
            SetButtonState(revealCatAvailableDot, revealCatAdIcon, revealCatUsesRemaining);
            SetButtonState(hintAvailableDot, hintAdIcon, hintUsesRemaining);
            SetVisible(active);
        }

        private static void HandlePowerupClick(bool hasFreeUse, Action freeUseRequested, Action rewardedUseRequested)
        {
            GameHaptics.Selection();
            SoundManager.PlaySound(SFX.ButtonClick);

            if (hasFreeUse)
            {
                freeUseRequested?.Invoke();
                return;
            }

#if USE_AVNADS_PLUGIN
            AVNPlugin.DTInstance?.ShowRewardedAd(() => rewardedUseRequested?.Invoke());
#else
            rewardedUseRequested?.Invoke();
#endif
        }

        private static void SetButtonState(GameObject availableDot, GameObject adIcon, int usesRemaining)
        {
            bool hasUses = usesRemaining > 0;
            if (availableDot != null)
            {
                availableDot.SetActive(hasUses);
            }

            if (adIcon != null)
            {
                adIcon.SetActive(!hasUses);
            }
        }

        /// <summary>
        /// Fades the whole bar in/out - used both by <see cref="Refresh"/>'s per-action active/inactive
        /// toggle and GameManager's lesson-only hard hide (the lesson never reaches Refresh's normal
        /// branch at all, so it calls this directly). Idempotent: a call matching the current target is a
        /// no-op, so Refresh calling this on every single action doesn't restart the tween or thrash
        /// blocksRaycasts. Assumes the bar starts authored fully visible in the Editor.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (targetVisible == visible)
            {
                return;
            }

            targetVisible = visible;
            visibilityTween?.Kill();

            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.blocksRaycasts = visible;

            if (visible)
            {
                gameObject.SetActive(true);
                visibilityTween = canvasGroup.DOFade(1f, FadeSeconds).SetEase(Ease.OutSine).SetUpdate(true);
            }
            else
            {
                visibilityTween = canvasGroup.DOFade(0f, FadeSeconds).SetEase(Ease.InOutSine).SetUpdate(true)
                    .OnComplete(() => gameObject.SetActive(false));
            }
        }
    }
}
