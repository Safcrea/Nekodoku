#if USE_AVNADS_PLUGIN
namespace AVN.AdsPlugin
{
    public interface IAppOpenAdService
    {
        bool IsSdkInitialized { get; }
        bool IsAnyAdReady { get; }
        bool IsLoadInProgress { get; }

        void ConfigureAdIds(string primaryId, string backfillId);
        void Load();
        bool Show();
        void DestroyAds();
    }
}
#endif