import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NavbarComponent } from '../../shared/navbar.component';
import { AuthService } from '../../core/auth.service';
import { GamesApi } from '../../core/games.api';
import { SignalRService } from '../../core/signalr.service';
import { problemMessage } from '../../core/problem';
import { GameDto } from '../../core/models';
import { GameBoardComponent } from './game-board.component';
import { DemoBoardComponent } from '../../shared/demo-board.component';

/** V1 game page: live board via SignalR, explicit Leave forfeit, reload = full resync. */
@Component({
  selector: 'ttt-game-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NavbarComponent, GameBoardComponent, DemoBoardComponent],
  template: `
    <ttt-navbar />
    <main class="mx-auto flex max-w-lg flex-col items-center px-4 py-8">
      @if (error(); as message) {
        <p role="alert" class="mb-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
          {{ message }}
        </p>
      }

      @if (game(); as game) {
        <!-- player tiles + turn indicator -->
        <div class="glass-card mb-5 flex w-full items-center justify-between gap-3 px-4 py-3">
          <div class="flex items-center gap-3 rounded-xl px-2 py-1.5 transition-shadow"
               [class.animate-pulse-ring]="game.status === 'InProgress' && game.currentTurn === 'X'">
            <span class="flex h-10 w-10 items-center justify-center rounded-xl border border-mark-x/40 bg-mark-x/10 font-mono text-lg font-black text-mark-x glow-x">
              {{ game.playerX.username[0].toUpperCase() }}
            </span>
            <div>
              <p class="text-sm font-bold">{{ game.playerX.username }}{{ isMe(game.playerX.id) ? ' (you)' : '' }}</p>
              <p class="text-xs text-slate-500">{{ game.status === 'Waiting' ? 'waiting for opponent…' : 'Player X' }}</p>
            </div>
          </div>
          <div class="flex flex-col items-center">
            <span class="font-mono text-[10px] uppercase tracking-wider"
                  [class.text-success]="connection() === 'connected'"
                  [class.text-amber-400]="connection() === 'reconnecting'"
                  [class.text-slate-500]="connection() === 'disconnected'">
              {{ connection() === 'connected' ? '● live' : connection() === 'reconnecting' ? '● reconnecting' : '● offline' }}
            </span>
            <span class="mt-0.5 font-mono text-lg font-black text-slate-600">VS</span>
            @if (!opponentConnected() && game.status === 'InProgress') {
              <span class="mt-0.5 whitespace-nowrap text-[10px] font-bold uppercase tracking-wider text-amber-400" aria-live="polite">
                ⚠ opponent away
              </span>
            }
          </div>
          <div class="flex items-center gap-3 rounded-xl px-2 py-1.5 text-right transition-shadow"
               [class.animate-pulse-ring]="game.status === 'InProgress' && game.currentTurn === 'O'">
            <div>
              <p class="text-sm font-bold">
                {{ game.playerO ? game.playerO.username + (isMe(game.playerO.id) ? ' (you)' : '') : 'waiting…' }}
              </p>
              <p class="text-xs text-slate-500">Player O</p>
            </div>
            <span class="flex h-10 w-10 items-center justify-center rounded-xl border font-mono text-lg font-black"
                  [class.border-mark-o/40]="game.playerO" [class.bg-mark-o/10]="game.playerO"
                  [class.text-mark-o]="game.playerO" [class.glow-o]="game.playerO"
                  [class.border-surface-border]="!game.playerO" [class.text-slate-600]="!game.playerO">
              {{ game.playerO ? game.playerO.username[0].toUpperCase() : '?' }}
            </span>
          </div>
        </div>

        <!-- waiting state -->
        @if (game.status === 'Waiting') {
          <section class="glass-card animate-rise flex w-full flex-col items-center gap-5 px-6 py-10" aria-live="polite">
            <!-- demo match: plays a random game while you wait -->
            <ttt-demo-board size="lg" />

            <div class="text-center">
              <h2 class="text-shimmer animate-text-shimmer text-lg font-bold">Waiting for an opponent<span class="wait-dots ml-0.5" aria-hidden="true"><span>.</span><span>.</span><span>.</span></span></h2>
              <p class="mt-1 text-xs text-slate-500">Your game is live in the lobby — anyone can join.</p>
            </div>

            <!-- status details -->
            <dl class="w-full rounded-xl border border-surface-border/70 bg-bg/40 px-4 py-3 font-mono text-xs">
              <div class="flex items-center justify-between gap-3">
                <dt class="uppercase tracking-wider text-slate-500">Status</dt>
                <dd class="flex items-center gap-1.5 font-semibold"
                    [class.text-success]="connection() === 'connected'"
                    [class.text-amber-400]="connection() === 'reconnecting'"
                    [class.text-slate-500]="connection() === 'disconnected'">
                  <span class="inline-block h-1.5 w-1.5 animate-pulse rounded-full bg-current" aria-hidden="true"></span>
                  {{ connection() === 'connected' ? 'Searching' : connection() === 'reconnecting' ? 'Reconnecting' : 'Offline' }}
                </dd>
              </div>
              <div class="mt-2 flex items-center justify-between gap-3 border-t border-surface-border/60 pt-2">
                <dt class="uppercase tracking-wider text-slate-500">You play as</dt>
                <dd class="font-black {{ myMark() === 'X' ? 'text-mark-x' : 'text-mark-o' }}">{{ myMark() ?? 'X' }}</dd>
              </div>
              <div class="mt-2 flex items-center justify-between gap-3 border-t border-surface-border/60 pt-2">
                <dt class="uppercase tracking-wider text-slate-500">Game</dt>
                <dd class="max-w-40 truncate text-slate-400" [title]="game.id">#{{ shortId(game.id) }}</dd>
              </div>
            </dl>

            <button (click)="cancel()" class="btn-ghost danger mt-1">Cancel game</button>
          </section>
        } @else {
          <ttt-game-board [game]="game" [myMark]="myMark()" [shake]="shake()" (placed)="place($event)" />

          @if (game.status === 'InProgress') {
            <div class="mt-5 flex w-full items-center justify-between">
              <p class="text-sm font-semibold" aria-live="polite">
                @if (game.currentTurn === myMark()) {
                  <span class="mr-1.5 inline-block h-2 w-2 animate-pulse rounded-full bg-success align-middle"></span>
                  Your turn — you are <span class="font-mono font-black {{ myMark() === 'X' ? 'text-mark-x' : 'text-mark-o' }}">{{ myMark() }}</span>
                } @else {
                  <span class="mr-1.5 inline-block h-2 w-2 rounded-full bg-slate-600 align-middle"></span>
                  Opponent's turn…
                }
                @if (secondsLeft() !== null) {
                  <span class="ml-2 font-mono text-base font-bold tabular-nums align-middle {{ secondsLeft()! <= 10 ? 'text-danger animate-pulse' : 'text-slate-300' }}"
                        aria-label="Time left on the clock">
                    ⏱ {{ clockLabel() }}
                  </span>
                }
              </p>
              <button (click)="leave()" class="btn-ghost danger">Leave (forfeit)</button>
            </div>
          }
        }

        <!-- result modal -->
        @if (game.status === 'Finished') {
          <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4" role="dialog" aria-modal="true">
            <div class="animate-fade-in w-full max-w-sm rounded-card border border-surface-border bg-surface p-8 text-center">
              <p class="font-mono text-4xl font-bold {{ outcomeClass(game.result?.outcome) }}">
                {{ headline(game) }}
              </p>
              <p class="mt-2 text-sm text-slate-400">{{ subline(game) }}</p>
              <button (click)="backToLobby()"
                      class="mt-6 w-full rounded-lg bg-primary px-4 py-2.5 text-sm font-semibold text-slate-950 transition-colors hover:bg-primary-hover">
                Back to lobby
              </button>
            </div>
          </div>
        }
      }
    </main>
  `,
})
export class GamePage implements OnInit, OnDestroy {
  readonly id = input.required<string>();

  private readonly api = inject(GamesApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly hub = inject(SignalRService);

  readonly game = signal<GameDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly shake = signal(false);
  readonly connection = this.hub.state;
  readonly opponentConnected = signal(true);

  /** Ticks every second so the move clock counts down (JetBrains Mono per the design system). */
  readonly now = signal(Date.now());
  private clock: ReturnType<typeof setInterval> | null = null;

  readonly myMark = computed<'X' | 'O' | null>(() => {
    const game = this.game();
    const me = this.auth.user()?.id;
    if (!game || !me) {
      return null;
    }
    return game.playerX.id === me ? 'X' : game.playerO?.id === me ? 'O' : null;
  });

  async ngOnInit(): Promise<void> {
    const gameId = this.id();
    try {
      this.game.set(await this.api.byId(gameId));
    } catch (error) {
      this.error.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
      return;
    }

    await this.hub.connect({
      onGameStarted: (game) => this.apply(game),
      onMoveMade: (game) => this.apply(game),
      onGameFinished: (game) => this.apply(game),
      onGameCancelled: () => void this.router.navigateByUrl('/lobby'),
      onOpponentConnection: ({ playerId, connected }) => {
        const me = this.auth.user()?.id;
        const opponentId = this.game()?.playerX.id === me ? this.game()?.playerO?.id : this.game()?.playerX.id;
        if (playerId === opponentId) {
          this.opponentConnected.set(connected);
        }
      },
    });
    await this.hub.joinGameRoom(gameId);
    this.clock = setInterval(() => this.now.set(Date.now()), 1000);
  }

  ngOnDestroy(): void {
    if (this.clock) {
      clearInterval(this.clock);
    }
    void this.hub.disconnect();
  }

  readonly secondsLeft = computed(() => {
    const deadline = this.game()?.moveDeadlineUtc;
    if (!deadline || this.game()?.status !== 'InProgress') {
      return null;
    }
    return Math.max(0, Math.ceil((new Date(deadline).getTime() - this.now()) / 1000));
  });

  async place(position: number): Promise<void> {
    const game = this.game();
    if (!game || game.status !== 'InProgress' || game.currentTurn !== this.myMark()) {
      this.pulseShake();
      return;
    }

    try {
      this.apply(await this.api.move(game.id, position));
    } catch (error) {
      // Invalid move (not your turn, occupied): subtle shake, board unchanged (US-08).
      this.pulseShake();
      this.error.set(problemMessage(error instanceof HttpErrorResponse ? error : null));
    }
  }

  async leave(): Promise<void> {
    const game = this.game();
    if (!game) {
      return;
    }
    try {
      await this.api.leave(game.id);
      await this.router.navigateByUrl('/lobby');
    } catch {
      await this.router.navigateByUrl('/lobby');
    }
  }

  async cancel(): Promise<void> {
    const game = this.game();
    if (!game) {
      return;
    }
    try {
      await this.api.cancel(game.id);
    } finally {
      await this.router.navigateByUrl('/lobby');
    }
  }

  backToLobby(): void {
    void this.router.navigateByUrl('/lobby');
  }

  clockLabel(): string {
    const s = this.secondsLeft();
    if (s === null) {
      return '';
    }
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  }

  isMe(playerId: string): boolean {
    return this.auth.user()?.id === playerId;
  }

  shortId(id: string): string {
    return id.length > 8 ? id.slice(0, 8) : id;
  }

  headline(game: GameDto): string {
    const outcome = game.result?.outcome;
    if (outcome === 'Draw') {
      return 'Draw';
    }
    const iWon = (outcome === 'XWins' && this.myMark() === 'X') || (outcome === 'OWins' && this.myMark() === 'O');
    return iWon ? 'Victory!' : 'Defeat';
  }

  subline(game: GameDto): string {
    if (game.result?.reason === 'Timeout') {
      const iWon = this.headline(game) === 'Victory!';
      return iWon ? 'Your opponent ran out of time.' : 'You ran out of time.';
    }
    return game.result?.reason === 'Forfeit' ? 'Your opponent left the game.' : 'Back to the lobby for a rematch.';
  }

  outcomeClass(outcome: string | undefined): string {
    if (outcome === 'Draw') {
      return 'text-slate-300';
    }
    const iWon = (outcome === 'XWins' && this.myMark() === 'X') || (outcome === 'OWins' && this.myMark() === 'O');
    return iWon ? 'text-success drop-shadow-[0_0_14px_var(--color-success)]' : 'text-danger drop-shadow-[0_0_14px_var(--color-danger)]';
  }

  private apply(game: unknown): void {
    if (game) {
      this.game.set(game as GameDto);
    }
  }

  private pulseShake(): void {
    this.shake.set(false);
    setTimeout(() => this.shake.set(true), 0);
    setTimeout(() => this.shake.set(false), 350);
  }
}
