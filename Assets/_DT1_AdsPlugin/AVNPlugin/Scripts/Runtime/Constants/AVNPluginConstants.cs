using System;
using UnityEngine;

public static class AVNPluginConstants
{
	//* Use this bool to check if Ads are enabled or not
	public static bool AdsEnabledMaster = true;
	public static bool EnableBannerAd = true;
	public static bool EnableInterAd = true;
	public static bool EnableRewardedAd = true;
	public static bool EnableAppOpenAd = true;

	//* don't use this value to check inter delay, it's for internal use
	public static int DefaultInterAdDelay = 30;
	private static bool? _cachedIsFirstSession;

	public static bool IsFirstSession
	{
		get
		{
			if (!_cachedIsFirstSession.HasValue)
			{
				_cachedIsFirstSession = !PlayerPrefs.HasKey("IsFirstSession");
			}
			return _cachedIsFirstSession.Value;
		}
		set
		{
			PlayerPrefs.SetInt("IsFirstSession", value ? 1 : 0);
			PlayerPrefs.Save();

			if (!_cachedIsFirstSession.HasValue)
			{
				_cachedIsFirstSession = !PlayerPrefs.HasKey("IsFirstSession");
			}

			// Keep current-session first-session state true until the session ends.
			if (_cachedIsFirstSession.Value && !value)
			{
				return;
			}

			_cachedIsFirstSession = value;
		}
	}
	public static bool CMPEligibleUser
	{
		get
		{
			return PlayerPrefs.GetInt("IsUserInCMPCountry", 1) == 1;
		}
		set
		{
			PlayerPrefs.SetInt("IsUserInCMPCountry", value ? 1 : 0);
			PlayerPrefs.Save();
		}
	}
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
			Debug.Log("[RemoveAdsPurchased] EnableInterAd set to: " + !value);
			EnableInterAd = !value;
			EnableBannerAd = !value;
			EnableAppOpenAd = !value;
		}
	}
	public static string UserID
	{
		get
		{
			if (PlayerPrefs.HasKey("AVN_UserId"))
			{
				return PlayerPrefs.GetString("AVN_UserId");
			}
			else
			{
				string newUserId = Guid.NewGuid().ToString();
				PlayerPrefs.SetString("AVN_UserId", newUserId);
				PlayerPrefs.Save();
				return newUserId;
			}

		}
	}
}