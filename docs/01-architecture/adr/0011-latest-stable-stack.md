# ADR-0011: Target the latest stable stack (.NET 10, EF Core 10, PostgreSQL 18, Angular 22, built-in OpenAPI + Scalar)

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Runtime, database, ORM, frontend framework major versions; API documentation tooling |

## Context

The original "Multiplayer Tic-Tac-Toe PRD & Technical Documentation" §6.2 pinned .NET 8 / EF Core 8 / PostgreSQL 16 / Angular 17+ / Tailwind with a config file / xUnit / Swagger (Swashbuckle implied). On 2026-11-04 the stakeholder directed the project to the **latest stable stack**: the internship's purpose is learning current, industry-standard tooling, the product is greenfield (no migration debt), and the delivery window (≈23 working days) starts after these majors are stable. The PRD's [Stack modernization note](../../00-product/PRD.md#7-technical-blueprint-summary) supersedes the original §6.2 table; this ADR records that decision. Following the decision, the original document itself was updated (v1.1, same day) to mirror this stack and then archived at [00-product/archive](../../00-product/archive/original-technical-documentation.md); this ADR preserves the history of the change.

## Decision

Adopt and pin the modernized stack:

| Layer | Was (original §6.2) | Now |
| --- | --- | --- |
| Runtime | .NET 8 (LTS) | **.NET 10 (LTS), C# 14** |
| ORM | EF Core 8 | **EF Core 10** (Npgsql) |
| Database | PostgreSQL 16 | **PostgreSQL 18** |
| Frontend | Angular 17+ | **Angular 22** (standalone, signals, zoneless) |
| Styling | Tailwind CSS + `tailwind.config.js` | **Tailwind CSS 4** (CSS-first `@theme`, no config JS) |
| API docs | Swagger UI | **.NET built-in OpenAPI (Microsoft.AspNetCore.OpenApi) + Scalar UI** |
| Tests | xUnit | **NUnit** (see ADR-0012) |

Unchanged: Clean Architecture/DDD/CQRS shape, MediatR, FluentValidation, Mapster, Identity+JWT design, SignalR contracts, REST API, database schema and domain design — the modernization is a version bump, not a redesign. Version pins live in the `Directory.Packages.props` central package management file and `package.json`/compose image tags.

## Consequences

**Positive**

- Longest runway before the next required upgrade (.NET 10 LTS support window outlasts the project's life by years).
- Learning value: the internship trains on the tooling the industry is moving to (zoneless Angular, CSS-first Tailwind, built-in OpenAPI) rather than versions already two majors old.
- Built-in OpenAPI removes the Swashbuckle dependency that historically lagged .NET releases; Scalar UI gives a modern, auth-aware docs experience with zero extra packages.
- EF Core 10 + PostgreSQL 18 performance/tooling improvements arrive for free (no code changes required by the schema).

**Negative**

- Occasionally thinner community answer base for the newest majors (Angular 22 zoneless, Tailwind 4 `@theme`) — official docs become the primary source and debugging may take longer.
- Third-party packages must be checked for .NET 10 / Angular 22 compatibility at M0; a straggler forces a pin or a workaround.
- PostgreSQL 18 images/tooling (backups, pgAdmin parity) are newer; CI (Testcontainers) validates the exact image so drift is caught early.
- Any tutorial or training material predating the modernization disagrees with the repo — PRD + these docs are canonical (stated explicitly in the PRD's assumptions).

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Keep the original §6.2 versions (.NET 8/EF8/PG16/NG17) | Maximum tutorial compatibility, but starts the project two majors behind with an upgrade looming inside the support horizon; contradicts the stakeholder's latest-stable direction |
| .NET 9 (current-but-not-LTS at modernization time) | Non-LTS runway shorter than the project's expected life; .NET 10 gives LTS with equal modernity |
| Mixed modernization (backend latest, frontend conservative) | Two half-modern stacks: the worst of both for learning and consistency; zoneless Angular was specifically part of the modernization intent |
| Await further patch releases before pinning | Greenfield project with no users — no reason to delay; patch updates flow through the central package pin anyway |

## References

- [PRD](../../00-product/PRD.md) — §7 Technical Blueprint (canonical stack table + modernization note; supersedes original §6.2)
- [System architecture](../system-architecture.md) §3 technology stack table
- [ADR-0003 PostgreSQL/EF Core](0003-postgresql-ef-core-npgsql.md) · [ADR-0007 Angular 22 / Tailwind 4](0007-angular-signals-zoneless-tailwind.md) · [ADR-0012 NUnit](0012-nunit-test-framework.md)
- [Docker & environments](../../06-devops/docker-and-environments.md) — image tags pinning these versions

## Assumptions

- "Latest stable" means the latest stable major with general-availability releases as of 2026-11-04, pinned at project start; minors/patches follow via central package management without a new ADR.
- The PRD remains the single source of truth where it and the original document disagree — including this version table (the original's v1.1, now archived, mirrors the PRD stack, so disagreements should no longer occur).
