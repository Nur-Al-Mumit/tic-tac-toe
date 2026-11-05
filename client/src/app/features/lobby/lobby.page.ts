import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ttt-lobby-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="mx-auto flex min-h-screen max-w-5xl flex-col items-center justify-center p-6">
      <section class="animate-fade-in rounded-card border border-surface-border bg-surface p-12 text-center shadow-xl">
        <h1 class="text-2xl font-bold">Lobby</h1>
        <p class="mt-2 text-sm text-slate-400">Open games, online players, create/join — arrives in milestone M4.</p>
      </section>
    </main>
  `,
})
export class LobbyPage {}
