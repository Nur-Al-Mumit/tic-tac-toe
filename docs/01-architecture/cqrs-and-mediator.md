# CQRS & Mediator (Cortex.Mediator)

*This document defines the command/query separation for the platform, the full command and query catalog, the Cortex.Mediator pipeline, the `Result<T>` pattern with error code conventions, and the domain-event to broadcast flow.*

Parent document: [PRD](../00-product/PRD.md) · Related: [Clean Architecture & DDD](clean-architecture-ddd.md) · [System architecture](system-architecture.md) · [API spec](../02-backend/api-spec.md) · Decision records: [ADR-0013](adr/0013-cortex-mediator.md) (supersedes [ADR-0002](adr/0002-mediatr-for-cqrs.md)) · [ADR-0009](adr/0009-result-pattern-expected-failures.md)

---

## 1. CQRS Principles for This Project

CQRS here is a code-organization discipline, not a two-database system. The split is logical: **commands** mutate state through the `Game` aggregate and repositories; **queries** are read-only projections that never touch aggregates. [Cortex.Mediator](adr/0013-cortex-mediator.md) enforces the split at the type level: commands implement `ICommand<T>` (`Cortex.Mediator.Commands`) and queries implement `IQuery<T>` (`Cortex.Mediator.Queries`) — two separate handler and pipeline families, not one shared request type.

| Principle | This project |
| --- | --- |
| Commands write, queries read — never both | Every use case is exactly one `ICommand<T>` or `IQuery<T>`; handlers never mix load-for-update with projection queries |
| Commands go through the aggregate | Load `Game` via `IGameRepository`, call a behavior method (`MakeMove`, `Join`, …), save via `IUnitOfWork` — domain rules are unskippable |
| Queries project, never load aggregates | Query handlers use `IReadDbContext` with `AsNoTracking` and `Select` straight to DTOs — no aggregate instantiation, no change tracking, no domain-rule execution on the read path |
| One database for the MVP | Both sides hit the same PostgreSQL 18 instance; CQRS introduces no extra store and no eventual consistency |
| The split is the seam for the future | Because queries depend only on `IReadDbContext`, a separate read store (e.g. cached lobby view, replica) can replace the implementation without touching handlers |
| Transport stays dumb | Controllers and the hub only create requests and map `Result` to HTTP/SignalR; they contain no business logic |

Why not full CQRS with event sourcing: the aggregate is two players and at most nine moves; a relational `games` + `moves` schema is simpler to query, trivially auditable (FR-12) and needs no replay infrastructure. The domain events remain as the integration surface for real-time push.

## 2. Command & Query Catalog

All commands/queries are dispatched through `IMediator` from controllers (REST under `/api/v1`) — commands with `SendAsync(command)`, queries with `QueryAsync(query)` (type inference picks the registered handler). The current user identity comes from `ICurrentUser` (JWT), never from the payload.

| Kind | Request | Inputs | Returns | REST surface |
| --- | --- | --- | --- | --- |
| Command (`ICommand<AuthResponse>`) | `RegisterCommand` | username, email, password | `Result<AuthResponse>` | `POST /auth/register` → 201 |
| Command (`ICommand<AuthResponse>`) | `UserLoginCommand` | identifier (email or username), password | `Result<AuthResponse>` | `POST /auth/login` → 200 |
| Command (`ICommand<AuthResponse>`) | `UserRefreshTokenCommand` | refreshToken | `Result<AuthResponse>` | `POST /auth/refresh` → 200 |
| Command (`ICommand`) | `UserLogoutCommand` | refreshToken (from `ICurrentUser` + body) | `Result` | `POST /auth/logout` → 204 |
| Command (`ICommand<GameDto>`) | `GameCreateCommand` | — (host = current user) | `Result<GameDto>` | `POST /games` → 201 |
| Command (`ICommand<GameDto>`) | `GameJoinCommand` | gameId (joiner = current user) | `Result<GameDto>` | `POST /games/{id}/join` → 200 |
| Command (`ICommand<GameDto>`) | `GameMoveCommand` | gameId, position (player = current user) | `Result<GameDto>` | `POST /games/{id}/moves` → 200 |
| Command (`ICommand`) | `GameCancelCommand` | gameId (host = current user) | `Result` | `POST /games/{id}/cancel` → 204 |
| Command (`ICommand`) | `GameLeaveCommand` | gameId, playerId (playerId required because the `DisconnectGraceService` forfeits on behalf of a disconnected user) | `Result` | `POST /games/{id}/leave` → 204 |
| Query (`IQuery<IReadOnlyList<LobbyGameDto>>`) | `GetAllGamesQuery` | status? (`waiting`, `inprogress` filter) | `Result<IReadOnlyList<LobbyGameDto>>` | `GET /games` → 200 |
| Query (`IQuery<GameDto>`) | `GetGameByIdQuery` | gameId | `Result<GameDto>` | `GET /games/{id}` → 200 / 404 |
| Query (`IQuery<GameDto?>`) | `GetMyActiveGameQuery` | — (current user) | `Result<GameDto?>` | `GET /games/active` → 200 / 204 |
| Query (`IQuery<IReadOnlyList<OnlinePlayerDto>>`) | `GetOnlinePlayersQuery` | — | `Result<IReadOnlyList<OnlinePlayerDto>>` | `GET /players/online` → 200 |

Handlers implement `ICommandHandler<TCommand, TResponse>` / `IQueryHandler<TQuery, TResponse>` with `Task<TResponse> Handle(T request, CancellationToken cancellationToken)` — void commands use the `ICommandHandler<TCommand>` variant.

### 2.1 Why commands return `GameDto` in this MVP

Commands here return the full `GameDto` rather than a bare acknowledgement for three practical reasons: (1) the caller's SPA updates immediately from the authoritative response and does not wait for its own SignalR echo; (2) after `MakeMove` the board, current turn and possibly the result have all changed, and the DTO carries exactly that reconciled state — the same shape the hub broadcasts, so one client model serves both channels; (3) it eliminates a follow-up `GET /games/{id}` on the hot path (helping the p95 < 200 ms budget). The cost is one Mapster projection per command — negligible against the aggregate load and save it follows.

## 3. Mediator Pipeline (Cortex.Mediator)

Cortex.Mediator has **separate pipelines for commands, queries and notifications**, so command-only cross-cutting concerns (validation, unit of work) apply to commands without marker-interface tricks, and queries run a leaner stack. The project's pipeline concerns, in order, and where each comes from:

| # | Concern | Implementation | Scope | On failure |
| --- | --- | --- | --- | --- |
| 1 | Logging | Built-in `LoggingCommandBehavior` / `LoggingQueryBehavior` / `LoggingNotificationBehavior` (registered by `AddDefaultBehaviors()`) — correlation ID, request type, user id, elapsed time | commands, queries, notifications | Passes exceptions through after logging them |
| 2 | Validation | `ValidationCommandBehavior` from the `Cortex.Mediator.Behaviors.FluentValidation` package: resolves all `IValidator<TCommand>`, runs them, collects failures | commands | Throws `ValidationException` → 400 ProblemDetails with per-field errors; nothing is persisted |
| 3 | Unit of work | Custom open **command** pipeline behavior `UnitOfWorkCommandPipelineBehavior<,>`: after the inner handler succeeds, calls `IUnitOfWork.SaveChangesAsync()` which commits and dispatches domain events post-commit | commands only | Concurrency exceptions bubble to the reload-once policy; commit failure → 500, no events published |
| 4 | Performance | Custom open behavior(s) `PerformanceCommandPipelineBehavior<,>` / `PerformanceQueryPipelineBehavior<,>`: stopwatch, warning when elapsed exceeds the configured threshold (default 500 ms) | commands, queries | Never fails the request |

Unexpected-exception translation to ProblemDetails stays in the Api layer's `ExceptionHandlingMiddleware` (the single mapping point), so Cortex's optional exception-handling behaviors are **not** enabled.

Registration sketch (Application `DependencyInjection`) — same shape as the reference curriculum usage, plus the open command behaviors:

```csharp
services.AddCortexMediator(
    new[] { typeof(DependencyInjection).Assembly },   // TicTacToe.Application
    options =>
    {
        options.AddDefaultBehaviors();                // logging behaviors
        // + open command pipeline behaviors:
        //   UnitOfWorkCommandPipelineBehavior<,> (innermost custom)
        //   PerformanceCommandPipelineBehavior<,>
        // + ValidationCommandBehavior (Cortex.Mediator.Behaviors.FluentValidation)
    });
```

Behavior order note: logging observes validation, validation runs before any persistence, and performance wraps the handler + commit; the exact overload names for registering open behaviors are verified against the package README at M0 (see Assumptions).

### 3.1 UnitOfWorkCommandPipelineBehavior (sketch)

```csharp
public async Task<TResponse> Handle(TCommand command,
    CommandHandlerDelegate<TResponse> next, CancellationToken ct)
{
    var response = await next();              // handler mutated the aggregate(s)
    await _unitOfWork.SaveChangesAsync(ct);   // commit + post-commit event dispatch
    return response;
}
```

Registered only on the **command** pipeline, so queries (read-only projections) never open a unit of work.

### 3.2 PerformanceCommandPipelineBehavior (sketch)

```csharp
var sw = Stopwatch.StartNew();
var response = await next();
if (sw.ElapsedMilliseconds > _options.SlowRequestMs)
    _logger.LogWarning("Slow use case {UseCase}: {ElapsedMs} ms",
        typeof(TCommand).Name, sw.ElapsedMilliseconds);
return response;
```

## 4. `Result<T>` Pattern

Expected failures are values, not exceptions. Handlers return `Result<T>`; only truly exceptional conditions (DB down, bugs, race-lost concurrency after reload) surface as exceptions.

| Member | Shape | Notes |
| --- | --- | --- |
| `Result` / `Result<T>` | `IsSuccess`, `IsFailure`, `Value` (T), `Error` | Static factories: `Result.Success()`, `Result.Success(value)`, `Result.Failure(error)` |
| `Error` | `record Error(string Code, string Message)` | `Code` is stable and machine-readable; `Message` is human-readable |
| Domain exceptions | Thrown inside the aggregate, caught by the handler and converted to `Result.Failure` | The aggregate stays idiomatic (`throw new NotYourTurnException()`); the boundary normalizes |
| Unexpected exceptions | Propagate to `ExceptionHandlingMiddleware` → 500 ProblemDetails | Never swallowed by handlers |

### 4.1 Error code conventions

| Category | Code prefix | Examples | HTTP mapping |
| --- | --- | --- | --- |
| Validation (shape) | `Validation.` | `Validation.Failure` | 400 |
| Auth | `Auth.` | `Auth.InvalidCredentials`, `Auth.LockedOut`, `Auth.DuplicateUsername`*, `Auth.DuplicateEmail`*, `Auth.InvalidRefreshToken` | 401 / 423 / 409 |
| Game rules (expected) | `Games.` | `Games.NotFound`, `Games.Full`, `Games.Busy`, `Games.NotYourTurn`, `Games.CellOccupied`, `Games.InvalidPosition`, `Games.NotInProgress`, `Games.CannotJoinOwnGame`, `Games.Forbidden`, `Games.NotHost` | 404 / 403 / 409 / 422 per [API spec](../02-backend/api-spec.md) |
| Concurrency | `Games.ConcurrencyConflict` | returned only if the reload-once retry still fails | 409 |

\* Duplicate-username/email surface as `Validation.Failure` with detail fields in the canonical registry; see [§1.4 error-code catalog](../02-backend/api-spec.md#14-error-code-catalog). The **canonical registry of exact code strings is the [API spec §1.4](../02-backend/api-spec.md#14-error-code-catalog)**; add new codes there and to the client's message map in the same change.

Codes are PascalCase, dot-namespaced, stable API surface for the Angular client's error-message mapping (`shared/error-messages.ts`). One `ProblemDetails` factory in the Api layer performs the `Error` → HTTP status translation so mapping lives in exactly one place.

## 5. Domain Event → Notification Handler Flow

Domain events are `INotification`s with `INotificationHandler<TNotification>` handlers (same names and semantics as the MediatR-era design). After `SaveChanges` succeeds, the UnitOfWork publishes them with `IMediator.PublishAsync(notification)`; Application handlers translate each event into the right `IGameNotifier` call, and Infrastructure delivers it to SignalR groups.

```mermaid
sequenceDiagram
    actor P as Player (SPA)
    participant C as GamesController
    participant PB as Command pipeline (Log → Val → UoW → Perf)
    participant MH as GameMoveCommandHandler
    participant G as Game aggregate
    participant UO as UnitOfWork
    participant DB as PostgreSQL
    participant NH as MoveMadeNotificationHandler
    participant GN as IGameNotifier → GameHub

    P->>C: POST /api/v1/games/{id}/moves { position }
    C->>PB: IMediator.SendAsync(GameMoveCommand)
    PB->>MH: after validation passes
    MH->>G: game.MakeMove(playerId, position)
    Note over G: guards pass; Move appended; board evaluated; turn flipped; MoveMade (+ GameFinished) raised
    MH-->>PB: Result<GameDto> mapping prepared
    PB->>UO: SaveChangesAsync
    UO->>DB: commit games/moves (xmin)
    DB-->>UO: ok
    UO->>NH: PublishAsync(MoveMade, GameFinished?) — post-commit
    NH->>GN: MoveMadeAsync(gameId, GameDto)
    GN-->>P: MoveMade(GameDto) → SignalR group game-{id}
    C-->>P: 200 GameDto
```

Notification handler catalog (one per event, all in Application `Games/EventHandlers`):

| Event | Handler action via `IGameNotifier` | Recipients |
| --- | --- | --- |
| `GameCreated` | lobby: `LobbyGameAdded(LobbyGameDto)` | group `lobby` |
| `GameStarted` | game room: `GameStarted(GameDto)`; lobby: `GameRemoved(gameId)` (no longer joinable) | groups `game-{id}`, `lobby` |
| `MoveMade` | game room: `MoveMade(GameDto)` | group `game-{id}` |
| `GameFinished` | game room: `GameFinished(GameDto)` with result + winning line; lobby: `GameRemoved(gameId)` | groups `game-{id}`, `lobby` |
| `GameCancelled` | game room: `GameCancelled`; lobby: `GameRemoved(gameId)` | groups `game-{id}`, `lobby` |

## Assumptions

- **Cortex.Mediator is pinned to 3.1.2** — the version used by the internship's reference solution, per [ADR-0013](adr/0013-cortex-mediator.md); upgrades are deliberate, not incidental.
- The dispatch surface used is `SendAsync` / `QueryAsync` / `PublishAsync` (the recommended extensions); the explicit `SendCommandAsync<TCommand,TResult>` / `SendQueryAsync<TQuery,TResult>` overloads exist for cases where type inference is ambiguous.
- Exact overload names for registering open pipeline behaviors (`AddOpenCommandPipelineBehavior` and friends) and the notifications namespace are verified against the package README at M0 and this section updated if they differ; the concerns and their order (Logging → Validation → UnitOfWork → Performance on commands) are the contract.
- Because Cortex.Mediator natively splits command and query pipelines, the MediatR-era `ICommand` marker-interface trick is unnecessary — the unit-of-work behavior simply lives on the command pipeline only.
- Pipeline order means performance timing starts after logging scope creation and wraps validation + commit; the measured time therefore excludes only trivial logging scope setup.
- Domain-event publication is synchronous, in-process, post-commit, and at-most-once per command; broadcast failures are logged but never fail the HTTP request (clients reconcile via `GET /api/v1/games/active`).
- Error codes listed above are the MVP contract; new codes must be added to this table and the API spec before client use.
