using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Sych.QuickActionsAssets.Runtime.Tools
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> ExecutionQueue = new();
        private static UnityMainThreadDispatcher _instance;

        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance != null) 
                return _instance;
           
            var obj = new GameObject("UnityMainThreadDispatcher");
            _instance = obj.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(obj);
            return _instance;
        }

        public void Update()
        {
            lock (ExecutionQueue)
                while (ExecutionQueue.TryDequeue(out var action))
                    action?.Invoke();
        }

        public void Enqueue(Action action)
        {
            lock (ExecutionQueue)
                ExecutionQueue.Enqueue(action);
        }
    }
    
    public static class UnityMainThreadDispatcherExtensions
    {
        public static void InvokeInUnityThread(this Action action) => UnityMainThreadDispatcher.Instance().Enqueue(action);

        public static void InvokeInUnityThread<T>(this Action<T> action, T arg) => UnityMainThreadDispatcher.Instance().Enqueue(() => action(arg));
    }
}