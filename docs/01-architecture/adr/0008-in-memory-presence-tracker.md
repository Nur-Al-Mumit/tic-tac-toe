# ADR-0008: In-memory presence tracker (single node)

| Status | Accepted — 2026-11-04 |
| --- | --- |
| Scope | Online-player tracking, disconnect grace timers, scale-out coupling |

## Context

FR-04 requires the lobby to show who is online (count + list) and US-10 requires disconnect detection to drive 30-second forfeit timers. SignalR tells the server about connections, not users: one player may hold several connections (tabs/devices), and presence must be per-user. The MVP targets 500 concurrent games on a **single node**; Redis is planned but not built (PRD scalability NFR and risk table).

## Decision

Track presence **in-process** behind the `IPresenceTracker` interface (owned by Application):

- Implementation: singleton `PresenceTracker` holding a `ConcurrentDictionary<Guid, HashSet<string>>` mapping userId → set of SignalR connectionIds. Multiple tabs/devices = multiple connectionIds = **one user**; a user goes offline only when the **last** connection drops.
- `GameHub` lifecycle drives it: `OnConnectedAsync` registers the connection, adds the user to the `lobby` group, and (if the user has an active game) re-adds them to `game-{id}` and cancels any grace timer; `OnDisconnectedAsync` removes the connection, and when it was the last one for a player in an `InProgress` game, hands off to `DisconnectGraceService` to start the 30 s forfeit timer and emits `OpponentConnection(false)`.
- `GetOnlinePlayersQuery` reads the tracker snapshot; `PresenceChanged { onlineCount, players[] }` broadcasts to `lobby` on every presence transition.
- The same hosted service also cancels stale `Waiting` games older than 15 minutes.
- Scale-out precondition: because tracker and timers are per-process, **multi-instance deployment is not supported until** presence moves to Redis (tracker store + SignalR backplane together, as one migration step).

Interface surface (owned by Application, implemented in Infrastructure/Realtime):

| `IPresenceTracker` member | Purpose |
| --- | --- |
| `AddConnection(userId, connectionId)` | register a tab/socket; returns whether the user just came online |
| `RemoveConnection(userId, connectionId)` | unregister; returns whether the user went fully offline |
| `IsOnline(userId)` | single-user check (grace-timer decisions) |
| `GetOnlinePlayers()` | snapshot for `GetOnlinePlayersQuery` and `PresenceChanged` |

## Consequences

**Positive**

- Zero external dependencies: no Redis in the MVP compose file, no serialization overhead; reads are O(users) dictionary snapshots.
- Correct tab semantics for free: connection-set semantics naturally implement "multiple tabs = one user" (PRD open question #2).
- Interface placement (Application) + implementation (Infrastructure) means swapping to Redis later touches one class plus registration, with handlers, hub and queries unchanged.
- Grace timers live next to presence logic, keeping all disconnect-forfeit mechanics in one Infrastructure folder.

**Negative**

- Presence does not survive API restarts: all users appear offline (their sockets drop, triggering grace timers); clients auto-reconnect and re-register, so the window is minutes, not data loss — still, it is a visible blip.
- Hard single-node ceiling: running two API replicas would split the presence sets and misreport online players and forfeit timers — a deployment constraint that must be respected operationally.
- In-memory timers are lost on process recycle; a forfeit scheduled but not fired simply never happens (game resumes on reconnect) — acceptable, safe-side behavior.
- `HashSet` mutation needs lock-per-user discipline to avoid lost connection updates under concurrent connect/disconnect.

## Alternatives considered

| Alternative | Why rejected |
| --- | --- |
| Redis presence store from day one | Adds a service to compose, CI and every dev machine for a single-node MVP the PRD explicitly sizes; premature until scale-out is real |
| PostgreSQL-backed presence table | Durable and familiar, but connection churn (multiple per user, network flaps) would hammer the write path the game itself needs; wrong tool for ephemeral state |
| Count SignalR connections as users (no per-user aggregation) | One player with two tabs counts twice — violates FR-04's intent and PRD open question #2's resolution |
| Distributed cache (`IDistributedCache`) abstraction now | Same Redis dependency in disguise, with a worse API for set-style data (per-user connection sets) |

## References

- [PRD](../../00-product/PRD.md) (FR-04, US-10, §9 in-memory presence risk, future scope: Redis backplane)
- [Real-time & SignalR](../../02-backend/realtime-signalr.md)
- [System architecture](../system-architecture.md) §4.3 disconnect/forfeit sequence, §7 scalability notes
- [ADR-0005 REST moves / SignalR push](0005-rest-moves-signalr-broadcasts.md) — hub-for-push carve-out including presence

## Assumptions

- A user is "online" iff they hold at least one live hub connection; REST-only activity without a hub connection does not count.
- The Redis migration (tracker + backplane + durable timers) is one coordinated change evaluated together, not three independent swaps.
