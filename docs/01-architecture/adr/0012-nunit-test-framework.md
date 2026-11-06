# ADR-0012: NUnit as the backend test framework

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Amendment | Assertions: superseded by [ADR-0014](0014-shouldly-assertions.md). Test scope and layout: superseded by [ADR-0015](0015-vertical-slice-structure-and-unit-only-tests.md) — one `Application.UnitTests` project, no integration project |
| Scope | .NET test framework and the supporting test libraries |

## Context

The PRD demands 80%+ unit coverage on Domain and Application, integration tests over real PostgreSQL, and hub tests — a substantial suite. The .NET ecosystem offers three mainstream frameworks (xUnit, NUnit, MSTest); the original technical document §6.2 named xUnit, but the stakeholder instruction for this project mandates **NUnit** (recorded in the PRD's assumptions). The framework choice must also pair with the supporting cast: FluentAssertions (assertions), NSubstitute (mocking), Testcontainers (real PostgreSQL), Respawn (test data reset), and NetArchTest (layering rules).

## Decision

Use **NUnit (NUnit 4.x)** as the mandated test framework across all three backend test projects, with the supporting libraries:

| Project | Framework | Libraries |
| --- | --- | --- |
| `TicTacToe.Domain.UnitTests` | NUnit 4 | FluentAssertions |
| `TicTacToe.Application.UnitTests` | NUnit 4 | FluentAssertions, NSubstitute |
| `TicTacToe.Api.IntegrationTests` | NUnit 4 | FluentAssertions, NSubstitute, Testcontainers (PostgreSQL 18), Respawn, WebApplicationFactory, Microsoft.AspNetCore.SignalR.Client, NetArchTest |

Conventions adopted with it:

- NUnit 4 syntax (`Assert.That`, constraint model) combined with FluentAssertions' `Should()` for readable domain assertions.
- One fixture per aggregate/use case (`GameMakeMoveTests`, `JoinGameHandlerTests`); `[TestCase]`/`[TestCaseSource]` for the 8 win lines × 2 marks, draw, all six domain exceptions, and the position boundary table.
- Integration tests share one PostgreSQL 18 Testcontainer per fixture run with Respawn resetting between tests; architecture (NetArchTest) rules run inside the integration project.
- Async tests use NUnit's native `async Task` support; no `[SetUp]` surprises via explicit, minimal setup methods.

## Consequences

**Positive**

- Stakeholder alignment: the mandated framework is used, full stop — no ambiguity for graders/reviewers comparing the repo to the instruction.
- NUnit's parameterized tests (`TestCaseSource`) express the win-line/board matrix compactly — one method covers 16+ scenarios, which is exactly the shape of this domain's test surface.
- NUnit 4 is modern (nullable-aware, `Assert.That`-only) and pairs cleanly with FluentAssertions and NSubstitute; `[Retry]`/`[Timeout]` attributes help with Testcontainers startup flakiness.
- Isolated per-test lifecycle (`[SetUp]`/new instance per test) suits handler tests that want pristine substitutes every time.

**Negative**

- Deviation from the original document §6.2 (xUnit) and from the .NET community's current default — every copied snippet from external docs needs a small mental translation.
- NUnit's test-per-instance model instantiates the fixture class per test — negligible here, but a habit to remember for expensive shared state (which belongs in `OneTimeSetUp` or the Testcontainer fixture).
- Parallelism defaults differ from xUnit's; collections must be explicitly non-parallel where the shared database demands it.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| xUnit (community default, original document's §6.2) | Technically equal for this codebase, but the stakeholder explicitly mandated NUnit; instruction compliance outweighs ecosystem habit |
| MSTest | Fully supported and modernized, but least used in modern Clean Architecture samples; no advantage over NUnit here |
| TUnit (next-gen source-generated) | Interesting performance/AOT story but young; wrong risk profile for an internship deliverable with a fixed timeline |

## References

- [PRD](../../00-product/PRD.md) (§7 stack table — "NUnit (mandated)"; Assumptions — stakeholder instruction)
- [Testing strategy](../../05-quality/testing-strategy.md) — test pyramid, coverage targets, Testcontainers/Respawn usage
- [CI/CD](../../06-devops/ci-cd.md) — running the suite in GitHub Actions
- [ADR-0001 Clean Architecture](0001-clean-architecture-and-ddd.md) — NetArchTest rules riding in the integration project
- [ADR-0011 Latest stable stack](0011-latest-stable-stack.md) — the umbrella modernization this mandate rode in on

## Assumptions

- NUnit 4.x (not 3.x) is the target major; NUnit 4's breaking changes from 3.x (namespace moves, classic asserts removed) are absorbed now at zero tests written.
- FluentAssertions' v8 licensing (paid for commercial use, free for non-commercial) fits this non-commercial internship project; the v7 line remains the fallback under the same reasoning as MediatR in ADR-0002.
