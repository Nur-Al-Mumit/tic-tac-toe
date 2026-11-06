import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NavbarComponent } from '../../shared/navbar.component';
import { AuthService } from '../../core/auth.service';
import { GamesApi } from '../../core/games.api';
import { SignalRService } from '../../core/signalr.service';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { GameDto, OnlinePlayerDto } from '../../core/models';

type LobbyTab = 'open' | 'inprogress';

/** Lobby: live game list (tabs) + online players panel; updates arrive over the hub, no refresh. */
@Component({
  selector: 'ttt-lobby-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NavbarComponent, RouterLink],
  template: `
    <ttt-navbar />
    <main class="mx-auto max-w-6xl px-4 py-8">
      @if (activeGame(); as active) {
        <div class="glass-card mb-4 flex flex-wrap items-center justify-between gap-3 border-primary/40 p-4">
          <div class="flex items-center gap-3">
            <span class="animate-pulse inline-block h-2.5 w-2.5 rounded-full bg-success"></span>
            <div>
              <p class="text-sm font-bold">
                You're in an active game vs {{ active.playerO ? (active.playerX.id === auth.user()?.id ? active.playerO.username : active.playerX.username) : '— waiting for an opponent' }}
              </p>
              <p class="text-xs text-slate-400">It keeps running while you're away. Rejoin from any device — or resign and move on.</p>
            </div>
          </div>
          <div class="flex gap-2">
            <a [routerLink]="['/game', active.id]" class="btn-cta">Return to game</a>
            <button (click)="resign(active.id)" [disabled]="busy()" class="btn-ghost danger">Quit &amp; resign</button>
          </div>
        </div>
      }

      <div class="grid gap-6 lg:grid-cols-3">
        <!-- games -->
        <section class="lg:col-span-2">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div class="flex items-center gap-1 rounded-xl border border-surface-border bg-surface/70 p-1" role="tablist" aria-label="Game filters">
              <button role="tab" [attr.aria-selected]="tab() === 'open'" (click)="tab.set('open')"
                      class="rounded-lg px-3.5 py-1.5 text-sm font-bold transition-colors {{ tab() === 'open' ? 'bg-primary text-white' : 'text-slate-400 hover:text-slate-100' }}">
                Open @if (openGames().length) { <span class="ml-1 font-mono text-xs opacity-70">{{ openGames().length }}</span> }
              </button>
              <button role="tab" [attr.aria-selected]="tab() === 'inprogress'" (click)="tab.set('inprogress')"
                      class="rounded-lg px-3.5 py-1.5 text-sm font-bold transition-colors {{ tab() === 'inprogress' ? 'bg-primary text-white' : 'text-slate-400 hover:text-slate-100' }}">
                In progress @if (runningGames().length) { <span class="ml-1 font-mono text-xs opacity-70">{{ runningGames().length }}</span> }
              </button>
            </div>
            <span class="font-mono text-xs uppercase tracking-wider {{ connection() === 'connected' ? 'text-success' : 'text-slate-500' }}">
              {{ connection() === 'connected' ? '●' : '○' }} {{ connection() === 'connected' ? 'live' : 'offline' }}
            </span>
            <button (click)="load()" class="btn-ghost">Refresh</button>
            <button (click)="create()" [disabled]="busy()" class="btn-cta">+ Create game</button>
          </div>

          @if (error(); as message) {
            <p role="alert" class="mt-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">{{ message }}</p>
          }

          @if (!loading() && shownGames().length === 0) {
            <div class="glass-card mt-5 border-dashed p-10 text-center">
              <p class="font-bold text-slate-200">{{ tab() === 'open' ? 'No open games right now' : 'No games in progress' }}</p>
              @if (tab() === 'open') {
                <p class="mt-1 text-sm text-slate-500">Create one — it appears live in every connected lobby.</p>
              }
            </div>
          }

          <ul class="mt-5 space-y-3">
            @for (game of shownGames(); track game.id; let i = $index) {
              <li class="glass-card animate-rise group flex items-center justify-between p-4 transition-[transform,border-color] duration-150 hover:-translate-y-0.5 hover:border-primary/40"
                  [style.animation-delay.ms]="i * 45">
                <div class="flex items-center gap-3.5">
                  <span class="flex h-11 w-11 items-center justify-center rounded-xl border font-mono text-lg font-black"
                        [class.border-mark-x/30]="!game.playerO" [class.bg-mark-x/10]="!game.playerO" [class.text-mark-x]="!game.playerO"
                        [class.border-surface-border]="game.playerO" [class.text-slate-300]="game.playerO">
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
                        <span class="ml-2 rounded-full border border-amber-400/30 bg-amber-400/10 px-2 py-0.5 align-middle text-[10px] font-bold uppercase tracking-wider text-amber-400">waiting</span>
                      }
                    </p>
                    <p class="mt-0.5 font-mono text-xs text-slate-500">created {{ shortTime(game.createdAtUtc) }}</p>
                  </div>
                </div>
                <div class="flex gap-2">
                  @if (isMine(game) && game.status === 'Waiting') {
                    <button (click)="cancel(game)" [disabled]="busy()" class="btn-ghost danger">Cancel</button>
                  } @else if (game.status === 'Waiting') {
                    <button (click)="join(game)" [disabled]="busy()" class="btn-cta">Join</button>
                  } @else if (game.status === 'InProgress' && isMine(game)) {
                    <a [routerLink]="['/game', game.id]" class="btn-cta">Resume</a>
                  }
                </div>
              </li>
            }
          </ul>
        </section>

        <!-- online players -->
        <aside class="glass-card h-fit p-4" aria-label="Online players">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-extrabold uppercase tracking-wider text-slate-300">Online</h2>
            <span class="font-mono text-xs text-success">{{ players().length }} online</span>
          </div>
          <ul class="mt-3 space-y-1.5">
            @for (player of players(); track player.id; let i = $index) {
              <li class="animate-rise flex items-center gap-2.5 rounded-lg px-2 py-1.5 transition-colors hover:bg-white/5"
                  [style.animation-delay.ms]="i * 30">
                <span class="relative flex h-2 w-2">
                  <span class="absolute inline-flex h-full w-full rounded-full {{ player.inGame ? 'bg-amber-400' : 'bg-success' }} opacity-60"></span>
                  <span class="relative inline-flex h-2 w-2 rounded-full {{ player.inGame ? 'bg-amber-400' : 'bg-success' }}"></span>
                </span>
                <span class="flex h-7 w-7 items-center justify-center rounded-full border border-primary/30 bg-primary/10 font-mono text-xs font-bold text-primary">
                  {{ player.username[0].toUpperCase() }}
                </span>
                <span class="truncate text-sm font-semibold" [class.text-slate-500]="player.id === auth.user()?.id">
                  {{ player.username }}{{ player.id === auth.user()?.id ? ' (you)' : '' }}
                </span>
                <span class="ml-auto text-[10px] font-bold uppercase tracking-wider {{ player.inGame ? 'text-amber-400' : 'text-success' }}">
                  {{ player.inGame ? 'in game' : 'available' }}
                </span>
              </li>
            } @empty {
              <li class="px-2 py-6 text-center text-sm text-slate-500">Nobody else online</li>
            }
          </ul>
        </aside>
      </div>
    </main>
  `,
})
export class LobbyPage implements OnInit, OnDestroy {
  private readonly api = inject(GamesApi);
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly hub = inject(SignalRService);
  private readonly toasts = inject(ToastService);

  readonly games = signal<GameDto[]>([]);
  readonly players = signal<OnlinePlayerDto[]>([]);
  readonly activeGame = signal<GameDto | null>(null);
  readonly tab = signal<LobbyTab>('open');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly connection = this.hub.state;

  readonly openGames = computed(() => this.games().filter((g) => g.status === 'Waiting'));
  readonly runningGames = computed(() => this.games().filter((g) => g.status === 'InProgress'));
  readonly shownGames = computed(() => (this.tab() === 'open' ? this.openGames() : this.runningGames()));

  ngOnInit(): void {
    void this.load();
    void this.api.online().then((p) => this.players.set(p)).catch(() => this.players.set([]));
    void this.api.active().then((g) => this.activeGame.set(g)).catch(() => this.activeGame.set(null));

    // Live lobby (US-04/US-05): games and presence update over the hub — no refresh.
    void this.hub.connect({
      onGameCreated: (game) => this.addGame(game as GameDto),
      onGameUpdated: (game) => this.updateGame(game as GameDto),
      onGameRemoved: ({ gameId }) => this.games.update((games) => games.filter((g) => g.id !== gameId)),
      onPresenceChanged: ({ players }) => this.players.set(players),
    });
  }

  ngOnDestroy(): void {
    void this.hub.disconnect();
  }

  private addGame(game: GameDto): void {
    if (game.status !== 'Waiting') {
      return;
    }
    this.games.update((games) => (games.some((g) => g.id === game.id) ? games : [game, ...games]));
  }

  private updateGame(game: GameDto): void {
    this.games.update((games) => {
      const rest = games.filter((g) => g.id !== game.id);
      return game.status === 'Waiting' || game.status === 'InProgress' ? [game, ...rest] : rest;
    });
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.games.set(await this.api.list());
      this.players.set(await this.api.online().catch(() => this.players()));
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

  /** Quit the running game: forfeit on the server, then refresh the lobby state. */
  async resign(gameId: string): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.leave(gameId);
      this.activeGame.set(null);
      this.toasts.info('You resigned the game.');
      await this.load();
    } catch (error) {
      this.error.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
    } finally {
      this.busy.set(false);
    }
  }

  isMine(game: GameDto): boolean {
    return this.auth.user()?.id === game.playerX.id || this.auth.user()?.id === game.playerO?.id;
  }

  shortTime(isoUtc: string): string {
    return new Date(isoUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
}
