using UnityEngine;

namespace ZombieGame.UnityClient.Core
{
  [CreateAssetMenu(fileName = "GameRuntimeConfig", menuName = "ZombieGame/Game Runtime Config")]
  public sealed class GameRuntimeConfig : ScriptableObject
  {
    [Header("API")]
    [Tooltip("Development: http://localhost:5232 — Android emulator: http://10.0.2.2:5232")]
    public string baseUrl = "http://localhost:5232";

    [Header("Client")]
    public bool autoRegisterGuestOnStart;
    public bool restoreSessionFromPrefs = true;
    public float queuePollIntervalSeconds = 2f;
    public float queueTimeoutSeconds = 120f;

    public string BaseUrl => baseUrl.TrimEnd('/');
  }
}
