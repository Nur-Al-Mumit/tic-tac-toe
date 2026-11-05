using NUnit.Framework;

namespace TicTacToe.Api.IntegrationTests.Fixtures;

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
