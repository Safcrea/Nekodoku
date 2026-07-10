#if USE_AVNADS_PLUGIN
using System.Collections.Generic;
using AVN.AdsPlugin.Controllers;
using AVN.AdsPlugin.Services;
using UnityEngine;

public static class GameAnalyticsEvents
{
#pragma warning disable CS0618
    //---── Level flow ────────────────────────────────────────────────────────────

    public static void LevelAnalysis(int levelNo, LevelState levelState, LevelMode levelMode) =>
        AVNPlugin.DTInstance?.SendLevelAnalysisEvent(levelNo, levelState, levelMode);
    //---── Level moves ───────────────────────────────────────────────────────────
    public static void CustomLevelAnalysis(int levelNo, LevelState levelState)
    {
        var payload = new Dictionary<string, string>
            {
                { levelState.ToString(), levelNo.ToString() },
            };

        AVNPlugin.DTInstance?.SendCustomGameEvent(AnalyticsEventService.LevelAnalysisEventName, payload, true);
    }
    //---── Powerups ──────────────────────────────────────────────────────────────

    public static void PowerupUsage(PowerupType powerupType, bool isRewardedUse, int levelNo)
    {
        var parameters = new Dictionary<string, string>
        {
            { "POWERUP_TYPE", powerupType.ToString() },
            { "USE_TYPE", isRewardedUse ? "rewarded" : "free" },
            { "LEVEL_NUMBER", levelNo.ToString() },
        };

        AVNPlugin.DTInstance?.SendCustomGameEvent("POWERUP_USAGE", parameters, true, true);
    }
    //---── Extra life ────────────────────────────────────────────────────────────

    public static void ExtraHeartUsed(int levelNo)
    {
        var parameters = new Dictionary<string, string>
        {
            { "LEVEL_NUMBER", levelNo.ToString() },
        };

        AVNPlugin.DTInstance?.SendCustomGameEvent("EXTRA_HEART_USED", parameters, true, true);
    }
    //---── Tutorial ──────────────────────────────────────────────────────────────

    /// <summary>One event per tutorial step as it's reached (stepNo 1-6: first cat click, first rule
    /// play, second cat click, second rule play, third cat click, third rule play) - fired when the
    /// step starts, not when it's completed.</summary>
    public static void TutorialAnalysis(int stepNo)
    {
        var payload = new Dictionary<string, string>
        {
            { "Started", stepNo.ToString() },
        };

        AVNPlugin.DTInstance?.SendCustomGameEvent("TUTORIAL_ANALYSIS", payload, true, true);
    }

    /// <summary>
    /// Logs device name and battery percentage to Firebase Analytics.s
    /// </summary>
    public static void SendSingleEvent(string eventName)
    {
        AVNPlugin.DTInstance?.SendCustomGameEvent(eventName, null, true, true);
    }
}
public enum LevelMode
{
    DEFAULT,
}

#pragma warning restore CS0618
#endif
