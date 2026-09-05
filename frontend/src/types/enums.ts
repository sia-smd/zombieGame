export enum DevicePlatform {
  Android = 0,
  iOS = 1,
}

export enum GamePhase {
  Lobby = 0,
  Day = 1,
  Discussion = 2,
  Voting = 3,
  Resolution = 4,
}

export enum RoomPhase {
  Lobby = 0,
  DayStart = 1,
  OpponentSelection = 2,
  CardBattle = 3,
  BattleResult = 4,
  Discussion = 5,
  Voting = 6,
  VoteResult = 7,
  Finished = 8,
  BattlePreparation = 9,
  DaySummary = 10,
}

export enum RoomPlayerActivity {
  Available = 0,
  Inviting = 1,
  Waiting = 2,
  InBattle = 3,
  Resting = 4,
  Disconnected = 5,
  Eliminated = 6,
}

export enum PlayerRole {
  Unknown = 0,
  Human = 1,
  Zombie = 2,
  PowerZombie = 3,
}

export enum DayEventType {
  NormalDay = 0,
  SunnyDay = 1,
  Storm = 2,
}

export enum BattlePairStatus {
  Pending = 0,
  InProgress = 1,
  Finished = 2,
}

export enum BattlePublicAction {
  None = 0,
  Action = 1,
  Pass = 2,
}

export enum MatchStatus {
  Waiting = 0,
  InProgress = 1,
  Finished = 2,
}

export enum WinTeam {
  None = 0,
  Humans = 1,
  Zombies = 2,
}

/** Public room tension — mirrors backend RoomMood (alive human vs infected counts). */
export enum RoomMood {
  Safe = 0,
  Suspicious = 1,
  Danger = 2,
  Critical = 3,
}
