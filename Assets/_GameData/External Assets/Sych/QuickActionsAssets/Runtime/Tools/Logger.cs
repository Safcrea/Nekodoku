using System;
using UnityEngine;

namespace Sych.QuickActionsAssets.Runtime.Tools
{
    internal static class Logger
    {
        public static bool LoggingEnable { get; set; } = true;

        public static void Log(string tag, object message)
        {
            if (!LoggingEnable)
                return;

            Debug.unityLogger.Log(LogType.Log, tag, message);
        }

        public static void Warning(string tag, object message)
        {
            if (!LoggingEnable)
                return;

            Debug.unityLogger.Log(LogType.Warning, tag, message);
        }

        public static void Error(string tag, object message)
        {
            if (!LoggingEnable)
                return;

            Debug.unityLogger.Log(LogType.Error, tag, message);
        }

        public static void Exception(string tag, Exception exception)
        {
            if (!LoggingEnable)
                return;

            Debug.unityLogger.Log(LogType.Exception, tag, exception);
        }
    }
}