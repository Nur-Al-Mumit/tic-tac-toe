using FluentValidation.TestHelper;
using NUnit.Framework;
using Shouldly;
using TicTacToe.Application.Auth.Commands;

namespace TicTacToe.Application.UnitTests.Auth;

public class AuthValidatorsTests
{
    private readonly RegisterUserCommandValidator _registerValidator = new();
    private readonly LoginCommandValidator _loginValidator = new();

    [Test]
    public void Register_ValidCommand_Passes()
    {
        var command = new RegisterUserCommand("sara_01", "sara@example.com", "Password1");

        var result = _registerValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestCase("ab", "too short")]
    [TestCase("abcdefghijklmnopqrstu", "too long")]
    [TestCase("has space", "invalid characters")]
    [TestCase("døsh", "non-ascii")]
    public void Register_InvalidUsername_Fails(string username, string because)
    {
        var result = _registerValidator.TestValidate(new RegisterUserCommand(username, "x@example.com", "Password1"));

        result.ShouldHaveValidationErrorFor(c => c.Username);
    }

    [TestCase("password1", "missing uppercase")]
    [TestCase("PASSWORD1", "missing lowercase")]
    [TestCase("Passwordx", "missing digit")]
    [TestCase("Pa1", "too short")]
    public void Register_WeakPassword_Fails(string password, string because)
    {
        var result = _registerValidator.TestValidate(new RegisterUserCommand("sara_01", "x@example.com", password));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Test]
    public void Register_InvalidEmail_Fails()
    {
        var result = _registerValidator.TestValidate(new RegisterUserCommand("sara_01", "not-an-email", "Password1"));

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [TestCase("", "")]
    [TestCase("sara", "")]
    [TestCase("", "pw")]
    public void Login_MissingFields_Fails(string identifier, string password)
    {
        var result = _loginValidator.TestValidate(new LoginCommand(identifier, password));

        result.IsValid.ShouldBeFalse();
    }
}
