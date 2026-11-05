using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;
using Shouldly;
using TicTacToe.Api.IntegrationTests.Fixtures;

namespace TicTacToe.Api.IntegrationTests.Auth;

[TestFixture]
public class AuthEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient Client => ApiFixture.Factory.CreateClient();

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), null, "application/json");

    private static (string Username, string Email, string Password) NewUser()
        => ($"user_{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@test.local", "Password1");

    private record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

    private record Problem(int? Status, string? Detail, string? ErrorCode);

    [Test]
    public async Task Register_Returns201WithTokens_ThenLoginWorksByUsernameAndEmail()
    {
        var (username, email, password) = NewUser();

        var register = await Client.PostAsync("/api/v1/auth/register", Body(new { username, email, password }));
        register.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tokens = await register.Content.ReadFromJsonAsync<AuthResponse>();
        tokens!.AccessToken.ShouldNotBeNull();
        tokens.RefreshToken.ShouldNotBeNull();

        var byUsername = await Client.PostAsync("/api/v1/auth/login", Body(new { identifier = username, password }));
        byUsername.StatusCode.ShouldBe(HttpStatusCode.OK);

        var byEmail = await Client.PostAsync("/api/v1/auth/login", Body(new { identifier = email, password }));
        byEmail.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Register_DuplicateUsername_Returns409()
    {
        var (username, email, password) = NewUser();
        await Client.PostAsync("/api/v1/auth/register", Body(new { username, email, password }));

        var duplicate = await Client.PostAsync("/api/v1/auth/register",
            Body(new { username, email = $"{Guid.NewGuid():N}@test.local", password }));

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Auth.DuplicateUsername");
    }

    [Test]
    public async Task Register_WeakPassword_Returns400ValidationFailure()
    {
        var (username, email, _) = NewUser();

        var response = await Client.PostAsync("/api/v1/auth/register", Body(new { username, email, password = "weak" }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<Problem>();
        problem!.ErrorCode.ShouldBe("Validation.Failure");
    }

    [Test]
    public async Task Login_WrongPassword_Returns401_AndLocksOutAfterFiveFailures()
    {
        var (username, email, password) = NewUser();
        await Client.PostAsync("/api/v1/auth/register", Body(new { username, email, password }));

        var wrong = await Client.PostAsync("/api/v1/auth/login", Body(new { identifier = username, password = "WrongPass1" }));
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await wrong.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Auth.InvalidCredentials");

        HttpResponseMessage? locked = null;
        for (var i = 0; i < 5; i++)
        {
            locked = await Client.PostAsync("/api/v1/auth/login", Body(new { identifier = username, password = "WrongPass1" }));
        }

        locked!.StatusCode.ShouldBe((HttpStatusCode)423);
        (await locked.Content.ReadFromJsonAsync<Problem>())!.ErrorCode.ShouldBe("Auth.LockedOut");
    }

    [Test]
    public async Task Refresh_RotatesToken_OldTokenBecomesInvalid()
    {
        var (username, email, password) = NewUser();
        var register = await Client.PostAsync("/api/v1/auth/register", Body(new { username, email, password }));
        var tokens = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;

        var refresh = await Client.PostAsync("/api/v1/auth/refresh", Body(new { refreshToken = tokens.RefreshToken }));
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!;
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);

        var reuse = await Client.PostAsync("/api/v1/auth/refresh", Body(new { refreshToken = tokens.RefreshToken }));
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Reuse detection revoked the family: even the freshly rotated token is dead now.
        var familyKilled = await Client.PostAsync("/api/v1/auth/refresh", Body(new { refreshToken = rotated.RefreshToken }));
        familyKilled.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Logout_RevokesRefreshToken_WithBearer()
    {
        var (username, email, password) = NewUser();
        var register = await Client.PostAsync("/api/v1/auth/register", Body(new { username, email, password }));
        var tokens = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;

        using var authorized = ApiFixture.Factory.CreateClient();
        authorized.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);

        var logout = await authorized.PostAsync("/api/v1/auth/logout", Body(new { refreshToken = tokens.RefreshToken }));
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refreshAfterLogout = await Client.PostAsync("/api/v1/auth/refresh", Body(new { refreshToken = tokens.RefreshToken }));
        refreshAfterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Me_RequiresBearer()
    {
        var anonymous = await Client.GetAsync("/api/v1/auth/me");
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var (username, email, password) = NewUser();
        var register = await Client.PostAsync("/api/v1/auth/register", Body(new { username, email, password }));
        var tokens = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;

        using var authorized = ApiFixture.Factory.CreateClient();
        authorized.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);

        var me = await authorized.GetAsync("/api/v1/auth/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await me.Content.ReadAsStringAsync();
        body.ShouldContain(username);
    }
}
