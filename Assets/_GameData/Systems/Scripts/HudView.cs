using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The "gameplay screen" chrome: level title/count/status, the win panel, and
    /// the four nav buttons. Pure presentation - it never mutates game state,
    /// only reflects the validation result NekoGameManager hands it each refresh.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private static readonly Color ConflictColor = new Color(0.94f, 0.24f, 0.2f, 1f);
        private static readonly Color RevealedColor = new Color(0.14f, 0.52f, 0.48f, 1f);
        private static readonly Color SoftInkColor = new Color(0.43f, 0.39f, 0.36f, 1f);

        [SerializeField]
        private Text titleText;

        [SerializeField]
        private Text countText;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        private RectTransform winPanel;

        [SerializeField]
        private Text winWordText;

        [SerializeField]
        private Text winSubtitleText;

        [SerializeField]
        private Button previousLevelButton;

        [SerializeField]
        private Button undoButton;

        [SerializeField]
        private Button restartButton;

        [SerializeField]
        private Button nextLevelButton;

        private Coroutine winPanelPopRoutine;

        public bool Validate()
        {
            bool valid = true;
            valid &= SceneValidation.LogIfMissing(titleText, "Title Text", this);
            valid &= SceneValidation.LogIfMissing(countText, "Count Text", this);
            valid &= SceneValidation.LogIfMissing(statusText, "Status Text", this);
            valid &= SceneValidation.LogIfMissing(winPanel, "Win Panel", this);
            valid &= SceneValidation.LogIfMissing(winWordText, "Win Word Text", this);
            valid &= SceneValidation.LogIfMissing(winSubtitleText, "Win Subtitle Text", this);
            valid &= SceneValidation.LogIfMissing(previousLevelButton, "Previous Level Button", this);
            valid &= SceneValidation.LogIfMissing(undoButton, "Undo Button", this);
            valid &= SceneValidation.LogIfMissing(restartButton, "Restart Button", this);
            valid &= SceneValidation.LogIfMissing(nextLevelButton, "Next Level Button", this);
            return valid;
        }

        public void BindButtons(UnityAction onPrevious, UnityAction onUndo, UnityAction onRestart, UnityAction onNext)
        {
            previousLevelButton.onClick.AddListener(onPrevious);
            undoButton.onClick.AddListener(onUndo);
            restartButton.onClick.AddListener(onRestart);
            nextLevelButton.onClick.AddListener(onNext);
        }

        public void Refresh(int levelIndex, Level level, ValidationResult validation)
        {
            titleText.text = $"Level {levelIndex + 1}: {level.Title}";
            countText.text = $"Found {validation.RevealedCatCount}/{level.Size}   Hearts {HeartLabel(validation.HeartsRemaining)}";
            statusText.text = validation.IsSolved ? "Solved" : validation.Message;
            statusText.color = validation.IsFailed || validation.HasConflict ? ConflictColor : validation.IsSolved ? RevealedColor : SoftInkColor;
        }

        public void SetWinPanelVisible(bool shouldShow, Level level, ValidationResult validation)
        {
            if (!shouldShow)
            {
                HideWinPanel();
                return;
            }

            winWordText.text = level.TargetWord;
            winSubtitleText.text = $"{StarLabel(validation.MistakeCount)}   Hearts {HeartLabel(validation.HeartsRemaining)}";
            bool wasVisible = winPanel.gameObject.activeSelf;
            winPanel.gameObject.SetActive(true);
            if (!wasVisible)
            {
                PlayWinPanelPop();
            }
        }

        public void HideWinPanel()
        {
            StopWinPanelPop();
            winPanel.gameObject.SetActive(false);
            winPanel.localScale = Vector3.one;
        }

        private void PlayWinPanelPop()
        {
            StopWinPanelPop();
            if (isActiveAndEnabled)
            {
                winPanelPopRoutine = StartCoroutine(AnimateWinPanelPop());
            }
        }

        private IEnumerator AnimateWinPanelPop()
        {
            float elapsed = 0f;
            const float seconds = 0.34f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float scale = Mathf.Lerp(0.84f, 1f, NekoEasing.OutBack(t));
                winPanel.localScale = Vector3.one * scale;
                yield return null;
            }

            winPanel.localScale = Vector3.one;
            winPanelPopRoutine = null;
        }

        private void StopWinPanelPop()
        {
            if (winPanelPopRoutine != null)
            {
                StopCoroutine(winPanelPopRoutine);
                winPanelPopRoutine = null;
            }
        }

        private string HeartLabel(int hearts)
        {
            switch (hearts)
            {
                case 3:
                    return "3/3";
                case 2:
                    return "2/3";
                case 1:
                    return "1/3";
                default:
                    return "0/3";
            }
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
