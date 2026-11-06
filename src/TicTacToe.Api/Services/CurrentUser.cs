using System.Security.Claims;
using TicTacToe.Application.Contracts.Services;

namespace TicTacToe.Api.Services;

/// <summary>Reads the authenticated caller from the JWT (sub + name claims); never trusts payloads.</summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
        => Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Username => Principal?.FindFirst("name")?.Value;
}
