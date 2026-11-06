export interface UserDto {
  id: string;
  username: string;
  email: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  user: UserDto;
}

/** RFC 7807 error body produced by the API (api-spec §1.4 errorCode registry). */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  errorCode?: string;
  errors?: Record<string, string>;
}

export interface PlayerRef {
  id: string;
  username: string;
}

export interface GameResultDto {
  outcome: 'XWins' | 'OWins' | 'Draw';
  reason: 'Normal' | 'Forfeit' | 'Timeout';
  winningLine: number[] | null;
}

export interface GameDto {
  id: string;
  status: 'Waiting' | 'InProgress' | 'Finished' | 'Cancelled';
  playerX: PlayerRef;
  playerO: PlayerRef | null;
  board: Array<'X' | 'O' | null>;
  currentTurn: 'X' | 'O';
  result: GameResultDto | null;
  createdAtUtc: string;
  moveDeadlineUtc: string | null;
}

export type LobbyGameDto = GameDto;

export interface OnlinePlayerDto {
  id: string;
  username: string;
  inGame: boolean;
}
