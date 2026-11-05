import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { TokenStorage } from './token.storage';
import { AuthResponse } from './models';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  let storage: TokenStorage;

  const response: AuthResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    expiresAtUtc: new Date().toISOString(),
    user: { id: 'u1', username: 'sara_01', email: 'sara@example.com' },
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    storage = TestBed.inject(TokenStorage);
  });

  it('login stores tokens and the session', async () => {
    const promise = service.login('sara_01', 'Password1');

    const req = http.expectOne('/api/v1/auth/login');
    req.flush(response);
    await promise;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()?.username).toBe('sara_01');
    expect(storage.accessToken()).toBe('access-1');
    expect(storage.getRefreshToken()).toBe('refresh-1');
  });

  it('refreshSession rotates tokens once even when called concurrently', async () => {
    localStorage.setItem('ttt.refreshToken', 'refresh-1');
    const calls = [service.refreshSession(), service.refreshSession(), service.refreshSession()];
    await Promise.resolve();

    const refresh = http.expectOne('/api/v1/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush({ ...response, accessToken: 'access-2', refreshToken: 'refresh-2' });

    const results = await Promise.all(calls);
    expect(results.every((ok) => ok)).toBe(true);
    http.verify();
  });

  it('refreshSession clears the session when the refresh token is dead', async () => {
    localStorage.setItem('ttt.refreshToken', 'refresh-dead');
    const promise = service.refreshSession();

    http
      .expectOne('/api/v1/auth/refresh')
      .flush({ status: 401, errorCode: 'Auth.InvalidRefreshToken' }, { status: 401, statusText: 'Unauthorized' });
    await promise;

    expect(service.isAuthenticated()).toBe(false);
    expect(storage.getRefreshToken()).toBeNull();
  });
});
