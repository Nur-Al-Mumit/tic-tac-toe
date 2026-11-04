# ADR-0002: MediatR as the in-process mediator for CQRS

> **Superseded (2026-11-04):** the mediator is now **Cortex.Mediator** — see [ADR-0013](0013-cortex-mediator.md). This record is kept as history; the pipeline-concern design below (Logging → Validation → UnitOfWork → Performance) carries over unchanged.

| Status | **Superseded** by [ADR-0013](0013-cortex-mediator.md) — 2026-11-04 |
| --- | --- |
| Scope | Application-layer dispatch, pipeline behaviors, notification handling |

## Context

The application layer has ~13 use cases (auth, game lifecycle, lobby queries) that must share cross-cutting concerns — logging, FluentValidation, transactional commit with post-commit event dispatch, slow-request detection — uniformly. Controllers should stay thin, and domain events raised by the `Game` aggregate must fan out to notification handlers without the domain referencing infrastructure. MediatR is the de facto standard here, but its licensing changed: starting with MediatR v13 (the Lucky Penny / Pixel Instruments acquisition era), the package moved to a commercial license with a free community tier; earlier versions (through 12.x) are Apache-2.0. This project is a non-commercial internship product.

## Decision

Use **MediatR (latest stable)** as the in-process mediator for commands, queries, and domain-event notifications:

- Every use case is one `IRequest<T>` handled by one handler; controllers and `GameHub` dispatch via `IMediator` and contain no logic.
- Cross-cutting concerns are pipeline behaviors registered in the fixed order **Logging → Validation → UnitOfWork → Performance**; the order is part of this decision (logging must observe validation failures; validation must precede any commit; performance wraps the work).
- Domain events (`GameCreated`, `GameStarted`, `MoveMade`, `GameFinished`, `GameCancelled`) are published as `INotification` after `SaveChanges`; one `INotificationHandler` per event translates to `IGameNotifier` calls.
- **Licensing note:** this project operates inside MediatR v13+'s free tier (independent/non-commercial use, single product, small team). If the project ever becomes commercial or the free tier terms become unacceptable, the fallback is to **pin MediatR 12.x (Apache-2.0)** — the API surface used here (`ISender`, `IRequest<T>`, `IPipelineBehavior<,>`, `INotification`) is compatible across both lines, so the downgrade is a package reference change, not a redesign.

The fixed behavior order and why each position matters:

| Order | Behavior | Why it sits there |
| --- | --- | --- |
| 1 | `LoggingBehavior` | Outermost so it observes validation failures and total elapsed time |
| 2 | `ValidationBehavior` | Before persistence so invalid requests never open a transaction |
| 3 | `UnitOfWorkBehavior` | Innermost wrapper: commits and publishes events only after the handler succeeds |
| 4 | `PerformanceBehavior` | Times the whole inner stack (validation + handler + commit) |

## Consequences

**Positive**

- One pipeline enforces validation-before-persistence and commit-then-publish semantics for every use case; new commands inherit all four behaviors by default.
- Controllers, hub and hosted service (`DisconnectGraceService` sends `ForfeitGameCommand`) all enter the application through the same door.
- Domain-event fan-out stays decoupled: aggregate → `INotification` → handler → `IGameNotifier`; adding a side effect (e.g. lobby cache update) means a new handler.
- Behavior code is written once and tested once (`Application.UnitTests`), not repeated in 13 handlers.

**Negative**

- MediatR v13 licensing requires awareness during any commercialization conversation; a future pin to 12.x freezes new upstream features.
- Extra indirection: every request flows controller → mediator → behavior chain → handler; stack traces and onboarding cost grow slightly.
- Delegate-based pipelines can hide control flow (mitigated by the fixed, documented order and by keeping each behavior tiny).
- Compile-time safety is weaker than calling handlers directly — an unregistered or multiply-registered handler surfaces at test/runtime time (covered by integration tests).

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Plain injected handlers (no mediator) | Cross-cutting concerns would move into a decorator per interface or into controllers; no uniform ordering guarantee; more boilerplate than four behaviors |
| Warp, Mediator (source-generated), or Brighter/Tranquil | Each viable; rejected as less widely documented for this team and stack, and Brighter pulls in its own command routing/outbox scope the MVP does not need |
| Hand-rolled mediator class | Reimplements `IPipelineBehavior` composition and DI scanning poorly; hidden custom infrastructure is harder to reason about than a known library |
| Immediate removal of mediator post-v13-license news | The free tier covers this project; the abstraction (thin handlers + behaviors) is mediator-agnostic anyway, so removal is cheap later — not worth paying now |

## References

- [PRD](../../00-product/PRD.md) (§7 Technical Blueprint — MediatR row)
- [CQRS & Mediator](../cqrs-and-mediator.md) — pipeline details, sketches, event flow
- [Clean Architecture & DDD](../clean-architecture-ddd.md) — event dispatch design
- [ADR-0009 Result pattern](0009-result-pattern-expected-failures.md) — how handlers report expected failures
- [ADR-0011 Latest stable stack](0011-latest-stable-stack.md)

## Assumptions

- The product remains non-commercial/independent work under the MediatR free tier for the foreseeable future; any commercial pivot triggers the 12.x pin decision explicitly.
- Only the stable, documented MediatR API surface described here is used — no custom dispatcher internals — keeping the 12.x fallback realistic.
