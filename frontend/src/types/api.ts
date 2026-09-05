import type {
  BattlePublicAction,
  BattlePairStatus,
  DayEventType,
  GamePhase,
  PlayerRole,
  RoomMood,
  RoomPhase,
  RoomPlayerActivity,
  WinTeam,
} from './enums'

export interface AuthTokens {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
}

export interface GuestProfile {
  name: string
  imageId: string
  level: number
  coins: number
  wins: number
  losses: number
}

export interface RegisterGuestResponse {
  playerId: string
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  profile: GuestProfile
}

export interface LoginResponse {
  playerId: string
  username: string
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  profile: GuestProfile
}

export interface RecoverAccountSendResponse {
  success: boolean
  message: string
}

export interface UserProfile {
  id: string
  username: string
  email?: string | null
  phoneNumber: string
  coins: number
  wins: number
  losses: number
  createdAt: string
}

export interface ActiveMatchResponse {
  hasActiveMatch: boolean
  matchId?: string | null
  day: number
  phase: string
  battleId?: string | null
  alive: boolean
  role: string
  roomName?: string | null
  playersAlive: number
  sessionToken?: string | null
}

export interface JoinQueueResponse {
  queued: boolean
  message: string
  matchId?: string | null
  sessionToken?: string | null
}

export interface CreateRoomRequest {
  roomName: string
  maxPlayers: number
  fillWithBots: boolean
}

export interface CreateRoomResponse {
  matchId: string
  sessionToken: string
  roomName: string
  maxPlayers: number
  fillWithBots: boolean
  entryFeeCoins: number
}

export interface OpenRoomDto {
  matchId: string
  roomName: string
  playerCount: number
  maxPlayers: number
  createdAt: string
  hostUsername?: string | null
  roomCode: string
  fillWithBots: boolean
  entryFeeCoins: number
}

export interface SendRoomInviteRequest {
  matchId: string
  username: string
}

export interface SendRoomInviteResponse {
  inviteId: string
  expiresAt: string
}

export interface RoomInviteReceivedDto {
  inviteId: string
  matchId: string
  roomName: string
  roomCode: string
  fromUsername: string
  expiresAt: string
}

export interface RoomInviteResolvedDto {
  inviteId: string
  matchId: string
  accepted: boolean
  toUsername: string
}

export interface JoinOpenRoomRequest {
  matchId?: string | null
  roomCode?: string | null
}

export interface JoinOpenRoomResponse {
  matchId: string
  sessionToken: string
  roomName: string
  maxPlayers: number
  fillWithBots: boolean
  entryFeeCoins: number
  roomCode: string
}

export interface RoomConfigResponse {
  minPlayers: number
  maxPlayers: number
  defaultMaxPlayers: number
  entryFeeCoins: number
  fillWithBotsDefault: boolean
  botFillTimeoutSeconds: number
}

export interface MatchSummary {
  matchId: string
  status: number
  currentPhase: GamePhase
  playerCount: number
  maxPlayers: number
  createdAt: string
  name?: string | null
}

export interface MatchPlayerSummary {
  userId: string
  username: string
  isBot: boolean
  isAlive: boolean
  seatIndex: number
  role: PlayerRole
}

export interface GamePlayerState {
  userId: string
  username: string
  role: number
  isBot: boolean
  isAlive: boolean
  seatIndex: number
  hasShield: boolean
  remainingHealth: number
  actionsUsedThisTurn: number
  actionsPerTurn: number
  remainingActions: number
  hasRevealedThisDay: boolean
}

export interface PlayerCardState {
  userId: string
  roleCardId: string
  inventorySlot1?: string | null
  inventorySlot2?: string | null
  disabledCardIds: string[]
}

export interface GameStateDto {
  matchId: string
  sessionToken: string
  currentPhase: GamePhase
  turnNumber: number
  phaseEndsAt?: string | null
  players: GamePlayerState[]
  playerHands: PlayerCardState[]
}

export interface RoomPlayerDto {
  userId: string
  username: string
  imageId?: string
  isAlive: boolean
  isBot: boolean
  seatIndex: number
  isPaired: boolean
  hasSentInvitationToday: boolean
  hasPendingInvitation: boolean
  isResting: boolean
  isDisconnected: boolean
  activity: RoomPlayerActivity
  role?: PlayerRole | null
}

export interface RoomInvitationDto {
  id: string
  fromUserId: string
  fromUsername: string
  fromImageId?: string
  toUserId: string
  toUsername: string
  sentAt: string
  expiresAt?: string | null
}

export interface BattlePairDto {
  pairId: string
  player1Id: string
  player2Id: string
  status: BattlePairStatus
  player1Summary: BattlePublicAction
  player2Summary: BattlePublicAction
}

export interface RoomStateDto {
  matchId: string
  currentPhase: RoomPhase
  dayNumber: number
  phaseEndsAt?: string | null
  players: RoomPlayerDto[]
  battlePairs: BattlePairDto[]
  battleSummaries: BattleSummaryDto[]
  availableOpponents: string[]
  pendingInvitations: RoomInvitationDto[]
  availableOpponentsByPlayer: Record<string, string[]>
  votesRevealed: boolean
  voteCounts?: Record<string, number> | null
  currentDayEvent: DayEventType
  roomName?: string | null
  maxPlayers?: number
  fillWithBots?: boolean
  hostUserId?: string | null
  botsJoinAt?: string | null
  botFillTimeoutSeconds?: number
  invitationTimeoutSeconds?: number
  daySummary?: DaySummaryDto | null
  me?: RoomMeDto | null
  lastEliminatedPlayerId?: string | null
  winTeam?: WinTeam
  roomMood?: RoomMood
  snapshotVersion?: number
  phaseSecondsRemaining?: number
  nextDayNumber?: number | null
  nextDayEvent?: DayEventType | null
}

export interface BattleSummaryDto {
  dayNumber: number
  player1Id: string
  player1Name: string
  player1Action: BattlePublicAction
  player2Id: string
  player2Name: string
  player2Action: BattlePublicAction
}

export interface RoomMeDto {
  userId: string
  role: PlayerRole
  roleCardId: string
  health: number
  maxHealth: number
  actionsPerTurn: number
  remainingActions: number
  inventoryCardIds: string[]
  inventorySlots?: (string | null)[]
  pairId?: string | null
  opponentId?: string | null
  playedCardIds?: string[]
  opponentPlayedCardIds?: string[]
  myTurnFinished?: boolean
  opponentFinished?: boolean
  battleFinished?: boolean
  myVoteTargetId?: string | null
}

export interface DaySummaryDto {
  eliminatedPlayerIds: string[]
  newlyInfectedPlayerIds: string[]
  restingPlayerIds: string[]
  aliveCount: number
}

export interface ChatMessageDto {
  id: string
  userId: string
  username: string
  text: string
  sentAt: string
}

export interface CardDefinition {
  id: string
  name: string
  type: string
  effectKey: string
  description?: string
}

export interface ToastMessage {
  id: string
  type: 'info' | 'success' | 'warning' | 'error'
  message: string
}

export type SignalREventMap = {
  PlayerJoined: { playerId: string }
  PlayerLeft: { playerId: string }
  PlayerReady: RoomStateDto
  GameStarted: RoomStateDto | void
  GameStateUpdated: GameStateDto | RoomStateDto
  CardPlayed: RoomStateDto
  VotingStarted: void
  VotingEnded: unknown
  PlayerDead: { playerId: string }
  GameFinished: { winner: WinTeam }
  RoomUpdated: RoomStateDto
  SyncState: GameStateDto
  BattleState: RoomStateDto
  ChatMessage: ChatMessageDto
  VoteStarted: void
  VoteUpdated: unknown
  VoteFinished: unknown
  PlayerEliminated: { playerId: string }
  DayStarted: { dayNumber: number; dayEvent: DayEventType }
  PhaseChanged: { phase: RoomPhase; message: string }
  GameEvent: { success: boolean; message: string }
  RoomInviteReceived: RoomInviteReceivedDto
  RoomInviteResolved: RoomInviteResolvedDto
  InvitationSent: unknown
  InvitationAccepted: unknown
  InvitationsCancelled: unknown
  BattleStarted: unknown
  BattleFinished: unknown
  MatchResumed: unknown
  DiscussionStarted: unknown
}
