using System;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The out-of-hearts popup: panel/label/button pop-in with a Text Animator-driven
    /// message. Raises <see cref="RetryRequested"/> when the retry button is clicked -
    /// GameManager subscribes to that in Start() rather than this class knowing anything
    /// about level restarting.
    /// </summary>
    public sealed class LevelFailedScreen : MonoBehaviour
    {
        private const float PopSeconds = 0.34f;
        private const float StaggerSeconds = 0.08f;
        private const float LabelWobbleDegrees = 8f;

        [SerializeField] private RectTransform failedPanel;
        [SerializeField] private CanvasGroup failedPanelCanvasGroup;
        [SerializeField] private RectTransform failedLabel;
        [SerializeField] private CanvasGroup failedLabelCanvasGroup;
        [SerializeField] private TypewriterComponent failedTypewriter;
        [SerializeField] private RectTransform retryButton;
        [SerializeField] private CanvasGroup retryButtonCanvasGroup;

        public event Action RetryRequested;

        private void Awake()
        {
            if (retryButton != null && retryButton.TryGetComponent(out Button button))
            {
                button.onClick.AddListener(() =>
                {
                    GameHaptics.Selection();
                    SoundManager.PlaySound(SFX.ButtonClick);
                    RetryRequested?.Invoke();
                });
            }
        }

        public void ShowFailAnimation()
        {
            bool wasVisible = failedPanel.gameObject.activeSelf;
            failedPanel.gameObject.SetActive(true);
            if (wasVisible)
            {
                return;
            }

            failedPanel.DOKill();
            failedPanelCanvasGroup.DOKill();
            failedLabel.DOKill();
            failedLabelCanvasGroup.DOKill();
            retryButton.DOKill();
            retryButtonCanvasGroup.DOKill();

            if (failedTypewriter != null)
            {
                failedTypewriter.ShowText(BuildFailMessage());
            }

            SoundManager.PlaySound(SFX.LevelFailed);
            GameHaptics.Failure();

            failedPanel.localScale = Vector3.one * 0.84f;
            failedPanelCanvasGroup.alpha = 0f;
            failedLabel.localScale = Vector3.one * 0.84f;
            failedLabel.localRotation = Quaternion.identity;
            failedLabelCanvasGroup.alpha = 0f;
            retryButton.localScale = Vector3.one * 0.84f;
            retryButtonCanvasGroup.alpha = 0f;

            failedPanelCanvasGroup.DOFade(1f, PopSeconds).SetUpdate(true);
            failedPanel.DOScale(1f, PopSeconds).SetEase(Ease.OutBack).SetUpdate(true);

            failedLabelCanvasGroup.DOFade(1f, PopSeconds * 0.6f).SetDelay(StaggerSeconds).SetUpdate(true);
            failedLabel.DOScale(1f, PopSeconds * 0.6f).SetEase(Ease.OutBack).SetDelay(StaggerSeconds).SetUpdate(true);
            failedLabel.DOPunchRotation(new Vector3(0f, 0f, LabelWobbleDegrees), PopSeconds * 1.4f, 6, 0.7f)
                .SetDelay(StaggerSeconds).SetUpdate(true);

            retryButtonCanvasGroup.DOFade(1f, PopSeconds * 0.5f).SetDelay(StaggerSeconds * 2f).SetUpdate(true);
            retryButton.DOScale(1f, PopSeconds * 0.5f).SetEase(Ease.OutBack).SetDelay(StaggerSeconds * 2f).SetUpdate(true);
        }

        public void Hide()
        {
            failedPanel.DOKill();
            failedPanelCanvasGroup.DOKill();
            failedLabel.DOKill();
            failedLabelCanvasGroup.DOKill();
            retryButton.DOKill();
            retryButtonCanvasGroup.DOKill();

            failedPanel.gameObject.SetActive(false);

            failedPanel.localScale = Vector3.one;
            failedPanelCanvasGroup.alpha = 1f;
            failedLabel.localScale = Vector3.one;
            failedLabel.localRotation = Quaternion.identity;
            failedLabelCanvasGroup.alpha = 1f;
            retryButton.localScale = Vector3.one;
            retryButtonCanvasGroup.alpha = 1f;

            if (failedTypewriter != null)
            {
                failedTypewriter.StopShowingText();
            }
        }

        private string BuildFailMessage()
        {
            return "{shake}Out of Hearts!{/shake}\n<fade>Undo a move or retry the level.</fade>";
        }
    }
}
