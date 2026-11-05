using Microsoft.AspNetCore.Mvc;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Api.Common;

/// <summary>Single Result → HTTP translation (error codes per api-spec §1.4, the canonical registry).</summary>
public static class HttpResults
{
    private static readonly Dictionary<string, int> StatusByCode = new()
    {
        ["Validation.Failure"] = StatusCodes.Status400BadRequest,
        ["Auth.Unauthorized"] = StatusCodes.Status401Unauthorized,
        ["Auth.InvalidCredentials"] = StatusCodes.Status401Unauthorized,
        ["Auth.InvalidRefreshToken"] = StatusCodes.Status401Unauthorized,
        ["Auth.LockedOut"] = StatusCodes.Status423Locked,
        ["Auth.DuplicateUsername"] = StatusCodes.Status409Conflict,
        ["Auth.DuplicateEmail"] = StatusCodes.Status409Conflict,
        ["Auth.RegistrationFailed"] = StatusCodes.Status400BadRequest,
        ["Games.NotFound"] = StatusCodes.Status404NotFound,
        ["Games.Full"] = StatusCodes.Status409Conflict,
        ["Games.Busy"] = StatusCodes.Status409Conflict,
        ["Games.NotYourTurn"] = StatusCodes.Status409Conflict,
        ["Games.CellOccupied"] = StatusCodes.Status422UnprocessableEntity,
        ["Games.InvalidPosition"] = StatusCodes.Status422UnprocessableEntity,
        ["Games.NotInProgress"] = StatusCodes.Status409Conflict,
        ["Games.CannotJoinOwnGame"] = StatusCodes.Status409Conflict,
        ["Games.Forbidden"] = StatusCodes.Status403Forbidden,
        ["Games.NotHost"] = StatusCodes.Status403Forbidden,
        ["Games.ConcurrencyConflict"] = StatusCodes.Status409Conflict,
    };

    public static IActionResult FromResult(this ControllerBase controller, Result result)
        => result.IsSuccess
            ? controller.NoContent()
            : controller.Problem(result.Error!);

    public static IActionResult FromResult<T>(this ControllerBase controller, Result<T> result, int successStatus = StatusCodes.Status200OK)
        => result.IsSuccess
            ? Success(controller, result.Value, successStatus)
            : controller.Problem(result.Error!);

    public static IActionResult FromResult<T>(this ControllerBase controller, Result<T?> result, int successStatus, int nullStatus)
        => result.IsSuccess
            ? result.Value is null ? new StatusCodeResult(nullStatus) : Success(controller, result.Value, successStatus)
            : controller.Problem(result.Error!);

    private static IActionResult Success<T>(ControllerBase controller, T value, int status)
        => status switch
        {
            StatusCodes.Status201Created => controller.Created(string.Empty, value),
            _ => controller.StatusCode(status, value),
        };

    private static IActionResult Problem(this ControllerBase controller, Error error)
    {
        var status = StatusByCode.TryGetValue(error.Code, out var mapped)
            ? mapped
            : StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrase(status),
            Detail = error.Message,
            Extensions = { ["errorCode"] = error.Code },
        };

        return controller.StatusCode(status, problem);
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        423 => "Locked",
        _ => "Internal Server Error",
    };
}
