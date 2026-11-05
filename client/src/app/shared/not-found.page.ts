import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'ttt-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="flex min-h-screen items-center justify-center p-6">
      <div class="rounded-card border border-surface-border bg-surface p-10 text-center shadow-xl">
        <p class="font-mono text-5xl font-bold text-primary">404</p>
        <h1 class="mt-3 text-lg font-semibold text-slate-100">Page not found</h1>
        <p class="mt-2 text-sm text-slate-400">The page you are looking for does not exist.</p>
        <a routerLink="/lobby"
           class="mt-6 inline-block rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-slate-950 transition-colors hover:bg-primary-hover">
          Back to lobby
        </a>
      </div>
    </main>
  `,
})
export class NotFoundPage {}
