import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ttt-register-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="flex min-h-screen items-center justify-center p-6">
      <section class="animate-fade-in rounded-card border border-surface-border bg-surface p-10 text-center shadow-xl">
        <h1 class="text-xl font-bold">Create account</h1>
        <p class="mt-2 text-sm text-slate-400">Registration arrives in milestone M1.</p>
      </section>
    </main>
  `,
})
export class RegisterPage {}
