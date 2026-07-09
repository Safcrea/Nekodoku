#if USE_AVNADS_PLUGIN
using System;
using System.Collections.Generic;
using AVN.AdsPlugin.Controllers;
using AVN.AdsPlugin.Testing;
#if USE_BYTEBREW
using ByteBrewSDK;
#endif
#if USE_FIREBASE
using Firebase.Analytics;
#endif
using UnityEngine;

namespace AVN.AdsPlugin.Services
{
    public sealed class AnalyticsEventService
    {
        public const string LevelAnalysisEventName = "LEVEL_ANALYSIS";
        public const string LevelStateParameterName = "STATE";
        public const string LevelNumberParameterName = "NUMBER";
        public const string LevelModeParameterName = "MODE";


        public bool LevelAnalysisEvent(int levelNumber, LevelState levelState, LevelMode levelMode)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(
                DiagnosticKeysAVNPlugin.AnalyticsEvent,
                DiagnosticStateAVNPlugin.Sending,
                $"LevelAnalysisEvent requested: level={levelNumber}, state={levelState}");

            var payloadByteBrew = new Dictionary<string, string>
            {
                { LevelStateParameterName, levelState.ToString() },
                { LevelNumberParameterName, levelNumber.ToString() },
                { LevelModeParameterName, levelMode.ToString() }
            };
            bool successByteBrew = SendCustomEvent(LevelAnalysisEventName, payloadByteBrew, false, true);
            if (levelState != LevelState.Started) return successByteBrew;


            var payloadFirebase = new Dictionary<string, string>
            {
                { "Started",  levelNumber.ToString()},
            };
            return SendCustomEvent(LevelAnalysisEventName, payloadFirebase, true, false);
        }
        public bool SendCustomEvent(string eventName, Dictionary<string, string> parameters, bool sendToFirebase = true, bool sendToByteBrew = true)
        {
            DiagnosticsHubAVNPlugin.PublishStatus(
                DiagnosticKeysAVNPlugin.AnalyticsEvent,
                DiagnosticStateAVNPlugin.Sending,
                $"SendCustomEvent requested: event={eventName}, toFirebase={sendToFirebase}, toByteBrew={sendToByteBrew}");
            var payload = parameters != null
                ? new Dictionary<string, string>(parameters)
                : new Dictionary<string, string>();

            var success = false;
            if (sendToByteBrew)
            {
                success |= SendToByteBrew(eventName, payload);
            }

            if (sendToFirebase)
            {
                success |= SendToFirebase(eventName, payload);
            }

            var summary = $"Event={eventName}, Params={payload.Count}";
            DiagnosticsHubAVNPlugin.PublishStatus(
                DiagnosticKeysAVNPlugin.AnalyticsEvent,
                success ? DiagnosticStateAVNPlugin.Sent : DiagnosticStateAVNPlugin.SendFailed,
                $"Custom event {(success ? "sent" : "failed to send")}: {summary}",
                payload);
            return success;
        }

        private static bool SendToByteBrew(string eventName, Dictionary<string, string> payload)
        {
#if USE_BYTEBREW
            try
            {
                if (!ByteBrew.IsInitilized)
                {
                    DebugLogger.AVNLog($"SendToByteBrew failed: not initialized, event={eventName}");
                    DiagnosticsHubAVNPlugin.PublishStatus(
                        DiagnosticKeysAVNPlugin.AnalyticsEvent,
                        DiagnosticStateAVNPlugin.SendFailed,
                        $"{eventName}: ByteBrew is not initialized",
                        payload);
                    return false;
                }

                ByteBrew.NewCustomEvent(eventName, payload);
                DebugLogger.AVNLog($"SendToByteBrew sent event={eventName}, params={payload.Count}");
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.AnalyticsEvent,
                    DiagnosticStateAVNPlugin.Sent,
                    $"{eventName} sent",
                    payload);
                return true;
            }
            catch (Exception exception)
            {
                DebugLogger.AVNLog($"SendToByteBrew exception event={eventName}, message={exception.Message}");
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.AnalyticsEvent,
                    DiagnosticStateAVNPlugin.SendFailed,
                    $"{eventName} failed: {exception.Message}",
                    payload);
                return false;
            }
#else
            return false;
#endif
        }
        private static bool SendToFirebase(string eventName, Dictionary<string, string> payload)
        {
#if USE_FIREBASE
            try
            {
                if (!AVNPlugin.FirebaseInitSucceeded) return false;
                var firebaseParameters = new List<Parameter>(payload.Count);
                foreach (var pair in payload)
                {
                    firebaseParameters.Add(new Parameter(pair.Key, pair.Value ?? string.Empty));
                }

                FirebaseAnalytics.LogEvent(eventName, firebaseParameters.ToArray());
                DebugLogger.AVNLog($"SendToFirebase sent event={eventName}, params={payload.Count}");
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.AnalyticsEvent,
                    DiagnosticStateAVNPlugin.Sent,
                    $"{eventName} sent",
                    payload);
                return true;
            }
            catch (Exception exception)
            {
                DebugLogger.AVNLog($"SendToFirebase exception event={eventName}, message={exception.Message}");
                DiagnosticsHubAVNPlugin.PublishStatus(
                    DiagnosticKeysAVNPlugin.AnalyticsEvent,
                    DiagnosticStateAVNPlugin.SendFailed,
                    $"{eventName} failed: {exception.Message}",
                    payload);
                return false;
            }
#endif
            return false;
        }

    }
}
public enum LevelState
{
    Started,
    Completed,
    Failed,
    Restarted,
    Skipped
}
public enum UserReturnStatus
{
    SERVED_AD,
    RETURNED_FROM_AD
}
#endif
