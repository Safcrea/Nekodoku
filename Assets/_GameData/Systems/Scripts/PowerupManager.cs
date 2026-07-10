using UnityEngine;
using UnityEngine.Serialization;

namespace Meowdoku
{
    /// <summary>
    /// Owns the global free-use budget for each powerup (Reveal A Cat, Hint). No currency/economy system
    /// exists yet - the configured counts are granted exactly once per install (persisted via
    /// <see cref="PlayerPrefs"/>, not just in-memory) and are never refilled on level load, retry, or a
    /// fresh app launch.
    /// </summary>
    public sealed class PowerupManager : MonoBehaviour
    {
        private const string RevealCatUsesRemainingPlayerPrefsKey = "Nekodoku.PowerupManager.RevealCatUsesRemaining";
        private const string HintUsesRemainingPlayerPrefsKey = "Nekodoku.PowerupManager.HintUsesRemaining";

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
            revealCatUsesRemaining = LoadOrGrant(RevealCatUsesRemainingPlayerPrefsKey, freeRevealCatUses);
            hintUsesRemaining = LoadOrGrant(HintUsesRemainingPlayerPrefsKey, freeHintUses);
        }

        /// <summary>First-ever call for this key grants (and persists) the configured free-use count;
        /// every call after that reads back whatever was last persisted, however low - this is what makes
        /// the budget a one-time-per-install grant rather than something that quietly refills on relaunch.</summary>
        private static int LoadOrGrant(string playerPrefsKey, int freeUses)
        {
            if (PlayerPrefs.HasKey(playerPrefsKey))
            {
                return Mathf.Max(0, PlayerPrefs.GetInt(playerPrefsKey));
            }

            int granted = Mathf.Max(0, freeUses);
            PlayerPrefs.SetInt(playerPrefsKey, granted);
            PlayerPrefs.Save();
            return granted;
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
                PlayerPrefs.SetInt(RevealCatUsesRemainingPlayerPrefsKey, revealCatUsesRemaining);
                PlayerPrefs.Save();
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
                PlayerPrefs.SetInt(HintUsesRemainingPlayerPrefsKey, hintUsesRemaining);
                PlayerPrefs.Save();
                return true;
            }

            // TODO: out of free uses - unlock one more via a rewarded ad once ad integration exists.
            // AdPlugin.ShowRewardAd(() => GiveReward);
            return false;
        }
    }
}
