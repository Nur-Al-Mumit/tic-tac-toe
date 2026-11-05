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
