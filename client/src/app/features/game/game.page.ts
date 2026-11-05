import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ttt-game-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="flex min-h-screen items-center justify-center p-6">
      <section class="animate-fade-in rounded-card border border-surface-border bg-surface p-12 text-center shadow-xl">
        <h1 class="text-2xl font-bold">Game</h1>
        <p class="mt-2 text-sm text-slate-400">The live board arrives in milestone M5.</p>
      </section>
    </main>
  `,
})
export class GamePage {}
