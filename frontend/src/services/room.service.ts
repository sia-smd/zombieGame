import { connectHub, invokeHub, onHubEvent } from './signalr'
import { api, createIdempotencyKey } from './api'
import type { RoomStateDto } from '@/types/api'

export const roomService = {
  async joinRoom(matchId: string, sessionToken: string): Promise<void> {
    await connectHub('room')
    await invokeHub('room', 'JoinRoom', matchId, sessionToken)
  },

  async resumeMatch(matchId: string, sessionToken: string): Promise<void> {
    await connectHub('room')
    await invokeHub('room', 'ResumeMatch', matchId, sessionToken)
  },

  async syncRoom(matchId: string, sessionToken: string): Promise<void> {
    await invokeHub('room', 'SyncRoom', matchId, sessionToken)
  },

  async startGame(matchId: string, sessionToken: string): Promise<void> {
    await invokeHub('room', 'StartGame', matchId, sessionToken)
  },

  async markPhaseReady(matchId: string, sessionToken: string): Promise<void> {
    await invokeHub('room', 'MarkPhaseReady', matchId, sessionToken)
  },

  async sendInvitation(matchId: string, sessionToken: string, targetUserId: string): Promise<void> {
    await invokeHub('room', 'SendInvitation', matchId, sessionToken, { targetUserId })
  },

  async respondInvitation(
    matchId: string,
    sessionToken: string,
    invitationId: string,
    accept: boolean,
  ): Promise<void> {
    await invokeHub('room', 'RespondInvitation', matchId, sessionToken, { invitationId, accept })
  },

  async playCardInBattle(
    matchId: string,
    sessionToken: string,
    pairId: string,
    cardId: string,
    targetUserId?: string | null,
  ): Promise<void> {
    await invokeHub('room', 'PlayCardInBattle', matchId, sessionToken, {
      pairId,
      cardId,
      targetUserId: targetUserId ?? null,
      idempotencyKey: createIdempotencyKey(),
    })
  },

  async passInBattle(matchId: string, sessionToken: string, pairId: string): Promise<void> {
    await invokeHub('room', 'PassInBattle', matchId, sessionToken, {
      pairId,
      idempotencyKey: createIdempotencyKey(),
    })
  },

  async sendChat(matchId: string, sessionToken: string, text: string): Promise<void> {
    await invokeHub('room', 'SendChat', matchId, sessionToken, { text })
  },

  async vote(matchId: string, sessionToken: string, targetUserId: string): Promise<void> {
    await invokeHub('room', 'Vote', matchId, sessionToken, {
      targetUserId,
      idempotencyKey: createIdempotencyKey(),
    })
  },

  async leaveRoom(matchId: string): Promise<void> {
    await api.post('/api/matchmaking/leave-room', { matchId })
    await invokeHub('room', 'LeaveWaitingRoom', matchId).catch(() => undefined)
  },

  async joinBattle(matchId: string, sessionToken: string, battleId: string): Promise<void> {
    await invokeHub('room', 'JoinBattle', matchId, sessionToken, battleId)
  },

  onRoomUpdated(handler: (state: RoomStateDto) => void) {
    return onHubEvent('room', 'RoomUpdated', handler)
  },

  onGameStarted(handler: (state?: RoomStateDto) => void) {
    return onHubEvent('room', 'GameStarted', handler as (p: RoomStateDto | void) => void)
  },

  onPhaseChanged(handler: (payload: import('@/types/api').SignalREventMap['PhaseChanged']) => void) {
    return onHubEvent('room', 'PhaseChanged', handler)
  },

  onPlayerJoined(handler: (payload: { playerId: string }) => void) {
    return onHubEvent('room', 'PlayerJoined', handler)
  },

  onBattleState(handler: (state: RoomStateDto) => void) {
    return onHubEvent('room', 'BattleState', handler)
  },

  onChatMessage(handler: (msg: import('@/types/api').ChatMessageDto) => void) {
    return onHubEvent('room', 'ChatMessage', handler)
  },

  onVoteStarted(handler: () => void) {
    return onHubEvent('room', 'VoteStarted', handler)
  },

  onVoteFinished(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'VoteFinished', handler)
  },

  onPlayerEliminated(handler: (payload: { playerId: string }) => void) {
    return onHubEvent('room', 'PlayerEliminated', handler)
  },

  onDayStarted(handler: (payload: { dayNumber: number; dayEvent: number }) => void) {
    return onHubEvent('room', 'DayStarted', handler)
  },

  onGameFinished(handler: (payload: { winner: number }) => void) {
    return onHubEvent('room', 'GameFinished', handler)
  },

  onRoomInviteReceived(handler: (payload: import('@/types/api').RoomInviteReceivedDto) => void) {
    return onHubEvent('room', 'RoomInviteReceived', handler)
  },

  onRoomInviteResolved(handler: (payload: import('@/types/api').RoomInviteResolvedDto) => void) {
    return onHubEvent('room', 'RoomInviteResolved', handler)
  },

  onInvitationSent(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'InvitationSent', handler)
  },

  onInvitationAccepted(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'InvitationAccepted', handler)
  },

  onInvitationsCancelled(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'InvitationsCancelled', handler)
  },

  onBattleStarted(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'BattleStarted', handler)
  },

  onBattleFinished(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'BattleFinished', handler)
  },

  onVoteUpdated(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'VoteUpdated', handler)
  },

  onMatchResumed(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'MatchResumed', handler)
  },

  onDiscussionStarted(handler: (payload: unknown) => void) {
    return onHubEvent('room', 'DiscussionStarted', handler)
  },
}
