import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NavbarComponent } from '../../shared/navbar.component';
import { AuthService } from '../../core/auth.service';
import { GamesApi } from '../../core/games.api';
import { problemMessage } from '../../core/problem';
import { GameDto } from '../../core/models';

/** V1 lobby slice: waiting games over REST + create/join/cancel (live updates arrive in V1.1). */
@Component({
  selector: 'ttt-lobby-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NavbarComponent],
  template: `
    <ttt-navbar />
    <main class="mx-auto max-w-3xl px-4 py-8">
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold">Open games</h1>
          <p class="mt-1 text-sm text-slate-400">Join someone's game or create your own.</p>
        </div>
        <div class="flex gap-2">
          <button (click)="load()"
                  class="rounded-lg border border-surface-border px-3 py-2 text-sm text-slate-300 transition-colors hover:border-primary hover:text-slate-100">
            @if (loading()) { Loading… } @else { Refresh }
          </button>
          <button (click)="create()"
                  [disabled]="busy()"
                  class="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-slate-950 transition-colors hover:bg-primary-hover disabled:opacity-50">
            + Create game
          </button>
        </div>
      </div>

      @if (error(); as message) {
        <p role="alert" class="mt-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
          {{ message }}
        </p>
      }

      @if (!loading() && games().length === 0) {
        <div class="mt-8 rounded-card border border-dashed border-surface-border bg-surface/50 p-12 text-center">
          <p class="font-mono text-3xl">∅</p>
          <p class="mt-2 text-sm text-slate-400">No open games right now — create one and wait for an opponent.</p>
        </div>
      }

      <ul class="mt-6 space-y-3">
        @for (game of games(); track game.id) {
          <li class="animate-fade-in flex items-center justify-between rounded-card border border-surface-border bg-surface p-4">
            <div>
              <p class="font-semibold">
                <span class="font-mono text-mark-x">X</span>
                {{ game.playerX.username }}
                @if (game.playerO; as opponent) {
                  <span class="text-slate-500">vs</span>
                  <span class="font-mono text-mark-o">O</span>
                  {{ opponent.username }}
                }
              </p>
              <p class="mt-0.5 text-xs text-slate-500">
                {{ game.status }} · created {{ shortTime(game.createdAtUtc) }}
              </p>
            </div>
            <div class="flex gap-2">
              @if (isMine(game)) {
                <button (click)="cancel(game)"
                        class="rounded-lg border border-surface-border px-3 py-2 text-sm text-slate-300 transition-colors hover:border-danger hover:text-red-400">
                  Cancel
                </button>
              } @else {
                <button (click)="join(game)"
                        [disabled]="busy()"
                        class="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-slate-950 transition-colors hover:bg-primary-hover disabled:opacity-50">
                  Join
                </button>
              }
            </div>
          </li>
        }
      </ul>
    </main>
  `,
})
export class LobbyPage implements OnInit {
  private readonly api = inject(GamesApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly games = signal<GameDto[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.games.set(await this.api.listWaiting());
    } catch (error) {
      this.error.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
    } finally {
      this.loading.set(false);
    }
  }

  async create(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const game = await this.api.create();
      await this.router.navigateByUrl(`/game/${game.id}`);
    } catch (error) {
      this.error.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
    } finally {
      this.busy.set(false);
    }
  }

  async join(game: GameDto): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.join(game.id);
      await this.router.navigateByUrl(`/game/${game.id}`);
    } catch (error) {
      // The classic V1 case: someone else joined first — the list refresh shows it as gone.
      this.error.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  async cancel(game: GameDto): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.cancel(game.id);
      this.games.update((games) => games.filter((g) => g.id !== game.id));
    } catch (error) {
      this.error.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
    } finally {
      this.busy.set(false);
    }
  }

  isMine(game: GameDto): boolean {
    return this.auth.user()?.id === game.playerX.id;
  }

  shortTime(isoUtc: string): string {
    return new Date(isoUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
