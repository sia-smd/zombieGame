# Unity Setup — ZombieGame Client

## 1. Copy scripts

Copy `unity/Scripts` into your Unity project under `Assets/ZombieGame/Scripts`.

## 2. Install NuGet packages (SignalR)

Use [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) or add DLLs manually:

| Package | Version (min) |
|---------|----------------|
| `Microsoft.AspNetCore.SignalR.Client` | 8.0.x |
| `Microsoft.AspNetCore.SignalR.Protocols.Json` | 8.0.x |
| `System.Net.Http.Json` | (included in .NET Standard 2.1 / Unity 2022+) |

After install, Unity should resolve `Microsoft.AspNetCore.SignalR.Client` for `GameHubClient.cs`.

## 3. Player Settings

- **Api Compatibility Level:** .NET Standard 2.1 or .NET Framework 4.x (with compatible packages)
- For **Android** dev builds against `http://localhost:10.0.2.2:5232` (emulator) enable cleartext HTTP in manifest or use HTTPS.

## 4. Scene setup

1. Create empty GameObject `ZombieGameClient`
2. Add component `ZombieGameClientBehaviour`
3. Set `Base Url` = `http://localhost:5232` (or your machine IP for device builds)
4. Wire UI buttons:
   - `RegisterGuest()` — guest login
   - `JoinQueue()` — matchmaking + hub join
   - `StartGame()` — start match
   - `Pass()` / `EndTurn()` / `SyncState()`

Subscribe to `Changed` event to refresh UI when `LatestState` updates.

## 5. Backend

```powershell
cd d:\siavash\zombie\ZombieGame\ZombieGame
dotnet run --launch-profile http
```

Development DB: `(localdb)\mssqllocaldb` / `ZombieGameDb`

Set `MatchmakingPlayerCount: 2` in `appsettings.Development.json` for quick tests.

## 6. Code-first usage (no MonoBehaviour)

```csharp
await using var session = new ZombieGameSession("http://localhost:5232");
await session.RegisterGuestAsync(deviceId, DevicePlatform.Android, "1.0.0");
var queue = await session.JoinQueueAsync();
if (!queue.Queued)
{
    await session.ConnectHubAsync();
    await session.JoinMatchAsync();
    await session.StartGameAsync();
}
```
