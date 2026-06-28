using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ZombieGame.UnityClient.Networking.Models;

namespace ZombieGame.UnityClient.Networking
{
    /// <summary>
    /// Drop on a GameObject in your first scene. Wire UI buttons to public methods.
    /// </summary>
    public sealed class ZombieGameClientBehaviour : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string baseUrl = "http://localhost:5232";
        [SerializeField] private DevicePlatform platform = DevicePlatform.Android;
        [SerializeField] private string appVersion = "1.0.0";

        private ZombieGameSession? _session;
        private CancellationTokenSource? _cts;

        public GameStateDto? LatestState { get; private set; }
        public CurrentPlayerProfileResponse? Profile { get; private set; }
        public string? LastStatus { get; private set; }
        public string? LastError { get; private set; }

        public event Action? Changed;

        private void Awake()
        {
            _ = UnityMainThreadDispatcher.Instance;
            _session = new ZombieGameSession(baseUrl);
            _session.StateChanged += state => Dispatch(() =>
            {
                LatestState = state;
                Changed?.Invoke();
            });
            _session.GameEventReceived += evt => Dispatch(() =>
            {
                LastStatus = evt.Message;
                Changed?.Invoke();
            });
            _session.StatusMessage += msg => Dispatch(() =>
            {
                LastStatus = msg;
                Changed?.Invoke();
            });
            _session.Error += ex => Dispatch(() =>
            {
                LastError = ex.Message;
                Debug.LogError(ex);
                Changed?.Invoke();
            });
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            if (_session is not null)
                _ = _session.DisposeAsync();
        }

        public async void RegisterGuest()
        {
            await RunSafe(async ct =>
            {
                await _session!.RegisterGuestAsync(SystemInfo.deviceUniqueIdentifier, platform, appVersion, ct);
                Profile = await LoadProfile(ct);
                LastStatus = $"Logged in as {Profile?.Name}";
            });
        }

        public async void JoinQueue()
        {
            await RunSafe(async ct =>
            {
                var queue = await _session!.JoinQueueAsync(ct);
                if (!queue.Queued && queue.MatchId is not null)
                {
                    await _session.ConnectHubAsync(ct);
                    await _session.JoinMatchAsync(ct);
                    LastStatus = "Joined match lobby.";
                }
            });
        }

        public async void StartGame()
        {
            await RunSafe(ct => _session!.StartGameAsync(ct));
        }

        public async void Pass()
        {
            await RunSafe(ct => _session!.PassAsync(ct));
        }

        public async void EndTurn()
        {
            await RunSafe(ct => _session!.EndTurnAsync(ct));
        }

        public async void SyncState()
        {
            await RunSafe(ct => _session!.RequestSyncStateAsync(ct));
        }

        public async void PlayCard(string cardIdText, string targetUserIdText)
        {
            if (!Guid.TryParse(cardIdText, out var cardId))
            {
                LastError = "Invalid card id.";
                return;
            }

            Guid? targetId = Guid.TryParse(targetUserIdText, out var tid) ? tid : null;
            await RunSafe(ct => _session!.PlayCardAsync(cardId, targetId, ct));
        }

        private async Task<CurrentPlayerProfileResponse?> LoadProfile(CancellationToken ct)
        {
            await _session!.LoadProfileAsync(ct);
            Profile = _session.Profile;
            return Profile;
        }

        private async void RunSafe(Func<CancellationToken, Task> action)
        {
            try
            {
                LastError = null;
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                await action(_cts.Token);
                Dispatch(() => Changed?.Invoke());
            }
            catch (Exception ex)
            {
                Dispatch(() =>
                {
                    LastError = ex.Message;
                    Debug.LogException(ex);
                    Changed?.Invoke();
                });
            }
        }

        private static void Dispatch(Action action) =>
            UnityMainThreadDispatcher.Enqueue(action);
    }
}
