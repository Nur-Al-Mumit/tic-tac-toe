import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { authGuard, guestGuard } from './guards';
import { AuthService } from './auth.service';
import { TokenStorage } from './token.storage';
import { AuthResponse } from './models';

describe('guards', () => {
  const tokens: AuthResponse = {
    accessToken: 'a',
    refreshToken: 'r',
    expiresAtUtc: new Date().toISOString(),
    user: { id: 'u1', username: 'sara_01', email: 's@example.com' },
  };

  function login() {
    TestBed.inject(TokenStorage).set(tokens.accessToken, tokens.refreshToken, true);
    TestBed.inject(AuthService)['session'].set(tokens.user); // simulate a live session
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });

  it('authGuard allows authenticated users', () => {
    login();
    const result = TestBed.runInInjectionContext(() => authGuard(null as never, undefined as never));
    expect(result).toBe(true);
  });

  it('authGuard redirects anonymous users to /login', () => {
    const result = TestBed.runInInjectionContext(() => authGuard(null as never, undefined as never)) as UrlTree;
    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result)).toBe('/login');
  });

  it('guestGuard redirects logged-in users to /lobby', () => {
    login();
    const result = TestBed.runInInjectionContext(() => guestGuard(null as never, undefined as never)) as UrlTree;
    const router = TestBed.inject(Router);
    expect(router.serializeUrl(result)).toBe('/lobby');
  });
});
