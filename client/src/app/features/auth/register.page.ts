import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';

@Component({
  selector: 'ttt-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <main class="flex min-h-screen items-center justify-center p-4 sm:p-6">
      <div class="grid w-full max-w-4xl overflow-hidden rounded-card border border-white/10 shadow-2xl shadow-primary/10 backdrop-blur-sm md:grid-cols-2">
        <!-- brand panel (mirrored: marks drift the other way) -->
        <section aria-hidden="true" class="relative hidden items-center justify-center overflow-hidden bg-gradient-to-bl from-mark-o/20 via-bg to-bg p-10 md:flex md:order-2">
          <span class="animate-float-b absolute left-10 top-14 font-mono text-8xl font-black text-mark-o/60 glow-o">O</span>
          <span class="animate-float-a absolute bottom-16 right-12 font-mono text-7xl font-black text-mark-x/60 glow-x">X</span>
          <div class="relative z-10 text-center">
            <div class="mx-auto mb-5 grid w-fit grid-cols-3 gap-1.5">
              @for (cell of brandBoard; track $index) {
                <span class="flex h-9 w-9 items-center justify-center rounded-lg border border-white/10 bg-bg/60 font-mono text-base font-black"
                      [class.text-mark-x]="cell === 'X'" [class.text-mark-o]="cell === 'O'">{{ cell }}</span>
              }
            </div>
            <h2 class="text-2xl font-black tracking-tight">Tic-Tac-Toe</h2>
            <p class="mt-1 text-[11px] font-semibold uppercase tracking-[0.25em] text-slate-400">Your board is waiting.</p>
          </div>
        </section>

        <!-- form panel -->
        <section class="bg-surface/80 p-8 sm:p-10">
          <div class="mb-6 flex items-center justify-center gap-3 md:hidden" aria-hidden="true">
            <span class="font-mono text-2xl font-black text-mark-o">O</span>
            <span class="text-sm font-black uppercase tracking-widest text-slate-200">Tic-Tac-Toe</span>
            <span class="font-mono text-2xl font-black text-mark-x">X</span>
          </div>

          <h1 class="text-xl font-extrabold tracking-tight">Create account</h1>
          <p class="mt-1 text-sm text-slate-400">Seconds to your first game</p>

          @if (serverError(); as message) {
            <p role="alert" class="mt-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
              {{ message }}
            </p>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4" novalidate>
            <div>
              <label for="username" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Username</label>
              <input id="username" formControlName="username" autocomplete="username" required placeholder="sara_01"
                     class="field-input" [class.is-invalid]="showError(username)"
                     [readonly]="busy()" [attr.aria-invalid]="!!showError(username)"
                     [attr.aria-describedby]="showError(username) ? 'username-error' : null" />
              @if (showError(username); as message) { <p id="username-error" class="mt-1 text-xs text-red-400">{{ message }}</p> }
            </div>

            <div>
              <label for="email" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Email</label>
              <input id="email" formControlName="email" autocomplete="email" required placeholder="you@example.com"
                     class="field-input" [class.is-invalid]="showError(email)"
                     [readonly]="busy()" [attr.aria-invalid]="!!showError(email)"
                     [attr.aria-describedby]="showError(email) ? 'email-error' : null" />
              @if (showError(email); as message) { <p id="email-error" class="mt-1 text-xs text-red-400">{{ message }}</p> }
            </div>

            <div>
              <label for="password" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Password</label>
              <div class="relative">
                <input id="password" formControlName="password" autocomplete="new-password" required
                       [type]="showPassword() ? 'text' : 'password'"
                       class="field-input pr-16" [class.is-invalid]="showError(password)"
                       [readonly]="busy()" [attr.aria-invalid]="!!showError(password)"
                       [attr.aria-describedby]="showError(password) ? 'password-error' : null" />
                <button type="button" (click)="showPassword.set(!showPassword())"
                        [attr.aria-pressed]="showPassword()" [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
                        class="absolute inset-y-0 right-2 my-auto h-7 rounded px-2 text-xs font-semibold text-slate-400 hover:text-slate-100">
                  {{ showPassword() ? 'Hide' : 'Show' }}
                </button>
              </div>
              <div class="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-surface-border" aria-hidden="true">
                <div class="h-full transition-all duration-300" [style.width.%]="strength().percent" [style.background-color]="strength().color"></div>
              </div>
              @if (showError(password); as message) { <p id="password-error" class="mt-1 text-xs text-red-400">{{ message }}</p> }
            </div>

            <div>
              <label for="confirm" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Confirm password</label>
              <input id="confirm" formControlName="confirm" autocomplete="new-password" required type="password"
                     class="field-input" [class.is-invalid]="confirmTouched() && form.invalid"
                     [readonly]="busy()" aria-describedby="confirm-error" />
              @if (confirmTouched() && form.errors?.['passwordMismatch']) {
                <p id="confirm-error" class="mt-1 text-xs text-red-400">Passwords don't match.</p>
              }
            </div>

            <button type="submit" [disabled]="busy()" [attr.aria-busy]="busy()" class="btn-cta w-full !py-3">
              @if (busy()) {
                <span class="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white" aria-hidden="true"></span>
                Creating account…
              } @else {
                Register
              }
            </button>
          </form>

          <p class="mt-6 text-center text-sm text-slate-400">
            Already playing? <a routerLink="/login" class="font-bold text-primary-hover hover:underline">Log in</a>
          </p>
        </section>
      </div>
    </main>
  `,
})
export class RegisterPage {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  readonly brandBoard = ['O', 'X', '', 'X', 'O', '', '', 'X', 'O'];

  readonly form = this.fb.group(
    {
      username: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9_]{3,20}$/)]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/[A-Z]/), Validators.pattern(/[a-z]/), Validators.pattern(/[0-9]/)]],
      confirm: ['', Validators.required],
    },
    { validators: (group) => (group.value.password === group.value.confirm ? null : { passwordMismatch: true }) },
  );

  readonly busy = signal(false);
  readonly submitted = signal(false);
  readonly showPassword = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly username = this.form.controls.username;
  readonly email = this.form.controls.email;
  readonly password = this.form.controls.password;
  readonly confirm = this.form.controls.confirm;

  readonly strength = computed(() => {
    const value = this.password.value ?? '';
    let score = 0;
    if (value.length >= 8) score++;
    if (/[A-Z]/.test(value)) score++;
    if (/[a-z]/.test(value)) score++;
    if (/[0-9]/.test(value)) score++;
    const percent = value.length === 0 ? 0 : (score / 4) * 100;
    const color = score <= 1 ? '#f87171' : score <= 2 ? '#fbbf24' : score <= 3 ? '#38bdf8' : '#34d399';
    return { percent, color };
  });

  confirmTouched(): boolean {
    return this.submitted() || this.confirm.touched;
  }

  showError(control: { invalid: boolean; touched: boolean; errors: Record<string, unknown> | null }): string | null {
    if (!this.submitted() && !control.touched) return null;
    if (!control.invalid) return null;
    if (control.errors?.['required']) return 'This field is required.';
    if (control === this.username) return 'Username must be 3–20 characters: letters, digits, underscore.';
    if (control === this.email) return 'Enter a valid email address.';
    return 'Use 8+ characters with upper, lower and a digit.';
  }

  async submit(): Promise<void> {
    this.submitted.set(true);
    this.serverError.set(null);
    if (this.form.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    try {
      await this.auth.register(this.username.value, this.email.value, this.password.value);
      this.toasts.success('Account created — welcome aboard!');
      await this.router.navigateByUrl('/lobby');
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const problem = error.error as { errorCode?: string } | null;
        if (problem?.errorCode === 'Auth.DuplicateUsername') {
          this.serverError.set('That username is taken. Try another.');
        } else if (problem?.errorCode === 'Auth.DuplicateEmail') {
          this.serverError.set('An account with this email already exists.');
        } else {
          this.serverError.set(problemMessage(error));
        }
      }
    } finally {
      this.busy.set(false);
    }
  }
}
