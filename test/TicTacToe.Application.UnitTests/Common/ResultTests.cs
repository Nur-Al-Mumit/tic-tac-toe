using NUnit.Framework;
using Shouldly;
using TicTacToe.Application.Common.Results;

namespace TicTacToe.Application.UnitTests.Common;

public class ResultTests
{
    [Test]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Test]
    public void Failure_CarriesError()
    {
        var error = Error.InvalidCredentials();

        var result = Result.Failure(error);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        result.Error!.Code.ShouldBe("Auth.InvalidCredentials");
    }

    [Test]
    public void FailureResult_ThrowsWhenValueAccessed()
    {
        var result = Result.Failure<string>(Error.Validation());

        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Test]
    public void SuccessWithValue_ExposesValue_AndImplicitConversion()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Test]
    public void ErrorCodes_AreStableStrings()
    {
        Error.Validation().Code.ShouldBe("Validation.Failure");
        Error.Unauthorized().Code.ShouldBe("Auth.Unauthorized");
        Error.LockedOut().Code.ShouldBe("Auth.LockedOut");
        Error.InvalidRefreshToken().Code.ShouldBe("Auth.InvalidRefreshToken");
    }
}
