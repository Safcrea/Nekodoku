using AllIn1SpringsToolkit;
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
        private const string RevealedAnimationName = "Revealed";
        private const string ExcitedAnimationName = "Excited";

        [SerializeField] private TMP_Text countText;

        [SerializeField] private TransformSpringComponent countSpring;
        [SerializeField] private SpriteSheetAnimationPlayer catAnimationPlayer;

        private void OnEnable()
        {
            PlayIdleCatAnimation();
        }

        public void Refresh(int revealedCatCount, int totalCats)
        {
            if (countText != null)
            {
                countText.text = $"{revealedCatCount}/{totalCats}";
            }
        }

        /// <summary>Pops the counter when a cat is found. Purely additive - Refresh already set the correct number.</summary>
        public void PlayCatCollected()
        {
            if (countSpring != null)
            {
                countSpring.AddVelocityScale(Vector3.one * PopScaleImpulse);
            }
            if (catAnimationPlayer != null)
            {
                catAnimationPlayer.PlayOnceThen(ExcitedAnimationName, RevealedAnimationName);
            }
        }

        private void PlayIdleCatAnimation()
        {
            if (catAnimationPlayer != null)
            {
                catAnimationPlayer.Play(RevealedAnimationName, false);
            }
        }
    }
}
