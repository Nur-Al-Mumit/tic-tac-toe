import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NavbarComponent } from '../../shared/navbar.component';
import { AuthService } from '../../core/auth.service';
import { GamesApi } from '../../core/games.api';
import { SignalRService } from '../../core/signalr.service';
import { problemMessage } from '../../core/problem';
import { GameDto } from '../../core/models';

/** Lobby: waiting games over REST + create/join/cancel; the list updates live via GameCreated/GameRemoved. */
@Component({
  selector: 'ttt-lobby-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NavbarComponent, RouterLink],
  template: `
    <ttt-navbar />
    <main class="mx-auto max-w-3xl px-4 py-8">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-black tracking-tight">Open games</h1>
          <p class="mt-1 text-sm text-slate-400">
            <span class="font-mono {{ connection() === 'connected' ? 'text-success' : 'text-slate-500' }}">
              {{ connection() === 'connected' ? '●' : '○' }} {{ connection() === 'connected' ? 'live' : 'offline' }}
            </span>
            — join someone's game or create your own.
          </p>
        </div>
        <div class="flex items-center gap-2">
          <button (click)="load()" class="btn-ghost">Refresh</button>
          <button (click)="create()" [disabled]="busy()" class="btn-cta">
            + Create game
          </button>
        </div>
      </div>

      @if (activeGame(); as active) {
        <a [routerLink]="['/game', active.id]"
           class="glass-card mb-4 flex items-center justify-between border-primary/40 p-4 transition-colors hover:border-primary">
          <div class="flex items-center gap-3">
            <span class="animate-pulse inline-block h-2.5 w-2.5 rounded-full bg-success"></span>
            <div>
              <p class="text-sm font-bold">
                You're in an active game vs {{ active.playerO ? (active.playerX.id === auth.user()?.id ? active.playerO.username : active.playerX.username) : '— waiting for an opponent' }}
              </p>
              <p class="text-xs text-slate-400">It keeps running while you're away — jump back in, even from another device.</p>
            </div>
          </div>
          <span class="btn-cta">Return to game</span>
        </a>
      }

      @if (error(); as message) {
        <p role="alert" class="mt-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
          {{ message }}
        </p>
      }

      @if (!loading() && games().length === 0) {
        <div class="glass-card mt-8 border-dashed p-12 text-center">
          <div class="mx-auto mb-4 grid w-fit grid-cols-3 gap-1.5 opacity-60" aria-hidden="true">
            @for (cell of emptyBoard; track $index) {
              <span class="h-8 w-8 rounded-lg border border-surface-border bg-bg/60"></span>
            }
          </div>
          <p class="font-bold text-slate-200">No open games right now</p>
          <p class="mt-1 text-sm text-slate-500">Create one — it appears live in every connected lobby.</p>
        </div>
      }

      <ul class="mt-6 space-y-3">
        @for (game of games(); track game.id; let i = $index) {
          <li class="glass-card animate-rise group flex items-center justify-between p-4 transition-[transform,border-color] duration-150 hover:-translate-y-0.5 hover:border-primary/40"
              [style.animation-delay.ms]="i * 45">
            <div class="flex items-center gap-3.5">
              <span class="flex h-11 w-11 items-center justify-center rounded-xl border border-mark-x/30 bg-mark-x/10 font-mono text-lg font-black text-mark-x glow-x">
                {{ game.playerX.username[0].toUpperCase() }}
              </span>
              <div>
                <p class="font-bold">
                  <span class="font-mono text-mark-x">X</span>
                  {{ game.playerX.username }}
                  @if (game.playerO; as opponent) {
                    <span class="mx-1 text-xs font-semibold uppercase tracking-wider text-slate-500">vs</span>
                    <span class="font-mono text-mark-o">O</span>
                    {{ opponent.username }}
                  } @else {
                    <span class="ml-2 rounded-full border border-amber-400/30 bg-amber-400/10 px-2 py-0.5 align-middle text-[10px] font-bold uppercase tracking-wider text-amber-400">
                      waiting
                    </span>
                  }
                </p>
                <p class="mt-0.5 font-mono text-xs text-slate-500">created {{ shortTime(game.createdAtUtc) }}</p>
              </div>
            </div>
            <div class="flex gap-2">
              @if (isMine(game)) {
                <button (click)="cancel(game)" [disabled]="busy()" class="btn-ghost danger">Cancel</button>
              } @else {
                <button (click)="join(game)" [disabled]="busy()" class="btn-cta">Join</button>
              }
            </div>
          </li>
        }
      </ul>
    </main>
  `,
})
export class LobbyPage implements OnInit, OnDestroy {
  private readonly api = inject(GamesApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly hub = inject(SignalRService);

  readonly games = signal<GameDto[]>([]);
  readonly activeGame = signal<GameDto | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly connection = this.hub.state;

  readonly emptyBoard = Array(9).fill(0);

  ngOnInit(): void {
    void this.load();
    void this.api.active().then((g) => this.activeGame.set(g)).catch(() => this.activeGame.set(null));
    // Live lobby: new waiting games appear, joined/cancelled games disappear — no refresh (US-05).
    void this.hub.connect({
      onGameCreated: (game) => this.addGame(game as GameDto),
      onGameRemoved: ({ gameId }) => this.games.update((games) => games.filter((g) => g.id !== gameId)),
    });
  }

  ngOnDestroy(): void {
    void this.hub.disconnect();
  }

  private addGame(game: GameDto): void {
    if (game.status !== 'Waiting') {
      return;
    }
    this.games.update((games) =>
      games.some((g) => g.id === game.id) ? games : [game, ...games],
    );
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
      // The classic case: someone else joined first — the list refresh shows it as gone.
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
