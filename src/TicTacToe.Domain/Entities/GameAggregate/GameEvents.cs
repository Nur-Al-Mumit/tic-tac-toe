using TicTacToe.Domain.Contracts;

namespace TicTacToe.Domain.Entities.GameAggregate;

public sealed record GameCreated(
    Guid GameId,
    Guid HostPlayerId,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record GameStarted(
    Guid GameId,
    Guid PlayerXId,
    Guid PlayerOId,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record MoveMade(
    Guid GameId,
    Guid PlayerId,
    Mark Mark,
    int Position,
    int MoveNumber,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record GameFinished(
    Guid GameId,
    GameOutcome Outcome,
    FinishReason Reason,
    int[]? WinningLine,
    Guid? WinnerPlayerId,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record GameCancelled(
    Guid GameId,
    Guid HostPlayerId,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
