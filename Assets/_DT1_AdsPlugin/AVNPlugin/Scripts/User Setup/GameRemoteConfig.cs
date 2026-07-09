#if USE_AVNADS_PLUGIN
using AVN.AdsPlugin;
using AVN.AdsPlugin.Controllers;
using AVN.AdsPlugin.UserHandlers;
using UnityEngine;

public class GameRemoteConfig : AVNHandlerSetup
{
    protected override void OnRegisterKeys()
    {
        // RegisterKey("hard_mode_enabled", RemoteDataType.Bool,   v => GameConfig.HardMode   = v == "true");
        // RegisterKey("starting_coins",    RemoteDataType.Int,    v => GameConfig.StartCoins  = int.Parse(v));
        // RegisterKey("welcome_message",   RemoteDataType.String, v => GameConfig.WelcomeMsg  = v);
        RegisterKey("GameSettings", RemoteDataType.Json, v =>
        {
            GameSettings settings = JsonUtility.FromJson<GameSettings>(v);
        });


    }

    protected override void OnConfigFetched()
    {
        //* Called after every successful Firebase fetch.
        //* All registered key callbacks have already fired when this runs.
    }
    public class GameSettings
    {
        public bool ShowSkipButton;
        public bool ShowResumePopUp;
        public int ResumePopUpStepCountThreshold;
        public bool CanCompleteScratchStepWhileTouchActive;
        public bool EnableANRFixer;
        public int AdDelayForFirstLevel;
        public bool EnableAdBreak;
        public bool CanUnlockLevelViaReward;
        public bool showStepCountInUI;
    }
}
#endif
