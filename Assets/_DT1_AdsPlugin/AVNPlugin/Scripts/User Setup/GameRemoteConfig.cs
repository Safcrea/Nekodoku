#if USE_AVNADS_PLUGIN
using AVN.AdsPlugin;
using AVN.AdsPlugin.UserHandlers;
using Meowdoku;

public class GameRemoteConfig : AVNHandlerSetup
{
    private const string LocalizationConfigKey = "Localization";

    protected override void OnRegisterKeys()
    {
        RegisterKey(LocalizationConfigKey, RemoteDataType.Json, ApplyLocalizationJson);
    }

    protected override void OnConfigFetched()
    {
        //* Called after every successful Firebase fetch.
        //* All registered key callbacks have already fired when this runs.
        LocalizationService.InitializeDefaultIfNeeded();
    }

    private static void ApplyLocalizationJson(string json)
    {
        if (!LocalizationService.InitializeFromRemoteJson(json))
        {
            LocalizationService.InitializeFromDefaultResources();
        }
    }
}
#endif
