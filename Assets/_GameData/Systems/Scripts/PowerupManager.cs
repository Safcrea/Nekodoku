using UnityEngine;
using UnityEngine.Serialization;

namespace Meowdoku
{
    /// <summary>
    /// Owns the global free-use budget for each powerup (Reveal A Cat, Hint). No currency/economy system
    /// exists yet - the configured counts are granted once when the manager initializes and are not refilled
    /// on level load or retry.
    /// </summary>
    public sealed class PowerupManager : MonoBehaviour
    {
        [FormerlySerializedAs("freeRevealCatUsesPerLevel")]
        [SerializeField] private int freeRevealCatUses = 1;

        [FormerlySerializedAs("freeHintUsesPerLevel")]
        [SerializeField] private int freeHintUses = 1;

        private int revealCatUsesRemaining;
        private int hintUsesRemaining;
        private bool initialized;

        public int RevealCatUsesRemaining
        {
            get
            {
                InitializeGlobalBudget();
                return revealCatUsesRemaining;
            }
        }

        public int HintUsesRemaining
        {
            get
            {
                InitializeGlobalBudget();
                return hintUsesRemaining;
            }
        }

        private void Awake()
        {
            InitializeGlobalBudget();
        }

        public void InitializeGlobalBudget()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            revealCatUsesRemaining = Mathf.Max(0, freeRevealCatUses);
            hintUsesRemaining = Mathf.Max(0, freeHintUses);
        }

        /// <summary>True (and consumes one use) if a free use was available. The caller should still let
        /// the button be pressed at zero remaining - false is exactly where the future rewarded-ad unlock
        /// hooks in below - so this never blocks the click itself, only whether it grants anything yet.</summary>
        public bool TryConsumeRevealCat()
        {
            InitializeGlobalBudget();

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
            InitializeGlobalBudget();

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
