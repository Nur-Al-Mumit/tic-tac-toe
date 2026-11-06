import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, signal } from '@angular/core';

type DemoMark = 'X' | 'O';

/**
 * Decorative board that plays a random legal tic-tac-toe match on loop:
 * marks pop in one by one, the winning line lights up, then a fresh random
 * match is generated. Purely presentational (aria-hidden).
 */
@Component({
  selector: 'ttt-demo-board',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `
    <div class="grid w-fit grid-cols-3 gap-1.5">
      @for (cell of board(); track $index; let i = $index) {
        <span [class]="cellClasses(i)">
          @if (cell; as mark) {
            <span class="animate-scale-in font-mono font-black"
                  [class.text-mark-x]="mark === 'X'" [class.text-mark-o]="mark === 'O'"
                  [class.glow-x]="lastPlacedIndex() === i || (isWinCell(i) && mark === 'X')"
                  [class.glow-o]="lastPlacedIndex() === i || (isWinCell(i) && mark === 'O')">{{ mark }}</span>
          }
        </span>
      }
    </div>
  `,
})
export class DemoBoardComponent {
  private static readonly WIN_LINES = [
    [0, 1, 2], [3, 4, 5], [6, 7, 8],
    [0, 3, 6], [1, 4, 7], [2, 5, 8],
    [0, 4, 8], [2, 4, 6],
  ];
  private static readonly TICK_MS = 450;
  private static readonly HOLD_TICKS = 5;

  /** Cell size of the board squares. */
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  /** Marks currently visible on the demo board. */
  readonly board = signal<Array<DemoMark | null>>(Array(9).fill(null));
  /** Board index of the most recently placed mark (drives its glow). */
  readonly lastPlacedIndex = signal(-1);
  /** Highlighted winning line once the match is fully played. */
  readonly winLine = signal<number[]>([]);

  private moves: Array<{ index: number; mark: DemoMark }> = [];
  private winningLine: number[] | null = null;
  private shown = 0;
  private hold = 0;

  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.generateMatch();
    const timer = setInterval(() => this.tick(), DemoBoardComponent.TICK_MS);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  isWinCell(index: number): boolean {
    return this.winLine().includes(index);
  }

  cellClasses(index: number): string {
    const sizes = { sm: 'h-7 w-7 text-sm', md: 'h-9 w-9 text-base', lg: 'h-11 w-11 text-lg' } as const;
    const win = this.isWinCell(index) ? ' border-primary/70 bg-primary/10' : ' border-white/10';
    return `flex items-center justify-center rounded-lg border bg-bg/60 transition-colors duration-300${win} ${sizes[this.size()]}`;
  }

  /** Advances the demo by one step: reveal a move, hold the result, then play a fresh random match. */
  private tick(): void {
    if (this.hold > 0) {
      if (--this.hold === 0) {
        this.generateMatch();
      }
      return;
    }

    if (this.shown < this.moves.length) {
      const move = this.moves[this.shown++];
      const board = [...this.board()];
      board[move.index] = move.mark;
      this.board.set(board);
      this.lastPlacedIndex.set(move.index);
      if (this.shown === this.moves.length) {
        this.winLine.set(this.winningLine ?? []);
        this.hold = DemoBoardComponent.HOLD_TICKS;
      }
    }
  }

  /** Generates a random legal match: alternating turns on random empty cells, stopping at a win or draw. */
  private generateMatch(): void {
    const board: Array<DemoMark | null> = Array(9).fill(null);
    const moves: Array<{ index: number; mark: DemoMark }> = [];
    this.winningLine = null;

    for (let turn = 0; turn < 9; turn++) {
      const mark: DemoMark = turn % 2 === 0 ? 'X' : 'O';
      const empty = board.flatMap((cell, index) => (cell ? [] : [index]));
      if (empty.length === 0) {
        break;
      }
      const index = empty[Math.floor(Math.random() * empty.length)];
      board[index] = mark;
      moves.push({ index, mark });

      const line = DemoBoardComponent.WIN_LINES.find(([a, b, c]) =>
        board[a] === mark && board[b] === mark && board[c] === mark);
      if (line) {
        this.winningLine = line;
        break;
      }
    }

    this.moves = moves;
    this.shown = 0;
    this.hold = 0;
    this.board.set(Array(9).fill(null));
    this.lastPlacedIndex.set(-1);
    this.winLine.set([]);
  }
}
