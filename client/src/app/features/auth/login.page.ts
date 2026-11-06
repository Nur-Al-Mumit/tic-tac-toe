import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, NonNullableFormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';

@Component({
  selector: 'ttt-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <main class="flex min-h-screen items-center justify-center p-4 sm:p-6">
      <div class="grid w-full max-w-4xl overflow-hidden rounded-card border border-white/10 shadow-2xl shadow-primary/10 backdrop-blur-sm md:grid-cols-2">
        <!-- brand panel (desktop) -->
        <section aria-hidden="true" class="relative hidden items-center justify-center overflow-hidden bg-gradient-to-br from-primary/25 via-bg to-bg p-10 md:flex">
          <span class="animate-float-a absolute left-10 top-12 font-mono text-8xl font-black text-mark-x/70 glow-x">X</span>
          <span class="animate-float-b absolute bottom-10 right-10 font-mono text-8xl font-black text-mark-o/70 glow-o">O</span>
          <span class="animate-float-b absolute left-1/4 bottom-1/4 font-mono text-6xl font-black text-mark-o/30">O</span>
          <div class="relative z-10 text-center">
            <div class="mx-auto mb-5 grid w-fit grid-cols-3 gap-1.5">
              @for (cell of brandBoard; track $index) {
                <span class="flex h-9 w-9 items-center justify-center rounded-lg border border-white/10 bg-bg/60 font-mono text-base font-black"
                      [class.text-mark-x]="cell === 'X'" [class.text-mark-o]="cell === 'O'">{{ cell }}</span>
              }
            </div>
            <h2 class="text-2xl font-black tracking-tight">Tic-Tac-Toe</h2>
            <p class="mt-1 text-[11px] font-semibold uppercase tracking-[0.25em] text-slate-400">Play a real person. Right now.</p>
          </div>
        </section>

        <!-- form panel -->
        <section class="bg-surface/80 p-8 sm:p-10">
          <!-- compact brand header (mobile only, pages-and-flows §2.2) -->
          <div class="mb-6 flex items-center justify-center gap-3 md:hidden" aria-hidden="true">
            <span class="font-mono text-2xl font-black text-mark-x">X</span>
            <span class="text-sm font-black uppercase tracking-widest text-slate-200">Tic-Tac-Toe</span>
            <span class="font-mono text-2xl font-black text-mark-o">O</span>
          </div>

          <h1 class="text-xl font-extrabold tracking-tight">Welcome back</h1>
          <p class="mt-1 text-sm text-slate-400">Sign in to keep the streak alive</p>

          <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4" novalidate>
            <div>
              <label for="identifier" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Email or username</label>
              <input id="identifier" formControlName="identifier" autocomplete="username" required
                     placeholder="you@example.com or username"
                     class="field-input" [class.is-invalid]="submitted() && identifier.invalid"
                     [readonly]="busy()" [attr.aria-invalid]="submitted() && identifier.invalid"
                     [attr.aria-describedby]="submitted() && identifier.invalid ? 'identifier-error' : null" />
              @if (submitted() && identifier.errors?.['required']) {
                <p id="identifier-error" class="mt-1 text-xs text-red-400">Enter your email or username</p>
              }
            </div>

            <div>
              <label for="password" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Password</label>
              <div class="relative">
                <input id="password" formControlName="password" autocomplete="current-password" required
                       [type]="showPassword() ? 'text' : 'password'"
                       class="field-input pr-16" [class.is-invalid]="submitted() && password.invalid"
                       [readonly]="busy()" [attr.aria-invalid]="submitted() && password.invalid"
                       [attr.aria-describedby]="submitted() && password.invalid ? 'password-error' : null" />
                <button type="button" (click)="showPassword.set(!showPassword())"
                        [attr.aria-pressed]="showPassword()"
                        [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
                        class="absolute inset-y-0 right-2 my-auto h-7 rounded px-2 text-xs font-semibold text-slate-400 hover:text-slate-100">
                  {{ showPassword() ? 'Hide' : 'Show' }}
                </button>
              </div>
              @if (submitted() && password.errors?.['required']) {
                <p id="password-error" class="mt-1 text-xs text-red-400">Enter your password</p>
              }
            </div>

            <label class="flex cursor-pointer items-center gap-2 text-sm text-slate-300">
              <input type="checkbox" formControlName="remember" class="h-4 w-4 rounded border-surface-border bg-bg accent-primary" />
              Remember me
            </label>

            <button type="submit" [disabled]="busy()" [attr.aria-busy]="busy()" class="btn-cta w-full !py-3">
              @if (busy()) {
                <span class="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white" aria-hidden="true"></span>
                Signing in…
              } @else {
                Sign in
              }
            </button>
          </form>

          <p class="mt-6 text-center text-sm text-slate-400">
            No account? <a routerLink="/register" class="font-bold text-primary-hover hover:underline">Register</a>
          </p>
        </section>
      </div>
    </main>
  `,
})
export class LoginPage {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  readonly brandBoard = ['X', 'O', 'X', 'O', 'X', '', '', '', 'O'];

  readonly form = this.fb.group({
    identifier: ['', Validators.required],
    password: ['', Validators.required],
    remember: [true],
  });

  readonly busy = signal(false);
  readonly submitted = signal(false);
  readonly showPassword = signal(false);

  readonly identifier = this.form.controls.identifier;
  readonly password = this.form.controls.password;
  readonly remember = this.form.controls.remember;

  async submit(): Promise<void> {
    this.submitted.set(true);
    if (this.form.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    try {
      await this.auth.login(this.identifier.value, this.password.value, this.remember.value);
      // Returning players choose on the lobby banner: rejoin the running game or quit & resign.
      await this.router.navigateByUrl('/lobby');
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        // Per-status copy from pages-and-flows §2.4.
        if (error.status === 401) {
          this.toasts.error('Invalid credentials. Check your email/username and password.');
        } else if (error.status === 423) {
          this.toasts.error('Too many attempts. Try again in a minute.');
        } else if (error.status === 0) {
          this.toasts.error("Can't reach the server. Check your connection.");
        } else {
          this.toasts.error(problemMessage(error));
        }
      }
    } finally {
      this.busy.set(false);
    }
  }
}
