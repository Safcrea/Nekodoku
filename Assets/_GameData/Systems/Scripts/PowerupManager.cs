using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Owns the free-use budget for each powerup (Reveal A Cat, Hint). No currency/economy system
    /// exists yet - <see cref="freeRevealCatUsesPerLevel"/>/<see cref="freeHintUsesPerLevel"/> are the
    /// whole "gate" for now. <see cref="ResetForLevel"/> is called by GameManager on both a fresh level
    /// load and an explicit retry (mirroring PuzzleBoard's own per-attempt reset), but NOT when an extra
    /// life is granted to continue the same attempt - the budget is per-attempt, not per-heart.
    /// </summary>
    public sealed class PowerupManager : MonoBehaviour
    {
        [SerializeField] private int freeRevealCatUsesPerLevel = 1;
        [SerializeField] private int freeHintUsesPerLevel = 1;

        private int revealCatUsesRemaining;
        private int hintUsesRemaining;

        public int RevealCatUsesRemaining => revealCatUsesRemaining;
        public int HintUsesRemaining => hintUsesRemaining;

        public void ResetForLevel()
        {
            revealCatUsesRemaining = freeRevealCatUsesPerLevel;
            hintUsesRemaining = freeHintUsesPerLevel;
        }

        /// <summary>True (and consumes one use) if a free use was available. The caller should still let
        /// the button be pressed at zero remaining - false is exactly where the future rewarded-ad unlock
        /// hooks in below - so this never blocks the click itself, only whether it grants anything yet.</summary>
        public bool TryConsumeRevealCat()
        {
            if (revealCatUsesRemaining > 0)
            {
                revealCatUsesRemaining--;
                return true;
            }

            // TODO: out of free uses - unlock one more via a rewarded ad once ad integration exists.
            // AdPlugin.ShowRewardAd(() => GiveReward);
            return false;
        }

        public bool TryConsumeHint()
        {
            if (hintUsesRemaining > 0)
            {
                hintUsesRemaining--;
                return true;
            }

            // TODO: out of free uses - unlock one more via a rewarded ad once ad integration exists.
            // AdPlugin.ShowRewardAd(() => GiveReward);
            return false;
        }
    }
}
