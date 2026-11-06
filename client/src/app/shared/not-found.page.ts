import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'ttt-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <main class="flex min-h-screen items-center justify-center p-6">
      <div class="glass-card animate-scale-in p-12 text-center">
        <div class="mx-auto mb-4 grid w-fit grid-cols-3 gap-1" aria-hidden="true">
          <span class="h-4 w-4 rounded bg-surface-border"></span>
          <span class="h-4 w-4 rounded bg-mark-x/80 animate-glow-breathe"></span>
          <span class="h-4 w-4 rounded bg-surface-border"></span>
          <span class="h-4 w-4 rounded bg-mark-o/80 animate-glow-breathe"></span>
          <span class="h-4 w-4 rounded bg-surface-border"></span>
          <span class="h-4 w-4 rounded bg-surface-border"></span>
        </div>
        <p class="font-mono text-5xl font-black text-gradient-cta">404</p>
        <h1 class="mt-3 text-lg font-bold">Not a winning move</h1>
        <p class="mt-2 text-sm text-slate-400">This page doesn't exist on the board.</p>
        <a routerLink="/lobby" class="btn-cta mt-6">Back to the arena</a>
      </div>
    </main>
  `,
})
export class NotFoundPage {}
