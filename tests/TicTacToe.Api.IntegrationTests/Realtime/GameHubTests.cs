using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NUnit.Framework;
using Shouldly;
using TicTacToe.Api.IntegrationTests.Fixtures;

namespace TicTacToe.Api.IntegrationTests.Realtime;

/// <summary>US-08 "instantly": two SignalR clients in the game-{id} group receive GameStarted/MoveMade/GameFinished.</summary>
[TestFixture]
public class GameHubTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record AuthResponse(string AccessToken, string RefreshToken);

    private static async Task<(HttpClient Client, string AccessToken)> NewUserAsync()
    {
        var username = $"u_{Guid.NewGuid():N}"[..20];
        var email = $"{Guid.NewGuid():N}@test.local";
        var response = await ApiFixture.Factory.CreateClient()
            .PostAsync("/api/v1/auth/register",
                new StringContent(JsonSerializer.Serialize(new { username, email, password = "Password1" }, Json), null, "application/json"));
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;

        var client = ApiFixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        return (client, tokens.AccessToken);
    }

    private static HubConnection NewHubConnection(string accessToken)
        => new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken)!;
                options.HttpMessageHandlerFactory = _ => ApiFixture.Factory.Server.CreateHandler();
            })
            .Build();

    [Test]
    public async Task MovesBroadcastToBothPlayers_Instantly()
    {
        var (host, hostToken) = await NewUserAsync();
        var (opponent, opponentToken) = await NewUserAsync();

        var game = (await (await host.PostAsync("/api/v1/games", content: null))
            .Content.ReadFromJsonAsync<JsonElement>())!;
        var gameId = game.GetProperty("id").GetString()!;

        await using var hostHub = NewHubConnection(hostToken);
        await using var opponentHub = NewHubConnection(opponentToken);

        var hostStarted = new TaskCompletionSource(JsonTaskOptions);
        var opponentStarted = new TaskCompletionSource(JsonTaskOptions);
        var hostMove = new TaskCompletionSource<JsonElement>(JsonTaskOptions);
        var opponentMove = new TaskCompletionSource<JsonElement>(JsonTaskOptions);
        var finished = new TaskCompletionSource<JsonElement>(JsonTaskOptions);

        hostHub.On<JsonElement>("GameStarted", _ => hostStarted.TrySetResult());
        opponentHub.On<JsonElement>("GameStarted", _ => opponentStarted.TrySetResult());
        hostHub.On<JsonElement>("MoveMade", g => hostMove.TrySetResult(g));
        opponentHub.On<JsonElement>("MoveMade", g => opponentMove.TrySetResult(g));
        opponentHub.On<JsonElement>("GameFinished", g => finished.TrySetResult(g));

        await hostHub.StartAsync();
        await opponentHub.StartAsync();
        await hostHub.InvokeAsync("JoinGameRoom", Guid.Parse(gameId));

        // Joining starts the game; only the opponent is a participant and may join the room.
        var joined = await opponent.PostAsync($"/api/v1/games/{gameId}/join", content: null);
        joined.StatusCode.ShouldBe(HttpStatusCode.OK);
        await opponentHub.InvokeAsync("JoinGameRoom", Guid.Parse(gameId));

        await Task.WhenAll(hostStarted.Task, opponentStarted.Task);

        // Opponent is O; host (X) moves first — both clients receive MoveMade with the board.
        var move = await host.PostAsync($"/api/v1/games/{gameId}/moves",
            new StringContent("""{"position":4}""", null, "application/json"));
        move.StatusCode.ShouldBe(HttpStatusCode.OK);

        var hostReceived = await hostMove.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var opponentReceived = await opponentMove.Task.WaitAsync(TimeSpan.FromSeconds(10));
        hostReceived.GetProperty("board")[4].GetString().ShouldBe("X");
        opponentReceived.GetProperty("board")[4].GetString().ShouldBe("X");

        // Play to an X win: 0,1,2 (O: 3,4 — 4 already X? use 3,5).
        foreach (var (x, o) in new[] { (0, 3), (1, 5) })
        {
            await host.PostAsync($"/api/v1/games/{gameId}/moves", new StringContent($"{{\"position\":{x}}}", null, "application/json"));
            await opponent.PostAsync($"/api/v1/games/{gameId}/moves", new StringContent($"{{\"position\":{o}}}", null, "application/json"));
        }

        await host.PostAsync($"/api/v1/games/{gameId}/moves", new StringContent("""{"position":2}""", null, "application/json"));

        var finishedGame = await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        finishedGame.GetProperty("status").GetString().ShouldBe("Finished");
        finishedGame.GetProperty("result").GetProperty("outcome").GetString().ShouldBe("XWins");
    }

    [Test]
    public async Task NonParticipant_CannotJoinGameRoom()
    {
        var (host, hostToken) = await NewUserAsync();
        var (stranger, strangerToken) = await NewUserAsync();

        var game = (await (await host.PostAsync("/api/v1/games", content: null))
            .Content.ReadFromJsonAsync<JsonElement>())!;
        var gameId = game.GetProperty("id").GetString()!;

        await using var hub = NewHubConnection(strangerToken);
        await hub.StartAsync();

        Should.ThrowAsync<Exception>(() => hub.InvokeAsync("JoinGameRoom", Guid.Parse(gameId)))
            .Wait(TimeSpan.FromSeconds(10));
    }

    private static readonly TaskCreationOptions JsonTaskOptions = TaskCreationOptions.RunContinuationsAsynchronously;
}
