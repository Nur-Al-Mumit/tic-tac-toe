using Cortex.Mediator.Commands;
using FluentValidation;
using TicTacToe.Application.Common.Results;
using TicTacToe.Application.Features.Games.Dtos;

namespace TicTacToe.Application.Features.Games.Command;

public sealed record GameCreateCommand : ICommand<Result<GameDto>>;

public sealed class GameCreateCommandValidator : AbstractValidator<GameCreateCommand>;
