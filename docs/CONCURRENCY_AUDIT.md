# Concurrency audit — Room / Battle

Date: 2026-09-09  
Scope: RoomState, Battle, GameSessionState (room path + legacy GameHub), IRoomLock, RoomLoopHostedService.

Tests were executed, including overlapping `Task.WhenAll` loops (50–100 iterations).  
This document does **not** claim “no races.” Remaining risks are listed at the end.

---

## Lock implementation (`IRoomLock`)

| Question | Answer |
|---|---|
| Distributed? | **Yes, when Redis is enabled.** `RedisMatchLock` uses `SET key NX PX ttl`. |
| Redis key | `{KeyPrefix}:match:{matchId}:lock` — **same key** for RoomStateMachine and GameService. |
| Ownership | Random GUID token. Release is Lua `GET` == token then `DEL`. |
| Lease / TTL | Was hardcoded **5 seconds**. Now `RoomSettings.LockTtlSeconds` (**30**). |
| Crash | Process crash: lock expires with TTL. Safe. |
| Expire while still in critical section? | **Yes.** There is no heartbeat/extension. If DayStart + SQL fees exceed the lease, another node can enter. |
| Blocking vs TryAcquire | Adapter is **TryAcquire** (non-blocking). `RoomStateMachine` / `GameService` **retry** until `LockWaitMilliseconds` (default 2000). |
| After wait timeout | Dispatch / StartGame / RemovePlayer throw `Room is busy. Try again.` Tick returns Stay (skips). |
| Two instances own the lock? | Only if the lease expires or Redis `SET NX` is bypassed. |
| Re-entrant? | **No.** Nested acquire from the same flow would fail/wait. Callers must not re-enter. |
| In-memory adapter | `SemaphoreSlim(1,1)`, `WaitAsync(TimeSpan.Zero)`. **Ignores TTL.** Single process only. |

---

## Mutation map (writers)

### RoomState (`IRoomStateStore`)

Redis `StringSet` last-write-wins. `SnapshotVersion++` is **not** compare-and-swap.

| Method | Lock? (after this audit) |
|---|---|
| `DispatchAsync` | Required |
| `TickAsync` | Try (wait, then skip) |
| `StartGameAsync` | Required (**was none**) |
| `SyncPlayersFromMatchAsync` | Try; skip write if busy (**was none**) |
| `RemovePlayerFromLobbyAsync` | Required (**used to write even when acquire failed**) |
| `DiscardLobbyRoomAsync` | Required (**same bug**) |
| `SetPresenceAsync` | Try; skip write if busy |
| `InitializeRoomAsync` | `TryAdd` only; **no longer `Set` over an existing room** |
| `GetPublicStateAsync` | **No write** (removed `SyncPlayers` from the GET path) |

### Battle (`IBattleStore`)

Redis `StringSet` last-write-wins. **No version / CAS.**

All production callers are phase handlers invoked from `DispatchAsync` / `TickAsync` / `StartGameAsync` **after** the room lock is held:

- `BattleService.PlayCardAsync` / `FinishTurnAsync` / `PassAsync` / `AutoPassExpiredAsync` / `StartBattlesForDayAsync` / `CreateBattleAsync`
- `CardBattlePhaseHandler`, `BattlePreparationPhaseHandler`, `OpponentSelectionPhaseHandler`

`BattleService` itself does **not** take the lock. Calling it without the room lock is a concurrency boundary violation.

**Proof:** `BattleLostUpdateTests.OverlappingGetThenSave_LosesTheFirstPlayersCard`  
Interleaving: A Get → B Get → A Save shotgun → B Save heal on stale copy → shotgun disappears.

### GameSessionState (legacy GameHub)

`GameService` previously Get/mutate/`SetAsync` with **no lock**. Now uses the **same** `IRoomLock` key.

### Match (SQL)

`StartGame` still updates `Match.Status` in EF **outside** the room lock in `RoomService` (after `StartGameAsync`). Duplicate StartGame from two tabs: state machine is now serialized; SQL `InProgress` update can still race with itself (idempotent enough). Fees are charged in DayStart `OnEnter` under the lock; `CoinService` also skips duplicate `MatchEntryFee` rows.

---

## Findings

### 1. Battle last-write-wins without lock

**Severity:** CRITICAL (if BattleService is used without the room lock)  
**Location:** `BattleService.PlayCardAsync` / `FinishTurnAsync` + `RedisBattleStore.SaveAsync`  
**Race:** Two `PlayCardAsync` on the same battle.  
**Interleaving:** A1 Get, B1 Get, A2 Save, B2 Save stale.  
**Impact:** Lost queued card; `ActionsUsedThisTurn` on RoomState can disagree with `CardsPlayed`.  
**Proof:** `BattleLostUpdateTests.OverlappingGetThenSave_LosesTheFirstPlayersCard`  
**Fix:** All hub/tick paths hold `IRoomLock`. Documented on `IBattleService`. No Battle CAS (callers are locked).  
**Regression:** `BattleCommandConcurrencyTests.SamePlayer_TwoPlayCard_NeverExceedsActionsPerTurn` (100×)

### 2. `StartGameAsync` had no lock

**Severity:** CRITICAL  
**Location:** `RoomStateMachine.StartGameAsync`  
**Race:** Two tabs `StartGame` **or** StartGame + Tick.  
**Interleaving:** A Get Lobby, B Get Lobby, A DayStart OnEnter (fees + roles), B DayStart OnEnter, last Set wins.  
**Impact:** Double `CollectMatchEntryFeesAsync`, double role assignment on copies, clobbered room.  
**Fix:** Acquire required lock, re-read, no-op if not Lobby.  
**Regression:** `RoomLockConcurrencyTests.ConcurrentStartGame_DayStartOnce` (50×)

### 3. Unlocked RoomState writes

**Severity:** CRITICAL  
**Location:** `SyncPlayersFromMatchAsync`, `GetPublicStateAsync` (was a writer), `InitializeRoomAsync` fallback `Set`, `RemovePlayerFromLobbyAsync` / `DiscardLobbyRoomAsync` when `TryAcquire` returned null.  
**Race:** SyncRoom GET vs PlayCard; failed lock still mutated lobby.  
**Impact:** Clobber in-progress battle / action points; lobby strip without exclusion.  
**Fix:** Sync takes lock (skip if busy); GET path no longer writes; Initialize does not overwrite; Remove/Discard require lock.  
**Regression:** `RoomLockConcurrencyTests.SyncPlayers_SkipsWhenLockHeld`, `RemovePlayer_WithoutLock_ThrowsBusy`

### 4. Lock wait was zero — second player got “busy” or Tick skipped

**Severity:** HIGH  
**Location:** `AcquireLockAsync` used a single TryAcquire.  
**Race:** Two players in the same battle `PlayCard` at once.  
**Impact:** One action rejected; without client retry the other card never queued. With cloning store and **Always** lock, Save could also clobber.  
**Fix:** Retry acquire up to `LockWaitMilliseconds` so overlapping valid plays serialize.  
**Regression:** `BattleCommandConcurrencyTests.TwoPlayers_BothValidCardsSurvive` (100×)

### 5. 5s lock lease

**Severity:** HIGH  
**Location:** `RoomStateMachine.AcquireLockAsync`  
**Impact:** DayStart fees / slow SQL can outlive the lock; Node B Tick enters the same match.  
**Fix:** TTL 30s via `RoomSettings.LockTtlSeconds`.  
**Remaining:** still no lock extension.

### 6. Dual store crash window (Battle then Room)

**Severity:** MEDIUM  
**Location:** `CardBattlePhaseHandler.HandlePlayCardAsync` → `BattleService` `SaveAsync` then `RoomStateMachine.ApplyTransitionAsync` `SetAsync`.  
**Race:** Process crash / Redis failure between the two writes.  
**Impact:** Card queued in Battle, Room `ActionsUsedThisTurn` not updated (or reverse if order changes).  
**Fix:** Not introducing a Redis transaction. Lock + retry is the practical bound.  
**Remaining risk:** yes.

### 7. Legacy GameHub `GameService` had no lock

**Severity:** HIGH  
**Location:** `GameService.PlayCardAsync` / `PassActionAsync` / `EndTurnAsync` / `VotePlayerAsync` / `AdvancePhaseIfExpiredAsync`  
**Race:** Two tabs + `GameLoopHostedService.AdvancePhaseIfExpiredAsync`.  
**Impact:** Lost card / double phase advance on `IGameSessionStore` last-write-wins.  
**Fix:** Same `IRoomLock` + wait helper.  
**Regression:** covered by constructor wiring; dedicated GameService overlap test not added (room path is the live card battle).

### 8. Concurrent Tick without exclusion

**Severity:** HIGH  
**Location:** `RoomLoopProcessor` on two API instances.  
**Expected:** Only one node transitions.  
**Fix:** Existing TryAcquire + wait. Second Tick after first completed sees new phase.  
**Regression:** `RoomLockConcurrencyTests.ConcurrentTick_OnlyOneAdvances` (50×)

### 9. ResolveBattle exactly once

**Severity:** HIGH without lock  
**Location:** `BattleService.CompletePlayerTurnAsync` → `ResolveBattleAsync`  
**Race:** A Finish + B Finish + AutoPass overlapping Gets.  
**Fix:** Serialized by room lock.  
**Regression:** `BattleCommandConcurrencyTests.ConcurrentFinishBothPlayers_ResolvesOnce` (50×), `FinishTurnTwice_IsIdempotent` (50×)

### 10. Same slot / action cap

**Severity:** HIGH without lock  
**Fix:** Lock + existing slot/action checks.  
**Regression:** `SameSlot_ExactlyOneSucceeds` (100×), `SamePlayer_TwoPlayCard_NeverExceedsActionsPerTurn` (100×)

### 11. PlayCard vs AutoPass / FinishTurn

**Severity:** HIGH without lock  
**Fix:** Same lock. After finish, `RequireBattleAsync` rejects. Expired Tick may move to BattleResult; PlayCard then fails phase check.  
**Regression:** `PlayCardAndFinishTurn_NoCardAfterFinished` (100×), `PlayCardAndExpiredTick_SingleConsistentOutcome` (50×), `DispatchAndTick_NoLostPlay` (50×)

### 12. `RoomBotExecutor` mutates `GetStateAsync` without lock

**Severity:** MEDIUM  
**Location:** `RoomBotExecutor.ProcessRoomBotsAsync`  
**Impact:** Redis: mutations on a clone are discarded (bots may retry). In-memory store: **same object** as stored — writes `BotNextActionAt` without the lock.  
**Fix:** Not in this change (would be clone-on-get for in-memory, larger behavior change).  
**Remaining.**

### 13. In-memory RoomState/Battle stores return the live object

**Severity:** MEDIUM (Redis disabled)  
**Impact:** Tests that skip cloning **cannot** reproduce Redis lost updates. Production without Redis is one process; lock + shared reference hides lost-update but not thread-unsafe `List.Add`.  
**Tests use `CloningRoomStateStore` / `CloningBattleStore`.**

---

## Command idempotency (external)

| Command | Retry safe? |
|---|---|
| PlayCard (room) | Second call: slot already queued or no actions or finished. Not a silent double queue **under the lock**. No client idempotency key. |
| FinishTurn / Pass | Idempotent if already finished (`HasFinished` return). |
| Vote | Last write per voter (`Votes[userId] = target`). Retry OK. |
| RespondInvitation | Coordinator must reject non-pending; not re-tested here. |
| MarkPhaseReady | Set membership; duplicate OK. |
| StartGame | Second call Stay “already started”. |
| GameHub PlayCard | Has `IdempotencyKey` **plus** lock now. |

Network retry of PlayCard **after success** with the same slot: rejected (“already used”). Different slot: second card if actions remain — correct, not a duplicate of the first.

---

## Two-node Redis test

Not run. CI / this workspace does not provide a shared Redis for two process hosts.  
In-process cloning stores + `InMemoryRoomLock` were used to force overlap.

---

## Fixes implemented

1. Required lock on StartGame, RemovePlayer, Discard, Dispatch.  
2. SyncPlayers lock (skip if busy).  
3. InitializeRoom: TryAdd only.  
4. GetPublicState no longer writes.  
5. Lock wait loop + 30s lease.  
6. GameService uses the same lock.  
7. Concurrent tests (13) with 50–100 iterations.

---

## Test index

| Test | What it proves |
|---|---|
| `OverlappingGetThenSave_LosesTheFirstPlayersCard` | Real lost update **without** lock |
| `SamePlayer_TwoPlayCard_NeverExceedsActionsPerTurn` | A |
| `SameSlot_ExactlyOneSucceeds` | B |
| `PlayCardAndFinishTurn_NoCardAfterFinished` | C |
| `PlayCardAndExpiredTick_SingleConsistentOutcome` | D |
| `FinishTurnTwice_IsIdempotent` | E |
| `TwoPlayers_BothValidCardsSurvive` | F |
| `ConcurrentFinishBothPlayers_ResolvesOnce` | Resolve once |
| `ConcurrentTick_OnlyOneAdvances` | Two Ticks |
| `ConcurrentStartGame_DayStartOnce` | Double StartGame |
| `DispatchAndTick_NoLostPlay` | Command + Tick |
| `RemovePlayer_WithoutLock_ThrowsBusy` | Failed acquire must not write |
| `SyncPlayers_SkipsWhenLockHeld` | GET/sync cannot clobber |

---

## Counts

| | |
|---|---|
| Issues found | 13 |
| CRITICAL | 3 (Battle unlocked LWW; StartGame unlocked; other unlocked Room writes) |
| HIGH | 5 (lease 5s; no lock wait; GameService; concurrent Tick; double resolve) |
| MEDIUM | 4 (dual store crash; lock expiry; bot unlocked mutate; in-memory live refs) |
| LOW | 1 (SnapshotVersion not CAS) |
| Tests added | 13 |
| Tests that reproduced a real race | `BattleLostUpdateTests.OverlappingGetThenSave_LosesTheFirstPlayersCard` |
| Fixes implemented | Lock coverage, wait, TTL, Initialize TryAdd, GET no longer writes, GameService lock |
| Remaining risks | Lock lease expiry; Battle/Room two-key crash; BattleStore still LWW if called without lock; no 2-node Redis test; RoomBotExecutor; in-memory TTL ignored |
| Safe for multiple API instances? | **Yes, if every writer keeps using this lock and commands finish within 30s.** Not safe if a new code path writes Room/Battle/session without `IRoomLock`. |

Full suite: concurrency 13 passed. Unrelated pre-existing failures remain in bot/simulation/role-card tests (`PassActionTests`, `DayIdentityRevealTests`, `BotCognitionTests`, `InfectionTelemetryTests`) and were not caused by this lock change.
