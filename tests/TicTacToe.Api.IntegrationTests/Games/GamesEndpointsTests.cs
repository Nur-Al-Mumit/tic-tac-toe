using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;
using Shouldly;
using TicTacToe.Api.IntegrationTests.Fixtures;

namespace TicTacToe.Api.IntegrationTests.Games;

[TestFixture]
public class GamesEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record AuthResponse(string AccessToken, string RefreshToken);

    private record PlayerRef(Guid Id, string Username);

    private record GameDto(
        Guid Id,
        string Status,
        PlayerRef PlayerX,
        PlayerRef? PlayerO,
        string?[] Board,
        string? CurrentTurn,
        ResultDto? Result,
        DateTime CreatedAtUtc);

    private record ResultDto(string Outcome, string Reason, int[]? WinningLine);

    private record Problem(int? Status, string? Detail, string? ErrorCode);

    private static async Task<(HttpClient Client, string Username)> NewUserAsync()
    {
        var username = $"u_{Guid.NewGuid():N}"[..20];
        var email = $"{Guid.NewGuid():N}@test.local";

        var response = await ApiFixture.Factory.CreateClient()
            .PostAsync("/api/v1/auth/register", Body(new { username, email, password = "Password1" }));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = ApiFixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, username);
    }

    private static StringContent Body(object payload)
        => new(JsonSerializer.Serialize(payload, Json), null, "application/json");

    [Test]
    public async Task FullPlaythrough_CreateJoinPlayToWin()
    {
        var (host, hostName) = await NewUserAsync();
        var (opponent, _) = await NewUserAsync();

        var created = await host.PostAsync("/api/v1/games", content: null);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var game = (await created.Content.ReadFromJsonAsync<GameDto>())!;
        game.Status.ShouldBe("Waiting");
        game.PlayerX.Username.ShouldBe(hostName);
        game.Board.ShouldAllBe(b => b == null);
        game.CurrentTurn.ShouldBe("X");

        var joined = await opponent.PostAsync($"/api/v1/games/{game.Id}/join", content: null);
        joined.StatusCode.ShouldBe(HttpStatusCode.OK);
        var started = (await joined.Content.ReadFromJsonAsync<GameDto>())!;
        started.Status.ShouldBe("InProgress");
        started.PlayerO.ShouldNotBeNull();

        // X wins the top row while O answers below.
        int[] xMoves = [0, 1, 2];
        int[] oMoves = [3, 4];
        foreach (var (x, o) in xMoves.Zip(oMoves))
        {
            (await host.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = x }))).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await opponent.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = o }))).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var winning = await host.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = xMoves[2] }));
        winning.StatusCode.ShouldBe(HttpStatusCode.OK);
        var finished = (await winning.Content.ReadFromJsonAsync<GameDto>())!;
        finished.Status.ShouldBe("Finished");
        finished.Result!.Outcome.ShouldBe("XWins");
        finished.Result.WinningLine.ShouldBe([0, 1, 2]);
        finished.Board[0].ShouldBe("X");
        finished.Board[3].ShouldBe("O");

        // Finished games are immutable.
        var lateMove = await host.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = 8 }));
        lateMove.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await lateMove.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Games.NotInProgress");

        // No active game anymore for either player.
        var hostActive = await host.GetAsync("/api/v1/games/active");
        hostActive.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task JoinRace_TwoSimultaneousJoiners_ExactlyOneWins()
    {
        var (host, _) = await NewUserAsync();
        var (a, _) = await NewUserAsync();
        var (b, _) = await NewUserAsync();

        var game = (await (await host.PostAsync("/api/v1/games", content: null)).Content.ReadFromJsonAsync<GameDto>())!;

        var first = a.PostAsync($"/api/v1/games/{game.Id}/join", content: null);
        var second = b.PostAsync($"/api/v1/games/{game.Id}/join", content: null);
        var responses = await Task.WhenAll(first, second);

        var ordered = responses.OrderBy(r => (int)r.StatusCode).ToList();
        ordered[0].StatusCode.ShouldBe(HttpStatusCode.OK);
        ordered[1].StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ordered[1].Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Games.Full");

        var final = (await (await host.GetAsync($"/api/v1/games/{game.Id}")).Content.ReadFromJsonAsync<GameDto>())!;
        final.Status.ShouldBe("InProgress");
    }

    [Test]
    public async Task MoveRejections_NotYourTurn_Occupied_OutOfRange()
    {
        var (host, _) = await NewUserAsync();
        var (opponent, _) = await NewUserAsync();
        var game = (await (await host.PostAsync("/api/v1/games", content: null)).Content.ReadFromJsonAsync<GameDto>())!;
        await opponent.PostAsync($"/api/v1/games/{game.Id}/join", content: null);

        // O moves first — rejected.
        var notYourTurn = await opponent.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = 0 }));
        notYourTurn.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await notYourTurn.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Games.NotYourTurn");

        // X plays 0; O repeats 0 — occupied.
        await host.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = 0 }));
        var occupied = await opponent.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = 0 }));
        occupied.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await occupied.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Games.CellOccupied");

        // Out of range is a request-shape failure (400 Validation.Failure, api-spec §validation).
        var outOfRange = await host.PostAsync($"/api/v1/games/{game.Id}/moves", Body(new { position = 9 }));
        outOfRange.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await outOfRange.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Validation.Failure");
    }

    [Test]
    public async Task CreateWhileActive_Returns409Busy()
    {
        var (host, _) = await NewUserAsync();
        await host.PostAsync("/api/v1/games", content: null);

        var second = await host.PostAsync("/api/v1/games", content: null);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Games.Busy");
    }

    [Test]
    public async Task Cancel_WaitingGame_ByHostOnly()
    {
        var (host, _) = await NewUserAsync();
        var (other, _) = await NewUserAsync();
        var game = (await (await host.PostAsync("/api/v1/games", content: null)).Content.ReadFromJsonAsync<GameDto>())!;

        var notHost = await other.PostAsync($"/api/v1/games/{game.Id}/cancel", content: null);
        notHost.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await notHost.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Games.NotHost");

        var cancelled = await host.PostAsync($"/api/v1/games/{game.Id}/cancel", content: null);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = (await (await host.GetAsync($"/api/v1/games/{game.Id}")).Content.ReadFromJsonAsync<GameDto>())!;
        after.Status.ShouldBe("Cancelled");
    }

    [Test]
    public async Task Leave_Forfeits_ThenBothPlayersAreFree()
    {
        var (host, _) = await NewUserAsync();
        var (opponent, _) = await NewUserAsync();
        var game = (await (await host.PostAsync("/api/v1/games", content: null)).Content.ReadFromJsonAsync<GameDto>())!;
        await opponent.PostAsync($"/api/v1/games/{game.Id}/join", content: null);

        var leave = await host.PostAsync($"/api/v1/games/{game.Id}/leave", content: null);
        leave.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = (await (await opponent.GetAsync($"/api/v1/games/{game.Id}")).Content.ReadFromJsonAsync<GameDto>())!;
        after.Status.ShouldBe("Finished");
        after.Result!.Outcome.ShouldBe("OWins");
        after.Result.Reason.ShouldBe("Forfeit");

        // Both players can start new games.
        (await host.PostAsync("/api/v1/games", content: null)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task LobbyList_ShowsWaitingGames_AndJoinOwnGameIsRejected()
    {
        var (host, hostName) = await NewUserAsync();
        var game = (await (await host.PostAsync("/api/v1/games", content: null)).Content.ReadFromJsonAsync<GameDto>())!;

        var list = await host.GetAsync("/api/v1/games?status=waiting");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await list.Content.ReadAsStringAsync();
        body.ShouldContain(game.Id.ToString());
        body.ShouldContain(hostName);

        var own = await host.PostAsync($"/api/v1/games/{game.Id}/join", content: null);
        own.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await own.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Games.CannotJoinOwnGame");
    }

    [Test]
    public async Task AllEndpoints_RequireAuthentication()
    {
        var anonymous = ApiFixture.Factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/games")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/v1/games", content: null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"/api/v1/games/{Guid.NewGuid()}/join", content: null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
