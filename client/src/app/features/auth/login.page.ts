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
      <div class="grid w-full max-w-4xl overflow-hidden rounded-card border border-white/10 shadow-2xl shadow-primary/10 backdrop-blur-sm md:grid-cols-2">
        <!-- brand panel -->
        <section class="relative hidden items-center justify-center overflow-hidden bg-gradient-to-br from-primary/25 via-bg to-bg p-10 md:flex">
          <span class="animate-float-a absolute left-10 top-12 font-mono text-8xl font-black text-mark-x/70 glow-x">X</span>
          <span class="animate-float-b absolute bottom-10 right-10 font-mono text-8xl font-black text-mark-o/70 glow-o">O</span>
          <div class="relative z-10 text-center">
            <div class="mx-auto mb-5 grid w-fit grid-cols-3 gap-1.5" aria-hidden="true">
              @for (cell of brandBoard; track $index) {
                <span class="flex h-9 w-9 items-center justify-center rounded-lg border border-white/10 bg-bg/60 font-mono text-base font-black"
                      [class.text-mark-x]="cell === 'X'" [class.text-mark-o]="cell === 'O'">{{ cell }}</span>
              }
            </div>
            <h2 class="text-2xl font-black tracking-tight">TIC·TAC·TOE</h2>
            <p class="mt-1 text-[11px] font-semibold uppercase tracking-[0.25em] text-slate-400">online arena</p>
            <p class="mx-auto mt-5 max-w-52 text-sm text-slate-400">Real opponents. Live moves. One grid, one winner.</p>
          </div>
        </section>

        <!-- form panel -->
        <section class="bg-surface/80 p-8 sm:p-10">
          <h1 class="text-xl font-extrabold tracking-tight">Welcome back</h1>
          <p class="mt-1 text-sm text-slate-400">Log in with your email or username.</p>

          @if (serverError(); as error) {
            <p role="alert" class="mt-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
              {{ error }}
            </p>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4" novalidate>
            <div>
              <label for="identifier" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Email or username</label>
              <input id="identifier" formControlName="identifier" autocomplete="username" required
                     class="field-input" [class.is-invalid]="submitted() && identifier.invalid" />
              @if (submitted() && identifier.errors?.['required']) {
                <p class="mt-1 text-xs text-red-400">Email or username is required.</p>
              }
            </div>

            <div>
              <label for="password" class="mb-1.5 block text-xs font-semibold uppercase tracking-wider text-slate-400">Password</label>
              <div class="relative">
                <input id="password" formControlName="password" autocomplete="current-password" required
                       [type]="showPassword() ? 'text' : 'password'"
                       class="field-input pr-16" [class.is-invalid]="submitted() && password.invalid" />
                <button type="button" (click)="showPassword.set(!showPassword())"
                        class="absolute inset-y-0 right-2 my-auto h-7 rounded px-2 text-xs font-semibold text-slate-400 hover:text-slate-100">
                  {{ showPassword() ? 'Hide' : 'Show' }}
                </button>
              </div>
              @if (submitted() && password.errors?.['required']) {
                <p class="mt-1 text-xs text-red-400">Password is required.</p>
              }
            </div>

            <button type="submit" [disabled]="busy()" class="btn-cta w-full !py-3">
              @if (busy()) { Logging in… } @else { Enter the arena }
            </button>
          </form>

          <p class="mt-6 text-center text-sm text-slate-400">
            No account yet?
            <a routerLink="/register" class="font-bold text-primary-hover hover:underline">Create one</a>
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

  readonly brandBoard = ['X', 'O', 'X', 'O', 'X', '', '', '', 'O'];

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
