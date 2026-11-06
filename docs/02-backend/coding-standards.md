# Backend Coding Standards

*The engineering standards for the backend — C# 14 conventions, per-layer rules, error-handling contract, Cortex.Mediator usage, EF Core discipline, logging rules, analyzer/formatting setup, and the commit-message and pull-request checklists.*

Parent document: [PRD](../00-product/PRD.md) · Related: [Clean Architecture & DDD](../01-architecture/clean-architecture-ddd.md) · [CQRS & Mediator](../01-architecture/cqrs-and-mediator.md) · [API spec](api-spec.md) · [Auth, Identity & JWT](auth-identity-jwt.md) · [Testing strategy](../05-quality/testing-strategy.md) · [CI/CD](../06-devops/ci-cd.md)

---

## 1. C# 14 Conventions

The house style, applied uniformly across `src/` and `tests/` and enforced by `.editorconfig` + analyzers (§7):

| Area | Rule |
| --- | --- |
| Namespaces | File-scoped (`namespace TicTacToe.Application.Games;`) — one per file, matching folder structure |
| Nullability | `<Nullable>enable</Nullable>` everywhere; no `!` suppressions except in tests; no `null` returns where a `Result` failure or `Option`-shaped API fits |
| `var` | Use `var` when the type is obvious from the right-hand side (`var game = await repo.GetByIdAsync(id);`); explicit types when it aids readability (`GameStatus status = …`, `Result<GameDto> result = …`) |
| Types | `sealed` by default (open for extension only where a plugin seam is intended — behaviors, validators); `static` classes for pure helpers; no inheritance except documented abstract bases (`Entity`, `ValueObject`) |
| DTOs | `record` types with `required init` properties — immutable, structural equality, cheap to test: `public sealed record GameDto(Guid Id, GameStatus Status, PlayerRef PlayerX, PlayerRef? PlayerO, …);` |
| Mutability | Domain entities own their mutation (private setters, behavior methods); DTOs and value objects are immutable; `required`/`init` everywhere a constructor is not clearer |
| Naming | Table below |

| Element | Casing | Examples |
| --- | --- | --- |
| Types, methods, properties, constants | PascalCase | `Game`, `MakeMove`, `CurrentTurn` |
| Parameters, locals | camelCase | `playerId`, `cmd` |
| Private fields | `_camelCase` | `_gameRepository` |
| Interfaces | `I` prefix | `IGameRepository` |
| Async methods | `Async` suffix | `GetByIdAsync`, `Handle` (interface-mandated names keep their contract) |
| Commands/queries | `<Verb><Noun>Command` / `Get<Noun>Query` | `MakeMoveCommand`, `GetLobbyGamesQuery` |
| Files | One primary type per file, named after it | `GameFullException.cs` |

Illustrative aggregate slice in house style:

```csharp
public sealed class Game : AggregateRoot
{
    private readonly List<Move> _moves = [];
    public Mark CurrentTurn { get; private set; }

    public Move MakeMove(Guid playerId, int position)
    {
        if (position is < 0 or > 8) throw new InvalidPositionException(position);
        // … guards, mutation, event
        RaiseDomainEvent(new MoveMade(Id, position, mark, _moves.Count, _clock.UtcNow));
        return move;
    }
}
```

## 2. Layer Rules

Dependencies point inward only ([clean architecture §1](../01-architecture/clean-architecture-ddd.md#1-the-dependency-rule)); the table below is the per-project law, enforced mechanically by NetArchTest rules in CI ([clean architecture §8](../01-architecture/clean-architecture-ddd.md#8-enforcing-the-layering-architecture-tests)):

| Project | Allowed | Forbidden |
| --- | --- | --- |
| `TicTacToe.Domain` | Pure C#/BCL only: aggregates, value objects, domain events, domain exceptions, repository interfaces | **No project references; no NuGet packages** (not even Cortex.Mediator/EF); no DTOs, no HTTP/DB/serialization concepts |
| `TicTacToe.Application` | References Domain; Cortex.Mediator, FluentValidation; `Contracts/` (`IApplicationUnitOfWork`, `Repositories/`, `Services/`); `Features/<Feature>/Command|Query` with DTOs and validators; `Common/` (Results, Behaviors, Events) | **No EF Core / Npgsql types in public contracts** (repositories speak aggregates; persistence lives behind Infrastructure implementations); no SignalR, no Identity types; no `HttpContext` |
| `TicTacToe.Infrastructure` | References Application + Domain; `Data/` (ApplicationDbContext, UnitOfWork/ApplicationUnitOfWork, generic Repository, repositories, migrations), `Extensions/`, `Identity/`, `Services/` | **No controllers, no hubs**; never references the host; no business rules — only adaptation of Application contracts |
| `TicTacToe.Api` | References Application (+ Infrastructure for DI composition only); controllers, `GameHub`, middleware, `Realtime/SignalRGameNotifier`, auth/OpenAPI/Serilog setup | **Thin controllers**: a `SendAsync`/`QueryAsync` to `IMediator` and a mapping of `Result` to an action — no repositories, no `DbContext`, no aggregates, no business logic |

Two corollaries: a new dependency may only enter at the layer that owns it (e.g. only Infrastructure sees Npgsql); and any type that appears in a public Application signature must itself live in Domain/Application — never an Infrastructure type leaked through.

## 3. Error Handling

The contract between Domain, Application and Api ([ADR-0009](../01-architecture/adr/0009-result-pattern-expected-failures.md)):

| Situation | Mechanism | Becomes |
| --- | --- | --- |
| Guard clause inside the aggregate | `throw new NotYourTurnException()` — idiomatic domain code | Caught at the **handler boundary**, converted to `Result.Failure(new Error("Games.NotYourTurn", …))` |
| Expected use-case failure (aggregate said no, identity said duplicate, concurrency lost the race) | `Result`/`Result<T>` value | Mapped to HTTP by **one** ProblemDetails factory in Api — 409/422/404/403/401/423 per the [error catalog](api-spec.md#14-error-code-catalog) |
| Malformed request shape | `ValidationBehavior` throws `ValidationException` before any handler runs | 400 `Validation.Failure` with per-field `errors` |
| Unexpected failure (DB down, bugs, second concurrency conflict) | Exception propagates | `ExceptionHandlingMiddleware` → 500 ProblemDetails, logged with correlation id |

Rules:

1. **Expected failures are values, not exceptions.** If a caller can reasonably receive the failure (full game, wrong turn), it is a `Result` error, and it must not pollute exception telemetry (the < 1% error-rate metric).
2. **One ProblemDetails factory.** `Error.Code` → HTTP status + body lives in exactly one Api-layer component; controllers never `switch` on codes; adding a code means updating the factory and the [catalog](api-spec.md#14-error-code-catalog) in the same PR.
3. **Never swallow exceptions.** No empty `catch`, no `catch (Exception) { }`-and-continue; the only narrow catches are the documented ones (handler catching domain exceptions to build a `Result`; the reload-once `DbUpdateConcurrencyException` handler — [concurrency §3](../03-database/concurrency.md#3-the-pattern-load--domain-method--save--reload-once--re-evaluate)).
4. **Broadcast failures never fail the request**: notification-handler exceptions are logged and the command still returns success (clients resync via `GET /games/active`).
5. **Validators validate shape; the aggregate is the authority on rules.** Where the API contract assigns a non-400 status to a rule (position range → 422), the rule belongs to the aggregate/`Result` path, not to FluentValidation — see [api-spec §2.12](api-spec.md#212-post-gamesidmoves--make-a-move).

## 4. Mediator Usage (Cortex.Mediator)

| Rule | Detail |
| --- | --- |
| One use case, one request, one handler | `CreateGameCommand` has exactly one `CreateGameCommandHandler`; no god-handlers, no handler dispatching to other handlers |
| One file trio per use case | `<Name>Command.cs`, `<Name>CommandHandler.cs`, `<Name>CommandValidator.cs` side by side in `Features/<Feature>/Command/` (queries in `Query/`) — findable by folder, not search |
| A validator per command | Every command has a FluentValidation validator even if trivial today; queries validate only when they accept input (e.g. `status` filter) |
| Handlers do the orchestration only | Load aggregate → call behavior method → map to DTO → return `Result`; authorization checks against `ICurrentUser` happen before touching the aggregate; no HTTP concepts anywhere near handlers |
| Controllers and hub stay dumb | Controller = bind → `SendAsync`/`QueryAsync` → map `Result` to action; `GameHub` = presence + group logic only. Any `if` that encodes a game rule in a controller/hub is a bug |
| Pipeline order is fixed | `LoggingCommandBehavior` → `ValidationCommandBehavior` → `UnitOfWorkCommandPipelineBehavior` → `PerformanceCommandPipelineBehavior` ([cqrs-and-mediator §3](../01-architecture/cqrs-and-mediator.md#3-mediator-pipeline-cortexmediator)); behaviors are generic — no per-use-case pipelines |
| Commit in one place | `UnitOfWorkBehavior` is the only `SaveChangesAsync` caller on the command path (queries never save); domain events dispatch post-commit from the same behavior |
| No `SendAsync` from handlers | Handlers do not send commands to other handlers; cross-cutting reactions to domain events are `INotificationHandler`s |

## 5. EF Core Rules

| Rule | Detail |
| --- | --- |
| No `IQueryable` out of repositories | `IGameRepository` returns materialized aggregates (`GetByIdAsync`, `GetActiveGameForPlayerAsync`, `AddAsync`, `Update`) — callers cannot compose un-translated queries; the read side projects through `IReadDbContext` instead |
| Read path is projection-only | Query handlers use `IReadDbContext` with `AsNoTracking()` and `Select` straight into DTOs; **every** read query sets `AsNoTracking` — a tracked read query is a review blocker |
| Write path goes through the aggregate | Only command handlers load via `IGameRepository` and call behavior methods; nothing else writes `games`/`moves` |
| One commit point | No `BeginTransaction` in handlers; the implicit transaction inside `SaveChangesAsync` (driven by `UnitOfWorkBehavior`) covers the aggregate's row plus its moves — the only explicit transaction discipline is that there is none outside the behavior |
| Concurrency | `Game.Version` (`uint`, `IsRowVersion()` → PostgreSQL `xmin`) guards updates; on `DbUpdateConcurrencyException` follow the reload-once-and-re-evaluate pattern — never blind-retry the write ([concurrency §3](../03-database/concurrency.md#3-the-pattern-load--domain-method--save--reload-once--re-evaluate)) |
| Migrations live in Infrastructure | `dotnet ef migrations add` runs against the Infrastructure project (design-time factory present); generated migrations are committed; raw SQL only in tests (timestamp manipulation) |
| Mapping | Snake-case naming via the convention plugin; enums as `smallint` with explicit numeric conversions; `Result` as an owned type; no `[Table]`/`[Column]` attributes on Domain types — configuration classes in `Persistence/Configurations` keep the Domain persistence-ignorant ([schema §8](../03-database/schema-and-erd.md#8-how-ef-core-maps-this)) |
| SQLSTATE `23505` unique violations | Translated to domain `Result` errors (`Games.Busy` etc.), never surfaced as 500 ([concurrency §7](../03-database/concurrency.md#7-unique-constraints-the-last-line-of-defense)) |

## 6. Logging Rules

Serilog, structured, configured in `Program.cs` ([system architecture §5](../01-architecture/system-architecture.md#5-cross-cutting-concerns)):

| Rule | Detail |
| --- | --- |
| Structured templates, not interpolation | `_logger.LogInformation("← {UseCase} in {ElapsedMs} ms", name, sw.ElapsedMilliseconds);` — properties become queryable fields; string interpolation in log templates is a review blocker |
| Message templates carry names, not values, for sensitive fields | Never log passwords, tokens (raw or hashed), Authorization headers, full emails or query strings of `/hubs` ([security §3.11](../05-quality/security.md#311-privacy-and-logging)); emails at most masked `j***@example.com` |
| Correlation everywhere | `CorrelationIdMiddleware` pushes the correlation id into `LogContext`; `LoggingBehavior` opens a per-request scope (`UseCase`, `UserId`, `RequestId`); every log line inside a request carries it |
| Right level | `Information` = use-case start/end + lifecycle events; `Warning` = expected-but-noteworthy (slow handler, rate-limited request, broadcast failure); `Error` = unexpected exceptions (with exception object, not `ex.Message` only); `Debug` = off in production |
| One logger per class | `ILogger<T>` via DI; no static loggers, no `Console.WriteLine` |
| Domain does not log | The aggregate throws; the boundary logs — `LoggingBehavior` logs every `Result` failure with its `Error.Code` so expected failures are observable without being exceptions |

## 7. Analyzers and Formatting

| Control | Setting |
| --- | --- |
| Warnings are errors | `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props` — the build is clean or it fails |
| Format | `dotnet format --verify-no-changes` runs in CI (`backend-unit` job — [testing strategy §12](../05-quality/testing-strategy.md#12-ci-integration)); run `dotnet format` locally before committing |
| `.editorconfig` essentials | 4-space indent, UTF-8; final newline + trimmed trailing whitespace enforced; `var` when apparent (`csharp_style_var_when_type_is_apparent: true:suggestion`); file-scoped namespaces enforced; naming rules from §1 (private fields `_camelCase`, parameters camelCase, all public symbols PascalCase) shipped as analyzer rules |
| Analyzer set | Roslyn IDE analyzers at `Warning`+ plus `Microsoft.CodeAnalysis.NetAnalyzers`; nullability analyzers on; XML doc comments required only on Application public abstractions, not on internal implementation |
| Test naming | `MethodName_Scenario_ExpectedResult` ([testing strategy §9.1](../05-quality/testing-strategy.md#91-naming)); tests follow AAA with visibly separated regions |

## 8. Commit Messages

Conventional Commits, imperative mood, lower-scoped type, ≤ 72-char subject:

```text
feat(auth): rotate refresh tokens with family revocation on reuse

Handlers return Auth.InvalidRefreshToken and revoke the replaced_by chain
when a revoked token is presented. Adds handler tests + integration test
Refresh_ReusedRevokedToken_Returns401AndRevokesFamily.
```

| Type | Use for |
| --- | --- |
| `feat` | New user-visible capability (endpoint, hub event, behavior) |
| `fix` | Bug fix |
| `refactor` | Behavior-preserving change |
| `test` | Test-only changes |
| `docs` | Documentation only |
| `chore` | Build, CI, tooling, dependencies |
| `perf` | Performance-focused change with no behavior change |

Scope is the area (`auth`, `games`, `realtime`, `db`, `ci`, `client`). Breaking contract changes add a `!` (`feat(api)!:`) and must reference the [API spec](api-spec.md#5-api-versioning-strategy) update. One logical change per commit; the subject alone should let a reviewer decide whether the commit concerns them.

## 9. Pull Request Checklist

A PR is ready when every row is checked (mirrors the Definition of Done in [testing strategy §14](../05-quality/testing-strategy.md#14-definition-of-done)):

| # | Check |
| --- | --- |
| 1 | Builds with `TreatWarningsAsErrors`; `dotnet format --verify-no-changes` clean |
| 2 | Layering respected (§2) — new dependencies justified and placed at the owning layer; NetArchTest still green |
| 3 | New/changed use case follows the trio convention (§4) with a validator, and its handler returns `Result` for every expected failure (§3) |
| 4 | Every failure path has a test asserting the `Error.Code` and, for endpoints, the HTTP status in the [catalog](api-spec.md#14-error-code-catalog) — codes added here in the same PR |
| 5 | Coverage gates hold for the touched projects (Domain ≥ 90%, Application ≥ 80%, backend ≥ 70% — [testing strategy §10.2](../05-quality/testing-strategy.md#102-gates-ci-fails-below)) |
| 6 | EF changes (if any): migration added in Infrastructure, snake_case honored, indexes reviewed against [migrations-and-indexes](../03-database/migrations-and-indexes.md) |
| 7 | Queries use `AsNoTracking` + projection; no `IQueryable` crossed a boundary (§5) |
| 8 | Logging follows §6 — structured templates, correlation scope, zero PII |
| 9 | OpenAPI/Scalar surfaces the change (`ProducesResponseType` attributes updated; DTOs are records) |
| 10 | Security-relevant rows of the [pre-release checklist](../05-quality/security.md#8-pre-release-security-checklist) re-verified if auth/hub/authorization was touched |
| 11 | Works in the docker-compose stack (`docker compose up` smoke) |
| 12 | Commit messages follow §8; PR description states what/why and links the user story (US-xx) |

## Assumptions

- Standards target C# 14 / .NET 10 as pinned by [ADR-0011](../01-architecture/adr/0011-latest-stable-stack.md); collection expressions (`[]`) and latest pattern matching are in style.
- The ProblemDetails factory and `ExceptionHandlingMiddleware` are two components of one mechanism (Result mapping vs exception mapping) and are reviewed together; "one factory" in §3 means one code→HTTP mapping location, not necessarily one class.
- Editorconfig specifics beyond the essentials (severity of each IDE rule) are implementation detail settled in the M0 PR that adds the file; the table in §7 is the binding part.
- Conventional Commits scope vocabulary is fixed to the listed set; introducing a new scope is allowed but should be rare and obvious.
- The PR checklist is enforced socially in review plus automatically where CI can see it (rows 1, 2, 4, 5, 8 partially); CI mapping is defined in [ci-cd](../06-devops/ci-cd.md).
