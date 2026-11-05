using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;
using TicTacToe.Domain.Players;
using TicTacToe.Infrastructure.Persistence;

namespace TicTacToe.Infrastructure.Identity;

public class IdentityService(
    UserManager<ApplicationUser> userManager,
    IPlayerProfileRepository profileRepository,
    IDateTimeProvider dateTimeProvider,
    AppDbContext context) : IIdentityService
{
    public async Task<Result<UserDto>> RegisterAsync(string username, string email, string password, CancellationToken cancellationToken)
    {
        var normalizedUsername = username.ToUpperInvariant();
        var normalizedEmail = userManager.NormalizeEmail(email);

        if (await userManager.Users.AnyAsync(u => u.NormalizedUserName == normalizedUsername, cancellationToken))
        {
            return Result.Failure<UserDto>(new Error("Auth.DuplicateUsername", $"Username '{username}' is already taken."));
        }

        if (await userManager.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return Result.Failure<UserDto>(new Error("Auth.DuplicateEmail", $"Email '{email}' is already registered."));
        }

        var user = new ApplicationUser { UserName = username, Email = email };

        // User + PlayerProfile commit atomically: UserManager participates in the same EF transaction.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var identityResult = await userManager.CreateAsync(user, password);
        if (!identityResult.Succeeded)
        {
            return Result.Failure<UserDto>(new Error("Auth.RegistrationFailed", "Account could not be created with the provided values."));
        }

        await profileRepository.AddAsync(new PlayerProfile(user.Id, username, dateTimeProvider.UtcNow), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new UserDto(user.Id, user.UserName!, user.Email!);
    }

    public async Task<Result<UserDto>> ValidateCredentialsAsync(string identifier, string password, CancellationToken cancellationToken)
    {
        var user = identifier.Contains('@')
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);

        if (user is null)
        {
            // Same error as a wrong password: no account enumeration.
            return Result.Failure<UserDto>(Error.InvalidCredentials());
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<UserDto>(Error.LockedOut());
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return Result.Failure<UserDto>(Error.InvalidCredentials());
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return new UserDto(user.Id, user.UserName!, user.Email!);
    }
}
