using AllIn1SpringsToolkit;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// The found-cats counter: a "N/M" label that pops when a new cat is found. No spatial
    /// collection visuals here - the gathering spectacle lives entirely in
    /// <see cref="LevelCompleteBucket"/>, which only runs once, on level complete.
    /// </summary>
    public sealed class CatCounter : MonoBehaviour
    {
        private const float PopScaleImpulse = 8f;
        private const float VisibilityFadeSeconds = 0.25f;
        private const string RevealedAnimationName = "Revealed";
        private const string ExcitedAnimationName = "Excited";

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text countText;


        private Tween visibilityTween;

        private void Awake()
        {
            SetVisibleImmediate(true);
        }

        private void OnDisable()
        {
            visibilityTween?.Kill();
            visibilityTween = null;
        }

        public void Refresh(int revealedCatCount, int totalCats)
        {
            if (countText != null)
            {
                countText.text = $"{revealedCatCount}/{totalCats}";
            }
        }

        public void SetVisible(bool visible)
        {
            CanvasGroup group = ResolveCanvasGroup();
            if (group == null)
            {
                return;
            }

            visibilityTween?.Kill();
            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (!isActiveAndEnabled)
            {
                group.alpha = visible ? 1f : 0f;
                visibilityTween = null;
                return;
            }

            visibilityTween = group.DOFade(visible ? 1f : 0f, VisibilityFadeSeconds)
                .SetEase(visible ? Ease.OutSine : Ease.InOutSine)
                .SetUpdate(true)
                .OnComplete(() => visibilityTween = null);
        }

        private void SetVisibleImmediate(bool visible)
        {
            CanvasGroup group = ResolveCanvasGroup();
            if (group == null)
            {
                return;
            }

            visibilityTween?.Kill();
            visibilityTween = null;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private CanvasGroup ResolveCanvasGroup()
        {
            if (canvasGroup != null)
            {
                return canvasGroup;
            }

            if (!TryGetComponent(out canvasGroup))
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }
    }
}
