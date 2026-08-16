# ZombieGame Unity Client

Networking layer for connecting a Unity client to the ZombieGame backend.

## Prerequisites

- Unity 2022.3+ (or compatible)
- Packages (via NuGetForUnity or copied DLLs):
  - `Microsoft.AspNetCore.SignalR.Client`
  - `System.Net.Http.Json`

## Project layout

```
unity/Scripts/Networking/
  Models/
    GameEnums.cs              # GamePhase, PlayerRole, AccountType, ...
    GameStateDtos.cs          # SyncState payload + hub request DTOs
    AccountDtos.cs            # Guest/profile REST DTOs
  NetworkingJson.cs           # Shared camelCase JSON options
  ApiException.cs             # HTTP error helper
  ZombieGameApiClient.cs      # REST (account, profile, matchmaking)
  GameHubClient.cs            # SignalR gameplay hub (live implementation)
  ZombieGameSession.cs        # High-level session (auth + queue + hub)
  ZombieGameClientBehaviour.cs # MonoBehaviour for scene wiring
  UnityMainThreadDispatcher.cs # SignalR → main thread
  PlayerSessionPrefs.cs       # PlayerPrefs token storage (dev)
unity/packages.config         # NuGetForUnity: SignalR.Client
unity/SETUP.md                # Step-by-step Unity install
```

## Dev backend

Default Development URL: `http://localhost:5232`

In `appsettings.Development.json` the database uses **LocalDB**:

```
Server=(localdb)\mssqllocaldb;Database=ZombieGameDb;...
```

For faster testing set `GameSettings:MatchmakingPlayerCount` to `2`.

---

## REST API

### Account (guest-first flow)

| Method | Path | Auth | Body | Description |
|--------|------|------|------|-------------|
| POST | `/api/account/register-guest` | No | `{ deviceId, platform, appVersion }` | Create guest player + tokens |
| POST | `/api/account/refresh-token` | No | `{ refreshToken }` | Rotate access/refresh tokens |
| POST | `/api/account/logout` | Bearer | — | Deactivate current session |
| POST | `/api/account/add-mobile` | Bearer | `{ mobileNumber }` | Link mobile (verification pending) |

`platform`: `0` = Android, `1` = iOS

**register-guest response:**

```json
{
  "playerId": "guid",
  "accessToken": "jwt",
  "refreshToken": "opaque",
  "accessTokenExpiresAt": "...",
  "refreshTokenExpiresAt": "...",
  "profile": { "name", "imageId", "level", "coins", "wins", "losses" }
}
```

### Profile

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/profile/me` | Bearer | Full player profile + stats + inventory |
| PUT | `/api/profile/update` | Bearer | `{ name, imageId }` |

### Account login

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/account/login` | No | Session-backed login with verified mobile, device, and password |

Official accounts are created through `register-guest → add-mobile → verify-mobile → change-password`. Direct `/api/auth/register` and `/api/auth/login` were removed.

### Matchmaking

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/matchmaking/queue/join` | Bearer | Join queue; returns `matchId` + `sessionToken` when filled |
| POST | `/api/matchmaking/queue/leave` | Bearer | Leave queue |
| GET | `/api/matchmaking/queue/status` | Bearer | `{ inQueue, queueCount }` |
| GET | `/api/matchmaking/matches/{matchId}` | Bearer | Match summary |
| GET | `/api/matchmaking/matches/{matchId}/players` | Bearer | Players in match |

---

## SignalR Game Hub

**URL:** `{baseUrl}/hubs/game?access_token={jwt}`

### Client → Server

| Method | Parameters | Description |
|--------|------------|-------------|
| `JoinMatch` | `matchId`, `sessionToken` | Join group + receive `SyncState` (reconnect) |
| `StartGame` | `matchId`, `sessionToken` | Start match (lobby only) |
| `PlayCard` | `matchId`, `sessionToken`, `PlayCardRequest` | Play a card |
| `PassAction` | `matchId`, `sessionToken`, `PassActionRequest` | Pass (consume action point) |
| `EndTurn` | `matchId`, `sessionToken`, `EndTurnRequest` | End day → discussion |
| `VotePlayer` | `matchId`, `sessionToken`, `VotePlayerRequest` | Cast vote |
| `SyncState` | `matchId`, `sessionToken` | Request current state |

**Request DTOs** (`Models/GameStateDtos.cs`):

```csharp
PlayCardRequest(cardId, targetUserId?, idempotencyKey)
PassActionRequest(idempotencyKey)
EndTurnRequest(idempotencyKey)
VotePlayerRequest(targetUserId, idempotencyKey)
```

Every mutating action needs a **unique** `idempotencyKey` (e.g. `Guid.NewGuid().ToString("N")`).

### Server → Client

| Event | Payload | Description |
|-------|---------|-------------|
| `SyncState` | `GameStateDto` | Authoritative game state |
| `GameEvent` | `{ success, message }` | Action result message |

### SyncState shape (`GameStateDto`)

```json
{
  "matchId": "guid",
  "sessionToken": "string",
  "currentPhase": 1,
  "turnNumber": 1,
  "phaseEndsAt": "2026-06-26T12:00:00Z",
  "players": [
    {
      "userId": "guid",
      "username": "Guest123456",
      "role": 1,
      "isAlive": true,
      "seatIndex": 0,
      "hasShield": false,
      "remainingActions": 2,
      "hasRevealedThisDay": false,
      "isInfectedTeam": false
    }
  ],
  "playerHands": [
    {
      "userId": "guid",
      "cardIds": ["guid", "guid"],
      "disabledCardIds": []
    }
  ]
}
```

**Phases:** `0` Lobby, `1` Day, `2` Discussion, `3` Voting, `4` Resolution

**Roles:** `0` Unknown, `1` Human, `2` Zombie, `3` PowerZombie

> Vote tallies are resolved server-side. The current `GameStateDto` does not include a `votes` map; during Voting UI use local selection then call `VotePlayer`.

---

## Quick start (C#)

```csharp
await using var session = new ZombieGameSession("http://localhost:5232");
await session.RegisterGuestAsync(
    SystemInfo.deviceUniqueIdentifier,
    DevicePlatform.Android,
    Application.version);

var queue = await session.JoinQueueAndWaitAsync(
    pollInterval: TimeSpan.FromSeconds(2),
    timeout: TimeSpan.FromMinutes(2));

await session.ConnectHubAsync();
await session.JoinMatchAsync();
session.StateChanged += state => Debug.Log($"Phase: {state.CurrentPhase}");
await session.StartGameAsync();
```

Or drop `ZombieGameClientBehaviour` on a GameObject and wire UI buttons — see `SETUP.md`.

---

## Security notes

- Coin balance and game rules are **server-authoritative**.
- Never trust client-side state over `SyncState`.
- Store `refreshToken` securely (Keychain / Keystore).
- Match access requires both JWT and per-match `sessionToken`.
