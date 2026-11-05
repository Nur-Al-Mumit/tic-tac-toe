using NUnit.Framework;
using NSubstitute;
using Shouldly;
using TicTacToe.Application.Auth.Commands;
using TicTacToe.Application.Auth.Dtos;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.UnitTests.Auth;

public class AuthCommandHandlersTests
{
    private static UserDto NewUser() => new(Guid.NewGuid(), "sara_01", "sara@example.com");

    private static AuthResponse TokensFor(UserDto user) => new("access", "refresh", DateTime.UtcNow.AddMinutes(15), user);

    [Test]
    public async Task Register_DuplicateUsername_ReturnsConflictFailure()
    {
        var identity = Substitute.For<IIdentityService>();
        var tokens = Substitute.For<ITokenService>();
        identity.RegisterAsync("sara_01", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserDto>(new Error("Auth.DuplicateUsername", "taken")));

        var result = await new RegisterUserCommandHandler(identity, tokens).Handle(
            new RegisterUserCommand("sara_01", "sara@example.com", "Password1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("Auth.DuplicateUsername");
        await tokens.DidNotReceive().IssueTokensAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Register_NewUser_IssuesTokens()
    {
        var user = NewUser();
        var identity = Substitute.For<IIdentityService>();
        var tokens = Substitute.For<ITokenService>();
        identity.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        tokens.IssueTokensAsync(user.Id, user.Username, user.Email, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(TokensFor(user));

        var result = await new RegisterUserCommandHandler(identity, tokens).Handle(
            new RegisterUserCommand(user.Username, user.Email, "Password1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access");
    }

    [Test]
    public async Task Login_InvalidCredentials_ReturnsUnauthorizedFailure()
    {
        var identity = Substitute.For<IIdentityService>();
        identity.ValidateCredentialsAsync("sara_01", "wrong", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserDto>(Error.InvalidCredentials()));

        var result = await new LoginCommandHandler(identity, Substitute.For<ITokenService>()).Handle(
            new LoginCommand("sara_01", "wrong"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("Auth.InvalidCredentials");
    }

    [Test]
    public async Task Login_ValidCredentials_IssuesTokens()
    {
        var user = NewUser();
        var identity = Substitute.For<IIdentityService>();
        var tokens = Substitute.For<ITokenService>();
        identity.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        tokens.IssueTokensAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(TokensFor(user));

        var result = await newLoginHandler(identity, tokens);

        result.Value.AccessToken.ShouldBe("access");

        static async Task<Result<AuthResponse>> newLoginHandler(IIdentityService i, ITokenService t)
            => await new LoginCommandHandler(i, t).Handle(new LoginCommand("sara_01", "Password1"), CancellationToken.None);
    }

    [Test]
    public async Task Refresh_PassesThroughTokenServiceResult()
    {
        var tokens = Substitute.For<ITokenService>();
        tokens.RefreshAsync("rt", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthResponse>(Error.InvalidRefreshToken()));

        var result = await new RefreshTokenCommandHandler(tokens).Handle(
            new RefreshTokenCommand("rt"), CancellationToken.None);

        result.Error!.Code.ShouldBe("Auth.InvalidRefreshToken");
    }

    [Test]
    public async Task Logout_RevokesOwnToken_AndSucceeds()
    {
        var user = NewUser();
        var tokens = Substitute.For<ITokenService>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(user.Id);

        var result = await new LogoutCommandHandler(tokens, currentUser).Handle(
            new LogoutCommand("rt"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await tokens.Received(1).RevokeAsync("rt", user.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Logout_WithoutAuthenticatedUser_DoesNotRevoke()
    {
        var tokens = Substitute.For<ITokenService>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns((Guid?)null);

        var result = await new LogoutCommandHandler(tokens, currentUser).Handle(
            new LogoutCommand("rt"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await tokens.DidNotReceive().RevokeAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
