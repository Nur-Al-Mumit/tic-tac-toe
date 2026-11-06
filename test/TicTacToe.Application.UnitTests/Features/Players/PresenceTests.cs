using NSubstitute;
using NUnit.Framework;
using Shouldly;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Application.Features.Players.Query;
using TicTacToe.Domain.Entities;
using TicTacToe.Domain.Entities.GameAggregate;
using TicTacToe.Infrastructure.Realtime;

namespace TicTacToe.Application.UnitTests.Features.Players;

public class PresenceTests
{
    [Test]
    public void Tracker_MultipleConnections_UserOnlineOnce()
    {
        var tracker = new PresenceTracker();
        var user = Guid.NewGuid();

        tracker.TrackConnection(user, "conn-1");
        tracker.TrackConnection(user, "conn-2");
        tracker.GetOnlineUserIds().ShouldBe([user]);

        tracker.RemoveConnection(user, "conn-1").ShouldBeFalse();  // still one tab open
        tracker.GetOnlineUserIds().ShouldBe([user]);

        tracker.RemoveConnection(user, "conn-2").ShouldBeTrue();   // last tab closed
        tracker.GetOnlineUserIds().ShouldBeEmpty();
    }

    [Test]
    public async Task GetOnlinePlayers_MarksInGameFromActiveGames()
    {
        var tracker = Substitute.For<IPresenceTracker>();
        var profiles = Substitute.For<IPlayerProfileRepository>();
        var games = Substitute.For<IGameRepository>();
        var now = DateTime.UtcNow;

        var host = Guid.NewGuid();
        var idle = Guid.NewGuid();
        var game = Game.Create(host, now); // host is in a Waiting game → In game

        tracker.GetOnlineUserIds().Returns([host, idle]);
        profiles.GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new PlayerProfile(host, "host_01", now), new PlayerProfile(idle, "idle_02", now)]);
        games.GetActiveGamesAsync(Arg.Any<CancellationToken>()).Returns(new List<Game> { game });

        var result = await new GetOnlinePlayersQueryHandler(tracker, profiles, games)
            .Handle(new GetOnlinePlayersQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var byName = result.Value.ToDictionary(p => p.Username);
        byName["host_01"].InGame.ShouldBeTrue();
        byName["idle_02"].InGame.ShouldBeFalse();
    }
}
