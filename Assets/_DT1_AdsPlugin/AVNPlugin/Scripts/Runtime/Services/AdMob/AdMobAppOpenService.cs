#if USE_AVNADS_PLUGIN && USE_ADMOB
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace AVN.AdsPlugin.Services
{
    public sealed class AdMobAppOpenService : IAppOpenAdService
    {
        private sealed class SlotState
        {
            public string AdUnitId = string.Empty;
            public AppOpenAd Ad;
            public bool IsLoading;
            public bool IsReady;
            public bool IsShowing;
            public Action OpenedHandler;
            public Action ClosedHandler;
            public Action<AdError> FailedHandler;
            public Action ClickedHandler;
            public Action ImpressionHandler;
            public Action<AdValue> OnAdPaidHandler;
        }

        private readonly SlotState[] slots = { new SlotState(), new SlotState() };
        private int currentShowingSlot = -1;

        public bool IsSdkInitialized => AdMobSdkInitializer.IsInitialized;
        public bool IsAnyAdReady => GetReadySlotIndex() >= 0;

        public bool IsLoadInProgress
        {
            get
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i].IsLoading)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void ConfigureAdIds(string primaryId, string backfillId)
        {
            ConfigureSlotId(0, NormalizeId(primaryId));
            ConfigureSlotId(1, NormalizeId(backfillId));
        }

        public void Load()
        {
            if (!IsSdkInitialized)
            {
                AdEvents.OnAppOpenLoadFailed?.Invoke("AdMob SDK is not initialized");
                return;
            }

            if (!HasConfiguredSlot(0) && !HasConfiguredSlot(1))
            {
                AdEvents.OnAppOpenLoadFailed?.Invoke("App-open ad unit ids are empty");
                return;
            }

            TryLoadSlot(0);
            TryLoadSlot(1);
        }

        public bool Show()
        {
            var slotIndex = GetReadySlotIndex();
            if (slotIndex < 0)
            {
                return false;
            }

            var slot = slots[slotIndex];
            if (slot.Ad == null)
            {
                DestroySlot(slotIndex);
                return false;
            }

            if (!slot.Ad.CanShowAd())
            {
                DestroySlot(slotIndex);
                return false;
            }

            slot.IsReady = false;
            slot.IsShowing = true;
            currentShowingSlot = slotIndex;
            AdEvents.OnBeforeAppOpenShown?.Invoke();

            try
            {
                slot.Ad.Show();
                DebugLogger.AVNLog($"AdMob AppOpen show sent for slot={slotIndex}");
                return true;
            }
            catch (Exception exception)
            {
                slot.IsShowing = false;
                currentShowingSlot = -1;
                DestroySlot(slotIndex);
                AdEvents.OnAppOpenFailToShow?.Invoke($"App-open slot {slotIndex} failed to show: {exception.Message}");
                return false;
            }
        }

        public void DestroyAds()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                DestroySlot(i);
            }
        }

        private void ConfigureSlotId(int slotIndex, string adUnitId)
        {
            var slot = slots[slotIndex];
            if (slot.AdUnitId == adUnitId)
            {
                return;
            }

            slot.AdUnitId = adUnitId;
            if (!slot.IsShowing)
            {
                DestroySlot(slotIndex);
            }
        }

        private bool HasConfiguredSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(slots[slotIndex].AdUnitId);
        }

        private void TryLoadSlot(int slotIndex)
        {
            var slot = slots[slotIndex];
            if (!HasConfiguredSlot(slotIndex) || slot.IsLoading || slot.IsShowing)
            {
                return;
            }

            if (slot.Ad != null)
            {
                if (slot.IsReady && slot.Ad.CanShowAd())
                {
                    return;
                }

                DestroySlot(slotIndex);
            }

            slot.IsLoading = true;
            DebugLogger.AVNLog($"AdMob AppOpen load requested for slot={slotIndex}");
            AppOpenAd.Load(slot.AdUnitId, new AdRequest(), (ad, error) => HandleLoadCompleted(slotIndex, ad, error));
        }

        private void HandleLoadCompleted(int slotIndex, AppOpenAd ad, LoadAdError error)
        {
            var slot = slots[slotIndex];
            slot.IsLoading = false;

            if (ad == null || error != null)
            {
                slot.IsReady = false;
                slot.Ad = null;
                var message = error != null
                    ? $"App-open slot {slotIndex} load failed: [{error.GetDomain()}/{error.GetCode()}] {error.GetMessage()}"
                    : $"App-open slot {slotIndex} load failed: ad was null";
                AdEvents.OnAppOpenLoadFailed?.Invoke(message);
                return;
            }

            DestroySlot(slotIndex);
            slot.Ad = ad;
            slot.IsReady = ad.CanShowAd();
            WireSlotEvents(slotIndex, ad);
            AdEvents.OnAppOpenLoaded?.Invoke($"App-open slot {slotIndex} loaded");
        }

        private void WireSlotEvents(int slotIndex, AppOpenAd ad)
        {
            var slot = slots[slotIndex];
            slot.OpenedHandler = () =>
            {
                AdEvents.OnAppOpenShown?.Invoke($"App-open slot {slotIndex} shown");
            };
            slot.ClosedHandler = () =>
            {
                slot.IsShowing = false;
                currentShowingSlot = -1;
                DestroySlot(slotIndex);
                AdEvents.OnAppOpenClosed?.Invoke($"App-open slot {slotIndex} closed");
                Load();
            };
            slot.FailedHandler = error =>
            {
                slot.IsShowing = false;
                currentShowingSlot = -1;
                DestroySlot(slotIndex);
                AdEvents.OnAppOpenFailToShow?.Invoke($"App-open slot {slotIndex} failed to show: [{error.GetDomain()}/{error.GetCode()}] {error.GetMessage()}");
                Load();
            };
            slot.ClickedHandler = () =>
            {
                DebugLogger.AVNLog($"AdMob AppOpen clicked slot={slotIndex}");
            };
            slot.ImpressionHandler = () =>
            {
                DebugLogger.AVNLog($"AdMob AppOpen impression slot={slotIndex}");
            };
            slot.OnAdPaidHandler = adValue =>
            {
                DebugLogger.AVNLog($"AdMob AppOpen paid event slot={slotIndex} value={adValue.Value} {adValue.CurrencyCode}");
                FinzAnalysisManager.Instance.PaidAdAnalytics(AdType.OPEN_AD.ToString(), slot.Ad.GetResponseInfo(), adValue);
            };

            ad.OnAdFullScreenContentOpened += slot.OpenedHandler;
            ad.OnAdFullScreenContentClosed += slot.ClosedHandler;
            ad.OnAdFullScreenContentFailed += slot.FailedHandler;
            ad.OnAdClicked += slot.ClickedHandler;
            ad.OnAdImpressionRecorded += slot.ImpressionHandler;
            ad.OnAdPaid += slot.OnAdPaidHandler;
        }

        private void DestroySlot(int slotIndex)
        {
            var slot = slots[slotIndex];
            if (slot.Ad != null)
            {
                if (slot.OpenedHandler != null)
                {
                    slot.Ad.OnAdFullScreenContentOpened -= slot.OpenedHandler;
                }

                if (slot.ClosedHandler != null)
                {
                    slot.Ad.OnAdFullScreenContentClosed -= slot.ClosedHandler;
                }

                if (slot.FailedHandler != null)
                {
                    slot.Ad.OnAdFullScreenContentFailed -= slot.FailedHandler;
                }

                if (slot.ClickedHandler != null)
                {
                    slot.Ad.OnAdClicked -= slot.ClickedHandler;
                }

                if (slot.ImpressionHandler != null)
                {
                    slot.Ad.OnAdImpressionRecorded -= slot.ImpressionHandler;
                }
                if (slot.OnAdPaidHandler != null)
                {
                    slot.Ad.OnAdPaid -= slot.OnAdPaidHandler;
                }

                slot.Ad.Destroy();
            }

            if (currentShowingSlot == slotIndex)
            {
                currentShowingSlot = -1;
            }

            slot.Ad = null;
            slot.IsLoading = false;
            slot.IsReady = false;
            slot.IsShowing = false;
            slot.OpenedHandler = null;
            slot.ClosedHandler = null;
            slot.FailedHandler = null;
            slot.ClickedHandler = null;
            slot.ImpressionHandler = null;
            slot.OnAdPaidHandler = null;
        }

        private int GetReadySlotIndex()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.Ad == null || slot.IsLoading || slot.IsShowing || !slot.IsReady)
                {
                    continue;
                }

                if (slot.Ad.CanShowAd())
                {
                    return i;
                }

                DestroySlot(i);
            }

            return -1;
        }

        private static string NormalizeId(string id) =>
            string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}
#endif