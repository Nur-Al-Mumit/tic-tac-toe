# ADR-0015: Vertical-slice code organization and a single unit-test project

| Status | Accepted — 2026-11-05 · Refines [ADR-0001](0001-clean-architecture-and-ddd.md) (structure detail) and [ADR-0012](0012-nunit-test-framework.md) (test scope) |
| --- | --- |
| Scope | Solution folder/class naming; test project layout; removal of the integration test project |

## Context

The V1 build initially used generic folder names (`Application/Games/Commands`, `Infrastructure/Persistence`, three test projects). As the codebase grows, use cases were split across folders by technical role, making every change touch several places. The decision: reorganize around **vertical slices** — one `Features/<Feature>/` folder per capability with `Command`/`Query` sides, a contracts-first Application, a `Data`-centric Infrastructure — and keep **one** test project: `Application.UnitTests` with NUnit + Shouldly only, containing both handler tests and domain-rule tests; the integration test project is dropped in favor of fast, containerless verification.

## Decision

### Code organization

| Project | Layout |
| --- | --- |
| `TicTacToe.Domain` | `Contracts/` (`IAggregateRoot<TKey>`, `IRepository<TAggregateRoot,TKey>`, `IUnitOfWork`, `IDomainEvent`, `IHasDomainEvents`) · `Entities/GameAggregate/` (Game, Move, enums, result VO, events, board evaluator) · `Entities/PlayerProfile.cs` · `Exceptions/` |
| `TicTacToe.Application` | `Contracts/` (`IApplicationUnitOfWork` exposing the repositories, `Repositories/`, `Services/`) · `Common/` (`Results/`, `Behaviors/`, `Events/`) · `Features/<Feature>/Command/<Name>Command{,Handler,Validator}.cs` · `Features/<Feature>/Query/GetXxxQuery{,Handler}.cs` · feature-local `Dtos/` and `EventHandlers/` |
| `TicTacToe.Infrastructure` | `Data/` (`ApplicationDbContext`, `UnitOfWork` base, `ApplicationUnitOfWork`, generic `Repository<T,TKey>`, `Repositories/`, `Migrations/`) · `Extensions/` (`DependencyInjections`, `DbContextExtension`, `IdentityConfiguration`) · `Identity/` · `Services/` |
| `TicTacToe.Api` | Host: `Controllers/`, `Hubs/`, `Middleware/`, `Realtime/`, `Services/`, `Common/` |
| `test/TicTacToe.Application.UnitTests` | **Singular `test/` folder, one project.** NUnit + Shouldly (+ NSubstitute, FluentValidation.TestHelper). Mirrors the features: `Features/<X>/Commands|Queries/<Name>HandlerTests.cs`; domain rules live alongside as `Features/<X>/<Thing>Tests.cs`. |

Naming: commands are noun-first (`GameMoveCommand`, `UserRegisterCommand`), queries read naturally (`GetAllGamesQuery`, `GetMyActiveGameQuery`), handlers inject `IApplicationUnitOfWork` and reach repositories through it. 

### Test scope

All automated backend verification is the single unit suite — domain rules (aggregate behaviors, all 8 win lines, draw, forfeit, immutability), command/query handlers (mocked via NSubstitute), validators, and pipeline behaviors. It runs anywhere `dotnet test` runs; no Docker required. The join race and SignalR delivery are covered by design (xmin reload-once, ADR-0006) plus the unit suites; an integration/E2E layer can be added post-V1 as a separate decision if needed.

## Consequences

**Positive**

- One predictable convention: any use case is found in exactly one folder, and its tests sit beside it.
- Feature folders group everything a use case touches (command, handler, validator, DTOs) — vertical slices.
- CI needs no Docker for tests; the suite is fast and runs on every push.
- The generic `Repository<T,TKey>`/`UnitOfWork` base keeps data-access plumbing in one reusable place.

**Negative**

- The integration suite (real PostgreSQL, two-client SignalR, the simultaneous-join race) is gone; those behaviors are now verified by unit tests plus the documented concurrency design, not by an executing test.
- Two `SaveAsync` idioms exist (`IUnitOfWork.SaveAsync()` without a cancellation parameter; handlers pass no token).
- `Move.Id` is EF-generated rather than domain-assigned — a client-set Guid key on a child of a *tracked* aggregate makes EF change-tracking treat the new move as an existing row (UPDATE → 0 rows → `DbUpdateConcurrencyException`). Generated keys keep DetectChanges classifying new children as Added.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Keep the previous layout (generic Commands/Queries folders, `Persistence/`, three test projects) | Workable, but use cases stay scattered across technical folders and the three-project test split adds ceremony without coverage |
| Keep the integration project alongside the unit project | Stakeholder mandated NUnit+Shouldly unit tests only; CI simplification is a side benefit |
| Domain-assigned `Move.Id` + explicit entity-state fixups | Fragile; generated Guid keys are the canonical EF pattern for appended children |

## References

- [Clean Architecture & DDD](../clean-architecture-ddd.md) — updated folder specification
- [CQRS & Mediator](../cqrs-and-mediator.md) — renamed use-case catalog
- [Testing strategy](../../05-quality/testing-strategy.md) — rewritten for the unit-only suite
- [ADR-0012](0012-nunit-test-framework.md) (NUnit) · [ADR-0014](0014-shouldly-assertions.md) (Shouldly) — unchanged tool choices

## Assumptions

- Applied during V1 (nothing shipped depends on the old names).
- Frontend testing is unaffected: vitest specs for the Angular client stay as they are.
- Post-V1 may reintroduce an integration suite as a new ADR if the team wants executing race/hub coverage.
