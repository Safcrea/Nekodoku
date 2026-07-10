using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// The gameplay HUD: level title/number and status message, plus the two sub-widgets
    /// that live in it - hearts (<see cref="LifeHearts"/>) and found cats
    /// (<see cref="CatCounter"/>). The single point of contact for both, so GameManager
    /// and BoardInputHandler don't need to know either exists. The win panel is its own
    /// <see cref="LevelCompleteScreen"/>.
    /// </summary>
    public sealed class GameplayScreen : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private LifeHearts lifeHearts;

        [SerializeField]
        private CatCounter catBasket;

        [Tooltip("Optional - the always-on Rules tab strip's three rule labels (row/column, touching, color). Set from LocalizationService each refresh so returning players who skip the once-ever tutorial still see localized text.")]
        [SerializeField] private TMP_Text rule1Text;
        [SerializeField] private TMP_Text rule2Text;
        [SerializeField] private TMP_Text rule3Text;

        public void Refresh(int levelIndex, Level level, ValidationResult validation)
        {
            titleText.text = LocalizationService.GetFormat("gameplay.levelTitleFormat", levelIndex + 1);
            lifeHearts.Refresh(validation.HeartsRemaining, validation.IsFailed);
            catBasket.Refresh(validation.RevealedCatCount, level.Size);

            if (rule1Text != null)
            {
                rule1Text.text = LocalizationService.Get("gameplay.rule1");
            }

            if (rule2Text != null)
            {
                rule2Text.text = LocalizationService.Get("gameplay.rule2");
            }

            if (rule3Text != null)
            {
                rule3Text.text = LocalizationService.Get("gameplay.rule3");
            }
        }

        public void PlayIntro()
        {
            lifeHearts.PlayIntro();
        }

        public void PlayHeartLost(int heartsRemaining)
        {
            lifeHearts.PlayHeartLostEffect(heartsRemaining);
        }

        public void PlayHeartGained(int heartsRemaining)
        {
            lifeHearts.PlayHeartGainedEffect(heartsRemaining);
        }

        public void PlayCatCollected()
        {
            //* Nothing to play yet
        }

        /// <summary>Hides the whole HUD strip - used while the tutorial lesson board is active, since
        /// its level number/hearts/cat-count aren't meaningful until the lesson is actually done.</summary>
        public void SetHudVisible(bool visible)
        {
            lifeHearts.gameObject.SetActive(visible);
            titleText.gameObject.SetActive(visible);
        }
    }
}
