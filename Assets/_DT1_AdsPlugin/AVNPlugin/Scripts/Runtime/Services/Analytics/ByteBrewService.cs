#if USE_AVNADS_PLUGIN && USE_BYTEBREW
using System;
 
using AVN.AdsPlugin.Testing;
using ByteBrewSDK;



namespace AVN.AdsPlugin.Services
{
    public sealed class ByteBrewService
    {
        public event Action OnInitialized;
        public event Action<string> OnInitializationFailed;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            DebugLogger.AVNLog("ByteBrew initialize requested");
            DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkByteBrew, DiagnosticStateAVNPlugin.Initializing, "Initializing ByteBrew");

            try
            {
                ByteBrew.InitializeByteBrew();
#if UNITY_EDITOR
                IsInitialized = true;
#else
                IsInitialized = ByteBrew.IsInitilized || ByteBrew.IsByteBrewInitialized();
#endif
                if (IsInitialized)
                {
                    DebugLogger.AVNLog("ByteBrew initialized successfully");
                    DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkByteBrew, DiagnosticStateAVNPlugin.Initialized, "ByteBrew initialized");
                    OnInitialized?.Invoke();
                }
                else
                {
                    DebugLogger.AVNLog("ByteBrew init completed but ready state was false");
                    DiagnosticsHubAVNPlugin.PublishStatus(
                        DiagnosticKeysAVNPlugin.SdkByteBrew,
                        DiagnosticStateAVNPlugin.Failed,
                        "ByteBrew initialization did not report ready state.");
                    OnInitializationFailed?.Invoke("ByteBrew initialization did not report ready state.");
                }
            }
            catch (Exception exception)
            {
                DebugLogger.AVNLog($"ByteBrew init exception: {exception.Message}");
                DiagnosticsHubAVNPlugin.PublishStatus(DiagnosticKeysAVNPlugin.SdkByteBrew, DiagnosticStateAVNPlugin.Failed, exception.Message);
                OnInitializationFailed?.Invoke(exception.Message);
            }
        }
    }
}
#endif