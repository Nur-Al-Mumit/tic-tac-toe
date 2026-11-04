# REST API Specification

*The canonical contract of the Multiplayer Tic-Tac-Toe REST API — conventions, per-endpoint reference with validation rules and JSON examples, DTO schemas, the error-code catalog, OpenAPI tooling, versioning and pagination.*

Parent document: [PRD](../00-product/PRD.md) · Related: [Real-time & SignalR](realtime-signalr.md) · [Auth, Identity & JWT](auth-identity-jwt.md) · [Coding standards](coding-standards.md) · [CQRS & Mediator](../01-architecture/cqrs-and-mediator.md) · [Security](../05-quality/security.md) · [Testing strategy](../05-quality/testing-strategy.md)

---

## 1. Conventions

### 1.1 Basics

| Rule | Value |
| --- | --- |
| Base URL | `/api/v1` (served behind HTTPS; nginx reverse-proxies `/api/v1` to the API — see [system architecture](../01-architecture/system-architecture.md#6-deployment-view-summary)) |
| Format | JSON only, UTF-8; request/response bodies `Content-Type: application/json` |
| Casing | camelCase for all JSON property names (System.Text.Json `JsonNamingPolicy.CamelCase`) |
| Dates | ISO 8601 UTC with `Z` suffix and `Utc`-suffixed property names, e.g. `"2026-11-04T14:20:31Z"` |
| Identifiers | GUIDs serialized as strings, e.g. `"3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01"` |
| Enums | Serialized as PascalCase strings (see [Enum values](#33-enum-values)) |
| Methods | `GET` for reads, `POST` for every command (mutations are not idempotent-safe PUT/DELETE in this API) |
| Empty-body commands | `POST /games`, `POST /games/{id}/join`, `POST /games/{id}/cancel`, `POST /games/{id}/leave` send no request body |
| Correlation | Every response carries `X-Correlation-Id`; an incoming `X-Correlation-Id` header is honored and propagated (see [system architecture §5](../01-architecture/system-architecture.md#5-cross-cutting-concerns)) |

### 1.2 Authentication

All endpoints require a valid Bearer access token **except** `POST /auth/register`, `POST /auth/login` and `POST /auth/refresh` (plus the non-API `/health` endpoint). Requests to protected endpoints without a valid token receive `401` with `errorCode: "Auth.Unauthorized"` (expired or malformed tokens behave identically — no reason detail is leaked). Token issuance, lifetimes and refresh rotation: [auth-identity-jwt](auth-identity-jwt.md).

```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 1.3 Error format — RFC 7807 ProblemDetails

Every non-2xx response is an RFC 7807 `ProblemDetails` object with `Content-Type: application/problem+json`, produced by a single factory in the Api layer (see [coding standards §3](coding-standards.md#3-error-handling)). Beyond the standard members, the API adds two extensions:

| Member | Kind | Meaning |
| --- | --- | --- |
| `errorCode` | extension (`string`) | Stable, machine-readable, dot-namespaced code from the [catalog](#14-error-code-catalog) — the key the Angular client maps messages by |
| `correlationId` | extension (`string`) | The request's correlation ID (same value as the `X-Correlation-Id` header) |
| `errors` | standard (ValidationProblemDetails) | Present only on 400 validation failures: object mapping field name → array of human-readable messages |

Example — expected domain failure (409, lost the join race, US-07):

```json
{
  "type": "https://tictacttoe.example/errors/Games.Full",
  "title": "Game is already full",
  "status": 409,
  "detail": "Two players have already joined this game.",
  "errorCode": "Games.Full",
  "correlationId": "0c1e2b7a-4f6d-4a70-9c8e-5d3b2a1f0e9d"
}
```

Example — request-shape validation failure (400):

```json
{
  "type": "https://tictacttoe.example/errors/Validation.Failure",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "The request body failed validation.",
  "errorCode": "Validation.Failure",
  "correlationId": "0c1e2b7a-4f6d-4a70-9c8e-5d3b2a1f0e9d",
  "errors": {
    "username": [ "Username must be 3-20 characters: letters, digits, underscore." ],
    "password": [ "Password must contain at least one digit." ]
  }
}
```

`type` is always `https://tictacttoe.example/errors/{errorCode}` — a stable identifier, not a served page. `title` is a short constant per code; `detail` is a developer-facing English sentence that may vary; the client must key off `errorCode`, never off `title`/`detail` text (see [ADR-0009](../01-architecture/adr/0009-result-pattern-expected-failures.md)).

### 1.4 Error code catalog

This table is the **canonical registry** of error codes (per [user-stories](../00-product/user-stories.md#assumptions), exact code strings are finalized in this file). New codes must be added here and to the client's message map in the same change.

| Code | HTTP | Emitted by | Meaning |
| --- | --- | --- | --- |
| `Validation.Failure` | 400 | All endpoints | Request shape/rules failed FluentValidation (`ValidationBehavior`); per-field `errors` dictionary present |
| `Auth.Unauthorized` | 401 | Protected endpoints | Missing, malformed, invalid or expired Bearer token |
| `Auth.InvalidCredentials` | 401 | `POST /auth/login` | Unknown identifier or wrong password — identical body either way (no user enumeration) |
| `Auth.LockedOut` | 423 | `POST /auth/login` | Account locked after 5 failed attempts; retry after the 5-minute lockout window |
| `Auth.InvalidRefreshToken` | 401 | `POST /auth/refresh` | Refresh token unknown, expired, or revoked (including revoked-by-reuse-detection families) |
| `Users.UsernameTaken` | 409 | `POST /auth/register` | Username already registered (case-insensitively) |
| `Users.EmailTaken` | 409 | `POST /auth/register` | Email already registered |
| `Games.NotFound` | 404 | `GET /games/{id}`, game commands | Unknown game id, or a game the caller is not a participant of (IDOR-safe 404 — see [security §3.7](../05-quality/security.md#37-authorization-and-anti-idor)) |
| `Games.Busy` | 409 | `POST /games`, `POST /games/{id}/join` | Caller already has an active (Waiting/InProgress) game — FR-07 |
| `Games.Full` | 409 | `POST /games/{id}/join` | Game already has two players (InProgress), or is Finished — includes the join-race loser |
| `Games.CannotJoinOwnGame` | 409 | `POST /games/{id}/join` | Host attempted to join their own game |
| `Games.NotInProgress` | 409 | join / moves / cancel / leave | Operation requires a different status: move on a Waiting/Finished/Cancelled game; cancel of a non-Waiting game; leave of a non-InProgress game; join of a Cancelled game |
| `Games.NotYourTurn` | 409 | `POST /games/{id}/moves` | It is the opponent's turn (includes O moving first — X always starts) |
| `Games.InvalidPosition` | 422 | `POST /games/{id}/moves` | `position` outside 0–8 |
| `Games.CellOccupied` | 422 | `POST /games/{id}/moves` | Target cell already carries a mark |
| `Games.Forbidden` | 403 | moves / leave | Caller is not a participant of this game |
| `Games.NotHost` | 403 | `POST /games/{id}/cancel` | Only the host (Player X) may cancel |
| `Games.ConcurrencyConflict` | 409 | game commands | Optimistic-concurrency conflict persisted after the reload-once retry (safe to re-fetch state and retry — see [concurrency](../03-database/concurrency.md)) |
| `RateLimit.Exceeded` | 429 | rate-limited endpoints | Rate limit breached; `Retry-After` header present |

Unexpected failures (database down, bugs) return `500` ProblemDetails with `errorCode` omitted and no internal detail leaked; they never carry the codes above.

### 1.5 Status code usage

| Status | Used for |
| --- | --- |
| 200 | Successful command returning the updated resource (join, move); successful queries; login; refresh |
| 201 | Resource created (register, create game) — includes a `Location` header where applicable |
| 204 | Successful command with no body (logout, cancel, leave); "no active game" on `GET /games/active` |
| 400 | Malformed JSON, wrong types, missing fields, rule failures caught by validators |
| 401 | Missing/invalid token; bad credentials; invalid refresh token |
| 403 | Authenticated but not allowed (non-participant, non-host) |
| 404 | Unknown resource — or resource existence not disclosed to the caller |
| 409 | Conflict with current state (full game, busy player, out of turn, concurrency) |
| 422 | Well-formed request that violates a game rule (position range, occupied cell) |
| 423 | Account locked |
| 429 | Rate limit exceeded (`Retry-After` header, seconds) |
| 500 | Unexpected server error (never carries a domain code) |

### 1.6 Rate limits

Enforced by the .NET rate-limiting middleware ([security §3.8](../05-quality/security.md#38-abuse-and-denial-of-service-controls)); breaches return `429` with `errorCode: "RateLimit.Exceeded"` and a `Retry-After` header.

| Policy | Endpoints | Partition | Limit |
| --- | --- | --- | --- |
| `auth` | `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh` | client IP | fixed window, 10 requests/minute |
| `moves` | `POST /games/{id}/moves` | authenticated user id | token bucket, 5 requests/second |
| global backstop | all others | — | generous default limiter (configuration) |

---

## 2. Endpoints

Route templates use literal `api/v1` prefixes; `{id}` is constrained to GUID format, so `/games/active` never collides with `/games/{id}`.

### 2.1 `POST /auth/register` — create account

Creates the Identity user, a `PlayerProfile` and the initial refresh token in one transaction, and returns a first token pair (auto-login).

- **Auth:** anonymous · **Rate limit:** `auth` (10/min/IP)

Request fields:

| Field | Type | Rules |
| --- | --- | --- |
| `username` | string | required; 3–20 chars; `^[A-Za-z0-9_]+$`; unique (case-insensitive) |
| `email` | string | required; valid email format; ≤ 256 chars; unique |
| `password` | string | required; ≥ 8 chars; at least one upper-case, one lower-case, one digit |

```json
{ "username": "sara_01", "email": "sara@example.com", "password": "Str0ngPass" }
```

- **201** — `AuthResponse` (see [§3.1](#31-authresponse)); `Location: /api/v1/auth/me`. The `user` reflects the newly created account.

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzZjlkNmMyZS04YTQxLTRiN2UtOWMyZC0xZjVhOGU2YjRjMDEiLCJuYW1lIjoic2FyYV8wMSIsImVtYWlsIjoic2FyYUBleGFtcGxlLmNvbSIsImp0aSI6IjExZjBhN2JjLTI0ZDctNDdmZi04MzEwLTVhYmNkZWYwMTIzNCIsImlhdCI6MTc5MTIwMDAwMH0.x7Q...",
  "refreshToken": "dGhpc2lzYTI1NmJpdHJhbmRvbXZhbHVlX19fX19fX19fX19fX19fX19f",
  "expiresAtUtc": "2026-11-04T14:35:31Z",
  "user": { "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01", "email": "sara@example.com" }
}
```

- **Errors:** `400 Validation.Failure` · `409 Users.UsernameTaken` · `409 Users.EmailTaken` · `429 RateLimit.Exceeded`

### 2.2 `POST /auth/login` — log in

- **Auth:** anonymous · **Rate limit:** `auth` (10/min/IP)

| Field | Type | Rules |
| --- | --- | --- |
| `identifier` | string | required; the account's email **or** username |
| `password` | string | required |

```json
{ "identifier": "sara_01", "password": "Str0ngPass" }
```

- **200** — `AuthResponse` (same shape as register; `expiresAtUtc` is 15 minutes ahead).

- **Errors:** `400 Validation.Failure` · `401 Auth.InvalidCredentials` (identical body for unknown identifier and wrong password) · `423 Auth.LockedOut` (after 5 consecutive failures; window is 5 minutes) · `429 RateLimit.Exceeded`

Behavior notes: lookup is by normalized email or normalized username; failure increments the lockout counter; success resets it. Details: [auth-identity-jwt §5](auth-identity-jwt.md#5-login-by-email-or-username).

### 2.3 `POST /auth/refresh` — rotate tokens

- **Auth:** anonymous (the refresh token itself is the credential) · **Rate limit:** `auth` (10/min/IP)

| Field | Type | Rules |
| --- | --- | --- |
| `refreshToken` | string | required; the opaque refresh token string |

```json
{ "refreshToken": "dGhpc2lzYTI1NmJpdHJhbmRvbXZhbHVlX19fX19fX19fX19fX19fX19f" }
```

- **200** — new `AuthResponse`. The presented token is **revoked** and replaced: the response's `refreshToken` is its successor; the old one is unusable from this moment (rotation, [auth-identity-jwt §7](auth-identity-jwt.md#7-refresh-tokens-rotation--reuse-detection)).

- **Errors:** `400 Validation.Failure` · `401 Auth.InvalidRefreshToken` (unknown, expired, or already revoked — including families revoked by reuse detection) · `429 RateLimit.Exceeded`

Presenting a revoked token additionally revokes the entire token family (reuse detection), so the legitimate newest descendant also starts returning 401.

### 2.4 `POST /auth/logout` — revoke session

- **Auth:** Bearer required · **Rate limit:** global backstop

| Field | Type | Rules |
| --- | --- | --- |
| `refreshToken` | string | required; the token this device currently holds |

- **204** — no body. The presented refresh token and its lineage are revoked server-side; the client then clears both tokens and stops the SignalR connection.

- **Errors:** `401 Auth.Unauthorized` · `400 Validation.Failure` (missing body field). Revoking an already-revoked token is a no-op that still returns 204 (idempotent).

### 2.5 `GET /auth/me` — current user

- **Auth:** Bearer required

- **200** — `UserDto`:

```json
{ "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01", "email": "sara@example.com" }
```

- **Errors:** `401 Auth.Unauthorized`

### 2.6 `GET /players/online` — online players

Returns one entry per currently online user (multiple tabs/devices count once). Sourced from the presence tracker, not the database ([ADR-0008](../01-architecture/adr/0008-in-memory-presence-tracker.md)).

- **Auth:** Bearer required

- **200** — array of `OnlinePlayerDto` (never contains emails):

```json
[
  { "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01" },
  { "id": "7c2e9a4f-6d3b-4f8a-b1e5-9d0c7a3f5e02", "username": "rafi_99" }
]
```

- **Errors:** `401 Auth.Unauthorized`. Live updates arrive via the `PresenceChanged` hub event ([realtime-signalr §5](realtime-signalr.md#5-server-to-client-events)) — poll this endpoint only for the initial snapshot or a resync.

### 2.7 `GET /games` — lobby list

Returns lobby-visible games (Waiting and/or InProgress), newest first, capped — see [§6 Pagination](#6-pagination-notes-lobby-list).

- **Auth:** Bearer required

Query parameters:

| Param | Rules |
| --- | --- |
| `status` | optional; comma-separated subset of `waiting`, `inprogress` (case-insensitive); default `waiting,inprogress`; any other value → `400 Validation.Failure` |

```
GET /api/v1/games?status=waiting,inprogress
```

- **200** — array of `LobbyGameDto`:

```json
[
  {
    "id": "a58c31e0-2f4b-4c9a-8e7d-6b3f1d9c4a03",
    "status": "Waiting",
    "playerX": { "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01" },
    "playerO": null,
    "createdAtUtc": "2026-11-04T14:20:31Z"
  },
  {
    "id": "c91d4e6a-7b0f-4d2e-a5c8-3e1f8b7a2d04",
    "status": "InProgress",
    "playerX": { "id": "5ab8f0d1-9c3e-4a6b-8d2f-7e4c1b9a5e05", "username": "mina_x" },
    "playerO": { "id": "2d6e8a1c-4f7b-4e9d-b3a5-8c1f6e2d9a06", "username": "tanvir_o" },
    "createdAtUtc": "2026-11-04T14:12:07Z"
  }
]
```

- **Errors:** `401 Auth.Unauthorized` · `400 Validation.Failure` (bad `status` value)

### 2.8 `POST /games` — create game

Creates a Waiting game with the caller as Player X (FR-05). No request body.

- **Auth:** Bearer required

- **201** — `GameDto`; `Location: /api/v1/games/{id}`:

```json
{
  "id": "a58c31e0-2f4b-4c9a-8e7d-6b3f1d9c4a03",
  "status": "Waiting",
  "playerX": { "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01" },
  "playerO": null,
  "board": [null, null, null, null, null, null, null, null, null],
  "currentTurn": "X",
  "result": null,
  "createdAtUtc": "2026-11-04T14:20:31Z"
}
```

- **Errors:** `401 Auth.Unauthorized` · `409 Games.Busy` (caller already in an active game — FR-07). Other lobby users learn about the game via the `GameCreated` hub event.

### 2.9 `GET /games/{id}` — game detail

- **Auth:** Bearer required · `{id}` must be a GUID.

- **200** — full `GameDto` (participants only; the example shows a finished game with a result):

```json
{
  "id": "a58c31e0-2f4b-4c9a-8e7d-6b3f1d9c4a03",
  "status": "Finished",
  "playerX": { "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01" },
  "playerO": { "id": "7c2e9a4f-6d3b-4f8a-b1e5-9d0c7a3f5e02", "username": "rafi_99" },
  "board": ["X", null, "O", null, "X", null, "O", null, "X"],
  "currentTurn": "X",
  "result": { "outcome": "XWins", "reason": "Normal", "winningLine": [0, 4, 8] },
  "createdAtUtc": "2026-11-04T14:20:31Z"
}
```

- **Errors:** `401 Auth.Unauthorized` · `404 Games.NotFound` — unknown id **or** caller is not a participant (spectators are a non-goal; outsiders get 404, not 403, to avoid disclosing existence).

### 2.10 `GET /games/active` — my active game (resync)

Returns the caller's Waiting or InProgress game if one exists — the canonical resync source after page refresh or hub reconnect (US-13). No request body.

- **Auth:** Bearer required

- **200** — `GameDto` of the active game · **204** — no active game (empty body).

- **Errors:** `401 Auth.Unauthorized`

### 2.11 `POST /games/{id}/join` — join a waiting game

Sets the caller as Player O and moves the game to InProgress (FR-06). Exactly one of two simultaneous joiners wins (xmin-guarded — see [concurrency](../03-database/concurrency.md)). No request body.

- **Auth:** Bearer required

- **200** — `GameDto` with `status: "InProgress"`, `playerO` set:

```json
{
  "id": "a58c31e0-2f4b-4c9a-8e7d-6b3f1d9c4a03",
  "status": "InProgress",
  "playerX": { "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01" },
  "playerO": { "id": "7c2e9a4f-6d3b-4f8a-b1e5-9d0c7a3f5e02", "username": "rafi_99" },
  "board": [null, null, null, null, null, null, null, null, null],
  "currentTurn": "X",
  "result": null,
  "createdAtUtc": "2026-11-04T14:20:31Z"
}
```

Both players are notified through the `GameStarted` hub event; the lobby receives `GameUpdated` ([realtime-signalr §5.4](realtime-signalr.md#54-domain-event-to-broadcast-mapping)).

- **Errors:** `401` · `404 Games.NotFound` · `409 Games.Full` (game already two-players/Finished — the join-race loser) · `409 Games.CannotJoinOwnGame` · `409 Games.Busy` (caller already in an active game) · `409 Games.NotInProgress` (game was cancelled, e.g. by the stale sweep) · `409 Games.ConcurrencyConflict`

### 2.12 `POST /games/{id}/moves` — make a move

Appends the caller's mark at `position` (FR-08/FR-09). The server validates participant, turn, cell bounds, cell emptiness and status; the authoritative post-move state is the response, and the opponent receives the `MoveMade` broadcast.

- **Auth:** Bearer required · **Rate limit:** `moves` (5/s per user)

| Field | Type | Rules |
| --- | --- | --- |
| `position` | integer | required; must deserialize as a JSON number (non-integer/missing → 400); domain rule **0–8** → 422 |

```json
{ "position": 4 }
```

Validation split: FluentValidation rejects malformed *shape* (missing/non-numeric `position`) with `400 Validation.Failure`; the semantic range rule (0–8) is the aggregate's authority and surfaces as `422 Games.InvalidPosition` (see [coding standards §3](coding-standards.md#3-error-handling)).

- **200** — updated `GameDto` (example: the winning move, diagonal 0-4-8):

```json
{
  "id": "a58c31e0-2f4b-4c9a-8e7d-6b3f1d9c4a03",
  "status": "Finished",
  "playerX": { "id": "3f9d6c2e-8a41-4b7e-9c2d-1f5a8e6b4c01", "username": "sara_01" },
  "playerO": { "id": "7c2e9a4f-6d3b-4f8a-b1e5-9d0c7a3f5e02", "username": "rafi_99" },
  "board": ["X", null, "O", null, "X", null, "O", null, "X"],
  "currentTurn": "X",
  "result": { "outcome": "XWins", "reason": "Normal", "winningLine": [0, 4, 8] },
  "createdAtUtc": "2026-11-04T14:20:31Z"
}
```

- **Errors:** `400 Validation.Failure` · `401` · `403 Games.Forbidden` (not a participant) · `404 Games.NotFound` · `409 Games.NotYourTurn` · `409 Games.NotInProgress` (waiting/finished/cancelled) · `409 Games.ConcurrencyConflict` · `422 Games.InvalidPosition` · `422 Games.CellOccupied` · `429 RateLimit.Exceeded`

### 2.13 `POST /games/{id}/cancel` — cancel my waiting game

Host-only cancellation of a Waiting game (US-12). No request body. Also used by the background sweep for stale games (> 15 min) — that path is internal and never surfaced to the host's client as their own action.

- **Auth:** Bearer required

- **204** — no body; the lobby receives `GameRemoved`.

- **Errors:** `401` · `403 Games.NotHost` (caller is not Player X) · `404 Games.NotFound` · `409 Games.NotInProgress` (game is InProgress, Finished or Cancelled — leaving an InProgress game is `/leave`, not `/cancel`)

### 2.14 `POST /games/{id}/leave` — leave / forfeit an in-progress game

The calling player forfeits; the opponent wins with `reason: "Forfeit"` (US-10, the explicit-leave sibling of the disconnect grace path). No request body.

- **Auth:** Bearer required

- **204** — no body; both players receive the `GameFinished` broadcast, the lobby `GameRemoved`.

- **Errors:** `401` · `403 Games.Forbidden` (not a participant) · `404 Games.NotFound` · `409 Games.NotInProgress` (game is Waiting — cancel it instead — or already terminal)

---

## 3. DTO Schemas

All DTOs are immutable C# records in the Application layer, mapped with Mapster ([ADR-0010](../01-architecture/adr/0010-mapster-mapping.md)); the JSON below is the wire truth.

### 3.1 `AuthResponse`

Returned by register, login and refresh.

| Field | Type | Notes |
| --- | --- | --- |
| `accessToken` | string | Signed JWT (HS-256), 15-minute lifetime; claims `sub`, `name`, `email`, `jti` |
| `refreshToken` | string | Opaque 256-bit random value (base64url); shown once — only its SHA-256 hash is stored server-side |
| `expiresAtUtc` | string | Access-token expiry, ISO 8601 UTC — lets the client schedule silent refresh |
| `user` | `UserDto` | The authenticated account |

### 3.2 `UserDto` / `PlayerRef`

`UserDto` (auth responses, `/auth/me`): `id`, `username`, `email`.
`PlayerRef` (embedded players in game DTOs): `id`, `username` — never the email.

### 3.3 Enum values

| JSON value | Domain enum | Where |
| --- | --- | --- |
| `"Waiting"` / `"InProgress"` / `"Finished"` / `"Cancelled"` | `GameStatus` | `GameDto.status`, `LobbyGameDto.status` |
| `"X"` / `"O"` | `Mark` | `board` cells, `currentTurn` |
| `"XWins"` / `"OWins"` / `"Draw"` | `Outcome` | `result.outcome` |
| `"Normal"` / `"Forfeit"` | `Reason` | `result.reason` |

The `status` **query parameter** is matched case-insensitively, so `waiting,inprogress` (lowercase, as issued by the SPA) binds to `Waiting,InProgress`.

### 3.4 `GameDto`

The authoritative game state; the same shape is carried by the `GameStarted`, `MoveMade` and `GameFinished` hub events, so one client model serves REST and SignalR.

| Field | Type | Notes |
| --- | --- | --- |
| `id` | string (GUID) | Game id; also the SignalR group name `game-{id}` |
| `status` | string | See [§3.3](#33-enum-values) |
| `playerX` | `PlayerRef` | Creator, always X |
| `playerO` | `PlayerRef \| null` | Joiner, always O; `null` while Waiting |
| `board` | array of 9 · `"X" \| "O" \| null` | Row-major, derived from persisted moves at query time (no stored board — see [schema §9](../03-database/schema-and-erd.md#9-board-is-derived-from-moves)) |
| `currentTurn` | `"X" \| "O"` | Mark allowed to move next; frozen on the final mark once Finished |
| `result` | `ResultDto \| null` | `null` until the game finishes |
| `createdAtUtc` | string | ISO 8601 UTC |

`ResultDto`: `outcome` (`"XWins"|"OWins"|"Draw"`), `reason` (`"Normal"|"Forfeit"`), `winningLine` (array of 3 cell indexes or `null` — `null` on draw and on forfeits, which end without a completed line).

Board indexing (row-major):

```text
0 | 1 | 2
3 | 4 | 5
6 | 7 | 8
```

### 3.5 `LobbyGameDto`

Summary projection for lists and lobby broadcasts — `GameDto` minus `board`, `currentTurn`, `result`: `id`, `status`, `playerX`, `playerO`, `createdAtUtc`.

### 3.6 `OnlinePlayerDto`

`id`, `username` — sourced from the presence tracker; deliberately excludes email.

---

## 4. OpenAPI Tooling

API documentation is generated by **.NET's built-in OpenAPI support** — no Swashbuckle ([ADR-0011](../01-architecture/adr/0011-latest-stable-stack.md)):

| Piece | Design |
| --- | --- |
| Document generation | `Microsoft.AspNetCore.OpenApi` — `builder.Services.AddOpenApi()` + `app.MapOpenApi()`; the document is served at `/openapi/v1.json`. Because DTOs are records and controllers carry `[ProducesResponseType]` attributes for every code in §2, request/response/error schemas are derived from the code itself — the spec above and the generated document cannot drift apart in shape |
| Interactive UI | **Scalar UI** (`Scalar.AspNetCore` package) — `app.MapScalarApiReference()` in **Development** at `/scalar/v1`, wired to the built-in document; supports the Bearer scheme so a developer can log in via `/auth/login` and exercise protected endpoints |
| Production | The document endpoint stays available in all environments but requires authentication in production (see [system architecture §5](../01-architecture/system-architecture.md#5-cross-cutting-concerns)) |
| Error schemas | The single ProblemDetails factory means every endpoint's error responses document as RFC 7807 bodies; `errorCode` values appear in endpoint descriptions |
| Build-time (optional) | `Microsoft.Extensions.ApiDescription.Server` can emit the document at build time as a CI artifact for diffing contract changes in pull requests |

## 5. API Versioning Strategy

| Rule | Detail |
| --- | --- |
| Version in URL | Major version is a literal path segment: `/api/v1/...` (set once in controller route templates). No header/query versioning |
| v1 is frozen at the contracts in this file | No breaking changes within v1 — ever |
| Additive changes allowed | New optional request fields, new endpoints, new response fields (clients ignore unknown fields), new enum values only where this spec declares the set open |
| Breaking changes | Removed/renamed fields, changed types, changed status codes/error codes, tightened validation → ship as `/api/v2` alongside v1; v1 is maintained for a deprecation window announced in release notes, after which it returns `410 Gone` |
| Client coupling | The Angular client depends on `errorCode` strings and DTO shapes, not on `title`/`detail` prose — prose may be reworded freely within v1 |

## 6. Pagination Notes (Lobby List)

The lobby list is **unpaginated in the MVP**: `GET /games` returns at most the **200 newest** matching games ordered by `createdAtUtc DESC` (served by the `ix_games_status_created_at_utc` index — see [migrations-and-indexes](../03-database/migrations-and-indexes.md)). Rationale: the PRD's 500-concurrent-games target keeps the live lobby far below any cap that would matter, and stale Waiting games are swept after 15 minutes, so the list is naturally bounded. Responses are plain JSON arrays — no envelope.

Future: when volume warrants it, cursor (keyset) paging will be added — `?limit=&cursor=` with `cursor` encoding `(createdAtUtc, id)` and responses gaining a `{ items, nextCursor }` envelope. That envelope change is breaking, so it will land behind `/api/v2/games` (or an explicitly versioned query mode) rather than mutating v1.

## Assumptions

- This file is the canonical registry of error-code strings; if any other document shows a variant spelling of a code, the namespaced codes in [§1.4](#14-error-code-catalog) bind, and the Angular client keys its message catalog on the code (or its suffix after the final dot) — never on prose.
- `type` URLs use the illustrative host `tictacttoe.example`; like the CORS origin examples in [security](../05-quality/security.md#35-cors), the real identifier is environment configuration and does not need to resolve.
- Enum wire values are PascalCase (`"InProgress"`) via `JsonStringEnumConverter`; the lowercase `status` query values bind case-insensitively, matching the SPA's issued requests.
- `expiresAtUtc` refers to the **access** token's expiry (15 minutes out); the refresh token's 7-day expiry is not surfaced — rotation failures teach the client it expired.
- The 200-item lobby cap and the 128 KB global body limit are configuration-backed defaults (`Lobby__MaxListSize`, request-size limits) and may be tuned without a contract change.
- `POST /auth/logout` requires the body's `refreshToken` to belong to the authenticated user; a token belonging to another user is treated as unknown and still yields 204 (no enumeration).
