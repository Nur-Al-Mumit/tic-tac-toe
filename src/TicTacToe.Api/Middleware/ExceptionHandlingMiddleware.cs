using Cortex.Mediator.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace TicTacToe.Api.Middleware;

/// <summary>Validation failures → 400 ProblemDetails (code Validation.Failure); anything else → logged 500.</summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors?
                .GroupBy(p => p.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => string.Join(" ", g.SelectMany(p => p.Value)));
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request",
                "One or more validation errors occurred.", "Validation.Failure", errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred.", "Internal.Error", null);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail, string errorCode, Dictionary<string, string>? errors)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["errorCode"] = errorCode;
        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        await context.Response.WriteAsJsonAsync(problem);
    }
}
