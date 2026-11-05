# V1 Scope — "Game Live" First

*Defines the first buildable version: authentication and a fully live, rules-complete game — and nothing else. Everything not listed as **in** is deferred, not deleted: the deep-dive specs remain the target design, and deferred features plug into the seams listed in §6 with no rework.*

Parent document: [PRD](PRD.md) · Related: [Roadmap](roadmap.md) · Build order: [AGENTS.md](../agents/AGENTS.md)

---

## 1. The Rule for V1

**Build only what is 100% required for two registered users to create/join a game and play it live, end to end — but build the rules and architecture completely.** The game's domain logic (aggregate, invariants, events, race-safety) is finished in V1 exactly as specified, so every later feature (presence, live lobby, auto-forfeit, ratings…) is *additive*, never a rewrite.

## 2. In Scope for V1 (the complete list)

### Authentication — complete, as specified

- Register (username/email/password per FR-01), login by email or username, logout, silent refresh (US-01, US-02, US-03).
- JWT access (15 min) + rotating hashed refresh tokens (7 days) with family revocation on reuse; lockout 5/5 min; `PlayerProfile` created on register. Full spec: [auth-identity-jwt](../02-backend/auth-identity-jwt.md).
- Login and Register pages (split layout, inline validation, show/hide password); `authInterceptor` with single-flight refresh.

### The live game — the whole point

- **Game domain complete:** `Game` aggregate with all five behaviors (`Create`, `Join`, `MakeMove`, `Forfeit`, `Cancel`), all five domain events, all six domain exceptions, every invariant — no stubs, no shortcuts ([clean-architecture-ddd](../01-architecture/clean-architecture-ddd.md)).
- **REST (V1 subset):** `POST /games` (create), `POST /games/{id}/join` (race-safe), `POST /games/{id}/moves`, `GET /games/{id}`, `GET /games/active`, `POST /games/{id}/cancel`, `POST /games/{id}/leave`. Concurrency exactly as specified: xmin + reload-once + partial unique indexes ([concurrency](../03-database/concurrency.md)).
- **Real-time (V1 subset):** `GameHub` at `/hubs/game` (`[Authorize]`), group `game-{id}` with validated `JoinGameRoom`/`LeaveGameRoom`, and exactly these server events: **`GameStarted`, `MoveMade`, `GameFinished`, `GameCancelled`** — dispatched post-commit via `IGameNotifier`. The opponent's moves and the result appear without a reload; that is what "live" means in V1.
- **Minimal lobby page:** one list of **Waiting** games fetched over REST (on page load, after your own actions, and via a Refresh button), Create Game button, Join button (disabled + "Full" when taken), Cancel for your own waiting game. No tabs, no live updates, no presence.
- **Game page:** 3×3 board with turn indicator, mark draw + winning-line animations, result modal (Victory / Defeat / Draw) → Back to Lobby, invalid-move shake, a simple Connected/Reconnecting badge, and an explicit **Leave (forfeit)** button — the escape hatch that replaces deferred auto-forfeit. Entering (or reloading) `/game/:id` fetches `GET /games/{id}` and rejoins the hub group — that is V1's whole reconnect story.
- **Persistence:** the full schema as specified, including `moves` history rows (FR-12 comes free with the design).
- **Tests for the shipped surface:** Domain unit (all rules, 8 win lines, race paths), Application unit, API integration (auth lifecycle, join race, full playthrough). DoD applies to everything built.

## 3. Deferred Until the Project Resumes (V1.1+)

Each item is fully specified today and *not built* in V1:

| Deferred feature | Spec lives in | Why it can wait |
| --- | --- | --- |
| Presence: `PresenceTracker`, online count, online players list, `PresenceChanged`, `GetOnlinePlayersQuery`, US-04 | [realtime-signalr](../02-backend/realtime-signalr.md) §presence | The game is live without knowing who else is online |
| Live lobby updates: `lobby` group, `GameCreated`/`GameUpdated`/`GameRemoved` broadcasts, animated list transitions, US-05 live part | [realtime-signalr](../02-backend/realtime-signalr.md) | V1's REST list + Refresh covers joinability |
| Disconnect grace & auto-forfeit: `DisconnectGraceService` (30 s timers), `OpponentConnection` events, US-10 | [realtime-signalr](../02-backend/realtime-signalr.md) §grace | Explicit Leave/forfeit button resolves abandoned games in V1 |
| Stale-Waiting sweep (15 min cancellation) | [realtime-signalr](../02-backend/realtime-signalr.md) | Hosts cancel manually in V1 |
| Sophisticated reconnect/resync policy (queue-aware resync, grace countdown UI) | [realtime-signalr](../02-backend/realtime-signalr.md), [angular-architecture](../04-frontend/angular-architecture.md) | Basic reload-and-rejoin covers page refresh |
| Lobby UI completeness: Open/In-Progress tabs, Online Players panel, avatar menu, empty-state illustrations, stagger/skeleton animations | [pages-and-flows](../04-frontend/pages-and-flows.md) | Minimal list is sufficient to find/join a game |
| Game UI polish beyond core: confetti burst, waiting-dots animation, hover previews beyond the specced basics | [animations](../04-frontend/animations.md) | Core draw/win-line animations already carry the feel |
| M6 hardening: rate limiting, security headers middleware, Playwright E2E suite, accessibility audit, load sanity | [security](../05-quality/security.md), [testing-strategy](../05-quality/testing-strategy.md) | Follows the feature work, as in the roadmap |

## 4. User Story Mapping

| Story | V1 status |
| --- | --- |
| US-01, US-02, US-03 (auth) | **In V1** |
| US-04 (online players/presence) | Deferred |
| US-05 (see games list) | **In V1, simplified** — REST-fetched Waiting list, no live updates |
| US-06 (create game) | **In V1** |
| US-07 (join, race-safe, locks) | **In V1** |
| US-08 (place mark, see opponent moves instantly) | **In V1** — instant via `game-{id}` events |
| US-09 (win/lose/draw with clear animation) | **In V1** — mark draw, win line, result modal |
| US-10 (disconnect → forfeit) | Deferred — explicit Leave forfeits in V1 |
| US-11 (return to lobby after game) | **In V1** |
| US-12 (cancel own waiting game) | **In V1** |
| US-13 (reconnect after refresh) | **In V1, simplified** — reload fetches state and rejoins the hub group |

## 5. Functional Requirement Mapping

| FR | V1 status |
| --- | --- |
| FR-01..FR-03 (auth) | **In V1** |
| FR-04 (presence) | Deferred |
| FR-05..FR-09 (create/join/rules/win/draw) | **In V1** — complete, no simplification |
| FR-10 (lobby real-time updates) | Deferred |
| FR-11 (disconnect grace → forfeit) | Deferred (manual forfeit via Leave instead) |
| FR-12 (persist completed games) | **In V1** — free with the schema |

## 6. Why Nothing Needs Rework Later (the seams)

The deferred features attach to interfaces that V1 already establishes:

| Seam already in V1 | Later feature that plugs in |
| --- | --- |
| `IGameNotifier` (only game-room sends used in V1) | Presence + lobby broadcasts add new methods/send targets |
| `INotificationHandler`s per domain event | `lobby`-group handlers for `GameCreated`/`GameUpdated`/`GameRemoved` |
| `ForfeitGameCommand` (invoked by the Leave button) | `DisconnectGraceService` invokes the same command from a timer |
| `CancelGameCommand` (host button) | Stale-Waiting sweep invokes the same command |
| Hub groups + `JoinGameRoom` validation | `lobby` group membership, `OpponentConnection` events |
| Full Clean Architecture / CQRS / Cortex.Mediator pipeline | Every new feature is new commands/queries/handlers |

If implementation reveals a seam that would force rework, that is a bug in this scope doc — fix the doc (and, if architectural, add an ADR) in the same change.

## 7. Milestone Mapping (build order unchanged, scope sliced)

| Milestone | V1 builds | V1 defers |
| --- | --- | --- |
| M0 Foundation | Everything (skeleton, Docker, CI, tokens, shell) | — |
| M1 Authentication | Everything | — |
| M2 Domain & Game API | Everything except `GetOnlinePlayersQuery` (needs presence) | `GetOnlinePlayersQuery` |
| M3 Real-time | Hub auth, `game-{id}` groups, `GameStarted`/`MoveMade`/`GameFinished`/`GameCancelled`, `SignalRGameNotifier` | `lobby` group, `PresenceTracker`, `PresenceChanged`, `OpponentConnection`, `DisconnectGraceService`, stale sweep |
| M4 Lobby UI | Minimal list page: Waiting games, Create, Join (Full state), Cancel, navbar with Logout | Tabs, Online Players panel, live updates, online pill, stagger/skeletons, illustrations |
| M5 Game UI | Board, turn indicator, mark-draw + win-line, result modal, shake, basic connection badge, Leave/forfeit, reload-rejoin | Grace countdown, confetti burst, advanced reconnect policy |
| M6 Hardening | — | All of it (rate limiting, headers, E2E, a11y, load) |

## Assumptions

- V1 is defined by this file; the [PRD](PRD.md) remains the product source of truth and its MVP (with presence, live lobby, auto-forfeit) is the **next** increment after V1 — nothing in the PRD is cancelled.
- The auth design ships whole in V1 (rotation + reuse detection + lockout) because a half-built token system is not "rules complete"; trimming it saves little and breaks the spec.
- Domain events and the `IGameNotifier` seam exist from day one even where V1 only uses part of them — that is what makes later features additive.
- "Live" in V1 means: opponent moves and results arrive over SignalR without a reload. It does *not* mean presence or live lobby lists.
- The deferred test suites (hub presence tests, grace-timer tests, E2E, load) are deferred **with** their features; coverage gates still apply to everything V1 ships.
