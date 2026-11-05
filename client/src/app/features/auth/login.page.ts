import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, NonNullableFormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { problemMessage } from '../../core/problem';

@Component({
  selector: 'ttt-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <main class="flex min-h-screen items-center justify-center p-6">
      <section class="animate-fade-in w-full max-w-md rounded-card border border-surface-border bg-surface p-8 shadow-xl">
        <h1 class="text-xl font-bold">Welcome back</h1>
        <p class="mt-1 text-sm text-slate-400">Log in with your email or username.</p>

        @if (serverError(); as error) {
          <p role="alert" class="mt-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
            {{ error }}
          </p>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4" novalidate>
          <div>
            <label for="identifier" class="mb-1 block text-sm font-medium text-slate-300">Email or username</label>
            <input id="identifier" formControlName="identifier" autocomplete="username" required
                   class="w-full rounded-lg border border-surface-border bg-bg px-3 py-2 text-sm outline-none transition-colors focus:border-primary"
                   [class.border-danger]="submitted() && identifier.invalid" />
            @if (submitted() && identifier.errors?.['required']) {
              <p class="mt-1 text-xs text-red-400">Email or username is required.</p>
            }
          </div>

          <div>
            <div class="mb-1 flex items-baseline justify-between">
              <label for="password" class="block text-sm font-medium text-slate-300">Password</label>
            </div>
            <div class="relative">
              <input id="password" formControlName="password" autocomplete="current-password" required
                     [type]="showPassword() ? 'text' : 'password'"
                     class="w-full rounded-lg border border-surface-border bg-bg px-3 py-2 pr-14 text-sm outline-none transition-colors focus:border-primary"
                     [class.border-danger]="submitted() && password.invalid" />
              <button type="button" (click)="showPassword.set(!showPassword())"
                      class="absolute inset-y-0 right-2 my-auto h-7 rounded px-2 text-xs text-slate-400 hover:text-slate-200">
                {{ showPassword() ? 'Hide' : 'Show' }}
              </button>
            </div>
            @if (submitted() && password.errors?.['required']) {
              <p class="mt-1 text-xs text-red-400">Password is required.</p>
            }
          </div>

          <button type="submit" [disabled]="busy()"
                  class="w-full rounded-lg bg-primary px-4 py-2.5 text-sm font-semibold text-slate-950 transition-colors hover:bg-primary-hover disabled:opacity-50">
            @if (busy()) { <span class="inline-block animate-pulse">Logging in…</span> } @else { Log in }
          </button>
        </form>

        <p class="mt-6 text-center text-sm text-slate-400">
          No account yet?
          <a routerLink="/register" class="font-semibold text-primary hover:underline">Create one</a>
        </p>
      </section>
    </main>
  `,
})
export class LoginPage {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = this.fb.group({
    identifier: ['', Validators.required],
    password: ['', Validators.required],
  });

  readonly busy = signal(false);
  readonly submitted = signal(false);
  readonly showPassword = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly identifier = this.form.controls.identifier;
  readonly password = this.form.controls.password;

  async submit(): Promise<void> {
    this.submitted.set(true);
    this.serverError.set(null);
    if (this.form.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    try {
      await this.auth.login(this.identifier.value, this.password.value);
      await this.router.navigateByUrl('/lobby');
    } catch (error) {
      this.serverError.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
    } finally {
      this.busy.set(false);
    }
  }
}
