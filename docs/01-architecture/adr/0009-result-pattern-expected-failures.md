# ADR-0009: Result pattern for expected failures

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Error handling contract between handlers, controllers and the client |

## Context

Most Tic-Tac-Toe "failures" are ordinary game outcomes, not exceptional conditions: joining a full game, moving out of turn, clicking an occupied cell, duplicate username. The domain expresses these naturally as exceptions (`GameFullException`, `NotYourTurnException`, …), while the API needs stable, machine-readable codes the Angular client maps to friendly messages — with a correct HTTP status per case (409, 422, 404, 401). Letting domain exceptions fly straight to middleware would work, but gives no typed channel for anticipated failures and blurs "expected" into "server error" in logs and tests.

## Decision

Handlers return **`Result<T>`** for every use case; **expected failures are values, exceptions are for the unexpected**:

- Shape: `Result` / `Result<T>` with `IsSuccess`, `IsFailure`, `Value` (T) and `Error`; `Error` is `record Error(string Code, string Message)`. Factories: `Result.Success(value)`, `Result.Failure(error)`.
- Domain exceptions are thrown inside the aggregate (idiomatic guard clauses) and **caught at the handler boundary**, converted to `Result.Failure` with dot-namespaced codes (`Game.Full`, `Game.NotYourTurn`, `Auth.InvalidCredentials`, …) per the [error code conventions](../cqrs-and-mediatr.md#41-error-code-conventions).
- One `ProblemDetails` factory in the Api layer maps `Error.Code` → HTTP status + ProblemDetails body; controllers never switch on codes.
- Unexpected exceptions (DB down, bugs, exhausted concurrency retry) propagate to `ExceptionHandlingMiddleware` → 500; they are never converted to `Result`.
- `ValidationBehavior`'s `ValidationException` → 400 with per-field errors stays a real exception (it is thrown before any handler runs).

Canonical error-to-HTTP examples (full table in the [CQRS & MediatR](../cqrs-and-mediatr.md#41-error-code-conventions) doc):

| `Error.Code` | HTTP | Example trigger |
| --- | --- | --- |
| `Game.Full` | 409 | lost the join race (US-07) |
| `Game.NotYourTurn` | 409 | moved on opponent's turn |
| `Game.CellOccupied` / `Game.InvalidPosition` | 422 | occupied cell / position outside 0..8 |
| `Auth.InvalidRefreshToken` | 401 | expired or revoked family |

## Consequences

**Positive**

- Method signature honesty: `Result<GameDto>` tells callers failure is planned-for; the success path carries the authoritative DTO.
- Codes (`Game.Full`) are a stable client contract independent of exception class names — refactoring the Domain cannot silently break the SPA's error-message mapping.
- Unit tests assert failure *reasons* (`result.Error.Code.Should().Be("Game.NotYourTurn")`), which reads better than exception-type assertions and matches the API contract exactly.
- Expected failures stay out of exception logging/alerting — Serilog noise reflects only real problems (supporting the <1% error-rate metric).

**Negative**

- One more pattern to learn; nothing forces a handler to check `IsFailure` in tests besides review and coverage rules.
- A mapping layer (exception → Error code) must be maintained in one place; a missed mapping would surface as an unexpected 500 — covered by handler unit tests enumerating every guard.
- `Result<T>` lacks language support (no `!` propagation); pipelining helpers may be added if nesting gets noisy.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Throw domain exceptions straight to middleware → HTTP | Fewer types, but the client couples to exception names or HTTP text, expected failures pollute exception telemetry, and handler unit tests become try/catch plumbing |
| `Result` with an Error **enum** | Exhaustive switch is nice, but enums invite numbering churn and can't carry a per-case message without a parallel lookup |
| OneOf / discriminated-union packages | Equivalent power; adds a dependency for a shape two records already express cleanly |
| Nullable returns (`GameDto?` with no reason) | Cannot distinguish "full" from "not found" from "not your turn" — US-07 explicitly demands distinct messaging |

## References

- [PRD](../../00-product/PRD.md) (US-07 acceptance criteria, FR-06/07/08)
- [CQRS & MediatR](../cqrs-and-mediatr.md) §4 — full pattern and error-code table
- [API spec](../../02-backend/api-spec.md) — code → HTTP status mapping
- [ADR-0002 MediatR](0002-mediatr-for-cqrs.md) — pipeline that carries this contract
- [ADR-0006 xmin](0006-optimistic-concurrency-xmin.md) — the `Game.ConcurrencyConflict` residual case

## Assumptions

- Every error code introduced must be added to the cqrs-and-mediatr table and the API spec in the same change.
- The client keys its message catalog on `Error.Code`, never on `Error.Message` (which is developer-facing English).
