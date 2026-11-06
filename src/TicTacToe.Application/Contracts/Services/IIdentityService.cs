using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.Contracts.Services;

public interface IIdentityService
{
    /// <summary>Creates the Identity user and the PlayerProfile in one transaction.</summary>
    Task<Result<UserDto>> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken);

    /// <summary>Validates credentials by email-or-username; enforces lockout (5 attempts / 5 min).</summary>
    Task<Result<UserDto>> ValidateCredentialsAsync(string identifier, string password, CancellationToken cancellationToken);
}
