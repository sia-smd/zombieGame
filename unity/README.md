# ZombieGame Unity Client Integration (MVP)

This folder contains C# stubs for connecting a Unity client to the ZombieGame backend.
No graphics or gameplay UI — networking layer only.

## Prerequisites

- Unity 2022.3+ (or compatible)
- Install NuGet packages in Unity via `NuGetForUnity` or copy DLLs:
  - `Microsoft.AspNetCore.SignalR.Client`
  - `System.Net.Http.Json`

## Backend Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | No | Register with username, phone, password |
| POST | `/api/auth/login` | No | Login with phone + password → JWT |
| GET | `/api/auth/profile` | Bearer | Get user profile + coins |
| POST | `/api/matchmaking/queue/join` | Bearer | Join matchmaking queue |
| POST | `/api/matchmaking/queue/leave` | Bearer | Leave queue |
| GET | `/api/matchmaking/queue/status` | Bearer | Queue status |
| GET | `/api/matchmaking/matches/{matchId}` | Bearer | Match summary |
| GET | `/api/matchmaking/matches/{matchId}/players` | Bearer | Match players |

## SignalR Hub

- URL: `{baseUrl}/hubs/game?access_token={jwt}`
- Hub methods (client → server):
  - `JoinMatch(matchId, sessionToken)`
  - `StartGame(matchId, sessionToken)`
  - `PlayCard(matchId, sessionToken, { cardId, targetUserId, idempotencyKey })`
  - `EndTurn(matchId, sessionToken, { idempotencyKey })`
  - `VotePlayer(matchId, sessionToken, { targetUserId, idempotencyKey })`
  - `SyncState(matchId, sessionToken)`
- Server events (server → client):
  - `SyncState` — full authoritative game state
  - `GameEvent` — action result messages

## Security Notes

- All coin changes happen server-side only.
- Every action requires a unique `idempotencyKey` (GUID string).
- Match access requires valid `sessionToken` from match creation response.

## Dev Settings

Lower `GameSettings:MatchmakingPlayerCount` in `appsettings.Development.json` to test with fewer players (e.g. 2).
