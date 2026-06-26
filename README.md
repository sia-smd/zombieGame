# ZombieGame Backend (MVP)

Multiplayer social deduction game backend — .NET 9, Clean Architecture, EF Core Code First, SignalR.

## Solution Structure

```
src/
  ZombieGame.Domain/        Entities, enums, repository interfaces
  ZombieGame.Application/   Services, DTOs, business logic
  ZombieGame.Infrastructure/  EF Core, repositories, auth, cards
ZombieGame/                 API layer (controllers, SignalR hub)
unity/                      Unity client networking stubs
```

## Prerequisites

- .NET 9 SDK
- SQL Server or LocalDB

## Run

```bash
cd ZombieGame
dotnet run --project ZombieGame/ZombieGame.csproj
```

Swagger UI: `https://localhost:7xxx/swagger` (see launchSettings for port)

Database migrations run automatically on startup.

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `GameSettings:MatchmakingPlayerCount` | 8 (2 in Development) | Players needed to start a match |
| `GameSettings:MatchEntryFeeCoins` | 2 | Coins deducted on game start |
| `GameSettings:MatchWinRewardCoins` | 4 | Coins awarded to winner |
| `GameSettings:StartingCoins` | 10 | New user balance |

## Architecture Highlights

- **Server authoritative** — all game state and coin logic on server
- **Idempotent actions** — `GameActionLog` with unique idempotency keys
- **Data-driven cards** — `CardDefinition` table + `ICardRegistry`
- **In-memory sessions** — `IGameSessionStore` for active match state (Redis-ready)
- **Extensible** — repositories, transaction log, and action log ready for anti-cheat, leaderboards, ranked MM

## SignalR Hub

Connect: `/hubs/game?access_token={jwt}`

Methods: `JoinMatch`, `StartGame`, `PlayCard`, `EndTurn`, `VotePlayer`, `SyncState`

Events: `SyncState`, `GameEvent`

## EF Migrations

```bash
dotnet ef migrations add MigrationName \
  --project src/ZombieGame.Infrastructure/ZombieGame.Infrastructure.csproj \
  --startup-project ZombieGame/ZombieGame.csproj \
  --output-dir Persistence/Migrations
```
