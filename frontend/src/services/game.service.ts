import { api } from './api'
import type {
  ActiveMatchResponse,
  CreateRoomRequest,
  CreateRoomResponse,
  JoinOpenRoomRequest,
  JoinOpenRoomResponse,
  JoinQueueResponse,
  MatchSummary,
  OpenRoomDto,
  RoomConfigResponse,
  SendRoomInviteRequest,
  SendRoomInviteResponse,
} from '@/types/api'

export const gameService = {
  async getRoomConfig(): Promise<RoomConfigResponse> {
    const { data } = await api.get<RoomConfigResponse>('/api/matchmaking/room-config')
    return data
  },

  async createRoom(payload: CreateRoomRequest): Promise<CreateRoomResponse> {
    const { data } = await api.post<CreateRoomResponse>('/api/matchmaking/rooms', payload)
    return data
  },

  async listOpenRooms(): Promise<OpenRoomDto[]> {
    const { data } = await api.get<OpenRoomDto[]>('/api/matchmaking/rooms')
    return data
  },

  async joinOpenRoom(payload: JoinOpenRoomRequest): Promise<JoinOpenRoomResponse> {
    const { data } = await api.post<JoinOpenRoomResponse>('/api/matchmaking/join-room', payload)
    return data
  },

  async sendRoomInvite(payload: SendRoomInviteRequest): Promise<SendRoomInviteResponse> {
    const { data } = await api.post<SendRoomInviteResponse>('/api/matchmaking/invites', payload)
    return data
  },

  async acceptRoomInvite(inviteId: string): Promise<JoinOpenRoomResponse> {
    const { data } = await api.post<JoinOpenRoomResponse>(`/api/matchmaking/invites/${inviteId}/accept`)
    return data
  },

  async denyRoomInvite(inviteId: string): Promise<void> {
    await api.post(`/api/matchmaking/invites/${inviteId}/deny`)
  },

  async joinQueue(): Promise<JoinQueueResponse> {
    const { data } = await api.post<JoinQueueResponse>('/api/matchmaking/queue/join')
    return data
  },

  async leaveQueue(): Promise<void> {
    await api.post('/api/matchmaking/queue/leave')
  },

  async getQueueStatus(): Promise<{ inQueue: boolean; queueCount: number }> {
    const { data } = await api.get<{ inQueue: boolean; queueCount: number }>(
      '/api/matchmaking/queue/status',
    )
    return data
  },

  async getMatch(matchId: string): Promise<MatchSummary> {
    const { data } = await api.get<MatchSummary>(`/api/matchmaking/matches/${matchId}`)
    return data
  },

  async getActiveMatch(): Promise<ActiveMatchResponse> {
    const { data } = await api.get<ActiveMatchResponse>('/api/match/active')
    return data
  },
}
