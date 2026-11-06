import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthResponse, UserDto } from './models';
import { SKIP_AUTH_REFRESH } from './http-context';
import { TokenStorage } from './token.storage';

const AuthEndpoint = { context: new HttpContext().set(SKIP_AUTH_REFRESH, true) };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStorage = inject(TokenStorage);

  private readonly session = signal<UserDto | null>(null);
  private refreshInFlight: Promise<boolean> | null = null;

  readonly user = this.session.asReadonly();
  readonly isAuthenticated = computed(() => this.session() !== null);

  async login(identifier: string, password: string, remember = true): Promise<void> {
    this.store(await firstValueFrom(this.http.post<AuthResponse>('/api/v1/auth/login', { identifier, password }, AuthEndpoint)), remember);
  }

  async register(username: string, email: string, password: string): Promise<void> {
    this.store(await firstValueFrom(this.http.post<AuthResponse>('/api/v1/auth/register', { username, email, password }, AuthEndpoint)), true);
  }

  async logout(): Promise<void> {
    const refreshToken = this.tokenStorage.getRefreshToken();
    this.tokenStorage.clear();
    this.session.set(null);
    if (refreshToken) {
      try {
        await firstValueFrom(this.http.post('/api/v1/auth/logout', { refreshToken }));
      } catch {
        // Revocation failures must never trap the user in a logged-out-but-stale UI.
      }
    }
  }

  /** Single-flight refresh: parallel 401s share one /auth/refresh call. */
  refreshSession(): Promise<boolean> {
    this.refreshInFlight ??= this.performRefresh().finally(() => (this.refreshInFlight = null));
    return this.refreshInFlight;
  }

  async restoreSession(): Promise<void> {
    if (this.tokenStorage.getRefreshToken()) {
      await this.refreshSession();
    }
  }

  private async performRefresh(): Promise<boolean> {
    const refreshToken = this.tokenStorage.getRefreshToken();
    if (!refreshToken) {
      return false;
    }

    try {
      this.storePreservingMemory(await firstValueFrom(this.http.post<AuthResponse>('/api/v1/auth/refresh', { refreshToken }, AuthEndpoint)));
      return true;
    } catch {
      this.tokenStorage.clear();
      this.session.set(null);
      return false;
    }
  }

  private store(response: AuthResponse, remember: boolean): void {
    this.tokenStorage.set(response.accessToken, response.refreshToken, remember);
    this.session.set(response.user);
  }

  /** Refresh rotation: keep the token where the user originally stored it. */
  private storePreservingMemory(response: AuthResponse): void {
    const existing = this.tokenStorage.getRefreshToken();
    this.tokenStorage.set(response.accessToken, response.refreshToken, localStorage.getItem('ttt.refreshToken') === existing);
    this.session.set(response.user);
  }
}
