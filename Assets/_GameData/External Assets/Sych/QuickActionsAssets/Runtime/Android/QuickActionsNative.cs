using System;
using Sych.QuickActionsAssets.Runtime.Tools;
using UnityEngine;

namespace Sych.QuickActionsAssets.Runtime.Android
{
    internal sealed class QuickActionsNative : AndroidJavaProxy
    {
        private const string QuickActionsPackage = "com.sych.quick.actions.QuickActions";
        private const string UnityPlayerPackage = "com.unity3d.player.UnityPlayer";
        private static AndroidJavaObject _quickActionsInstance;
        private static AndroidJavaClass _unityPlayer;
        private static AndroidJavaObject _unityActivity;
        private static QuickActionsNative _instance;

        public static string LastPerformedQuickActionId =>
            _quickActionsInstance?.CallStatic<string>("getLastPerformedQuickActionId");

        public static event Action<string> QuickActionPerformed;

        private QuickActionsNative()
            : base($"{QuickActionsPackage}$QuickActionCallback") { }

        public void onQuickActionReceived(string actionId) =>
            QuickActionPerformed?.InvokeInUnityThread(actionId);

        public static void Initialize()
        {
            _unityPlayer = new AndroidJavaClass(UnityPlayerPackage);
            _unityActivity = _unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var jc = new AndroidJavaClass(QuickActionsPackage);
            _quickActionsInstance = jc.CallStatic<AndroidJavaObject>("getInstance");
            _quickActionsInstance.CallStatic("setIsAppOpened", true);
            _instance ??= new QuickActionsNative();
            _quickActionsInstance.Call("setQuickActionCallback", _instance);
        }

        public static bool AddQuickAction(string json) =>
            _quickActionsInstance != null
            && _quickActionsInstance.CallStatic<bool>("addQuickAction", _unityActivity, json);

        public static bool RemoveQuickAction(string id) =>
            _quickActionsInstance != null
            && _quickActionsInstance.CallStatic<bool>("removeQuickAction", _unityActivity, id);

        public static void RemoveAllQuickActions() =>
            _quickActionsInstance?.CallStatic("removeAllQuickActions", _unityActivity);

        public static void ResetLastPerformedQuickActionId() =>
            _quickActionsInstance?.CallStatic("resetLastPerformedQuickActionId");

        public static string GetAllQuickActions() =>
            _quickActionsInstance == null
                ? "[]"
                : _quickActionsInstance.CallStatic<string>("getAllQuickActions", _unityActivity);
    }
}
