# ADR-0006: Optimistic concurrency with PostgreSQL xmin

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Concurrent game updates: join races, double moves, one-active-game enforcement |

## Context

The PRD's headline correctness scenario (US-07): two users click Join on the same Waiting game simultaneously — exactly one must win, the other must get a clean "game is full" failure. The same hazard exists for two browser tabs issuing moves for one player. The API is stateless and multi-request by design (load game → mutate aggregate → save), so some concurrency token must detect "the row changed under me" between load and save. PostgreSQL exposes `xmin`, the transaction-id system column that changes on every row write, requiring no schema addition.

## Decision

Use **PostgreSQL's `xmin` as the EF Core concurrency token** on `games` (via `UseXminAsConcurrencyToken()` in the entity configuration) and adopt a **reload-once-and-re-evaluate** policy:

- Command handlers load the `Game` aggregate (tracked), invoke the aggregate method, and save; EF Core emits `UPDATE … WHERE id = @id AND xmin = @loaded`.
- On `DbUpdateConcurrencyException`: reload the aggregate **once** from the database, replay the same domain operation against fresh state, and let the domain rules decide — typically the loser of a join race now sees `Status = InProgress` and receives `GameFullException` → `Result.Failure(Game.Full)` → HTTP 409. If the conflict persists after the single retry, return `Game.ConcurrencyConflict` (409).
- `moves` constraints (`UNIQUE (game_id, position)`, `UNIQUE (game_id, move_number)`) and the partial unique indexes enforcing one active game per player act as a **last line of defense** if a request slips past xmin (e.g. a manual write), converting races into constraint violations mapped to the same errors.
- No pessimistic locks (`SELECT … FOR UPDATE`), no application-managed version column.

Race outcomes under this policy:

| Scenario | First writer | Second writer | Result |
| --- | --- | --- | --- |
| Two joins, same game | commits (O set, InProgress) | xmin mismatch → reload → `Game.Full` | 200 / 409 |
| Two moves, same game (two tabs) | commits move n | reload → correct turn error or valid move n+1 | 200 / 409 or 422 |
| Join + forfeit overlap | commits forfeit | reload → `Game.NotInProgress` | 204 / 409 |
| Create while already in a game | commits | partial unique index violation → `Game.AlreadyInGame` | 201 / 409 |

## Consequences

**Positive**

- Zero schema cost: xmin is maintained by PostgreSQL itself; no version column to keep in sync or expose in DTOs.
- Correct under the stateless API model: safety does not depend on in-process locks, so it holds when the API scales to multiple replicas.
- The loser of any race gets a *domain-meaningful* answer ("full", "not your turn") instead of an opaque conflict, satisfying US-07's UX requirement.
- Testable determinism: the Api integration suite reproduces the two-join race against real PostgreSQL via Testcontainers.

**Negative**

- Retry-once policy must be implemented in handlers (a known, small pattern), adding a branch to every game command.
- xmin is technically an internal system column; relying on it couples concurrency code to PostgreSQL (accepted — the DB choice is itself fixed by ADR-0003).
- Replaying the operation can double domain-event side effects in-memory if the first attempt raised events before failing — prevented by collecting events only on successful save (events are cleared and re-raised on the retried execution).
- Under truly high contention on one row the loser pays one extra load; irrelevant at this product's scale.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Application-managed integer/Guid `Version` column | Works, but duplicates xmin semantics with extra schema and mapping; another value to maintain and test |
| Pessimistic locking (`SELECT … FOR UPDATE` / `Serializable` isolation) | Blocks callers and holds locks across the aggregate-mutation window; multiplies lock-ordering bugs; unnecessary at nine moves per game |
| Unique-constraint-only (no token) | Catches the join race via the partial index but cannot distinguish "cell taken" from "not your turn" with a clean message, and gives poor errors for double-moves from two tabs |
| queue-in-transaction (SERIALIZABLE retry loop) | Heavier reasoning and forced retries even for non-conflicting writes; xmin gives the same guarantee with targeted retries |

## References

- [PRD](../../00-product/PRD.md) (US-07, FR-06/07, §9 join-race risk)
- [Concurrency](../../03-database/concurrency.md) — full mechanism and failure matrix
- [Schema & ERD](../../03-database/schema-and-erd.md) · [Migrations & indexes](../../03-database/migrations-and-indexes.md)
- [System architecture](../system-architecture.md) §4.2 join-race sequence
- [ADR-0003 PostgreSQL/EF Core](0003-postgresql-ef-core-npgsql.md)

## Assumptions

- Commands retry at most once; a second consecutive conflict returns `Game.ConcurrencyConflict` rather than looping.
- xmin is never exposed through the API; clients deduplicate broadcasts by comparing game state, not versions.
