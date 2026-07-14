using System;
using AVN.AdsPlugin.Controllers;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        private const float OpenSeconds = 0.25f;
        private const float CloseSeconds = 0.28f;
        private const float BackgroundAlpha = 1f;
        private const float StartScale = 0.7f;
        private const float TogglePopScale = 1.1f;
        private const float TogglePopSeconds = 0.12f;
        private const float CloseDropPixels = 900f;

        [Serializable]
        private sealed class ToggleButton
        {
            public Button button = null;
            public Image image = null;
            public RectTransform animatedRoot = null;

            public RectTransform AnimatedRoot => animatedRoot != null
                ? animatedRoot
                : button != null ? button.transform as RectTransform : null;

            public void SetVisual(bool enabled, Sprite onSprite, Sprite offSprite)
            {
                if (image == null)
                {
                    return;
                }

                Sprite sprite = enabled ? onSprite : offSprite;
                if (sprite != null)
                {
                    image.sprite = sprite;
                }
            }

            public void PlayToggleAnimation()
            {
                RectTransform root = AnimatedRoot;
                if (root == null)
                {
                    return;
                }

                root.DOKill();
                root.localScale = Vector3.one;
                Sequence sequence = DOTween.Sequence().SetUpdate(true);
                sequence.Append(root.DOScale(TogglePopScale, TogglePopSeconds).SetEase(Ease.OutBack));
                sequence.Append(root.DOScale(1f, TogglePopSeconds).SetEase(Ease.InOutSine));
            }
        }

        [Header("Popup")]
        [SerializeField] private CanvasGroup backgroundCanvasGroup;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Button resumeButton;

        [Header("Shared Toggle Sprites")]
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;

        [Header("Toggles")]
        [SerializeField] private ToggleButton musicToggle;
        [SerializeField] private ToggleButton sfxToggle;
        [SerializeField] private ToggleButton hapticsToggle;

        private Vector2 panelRestAnchoredPosition;
        private Sequence panelSequence;
        private bool isOpen;

        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRestAnchoredPosition = panelRoot.anchoredPosition;
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(CloseSettings);
            }

            WireToggle(musicToggle, () => GameSettings.MusicEnabled, value => GameSettings.MusicEnabled = value);
            WireToggle(sfxToggle, () => GameSettings.SfxEnabled, value => GameSettings.SfxEnabled = value);
            WireToggle(hapticsToggle, () => GameSettings.HapticsEnabled, value => GameSettings.HapticsEnabled = value);

            RefreshToggleVisuals();
            HideImmediate();
        }

        public void OpenSettings()
        {
            if (isOpen || panelRoot == null)
            {
                return;
            }

            isOpen = true;
            RefreshToggleVisuals();
            gameObject.SetActive(true);
            AVNPlugin.DTInstance.ShowBannerAd(AVN.AdsPlugin.BannerAdTypes.MREC);
            if (backgroundCanvasGroup != null)
            {
                backgroundCanvasGroup.gameObject.SetActive(true);
                backgroundCanvasGroup.alpha = 0f;
                backgroundCanvasGroup.blocksRaycasts = true;
            }

            panelRoot.gameObject.SetActive(true);
            panelRoot.anchoredPosition = panelRestAnchoredPosition;
            panelRoot.localScale = Vector3.one * StartScale;

            panelSequence?.Kill();
            panelSequence = DOTween.Sequence().SetUpdate(true);
            if (backgroundCanvasGroup != null)
            {
                panelSequence.Join(backgroundCanvasGroup.DOFade(BackgroundAlpha, OpenSeconds).SetEase(Ease.OutSine));
            }



            panelSequence.Join(panelRoot.DOScale(1f, OpenSeconds).SetEase(Ease.OutBack));
        }

        public void CloseSettings()
        {
            if (!isOpen || panelRoot == null)
            {
                return;
            }

            isOpen = false;
            GameHaptics.Selection();
            SoundManager.PlaySound(SFX.ButtonClick);

            panelSequence?.Kill();
            panelSequence = DOTween.Sequence().SetUpdate(true);
            panelSequence.Join(panelRoot.DOAnchorPos(panelRestAnchoredPosition + Vector2.down * CloseDropPixels, CloseSeconds).SetEase(Ease.InBack));

            if (backgroundCanvasGroup != null)
            {
                backgroundCanvasGroup.blocksRaycasts = false;
                panelSequence.Join(backgroundCanvasGroup.DOFade(0f, CloseSeconds).SetEase(Ease.InOutSine));
            }
            AVNPlugin.DTInstance.HideBannerAd(AVN.AdsPlugin.BannerAdTypes.MREC);

            panelSequence.OnComplete(HideImmediate);
        }

        private void WireToggle(ToggleButton toggle, Func<bool> getValue, Action<bool> setValue)
        {
            if (toggle?.button == null)
            {
                return;
            }

            toggle.button.onClick.AddListener(() =>
            {
                bool nextValue = !getValue();
                GameHaptics.Selection();
                SoundManager.PlaySound(SFX.ButtonClick);
                setValue(nextValue);
                toggle.SetVisual(nextValue, onSprite, offSprite);
                toggle.PlayToggleAnimation();
            });
        }

        private void RefreshToggleVisuals()
        {
            musicToggle?.SetVisual(GameSettings.MusicEnabled, onSprite, offSprite);
            sfxToggle?.SetVisual(GameSettings.SfxEnabled, onSprite, offSprite);
            hapticsToggle?.SetVisual(GameSettings.HapticsEnabled, onSprite, offSprite);
        }

        private void HideImmediate()
        {
            panelSequence?.Kill();
            panelSequence = null;

            if (backgroundCanvasGroup != null)
            {
                backgroundCanvasGroup.alpha = 0f;
                backgroundCanvasGroup.blocksRaycasts = false;
                backgroundCanvasGroup.gameObject.SetActive(false);
            }

            if (panelRoot != null)
            {
                panelRoot.anchoredPosition = panelRestAnchoredPosition;
                panelRoot.localScale = Vector3.one;
                panelRoot.gameObject.SetActive(false);
            }


        }
    }
}
