#if USE_AVNADS_PLUGIN
using System;
using UnityEngine;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AVN.AdsPlugin.Controllers
{
    [CreateAssetMenu(fileName = "AVNAdIdsConfig", menuName = "AVN Ads Plugin/Ad IDs Config")]
    public sealed class AVNAdIdsConfig : ScriptableObject
    {
        //------------------ Ad IDs ------------------//
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private string adsSDKKey = string.Empty;
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private string bannerAdUnitId = string.Empty;
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private string mrecAdUnitId = string.Empty;
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private string rewardedAdUnitId = string.Empty;
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private string[] interAdUnitIds = Array.Empty<string>();
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private string appOpenAdUnitId1 = string.Empty;
        [FoldoutGroup("Ad IDs")]
        [SerializeField] private string appOpenAdUnitId2 = string.Empty;

        public string AdsSDKKey
        {
            get => adsSDKKey;
            set => adsSDKKey = value ?? string.Empty;
        }

        public string BannerAdUnitId
        {
            get => bannerAdUnitId;
            set => bannerAdUnitId = value ?? string.Empty;
        }

        public string MrecAdUnitId
        {
            get => mrecAdUnitId;
            set => mrecAdUnitId = value ?? string.Empty;
        }

        public string RewardedAdUnitId
        {
            get => rewardedAdUnitId;
            set => rewardedAdUnitId = value ?? string.Empty;
        }

        public string[] InterAdUnitIds
        {
            get => interAdUnitIds ?? Array.Empty<string>();
            set => interAdUnitIds = value ?? Array.Empty<string>();
        }

        public string AppOpenAdUnitId1
        {
            get => appOpenAdUnitId1;
            set => appOpenAdUnitId1 = value ?? string.Empty;
        }

        public string AppOpenAdUnitId2
        {
            get => appOpenAdUnitId2;
            set => appOpenAdUnitId2 = value ?? string.Empty;
        }

        #if UNITY_EDITOR
        //------------------ Editor ------------------//
        [Space]
        [FoldoutGroup("Editor")]
        [SerializeField] private bool showIdsJsonPreview = false;
        [FoldoutGroup("Editor"), ShowIf(nameof(showIdsJsonPreview))]
        [ShowInInspector, ReadOnly, TextArea(8, 20)]
        private string adIdsJsonPreview = string.Empty;

        [FoldoutGroup("Editor"), ShowIf(nameof(showIdsJsonPreview))]
        [Button("Build IDs JSON", ButtonSizes.Large)]
        private void BuildAdIdsJson()
        {
            var data = new
            {
                adsSDKKey,
                bannerAdUnitId,
                mrecAdUnitId,
                rewardedAdUnitId,
                interAdUnitIds,
                appOpenAdUnitId1,
                appOpenAdUnitId2
            };

            adIdsJsonPreview = JsonUtility.ToJson(new SerializableAdIdsData
            {
                BannerAdUnitId = bannerAdUnitId,
                MrecAdUnitId = mrecAdUnitId,
                RewardedAdUnitId = rewardedAdUnitId,
                InterAdUnitIds = interAdUnitIds,
                AppOpenAdUnitId1 = appOpenAdUnitId1,
                AppOpenAdUnitId2 = appOpenAdUnitId2
            }, true);
        }

        [FoldoutGroup("Editor")]
        [Button("Copy All IDs to Clipboard", ButtonSizes.Large)]
        private void CopyIdsToClipboard()
        {
            BuildAdIdsJson();
            GUIUtility.systemCopyBuffer = adIdsJsonPreview;
            EditorUtility.DisplayDialog("Success", "Ad IDs JSON copied to clipboard!", "OK");
        }
        #endif
    }

    #if UNITY_EDITOR
    [System.Serializable]
    public class SerializableAdIdsData
    {
        public string BannerAdUnitId;
        public string MrecAdUnitId;
        public string RewardedAdUnitId;
        public string[] InterAdUnitIds;
        public string AppOpenAdUnitId1;
        public string AppOpenAdUnitId2;
    }
    #endif
}
#endif

