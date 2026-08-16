using System;
using System.Threading;
using System.Threading.Tasks;
using ZombieGame.UnityClient.Networking;
using ZombieGame.UnityClient.Networking.Models;

namespace ZombieGame.UnityClient.Core
{
  public sealed class ZombieGameAppContext : IAsyncDisposable
  {
    private readonly GameRuntimeConfig _config;
    private CancellationTokenSource? _startupCts;

    public ZombieGameSession Session { get; }
    public GameRuntimeConfig Config => _config;
    public bool IsReady { get; private set; }
    public string? StartupError { get; private set; }

    public event Action? Ready;
    public event Action<string>? StartupFailed;

    private ZombieGameAppContext(GameRuntimeConfig config)
    {
      _config = config;
      Session = new ZombieGameSession(config.BaseUrl);
      WireSession(Session);
    }

    public static ZombieGameAppContext Create(GameRuntimeConfig config) => new(config);

    public void BeginStartup()
    {
      if (IsReady || _startupCts is not null)
        return;

      _startupCts = new CancellationTokenSource();
      _ = RunStartupAsync(_startupCts.Token);
    }

    private async Task RunStartupAsync(CancellationToken ct)
    {
      try
      {
        if (_config.restoreSessionFromPrefs
            && PlayerSessionPrefs.TryRestoreTokens(Session)
            && await Session.TryRefreshTokenAsync(ct))
        {
          await Session.LoadProfileAsync(ct);
          PlayerSessionPrefs.Save(Session);
          MarkReady();
          return;
        }

        if (_config.autoRegisterGuestOnStart)
        {
          await Session.RegisterGuestAsync(ct);
          PlayerSessionPrefs.Save(Session);
        }

        MarkReady();
      }
      catch (Exception ex)
      {
        StartupError = ex.Message;
        StartupFailed?.Invoke(ex.Message);
      }
    }

    private void MarkReady()
    {
      IsReady = true;
      Ready?.Invoke();
    }

    private static void WireSession(ZombieGameSession session)
    {
      session.StateChanged += _ => PlayerSessionPrefs.Save(session);
      session.StatusMessage += _ => PlayerSessionPrefs.Save(session);
    }

    public async ValueTask DisposeAsync()
    {
      _startupCts?.Cancel();
      _startupCts?.Dispose();
      await Session.DisposeAsync();
    }
  }
}
