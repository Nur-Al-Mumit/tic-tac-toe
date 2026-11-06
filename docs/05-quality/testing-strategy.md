# Testing Strategy

*How the Multiplayer Tic-Tac-Toe backend is verified: one NUnit + Shouldly unit suite in `test/TicTacToe.Application.UnitTests` covering domain rules, handlers, validators and behaviors — fast, containerless, and run on every push (ADR-0015).*

Parent document: [PRD](../00-product/PRD.md) · Related: [CQRS & Mediator](../01-architecture/cqrs-and-mediator.md) · [Security](security.md) · Decision records: [ADR-0012](../01-architecture/adr/0012-nunit-test-framework.md) · [ADR-0014](../01-architecture/adr/0014-shouldly-assertions.md) · [ADR-0015](../01-architecture/adr/0015-vertical-slice-structure-and-unit-only-tests.md)

---

## 1. Principles

1. **One project, one runner, one assertion library**: `test/TicTacToe.Application.UnitTests`, NUnit 4, Shouldly — `Assert.That` appears nowhere (ADR-0012, ADR-0014).
2. **The suite is pure unit**: no database, no network, no Docker — it runs on any machine with the .NET SDK, and in CI in seconds.
3. **Domain rules are the crown jewels**: the `Game` aggregate's full rule set is exercised exhaustively (all 8 win lines, draw, forfeit, cancel, immutability, event raising).
4. **Handlers are tested through their seams**: repositories and services are NSubstitute substitutes; expected failures are `Result` errors, never exceptions (ADR-0009).
5. **Tests follow the feature folders**: a handler's test sits in `Features/<Feature>/Commands|Queries/` next to the concept it verifies.

## 2. Tools

| Tool | Role |
| --- | --- |
| **NUnit 4** | Framework: `[Test]`, `[TestCase]`, `[SetUp]` |
| **Shouldly** | Sole assertion library: `ShouldBe`, `ShouldThrow`, `ShouldContain` |
| **NSubstitute** | Substitutes for `IApplicationUnitOfWork`, repositories, services; `.Received()` / `.DidNotReceive()` verification |
| **FluentValidation.TestHelper** | Validator tests: `TestValidate`, `ShouldHaveValidationErrorFor` |

## 3. Layout (follows `src/TicTacToe.Application/Features`)

```
test/TicTacToe.Application.UnitTests/
  Common/ResultTests.cs                                  # Result<T> + Error codes
  Features/
    Auth/Commands/UserAuthCommandHandlerTests.cs         # register/login/refresh/logout handlers
    Auth/Commands/UserAuthCommandValidatorTests.cs       # FR-01 rules (username regex, password policy)
    Games/GameTests.cs                                   # the aggregate rule book (30 tests)
    Games/Commands/GameCommandHandlerTests.cs            # create/join/move/cancel/leave handlers
```

Naming: `<Name>CommandHandlerTests`, `<Thing>Tests` for domain logic.

## 4. Domain rule tests (`Features/Games/GameTests.cs`)

The aggregate is exercised directly — no mocks, real domain objects:

| Area | Covered |
| --- | --- |
| Create | Host becomes X, status Waiting, `GameCreated` raised; empty-host guard |
| Join | O added + `GameStarted`; own-game, already-started (full), finished-game rejections |
| Move guards | X-first, strict alternation, position 0–8 bounds, occupied cell, only while InProgress |
| Win detection | **All 8 winning lines** (`[TestCase]` per line), O-wins path, winning-line payload |
| Draw | Verified draw board: 9 moves, no winner, `WinningLine == null` |
| Finish | Result set exactly once; finished game immutable (move/forfeit/join/cancel all throw) |
| Forfeit | X forfeits → OWins/Forfeit; O forfeits → XWins/Forfeit; `WinnerPlayerId` correct |
| Cancel | Host + Waiting only; `GameCancelled` raised |
| Events | One `MoveMade` per move; exactly one `GameFinished` |

## 5. Handler tests (`Features/*/Commands`)

Per-test substitutes (fresh state, no cross-test coupling):

| Test | Asserts |
| --- | --- |
| `GameCreate_WhenPlayerAlreadyActive_ReturnsGamesBusy` | `Games.Busy` failure; `AddAsync` never called |
| `GameCreate_NewGame_IsWaitingWithHostAsX` | DTO shape (status/players/board) + `SaveAsync` committed once |
| `GameJoin_FullGame_ReturnsGamesFull` / `…OwnGame…` | Correct error codes; game untouched |
| `GameJoin_WaitingGame_StartsGame` | InProgress DTO, O resolved from profiles, single `SaveAsync` |
| `GameMove_NotYourTurn_ReturnsGamesNotYourTurn` | Domain exception → mapped `Result` error |
| `GameMove_NonParticipant_ReturnsGamesForbidden` | Authorization seam before the aggregate |
| `GameMove_ValidMove_UpdatesBoardAndTurn` | Board derivation + turn flip in the DTO |
| `GameCancel_ByNonHost_ReturnsGamesNotHost` | 403-mapped failure |
| `GameLeave_ByNonParticipant_ReturnsGamesForbidden` | Explicit-player-id forfeit guard |
| `UserRegister_DuplicateUsername…` / `…NewUser_IssuesTokens` | Identity failure passthrough; token issuance on success |
| `UserLogin_InvalidCredentials…` / `UserRefreshToken…` / `UserLogout_RevokesOwnToken…` | Auth error mapping; revoke called only for the owner |

**Concurrency note:** the reload-once race policy (detach → reload → re-evaluate, ADR-0006) is part of the handler design; its safety rests on the xmin token plus the partial unique indexes ([concurrency](../03-database/concurrency.md)), exercised live in the running app rather than by a containerized test.

## 6. Validators

`UserRegisterCommandValidator` (username `^[A-Za-z0-9_]{3,20}$`, password upper/lower/digit/8+, email shape) via FluentValidation.TestHelper; `GameMoveCommandValidator` bounds the position 0–8 (request shape → 400, per [api-spec §validation](../02-backend/api-spec.md)).

## 7. CI

`.github/workflows/ci.yml` — backend job: restore → build (warnings as errors) → `dotnet test test/TicTacToe.Application.UnitTests`; frontend job: vitest + build (unchanged). No Docker required for either job's test step.

## 8. Frontend (client)

vitest specs cover the auth core (service, single-flight interceptor, guards), the route shell, and the board component's playable-cell logic — run with `npm test -- --watch=false` in `client/`.

## 9. Definition of Done

- Code reviewed and merged via pull request.
- `dotnet build TicTacToe.slnx` clean (warnings as errors) and the unit suite green.
- New use cases ship with: command + handler + validator **and their tests** in the matching feature folder.
- API changes reflected in the OpenAPI document; error codes added to the [api-spec registry](../02-backend/api-spec.md#14-error-code-catalog) in the same change.
- Structured logs for new paths; no secrets, passwords or tokens logged.

## Assumptions

- The integration/Testcontainers suite was removed by stakeholder direction (ADR-0015); an executing race/hub test layer may return post-V1 as a new ADR.
- Coverage percentage is monitored but the gate is the DoD checklist; the domain suite is intentionally exhaustive rather than coverage-driven.
