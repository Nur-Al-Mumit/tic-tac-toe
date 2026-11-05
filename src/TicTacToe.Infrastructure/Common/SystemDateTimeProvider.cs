using TicTacToe.Application.Common.Interfaces;

namespace TicTacToe.Infrastructure.Common;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
