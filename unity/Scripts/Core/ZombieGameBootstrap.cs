using UnityEngine;
using ZombieGame.UnityClient.Networking;

namespace ZombieGame.UnityClient.Core
{
  [DefaultExecutionOrder(-1000)]
  [DisallowMultipleComponent]
  public sealed class ZombieGameBootstrap : MonoBehaviour
  {
    public static ZombieGameAppContext? Context { get; private set; }

    [SerializeField] private GameRuntimeConfig? config;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureBootstrapExists()
    {
      if (FindAnyObjectByType<ZombieGameBootstrap>() is not null)
        return;

      var root = new GameObject("ZombieGame");
      DontDestroyOnLoad(root);
      root.AddComponent<UnityMainThreadDispatcher>();
      root.AddComponent<ZombieGameBootstrap>();
    }

    private void Awake()
    {
      if (Context is not null)
      {
        Destroy(gameObject);
        return;
      }

      DontDestroyOnLoad(gameObject);
      _ = UnityMainThreadDispatcher.Instance;

      if (config is null)
        config = Resources.Load<GameRuntimeConfig>("GameRuntimeConfig");

      if (config is null)
      {
        config = ScriptableObject.CreateInstance<GameRuntimeConfig>();
        Debug.LogWarning("GameRuntimeConfig not found in Resources. Using in-memory defaults.");
      }

      Context = ZombieGameAppContext.Create(config);
      Context.BeginStartup();
    }

    private void OnDestroy()
    {
      if (Context is null)
        return;

      _ = Context.DisposeAsync();
      Context = null;
    }
  }
}
