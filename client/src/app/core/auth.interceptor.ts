import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { SKIP_AUTH_REFRESH } from './http-context';
import { TokenStorage } from './token.storage';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenStorage = inject(TokenStorage);
  const auth = inject(AuthService);

  const withToken = tokenStorage.accessToken()
    ? req.clone({ setHeaders: { Authorization: `Bearer ${tokenStorage.accessToken()}` } })
    : req;

  return next(withToken).pipe(
    catchError((error: unknown) => {
      const is401 = error instanceof HttpErrorResponse && error.status === 401;
      const refreshable = !req.context.get(SKIP_AUTH_REFRESH);
      if (!is401 || !refreshable) {
        return throwError(() => error);
      }

      return from(auth.refreshSession()).pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            return throwError(() => error);
          }
          const retried = req.clone({ setHeaders: { Authorization: `Bearer ${tokenStorage.accessToken()}` } });
          return next(retried);
        }),
      );
    }),
  );
};
