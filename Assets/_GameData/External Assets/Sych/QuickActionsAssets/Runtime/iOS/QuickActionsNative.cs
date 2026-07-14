using System;
using System.Runtime.InteropServices;

namespace Sych.QuickActionsAssets.Runtime.iOS
{
    public static class QuickActionsNative
    {
        #region Native

        private const string DllName = "__Internal";

        [DllImport(DllName)]
        private static extern bool addQuickAction(string json);

        [DllImport(DllName)]
        private static extern IntPtr getAllQuickActions();

        [DllImport(DllName)]
        private static extern bool removeQuickAction(string id);

        [DllImport(DllName)]
        private static extern void removeAllQuickActions();

        [DllImport(DllName)]
        private static extern IntPtr getLastPerformedQuickActionId();

        [DllImport(DllName)]
        private static extern void resetLastPerformedQuickActionId();

        [DllImport(DllName)]
        private static extern void RegisterQuickActionCallback(QuickActionDelegate callback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QuickActionDelegate(IntPtr itemId);

        [AOT.MonoPInvokeCallback(typeof(QuickActionDelegate))]
        private static void QuickActionCallback(IntPtr id) =>
            QuickActionPreformed?.Invoke(id.ConvertToString());

        #endregion

        private static bool _isQuickActionCallbackRegistered;
        internal static string LastPerformedQuickActionId =>
            getLastPerformedQuickActionId().ConvertToString();

        internal static event Action<string> QuickActionPreformed;

        public static void ResetLastPerformedQuickActionId() => resetLastPerformedQuickActionId();

        internal static void EnsureQuickActionCallbackRegistered()
        {
            if (_isQuickActionCallbackRegistered)
                return;

            RegisterQuickActionCallback(QuickActionCallback);
            _isQuickActionCallbackRegistered = true;
        }

        internal static bool AddQuickAction(string json) => addQuickAction(json);

        internal static bool RemoveQuickAction(string id) => removeQuickAction(id);

        internal static void RemoveAllQuickActions() => removeAllQuickActions();

        internal static string GetAllQuickActions() => getAllQuickActions().ConvertToString();

        private static string ConvertToString(this IntPtr stringPtr) =>
            IntPtr.Zero == stringPtr ? null : Marshal.PtrToStringAuto(stringPtr);

        public static string GetLastPerformedQuickActionId()
        {
            return QuickActionsNative.LastPerformedQuickActionId;
        }
    }
}
