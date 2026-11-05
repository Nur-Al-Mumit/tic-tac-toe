using NUnit.Framework;
using TicTacToe.Api.IntegrationTests.Fixtures;

// NOTE: deliberately in the assembly's ROOT namespace — a NUnit [SetUpFixture] only applies
// to its own namespace and below, so a Fixtures-namesoped one never runs for Auth/Games/Realtime.
namespace TicTacToe.Api.IntegrationTests;

[SetUpFixture]
public class ApiFixture
{
    public static ApiFactory Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Factory = new ApiFactory();
        await Factory.InitializeAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Factory.DisposeAsync();
    }
}
