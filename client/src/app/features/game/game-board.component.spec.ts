import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GameBoardComponent } from './game-board.component';
import { GameDto } from '../../core/models';

function game(partial: Partial<GameDto>): GameDto {
  return {
    id: 'g1',
    status: 'InProgress',
    playerX: { id: 'px', username: 'host_01' },
    playerO: { id: 'po', username: 'opp_02' },
    board: Array(9).fill(null),
    currentTurn: 'X',
    result: null,
    createdAtUtc: new Date().toISOString(),
    ...partial,
  } as GameDto;
}

describe('GameBoardComponent', () => {
  let fixture: ComponentFixture<GameBoardComponent>;

  beforeEach(() => {
    fixture = TestBed.createComponent(GameBoardComponent);
  });

  it('marks empty cells playable only on my turn, with hover preview of my mark', () => {
    fixture.componentRef.setInput('game', game({}));
    fixture.componentRef.setInput('myMark', 'X');

    const cells = fixture.componentInstance.cells();
    expect(cells.every((c) => c.playable && c.preview === 'X')).toBe(true);
  });

  it('blocks all cells when it is not my turn', () => {
    fixture.componentRef.setInput('game', game({ currentTurn: 'O' }));
    fixture.componentRef.setInput('myMark', 'X');

    expect(fixture.componentInstance.cells().every((c) => !c.playable)).toBe(true);
  });

  it('blocks cells when the game is finished', () => {
    fixture.componentRef.setInput(
      'game',
      game({ status: 'Finished', result: { outcome: 'XWins', reason: 'Normal', winningLine: [0, 1, 2] } }),
    );
    fixture.componentRef.setInput('myMark', 'X');

    const cells = fixture.componentInstance.cells();
    expect(cells.every((c) => !c.playable)).toBe(true);
    expect(fixture.componentInstance.winningLine()).toEqual([0, 1, 2]);
  });

  it('describes cells for screen readers', () => {
    fixture.componentRef.setInput('game', game({ board: ['X', null, null, null, 'O', null, null, null, null] }));
    fixture.componentRef.setInput('myMark', 'O');

    expect(fixture.componentInstance.ariaLabel(0)).toBe('Row 1, column 1, X');
    expect(fixture.componentInstance.ariaLabel(4)).toBe('Row 2, column 2, O');
    expect(fixture.componentInstance.ariaLabel(8)).toBe('Row 3, column 3, empty');
  });
});
