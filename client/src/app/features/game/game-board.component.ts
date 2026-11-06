import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { GameDto } from '../../core/models';

/**
 * The 3×3 board: SVG stroke-draw marks (300 ms), winning-line overlay (500 ms), hover preview,
 * ARIA cell labels ("Row 1, Column 2, empty"), keyboard-operable cells (animations.md, pages-and-flows.md).
 */
@Component({
  selector: 'ttt-game-board',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="grid" aria-label="Tic-tac-toe board" (keydown)="onKeydown($event, focusedCell())"
         class="grid grid-cols-3 gap-2.5 rounded-2xl border border-white/5 bg-white/[0.02] p-2.5 shadow-[0_0_60px_-18px_var(--color-primary)]"
         [class.animate-shake]="shake()">
      @for (cell of cells(); track $index) {
        <button role="gridcell" [id]="'cell-' + $index"
                [attr.tabindex]="$index === focusedCell() ? 0 : -1"
                [attr.aria-label]="ariaLabel($index)"
                [disabled]="!cell.playable"
                (click)="cell.playable && placed.emit($index)"
                class="game-cell group relative flex aspect-square w-20 items-center justify-center sm:w-24 {{ cell.playable ? 'playable' : '' }} {{ cell.inWinningLine ? 'winning' : '' }}">
          @if (cell.mark === 'X') {
            <svg viewBox="0 0 100 100" class="glow-x h-12 w-12" aria-hidden="true">
              <line x1="22" y1="22" x2="78" y2="78" stroke="var(--color-mark-x)" stroke-width="10"
                    stroke-linecap="round" pathLength="100" class="animate-draw" />
              <line x1="78" y1="22" x2="22" y2="78" stroke="var(--color-mark-x)" stroke-width="10"
                    stroke-linecap="round" pathLength="100" class="animate-draw" />
            </svg>
          } @else if (cell.mark === 'O') {
            <svg viewBox="0 0 100 100" class="glow-o h-12 w-12" aria-hidden="true">
              <circle cx="50" cy="50" r="30" fill="none" stroke="var(--color-mark-o)" stroke-width="10"
                      pathLength="100" class="animate-draw" />
            </svg>
          } @else if (cell.preview) {
            <span class="font-mono text-3xl font-black opacity-0 transition-opacity duration-150 group-hover:opacity-25"
                  [class.text-mark-x]="cell.preview === 'X'" [class.text-mark-o]="cell.preview === 'O'">
              {{ cell.preview }}
            </span>
          }
        </button>
      }
    </div>

    @if (winningLine(); as line) {
      <svg viewBox="0 0 300 300" class="pointer-events-none absolute inset-0 h-full w-full p-2" aria-hidden="true">
        <line [attr.x1]="center(line[0]).x" [attr.y1]="center(line[0]).y"
              [attr.x2]="center(line[2]).x" [attr.y2]="center(line[2]).y"
              stroke="var(--color-primary)" stroke-width="8" stroke-linecap="round"
              pathLength="100" class="animate-draw" style="--duration-draw: 500ms; filter: drop-shadow(0 0 10px var(--color-primary))" />
      </svg>
    }
  `,
  styles: `
    :host {
      position: relative;
      display: block;
      width: fit-content;
    }
  `,
})
export class GameBoardComponent {
  private static readonly RowOf = (index: number) => Math.floor(index / 3);
  private static readonly ColumnOf = (index: number) => index % 3;

  readonly game = input.required<GameDto>();
  readonly myMark = input<'X' | 'O' | null>(null);
  readonly placed = output<number>();
  readonly shake = input(false);

  /** Roving tabindex: exactly one stop inside the grid, moved with arrow keys (pages-and-flows §10.3). */
  readonly focusedCell = signal(0);

  moveFocus(delta: number): void {
    const next = Math.min(8, Math.max(0, this.focusedCell() + delta));
    this.focusedCell.set(next);
    document.getElementById(`cell-${next}`)?.focus();
  }

  onKeydown(event: KeyboardEvent, index: number): void {
    this.focusedCell.set(index);
    switch (event.key) {
      case 'ArrowRight': this.moveFocus(1); break;
      case 'ArrowLeft': this.moveFocus(-1); break;
      case 'ArrowDown': this.moveFocus(3); break;
      case 'ArrowUp': this.moveFocus(-3); break;
      default: return;
    }
    event.preventDefault();
  }

  readonly winningLine = computed(() => this.game().result?.winningLine ?? null);

  readonly cells = computed(() => {
    const game = this.game();
    const active = game.status === 'InProgress';
    const myTurn = active && this.myMark() !== null && game.currentTurn === this.myMark();
    const line = game.result?.winningLine ?? [];
    return game.board.map((mark, index) => ({
      mark,
      preview: myTurn && !mark ? this.myMark() : null,
      playable: myTurn && !mark,
      inWinningLine: line.includes(index),
    }));
  });

  ariaLabel(index: number): string {
    const mark = this.game().board[index];
    const row = Math.floor(index / 3) + 1;
    const column = (index % 3) + 1;
    return mark ? `Row ${row}, column ${column}, ${mark}` : `Row ${row}, column ${column}, empty`;
  }

  private center(cell: number): { x: number; y: number } {
    return { x: (cell % 3) * 100 + 50, y: Math.floor(cell / 3) * 100 + 50 };
  }
}
