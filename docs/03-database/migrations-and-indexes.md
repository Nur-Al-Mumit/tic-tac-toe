# Migrations & Indexes

*How the PostgreSQL 18 schema is produced and evolved with EF Core 10 migrations, the full index catalog, the queries each index serves, and maintenance and retention policy.*

## 1. Migrations Workflow

### 1.1 Tooling and commands

Migrations are managed with the `dotnet ef` CLI tool at the repository root `/home/nur/Documents/Dev Skill/InternShip/Projects/tic-tac-toe`. `AppDbContext` lives in `TicTacToe.Infrastructure`, and `TicTacToe.Api` is the startup project (it hosts the DI composition), so every command passes both:

```bash
# One-time / upgrade: install the tool matching EF Core 10
dotnet tool install --global dotnet-ef --version 10.*

# Create a migration
dotnet ef migrations add InitialCreate \
  --project src/TicTacToe.Infrastructure \
  --startup-project src/TicTacToe.Api \
  --output-dir Persistence/Migrations

# Apply pending migrations to the database (local development shortcut)
dotnet ef database update \
  --project src/TicTacToe.Infrastructure \
  --startup-project src/TicTacToe.Api

# Generate a production-ready script (reviewed artifact for CI/CD)
dotnet ef migrations script --idempotent \
  --project src/TicTacToe.Infrastructure \
  --startup-project src/TicTacToe.Api \
  --output artifacts/migrations.sql

# Verify state
dotnet ef migrations list \
  --project src/TicTacToe.Infrastructure \
  --startup-project src/TicTacToe.Api

dotnet ef migrations has-pending-model-changes \
  --project src/TicTacToe.Infrastructure \
  --startup-project src/TicTacToe.Api
```

`migrations has-pending-model-changes` (run in CI) fails when the model and the last migration diverge, so a colleague cannot forget to commit one.

### 1.2 Where migrations live and why

Migrations, the model snapshot and theDbContext design-time factory live in `src/TicTacToe.Infrastructure/Persistence/`:

- `AppDbContext`, entity configurations and `DesignTimeDbContextFactory` are all in Infrastructure, which is the innermost layer that knows EF Core exists; keeping migrations beside them means `dotnet ef` needs no reference from Application or Domain.
- `DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>` reads the connection string from the `ConnectionStrings__Default` environment variable (falling back to the docker-compose localhost default) so the CLI works without booting the API host.
- `TicTacToe.Api` is passed as `--startup-project` only so the tool can resolve the DI graph; it contains no migration code.

### 1.3 Initial migration contents (`InitialCreate`)

| Operation | Objects |
| --- | --- |
| `CreateTable` | `users`, `asp_net_roles`, `asp_net_role_claims`, `asp_net_user_claims`, `asp_net_user_logins`, `asp_net_user_roles`, `asp_net_user_tokens`, `player_profiles`, `refresh_tokens`, `games`, `moves` |
| `CreateIndex` (unique) | `asp_net_user_names` (`users.normalized_user_name`), `uq_player_profiles_username`, `uq_refresh_tokens_token_hash`, `uq_games_player_x_id_active` (partial), `uq_games_player_o_id_active` (partial), `uq_moves_game_id_position`, `uq_moves_game_id_move_number` |
| `CreateIndex` | `asp_net_users_email_index`, `ix_refresh_tokens_user_id`, `ix_games_status_created_at_utc` (with `created_at_utc DESC`) |
| `AddCheckConstraint` | `ck_moves_position_range` (`position >= 0 AND position <= 8`) |
| `AddForeignKey` | profile/token/game/move FKs with the delete behaviors in the [schema constraint catalog](schema-and-erd.md#6-constraint-catalog) |

The partial unique indexes are **not** hand-written SQL: EF renders `HasFilter(...)` predicates natively, so `CreateIndex` emits e.g. `CREATE UNIQUE INDEX uq_games_player_x_id_active ON games (player_x_id) WHERE status IN (0, 1);` and the model snapshot round-trips the filter. `Down()` reverts everything in reverse order; the `__EFMigrationsHistory` table tracks applied versions.

## 2. Applying Migrations per Environment

| Environment | Mechanism | Why |
| --- | --- | --- |
| **Development** | `Database.MigrateAsync()` on startup, gated by `app.Environment.IsDevelopment()` (Program.cs) | Zero-friction local runs; applies pending migrations transactionally, one migration per transaction; never `EnsureCreated()` — that would create an untracked schema and break migrations |
| **Staging / Production** | CI/CD step: generate `--idempotent` script, review, apply with `psql` (or `dotnet ef database update --connection ...`) | The app's runtime DB user holds no DDL privileges (least privilege); a reviewed script is an auditable gate; the idempotent wrapper (`IF NOT EXISTS ... __EFMigrationsHistory`) makes re-runs safe after a partial deploy |

```csharp
// Program.cs — Development only
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>()
               .Database.MigrateAsync();
}
```

```yaml
# CI/CD sketch — full pipeline in 06-devops/ci-cd.md
- name: Generate migration script
  run: >
    dotnet ef migrations script --idempotent
    --project src/TicTacToe.Infrastructure
    --startup-project src/TicTacToe.Api
    --output artifacts/migrations.sql

- name: Apply migrations (Staging)
  run: psql "$DATABASE_URL" --set=ON_ERROR_STOP=1 -f artifacts/migrations.sql
  env:
    DATABASE_URL: ${{ secrets.STAGING_DATABASE_URL }}
```

**Tests:** integration fixtures start a PostgreSQL 18 container (Testcontainers) and apply migrations with the same `MigrateAsync()` path, so tests exercise the real schema; Respawn resets **data only** between tests, leaving schema and `__EFMigrationsHistory` intact (see [testing strategy](../05-quality/testing-strategy.md)).

## 3. Index Catalog

| Index | Table | Definition | Purpose / query served |
| --- | --- | --- | --- |
| `pk_games` | games | `PRIMARY KEY (id)` | Row identity; lookup for game detail and the xmin-guarded `UPDATE ... WHERE id = @id AND xmin = @v` |
| `ix_games_status_created_at_utc` | games | `INDEX (status, created_at_utc DESC)` | Lobby list — newest first within status |
| `uq_games_player_x_id_active` | games | `UNIQUE INDEX (player_x_id) WHERE status IN (0, 1)` | One active (Waiting/InProgress) game per player as host; also serves "find my active game" |
| `uq_games_player_o_id_active` | games | `UNIQUE INDEX (player_o_id) WHERE status = 1` | One InProgress game per player as opponent; same lookup from the O side |
| `pk_moves` | moves | `PRIMARY KEY (id)` | Row identity |
| `uq_moves_game_id_position` | moves | `UNIQUE INDEX (game_id, position)` | A cell holds one mark per game — last line of defense against double moves; leading `game_id` also serves FK-support lookups, so no separate `ix_moves_game_id` is needed |
| `uq_moves_game_id_move_number` | moves | `UNIQUE INDEX (game_id, move_number)` | Preserves the move sequence; serves move-history reads ordered by `move_number` |
| `pk_player_profiles` | player_profiles | `PRIMARY KEY (id)` | Shared PK with `users.id`; serves profile-by-user lookups |
| `uq_player_profiles_username` | player_profiles | `UNIQUE INDEX (username)` | Exact-case username uniqueness; profile lookup by name |
| `pk_refresh_tokens` | refresh_tokens | `PRIMARY KEY (id)` | Row identity |
| `uq_refresh_tokens_token_hash` | refresh_tokens | `UNIQUE INDEX (token_hash)` | Point lookup when validating a presented refresh token |
| `ix_refresh_tokens_user_id` | refresh_tokens | `INDEX (user_id)` | FK support: fetch a user's active/revoked tokens at rotation and logout |
| `pk_users` + Identity indexes | users | `PRIMARY KEY (id)`; `asp_net_user_names` UNIQUE on `normalized_user_name`; `asp_net_users_email_index` on `normalized_email` | Framework-generated; case-insensitive username uniqueness and login-by-email lookup |

### 3.1 How EF sets up the partial unique indexes

```csharp
// GameConfiguration — one active game per player
builder.HasIndex(g => g.PlayerXId)
       .IsUnique()
       .HasDatabaseName("uq_games_player_x_id_active")
       .HasFilter("status IN (0, 1)");           // raw SQL predicate, provider-specific

builder.HasIndex(g => g.PlayerOId)
       .IsUnique()
       .HasDatabaseName("uq_games_player_o_id_active")
       .HasFilter("status = 1");

// Lobby index, descending on time
builder.HasIndex(g => new { g.Status, g.CreatedAtUtc })
       .IsDescending(false, true)
       .HasDatabaseName("ix_games_status_created_at_utc");
```

Two details worth knowing:

- **Why the O-side filter is only `status = 1`:** a Waiting game has `player_o_id = NULL` and only InProgress games have an opponent; the moment a game finishes, its row drops out of both partial indexes automatically, freeing both players — no index maintenance anywhere.
- **Predicate literals are enum values, not names:** the filter references the `smallint` values from the [enum mapping table](schema-and-erd.md#5-enum-mappings), which are pinned in code (`Waiting = 0`, `InProgress = 1`, ...).

## 4. Query Patterns

### 4.1 Lobby list (`GET /api/v1/games?status=waiting,inprogress`)

```sql
SELECT g.id, g.player_x_id, g.player_o_id, g.status, g.created_at_utc
FROM games AS g
WHERE g.status IN (0, 1)
ORDER BY g.created_at_utc DESC
LIMIT 100;
```

`ix_games_status_created_at_utc` turns the filter into an index scan over the (tiny) active set; the final merge/sort of two status buckets is trivial at this cardinality.

### 4.2 Active game for a player (`GET /api/v1/games/active`, join/create guards)

```sql
SELECT g.*
FROM games AS g
WHERE (g.player_x_id = @userId AND g.status IN (0, 1))
   OR (g.player_o_id = @userId AND g.status = 1);
```

Each arm is answered by one of the partial unique indexes (bitmap index scans) — at most one row can match in total.

### 4.3 Move history / board reconstruction (`GET /api/v1/games/{id}`)

```sql
SELECT m.move_number, m.position, m.mark, m.player_id, m.played_at_utc
FROM moves AS m
WHERE m.game_id = @gameId
ORDER BY m.move_number;
```

`uq_moves_game_id_move_number` delivers the rows already ordered; the board array is derived from these rows ([schema](schema-and-erd.md#9-board-is-derived-from-moves)).

### 4.4 Refresh token validation (`POST /api/v1/auth/refresh`)

```sql
SELECT r.* FROM refresh_tokens AS r WHERE r.token_hash = @hash;
-- then, on rotation:
SELECT r.* FROM refresh_tokens AS r WHERE r.user_id = @userId;  -- ix_refresh_tokens_user_id
```

## 5. Maintenance

- **Vacuum:** PostgreSQL autovacuum (defaults) is sufficient. `games` rows are updated at most ~11 times per game (join + up to 9 moves + finish), and moves are insert-only, so dead-tuple volume is trivial at target load. Watch `pg_stat_user_tables` (`n_dead_tup`, `last_autovacuum`) after launch; no manual `VACUUM` is planned.
- **Statistics:** auto-analyze keeps planner statistics fresh; no manual `ANALYZE` jobs.
- **No index tuning until measured:** expected sizes (hundreds of active games, ≤ 9 moves each) keep every table and index fully cached. Before adding or changing any index, capture `EXPLAIN (ANALYZE, BUFFERS)` for the offending query in [testing](../05-quality/testing-strategy.md) or production diagnostics; only then propose a change. No `REINDEX` is anticipated.
- **Health check:** the API's `/health` endpoint verifies DB connectivity (see [Docker & environments](../06-devops/docker-and-environments.md)).

## 6. Data Retention

| Data | Policy |
| --- | --- |
| `games` rows (Finished/Cancelled) | **Kept permanently** — FR-12: completed games persist for future history features. They leave the partial unique indexes on completion, so they never affect active-game checks, and the lobby query always filters on status, so history does not degrade it |
| `moves` rows | **Kept permanently** with their game (cascade only removes them if a game row were ever deleted, which no feature does) |
| `refresh_tokens` | Revoked/expired rows are retained for the reuse-detection audit trail; volume is trivial (≤ a few rows per user per week), so no purge job ships in MVP — revisit with the token-family expiry feature post-MVP |
| `player_profiles` / `users` | Never deleted in MVP (games FKs are `ON DELETE RESTRICT`, which protects history even if deletion is added later) |

## Assumptions

- The `dotnet-ef` global tool tracks the EF Core 10 major version (`10.*`); developers install or update it per the command above.
- Generated scripts land in `artifacts/migrations.sql` (git-ignored) and are applied by the pipeline, not committed to the repo.
- CI enforces `migrations has-pending-model-changes` on every pull request so model and migrations never drift.
- Integration tests apply real migrations (never `EnsureCreated`) and use Respawn for data-only resets.
- No index changes beyond the catalog above occur before launch; anything later must be measurement-driven per the maintenance rules.
