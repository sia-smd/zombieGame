# Room Architecture V2 — Production Extensions

This document extends the approved Room State Machine (V1). **V1 code is preserved**; V2 adds aggregates, stores, and cross-cutting services around it.

## Aggregate Hierarchy

```
Match (SQL + RoomState in Redis)
 └── Day (DayNumber on RoomState)
      └── Battle (IBattleStore — independent aggregate)
           ├── PlayerA / PlayerB
           ├── Status, BattleTimer
           ├── CardsPlayed (private)
           └── BattleResult (public summary)
 └── Finished → MatchSummary (Redis + optional SQL)
```

| Aggregate | Owns | Room Coordinates |
|-----------|------|------------------|
| **Room** | Phase, timers, invitations, votes, player list | Transitions, ready checks |
| **Battle** | 1v1 card play, pass, timer, public Action/Pass | Creates battles when pairs form; advances when all battles finish |
| **Match** | Persistence, players, session token, status | Entry fee, completion |

## State Machine (unchanged phases)

```
Lobby → DayStart → OpponentSelection → CardBattle → BattleResult
  → Discussion → Voting → VoteResult → (DayStart | Finished)
```

V2 adds **side effects** on every transition: snapshot, event log, player-active-match update.

## Redis Key Layout (V2)

| Key | Purpose |
|-----|---------|
| `{prefix}:room:{matchId}:core` | RoomState JSON |
| `{prefix}:room:{matchId}:battle:{battleId}` | Battle aggregate |
| `{prefix}:room:{matchId}:battles:day:{n}` | SET of battle IDs for day |
| `{prefix}:room:{matchId}:history:{playerId}` | Opponent history SET |
| `{prefix}:room:{matchId}:chat` | Discussion LIST |
| `{prefix}:room:{matchId}:snapshots` | LIST of phase snapshots |
| `{prefix}:room:{matchId}:events` | LIST append-only event log |
| `{prefix}:room:{matchId}:summary` | Final MatchSummary |
| `{prefix}:player:{playerId}:active` | PlayerActiveMatch |
| `{prefix}:match:{matchId}:lock` | Distributed transition lock |

TTL: `SessionTtlHours` on all room keys; cleared on `Finished`.

## Battle Aggregate

```csharp
Battle {
  BattleId, MatchId, DayNumber,
  PlayerA, PlayerB,
  Status: Pending | InProgress | Finished,
  BattleEndsAt,
  PlayerAFinished, PlayerBFinished,
  PlayerAPublicAction, PlayerBPublicAction,  // Action | Pass
  CardsPlayed: [{ PlayerId, CardId, TargetUserId, At }]
}
```

- **SignalR group:** `battle-{BattleId}`
- **JoinBattle:** server validates membership via `IBattleStore` + `IPlayerActiveMatchStore`
- **Card play:** delegated to `IBattleService` → `IGameRulesEngine`

## Reconnect Flow

```
Login → GET /api/match/active
  → HasActiveMatch?
      Yes → client shows Resume | Lobby
      Resume → RoomHub.JoinRoom + JoinBattle(if any) + SyncRoom + SyncBattle
      → PlayerActiveMatchStore.LastSeen updated
```

`PlayerActiveMatch` fields: `PlayerId`, `MatchId`, `SessionToken`, `CurrentBattleId`, `CurrentRoomPhase`, `DayNumber`, `IsAlive`, `Role`, `LastSeen`, `ConnectionRole`.

## Snapshots

Written after phase **exit** (via `IRoomSnapshotService`):

| Kind | When |
|------|------|
| `EndOfDay` | Leaving VoteResult → next DayStart |
| `EndOfBattle` | Leaving CardBattle → BattleResult |
| `EndOfDiscussion` | Discussion → Voting |
| `EndOfVoting` | Voting → VoteResult |
| `EndOfMatch` | Finished |

Payload: room phase, day, alive players, battle summaries, votes (if applicable), timestamp.

Recovery: on startup, `RoomRecoveryHostedService` loads latest snapshot per active match from Redis.

## Event Log (append-only)

`IMatchEventLogStore.AppendAsync(matchId, MatchEventEntry)`

Types: `MatchStarted`, `DayStarted`, `InvitationSent`, `InvitationAccepted`, `InvitationsCancelled`, `BattleStarted`, `BattleFinished`, `PlayerInfected`, `PlayerEliminated`, `DiscussionStarted`, `VotingStarted`, `VoteFinished`, `PhaseReady`, `PlayerDisconnected`, `PlayerReconnected`, `GameFinished`, `ChatExported`.

Used for: debugging, analytics, replay, moderation.

## Invitation Atomicity

Under `IRoomLock` (already held by `RoomStateMachine`):

1. B accepts A's invitation
2. `IInvitationCoordinator.OnInvitationAccepted(room, invitation)`:
   - Cancel all other pending invitations involving A or B
   - Reset `HasSentInvitationToday` for affected senders
   - Emit `InvitationsCancelledEvent` per cancelled invitation
   - Create Battle aggregate + BattlePair reference
   - Prevent duplicate pairs (check `room.IsPaired`)

## Opponent selection finalize (unmatched)

When OpponentSelection closes (timer or all unpaired ready):

1. **Exactly 2 unmatched** (human or bot): auto-pair them (`TryPairLastTwoUnmatched`), expire pending invites involving them, record history. Neither rests.
2. **Exactly 1 unmatched**: Rest Mode (default `UnmatchedPlayerRule.Skip`). Natural odd leftover.
3. **3+ unmatched**: no auto-pair; all of them rest for the day.

## Ready System

`RoomState.PhaseReadyPlayers: HashSet<Guid>`

Command: `MarkPhaseReadyCommand(userId)`

Each timed phase handler:
- On `MarkPhaseReady`: add user; if all **required** players ready → advance immediately
- Required sets: alive players (OpponentSelection: unpaired only; Battle: in active battle; etc.)
- Timer remains fallback via `OnTickAsync`

## Battle Turn (queue + finish)

`GameSettings.ActionsPerTurn = 2`

- Each card play queues one card and spends one action point (max 2 cards per turn).
- Effects resolve only after **both** players press **Finish** (`FinishTurnAsync`), then `ResolveBattleAsync` runs in priority order (shield → heal → shoot → infect).
- Playing cards does **not** mark a player finished; only `FinishTurnAsync`, `PassAsync` (alias), timer expiry, or disconnect grace does.
- A Pass card queued as the first action burns the remaining action point but still requires Finish (or auto-pass) to lock the turn.
- Bots call `FinishTurnAsync` after they have no remaining action points or nothing left to queue.

## Player Activity Badge

`RoomPlayerActivity` is projected server-side by `RoomActivityTracker.GetActivity` and shipped in
`RoomPlayerDto.Activity`: `Available`, `Inviting` (sent an invite), `Waiting` (must answer one),
`InBattle`, `Resting`, `Disconnected`, `Eliminated`.

## Disconnect Behavior

| Phase | Behavior |
|-------|----------|
| OpponentSelection | Player stays in pool until timeout |
| CardBattle | Auto Pass after `RoomSettings.BattleDisconnectGraceSeconds`, or on the battle timer |
| Discussion | Free reconnect |
| Voting | Miss vote after timeout; match continues |

`RoomHub.OnDisconnectedAsync` → update `LastSeen`, set `RoomPlayerState.DisconnectedAt`, broadcast
`RoomUpdated`; never block room. Join/Resume clears `DisconnectedAt`.

## AFK Protection

`RoomState` tracks `HasActedToday` (invite, respond, battle action, chat, vote) and
`InactiveDayCount`. `DayStartPhaseHandler` closes the previous day through
`RoomActivityTracker.CloseDay`: idle humans accumulate an inactive day and are removed once
`RoomSettings.AfkDayLimit` (default 3) is reached, followed by a win-condition check.

## Observer Roles (future)

```csharp
enum MatchConnectionRole { Player, Spectator, Admin }
```

- **Player:** full participation (validated commands)
- **Spectator:** public events only (`RoomUpdated`, battle summaries)
- **Admin:** `IRoomInspectorService.GetFullState` (internal)

No UI in V2 — role stored on connection context + `PlayerActiveMatch`.

## Match Summary (on Finished)

`MatchSummary`: Winner, WinningTeam, TotalDays, Battles, Infections, Kills, Eliminations, Votes, CardsPlayed, MostActivePlayer, Mvp, Duration.

Accumulated in `RoomState.Statistics` during play; finalized by `IMatchSummaryService`.

## Security Layer

`IRoomCommandValidator` checks before every hub action:

- Match membership + session token
- Current phase allows command
- Player alive (when required)
- Battle membership (battle commands)
- Role permissions (Player vs Spectator)

Hub methods call `RoomService` only — never trust client phase/battle IDs without server lookup.

## Horizontal Scaling

- All mutable state in Redis
- `IRoomLock` for atomic transitions
- SignalR backplane-ready groups (`room-{id}`, `battle-{id}`)
- Stateless API nodes; `RoomLoopHostedService` uses lock to avoid duplicate ticks

## V2 Production Extensions (implemented)

See sections 1–12 of the production spec:

- **Battle aggregate** — `IBattleStore`, `BattleService`, `BattleId == PairId`
- **Reconnect** — `IPlayerActiveMatchStore`, `IPlayerReconnectService`, `GET /api/match/active`, `ResumeMatch` hub method
- **Snapshots** — `IRoomSnapshotService` on phase completion
- **Event log** — append-only `IMatchEventLogStore`
- **Invitation races** — `InvitationCoordinator` under room lock
- **Ready system** — `MarkPhaseReady` + `PhaseReadyService`
- **Disconnect** — `OnDisconnectedAsync`, battle auto-pass after grace window or on timer
- **AFK** — `RoomActivityTracker.CloseDay` at DayStart
- **Observer roles** — `MatchConnectionRole`, `IRoomInspectorService`
- **Match summary** — `IMatchSummaryService` on finish
- **Security** — `IRoomCommandValidator` on all hub commands

```
Domain/Models/Room/Battle.cs
Domain/Models/Room/PlayerActiveMatch.cs
Domain/Models/Room/MatchEventEntry.cs
Domain/Models/Room/RoomSnapshot.cs
Domain/Models/Room/MatchSummary.cs
Domain/Enums/MatchEventType.cs, MatchConnectionRole.cs, SnapshotKind.cs

Application/Room/Battle/IBattleService.cs, BattleService.cs
Application/Room/InvitationCoordinator.cs
Application/Room/PhaseReadyService.cs
Application/Room/RoomCommandValidator.cs
Application/Room/RoomSnapshotService.cs
Application/Room/MatchEventLogService.cs
Application/Room/MatchSummaryService.cs
Application/Room/PlayerReconnectService.cs
Application/Services/ActiveMatchQueryService.cs

Infrastructure/Game/RedisBattleStore.cs (+ InMemory)
Infrastructure/Game/RedisPlayerActiveMatchStore.cs (+ InMemory)
Infrastructure/Game/RedisRoomSnapshotStore.cs (+ InMemory)
Infrastructure/Game/RedisMatchEventLogStore.cs (+ InMemory)

ZombieGame/Controllers/MatchRecoveryController.cs
```
