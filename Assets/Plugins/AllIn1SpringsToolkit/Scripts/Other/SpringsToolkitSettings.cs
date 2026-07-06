using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AllIn1SpringsToolkit
{
    public static class SpringsToolkitSettings
    {
        private static SpringsToolkitSettingsAsset instance;
        
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            instance = Resources.Load<SpringsToolkitSettingsAsset>("SpringsToolkitSettings");
        }
#endif

        private static SpringsToolkitSettingsAsset Instance
        {
            get
            {
#if UNITY_EDITOR
                if(instance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("SpringsToolkitSettings t:ScriptableObject");
                    if(guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        instance = AssetDatabase.LoadAssetAtPath<SpringsToolkitSettingsAsset>(path);
                    }
                
                    if(instance == null)
                    {
                        Debug.LogWarning("SpringsToolkitSettings not found. Using default settings.");
                        instance = ScriptableObject.CreateInstance<SpringsToolkitSettingsAsset>();
                    }
                }
#endif

                return instance;
            }
        }

        // Convenience Properties
        public static bool DoFixedUpdateRate
        {
            get => Instance.doFixedUpdateRate;
            set => Instance.doFixedUpdateRate = value;
        }

        public static float SpringFixedTimeStep
        {
            get => Instance.springFixedTimeStep;
            set => Instance.springFixedTimeStep = value;
        }
        
        public static float MaxForceBeforeAnalyticalIntegration
        {
            get => Instance.maxForceBeforeAnalyticalIntegration;
            set => Instance.maxForceBeforeAnalyticalIntegration = value;
        }

        public static void SaveChanges()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();
#endif
        }
    }
}