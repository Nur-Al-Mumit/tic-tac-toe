# Multiplayer Tic-Tac-Toe

A real-time, two-player Tic-Tac-Toe platform with a chess.com-inspired experience: sign up in seconds, see who is online, and play a fast, animated game against another real person.

**Stack:** .NET 10 (LTS) · Clean Architecture + DDD · CQRS with Cortex.Mediator · ASP.NET Core Identity + JWT · EF Core 10 + PostgreSQL 18 · SignalR · Angular 22 (zoneless, signals) · Tailwind CSS 4 · Docker · GitHub Actions. Backend tests: NUnit + Shouldly.

## Documentation

All project documentation lives in [`docs/`](docs/README.md) — start from the [documentation index](docs/README.md).

| Start with | What it gives you |
| --- | --- |
| [Product Requirements Document](docs/00-product/PRD.md) | The source of truth: vision, scope, user stories, requirements, delivery plan. |
| [Documentation index](docs/README.md) | Map of every doc + reading orders per role. |
| [AGENTS.md](docs/agents/AGENTS.md) | Project rules, commands cheat-sheet, and the M0→M6 build order for contributors and coding agents. |
| [Roadmap](docs/00-product/roadmap.md) | Milestones M0–M6 with exit criteria, in dependency order. |

## Repository Layout

```
docs/      Product, architecture, backend, database, frontend, quality, DevOps docs + ADRs
src/       Backend solution — TicTacToe.{Domain,Application,Infrastructure,Api}   (from M0)
tests/     NUnit test projects — Domain, Application, Api integration             (from M0)
client/    Angular 22 SPA                                                        (from M0)
```

## Status

**V1 "Game Live" is implemented** (2026-11-05): register/login with rotating JWT refresh tokens, the rules-complete Game aggregate (all 8 win lines, draw, forfeit, race-safe join via xmin + reload-once), REST game API, live SignalR game events, lobby (create/join/cancel) and the animated game board — exactly the [V1 scope](docs/00-product/v1-scope.md); V1.1 adds presence, live lobby, auto-forfeit and hardening on the same seams.

Verified locally: solution builds with 0 warnings (warnings-as-errors), 68 NUnit tests + 17 vitest specs green. The Testcontainers integration suite (auth lifecycle, full playthrough, simultaneous join race, two-client SignalR) runs in GitHub Actions CI.

```bash
# run everything locally
dotnet build TicTacToe.slnx && dotnet test tests/TicTacToe.Domain.UnitTests && dotnet test tests/TicTacToe.Application.UnitTests
cd client && npm ci && npm test -- --watch=false && npm run build
docker compose up --build   # postgres 18 + api (:8081) + client (:8080)
```
