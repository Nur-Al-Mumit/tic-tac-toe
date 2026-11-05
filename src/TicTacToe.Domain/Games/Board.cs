namespace TicTacToe.Domain.Games;

/// <summary>Pure rule evaluator over the move list — the eight winning lines and the draw bound.</summary>
public static class Board
{
    public static readonly int[][] Lines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8], // rows
        [0, 3, 6], [1, 4, 7], [2, 5, 8], // columns
        [0, 4, 8], [2, 4, 6],            // diagonals
    ];

    public static int[]? WinningLine(IReadOnlyList<Move> moves)
    {
        var cells = new Mark?[9];
        foreach (var move in moves)
        {
            cells[move.Position] = move.Mark;
        }

        foreach (var line in Lines)
        {
            var a = cells[line[0]];
            var b = cells[line[1]];
            var c = cells[line[2]];
            if (a is not null && a == b && b == c)
            {
                return line;
            }
        }

        return null;
    }
}
