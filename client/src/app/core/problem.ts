import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Maps API ProblemDetails to friendly copy; keyed on errorCode per api-spec §1.4. */
const CODE_MESSAGES: Record<string, string> = {
  'Auth.InvalidCredentials': 'Wrong email/username or password.',
  'Auth.LockedOut': 'Too many attempts — the account is locked for 5 minutes.',
  'Auth.DuplicateUsername': 'That username is already taken.',
  'Auth.DuplicateEmail': 'That email is already registered.',
  'Auth.InvalidRefreshToken': 'Your session expired — please log in again.',
  'Validation.Failure': 'Please fix the highlighted fields.',
};

export function problemMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problem = error.error as ProblemDetails | null;
    const code = problem?.errorCode;
    if (code && CODE_MESSAGES[code]) {
      return CODE_MESSAGES[code];
    }
    return problem?.detail ?? 'Something went wrong. Please try again.';
  }
  return 'Something went wrong. Please try again.';
}
