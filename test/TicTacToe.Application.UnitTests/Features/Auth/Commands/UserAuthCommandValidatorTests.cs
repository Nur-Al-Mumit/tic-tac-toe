using FluentValidation.TestHelper;
using NUnit.Framework;
using TicTacToe.Application.Features.Auth.Command;

namespace TicTacToe.Application.UnitTests.Features.Auth.Commands;

public class UserAuthCommandValidatorTests
{
    private readonly UserRegisterCommandValidator _registerValidator = new();

    [Test]
    public void UserRegister_ValidCommand_Passes()
    {
        var result = _registerValidator.TestValidate(new UserRegisterCommand("sara_01", "sara@example.com", "Password1"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestCase("ab")]
    [TestCase("abcdefghijklmnopqrstu")]
    [TestCase("has space")]
    [TestCase("døsh")]
    public void UserRegister_InvalidUsername_Fails(string username)
    {
        var result = _registerValidator.TestValidate(new UserRegisterCommand(username, "x@example.com", "Password1"));

        result.ShouldHaveValidationErrorFor(c => c.Username);
    }

    [TestCase("password1")]
    [TestCase("PASSWORD1")]
    [TestCase("Passwordx")]
    [TestCase("Pa1")]
    public void UserRegister_WeakPassword_Fails(string password)
    {
        var result = _registerValidator.TestValidate(new UserRegisterCommand("sara_01", "x@example.com", password));

        result.ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Test]
    public void UserRegister_InvalidEmail_Fails()
    {
        var result = _registerValidator.TestValidate(new UserRegisterCommand("sara_01", "not-an-email", "Password1"));

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }
}
