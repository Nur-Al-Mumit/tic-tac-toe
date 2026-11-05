import { HttpClient, HttpContext, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { authInterceptor } from './auth.interceptor';
import { SKIP_AUTH_REFRESH } from './http-context';
import { TokenStorage } from './token.storage';

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let storage: TokenStorage;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    storage = TestBed.inject(TokenStorage);
  });

  it('attaches the bearer token', async () => {
    storage.set('access-1', 'refresh-1');
    const request = firstValueFrom(client.get('/api/v1/games'));

    const req = http.expectOne('/api/v1/games');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush([]);
    await request;
  });

  it('on 401: refreshes once and retries the original request with the new token', async () => {
    storage.set('expired-access', 'refresh-1');
    const request = firstValueFrom(client.get('/api/v1/games'));

    http.expectOne('/api/v1/games').flush(null, { status: 401, statusText: 'Unauthorized' });
    const refresh = http.expectOne('/api/v1/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush({
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
      expiresAtUtc: new Date().toISOString(),
      user: { id: 'u1', username: 'sara_01', email: 's@example.com' },
    });

    await new Promise((resolve) => setTimeout(resolve, 0));

    const retried = http.expectOne('/api/v1/games');
    expect(retried.request.headers.get('Authorization')).toBe('Bearer access-2');
    retried.flush([]);

    expect(await request).toEqual([]);
  });

  it('propagates the 401 when the refresh also fails', async () => {
    storage.set('expired-access', 'refresh-dead');
    const request = firstValueFrom(client.get('/api/v1/games'));

    http.expectOne('/api/v1/games').flush(null, { status: 401, statusText: 'Unauthorized' });
    http.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(request).rejects.toBeInstanceOf(HttpErrorResponse);
  });

  it('never refreshes for requests marked SKIP_AUTH_REFRESH', async () => {
    const request = firstValueFrom(
      client.post('/api/v1/auth/login', { identifier: 'x', password: 'y' }, { context: new HttpContext().set(SKIP_AUTH_REFRESH, true) }),
    );

    http.expectOne('/api/v1/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(request).rejects.toBeInstanceOf(HttpErrorResponse);
    http.verify();
  });
});
