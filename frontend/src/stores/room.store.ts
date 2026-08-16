import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { roomService } from '@/services/room.service'
import type {
  ChatMessageDto,
  RoomInvitationDto,
  RoomMeDto,
  RoomPlayerDto,
  RoomStateDto,
} from '@/types/api'
import { RoomPhase, RoomPlayerActivity, WinTeam, DayEventType } from '@/types/enums'
import { sameUserId } from '@/utils/ids'
import { parseRoomPhase } from '@/utils/roomFlow'
import { errorMessage } from '@/utils/errors'

export type RoomPlayerStatus =
  | 'eliminated'
  | 'inBattle'
  | 'inviting'
  | 'waiting'
  | 'available'
  | 'resting'
  | 'disconnected'

const ACTIVITY_STATUS: Record<RoomPlayerActivity, RoomPlayerStatus> = {
  [RoomPlayerActivity.Available]: 'available',
  [RoomPlayerActivity.Inviting]: 'inviting',
  [RoomPlayerActivity.Waiting]: 'waiting',
  [RoomPlayerActivity.InBattle]: 'inBattle',
  [RoomPlayerActivity.Resting]: 'resting',
  [RoomPlayerActivity.Disconnected]: 'disconnected',
  [RoomPlayerActivity.Eliminated]: 'eliminated',
}

export const useRoomStore = defineStore('room', () => {
  const matchId = ref<string | null>(null)
  const sessionToken = ref<string | null>(null)
  const state = ref<RoomStateDto | null>(null)
  const chatMessages = ref<ChatMessageDto[]>([])
  const isConnecting = ref(false)
  const lastEvent = ref<string | null>(null)
  // Private battle data only rides caller-scoped syncs; group broadcasts omit it,
  // so it is kept apart from `state` to survive those null-me updates.
  const myBattle = ref<RoomMeDto | null>(null)
  const winTeam = ref<WinTeam>(WinTeam.None)
  const snapshotReceivedAt = ref(0)
  const lastSyncError = ref<string | null>(null)

  const players = computed(() => state.value?.players ?? [])
  const currentPhase = computed(() => state.value?.currentPhase ?? RoomPhase.Lobby)
  const dayNumber = computed(() => state.value?.dayNumber ?? 0)
  const currentDayEvent = computed(() => state.value?.currentDayEvent ?? DayEventType.NormalDay)
  const battlePairs = computed(() => state.value?.battlePairs ?? [])
  const battleSummaries = computed(() => state.value?.battleSummaries ?? [])
  const daySummary = computed(() => state.value?.daySummary ?? null)
  const roomMood = computed(() => state.value?.roomMood ?? 0)
  const isVoting = computed(() => currentPhase.value === RoomPhase.Voting)
  const pendingInvitations = computed(() => state.value?.pendingInvitations ?? [])
  const alivePlayers = computed(() => players.value.filter((p) => p.isAlive))
  const invitationTimeoutSeconds = computed(() => state.value?.invitationTimeoutSeconds ?? 10)

  /** Invitation waiting for this player's answer. */
  function incomingInvitation(userId: string | null | undefined): RoomInvitationDto | null {
    return pendingInvitations.value.find((i) => sameUserId(i.toUserId, userId)) ?? null
  }

  /** Invitation this player sent and is still waiting on. */
  function outgoingInvitation(userId: string | null | undefined): RoomInvitationDto | null {
    return pendingInvitations.value.find((i) => sameUserId(i.fromUserId, userId)) ?? null
  }

  /**
   * Opponents this player may invite. The server filters out players already fought,
   * so the roster must be exhausted before rematches become possible.
   */
  function invitableIds(userId: string | null | undefined): string[] {
    if (!userId) return []
    const byPlayer = state.value?.availableOpponentsByPlayer
    if (byPlayer) {
      const key = Object.keys(byPlayer).find((k) => sameUserId(k, userId))
      if (key) return byPlayer[key]
    }
    return state.value?.availableOpponents ?? []
  }

  function canInvite(
    viewerId: string | null | undefined,
    targetId: string | null | undefined,
  ): boolean {
    if (currentPhase.value !== RoomPhase.OpponentSelection) return false
    if (!viewerId || !targetId || sameUserId(viewerId, targetId)) return false

    const viewer = players.value.find((p) => sameUserId(p.userId, viewerId))
    if (!viewer?.isAlive || viewer.isPaired || viewer.hasPendingInvitation) return false
    if (viewer.hasSentInvitationToday) return false

    return invitableIds(viewerId).some((id) => sameUserId(id, targetId))
  }

  /** The server projects the badge; the local checks only cover older payloads. */
  function playerStatus(player: RoomPlayerDto): RoomPlayerStatus {
    const fromServer = ACTIVITY_STATUS[player.activity]
    if (fromServer) return fromServer

    if (!player.isAlive) return 'eliminated'
    if (player.isDisconnected) return 'disconnected'
    if (player.isResting) return 'resting'
    if (player.isPaired) return 'inBattle'
    if (player.hasPendingInvitation) return 'inviting'
    return 'available'
  }

  let unsubscribers: Array<() => void> = []
  let recoveryPromise: Promise<void> | null = null

  function applyState(next: RoomStateDto) {
    const incomingVersion = next.snapshotVersion ?? 0
    const currentVersion = state.value?.snapshotVersion ?? 0
    if (
      state.value &&
      incomingVersion > 0 &&
      currentVersion > 0 &&
      incomingVersion < currentVersion &&
      next.matchId === state.value.matchId
    ) {
      return
    }

    // Day rollover must drop the previous private battle snapshot (e.g. day-1 Pass left
    // remainingActions at 0 and an obsolete pairId).
    if (state.value && next.dayNumber !== state.value.dayNumber) {
      myBattle.value = null
    }

    // StartGame can finish after DayStart has already elapsed and re-broadcast a stale
    // DayStart DTO (same dayNumber). Ignore that so the Day 1 countdown dialog cannot reopen.
    if (
      state.value &&
      next.currentPhase === RoomPhase.DayStart &&
      next.dayNumber === state.value.dayNumber &&
      state.value.currentPhase !== RoomPhase.DayStart &&
      state.value.currentPhase !== RoomPhase.Lobby
    ) {
      return
    }

    state.value = next
    snapshotReceivedAt.value = Date.now()
    matchId.value = next.matchId
    if (next.me) myBattle.value = next.me
    if (typeof next.winTeam === 'number' && next.winTeam !== WinTeam.None) {
      winTeam.value = next.winTeam
    }
  }

  /** Pulls a fresh snapshot when a pushed update is missed. */
  async function syncNow() {
    if (!matchId.value || !sessionToken.value) return false
    try {
      await roomService.syncRoom(matchId.value, sessionToken.value)
      lastSyncError.value = null
      return true
    } catch (error: unknown) {
      lastSyncError.value = errorMessage(error)
      throw error
    }
  }

  function registerHandlers() {
    clearHandlers()
    unsubscribers = [
      roomService.onRoomUpdated(applyState),
      roomService.onPhaseChanged((payload) => {
        lastEvent.value = 'PhaseChanged'
        const phase = parseRoomPhase(payload?.phase)
        if (phase === null) return
        // The full state comes in its own broadcast; only chase it when it never arrives.
        setTimeout(() => {
          if (state.value?.currentPhase !== phase) {
            void syncNow().catch(() => undefined)
          }
        }, 600)
      }),
      roomService.onGameStarted((s) => {
        lastEvent.value = 'GameStarted'
        if (s && typeof s === 'object' && 'matchId' in s) applyState(s)
      }),
      roomService.onPlayerJoined(() => { lastEvent.value = 'PlayerJoined' }),
      roomService.onBattleState(applyState),
      roomService.onChatMessage((msg) => {
        chatMessages.value = [...chatMessages.value, msg].slice(-100)
      }),
      roomService.onVoteStarted(() => { lastEvent.value = 'VotingStarted' }),
      roomService.onVoteFinished(() => { lastEvent.value = 'VotingEnded' }),
      roomService.onPlayerEliminated(() => { lastEvent.value = 'PlayerDead' }),
      roomService.onDayStarted((payload) => {
        lastEvent.value = 'DayStarted'
        if (!state.value || !payload) return
        const nextEvent =
          typeof payload.dayEvent === 'number' ? payload.dayEvent : state.value.currentDayEvent
        state.value = {
          ...state.value,
          dayNumber: payload.dayNumber ?? state.value.dayNumber,
          currentDayEvent: nextEvent,
        }
      }),
      roomService.onGameFinished((payload) => {
        lastEvent.value = 'GameFinished'
        const winner = payload?.winner
        if (typeof winner === 'number') winTeam.value = winner
      }),
      roomService.onInvitationSent(() => {
        lastEvent.value = 'InvitationSent'
      }),
      roomService.onInvitationAccepted(() => {
        lastEvent.value = 'InvitationAccepted'
      }),
      roomService.onInvitationsCancelled(() => {
        lastEvent.value = 'InvitationsCancelled'
      }),
      roomService.onBattleStarted(() => {
        lastEvent.value = 'BattleStarted'
      }),
      roomService.onBattleFinished(() => {
        lastEvent.value = 'BattleFinished'
      }),
      roomService.onVoteUpdated(() => {
        lastEvent.value = 'VoteUpdated'
      }),
      roomService.onMatchResumed(() => {
        lastEvent.value = 'MatchResumed'
        void syncNow().catch(() => undefined)
      }),
      roomService.onDiscussionStarted(() => {
        lastEvent.value = 'DiscussionStarted'
      }),
    ]
  }

  function clearHandlers() {
    unsubscribers.forEach((u) => u())
    unsubscribers = []
  }

  async function connect(id: string, token: string) {
    isConnecting.value = true
    matchId.value = id
    sessionToken.value = token
    registerHandlers()
    try {
      await roomService.joinRoom(id, token)
    } finally {
      isConnecting.value = false
    }
  }

  async function resume(id: string, token: string) {
    isConnecting.value = true
    matchId.value = id
    sessionToken.value = token
    registerHandlers()
    try {
      await roomService.resumeMatch(id, token)
    } finally {
      isConnecting.value = false
    }
  }

  async function recoverConnection(): Promise<void> {
    if (recoveryPromise) return recoveryPromise

    const id = matchId.value
    const token = sessionToken.value
    if (!id || !token) return

    const run = (async () => {
      await resume(id, token)
      await syncNow()
      if (state.value?.matchId !== id) {
        throw new Error('The room session could not be restored.')
      }
    })()

    recoveryPromise = run
    try {
      await run
    } finally {
      if (recoveryPromise === run) recoveryPromise = null
    }
  }

  function reset() {
    clearHandlers()
    matchId.value = null
    sessionToken.value = null
    state.value = null
    chatMessages.value = []
    lastEvent.value = null
    myBattle.value = null
    winTeam.value = WinTeam.None
    lastSyncError.value = null
    snapshotReceivedAt.value = 0
    recoveryPromise = null
  }

  return {
    matchId,
    sessionToken,
    state,
    chatMessages,
    isConnecting,
    lastEvent,
    myBattle,
    winTeam,
    lastSyncError,
    snapshotReceivedAt,
    players,
    alivePlayers,
    currentPhase,
    dayNumber,
    currentDayEvent,
    battlePairs,
    battleSummaries,
    daySummary,
    roomMood,
    isVoting,
    pendingInvitations,
    invitationTimeoutSeconds,
    incomingInvitation,
    outgoingInvitation,
    invitableIds,
    canInvite,
    playerStatus,
    connect,
    resume,
    recoverConnection,
    reset,
    syncNow,
    applyState,
    registerHandlers,
    clearHandlers,
  }
})
