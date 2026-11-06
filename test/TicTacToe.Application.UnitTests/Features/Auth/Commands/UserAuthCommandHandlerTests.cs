using NUnit.Framework;
using NSubstitute;
using Shouldly;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Auth.Command;
using TicTacToe.Application.Features.Auth.Dtos;

namespace TicTacToe.Application.UnitTests.Features.Auth.Commands;

public class UserAuthCommandHandlerTests
{
    private static UserDto NewUser() => new(Guid.NewGuid(), "sara_01", "sara@example.com");

    private static AuthResponse TokensFor(UserDto user) => new("access", "refresh", DateTime.UtcNow.AddMinutes(15), user);

    [Test]
    public async Task UserRegister_DuplicateUsername_ReturnsConflictFailure()
    {
        var identity = Substitute.For<IIdentityService>();
        var tokens = Substitute.For<ITokenService>();
        identity.RegisterAsync("sara_01", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserDto>(new Error("Auth.DuplicateUsername", "taken")));

        var result = await new UserRegisterCommandHandler(identity, tokens).Handle(
            new UserRegisterCommand("sara_01", "sara@example.com", "Password1"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("Auth.DuplicateUsername");
        await tokens.DidNotReceive().IssueTokensAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UserRegister_NewUser_IssuesTokens()
    {
        var user = NewUser();
        var identity = Substitute.For<IIdentityService>();
        var tokens = Substitute.For<ITokenService>();
        identity.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));
        tokens.IssueTokensAsync(user.Id, user.Username, user.Email, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(TokensFor(user));

        var result = await new UserRegisterCommandHandler(identity, tokens).Handle(
            new UserRegisterCommand(user.Username, user.Email, "Password1"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("access");
    }

    [Test]
    public async Task UserLogin_InvalidCredentials_ReturnsUnauthorizedFailure()
    {
        var identity = Substitute.For<IIdentityService>();
        identity.ValidateCredentialsAsync("sara_01", "wrong", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UserDto>(Error.InvalidCredentials()));

        var result = await new UserLoginCommandHandler(identity, Substitute.For<ITokenService>()).Handle(
            new UserLoginCommand("sara_01", "wrong"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("Auth.InvalidCredentials");
    }

    [Test]
    public async Task UserRefreshToken_PassesThroughTokenServiceResult()
    {
        var tokens = Substitute.For<ITokenService>();
        tokens.RefreshAsync("rt", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AuthResponse>(Error.InvalidRefreshToken()));

        var result = await new UserRefreshTokenCommandHandler(tokens).Handle(
            new UserRefreshTokenCommand("rt"), CancellationToken.None);

        result.Error!.Code.ShouldBe("Auth.InvalidRefreshToken");
    }

    [Test]
    public async Task UserLogout_RevokesOwnToken_AndSucceeds()
    {
        var user = NewUser();
        var tokens = Substitute.For<ITokenService>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(user.Id);

        var result = await new UserLogoutCommandHandler(tokens, currentUser).Handle(
            new UserLogoutCommand("rt"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await tokens.Received(1).RevokeAsync("rt", user.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UserLogout_WithoutAuthenticatedUser_DoesNotRevoke()
    {
        var tokens = Substitute.For<ITokenService>();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns((Guid?)null);

        var result = await new UserLogoutCommandHandler(tokens, currentUser).Handle(
            new UserLogoutCommand("rt"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await tokens.DidNotReceive().RevokeAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
