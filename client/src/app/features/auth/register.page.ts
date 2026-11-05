import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { problemMessage } from '../../core/problem';

@Component({
  selector: 'ttt-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <main class="flex min-h-screen items-center justify-center p-6">
      <section class="animate-fade-in w-full max-w-md rounded-card border border-surface-border bg-surface p-8 shadow-xl">
        <h1 class="text-xl font-bold">Create your account</h1>
        <p class="mt-1 text-sm text-slate-400">Sign up in seconds — no email verification needed.</p>

        @if (serverError(); as error) {
          <p role="alert" class="mt-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
            {{ error }}
          </p>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4" novalidate>
          <div>
            <label for="username" class="mb-1 block text-sm font-medium text-slate-300">Username</label>
            <input id="username" formControlName="username" autocomplete="username" required
                   class="w-full rounded-lg border border-surface-border bg-bg px-3 py-2 text-sm outline-none transition-colors focus:border-primary"
                   [class.border-danger]="showError(username)" />
            @if (showError(username); as message) { <p class="mt-1 text-xs text-red-400">{{ message }}</p> }
          </div>

          <div>
            <label for="email" class="mb-1 block text-sm font-medium text-slate-300">Email</label>
            <input id="email" formControlName="email" autocomplete="email" required
                   class="w-full rounded-lg border border-surface-border bg-bg px-3 py-2 text-sm outline-none transition-colors focus:border-primary"
                   [class.border-danger]="showError(email)" />
            @if (showError(email); as message) { <p class="mt-1 text-xs text-red-400">{{ message }}</p> }
          </div>

          <div>
            <label for="password" class="mb-1 block text-sm font-medium text-slate-300">Password</label>
            <div class="relative">
              <input id="password" formControlName="password" autocomplete="new-password" required
                     [type]="showPassword() ? 'text' : 'password'"
                     class="w-full rounded-lg border border-surface-border bg-bg px-3 py-2 pr-14 text-sm outline-none transition-colors focus:border-primary"
                     [class.border-danger]="showError(password)" />
              <button type="button" (click)="showPassword.set(!showPassword())"
                      class="absolute inset-y-0 right-2 my-auto h-7 rounded px-2 text-xs text-slate-400 hover:text-slate-200">
                {{ showPassword() ? 'Hide' : 'Show' }}
              </button>
            </div>
            <div class="mt-1.5 h-1 w-full overflow-hidden rounded-full bg-surface-border">
              <div class="h-full transition-all duration-300"
                   [style.width.%]="strength().percent"
                   [style.background-color]="strength().color"></div>
            </div>
            @if (showError(password); as message) { <p class="mt-1 text-xs text-red-400">{{ message }}</p> }
          </div>

          <div>
            <label for="confirm" class="mb-1 block text-sm font-medium text-slate-300">Confirm password</label>
            <input id="confirm" formControlName="confirm" autocomplete="new-password" required
                   type="password"
                   class="w-full rounded-lg border border-surface-border bg-bg px-3 py-2 text-sm outline-none transition-colors focus:border-primary"
                   [class.border-danger]="submitted() && confirm.invalid" />
            @if (submitted() && confirm.errors?.['required']) {
              <p class="mt-1 text-xs text-red-400">Please confirm the password.</p>
            } @else if (submitted() && form.errors?.['passwordMismatch']) {
              <p class="mt-1 text-xs text-red-400">Passwords do not match.</p>
            }
          </div>

          <button type="submit" [disabled]="busy()"
                  class="w-full rounded-lg bg-primary px-4 py-2.5 text-sm font-semibold text-slate-950 transition-colors hover:bg-primary-hover disabled:opacity-50">
            @if (busy()) { <span class="inline-block animate-pulse">Creating…</span> } @else { Create account }
          </button>
        </form>

        <p class="mt-6 text-center text-sm text-slate-400">
          Already registered?
          <a routerLink="/login" class="font-semibold text-primary hover:underline">Log in</a>
        </p>
      </section>
    </main>
  `,
})
export class RegisterPage {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

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

  showError(control: { invalid: boolean; touched: boolean } | { invalid: boolean; touched: boolean }): string | null {
    if (!this.submitted() && !control.touched) return null;
    if (!control.invalid) return null;
    return 'Please check this value.';
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
      await this.router.navigateByUrl('/lobby');
    } catch (error) {
      this.serverError.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
    } finally {
      this.busy.set(false);
    }
  }
}
