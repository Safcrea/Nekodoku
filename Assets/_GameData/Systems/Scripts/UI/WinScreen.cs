using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    public sealed class WinScreen : MonoBehaviour
    {
        private const float PopSeconds = 0.34f;
        private const float StaggerSeconds = 0.08f;

        [SerializeField] private RectTransform winPanel;
        [SerializeField] private CanvasGroup winPanelCanvasGroup;
        [SerializeField] private RectTransform winLabel;
        [SerializeField] private CanvasGroup winLabelCanvasGroup;
        [SerializeField] private Text winWordText;
        [SerializeField] private Text winSubtitleText;
        [SerializeField] private Button nextBtn;
        [SerializeField] private RectTransform nextButton;
        [SerializeField] private CanvasGroup nextButtonCanvasGroup;

        public void ShowWinAnimation(Level level, ValidationResult validation)
        {
            winWordText.text = level.TargetWord;
            winSubtitleText.text = StarLabel(validation.MistakeCount);

            bool wasVisible = winPanel.gameObject.activeSelf;
            winPanel.gameObject.SetActive(true);
            if (wasVisible)
            {
                return;
            }

            winPanel.DOKill();
            winPanelCanvasGroup.DOKill();
            winLabel.DOKill();
            winLabelCanvasGroup.DOKill();
            nextButton.DOKill();
            nextButtonCanvasGroup.DOKill();

            winPanel.localScale = Vector3.one * 0.84f;
            winPanelCanvasGroup.alpha = 0f;
            winLabel.localScale = Vector3.one * 0.84f;
            winLabelCanvasGroup.alpha = 0f;
            nextButton.localScale = Vector3.one * 0.84f;
            nextButtonCanvasGroup.alpha = 0f;

            winPanelCanvasGroup.DOFade(1f, PopSeconds).SetUpdate(true);
            winPanel.DOScale(1f, PopSeconds).SetEase(Ease.OutBack).SetUpdate(true);

            winLabelCanvasGroup.DOFade(1f, PopSeconds * 0.6f).SetDelay(StaggerSeconds).SetUpdate(true);
            winLabel.DOScale(1f, PopSeconds * 0.6f).SetEase(Ease.OutBack).SetDelay(StaggerSeconds).SetUpdate(true);

            nextButtonCanvasGroup.DOFade(1f, PopSeconds * 0.5f).SetDelay(StaggerSeconds * 2f).SetUpdate(true);
            nextButton.DOScale(1f, PopSeconds * 0.5f).SetEase(Ease.OutBack).SetDelay(StaggerSeconds * 2f).SetUpdate(true);
        }

        public void Hide()
        {
            winPanel.DOKill();
            winPanelCanvasGroup.DOKill();
            winLabel.DOKill();
            winLabelCanvasGroup.DOKill();
            nextButton.DOKill();
            nextButtonCanvasGroup.DOKill();

            winPanel.gameObject.SetActive(false);

            winPanel.localScale = Vector3.one;
            winPanelCanvasGroup.alpha = 1f;
            winLabel.localScale = Vector3.one;
            winLabelCanvasGroup.alpha = 1f;
            nextButton.localScale = Vector3.one;
            nextButtonCanvasGroup.alpha = 1f;
        }

        private string StarLabel(int mistakes)
        {
            if (mistakes <= 0)
            {
                return "3 Stars";
            }

            return mistakes <= 2 ? "2 Stars" : "1 Star";
        }
    }
}
