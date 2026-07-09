using UnityEngine;
#if USE_FIREBASE
using Firebase.Analytics;
#endif

public class FinzAdRevenueAggregator
{
    private const string PREF_TROAS_CACHE = "TroasCache";
    private const string PREF_AD_COUNT = "AdCount";

    // Updated thresholds
    private readonly int[] AD_THRESHOLDS = {2,3,5,8, 10, 14, 18, 22 };

    public void OnAdImpression(double impressionRevenue)
    {
#if USE_FIREBASE
        float previousTroasCache = PlayerPrefs.GetFloat(PREF_TROAS_CACHE, 0f);
        int previousAdCount = PlayerPrefs.GetInt(PREF_AD_COUNT, 0);

        float currentTroasCache = previousTroasCache + (float)impressionRevenue;
        int currentAdCount = previousAdCount + 1;

        // Fire event if threshold reached
        foreach (int threshold in AD_THRESHOLDS)
        {
            if (currentAdCount == threshold)
            {
                //Debug.Log("UJ:: currentAdCount is " + currentAdCount);
                string eventName = GetEventName(threshold);
                //Debug.Log("UJ:: eventName is " + eventName);
                LogTroasFirebaseAdRevenueEvent(currentTroasCache, eventName);
                break; // Only fire once per impression
            }
        }

        // Reset after highest threshold (22)
        if (currentAdCount >= 22)
        {
            //Debug.Log("UJ:: currentAdCount >= 22 SO Resetting Prefs to 0 ");
            PlayerPrefs.SetFloat(PREF_TROAS_CACHE, 0f);
            PlayerPrefs.SetInt(PREF_AD_COUNT, 0);
        }
        else
        {
            //Debug.Log("UJ:: currentAdCount is less than threshold so we are just saving the current adcount and RoasCatch ");
            PlayerPrefs.SetFloat(PREF_TROAS_CACHE, currentTroasCache);
            PlayerPrefs.SetInt(PREF_AD_COUNT, currentAdCount);
        }

        PlayerPrefs.Save();
#endif
    }

#if USE_FIREBASE
    private string GetEventName(int threshold)
    {
        switch (threshold)
        {
			case 2: return "TwoAdsShown";
			case 3: return "ThreeAdsShown";
			case 5: return "FiveAdsShown";
			case 8: return "EightAdsShown";
            case 10: return "TenAdsShown";
            case 14: return "FourteenAdsShown";
            case 18: return "EighteenAdsShown";
            case 22: return "TwentyTwoAdsShown";
            default: return "AdsShown";
        }
    }

    private void LogTroasFirebaseAdRevenueEvent(float totalRevenue, string eventName)
    {
        Parameter[] parameters = {
            new Parameter(FirebaseAnalytics.ParameterValue, totalRevenue),
            new Parameter(FirebaseAnalytics.ParameterCurrency, "USD")
        };

        FirebaseAnalytics.LogEvent(eventName, parameters);

        Debug.Log($"[AdAggregator] Fired '{eventName}' with revenue: {totalRevenue}");
    }
#endif
}
