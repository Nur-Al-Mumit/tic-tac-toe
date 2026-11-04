# ADR-0003: PostgreSQL 18 with EF Core 10 and Npgsql

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Database engine, ORM, provider, data access conventions |

## Context

The platform needs a relational store for Identity users, player profiles, rotating refresh tokens, games, and an append-only moves log. Hard requirements: transactional consistency for join/move races (exactly one winner), a row-version mechanism for optimistic concurrency, partial unique indexes to enforce one active game per player at the storage level, and array support for the winning line. The original document pinned PostgreSQL 16 / EF Core 8; the 2026-11-04 stack modernization (ADR-0011) moves to latest stable.

## Decision

Use **PostgreSQL 18** accessed through **EF Core 10 with the Npgsql provider**:

- `AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` with DbSets for `Game`, `PlayerProfile`, `RefreshToken` (moves are an owned/child collection of `Game`).
- Fluent configurations per entity; snake_case table/column naming via `UseSnakeCaseNamingConvention`.
- **xmin** system column mapped as the concurrency token (see ADR-0006); `moves` gets `UNIQUE (game_id, position)` and `UNIQUE (game_id, move_number)`; partial unique indexes enforce one active game per player; `result_winning_line` is `smallint[]`.
- `smallint` backing for enums (`status`, `current_turn`, `result_outcome`, `result_reason`, `mark`).
- Migrations live in Infrastructure, applied on startup in Development and as a CI/CD step elsewhere; integration tests run against real PostgreSQL 18 via Testcontainers and reset with Respawn.
- Query side goes through `IReadDbContext` with `AsNoTracking` projections; repositories expose no `IQueryable`.

Domain-to-storage mapping that this decision fixes:

| Domain concept | PostgreSQL construct |
| --- | --- |
| `Game` aggregate | `games` row (identity + status + result columns) |
| `Move` child entity | `moves` row, owned collection, `ON DELETE CASCADE` |
| `GameResult` value object | owned columns `result_outcome`, `result_reason`, `result_winning_line smallint[]` |
| Aggregate version | `xmin` system column (ADR-0006) |
| One-active-game rule | partial unique indexes on `player_x_id` / `player_o_id` |

## Consequences

**Positive**

- One free, battle-tested engine covers relational integrity, arrays, and partial indexes — all three race-safety mechanisms (domain guards, xmin, partial unique index) live in one database.
- EF Core 10 on .NET 10 keeps a single LTS toolchain; LINQ projections generate exactly the SQL the lobby needs, backed by `games(status, created_at_utc DESC)`.
- Testcontainers gives integration tests the same engine version as production, eliminating provider-specific surprises.
- Owned collections and owned `Result` type map the aggregate faithfully; the domain stays persistence-ignorant.

**Negative**

- EF Core's change tracker is overhead on the read path — accepted only because queries bypass it via `AsNoTracking` projections.
- Npgsql version-sensitive behavior (enums, arrays) adds a dependency to keep aligned with EF Core 10 minor updates.
- Relational mapping forces some aggregate compromises (owned collection cannot be queried without the owner), acceptable because moves are always accessed via their game.
- Developer machines need Docker (or a local PostgreSQL 18) to run integration tests.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| SQL Server + EF Core | Requires licensing for production-scale later; lacks partial unique indexes, which are a named mitigation for the one-active-game race; array type would become JSON |
| SQLite | No partial-index/concurrency parity with the production target; file-based semantics undermine the race-condition test matrix |
| PostgreSQL + Dapper (micro-ORM) | More SQL control, but hand-mapping the aggregate and its child collection multiplies maintenance; EF configuration centralizes snake_case and indexes declaratively |
| MongoDB (document store, game-as-document) | Single-document game updates are attractive, but Identity, partial unique constraints and the moves audit log pull strongly relational; team expertise is EF-first |

## References

- [PRD](../../00-product/PRD.md) (§7 stack table, §9 join-race mitigation)
- [Schema & ERD](../../03-database/schema-and-erd.md) · [Migrations & indexes](../../03-database/migrations-and-indexes.md) · [Concurrency](../../03-database/concurrency.md)
- [ADR-0006 Optimistic concurrency with xmin](0006-optimistic-concurrency-xmin.md)
- [ADR-0011 Latest stable stack](0011-latest-stable-stack.md) (supersedes original §6.2 PostgreSQL 16 / EF Core 8)

## Assumptions

- `result_winning_line` uses a nullable `smallint[]` (three cell indexes) rather than a join table; winning lines are never queried by value.
- Enum-to-`smallint` mapping (not Postgres native enums) keeps migrations reversible and tooling-friendly.
