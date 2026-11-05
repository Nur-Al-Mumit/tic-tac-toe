import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NavbarComponent } from '../../shared/navbar.component';
import { AuthService } from '../../core/auth.service';
import { GamesApi } from '../../core/games.api';
import { SignalRService } from '../../core/signalr.service';
import { problemMessage } from '../../core/problem';
import { GameDto } from '../../core/models';
import { GameBoardComponent } from './game-board.component';

/** V1 game page: live board via SignalR, explicit Leave forfeit, reload = full resync. */
@Component({
  selector: 'ttt-game-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NavbarComponent, GameBoardComponent],
  template: `
    <ttt-navbar />
    <main class="mx-auto flex max-w-lg flex-col items-center px-4 py-8">
      @if (error(); as message) {
        <p role="alert" class="mb-4 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-red-400">
          {{ message }}
        </p>
      }

      @if (game(); as game) {
        <!-- player badges + turn indicator -->
        <div class="mb-5 flex w-full items-center justify-between rounded-card border border-surface-border bg-surface px-4 py-3">
          <div class="flex items-center gap-2"
               [class.animate-pulse-ring]="game.status === 'InProgress' && game.currentTurn === 'X'">
            <span class="font-mono text-xl text-mark-x">X</span>
            <div>
              <p class="text-sm font-semibold">{{ game.playerX.username }}{{ isMe(game.playerX.id) ? ' (you)' : '' }}</p>
              <p class="text-xs text-slate-500">{{ game.status === 'Waiting' ? 'waiting for opponent' : 'Player X' }}</p>
            </div>
          </div>
          <span class="font-mono text-xs uppercase tracking-wider"
                [class.text-success]="connection() === 'connected'"
                [class.text-amber-400]="connection() === 'reconnecting'"
                [class.text-slate-500]="connection() === 'disconnected'">
            {{ connection() === 'connected' ? '● connected' : connection() === 'reconnecting' ? '● reconnecting…' : '● offline' }}
          </span>
          <div class="flex items-center gap-2"
               [class.animate-pulse-ring]="game.status === 'InProgress' && game.currentTurn === 'O'">
            <div class="text-right">
              <p class="text-sm font-semibold">
                {{ game.playerO ? game.playerO.username + (isMe(game.playerO.id) ? ' (you)' : '') : '—' }}
              </p>
              <p class="text-xs text-slate-500">Player O</p>
            </div>
            <span class="font-mono text-xl text-mark-o">O</span>
          </div>
        </div>

        <!-- waiting state -->
        @if (game.status === 'Waiting') {
          <div class="flex flex-col items-center gap-4 py-10">
            <p class="text-slate-300">Waiting for an opponent<span class="animate-pulse">…</span></p>
            <p class="text-xs text-slate-500">Share the lobby — anyone can join your open game.</p>
            <button (click)="cancel()"
                    class="rounded-lg border border-surface-border px-4 py-2 text-sm text-slate-300 transition-colors hover:border-danger hover:text-red-400">
              Cancel game
            </button>
          </div>
        } @else {
          <ttt-game-board [game]="game" [myMark]="myMark()" [shake]="shake()" (placed)="place($event)" />

          @if (game.status === 'InProgress') {
            <div class="mt-6 flex w-full items-center justify-between">
              <p class="text-sm text-slate-300" aria-live="polite">
                @if (game.currentTurn === myMark()) {
                  Your turn — you are <span class="font-mono">{{ myMark() }}</span>
                } @else {
                  Opponent's turn
                }
              </p>
              <button (click)="leave()"
                      class="rounded-lg border border-surface-border px-3 py-2 text-sm text-slate-300 transition-colors hover:border-danger hover:text-red-400">
                Leave (forfeit)
              </button>
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
export class GamePage implements OnInit {
  readonly id = input.required<string>();

  private readonly api = inject(GamesApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly hub = inject(SignalRService);

  readonly game = signal<GameDto | null>(null);
  readonly error = signal<string | null>(null);
  readonly shake = signal(false);
  readonly connection = this.hub.state;

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
    });
    await this.hub.joinGameRoom(gameId);
  }

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

  isMe(playerId: string): boolean {
    return this.auth.user()?.id === playerId;
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
    return game.result?.reason === 'Forfeit' ? 'Your opponent left the game.' : 'Back to the lobby for a rematch.';
  }

  outcomeClass(outcome: string | undefined): string {
    if (outcome === 'Draw') {
      return 'text-slate-300';
    }
    const iWon = (outcome === 'XWins' && this.myMark() === 'X') || (outcome === 'OWins' && this.myMark() === 'O');
    return iWon ? 'text-success' : 'text-danger';
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
