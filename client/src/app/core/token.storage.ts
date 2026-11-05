import { Injectable, signal } from '@angular/core';

/** Access token lives in memory only; the refresh token in localStorage (MVP — HttpOnly cookie in v1.1). */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  private static readonly RefreshTokenKey = 'ttt.refreshToken';

  private readonly _accessToken = signal<string | null>(null);

  readonly accessToken = this._accessToken.asReadonly();

  set(accessToken: string, refreshToken: string): void {
    this._accessToken.set(accessToken);
    localStorage.setItem(TokenStorage.RefreshTokenKey, refreshToken);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(TokenStorage.RefreshTokenKey);
  }

  clear(): void {
    this._accessToken.set(null);
    localStorage.removeItem(TokenStorage.RefreshTokenKey);
  }
}
