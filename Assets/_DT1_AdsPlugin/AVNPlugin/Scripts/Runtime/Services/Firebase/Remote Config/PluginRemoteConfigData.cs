#if USE_AVNADS_PLUGIN
using System;
using AVN.AdsPlugin.Controllers;
using UnityEngine;

namespace AVN.AdsPlugin
{
    [Serializable]
    public class PluginRemoteConfigData
    {
        public int EnableInterFromLevel = 3;
        public float InterAdDelay = 30f;
        public float DelayBeforeFirstAd = 30f;
        public int InterstitialShowMode = 1; // 0=PreferDefaultWithoutBackfill, 1=IterateSequence, 2=PreferDefaultWithBackfill
        public int ShowRateUsOnLevel = 4;
        public int ShowNotificationOnLevel = 5;
        public bool EnableBannerAd = true;
        public bool EnableInterAd = true;
        public bool EnableRewardedAd = true;
        public bool EnableAppOpenAd = true;


        public void Apply()
        {
            AVNPlugin.PluginInstance.EnableInterFromLevel = EnableInterFromLevel;
            AVNPlugin.PluginInstance.UpdateInterDelay(InterAdDelay);
            AVNPlugin.PluginInstance.UpdateDelayBeforeFirstAd(DelayBeforeFirstAd);
            AVNPlugin.PluginInstance.UpdateInterstitialShowMode(InterstitialShowMode);
            AVNPlugin.PluginInstance.ShowRateUsOnLevel = ShowRateUsOnLevel;
            AVNPlugin.PluginInstance.ShowNotificationOnLevel = ShowNotificationOnLevel;
            AVNPluginConstants.EnableRewardedAd = EnableRewardedAd;
            if (!AVNPluginConstants.RemoveAdsPurchased)
            {
                AVNPluginConstants.EnableBannerAd = EnableBannerAd;
                Debug.Log("[PluginRemoteConfigData.Apply] EnableInterAd set to: " + EnableInterAd);

                AVNPluginConstants.EnableInterAd = EnableInterAd;
                AVNPluginConstants.EnableAppOpenAd = EnableAppOpenAd;
            }
            AVNPluginConstants.DefaultInterAdDelay = (int)InterAdDelay;
        }
    }
    [Serializable]
    public sealed class AdIdsRemoteConfigData
    {
        public string BannerAdUnitId = string.Empty;
        public string MrecAdUnitId = string.Empty;
        public string RewardedAdUnitId = string.Empty;
        public string[] InterAdUnitIds = Array.Empty<string>();
        public string AppOpenAdUnitId1 = string.Empty;
        public string AppOpenAdUnitId2 = string.Empty;
    }
}
#endif