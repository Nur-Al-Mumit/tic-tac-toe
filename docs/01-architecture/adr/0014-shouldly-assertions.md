# ADR-0014: Use Shouldly for test assertions

| Status | Accepted — 2026-11-04 · Refines [ADR-0012](0012-nunit-test-framework.md) (NUnit decision unchanged) |
| --- | --- |
| Scope | Assertion library across all .NET test projects |

## Context

ADR-0012 paired NUnit 4 with FluentAssertions, whose v8 line moved to a commercial license (free for non-commercial use, paid otherwise) — the same licensing caveat MediatR carried before [ADR-0013](0013-cortex-mediator.md) removed it. The stakeholder directs that backend tests use **NUnit with Shouldly only**: one assertion library, no licensing question, no mixed assertion styles to arbitrate in review.

## Decision

**Shouldly** (latest stable) is the sole assertion library in every .NET test project. NUnit stays the framework ([ADR-0012](0012-nunit-test-framework.md)); assertions come from Shouldly only — `Assert.That` appears nowhere in the suites.

| Project | Framework | Libraries |
| --- | --- | --- |
| `TicTacToe.Domain.UnitTests` | NUnit 4 | Shouldly |
| `TicTacToe.Application.UnitTests` | NUnit 4 | Shouldly, NSubstitute |
| `TicTacToe.Api.IntegrationTests` | NUnit 4 | Shouldly, NSubstitute, Testcontainers (PostgreSQL 18), Respawn, WebApplicationFactory, Microsoft.AspNetCore.SignalR.Client, NetArchTest |

Syntax conventions used throughout the suites:

| FluentAssertions (ADR-0012) | Shouldly (this ADR) |
| --- | --- |
| `x.Should().Be(y)` | `x.ShouldBe(y)` |
| `x.Should().NotBeNull()` | `x.ShouldNotBeNull()` |
| `x.Should().BeOfType<T>()` | `x.ShouldBeOfType<T>()` |
| `list.Should().Contain(item)` | `list.ShouldContain(item)` |
| `act.Should().ThrowAsync<GameFullException>()` | `await Should.ThrowAsync<GameFullException>(act)` |
| `act.Should().NotThrow()` | `Should.NotThrow(act)` |

## Consequences

**Positive**

- Zero licensing ambiguity — Shouldly is BSD-3-Clause, free for any use.
- Terse, readable assertions with excellent failure output (Shouldly prints actual and expected values in the failure message automatically).
- One assertion style across all three test projects; reviewers never arbitrate between `Assert.That` and `Should()` chains.

**Negative**

- Most tutorials and Stack Overflow answers show FluentAssertions syntax — the mapping table above covers the translation.
- A few niche FluentAssertions conveniences (e.g. specialized event-raising assertions) have no direct Shouldly equivalent; those cases use `Should.NotThrow`/`Should.Throw` wrappers or assert on recorded state instead.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| FluentAssertions v8 (ADR-0012's pairing) | Capable, but the commercial/free-tier split never fully disappears; stakeholder mandated otherwise |
| Plain NUnit `Assert.That` | No extra dependency, but noisier assertions and worse failure output than Shouldly |
| AwesomeAssertions (Apache-2.0 fork of FluentAssertions 7) | License-clean and API-compatible with FA7, but again off the stakeholder mandate |

## References

- [ADR-0012](0012-nunit-test-framework.md) — NUnit decision (stands; supporting cast refined here)
- [Testing strategy](../../05-quality/testing-strategy.md) — tools table and assertion conventions
- [PRD](../../00-product/PRD.md) §7 Technical Blueprint — backend tests row

## Assumptions

- Latest stable Shouldly at project start; minors/patches follow via central package management without a new ADR.
- NetArchTest layering rules assert through their own API and are unaffected by the assertion-library choice.
- Frontend tests are out of scope here — vitest assertions (`expect`) remain the standard on the Angular side.
