using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>
    /// Dispatches callbacks to Unity main thread (required for SignalR → UI updates).
    /// Add once to a scene: GameObject + this component, or it auto-creates.
    /// </summary>
    public sealed class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher? _instance;
        private readonly ConcurrentQueue<Action> _queue = new();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance is not null)
                    return _instance;

                var go = new GameObject(nameof(UnityMainThreadDispatcher));
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        public static void Enqueue(Action action) => Instance._queue.Enqueue(action);

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
                action();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
