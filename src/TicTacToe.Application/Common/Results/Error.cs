namespace TicTacToe.Application.Common.Results;

/// <summary>Stable, machine-readable failure. Codes live in docs/02-backend/api-spec.md §1.4 (canonical registry).</summary>
public sealed record Error(string Code, string Message)
{
    public static Error Validation(string message = "One or more validation errors occurred.") => new("Validation.Failure", message);

    public static Error Unauthorized(string message = "Authentication is required.") => new("Auth.Unauthorized", message);

    public static Error InvalidCredentials(string message = "Email/username or password is incorrect.") => new("Auth.InvalidCredentials", message);

    public static Error LockedOut(string message = "Account is locked. Try again later.") => new("Auth.LockedOut", message);

    public static Error InvalidRefreshToken(string message = "Refresh token is invalid or expired.") => new("Auth.InvalidRefreshToken", message);
}
