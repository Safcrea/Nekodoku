#if USE_AVNADS_PLUGIN
using System.Diagnostics;
using AVN.AdsPlugin.Controllers;
using AVN.AdsPlugin.UserHandlers;
using Meowdoku;

public class GameAdsCheck : AVNHandlerSetup
{
    public override bool CanShowInterstitial()
    {
        return GameConstants.CurrentLevelIndex + 1 >= EnableInterFromLevel;
    }
    public override bool CanUseFirstAdDelay()
    {
        return false;
    }
    public override bool CanShowRateUsDialog()
    {
        return GameConstants.CurrentLevelIndex + 1 == ShowRateUsOnLevel;
    }

    public override bool CanShowNotification()
    {
        return GameConstants.CurrentLevelIndex + 1 == ShowNotificationOnLevel;
    }
}
#endif
