using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

public static class AdConstants
{

	public static bool resumeFromAds = false;
	public static bool AdsEnabledMaster = true;
	public static int EnableInterFromLevel = 3;
	public static float InterAdDelay = 30f;
	public static int ShowRateUsOnLevel = 4;
	public static int ShowNotificationOnLevel = 5;
	public static bool EnableBannerAd = true;
	public static bool EnableInterAd = true;
	public static bool EnableRewardedAd = true;
	public static bool EnableAppOpenAd = true;

	public static int InterCount
	{
		get
		{
			return PlayerPrefs.GetInt("InterCount", 0);
		}
		set
		{
			PlayerPrefs.SetInt("InterCount", value);
			PlayerPrefs.Save();
		}
	}

	public static bool RemoveAdsPurchased
	{
		get => PlayerPrefs.GetInt("AVN_RemoveAdsPurchased", 0) == 1;
		set
		{
			PlayerPrefs.SetInt("AVN_RemoveAdsPurchased", value ? 1 : 0);
			PlayerPrefs.Save();
			EnableInterAd   = !value;
			EnableBannerAd  = !value;
			EnableAppOpenAd = !value;
		}
	}
}
