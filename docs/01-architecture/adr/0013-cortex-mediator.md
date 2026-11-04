# ADR-0013: Use Cortex.Mediator as the in-process mediator for CQRS

| Status | Accepted — 2026-11-04 · Supersedes [ADR-0002](0002-mediatr-for-cqrs.md) |
| --- | --- |
| Scope | Application-layer dispatch, pipeline behaviors, notification handling |

## Context

ADR-0002 selected MediatR and immediately had to reason around its v13 commercial licensing (free tier for this non-commercial project, 12.x pin as fallback). The stakeholder then directed the project to use **Cortex.Mediator** — the mediator used throughout the internship's reference solution, which pins `Cortex.Mediator` 3.1.2 in every project that dispatches use cases (`Application`, `Api`, `Web`). Staying on the same library as the curriculum keeps samples, patterns and troubleshooting shared between training and this product.

Cortex.Mediator is a lightweight open-source in-process messaging library implementing the Mediator pattern with first-class CQRS support: separate command, query, stream-query and notification pipelines, built-in logging behaviors, an optional FluentValidation behavior package, and open generic pipeline behaviors for custom cross-cutting concerns — with no commercial licensing constraints.

## Decision

Use **Cortex.Mediator 3.1.2** (pinned to the reference solution's version) as the in-process mediator for commands, queries and domain-event notifications.

API mapping from the superseded MediatR design (the CQRS/pipeline architecture itself is unchanged):

| MediatR (ADR-0002) | Cortex.Mediator |
| --- | --- |
| `IRequest<T>` (command or query — indistinguishable) | `ICommand<T>` / `IQuery<T>` — two distinct contracts |
| `IRequestHandler<T, R>` | `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` (void commands: `ICommandHandler<TCommand>`) |
| `ISender.Send(request)` | `IMediator.SendAsync(command)` / `IMediator.QueryAsync(query)` (explicit overloads: `SendCommandAsync<,>` / `SendQueryAsync<,>`) |
| `IPublisher.Publish(evt)` | `IMediator.PublishAsync(notification)` |
| `INotification` / `INotificationHandler<T>` | same names, same semantics |
| `IPipelineBehavior<T, R>` (one shared pipeline) | separate open command / query / notification pipeline behaviors (`AddOpenCommandPipelineBehavior` etc.) |
| `services.AddMediatR(cfg)` | `services.AddCortexMediator(assemblies, options => options.AddDefaultBehaviors())` |

The pipeline concerns from ADR-0002 carry over exactly — Logging (built-in behaviors) → Validation (`Cortex.Mediator.Behaviors.FluentValidation`) → Unit of work (custom command-pipeline behavior) → Performance (custom open behavior). Full details: [CQRS & Mediator](../cqrs-and-mediator.md).

## Consequences

**Positive**

- No licensing question marks — unlike MediatR v13's commercial/free-tier split.
- Curriculum-aligned: the reference solution's registration idiom (`AddCortexMediator(new[] { … }, o => o.AddDefaultBehaviors())`), handler shapes and naming conventions transfer 1:1, so instructor examples work in this codebase.
- Commands and queries are **separate types and separate pipelines** — the unit-of-work behavior applies to commands only without marker-interface tricks, and query handlers cannot accidentally go through transactional machinery.
- Built-in logging behaviors and a ready-made FluentValidation behavior remove hand-rolled plumbing.

**Negative**

- Much smaller community than MediatR: fewer blog posts and Stack Overflow answers; the package README and source are the primary references.
- Most CQRS tutorials show MediatR shapes (`IRequest`, `Send`) — reading them requires the mental mapping table above.
- Pinned to 3.1.2 to match the curriculum; version bumps are deliberate decisions rather than free upgrades.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| MediatR 13 (free tier) / MediatR 12.x pin — ADR-0002 | Workable, but the licensing caveat never disappears, and it diverges from the internship's reference stack that motivated this decision |
| Hand-rolled dispatcher (DI `Func` registry) | Zero dependencies, but re-implements pipeline behaviors, assembly scanning and notification fan-out that Cortex.Mediator already provides |
| Martin Costello's source-generated `Mediator` | Excellent performance and no licensing issues, but again off-curriculum |
| Wolverine | Far more than a mediator (full messaging framework); heavyweight for ~13 in-process use cases |

## References

- [ADR-0002](0002-mediatr-for-cqrs.md) — the superseded decision (history)
- [CQRS & Mediator](../cqrs-and-mediator.md) — pipeline, catalog and event flow on Cortex.Mediator
- [PRD](../../00-product/PRD.md) §7 Technical Blueprint — Mediator row
- [ADR-0011](0011-latest-stable-stack.md) — the modernization that kept MediatR "unchanged" at the time; this ADR refines that list

## Assumptions

- Version 3.1.2 is the pin (matching the reference solution's `PackageReference` entries); later versions are adopted only after a deliberate check.
- The exact registration overloads for open pipeline behaviors and the notifications namespace are confirmed against the package README at M0; deviations update [CQRS & Mediator](../cqrs-and-mediator.md), not this decision.
- Cortex.Mediator's streaming queries (`IStreamQuery`) and query caching (`ICacheableQuery`) are **not** used in the MVP — noted as available, not adopted.
