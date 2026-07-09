#if USE_AVNADS_PLUGIN
using System;
using System.Collections.Generic;

namespace AVN.AdsPlugin.Testing
{
    public enum DiagnosticStateAVNPlugin
    {
        Info,
        Initializing,
        Initialized,
        Failed,
        Idle,
        Loading,
        Loaded,
        Showing,
        Showed,
        Closed,
        Destroyed,
        Sending,
        Sent,
        SendFailed
    }

    public static class DiagnosticKeysAVNPlugin
    {
        public const string SdkFirebase = "SDK.Firebase";
        public const string SdkByteBrew = "SDK.ByteBrew";
        public const string SdkRemoteConfig = "SDK.RemoteConfig";
        public const string SdkAppsFlyer = "SDK.AppsFlyer";
        public const string SdkIronSource = "SDK.IronSource";
        public const string InAppPurchaserScript = "SDK.Purchaser";
        public const string AdInterstitial = "Ad.Interstitial";
        public const string AdAppOpen = "Ad.AppOpen";
        public const string AdRewarded = "Ad.Rewarded";
        public const string AdBanner = "Ad.Banner";
        public const string AdBannerStandard = "Ad.Banner.Standard";
        public const string AdBannerMrec = "Ad.Banner.Mrec";

        public const string Notifcation = "Notification";
        public const string NativeRateUs = "NativeRateUs";
        public const string RemoteConfigValues = "RemoteConfig.Values";
        public const string AnalyticsEvent = "Analytics.Event";
    }

    public sealed class DiagnosticStatusAVNPlugin
    {
        public DiagnosticStateAVNPlugin State;
        public string Message;
        public DateTime UpdatedUtc;
        public Dictionary<string, string> Data;
    }

    public sealed class DiagnosticTimelineEntryAVNPlugin
    {
        public string Scope;
        public string Key;
        public string Message;
        public DateTime CreatedUtc;
    }

    public sealed class DiagnosticCountersAVNPlugin
    {
        public int LoadCallsSent;
        public int ShowCallsSent;
        public int AdsLoaded;
        public int AdsDisplayed;
        public int LoadCallsFailed;
        public int ShowCallsFailed;
    }

    public static class DiagnosticsHubAVNPlugin
    {
        private const int MaxTimelineEntries = 200;

        private static readonly Dictionary<string, DiagnosticStatusAVNPlugin> Statuses = new Dictionary<string, DiagnosticStatusAVNPlugin>();
        private static readonly Dictionary<string, DiagnosticCountersAVNPlugin> Counters = new Dictionary<string, DiagnosticCountersAVNPlugin>();
        private static readonly List<DiagnosticTimelineEntryAVNPlugin> Timeline = new List<DiagnosticTimelineEntryAVNPlugin>();

        public static event Action Changed;

        public static void PublishStatus(string key, DiagnosticStateAVNPlugin state, string message = "", Dictionary<string, string> data = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!string.IsNullOrEmpty(message))
            {
                DebugLogger.AVNLog($"Status {key} => {state}: {message}", false);
            }

            Statuses[key] = new DiagnosticStatusAVNPlugin
            {
                State = state,
                Message = message ?? string.Empty,
                UpdatedUtc = DateTime.UtcNow,
                Data = data != null ? new Dictionary<string, string>(data) : null
            };

            if (!string.IsNullOrEmpty(message))
            {
                AddTimeline("Status", key, message);
            }

            Changed?.Invoke();
        }

        public static bool TryGetStatus(string key, out DiagnosticStatusAVNPlugin status)
        {
            if (!Statuses.TryGetValue(key, out var found))
            {
                status = null;
                return false;
            }

            status = new DiagnosticStatusAVNPlugin
            {
                State = found.State,
                Message = found.Message,
                UpdatedUtc = found.UpdatedUtc,
                Data = found.Data != null ? new Dictionary<string, string>(found.Data) : null
            };
            return true;
        }

        public static bool TryGetCounters(string key, out DiagnosticCountersAVNPlugin counters)
        {
            if (!Counters.TryGetValue(key, out var found))
            {
                counters = null;
                return false;
            }

            counters = new DiagnosticCountersAVNPlugin
            {
                LoadCallsSent = found.LoadCallsSent,
                ShowCallsSent = found.ShowCallsSent,
                AdsLoaded = found.AdsLoaded,
                AdsDisplayed = found.AdsDisplayed,
                LoadCallsFailed = found.LoadCallsFailed,
                ShowCallsFailed = found.ShowCallsFailed
            };
            return true;
        }

        public static void IncrementLoadCallSent(string key)
        {
            var counters = GetOrCreateCounters(key);
            counters.LoadCallsSent++;
            DebugLogger.AVNLog($"Counter {key} LoadCallsSent={counters.LoadCallsSent}", false);
            AddTimeline("Counter", key, "Load call sent");
            Changed?.Invoke();
        }

        public static void IncrementAdsLoaded(string key)
        {
            var counters = GetOrCreateCounters(key);
            counters.AdsLoaded++;
            DebugLogger.AVNLog($"Counter {key} AdsLoaded={counters.AdsLoaded}");
            AddTimeline("Counter", key, "Ad loaded");
            Changed?.Invoke();
        }

        public static void IncrementLoadCallsFailed(string key, string reason = "")
        {
            var counters = GetOrCreateCounters(key);
            counters.LoadCallsFailed++;
            AddTimeline("Counter", key, string.IsNullOrEmpty(reason) ? "Load call failed" : $"Load call failed: {reason}");
            Changed?.Invoke();
        }

        public static void IncrementShowCallSent(string key)
        {
            var counters = GetOrCreateCounters(key);
            counters.ShowCallsSent++;
            DebugLogger.AVNLog($"Counter {key} ShowCallsSent={counters.ShowCallsSent}", false);
            AddTimeline("Counter", key, "Show call sent");
            Changed?.Invoke();
        }

        public static void IncrementAdsDisplayed(string key)
        {
            var counters = GetOrCreateCounters(key);
            counters.AdsDisplayed++;
            DebugLogger.AVNLog($"Counter {key} AdsDisplayed={counters.AdsDisplayed}", false);
            AddTimeline("Counter", key, "Ad displayed");
            Changed?.Invoke();
        }

        public static void IncrementShowCallsFailed(string key, string reason = "")
        {
            var counters = GetOrCreateCounters(key);
            counters.ShowCallsFailed++;
            AddTimeline("Counter", key, string.IsNullOrEmpty(reason) ? "Show call failed" : $"Show call failed: {reason}");
            Changed?.Invoke();
        }

        public static Dictionary<string, DiagnosticStatusAVNPlugin> GetStatusesSnapshot()
        {
            var snapshot = new Dictionary<string, DiagnosticStatusAVNPlugin>(Statuses.Count);
            foreach (var pair in Statuses)
            {
                snapshot[pair.Key] = new DiagnosticStatusAVNPlugin
                {
                    State = pair.Value.State,
                    Message = pair.Value.Message,
                    UpdatedUtc = pair.Value.UpdatedUtc,
                    Data = pair.Value.Data != null ? new Dictionary<string, string>(pair.Value.Data) : null
                };
            }

            return snapshot;
        }

        public static List<DiagnosticTimelineEntryAVNPlugin> GetTimelineSnapshot()
        {
            var copy = new List<DiagnosticTimelineEntryAVNPlugin>(Timeline.Count);
            foreach (var entry in Timeline)
            {
                copy.Add(new DiagnosticTimelineEntryAVNPlugin
                {
                    Scope = entry.Scope,
                    Key = entry.Key,
                    Message = entry.Message,
                    CreatedUtc = entry.CreatedUtc
                });
            }

            return copy;
        }

        private static void AddTimeline(string scope, string key, string message)
        {
            Timeline.Add(new DiagnosticTimelineEntryAVNPlugin
            {
                Scope = scope ?? string.Empty,
                Key = key ?? string.Empty,
                Message = message ?? string.Empty,
                CreatedUtc = DateTime.UtcNow
            });

            if (Timeline.Count > MaxTimelineEntries)
            {
                Timeline.RemoveAt(0);
            }
        }

        private static DiagnosticCountersAVNPlugin GetOrCreateCounters(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "Unknown";
            }

            if (!Counters.TryGetValue(key, out var counters))
            {
                counters = new DiagnosticCountersAVNPlugin();
                Counters[key] = counters;
            }

            return counters;
        }
    }
}
#endif
