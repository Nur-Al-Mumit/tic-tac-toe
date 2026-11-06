using TicTacToe.Application.Contracts.Services;

namespace TicTacToe.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
