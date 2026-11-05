# Multiplayer Tic-Tac-Toe — Documentation Index

*The map of every document in this repository: what each file covers, the recommended reading orders per role, and the conventions all docs follow. Start here, then follow a reading order below.*

**Source of truth:** the [PRD](00-product/PRD.md). If any doc disagrees with it, the PRD wins; fixes go into the doc or, for genuine decisions, a new [ADR](01-architecture/adr/). The PRD carries the 2026-11-04 stack modernization (.NET 10, EF Core 10, PostgreSQL 18, Angular 22 zoneless, Tailwind CSS 4, built-in OpenAPI + Scalar, NUnit for backend tests — recorded in [ADR-0011](01-architecture/adr/0011-latest-stable-stack.md) and [ADR-0012](01-architecture/adr/0012-nunit-test-framework.md)); the originating technical document — now archived at [00-product/archive](00-product/archive/original-technical-documentation.md) — was aligned to this stack in its v1.1 update.

Working in the code? Read [agents/AGENTS.md](agents/AGENTS.md) first — it carries the project rules, commands cheat-sheet, and the M0→M6 build order.

## Document Map

| Section | Document | What it covers |
| --- | --- | --- |
| **00-product** | [PRD](00-product/PRD.md) | Vision, scope, personas, user stories, functional/non-functional requirements, flows, delivery plan — the source of truth. |
| | [v1-scope](00-product/v1-scope.md) | The first build's boundary — "Game Live": auth + live game in; presence, live lobby, auto-forfeit, hardening out; FR/US and milestone mappings. |
| | [user-stories](00-product/user-stories.md) | US-01..US-13 expanded with Gherkin acceptance criteria, milestone + FR traceability matrix. |
| | [roadmap](00-product/roadmap.md) | Milestones M0–M6: scope by area, exit criteria, dependencies, non-goals guard, post-MVP outlook — no calendar schedule. |
| | [archive/original-technical-documentation](00-product/archive/original-technical-documentation.md) | The frozen v1.1 originating document (single-file overview); [PRD.md](00-product/PRD.md) supersedes it. |
| **01-architecture** | [system-architecture](01-architecture/system-architecture.md) | C4 context/container views, runtime sequences (move, join race, forfeit/reconnect), stack, cross-cutting concerns, scalability. |
| | [clean-architecture-ddd](01-architecture/clean-architecture-ddd.md) | Dependency rule, file-level folder spec per project, DDD tactical mapping, invariants, domain events, NetArchTest enforcement. |
| | [cqrs-and-mediator](01-architecture/cqrs-and-mediator.md) | Command/query catalog, pipeline behaviors (Logging → Validation → UnitOfWork → Performance), `Result<T>`, event→broadcast flow. |
| | [adr/](01-architecture/adr/0001-clean-architecture-and-ddd.md) | 14 decision records (ADRs 0001–0014) with context, alternatives, and consequences. |
| **02-backend** | [api-spec](02-backend/api-spec.md) | Full REST contract: per-endpoint reference, DTO schemas, canonical error-code registry, OpenAPI/Scalar, versioning. |
| | [realtime-signalr](02-backend/realtime-signalr.md) | Hub design: groups, event catalog with payloads, lifecycle sequences, grace/forfeit timers, reconnect/resync, scale-out path. |
| | [auth-identity-jwt](02-backend/auth-identity-jwt.md) | Identity setup, JWT claims/validation, refresh rotation with reuse detection, lockout, token storage strategy. |
| | [coding-standards](02-backend/coding-standards.md) | C# 14 conventions, layer rules, error-handling contract, Cortex.Mediator/EF Core rules, analyzers, PR checklist. |
| **03-database** | [schema-and-erd](03-database/schema-and-erd.md) | ERD, per-table column specs, enum mappings, constraint catalog, EF Core mapping notes. |
| | [migrations-and-indexes](03-database/migrations-and-indexes.md) | Migration workflow per environment, index catalog with DDL and the queries each index serves, retention. |
| | [concurrency](03-database/concurrency.md) | xmin optimistic concurrency, reload-once pattern, join-race and forfeit-race sequences, unique constraints as last line of defense. |
| **04-frontend** | [ux-ui-design-system](04-frontend/ux-ui-design-system.md) | Tailwind 4 CSS-first tokens (`@theme`), typography, spacing, 13 component specs with state matrices, WCAG AA contrast. |
| | [pages-and-flows](04-frontend/pages-and-flows.md) | Desktop/mobile wireframes for all pages, element specs, user-journey flows, state matrix, responsive and a11y annotations. |
| | [animations](04-frontend/animations.md) | Motion tokens and 15 animation specs (mark draw, win line, shake, burst…), keyframes, `prefers-reduced-motion` fallbacks. |
| | [angular-architecture](04-frontend/angular-architecture.md) | Folder structure, zoneless/signals playbook, routing/guards, auth interceptor with single-flight refresh, SignalR service, stores, testing. |
| **05-quality** | [testing-strategy](05-quality/testing-strategy.md) | Test pyramid and targets, NUnit test catalogs per suite (incl. join-race and forfeit scenarios), isolation, CI gates, DoD. |
| | [security](05-quality/security.md) | STRIDE threat model, trust-boundary diagram, expanded controls (tokens, CORS, headers, rate limits), OWASP mapping, release checklist. |
| **06-devops** | [docker-and-environments](06-devops/docker-and-environments.md) | Compose spec, api/client Dockerfiles, environment matrix, config & secrets, migrations per env, health, logging, cheat-sheet. |
| | [ci-cd](06-devops/ci-cd.md) | Trunk-based flow, GitHub Actions CI jobs, coverage gates, CD rollout order (migrate → api → client), rollback, secrets, versioning. |
| **agents** | [AGENTS](agents/AGENTS.md) | Operating manual for coding agents: golden rules, conventions, commands, M0→M6 build order with done-when checklists. |
| | [prompts/](agents/prompts/product-manager.md) | One role prompt per discipline (7 files): mission, ownership, must-reads, guardrails, definition of done. |

## Reading Orders

### New contributor (top to bottom, ~2 hours)

1. [PRD](00-product/PRD.md) — what we are building and why.
2. [system-architecture](01-architecture/system-architecture.md) — how the pieces fit at runtime.
3. [clean-architecture-ddd](01-architecture/clean-architecture-ddd.md) — where code lives and the dependency rule.
4. [api-spec](02-backend/api-spec.md) + [realtime-signalr](02-backend/realtime-signalr.md) — the contracts the SPA talks to.
5. [schema-and-erd](03-database/schema-and-erd.md) — the data underneath.
6. [angular-architecture](04-frontend/angular-architecture.md) — the frontend structure.
7. [AGENTS.md](agents/AGENTS.md) — the rules of engagement before your first commit.

### By role (read your lane first, then the contracts)

| Role | Read first | Then |
| --- | --- | --- |
| Product | [PRD](00-product/PRD.md), [user-stories](00-product/user-stories.md), [roadmap](00-product/roadmap.md) | [pages-and-flows](04-frontend/pages-and-flows.md) |
| Backend | [clean-architecture-ddd](01-architecture/clean-architecture-ddd.md), [cqrs-and-mediator](01-architecture/cqrs-and-mediator.md), [api-spec](02-backend/api-spec.md), [coding-standards](02-backend/coding-standards.md) | [realtime-signalr](02-backend/realtime-signalr.md), [auth-identity-jwt](02-backend/auth-identity-jwt.md), [concurrency](03-database/concurrency.md) |
| Database | [schema-and-erd](03-database/schema-and-erd.md), [migrations-and-indexes](03-database/migrations-and-indexes.md), [concurrency](03-database/concurrency.md) | [ADR-0006](01-architecture/adr/0006-optimistic-concurrency-xmin.md), [ADR-0003](01-architecture/adr/0003-postgresql-ef-core-npgsql.md) |
| UI/UX | [ux-ui-design-system](04-frontend/ux-ui-design-system.md), [pages-and-flows](04-frontend/pages-and-flows.md), [animations](04-frontend/animations.md) | [PRD §6](00-product/PRD.md#6-experience-summary), [user-stories](00-product/user-stories.md) |
| Angular | [angular-architecture](04-frontend/angular-architecture.md), [ux-ui-design-system](04-frontend/ux-ui-design-system.md) | [api-spec](02-backend/api-spec.md), [realtime-signalr](02-backend/realtime-signalr.md) |
| QA / Security | [testing-strategy](05-quality/testing-strategy.md), [security](05-quality/security.md) | [user-stories](00-product/user-stories.md) (acceptance criteria), [api-spec §1.4](02-backend/api-spec.md#14-error-code-catalog) |
| DevOps | [docker-and-environments](06-devops/docker-and-environments.md), [ci-cd](06-devops/ci-cd.md) | [system-architecture](01-architecture/system-architecture.md), [ADR-0008](01-architecture/adr/0008-in-memory-presence-tracker.md) |

### By milestone

Each milestone's docs-to-reread list lives in [AGENTS.md build order](agents/AGENTS.md) and in [roadmap](00-product/roadmap.md).

## Conventions

- Filenames are kebab-case; every doc opens with `# Title` + an italic one-line purpose and ends with `## Assumptions` (assumptions beyond the PRD made while writing it).
- Diagrams are Mermaid; enumerable facts are tables.
- Cross-links are relative Markdown links (this index is the hub; ADRs link their related docs).
- Decisions that change an ADR get a new numbered ADR — existing ADRs are never edited retroactively (status may change to Superseded with a pointer).
- No application code in docs — only illustrative snippets (signatures, config, DDL, YAML, keyframes) where the section demands them.

## Assumptions

- This index describes the docs tree as generated on 2026-11-04; if files are added later, update the table and the relevant reading order in the same change (per the ADR/AGENTS conventions).
- The originating document is archived (frozen at v1.1, aligned to the modern stack) at [00-product/archive/original-technical-documentation.md](00-product/archive/original-technical-documentation.md); [docs/00-product/PRD.md](00-product/PRD.md) is the working source of truth and the only PRD new work should cite.
