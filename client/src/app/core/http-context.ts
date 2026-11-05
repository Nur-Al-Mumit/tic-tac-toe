import { HttpContextToken } from '@angular/common/http';

/** Marks a request as auth-endpoint traffic: a 401 there must NOT trigger a refresh. */
export const SKIP_AUTH_REFRESH = new HttpContextToken<boolean>(() => false);
