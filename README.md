# Multiplayer Tic-Tac-Toe

A real-time, two-player Tic-Tac-Toe platform with a chess.com-inspired experience: sign up in seconds, see who is online, and play a fast, animated game against another real person.

**Stack:** .NET 10 (LTS) · Clean Architecture + DDD · CQRS with MediatR · ASP.NET Core Identity + JWT · EF Core 10 + PostgreSQL 18 · SignalR · Angular 22 (zoneless, signals) · Tailwind CSS 4 · Docker · GitHub Actions. Backend tests: NUnit.

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

Documentation phase complete (2026-11-04). Implementation follows the [M0→M6 build order](docs/agents/AGENTS.md) — M0 Foundation is next: solution skeleton, Docker, CI, and design tokens.
