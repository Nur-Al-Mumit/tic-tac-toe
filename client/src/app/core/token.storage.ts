import { Injectable, signal } from '@angular/core';

/** Access token lives in memory only. The refresh token goes to localStorage when "remember me"
 * is checked and to sessionStorage otherwise (design system §Token storage; pages-and-flows §2.4). */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  private static readonly RefreshTokenKey = 'ttt.refreshToken';

  private readonly _accessToken = signal<string | null>(null);

  readonly accessToken = this._accessToken.asReadonly();

  set(accessToken: string, refreshToken: string, remember: boolean): void {
    this._accessToken.set(accessToken);
    const store = remember ? localStorage : sessionStorage;
    store.setItem(TokenStorage.RefreshTokenKey, refreshToken);
    (remember ? sessionStorage : localStorage).removeItem(TokenStorage.RefreshTokenKey);
  }

  getRefreshToken(): string | null {
    return sessionStorage.getItem(TokenStorage.RefreshTokenKey)
      ?? localStorage.getItem(TokenStorage.RefreshTokenKey);
  }

  clear(): void {
    this._accessToken.set(null);
    sessionStorage.removeItem(TokenStorage.RefreshTokenKey);
    localStorage.removeItem(TokenStorage.RefreshTokenKey);
  }
}
