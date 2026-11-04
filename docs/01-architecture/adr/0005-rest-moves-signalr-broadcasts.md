# ADR-0005: Moves over REST, SignalR for push only

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Transport responsibilities for the write path vs the real-time path |

## Context

Players submit moves, joins, cancels and forfeits; both players plus the lobby must see resulting state changes instantly. Two transports are available once SignalR is in the stack for presence and lobby updates anyway: moves could be submitted as hub invocations, or as REST commands. The choice determines where validation, authorization, error semantics and test effort live. WebSockets are also unreliable on flaky mobile networks — exactly the audience US-10 protects with a grace period.

## Decision

**All state mutations travel over REST** (`POST /api/v1/games`, `/{id}/join`, `/{id}/moves`, `/{id}/cancel`, `/{id}/leave`); **SignalR (`/hubs/game`) is used for push only, not state mutation — the sole exception being presence** (connection/disconnection tracking, group joins):

- Moves: `POST /api/v1/games/{id}/moves` with `{ position }` → 200 `GameDto`, or RFC 7807 `ProblemDetails` (409 not your turn, 422 invalid). Validation (FluentValidation), authorization (participant check) and domain rules run in the same MediatR pipeline as every other command.
- Results of any command are broadcast by domain-event notification handlers through `IGameNotifier` to groups `lobby` and `game-{id}` (`GameStarted`, `MoveMade`, `GameFinished`, `GameRemoved`, `PresenceChanged`, `OpponentConnection`).
- Client→server hub methods are limited to presence/room concerns: `JoinGameRoom(gameId)` (validated server-side as a participant), `LeaveGameRoom(gameId)`; the connect/disconnect lifecycle updates presence and grace timers.
- The authoritative response to the mover is the REST `GameDto`; the SignalR echo is ignored by the actor's client (deduplicated by game version).

Channel responsibility split:

| Channel | Carries | Never carries |
| --- | --- | --- |
| REST `/api/v1` | all mutations (auth, create, join, moves, cancel, forfeit), all reads | server-initiated pushes |
| Hub `/hubs/game` (server→client) | game/lobby broadcasts, presence changes, opponent-connection state | state mutation requests |
| Hub (client→server) | `JoinGameRoom`, `LeaveGameRoom`, connect/disconnect lifecycle | moves, joins, cancels, forfeits |

## Consequences

**Positive**

- One write pipeline: validation, auth, `xmin` concurrency and unit-of-work semantics are identical for moves, joins and auth — tested once via WebApplicationFactory integration tests.
- Rich HTTP error semantics (status codes + ProblemDetails) map naturally onto the existing `Result` → HTTP factory; hub-invocation error handling is comparatively primitive.
- REST requests work through every proxy/network where WebSockets may fail; the write path degrades to polling if SignalR drops, since `GET /games/active` remains available for resync.
- The hub surface stays tiny and auditable — no authorization rules duplicated in two transports.
- Load testing and rate limiting (5 moves/s per user) operate on measurable HTTP endpoints.

**Negative**

- Two channels per move (request + broadcast) instead of one round trip — a few bytes of overhead per move, negligible against the 200 ms p95 budget.
- The mover receives its own action twice (HTTP response + group broadcast it also subscribes to); the client must deduplicate.
- WebSocket-only features like server push to a caller return value are unused; some SignalR capability is "wasted".

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Moves as hub invocations (`hub.invoke("MakeMove", …)`) | Single channel is elegant, but loses HTTP status semantics, FluentValidation-to-ProblemDetails mapping, uniform rate limiting, and forces move tests through SignalR client plumbing; duplicates the pipeline entry point |
| REST for moves + polling for updates (no hub) | Simpler, but lobby/presence real-time requirements (FR-04, FR-10) demand push anyway; polling 500 games wastes far more than it saves |
| Dual write paths (REST and hub) for flexibility | Two authorization/validation surfaces to test and keep consistent — an incident waiting to happen |
| Raw WebSockets instead of SignalR | Reimplements protocol negotiation, groups, automatic reconnect, and backplane story (needed for the planned Redis scale-out) |

## References

- [PRD](../../00-product/PRD.md) (FR-04, FR-08, FR-10, NFR Performance/Scalability)
- [Real-time & SignalR](../../02-backend/realtime-signalr.md) — full event contract
- [API spec](../../02-backend/api-spec.md)
- [System architecture](../system-architecture.md) §4.1 make-move round trip
- [ADR-0002 MediatR](0002-mediatr-for-cqrs.md) · [ADR-0008 Presence tracker](0008-in-memory-presence-tracker.md)

## Assumptions

- Clients treat the REST response as authoritative for their own actions and use broadcasts only for opponent/lobby changes plus their own deduplicated reconciliation.
- Presence (connection lifecycle, group membership) is considered transport bookkeeping, not domain state, hence the carve-out from "no mutation over the hub".
